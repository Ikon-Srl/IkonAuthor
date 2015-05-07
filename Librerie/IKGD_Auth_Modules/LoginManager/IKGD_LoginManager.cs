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
using System.IO;
using System.Text;
using System.Linq.Expressions;
using System.Threading;
using System.Reflection;
using System.Diagnostics;
using System.Linq.Dynamic;
using System.Transactions;
using System.Web.Caching;
using System.Runtime.Serialization;
using LinqKit;

using Ikon;
using Ikon.GD;
using Ikon.Log;


namespace Ikon.Auth.Login
{
  public static class LoginManager
  {
    private static object _lock = new object();

    //
    // funzionalita' per gestire l'attivazione/verifica dell'account tramite email+url+hash (encrypt del guid + scadenza hash di 1 ora)
    //

    //
    // nel CMS il login di ASP.NET viene utilizzato esclusivamente per l'accesso con autentifica esplicita
    // ed e' SEMPRE associato a cookie non permanenti e diversa da qualla utilizzata per il VFS
    // la gestione del logout(esplicito) comporta la cancellazione delle sessione permanente del VFS
    // persistentVFS si riferisce alla modalita' di creazione della cookie per il VFS
    // persistentVFS = false generalmente solo per il LoginFake.aspx
    //
    public static bool LoginUser(string username) { return LoginUser(username, true, null, null); }
    public static bool LoginUser(MembershipUser user) { return LoginUser(user, true, null, null); }
    //
    public static bool LoginUser(string username, bool approvedOnly, bool? persistentVFS, bool? migrateAnonymousData) { return LoginUser(username, approvedOnly, persistentVFS, migrateAnonymousData, null); }
    public static bool LoginUser(string username, bool approvedOnly, bool? persistentVFS, bool? migrateAnonymousData, bool? fromFakeLogin)
    {
      MembershipUser user = null;
      try { user = Membership.GetUser(username); }
      catch (Exception ex) { HttpContext.Current.Trace.Write("Membership.GetUser(username);", ex.Message); }
      if (user == null && fromFakeLogin == true)
      {
        user = new MembershipUser(Membership.Provider.Name, username, Guid.NewGuid(), null, null, username, true, false, DateTime.Now, Utility.DateTimeMinValueDB, Utility.DateTimeMinValueDB, Utility.DateTimeMinValueDB, Utility.DateTimeMinValueDB);
      }
      return LoginUser(user, approvedOnly, persistentVFS, migrateAnonymousData);
    }
    //
    public static bool LoginUser(MembershipUser user, bool approvedOnly, bool? persistentVFS, bool? migrateAnonymousData)
    {
      try
      {
        if (approvedOnly)
        {
          if (user == null || user.IsLockedOut)
          {
            return false;
          }
        }
        if (!MembershipHelper.HasMembershipCookie || user == null || MembershipHelper.UserName != user.UserName)
        {
          MembershipHelper.MembershipLogin(user, persistentVFS, migrateAnonymousData);
        }
        //
        // gli utenti non ancora verificati hanno accesso al VFS ma non devono avere la cookie di login attivata
        //
        if (approvedOnly && (user == null || !user.IsApproved))
          return false;
        //
        // il layer di login per il CMS e' del tutto regolare, non viene gestito in maniera molto piu' complessa come
        // per IkonPortal/CMS dove devo avere la gestione anonima/persistente e posso utilizzare i normali settings
        // presenti in web.config
        //
        bool persistent = Utility.TryParse<bool>(IKGD_Config.AppSettings["SSO_PersistentAutenticationLogin"], false);
        string userName = (user != null) ? user.UserName : MembershipHelper.UserName;
        //
        //DateTime dateExpiry = persistent ? DateTime.Now.AddDays(Utility.TryParse<int>(IKGD_Config.AppSettings["SSO_PersistentMembershipExpirationDays"], 30)) : DateTime.Now.AddMinutes(Utility.TryParse<int>(IKGD_Config.AppSettings["SSO_PersistentMembershipExpirationMinutes"], 60));
        DateTime dateExpiry = persistent ? DateTime.Now.AddDays(Utility.TryParse<int>(IKGD_Config.AppSettings["SSO_PersistentMembershipExpirationDays"], 30)) : DateTime.MaxValue;
        //
        HttpCookie newCookie = MembershipHelper.GetPersistentTicketCookie(userName, FormsAuthentication.FormsCookieName, dateExpiry, persistent, FormsAuthentication.FormsCookiePath, null);
        HttpContext.Current.Response.Cookies.Add(newCookie);
        //
        if (Utility.TryParse<bool>(IKGD_Config.AppSettings["IKG_LOGGER_Enabled"], true))
        {
          try { Ikon.IKCMS.IKCMS_HitLogger.ProcessLogger(userName, Ikon.IKCMS.IKCMS_HitLogger.LoggerActions.Login, Utility.GetRequestAddressExt(null)); }
          catch { }
        }
        //
        return true;
      }
      catch (Exception ex)
      {
        HttpContext.Current.Trace.Write("LoginUser exception:", ex.Message);
        return false;
      }
    }


    public static void LogoutUser()
    {
      lock (_lock)
      {
        MembershipHelper.MembershipLogout();
        // deve essere gestita dal CMS non dal VFS
        try { Utility.CookieRemove(FormsAuthentication.FormsCookieName); }
        catch { }
        // non si deve usare la funzionalita' standard di logout altrimenti viene generata una nuova coockie che non c'entra niente
        //try { FormsAuthentication.SignOut(); }
        //catch { }
        try
        {
          if (HttpContext.Current.Session != null && string.IsNullOrEmpty(HttpContext.Current.Session.SessionID))
          {
            HttpContext.Current.Session.Clear();
            // TODO: ma serve ancora veramente?
            foreach (HttpCookie aux_ck in HttpContext.Current.Request.Cookies.OfType<HttpCookie>().Where(c => c.Value == HttpContext.Current.Session.SessionID).ToList())
            {
              aux_ck.Expires = DateTime.Now.AddYears(-1);
              Utility.CookieSetDomainAuto(aux_ck, null);
              HttpContext.Current.Response.Cookies.Add(aux_ck);
            }
          }
          HttpContext.Current.Session.Abandon();
        }
        catch { }
      }
    }


    public static List<string> AuthTokensList
    {
      get
      {
        if (HttpContext.Current.Application["AuthTokensList"] == null)
          HttpContext.Current.Application["AuthTokensList"] = new List<string>();
        return HttpContext.Current.Application["AuthTokensList"] as List<string>;
      }
    }

    public static string EncryptWithAuthToken(string token, string data)
    {
      return Utility.Encrypt((token ?? string.Empty) + "|" + (data ?? string.Empty));
    }

    public static string DecryptWithAuthToken(string encodedToken)
    {
      try
      {
        string msg = Utility.Decrypt(encodedToken);
        string[] frags = msg.Split("|".ToCharArray(), 2);
        string token = frags[0];
        string data = frags[1];
        if (AuthTokensList.Contains(token))
        {
          AuthTokensList.Remove(token);
          return data;
        }
      }
      catch { }
      return null;
    }


  }
}
