/*
 * 
 * Intranet Ikon
 * 
 * Copyright (C) 2008 Ikon Srl
 * Tutti i Diritti Riservati. All Rights Reserved
 * 
 */


using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Data;
using System.Linq;
using System.Linq.Expressions;
using System.Web;
using System.Web.Security;
using System.Security;
using System.Security.Principal;
using System.Net;
using LinqKit;


namespace Ikon.Auth
{

  //
  // classe base per creare membership provider custom
  // con il supporto per l'autenticazione di root e di un fallback provider
  //
  // parametri del provider
  //    ApplicationName="xyz"
  //    fallBackProvider="Membership_AuxMapUR"   // nome del provider di fallback se non trova l'account con il provider custom (e' possibile utilizzare map_user_role)
  //
  public abstract class Membership_IKGD : MembershipProvider
  {
    //
    protected NameValueCollection savedConfig;
    public virtual string fallBackProviderName { get; protected set; }
    //


    public Membership_IKGD()
    {
    }


    public override void Initialize(string name, NameValueCollection config)
    {
      if (config == null)
        throw new ArgumentNullException("config");
      //
      base.Initialize(name, config);
      savedConfig = new NameValueCollection(config);
      //
      ApplicationName = GetConfigValue("ApplicationName", System.Web.Hosting.HostingEnvironment.ApplicationVirtualPath);
      fallBackProviderName = GetConfigValue("fallBackProvider", "map_user_role");
      //
    }


    //
    // A helper function to retrieve config values from the configuration file.
    //
    protected string GetConfigValue(string itemName, string defaultValue)
    {
      try { return savedConfig[itemName]; }
      catch { return defaultValue; }
    }


    public virtual MembershipProvider fallBackProvider { get { return Membership.Providers[fallBackProviderName]; } }


    public override string ApplicationName { get; set; }
    public override bool EnablePasswordReset { get { return false; } }
    public override bool EnablePasswordRetrieval { get { return false; } }
    public override bool RequiresQuestionAndAnswer { get { return false; } }
    public override bool RequiresUniqueEmail { get { return false; } }
    public override int MaxInvalidPasswordAttempts { get { return int.MaxValue; } }
    public override int PasswordAttemptWindow { get { return int.MaxValue; } }
    public override MembershipPasswordFormat PasswordFormat { get { return MembershipPasswordFormat.Clear; } }
    public override int MinRequiredNonAlphanumericCharacters { get { return 0; } }
    public override int MinRequiredPasswordLength { get { return 0; } }
    public override string PasswordStrengthRegularExpression { get { return string.Empty; } }


    public override bool ChangePassword(string username, string oldPassword, string newPassword)
    {
      throw new NotImplementedException();
    }

    public override bool ChangePasswordQuestionAndAnswer(string username, string password, string newPasswordQuestion, string newPasswordAnswer)
    {
      throw new NotImplementedException();
    }

    public override MembershipUser CreateUser(string username, string password, string email, string passwordQuestion, string passwordAnswer, bool isApproved, object providerUserKey, out MembershipCreateStatus status)
    {
      throw new NotImplementedException();
    }

    public override bool DeleteUser(string username, bool deleteAllRelatedData)
    {
      throw new NotImplementedException();
    }

    public override int GetNumberOfUsersOnline()
    {
      throw new NotImplementedException();
    }

    public override string ResetPassword(string username, string answer)
    {
      throw new NotImplementedException();
    }

    public override string GetPassword(string username, string answer)
    {
      throw new NotImplementedException();
    }

    public override bool UnlockUser(string userName)
    {
      throw new NotImplementedException();
    }

    public override void UpdateUser(MembershipUser user)
    {
      throw new NotImplementedException();
    }

    public override string GetUserNameByEmail(string email)
    {
      throw new NotImplementedException();
    }


    public abstract bool ValidateUserCustom(string username, string password);
    public override bool ValidateUser(string username, string password)
    {
      try
      {
        //
        // prevedere un accesso custom per root con la password hashed
        // e un accesso fallback per utenti privati con il solito aspnet provider (se definito)
        //
        if (username == "root")
        {
          //return Membership.ValidateUser(username, password);
          return FormsAuthentication.Authenticate(username, password);
        }
        //
        if (ValidateUserCustom(username, password))
          return true;
        //
        // non e' stato possibile autenticare l'utente tramite il provider custom
        // adesso e' possibile utilizzare il fallback provider
        //
        if (fallBackProvider != null)
          return fallBackProvider.ValidateUser(username, password);
      }
      catch { }
      return false;
    }


    public abstract MembershipUser GetUserCustom(object providerUserKey, bool userIsOnline);
    public override MembershipUser GetUser(object providerUserKey, bool userIsOnline)
    {
      try
      {
        //
        // prevedo un accesso custom per root
        //
        if (providerUserKey is int && (int)providerUserKey == 0)
        {
          return new MembershipUser(this.Name, "root", Guid.NewGuid(), string.Empty, string.Empty, "root", true, false, DateTime.MinValue, DateTime.MinValue, DateTime.MinValue, DateTime.MinValue, DateTime.MinValue);
        }
        //
        MembershipUser userCustom = GetUserCustom(providerUserKey, userIsOnline);
        if (userCustom != null)
          return userCustom;
        //
        // fallback provider
        //
        if (fallBackProvider != null)
          return fallBackProvider.GetUser(providerUserKey, userIsOnline);
        //
      }
      catch { }
      return null;
    }


    public abstract MembershipUser GetUserCustom(string username, bool userIsOnline);
    public override MembershipUser GetUser(string username, bool userIsOnline)
    {
      try
      {
        //
        // prevedo un accesso custom per root
        //
        if (username == "root")
        {
          return new MembershipUser(this.Name, "root", Guid.NewGuid(), string.Empty, string.Empty, "root", true, false, DateTime.MinValue, DateTime.MinValue, DateTime.MinValue, DateTime.MinValue, DateTime.MinValue);
        }
        //
        MembershipUser userCustom = GetUserCustom(username, userIsOnline);
        if (userCustom != null)
          return userCustom;
        //
        // fallback provider
        //
        if (fallBackProvider != null)
          return fallBackProvider.GetUser(username, userIsOnline);
        //
      }
      catch
      {
        //throw;
      }
      return null;
    }


    public override MembershipUserCollection GetAllUsers(int pageIndex, int pageSize, out int totalRecords)
    {
      throw new NotImplementedException();
    }


    public override MembershipUserCollection FindUsersByName(string usernameToMatch, int pageIndex, int pageSize, out int totalRecords)
    {
      throw new NotImplementedException();
    }


    public override MembershipUserCollection FindUsersByEmail(string emailToMatch, int pageIndex, int pageSize, out int totalRecords)
    {
      throw new NotImplementedException();
    }


  }



}
