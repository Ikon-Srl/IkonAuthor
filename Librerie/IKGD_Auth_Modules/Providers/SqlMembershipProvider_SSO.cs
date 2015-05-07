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

using Ikon.GD;
using Ikon.SSO;
using Ikon.Auth;
using Ikon;


namespace System.Web.Security
{

    public class SqlMembershipProvider_SSO : SqlMembershipProvider
    {
        //
        protected NameValueCollection savedConfig;
        //
        public bool PasswordExpiryEnabled { get; protected set; }
        public bool ForceChangePasswordEnabled { get; protected set; }


        public SqlMembershipProvider_SSO()
            : base()
        {
        }


        public override void Initialize(string name, NameValueCollection config)
        {
            if (config == null)
                throw new ArgumentNullException("config");
            //
            savedConfig = new NameValueCollection(config);
            //
            var customAttributes = new string[] { "passwordExpiryEnabled", "forceChangePasswordEnabled" };
            customAttributes.ForEach(r => config.Remove(r));
            //
            base.Initialize(name, config);
            //
            //ApplicationName = GetConfigValue("ApplicationName", System.Web.Hosting.HostingEnvironment.ApplicationVirtualPath);
            //
            PasswordExpiryEnabled = Utility.TryParse<bool>(GetConfigValue("passwordExpiryEnabled", "false"));
            ForceChangePasswordEnabled = Utility.TryParse<bool>(GetConfigValue("forceChangePasswordEnabled", "false"));
        }


        //
        // A helper function to retrieve config values from the configuration file.
        //
        protected string GetConfigValue(string itemName, string defaultValue)
        {
            string val = null;
            try { val = savedConfig[itemName] ?? savedConfig[savedConfig.AllKeys.FirstOrDefault(k => string.Equals(k, itemName, StringComparison.OrdinalIgnoreCase))]; }
            catch { }
            return val ?? defaultValue;
        }


        public static bool IsExternalSSO_Enabled { get { return Roles_IKGD.IsExternalSSO_Enabled; } }

        //
        // seleziona il primo provider di tipo SqlMembershipProvider_SSO
        //
        protected static SqlMembershipProvider_SSO _Provider_Cached = null;
        public static SqlMembershipProvider_SSO Provider
        {
            get
            {
                if (_Provider_Cached == null)
                    _Provider_Cached = Membership.Providers.OfType<SqlMembershipProvider_SSO>().FirstOrDefault();
                return _Provider_Cached;
            }
        }
        //


        public override bool ChangePassword(string username, string oldPassword, string newPassword)
        {
            if (IsExternalSSO_Enabled)
            {
                return SSO_Manager.ProxySSO<bool>("Membership", "ChangePassword", username, oldPassword, newPassword);
            }
            return base.ChangePassword(username, oldPassword, newPassword);
        }


        public override bool ChangePasswordQuestionAndAnswer(string username, string password, string newPasswordQuestion, string newPasswordAnswer)
        {
            if (IsExternalSSO_Enabled)
            {
                return SSO_Manager.ProxySSO<bool>("Membership", "ChangePasswordQuestionAndAnswer", username, password, newPasswordQuestion, newPasswordAnswer);
            }
            return base.ChangePasswordQuestionAndAnswer(username, password, newPasswordQuestion, newPasswordAnswer);
        }


        public override MembershipUser CreateUser(string username, string password, string email, string passwordQuestion, string passwordAnswer, bool isApproved, object providerUserKey, out MembershipCreateStatus status)
        {
            if (IsExternalSSO_Enabled)
            {
                MembershipUser user = null;
                status = MembershipCreateStatus.ProviderError;
                try
                {
                    user = SSO_Manager.ProxySSO<MembershipUser>("Membership", "CreateUser", username, password, email, passwordQuestion, passwordAnswer, isApproved, providerUserKey, status);
                    if (user != null)
                        status = MembershipCreateStatus.Success;
                }
                catch { }
                return user;
            }
            return base.CreateUser(username, password, email, passwordQuestion, passwordAnswer, isApproved, providerUserKey, out status);
        }


        public override bool DeleteUser(string username, bool deleteAllRelatedData)
        {
            if (IsExternalSSO_Enabled)
            {
                return SSO_Manager.ProxySSO<bool>("Membership", "DeleteUser", username, deleteAllRelatedData);
            }
            return base.DeleteUser(username, deleteAllRelatedData);
        }


        public override int GetNumberOfUsersOnline()
        {
            if (IsExternalSSO_Enabled)
            {
                return SSO_Manager.ProxySSO<int>("Membership", "GetNumberOfUsersOnline");
            }
            return base.GetNumberOfUsersOnline();
        }


        public override string ResetPassword(string username, string answer)
        {
            if (IsExternalSSO_Enabled)
            {
                return SSO_Manager.ProxySSO<string>("Membership", "ResetPassword", username, answer);
            }
            return base.ResetPassword(username, answer);
        }


        public override string GetPassword(string username, string answer)
        {
            if (IsExternalSSO_Enabled)
            {
                return SSO_Manager.ProxySSO<string>("Membership", "GetPassword", username, answer);
            }
            return base.GetPassword(username, answer);
        }


        public override bool UnlockUser(string userName)
        {
            if (IsExternalSSO_Enabled)
            {
                return SSO_Manager.ProxySSO<bool>("Membership", "UnlockUser", userName);
            }
            return base.UnlockUser(userName);
        }


        public override void UpdateUser(MembershipUser user)
        {
            if (IsExternalSSO_Enabled)
            {
                var userFake = Ikon.SSO.SSO_Manager.ProxySSO_TypeMapper(user);
                SSO_Manager.ProxySSO("Membership", "UpdateUserForSSO", userFake);
                return;
            }
            base.UpdateUser(user);
        }


        // metodo duplicato con un nome differente per evitare ambiguita' con la reflection quando vengono passati parametri non tipizzati completamente
        public virtual void UpdateUserForSSO(Ikon.SSO.SSO_Manager.MembershipUserFake userFake)
        {
            MembershipUser user = Ikon.SSO.SSO_Manager.ProxySSO_TypeMapper(userFake) as MembershipUser;
            base.UpdateUser(user);
        }


        public override string GetUserNameByEmail(string email)
        {
            if (IsExternalSSO_Enabled)
            {
                return SSO_Manager.ProxySSO<string>("Membership", "GetUserNameByEmail", email);
            }
            return base.GetUserNameByEmail(email);
        }


        public override bool ValidateUser(string username, string password)
        {
            if (IsExternalSSO_Enabled)
            {
                return SSO_Manager.ProxySSO<bool>("Membership", "ValidateUser", username, password);
            }
            bool result = base.ValidateUser(username, password);
            result &= (CheckIfPasswordExpired(null, username) == false);
            return result;
        }


        public override MembershipUser GetUser(object providerUserKey, bool userIsOnline)
        {
            if (IsExternalSSO_Enabled)
            {
                return SSO_Manager.ProxySSO<MembershipUser>("Membership", "GetUserByKey", providerUserKey, userIsOnline);
            }
            return base.GetUser(providerUserKey, userIsOnline);
        }


        // metodo duplicato con un nome differente per evitare ambiguita' con la reflection quando vengono passati parametri non tipizzati completamente
        public virtual MembershipUser GetUserByKey(object providerUserKey, bool userIsOnline)
        {
            if (providerUserKey is string)
            {
                try { providerUserKey = new Guid(providerUserKey as string); }
                catch { }
            }
            return base.GetUser(providerUserKey, userIsOnline);
        }


        public override MembershipUser GetUser(string username, bool userIsOnline)
        {
            //
            // quando nel vecchio codice vogliamo acquisire lo user con
            // MembershipUser user = Membership.GetUser();
            // viene chiamata questo metodo del provider con username = ""
            if (username.IsNullOrEmpty() && Utility.TryParse<bool>(IKGD_Config.AppSettings["MembershipEnableGetUserFromLazyLogin"], false))
            {
                username = Ikon.GD.MembershipHelper.UserName;
            }
            if (IsExternalSSO_Enabled)
            {
                return SSO_Manager.ProxySSO<MembershipUser>("Membership", "GetUser", username, userIsOnline);
            }
            return base.GetUser(username, userIsOnline);
        }


        public override MembershipUserCollection GetAllUsers(int pageIndex, int pageSize, out int totalRecords)
        {
            if (IsExternalSSO_Enabled)
            {
                totalRecords = 0;
                return SSO_Manager.ProxySSO<MembershipUserCollection>("Membership", "GetAllUsers", pageIndex, pageSize, totalRecords);
            }
            return base.GetAllUsers(pageIndex, pageSize, out totalRecords);
        }


        public override MembershipUserCollection FindUsersByName(string usernameToMatch, int pageIndex, int pageSize, out int totalRecords)
        {
            if (IsExternalSSO_Enabled)
            {
                totalRecords = 0;
                return SSO_Manager.ProxySSO<MembershipUserCollection>("Membership", "FindUsersByName", usernameToMatch, pageIndex, pageSize, totalRecords);
            }
            return base.FindUsersByName(usernameToMatch, pageIndex, pageSize, out totalRecords);
        }


        public override MembershipUserCollection FindUsersByEmail(string emailToMatch, int pageIndex, int pageSize, out int totalRecords)
        {
            if (IsExternalSSO_Enabled)
            {
                totalRecords = 0;
                return SSO_Manager.ProxySSO<MembershipUserCollection>("Membership", "FindUsersByEmail", emailToMatch, pageIndex, pageSize, totalRecords);
            }
            return base.FindUsersByEmail(emailToMatch, pageIndex, pageSize, out totalRecords);
        }


        //
        // custom extensions
        //

        //
        // PasswordExpiry - begin
        //
        public virtual bool CheckIfPasswordExpired(object providerUserKey, string username)
        {
            if (!PasswordExpiryEnabled)
            {
                return false;
            }
            DateTime? expiryDate = GetPasswordExpiry(providerUserKey, username);
            if (expiryDate != null)
            {
                return FS_OperationsHelpers.DateTimeSession > expiryDate.Value;
            }
            return false;
        }

        public virtual DateTime? GetPasswordExpiry(object providerUserKey, string username)
        {
            if (!PasswordExpiryEnabled)
            {
                return null;
            }
            if (IsExternalSSO_Enabled)
            {
                return SSO_Manager.ProxySSO<DateTime?>("Membership", "GetPasswordExpiry", providerUserKey, username);
            }
            //
            DateTime? expiryDate = null;
            try
            {
                // attenzione che la tabella potrebbe non esistere su tutti i database
                using (Ikon.Auth.ExtraDB.DataContext DB = Ikon.Auth.ExtraDB.DataContext.Factory())
                {
                    if (providerUserKey != null && providerUserKey is Guid)
                    {
                        Guid uid = (Guid)providerUserKey;
                        expiryDate = DB.aspnet_MembershipExts.Where(r => r.UserId == uid).Select(r => r.PasswordExpiryDate).FirstOrDefault();
                    }
                    else
                    {
                        expiryDate = DB.aspnet_MembershipExts.Where(r => string.Equals(r.aspnet_Membership.aspnet_User.LoweredUserName, username)).Select(r => r.PasswordExpiryDate).FirstOrDefault();
                    }
                }
            }
            catch { }
            //
            return expiryDate;
        }

        public virtual bool SetPasswordExpiry(object providerUserKey, string username, DateTime? expiryDate)
        {
            if (!PasswordExpiryEnabled)
            {
                return false;
            }
            if (IsExternalSSO_Enabled)
            {
                return SSO_Manager.ProxySSO<bool>("Membership", "SetPasswordExpiry", providerUserKey, username, expiryDate);
            }
            bool status = false;
            try
            {
                // attenzione che la tabella potrebbe non esistere su tutti i database
                if (expiryDate != null)
                {
                    expiryDate = Utility.Min(Utility.Max(expiryDate.Value, Utility.DateTimeMinValueDB), Utility.DateTimeMaxValueDB);
                }
                using (Ikon.Auth.ExtraDB.DataContext DB = Ikon.Auth.ExtraDB.DataContext.Factory())
                {
                    Guid uid;
                    Ikon.Auth.ExtraDB.aspnet_MembershipExt record = null;
                    if (providerUserKey != null && providerUserKey is Guid)
                    {
                        uid = (Guid)providerUserKey;
                    }
                    else
                    {
                        uid = DB.aspnet_Users.Where(r => string.Equals(r.LoweredUserName, username)).Select(r => r.UserId).FirstOrDefault();
                    }
                    record = DB.aspnet_MembershipExts.FirstOrDefault(r => r.UserId == uid);
                    //
                    if (record == null)
                    {
                        record = new Ikon.Auth.ExtraDB.aspnet_MembershipExt() { UserId = uid };
                        DB.aspnet_MembershipExts.InsertOnSubmit(record);
                    }
                    record.PasswordExpiryDate = expiryDate;
                    var chg = DB.GetChangeSet();
                    DB.SubmitChanges();
                    status = true;
                }
            }
            catch { }
            return status;
        }
        //
        // PasswordExpiry - end
        //


        //
        // ForceChangePassword - begin
        //
        public virtual bool CheckIfMustChangePassword(object providerUserKey, string username)
        {
            if (!ForceChangePasswordEnabled)
            {
                return true;
            }
            bool? forceChange = GetForceChangePassword(providerUserKey, username);
            if (forceChange != null)
            {
                return forceChange.Value;
            }
            return false;
        }

        public virtual bool? GetForceChangePassword(object providerUserKey, string username)
        {
            if (!ForceChangePasswordEnabled)
            {
                return null;
            }
            if (IsExternalSSO_Enabled)
            {
                return SSO_Manager.ProxySSO<bool?>("Membership", "GetForceChangePassword", providerUserKey, username);
            }
            //
            bool? forceChange = null;
            try
            {
                // attenzione che la tabella potrebbe non esistere su tutti i database
                using (Ikon.Auth.ExtraDB.DataContext DB = Ikon.Auth.ExtraDB.DataContext.Factory())
                {
                    if (providerUserKey != null && providerUserKey is Guid)
                    {
                        Guid uid = (Guid)providerUserKey;
                        forceChange = DB.aspnet_MembershipExts.Where(r => r.UserId == uid).Select(r => r.ForceChangePassword).FirstOrDefault();
                    }
                    else
                    {
                        forceChange = DB.aspnet_MembershipExts.Where(r => string.Equals(r.aspnet_Membership.aspnet_User.LoweredUserName, username)).Select(r => r.ForceChangePassword).FirstOrDefault();
                    }
                }
            }
            catch { }
            //
            return forceChange;
        }

        public virtual bool SetForceChangePassword(object providerUserKey, string username, bool? forceChange)
        {
            if (!ForceChangePasswordEnabled)
            {
                return false;
            }
            if (IsExternalSSO_Enabled)
            {
                return SSO_Manager.ProxySSO<bool>("Membership", "SetForceChangePassword", providerUserKey, username, forceChange);
            }
            bool status = false;
            try
            {
                // attenzione che la tabella potrebbe non esistere su tutti i database
                using (Ikon.Auth.ExtraDB.DataContext DB = Ikon.Auth.ExtraDB.DataContext.Factory())
                {
                    Guid uid;
                    Ikon.Auth.ExtraDB.aspnet_MembershipExt record = null;
                    if (providerUserKey != null && providerUserKey is Guid)
                    {
                        uid = (Guid)providerUserKey;
                    }
                    else
                    {
                        uid = DB.aspnet_Users.Where(r => string.Equals(r.LoweredUserName, username)).Select(r => r.UserId).FirstOrDefault();
                    }
                    record = DB.aspnet_MembershipExts.FirstOrDefault(r => r.UserId == uid);
                    //
                    if (record == null)
                    {
                        record = new Ikon.Auth.ExtraDB.aspnet_MembershipExt() { UserId = uid };
                        DB.aspnet_MembershipExts.InsertOnSubmit(record);
                    }
                    record.ForceChangePassword = forceChange;
                    var chg = DB.GetChangeSet();
                    DB.SubmitChanges();
                    status = true;
                }
            }
            catch { }
            return status;
        }
        //
        // PasswordExpiry - end
        //

    }

}
