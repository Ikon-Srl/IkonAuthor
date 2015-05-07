/*
 * 
 * Intranet Ikon
 * 
 * Copyright (C) 2012 Ikon Srl
 * Tutti i Diritti Riservati. All Rights Reserved
 * 
 */


using System;
using System.Collections.Specialized;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Web;
using System.Web.Configuration;
using System.Web.Security;
using System.Configuration;
using System.Reflection;
using System.Runtime.Serialization;
using System.Net;
using Newtonsoft.Json;
using LinqKit;

using Ikon;
using Ikon.GD;


namespace Ikon.SSO
{


  public static class SSO_Manager
  {
    private static object _lock = new object();
    //
    private static Ikon.Auth.Roles_IKGD _ProviderRoles = null;
    private static MembershipProvider _ProviderMembership = null;
    private static List<MethodInfo> _ProviderRolesMethods = null;
    private static List<MethodInfo> _ProviderMembershipMethods = null;
    //


    static SSO_Manager()
    {
      try
      {
        _ProviderRoles = Ikon.Auth.Roles_IKGD.Provider;
        _ProviderRolesMethods = _ProviderRoles.GetType().GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.InvokeMethod).ToList();
      }
      catch { }
      try
      {
        _ProviderMembership = Membership.Provider;
        _ProviderMembershipMethods = _ProviderMembership.GetType().GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.InvokeMethod).ToList();
      }
      catch { }
    }



    //
    // consente di verificare la validità di un token
    public static bool VerifyToken(string token)
    {
      try
      {
        FormsAuthenticationTicket tk = FormsAuthentication.Decrypt(token);
        if (!tk.Expired)
          return true;
      }
      catch { }
      return false;
    }


    public static string GetToken(string UserName)
    {
      string token = null;
      try
      {
        bool usePersistentCookie = Utility.TryParse<bool>(IKGD_Config.AppSettings["SSO_UsePersistentCookie"], true);
        int timeoutCookie = Utility.TryParse<int>(IKGD_Config.AppSettings["SSO_TimeoutCookie"], 3600);
        FormsAuthenticationTicket ticket = new FormsAuthenticationTicket(2, UserName, DateTime.Now, DateTime.Now.AddSeconds(timeoutCookie), true, string.Empty, "/");
        token = FormsAuthentication.Encrypt(ticket);
      }
      catch { }
      return token;
    }


    //
    // consente l'autenticazione dell'utente e ritorna il token, analogamente alle funzionalità offerte dalla pagina di login. 
    // Può essere utilizzata per verificare le credenziali di un utente da javascript o per applicazioni non Web.
    public static string AuthenticateUser(string UserName, string Password)
    {
      string token = null;
      try
      {
        if (System.Web.Security.Membership.ValidateUser(UserName, Password))
        {
          bool usePersistentCookie = Utility.TryParse<bool>(IKGD_Config.AppSettings["SSO_UsePersistentCookie"], true);
          int timeoutCookie = Utility.TryParse<int>(IKGD_Config.AppSettings["SSO_TimeoutCookie"], 3600);
          FormsAuthenticationTicket ticket = new FormsAuthenticationTicket(2, UserName, DateTime.Now, DateTime.Now.AddSeconds(timeoutCookie), true, string.Empty, "/");
          token = FormsAuthentication.Encrypt(ticket);
        }
      }
      catch { }
      return token;
    }

    //
    // fornisce lo username dell'utente
    public static string GetUserName(string token)
    {
      try
      {
        FormsAuthenticationTicket tk = FormsAuthentication.Decrypt(token);
        if (!tk.Expired)
        {
          return tk.Name;
        }
      }
      catch { }
      return null;
    }

    //
    // fornisce le informazioni principali riguardanti l'utente corrente: UserName, UserId, Email, Nome Completo, LazyLoginId
    public static SSO_UserInfo GetUserInfo(string token) { return GetUserInfo(token, null, null, null); }

    //
    // fornisce le informazioni principali riguardanti l'utente corrente: UserName, UserId, Email, Nome Completo, LazyLoginId
    public static SSO_UserInfo GetUserInfo(string token, bool? getRoles, bool? getAreas, bool? getVariables)
    {
      SSO_UserInfo userInfoOut = null;
      try
      {
        FormsAuthenticationTicket tk = FormsAuthentication.Decrypt(token);
        if (!tk.Expired)
        {
          ILazyLoginMapper llMapper = null;
          MembershipUser userInfo = null;
          List<string> roles = null;
          List<string> areas = null;
          List<SSO_UserVariable> userVariables = null;
          try
          {
            userInfo = Membership.GetUser(tk.Name);
            if (getRoles.GetValueOrDefault(false))
            {
              try { roles = Ikon.Auth.Roles_IKGD.Provider.GetRolesForUser(tk.Name).ToList(); }
              catch { }
            }
            if (getAreas.GetValueOrDefault(false))
            {
              try { areas = Ikon.Auth.Roles_IKGD.Provider.GetAreasForUser(tk.Name).ToList(); }
              catch { }
            }
            //
            if (getVariables.GetValueOrDefault(false))
            {
              try
              {
                using (IKGD_DataContext DC = IKGD_DBH.GetDB())
                {
                  userVariables = DC.SSO_KEYVALUEs.Where(r => r.UserId == (Guid)userInfo.ProviderUserKey).OrderBy(r => r.Id).Select(r => new SSO_UserVariable(r)).ToList();
                }
              }
              catch { }
            }
            //
            //ILazyLoginMapperFK llAnagrafica = null;
            //using (var DC = Autovie.DB.DataContext_XYZ.Factory())
            //{
            //  llMapper = Ikon.GD.LazyLoginDataContextExtensions.GetLazyLoginMapper(DC, (Guid)userInfo.ProviderUserKey, true);
            //  if (llMapper != null)
            //  {
            //    try { llAnagrafica = Ikon.GD.LazyLoginDataContextExtensions.GetLazyLoginMapperChild<XYZ.DB.LazyLogin_AnagraficaMain>(DC, llMapper, true); }
            //    catch { }
            //  }
            //}
          }
          catch { }
          //
          userInfoOut = new SSO_UserInfo(userInfo, llMapper);
          userInfoOut.Roles = roles;
          userInfoOut.Areas = areas;
          userInfoOut.UserVariables = userVariables;
          //
        }
      }
      catch { }
      return userInfoOut;
    }

    //
    // fornisce un'array con la lista dei ruoli associati all'utente corrente
    public static List<string> GetUserRoles(string token)
    {
      List<string> roles = null;
      try
      {
        FormsAuthenticationTicket tk = FormsAuthentication.Decrypt(token);
        if (!tk.Expired)
        {
          try { roles = Ikon.Auth.Roles_IKGD.Provider.GetRolesForUser(tk.Name).ToList(); }
          catch { }
        }
      }
      catch { }
      return roles;
    }

    //
    // fornisce un'array con la lista delle aree associate all'utente corrente
    public static List<string> GetUserAreas(string token)
    {
      List<string> areas = null;
      try
      {
        FormsAuthenticationTicket tk = FormsAuthentication.Decrypt(token);
        if (!tk.Expired)
        {
          try { areas = Ikon.Auth.Roles_IKGD.Provider.GetAreasForUser(tk.Name).ToList(); }
          catch { }
        }
      }
      catch { }
      return areas;
    }


    //
    // fornisce un oggetto con la lista delle user variables selezionate.
    // I parametri subsection e filter sono opzionali e permettono di selezionare il subset di variabili ('ACL', 'Anagrafica', 'Profile', ecc.) e/o un filtro sulle key (utilizzando una regular expression).
    // Viene ritornato un array con oggetti serializzati corrispondenti alla struttura del record presente sul database integrato con un ulteriore field “Value” contenente un valore serializzato corrispondente al Type specificato nel record.
    // Nel caso venisse specificato solo il token allora il sistema ritorna la lista completa delle user variables.
    public static List<SSO_UserVariable> GetUserVariables(string token)
    {
      List<SSO_UserVariable> userVariables = null;
      try
      {
        FormsAuthenticationTicket tk = FormsAuthentication.Decrypt(token);
        if (!tk.Expired)
        {
          return SSO_VariablesManager.GetUserVariables(tk.Name, null, true);
        }
      }
      catch { }
      return userVariables ?? new List<SSO_UserVariable>();
    }


    public static List<SSO_UserVariable> GetUserVariablesPublic(string token)
    {
      List<SSO_UserVariable> userVariables = null;
      try
      {
        FormsAuthenticationTicket tk = FormsAuthentication.Decrypt(token);
        if (!tk.Expired)
        {
          return SSO_VariablesManager.GetUserVariables(tk.Name, null, false);
        }
      }
      catch { }
      return userVariables ?? new List<SSO_UserVariable>();
    }


    //
    // methods to support remote membership functionality
    //


    //
    // validazione delle richieste di interazione remota con i Providers in base all'indirzzo IP della richiesta
    //
    public static bool CheckIfAllowedClient()
    {
      string networks = IKGD_Config.AppSettings["RemoteMembershipAllowedNETs"];
      if (networks.IsNullOrEmpty())
        return true;
      bool result = false;
      try
      {
        result |= IKGD_Config.IsLocalRequestWrapper;
        result |= Utility.CheckNetMaskIP(Utility.GetRequestAddressExt(null), networks);
      }
      catch { }
      return result;
    }


    //
    // helper per gestire la redirezione della richiesta al manager remoto dei Providers
    //
    public static T ProxySSO<T>(string module, string command, params object[] args) { return (T)ProxySSO(module, command, args); }
    public static object ProxySSO(string module, string command, params object[] args)
    {
      object result = null;
      string args_serialized = null;
      if (args != null)
      {
        try
        {
          args_serialized = Newtonsoft.Json.JsonConvert.SerializeObject(args, Newtonsoft.Json.Formatting.None, new Newtonsoft.Json.JsonSerializerSettings { TypeNameHandling = Newtonsoft.Json.TypeNameHandling.All });
          args_serialized = Utility.Encrypt(args_serialized);
        }
        catch { }
      }
      //
      string url = string.Format("{0}AuthSSO/ProxySSO", Ikon.Auth.Roles_IKGD.Provider.SSO_ServiceBaseUrl);
      //
      // eventualmente usare HttpWebRequest per gestire i timeout, cookies, headers, ecc.
      using (WebClient client = new WebClient())
      {
        client.Encoding = Encoding.UTF8;
        NameValueCollection reqparm = new NameValueCollection();
        reqparm.Add("module", module);
        reqparm.Add("command", command);
        reqparm.Add("args", args_serialized);
        byte[] responsebytes = client.UploadValues(url, "POST", reqparm);
        string responsebody = Encoding.UTF8.GetString(responsebytes);
        try
        {
          result = Newtonsoft.Json.JsonConvert.DeserializeObject(responsebody, new Newtonsoft.Json.JsonSerializerSettings { TypeNameHandling = Newtonsoft.Json.TypeNameHandling.All });
          result = ProxySSO_TypeMapper(result);
        }
        catch { }
      }
      return result;
    }


    //
    // worker module che processa la richista proveniente dal server remoto al controller locale
    // e si occupa di gestire la chiamata sul server che implementa fisicamente i providers
    //
    public static object ProxySSO_Worker(string module, string command, params object[] args)
    {
      object result = null;
      if (command.IsNullOrEmpty() || !CheckIfAllowedClient())
        return result;
      try
      {
        object objectBase = null;
        Type typeBase = typeof(Ikon.SSO.SSO_Manager);
        switch (module)
        {
          case "Role":
          case "Roles":
            objectBase = _ProviderRoles;
            typeBase = objectBase.GetType();
            break;
          case "Membership":
            objectBase = _ProviderMembership;
            typeBase = objectBase.GetType();
            break;
        }
        //
        MethodInfo mi = null;
        if (args != null)
        {
          try { mi = typeBase.GetMethod(command, args.Select(r => r == null ? typeof(object) : r.GetType()).ToArray()); }
          catch { }
          if (mi == null)
          {
            try { mi = typeBase.GetMethod(command, args.Select(r => r == null ? typeof(object) : r.GetType()).Select(t => t == typeof(Int64) ? typeof(Int32) : t).ToArray()); }
            catch { }
          }
        }
        if (mi == null)
        {
          try { mi = typeBase.GetMethod(command); }
          catch { }
        }
        if (mi != null)
        {
          try
          {
            // processing per sistemare i problemi di deserializzazione da JSON, che per design
            // deserializza Int32 in Int64 in mancanza di un tipo definito nell'oggetto di destinazione
            // il tutto complicato dalla presenza di parametri OUT nelle firme dei metodi trattati.
            var method_params = mi.GetParameters();
            for (int i = Math.Min(method_params.Length, args.Length) - 1; i >= 0; i--)
            {
              if (args[i] == null)
                continue;
              var t1 = args[i].GetType();
              var t2 = method_params[i].ParameterType;
              if (t1.IsByRef)
                t1 = t1.GetElementType();
              if (t2.IsByRef)
                t2 = t2.GetElementType();
              if (t1 != t2 && (t2.IsAssignableFrom(t1) || (t1 == typeof(Int64) && t2 == typeof(Int32))))
              {
                try { args[i] = Convert.ChangeType(args[i], t2); }
                catch { }
              }
              else if (t1 != t2 && t2.IsEnum)
              {
                try
                {
                  int val = Convert.ToInt32(args[i]);
                  args[i] = Enum.ToObject(t2, val);
                }
                catch { }
              }
            }
            //var tmp01 = args.Select(r => r == null ? typeof(object) : r.GetType()).ToArray();
            result = mi.Invoke(objectBase, args);
            result = ProxySSO_TypeMapper(result);
          }
          catch { }
        }
        //
      }
      catch { }
      //
      return result;
    }


    //
    // helper per la conversione bidirezionale tra i tipi non direttamente deserializzabili
    //
    public static object ProxySSO_TypeMapper(object data)
    {
      if (data != null)
      {
        if (data is MembershipUserFake)
        {
          data = MembershipUserFake.ToMembershipUser(data as MembershipUserFake);
        }
        else if (data is MembershipUser)
        {
          data = MembershipUserFake.ToMembershipUserFake(data as MembershipUser);
        }
        else if (data is IEnumerable<MembershipUserFake>)
        {
          var dataTmp = new MembershipUserCollection();
          (data as IEnumerable<MembershipUserFake>).ForEach(u => dataTmp.Add(MembershipUserFake.ToMembershipUser(u)));
          data = dataTmp;
        }
        else if (data is MembershipUserCollection)
        {
          data = (data as MembershipUserCollection).OfType<MembershipUser>().Select(u => MembershipUserFake.ToMembershipUserFake(u)).ToList();
        }
      }
      return data;
    }



    public class MembershipUserFake : MembershipUser
    {
      public new DateTime CreationDate { get; set; }
      public new DateTime LastLockoutDate { get; set; }
      public new DateTime LastPasswordChangedDate { get; set; }
      public new string PasswordQuestion { get; set; }
      public new string ProviderName { get; set; }
      public new object ProviderUserKey { get; set; }
      public new string UserName { get; set; }
      public new bool IsLockedOut { get; set; }
      public new bool IsOnline { get; set; }

      public static MembershipUser ToMembershipUser(MembershipUserFake fkUser)
      {
        return new MembershipUser(fkUser.ProviderName, fkUser.UserName, fkUser.ProviderUserKey, fkUser.Email, fkUser.PasswordQuestion, fkUser.Comment, fkUser.IsApproved, fkUser.IsLockedOut, fkUser.CreationDate, fkUser.LastLoginDate, fkUser.LastActivityDate, fkUser.LastPasswordChangedDate, fkUser.LastLockoutDate);
      }

      public static MembershipUserFake ToMembershipUserFake(MembershipUser user)
      {
        MembershipUserFake fkUser = new MembershipUserFake();
        fkUser.ProviderName = user.ProviderName;
        fkUser.UserName = user.UserName;
        fkUser.ProviderUserKey = user.ProviderUserKey;
        fkUser.Email = user.Email;
        fkUser.PasswordQuestion = user.PasswordQuestion;
        fkUser.Comment = user.Comment;
        fkUser.IsApproved = user.IsApproved;
        fkUser.IsLockedOut = user.IsLockedOut;
        fkUser.CreationDate = user.CreationDate;
        fkUser.LastLoginDate = user.LastLoginDate;
        fkUser.LastActivityDate = user.LastActivityDate;
        fkUser.LastPasswordChangedDate = user.LastPasswordChangedDate;
        fkUser.LastLockoutDate = user.LastLockoutDate;
        return fkUser;
      }

    }


  }




  [DataContract]
  public class SSO_UserInfo
  {
    [DataMember]
    public string UserName { get; set; }
    [DataMember]
    public Guid UserId { get; set; }
    [DataMember]
    public int IdLL { get; set; }
    [DataMember]
    public string Email { get; set; }
    [DataMember]
    public string FullName { get; set; }
    [DataMember]
    public List<string> Roles { get; set; }
    [DataMember]
    public List<string> Areas { get; set; }
    [DataMember]
    public List<SSO_UserVariable> UserVariables = null;


    public SSO_UserInfo()
    {
      Roles = new List<string>();
      Areas = new List<string>();
    }


    public SSO_UserInfo(MembershipUser user, ILazyLoginMapper llMapper)
      : this()
    {
      try
      {
        if (user != null)
        {
          UserName = user.UserName;
          Email = user.Email;
          FullName = user.Comment;
          if (user.ProviderUserKey is Guid)
            UserId = (Guid)user.ProviderUserKey;
        }
        if (llMapper != null)
        {
          IdLL = llMapper.Id;
          UserId = llMapper.UserId;
        }
        // ,ILazyLoginMapperFK llAnagrafica
        //if (llAnagrafica != null && llAnagrafica.IdLL == IdLL)
        //{
        //  string nome = Utility.FindPropertySafe<string>(llAnagrafica, "Nome");
        //  string cognome = Utility.FindPropertySafe<string>(llAnagrafica, "Cognome");
        //  FullName = Utility.Implode(new string[] { nome, cognome }, " ", null, true, true);
        //}
      }
      catch { }
    }
  }




}
