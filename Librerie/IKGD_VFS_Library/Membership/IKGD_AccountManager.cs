/*
 * 
 * Intranet Ikon
 * 
 * Copyright (C) 2009 Ikon Srl
 * Tutti i Diritti Riservati. All Rights Reserved
 * 
 */


using System;
using System.Data;
using System.Configuration;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.UI;
using System.Web.Security;
using System.Xml.Linq;
using System.Data.Linq;
using System.IO;
using System.Text;
using System.Linq.Expressions;
using System.Threading;
using System.Reflection;
using System.Data.Linq.Mapping;
using System.Diagnostics;
using System.Linq.Dynamic;
using System.Transactions;
using System.Web.Caching;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;  // assembly System.ServiceModel.Web
using System.Web.SessionState;
using LinqKit;

using Ikon;
using Ikon.GD;
using Ikon.Log;


namespace Ikon.GD
{
  public static class MembershipHelper
  {
    private static object _lock = new object();
    private static object _lockLong = new object();
    public static readonly string cookie_VFS_Membership = "IKGD_VFS_Membership_";
    private static List<Type> AnonymousDataMigrationTypes;


    //
    // per verificare la presenza di una cookie di identificazione
    // nel caso non sia presente e' possibile procedere con un login senza attivare automaticamente un accesso anonimo
    //
    private static string CookieNameMembership { get { return cookie_VFS_Membership + IKGD_Config.ApplicationFullName; } }
    public static bool HasMembershipCookie { get { return (HttpContext.Current.Request.Cookies.Value(CookieNameMembership) != null); } }


    //
    // parsing e decodifica della cookie di identificazione per lo username e storage in Session
    // con lo scope della request corrente (non c'e' il problema di ritrovarsi con autentifiche vecchie in sessione)
    //
    public static IKGD_VFS_Membership MembershipSession
    {
      get
      {
        lock (_lock)
        {
          //
          // storage solo per la request corrente
          //return (IKGD_VFS_Membership)(HttpContext.Current.Items[cookie_VFS_Membership] ?? (HttpContext.Current.Items[cookie_VFS_Membership] = new IKGD_VFS_Membership()));
          //
          // storage in sessione
          try
          {
            // e' previsto anche il salvataggio rapido di un flag in HttpContext.Current.Items per bypassare la verifica della cookie
            if (HttpContext.Current.Items[cookie_VFS_Membership] is bool && HttpContext.Current.Session != null && HttpContext.Current.Session[cookie_VFS_Membership] != null)
              return HttpContext.Current.Session[cookie_VFS_Membership] as IKGD_VFS_Membership;
            else if (HttpContext.Current.Session != null && HttpContext.Current.Request.Browser.Crawler == false && HttpContext.Current.Request.Browser.Cookies == true)
            {
              if (HttpContext.Current.Session[cookie_VFS_Membership] == null || (HttpContext.Current.Session[cookie_VFS_Membership] as IKGD_VFS_Membership).VerifyCookie() == false)
                HttpContext.Current.Session[cookie_VFS_Membership] = new IKGD_VFS_Membership();
              HttpContext.Current.Items[cookie_VFS_Membership] = true;  // per evitare il check della cookie dopo il primo test
              return HttpContext.Current.Session[cookie_VFS_Membership] as IKGD_VFS_Membership;
            }
            else
            {
              // nel caso non ci sia la session disponibile mi appoggio a HttpContext.Current.Items
              if (HttpContext.Current.Items[cookie_VFS_Membership] == null || (HttpContext.Current.Items[cookie_VFS_Membership] as IKGD_VFS_Membership).VerifyCookie() == false)
                HttpContext.Current.Items[cookie_VFS_Membership] = new IKGD_VFS_Membership();
              return HttpContext.Current.Items[cookie_VFS_Membership] as IKGD_VFS_Membership;
            }
          }
          catch { }
          // nel caso di Exception o di sessione non disponibile (es. in httpmodules)
          return new IKGD_VFS_Membership();
        }
      }
    }


    public static string UserName { get { try { return MembershipSession.UserName; } catch { return null; } } }
    public static object ProviderUserKey { get { try { return MembershipSession.ProviderUserKey; } catch { return null; } } }
    public static Guid ProviderUserKeyGuid { get { return (Guid)MembershipSession.ProviderUserKey; } }
    public static bool IsAnonymous { get { try { return MembershipSession.IsAnonymous; } catch { return true; } } }
    public static object CustomData { get { try { return MembershipSession.CustomData; } catch { return null; } } }

    //
    public static bool IsMembershipVerified
    {
      get
      {
        var data = MembershipSession;
        try { if (!data.IsAnonymous) return data.MembershipData.IsApproved; }
        catch { }
        return false;
      }
    }

    public static string Email
    {
      get
      {
        var data = MembershipSession;
        try { if (!data.IsAnonymous) return data.MembershipData.Email; }
        catch { }
        return null;
      }
    }

    //public static string FullName { get { return MembershipSession.MembershipData.Comment; } }
    //public static string FullNameNN { get { return MembershipSession.MembershipData.Comment.NullIfEmpty() ?? MembershipSession.MembershipData.UserName; } }
    public static string FullName { get { return MembershipSession.MembershipDataKVT.FullName; } }
    public static string FullNameNN { get { return MembershipSession.MembershipDataKVT.FullNameNN; } }
    public static List<string> Roles { get { return MembershipSession.Roles; } }
    public static List<string> Areas { get { return MembershipSession.Areas; } }
    public static ILazyLoginMapper LazyLoginMapperObject { get { return MembershipSession.LazyLoginMapperObject; } }


    public static void MembershipSessionReset()
    {
      try { HttpContext.Current.Session.Remove(cookie_VFS_Membership); }
      catch { }
    }


    public static void MembershipLogout()
    {
      lock (_lock)
      {
        if (HasMembershipCookie)
        {
          FS_OperationsHelpers.ClearCachedData();
        }
        // vengono ripuliti sia la session che gli items per funzionare sia con storage temporaneo che permanente
        try { HttpContext.Current.Items.Remove(cookie_VFS_Membership); }
        catch { }
        try { HttpContext.Current.Session.Remove(cookie_VFS_Membership); }
        catch { }
        try
        {
          Utility.CookieRemoveFromCurrentRequest(CookieNameMembership);
          Utility.CookieRemove(CookieNameMembership);
        }
        catch { }
        // deve essere gestita dal CMS non dal VFS
        //try { Utility.CookieRemove(FormsAuthentication.FormsCookieName); }
        //catch { }
        //if (HttpContext.Current.Session != null && string.IsNullOrEmpty(HttpContext.Current.Session.SessionID))
        //  HttpContext.Current.Session.Clear();
        //HttpContext.Current.Session.Abandon();
      }
    }


    public static bool MembershipLogin(string userName, bool? persistentVFS, bool? migrateAnonymousData) { return MembershipLogin((string.IsNullOrEmpty(userName) ? null : Membership.GetUser(userName)), persistentVFS, migrateAnonymousData); }
    public static bool MembershipLogin(MembershipUser userNew, bool? persistentVFS, bool? migrateAnonymousData)
    {
      bool status = true;
      MembershipUser userOld = null;
      bool userOldAnon = false;
      lock (_lockLong)
      {
        try
        {
          if (HasMembershipCookie)
          {
            userOld = MembershipSession.MembershipData;
            userOldAnon = IsAnonymous;
            MembershipLogout();
          }
          BuildTicketCookie(userNew, persistentVFS, true);
          //
          // nessuna migrazione di analytics tra user registrati o verso anonimi, viene attivata solo arrivando da anonimo
          //
          if (!userOldAnon || IsAnonymous)
            return status;
          migrateAnonymousData = migrateAnonymousData ?? Utility.TryParse<bool>(IKGD_Config.AppSettings["SSO_MigrateAnonymousData"], true);
          if (migrateAnonymousData == true)
          {
            status &= MembershipLoginMigrateAnonymousData(userOld, userNew);
          }
          //
        }
        catch { return false; }
      }
      return status;
    }


    public static bool MembershipLoginMigrateAnonymousData(MembershipUser userOld, MembershipUser userNew)
    {
      bool status = true;
      try
      {
        if (AnonymousDataMigrationTypes == null)
        {
          AnonymousDataMigrationTypes = Utility.FindTypesWithInterfaces(typeof(I_IKGD_MembershipAnonymousDataMigration)).ToList();
        }
        using (IKGD_DataContext DB = IKGD_DBH.GetDB())
        {
          //sembra che questa query combini un casino con lo scan della tabella di lazylogin
          //ILazyLoginMapper UserLL_Old = userOld == null ? null : DB.LazyLoginMappers.FirstOrDefault(r => r.UserId == (Guid)userOld.ProviderUserKey);
          //ILazyLoginMapper UserLL_New = userNew == null ? null : DB.LazyLoginMappers.FirstOrDefault(r => r.UserId == (Guid)userNew.ProviderUserKey);
          ILazyLoginMapper UserLL_Old = userOld == null ? null : DB.ExecuteQuery<LazyLoginMapper>("SELECT TOP (1) [ts], [Id], [UserId], [flag_active], [Creat] FROM [LazyLoginMapper] WHERE ([UserId] = {0})", userOld.ProviderUserKey.ToString()).FirstOrDefault();
          ILazyLoginMapper UserLL_New = userNew == null ? null : DB.ExecuteQuery<LazyLoginMapper>("SELECT TOP (1) [ts], [Id], [UserId], [flag_active], [Creat] FROM [LazyLoginMapper] WHERE ([UserId] = {0})", userNew.ProviderUserKey.ToString()).FirstOrDefault();
          //
          if (UserLL_New == null)
          {
            UserLL_New = DB.GetLazyLoginMapper((Guid)userNew.ProviderUserKey, true);
          }
          if (UserLL_New != null && UserLL_Old != null && UserLL_New.Id != UserLL_Old.Id)
          {
            foreach (Type ty in AnonymousDataMigrationTypes)
            {
              try { status &= (bool)ty.InvokeMember("MigrateAnonymousData", BindingFlags.InvokeMethod, null, null, new object[] { DB, userOld, UserLL_Old, userNew, UserLL_New }); }
              catch { status = false; }
            }
          }
        }
      }
      catch { status &= false; }
      return status;
    }


    public static bool HasMembershipACL()
    {
      return HasModuleXmlACL("main_admin_roles");
      //bool enabled = FS_OperationsHelpers.IsRoot;
      //List<string> rolesList = FS_OperationsHelpers.IsRoot ? System.Web.Security.Roles.GetAllRoles().ToList() : System.Web.Security.Roles.GetRolesForUser().ToList();
      //enabled |= rolesList.Intersect(Utility.Explode(IKGD_Config.AppSettings["AuthorMenu_RoleACL_main_admin_roles"], ",", " ", true)).Any();
      //enabled |= Utility.Explode(IKGD_Config.AppSettings["AuthorMenu_RoleACL_main_admin_roles"], ",", " ", true).Any(r => r == UserName);
      //return enabled;
    }


    public static bool HasModuleXmlACL(string moduleTagXml)
    {
      bool enabled = FS_OperationsHelpers.IsRoot;
      try
      {
        List<string> rolesList = FS_OperationsHelpers.IsRoot ? System.Web.Security.Roles.GetAllRoles().ToList() : System.Web.Security.Roles.GetRolesForUser().ToList();
        var xCfg = IKGD_Config.xConfigAuthor.Element(moduleTagXml);
        if (xCfg != null)
        {
          if (xCfg.AttributeValue("UsersACL") == "*" || Utility.Explode(xCfg.AttributeValue("UsersACL"), ",", " ", true).Contains(Ikon.GD.MembershipHelper.UserName))
            enabled |= true;
          if (Utility.Explode(xCfg.AttributeValue("RolesACL"), ",", " ", true).Intersect(rolesList).Any())
            enabled |= true;
          // devono essere gli ultimi della lista
          if (Utility.TryParse<bool>(xCfg.AttributeValue("LocalOnly"), false) == true && !IKGD_Config.IsLocalRequestWrapper)
            enabled = false;
          if (Utility.TryParse<bool>(xCfg.AttributeValue("Enabled"), true) == false)
            enabled = false;
        }
      }
      catch { }
      return enabled;
    }


    //
    // fornisce un riferimento a ILazyLoginMapper per l'utente corrente
    // creando il record se necessario
    //
    public static ILazyLoginMapper EnsureLazyLoginMapperVFS() { return EnsureLazyLoginMapperVFS(ProviderUserKeyGuid); }
    public static ILazyLoginMapper EnsureLazyLoginMapperVFS(Guid guid)
    {
      using (IKGD_DataContext DC = IKGD_DBH.GetDB())
      {
        //DC.ObjectTrackingEnabled = false;  // attenzione, se attivato non permette il salvataggio dei record!
        return DC.GetLazyLoginMapper(guid, null);
        //return EnsureLazyLoginMapperVFS(DC, guid);
      }
    }


    //
    // creazione ex novo di una cookie di identificazione per utenti registrati/anonimi
    //
    public static HttpCookie BuildTicketCookie(MembershipUser user, bool? persistentVFS, bool register)
    {
      bool anonymousAllowed = Utility.TryParse<bool>(IKGD_Config.AppSettings["SSO_AnonymousMembership"], true);
      if (user == null && anonymousAllowed)
        user = BuildAnonymousMembership(null, Guid.NewGuid());
      if (user == null)
        throw new Exception("Invalid UserName.");
      //
      // per la scadenza uso due settings differenti: giorni se permanente oppure la scadenza di default definita in web.config per il login
      //
      persistentVFS = persistentVFS ?? Utility.TryParse<bool>(IKGD_Config.AppSettings["SSO_PersistentMembership"], true);
      //
      //DateTime dateExpiry = (persistentVFS == true) ? DateTime.Now.AddDays(Utility.TryParse<int>(IKGD_Config.AppSettings["SSO_PersistentMembershipExpirationDays"], 30)) : DateTime.Now.AddMinutes(Utility.TryParse<int>(IKGD_Config.AppSettings["SSO_PersistentMembershipExpirationMinutes"], 60));
      DateTime dateExpiry = (persistentVFS == true) ? DateTime.Now.AddDays(Utility.TryParse<int>(IKGD_Config.AppSettings["SSO_PersistentMembershipExpirationDays"], 30)) : DateTime.MaxValue;
      //
      bool anonymous = (user.IsApproved == false && user.Email == null);
      string userData = user.ProviderUserKey.ToString() + "|" + anonymous.ToString();
      //
      HttpCookie newCookie = GetPersistentTicketCookie(user.UserName, CookieNameMembership, dateExpiry, persistentVFS.Value, "/", userData);
      if (register)
      {
        // e' assolutamente inutile tentare di cancellare le cookie per la sessione corrente, saranno eliminate solo al
        // reload successivo, piuttosto e' meglio usare un scan dell'ultima cookie inserita
        //Utility.CookieRemoveFromCurrentRequest(newCookie.Name);
        HttpContext.Current.Request.Cookies.Set(newCookie);  // per l'uso nella request corrente
        HttpContext.Current.Response.Cookies.Set(newCookie);  // per la registrazione
        //var cks1 = HttpContext.Current.Request.Cookies.LINQ().ToList();
        //var cks2 = HttpContext.Current.Response.Cookies.LINQ().ToList();
      }
      //
      return newCookie;
    }


    public static HttpCookie GetPersistentTicketCookie(string userName, string cookieName, DateTime dateExpiry, bool persistent, string path, string userData)
    {
      path = path ?? "/";
      userData = userData ?? string.Empty;
      FormsAuthenticationTicket ticket = new FormsAuthenticationTicket(2, userName, DateTime.Now, dateExpiry, persistent, userData, path);
      HttpCookie newCookie = new HttpCookie(cookieName);
      newCookie.Value = FormsAuthentication.Encrypt(ticket);
      newCookie.HttpOnly = true;
      if (persistent)
        newCookie.Expires = ticket.Expiration;
      newCookie.Path = path;
      Utility.CookieSetDomainAuto(newCookie, null);
      return newCookie;
    }


    public static MembershipUser BuildAnonymousMembership(string username, Guid guid)
    {
      username = username ?? string.Format("anonymous-{0}", guid.GetHashCode().ToString("x8"));
      return new MembershipUser(Membership.Provider.Name, username, guid, null, null, string.Empty, false, false, DateTime.Now, Utility.DateTimeMinValueDB, Utility.DateTimeMinValueDB, Utility.DateTimeMinValueDB, Utility.DateTimeMinValueDB);
      //return new MembershipUser(Membership.Provider.Name, username, guid, null, null, "Anonymous", false, false, DateTime.Now, Utility.DateTimeMinValueDB, Utility.DateTimeMinValueDB, Utility.DateTimeMinValueDB, Utility.DateTimeMinValueDB);
    }


    //
    // oggetto ausiliario per salvare le informazioni relative alla membership del VFS in Session
    // con memorizzazione delle info principali dell'utente (dalla cookie) e di quelle piu'
    // pesanti (MembershipUser) solo ondemand
    // l'oggetto consente anche lo storage di settings personalizzati in CustomData
    //
    public class IKGD_VFS_Membership
    {
      public bool IsCookieLess { get; protected set; }
      public string UserName { get; protected set; }
      public Guid ProviderUserKey { get; protected set; }
      public bool IsAnonymous { get; protected set; }
      public string CookieValue { get; protected set; }
      public List<string> Roles { get; protected set; }
      public List<string> Areas { get { return FS_OperationsHelpers.CachedAreasExtended.AreasAllowed; } }
      //
      public List<SSO_UserVariable> _UserVariables;
      public List<SSO_UserVariable> UserVariables
      {
        get
        {
          lock (_lock)
          {
            if (_UserVariables == null)
            {
              try
              {
                using (IKGD_DataContext DC = IKGD_DBH.GetDB())
                {
                  _UserVariables = DC.SSO_KEYVALUEs.Where(r => r.UserId == ProviderUserKey).OrderBy(r => r.Id).Select(r => new SSO_UserVariable(r)).ToList();
                }
              }
              catch { _UserVariables = new List<SSO_UserVariable>(); }
            }
            return _UserVariables;
          }
        }
      }


      public object CustomData { get; set; }
      //
      public MembershipUser _MembershipData;
      public MembershipUser MembershipData
      {
        get
        {
          if (_MembershipData == null)
          {
            if (!IsAnonymous)
            {
              try { _MembershipData = Membership.GetUser(UserName); }
              catch { }
            }
            if (_MembershipData == null)
            {
              try { _MembershipData = MembershipHelper.BuildAnonymousMembership(UserName, ProviderUserKey); }
              catch { }
            }
          }
          return _MembershipData;
        }
      }


      public Ikon.IKCMS.MembershipUserKVT _MembershipDataKVT;
      public Ikon.IKCMS.MembershipUserKVT MembershipDataKVT
      {
        get
        {
          if (_MembershipDataKVT == null)
          {
            _MembershipDataKVT = new Ikon.IKCMS.MembershipUserKVT(MembershipData);
          }
          return _MembershipDataKVT;
        }
      }


      private ILazyLoginMapper _LazyLoginMapperObject = null;
      public ILazyLoginMapper LazyLoginMapperObject
      {
        get
        {
          return (_LazyLoginMapperObject = _LazyLoginMapperObject ?? EnsureLazyLoginMapperVFS(ProviderUserKey));
        }
        set
        {
          _LazyLoginMapperObject = value;
        }
      }

      public void ClearLazyLoginMapper() { _LazyLoginMapperObject = null; }

      public ILazyLoginMapper EnsureLazyLoginMapper() { return LazyLoginMapperObject; }
      public ILazyLoginMapper EnsureLazyLoginMapper(ILazyLoginDataContext DC)
      {
        return _LazyLoginMapperObject = _LazyLoginMapperObject ?? DC.GetLazyLoginMapper(ProviderUserKey, null);
      }
      public ILazyLoginMapper EnsureLazyLoginMapper(Func<ILazyLoginDataContext> DC)
      {
        return _LazyLoginMapperObject = _LazyLoginMapperObject ?? DC().GetLazyLoginMapper(ProviderUserKey, null);
      }


      public ILazyLoginMapper EnsureLazyLoginMapperWithType(ILazyLoginDataContext DC)
      {
        if (_LazyLoginMapperObject == null || _LazyLoginMapperObject.GetType() != DC.GetLazyLoginMapperType())
          _LazyLoginMapperObject = DC.GetLazyLoginMapper();
        return _LazyLoginMapperObject;
      }

      public LLM EnsureLazyLoginMapperWithType<LLM>(ILazyLoginDataContext DC) where LLM : class, ILazyLoginMapper { return EnsureLazyLoginMapperWithType<LLM>(DC, false, null); }
      public LLM EnsureLazyLoginMapperWithType<LLM>(ILazyLoginDataContext DC, bool forceReload) where LLM : class, ILazyLoginMapper { return EnsureLazyLoginMapperWithType<LLM>(DC, forceReload, null); }
      public LLM EnsureLazyLoginMapperWithType<LLM>(ILazyLoginDataContext DC, bool forceReload, bool? autoSubmit)
        where LLM : class, ILazyLoginMapper
      {
        if (_LazyLoginMapperObject == null || !(_LazyLoginMapperObject is LLM) || forceReload)
          _LazyLoginMapperObject = DC.GetLazyLoginMapper(autoSubmit) as LLM;
        return _LazyLoginMapperObject as LLM;
      }


      public T EnsureCustomData<T>()
        where T : class, ILoginUserSettings, new()
      {
        lock (this)
        {
          if (CustomData == null || !(CustomData is T))
          {
            T obj = Activator.CreateInstance<T>();
            obj.Setup();
            CustomData = obj;
          }
        }
        return CustomData as T;
      }


      // attenzione usiamo l'hash dell'indirizzo IP perche' lo abbiamo solo come stringa va bene comunque
      protected Guid GetCookieLessGuid { get { return new Guid(Utility.GetRequestAddressExt(null).GetHashCode(), 0x6592, 0x4792, 0x81, 0x86, 0x57, 0x1b, 0x69, 0x8d, 0x2f, 0xce); } } //{IPADDRESS-6592-4792-8186-571B698D2FCE}

      protected HttpCookie FindLastCookie { get { return HttpContext.Current.Request.Cookies.LINQ().Where(c => c != null && c.Value != null && c.Name == CookieNameMembership).LastOrDefault(); } }

      //
      public bool VerifyCookie()
      {
        // crawler
        if (IsCookieLess)
          return true;
        HttpCookie cookie = FindLastCookie;
        return (cookie != null && cookie.Value == CookieValue);
      }

      //
      public IKGD_VFS_Membership()
      {
        try
        {
          // crawler/BOT
          IsCookieLess = (HttpContext.Current.Request.Browser.Crawler == true || HttpContext.Current.Request.Browser.Cookies == false);
          //IsCookieLess |= Utility.CheckIfBOT();
          if (IsCookieLess)
          {
            ProviderUserKey = GetCookieLessGuid;
            IsAnonymous = true;
            UserName = "Anonymous";
            Roles = new List<string>();
            CustomData = null;
            return;
          }
          FormsAuthenticationTicket ticket = null;
          HttpCookie cookie = FindLastCookie;
          if (cookie == null)
            cookie = MembershipHelper.BuildTicketCookie(null, null, true);
          try { ticket = FormsAuthentication.Decrypt(cookie.Value); }
          catch { }
          if (ticket == null)
          {
            cookie = MembershipHelper.BuildTicketCookie(null, null, true);
            try { ticket = FormsAuthentication.Decrypt(cookie.Value); }
            catch { }
          }
          if (ticket != null)
          {
            UserName = ticket.Name;
            ProviderUserKey = new Guid(ticket.UserData.Split('|')[0]);
            IsAnonymous = Utility.TryParse<bool>(ticket.UserData.Split('|')[1], true);
          }
          else
          {
            UserName = "Anonymous";
            ProviderUserKey = new Guid();
            IsAnonymous = true;
          }
          CookieValue = cookie.Value;
          CustomData = null;
          Roles = System.Web.Security.Roles.GetRolesForUser(UserName).ToList();
        }
        catch { }
      }

    }


  }



  //
  // dovrebbe essere usato per settare una cookie di sessione valida per piu' domini ma non sembra funzionare afffatto
  // <sessionState mode="InProc" ... customProvider="Ikon.GD.IKGD_SessionManagerCustom" />
  //
  public class IKGD_SessionManagerCustom : SessionIDManager, ISessionIDManager
  {
    void ISessionIDManager.SaveSessionID(HttpContext context, string id, out bool redirected, out bool cookieAdded)
    {
      base.SaveSessionID(context, id, out redirected, out cookieAdded);
      if (cookieAdded)
      {
        var cookie = context.Response.Cookies.LINQ().FirstOrDefault(c => c.Value == id);
        if (cookie != null)
        {
          Utility.CookieSetDomainAuto(cookie, null);
        }
        //HttpContext.Current.Session.SessionID
        //var name = "ASP.NET_SessionId";
        //var cookie = context.Response.Cookies[name];
        //cookie.Domain = "example.com";
      }
    }
  }



  [DataContract]
  public class SSO_UserVariable
  {
    [DataMember]
    public int Id { get; set; }
    [DataMember]
    public string SubSystem { get; set; }
    [DataMember]
    public string KeyParent { get; set; }
    [DataMember]
    public string Key { get; set; }
    //
    [DataMember]
    public int Type { get; set; }
    public Ikon.GD.SSO_KEYVALUE.SSO_KEYVALUE_TypeEnum TypeEnum { get { return (Ikon.GD.SSO_KEYVALUE.SSO_KEYVALUE_TypeEnum)Type; } set { Type = (int)value; } }
    //
    [DataMember]
    public int? ValueInt { get; set; }
    [DataMember]
    public double? ValueDouble { get; set; }
    [DataMember]
    public DateTime? ValueDate { get; set; }
    [DataMember]
    public DateTime? ValueDateExt { get; set; }
    [DataMember]
    public string ValueString { get; set; }


    public SSO_UserVariable() { }
    public SSO_UserVariable(SSO_KEYVALUE item)
    {
      Id = item.Id;
      SubSystem = item.SubSystem;
      Key = item.Key;
      KeyParent = item.KeyParent;
      Type = item.Type;
      ValueInt = item.ValueInt;
      ValueDouble = item.ValueDouble;
      ValueDate = item.ValueDate;
      ValueDateExt = item.ValueDateExt;
      ValueString = item.ValueText;
    }

  }



  public static class SSO_VariablesManager
  {


    public static List<SSO_UserVariable> GetUserVariables(Guid? providerUserKey) { return GetUserVariables(providerUserKey, null, true); }
    public static List<SSO_UserVariable> GetUserVariables(Guid? providerUserKey, string subSystem) { return GetUserVariables(providerUserKey, subSystem, false); }
    public static List<SSO_UserVariable> GetUserVariables(Guid? providerUserKey, string subSystem, bool allVars)
    {
      List<SSO_UserVariable> userVariables = new List<SSO_UserVariable>();
      try
      {
        providerUserKey = providerUserKey ?? MembershipHelper.ProviderUserKeyGuid;
        using (IKGD_DataContext DC = IKGD_DBH.GetDB())
        {
          if (allVars)
          {
            userVariables = DC.SSO_KEYVALUEs.Where(r => r.UserId == providerUserKey.Value).OrderBy(r => r.Id).Select(r => new SSO_UserVariable(r)).ToList();
          }
          else
          {
            userVariables = DC.SSO_KEYVALUEs.Where(r => r.UserId == providerUserKey.Value && string.Equals(r.SubSystem, subSystem)).OrderBy(r => r.Id).Select(r => new SSO_UserVariable(r)).ToList();
          }
        }
      }
      catch { }
      return userVariables;
    }


    public static List<SSO_UserVariable> GetUserVariables(string userName) { return GetUserVariables(userName, null, true); }
    public static List<SSO_UserVariable> GetUserVariables(string userName, string subSystem) { return GetUserVariables(userName, subSystem, false); }
    public static List<SSO_UserVariable> GetUserVariables(string userName, string subSystem, bool allVars)
    {
      Guid? providerUserKey = null;
      if (userName.IsNotEmpty())
      {
        try { providerUserKey = (Guid)Membership.GetUser(userName).ProviderUserKey; }
        catch { }
      }
      return GetUserVariables((Guid?)null, subSystem, allVars);
    }


  }



  //
  // interface per definire il supporto per la gestione di custom settings nei dati di login in sessione (CustomData)
  //
  public interface ILoginUserSettings
  {
    void Setup();  // metodo da chiamare nel costruttore o per reinizializzare l'oggetto
    void Update();
    void Update(bool force);  // metodo per effettuare l'update su persistent storage dei settings modificati
  }



  //
  // funzionalita' di base per la gestione della migrazione delle informazioni accumulate durante le sessioni anonime
  // verso l'account reale dell'utente
  //

  // le classi che implementano questa interfaccia deve avere un metodo statico che non e' definibile nell'interfaccia
  public interface I_IKGD_MembershipAnonymousDataMigration
  {
    //public static int MigrateAnonymousData(IKGD_DataContext DB, MembershipUser userOld, MembershipUser userNew)
  }


  //
  // migrazione dei dati relativi alla configurazione di IkonPortal (tabs, widgets, preferences, settings)
  //
  public class IKGD_MembershipAnonymousDataMigration_IkonPortal : I_IKGD_MembershipAnonymousDataMigration
  {
    public static int MigrateAnonymousData(IKGD_DataContext DB, MembershipUser userOld, ILazyLoginMapper UserLL_Old, MembershipUser userNew, ILazyLoginMapper UserLL_New)
    {
      using (TransactionScope ts = IKGD_TransactionFactory.Transaction(null))
      {
        try
        {
          int res01 = DB.ExecuteCommand("DELETE tNew FROM [IKG_PREF] tOld, [IKG_PREF] tNew WHERE (tOld.username='{0}' AND tNew.username='{1}' AND tOld.key=tNew.key)", userOld.UserName, userNew.UserName);
          int res02 = DB.ExecuteCommand("DELETE tNew FROM [IKG_SETTING] tOld, [IKG_SETTING] tNew WHERE (tOld.username='{0}' AND tNew.username='{1}' AND tOld.sNode=tNew.sNode)", userOld.UserName, userNew.UserName);
          int res03 = DB.ExecuteCommand("DELETE tNew FROM [IKG_TAB] tOld, [IKG_TAB] tNew WHERE (tOld.username='{0}' AND tNew.username='{1}' AND tOld.sNode=tNew.sNode)", userOld.UserName, userNew.UserName);  // cancella a catena anche i widget del tab
          //
          int res04 = DB.ExecuteCommand("UPDATE [IKG_PREF] SET username='{1}' WHERE (username='{0}')", userOld.UserName, userNew.UserName);
          int res05 = DB.ExecuteCommand("UPDATE [IKG_SETTING] SET username='{1}' WHERE (username='{0}')", userOld.UserName, userNew.UserName);
          int res06 = DB.ExecuteCommand("UPDATE [IKG_TAB] SET username='{1}' WHERE (username='{0}')", userOld.UserName, userNew.UserName);
          // attenzione c'e' un cascade sulla FK tra IKG_TAB e IKG_WIDGET per cui dovrebbe essere gia' tutto sistemato con il comando precedente
          int res07 = DB.ExecuteCommand("UPDATE [IKG_WIDGET] SET username='{1}' WHERE (username='{0}')", userOld.UserName, userNew.UserName);
          //
          int res08 = DB.ExecuteCommand("UPDATE [IKG_LOGGER] SET username='{1}' WHERE (username='{0}')", userOld.UserName, userNew.UserName);
          //
          ts.Committ();
        }
        catch { }
      }
      return 0;
    }
  }


  //
  // migrazione dei dati relativi al logging generico e gestione voti/commenti
  //
  public class IKGD_MembershipAnonymousDataMigration_LL_LogVote : I_IKGD_MembershipAnonymousDataMigration
  {
    public static int MigrateAnonymousData(IKGD_DataContext DB, MembershipUser userOld, ILazyLoginMapper UserLL_Old, MembershipUser userNew, ILazyLoginMapper UserLL_New)
    {
      using (TransactionScope ts = IKGD_TransactionFactory.Transaction(null))
      {
        if (UserLL_Old != null && UserLL_New != null)
        {
          try
          {
            // migrazione dei Log
            int res01 = DB.ExecuteCommand("UPDATE [LazyLogin_Log] SET IdLL={1} WHERE (IdLL={0})", UserLL_Old.Id, UserLL_New.Id);
            // cancellazione degli eventuali Vote che darebbero conflitti con la PK (i nuovi Vote sovrascrivono solo i vecchi gia' esistenti)
            int res02 = DB.ExecuteCommand("DELETE [LazyLogin_Vote] WHERE (IdLL={0} AND rNode IN (SELECT rNode WHERE (IdLL={1})))", UserLL_Old.Id, UserLL_New.Id);
            int res03 = DB.ExecuteCommand("UPDATE [LazyLogin_Vote] SET IdLL={1} WHERE (IdLL={0})", UserLL_Old.Id, UserLL_New.Id);
          }
          catch { }
        }
        if (userOld != null)
        {
          try
          {
            // non posso cancellare il record del mapper ma posso solo disabilitarlo in quanto serve ancora per gli altri moduli di processing
            int res04 = DB.ExecuteCommand("UPDATE [LazyLoginMapper] SET flag_active=0 WHERE (UserId={0})", (Guid)userOld.ProviderUserKey);
          }
          catch { }
        }
        //
        ts.Committ();
      }
      return 0;
    }
  }


}
