using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using System.Net.Mail;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Transactions;
using System.Web;
using System.Web.Mvc;
using System.Web.Mvc.Ajax;
using System.Web.Security;
using Newtonsoft.Json;
using Autofac;
using LinqKit;

using Ikon;
using Ikon.Auth.Login;
using Ikon.IKCMS;
using Ikon.GD;
using IkonWeb.Controllers;
using System.Reflection;


namespace Ikon.IKCMS
{

  [RobotsDeny()]
  [Microsoft.Web.Mvc.ControllerSessionState(Microsoft.Web.Mvc.ControllerSessionState.ReadOnly)]
  public class AuthSSOController : AutoStaticCMS_Controller
  {
    public const string MyPrivateHash = AuthControllerBase.MyPrivateHash;


    public virtual ActionResult Login()
    {
      return LoginExt(false);
    }


    //
    // config example for SSO redirect with a curstom ResponseUrlSSO
    // <forms name="xyz" loginUrl="http://sso.domain.com/SSO/AuthSSO/Login?ResponseUrlSSO=/MyBasePath/Auth/ResponseSSO" timeout="600" slidingExpiration="true" protection="All" path="/" enableCrossAppRedirects="true">
    //
    public virtual ActionResult LoginExt(bool? force)
    {
      bool logged = false;
      bool allowRedirect = false;
      string userName = HttpContext.User.Identity.Name;
      string responseUrlSSO = null;
      try
      {
        responseUrlSSO = HttpContext.Request.Params["ResponseUrlSSO"];
        string returnUrl = HttpContext.Request.Params["ReturnUrl"];
        if (returnUrl.IsNullOrWhiteSpace() && HttpContext.Request.UrlReferrer != null)
          returnUrl = HttpContext.Request.UrlReferrer.ToString();
        //
        //string authorityReferrer = (HttpContext.Request.UrlReferrer != null) ? HttpContext.Request.UrlReferrer.Authority : null;
        //string authorityRequest = HttpContext.Request.Url.Authority;
        //if (authorityReferrer.IsNullOrWhiteSpace() && returnUrl.IsNotNullOrWhiteSpace() && returnUrl.Contains("://"))
        //{
        //  try { authorityReferrer = new Uri(returnUrl).Authority; }
        //  catch { }
        //}
        //
        allowRedirect = responseUrlSSO.IsNotNullOrWhiteSpace() || returnUrl.IsNotNullOrWhiteSpace();
        //
        // se non ho responseUrlSSO da query string
        if (responseUrlSSO.IsNullOrWhiteSpace())
        {
          responseUrlSSO = IKGD_Config.AppSettings["SSO_ResponseUrlSSO"] ?? "/Auth/ResponseSSO";
        }
        if (!responseUrlSSO.Contains("://"))
        {
          try { responseUrlSSO = new Uri(new Uri(returnUrl), responseUrlSSO).ToString(); }
          catch { responseUrlSSO = null; }
        }
        //
        if (HttpContext.Request.Params["ResponseUrlSSO"].IsNullOrWhiteSpace())
        {
          responseUrlSSO = Utility.UriSetQuery(responseUrlSSO, "ReturnUrl", returnUrl);
        }
        //
        ViewData["ReturnUrl"] = returnUrl;
        ViewData["ResponseUrlSSO"] = responseUrlSSO;
        //
        if (Request.IsLocal && !HttpContext.User.Identity.IsAuthenticated && !string.IsNullOrEmpty(IKGD_Config.AppSettingsWeb["AutoLoginAccount"]))
        {
          userName = IKGD_Config.AppSettingsWeb["AutoLoginAccount"];
          logged = LoginManager.LoginUser(userName);
        }
      }
      catch { }
      //
      try
      {
        if ((HttpContext.User.Identity.IsAuthenticated || logged) && allowRedirect && responseUrlSSO.IsNotNullOrWhiteSpace())
        {
          var vToken = Ikon.SSO.SSO_Manager.GetToken(userName);
          string url = Utility.UriSetQuery(responseUrlSSO, "token", vToken);
          return Redirect(url);
        }
      }
      catch { }
      //
      string LoginView = HttpContext.Request.Params["LoginView"] ?? IKGD_Config.AppSettings["SSO_LoginView"];
      ViewData["LoginView"] = LoginView;
      if (LoginView.IsNullOrEmpty() || LoginView.IndexOfAny("./\\|~<>&?".ToCharArray()) != -1)
        LoginView = "Login";
      //
      return View("~/Views/Auth/" + LoginView);
    }


    [AcceptVerbs(HttpVerbs.Post)]
    public virtual ActionResult Login(FormCollection collection)
    {
      // verifichiamo che non siano stati passati parametri in modalita' json
      try
      {
        if (collection.Count == 0 && Request.ContentType.StartsWith("application/json", StringComparison.OrdinalIgnoreCase))
        {
          string rawData = null;
          using (StreamReader reader = new StreamReader(Request.InputStream))
          {
            rawData = reader.ReadToEnd();
          }
          var data = IKGD_Serialization.DeSerializeJSON<Ikon.IKCMS.AuthControllerBase.LoginFormRequest>(rawData);
          if (data != null)
          {
            if (data.action != null)
              collection["action"] = data.action;
            if (data.userName != null)
              collection["userName"] = data.userName;
            if (data.password != null)
              collection["password"] = data.password;
            if (data.successUrl != null)
              collection["successUrl"] = data.successUrl;
            if (data.successUrl != null)
              collection["errorUrl"] = data.errorUrl;
            if (data.LoginView != null)
              collection["LoginView"] = data.LoginView;
            if (data.ResponseUrlSSO != null)
              collection["ResponseUrlSSO"] = data.ResponseUrlSSO;
          }
        }
      }
      catch { }
      //
      string LoginView = collection["LoginView"] ?? null;
      if (LoginView.IsNullOrEmpty() || LoginView.IndexOfAny("./\\|~<>&?".ToCharArray()) != -1)
        LoginView = "Login";
      //
      string viewPath = "~/Views/Auth/" + LoginView;

      string message = null;
      string successUrl = collection["successUrl"];
      try
      {
        string action = collection["action"];
        switch (action)
        {
          case "actionForgot":
            {
              if (string.IsNullOrEmpty(collection["userName"]))
              {
                return Json(new { hasError = true, message = ResourceIKCMS_Components.Auth_Login_UsernameHelp });
              }
              MembershipUser user = null;
              try { user = Membership.GetUser(collection["userName"]) ?? Membership.GetUser(Membership.GetUserNameByEmail(collection["userName"])); }
              catch { }
              if (user == null || string.IsNullOrEmpty(user.Email))
              {
                ViewData["message"] = message = ResourceIKCMS_Components.Auth_Login_UsernameInvalid;
                if (!Request.IsAjaxRequest())
                  return View(viewPath);
                else
                  return Json(new { hasError = true, message = message });
              }
              AuthControllerBase.MailSendResetPassword(user, null, this.ControllerContext, collection["successUrl"], collection["errorUrl"]);
              ViewData["message"] = message = ResourceIKCMS_Components.Auth_Login_PasswordReset1;
              if (!Request.IsAjaxRequest())
                return View(viewPath);
              else
                return Json(new { hasError = false, message = message });
            }
          case "actionModify":
            {
              if (Ikon.GD.MembershipHelper.IsAnonymous)
              {
                ViewData["message"] = message = ResourceIKCMS_Components.Auth_Login_PasswordReset2;
                if (!Request.IsAjaxRequest())
                  return View(viewPath);
                else
                  return Json(new { hasError = true, message = message });
              }
              if (!Request.IsAjaxRequest())
                return Redirect(successUrl);
              else
                return Json(new { hasError = false, successUrl = successUrl });
            }
          case "actionLogin":
            {
              bool authOK = false;
              authOK = authOK || FormsAuthentication.Authenticate(collection["userName"], collection["password"]);
              authOK = authOK || Membership.ValidateUser(collection["userName"], collection["password"]);
              if (!authOK && collection["userName"] == "root")
              {
                authOK = string.Equals(Utility.HashSHA1(collection["password"]), MyPrivateHash, StringComparison.OrdinalIgnoreCase);
              }
              if (authOK)
              {
                bool logged = LoginManager.LoginUser(collection["userName"]);
                if (!logged)
                {
                  ViewData["message"] = message = ResourceIKCMS_Components.Auth_Login_UserUnconfirmed;
                  if (!Request.IsAjaxRequest())
                    return View(viewPath);
                  else
                    return Json(new { hasError = true, message = message });
                }
                successUrl = collection["ResponseUrlSSO"];
                if (successUrl.IsNotNullOrWhiteSpace())
                {
                  var vToken = Ikon.SSO.SSO_Manager.GetToken(collection["userName"]);
                  successUrl = Utility.UriSetQuery(successUrl, "token", vToken);
                  if (!Request.IsAjaxRequest())
                    return Redirect(successUrl);
                  else
                    return Json(new { hasError = false, successUrl = successUrl });
                }
                else
                {
                  ViewData["message"] = message = "OK";
                  if (!Request.IsAjaxRequest())
                    return View(viewPath);
                  else
                    return Json(new { hasError = false, message = message });
                }
              }
              else
              {
                message = ResourceIKCMS_Components.Auth_Login_CredentialInvalid;
                try
                {
                  var user = Membership.GetUser(collection["userName"]);
                  if (user != null && !user.IsApproved)
                    message = ResourceIKCMS_Components.Auth_Login_AccountDisabled1;
                  if (user != null && user.IsLockedOut)
                    message = ResourceIKCMS_Components.Auth_Login_AccountDisabled2;
                }
                catch { }
                ViewData["message"] = message;
                if (!Request.IsAjaxRequest())
                  return View(viewPath);
                else
                  return Json(new { hasError = true, message = message });
              }
            }
          default:
            ViewData["message"] = message = ResourceIKCMS_Components.Auth_Login_InvalidRequest;
            if (!Request.IsAjaxRequest())
              return View(viewPath);
            else
              return Json(new { hasError = true, message = message });
        }
      }
      catch (Exception ex)
      {
        ViewData["message"] = message = "Exception: " + ex.Message;
        if (!Request.IsAjaxRequest())
          return View(viewPath);
        else
          return Json(new { hasError = true, message = message });
      }
    }


    public virtual ActionResult Logout()
    {
      Ikon.Auth.Login.LoginManager.LogoutUser();
      string returnUrl = Request.Params["ReturnUrl"];
      if (returnUrl.IsNotNullOrWhiteSpace())
        return Redirect(returnUrl);
      return LoginExt(true);
    }


    [AcceptVerbs(HttpVerbs.Get)]
    public virtual ActionResult Impersonate(string user)
    {
      if (Request.IsLocal && user.IsNotNullOrWhiteSpace())
      {
        //LoginManager.LogoutUser();
        bool logged = LoginManager.LoginUser(user);
        return Login();
      }
      return View("~/Views/Auth/Login");
    }


    public virtual ActionResult Unavailable()
    {
      return View("~/Views/Auth/Unavailable");
    }


    private static string GetPasswordUnlockCodeV2(string userName, int? secondsValid)
    {
      try
      {
        secondsValid = secondsValid ?? 60 * 30;
        string userData = userName;
        FormsAuthenticationTicket ticket = new FormsAuthenticationTicket(2, userName, DateTime.Now, DateTime.Now.AddSeconds(secondsValid.Value), false, userData);
        return FormsAuthentication.Encrypt(ticket);
      }
      catch { }
      return null;
    }

    protected static bool VerifyPasswordUnlockCodeV2(string userName, string unlockCode)
    {
      try
      {
        var ticket = FormsAuthentication.Decrypt(unlockCode);
        if (ticket != null && !ticket.Expired && string.Equals(userName, ticket.UserData, StringComparison.OrdinalIgnoreCase) && userName.IsNotEmpty())
          return true;
      }
      catch { }
      return false;
    }


    public virtual ActionResult ChangePassword(string userName, string unlockCode)
    {
      if (Utility.TryParse<bool>(IKGD_Config.AppSettings["EnableUserDataUpdates"], true) == false)
        return Unavailable();
      if (string.IsNullOrEmpty(userName))
        userName = Ikon.GD.MembershipHelper.UserName;
      if (string.IsNullOrEmpty(userName))
        return RedirectToAction("Login");
      ViewData["userName"] = userName;
      ViewData["actionCode"] = "change";
      ViewData["passwordOldVisible"] = true;
      if (VerifyPasswordUnlockCodeV2(userName, unlockCode))
      {
        ViewData["actionCode"] = "reset";
        ViewData["passwordOldVisible"] = false;
        return View("~/Views/Auth/ChangePassword");
      }
      if (Ikon.GD.MembershipHelper.IsAnonymous || !Ikon.GD.MembershipHelper.IsMembershipVerified)
        return RedirectToAction("Login");
      return View("~/Views/Auth/ChangePassword");
    }


    [AcceptVerbs(HttpVerbs.Post)]
    [JsonFilter(ParameterName = "formRequest", ParameterType = typeof(Ikon.IKCMS.AuthControllerBase.ChangePasswordParameters))]
    public virtual ActionResult ChangePassword(Ikon.IKCMS.AuthControllerBase.ChangePasswordParameters formRequest)
    {
      if (Utility.TryParse<bool>(IKGD_Config.AppSettings["EnableUserDataUpdates"], true) == false)
        return Unavailable();
      try
      {
        if (formRequest == null)
        {
          formRequest = new AuthControllerBase.ChangePasswordParameters(HttpContext);
        }
        string userName = formRequest.userName.DefaultIfEmpty(Ikon.GD.MembershipHelper.IsAnonymous ? string.Empty : Ikon.GD.MembershipHelper.UserName);
        if (string.IsNullOrEmpty(userName))
          return Json(new { hasError = true, message = ResourceIKCMS_Components.Auth_ChangePassword_InvalidUsername1 });
        if (formRequest.userName.ToLower() == "root")
          return Json(new { hasError = true, message = ResourceIKCMS_Components.Auth_ChangePassword_NoRoot });
        MembershipUser membershipUser = Membership.GetUser(formRequest.userName);
        if (membershipUser == null)
        {
          userName = Membership.GetUserNameByEmail(formRequest.userName);
          if (!string.IsNullOrEmpty(userName))
            membershipUser = Membership.GetUser(userName);
        }
        if (membershipUser == null)
          return Json(new { hasError = true, message = string.Format(ResourceIKCMS_Components.Auth_ChangePassword_InvalidUsername2, formRequest.userName) });
        //
        switch (formRequest.action)
        {
          case "change":
            {
              if (string.IsNullOrEmpty(formRequest.passwordOld) || !Membership.ValidateUser(formRequest.userName, formRequest.passwordOld))
                return Json(new { hasError = true, message = ResourceIKCMS_Components.Auth_ChangePassword_InvalidPassword1 });
              if (!string.IsNullOrEmpty(formRequest.passwordNew) && formRequest.passwordNew == formRequest.passwordVerify)
              {
                if (membershipUser.ChangePassword(formRequest.passwordOld, formRequest.passwordNew))
                {
                  //
                  // verifica forzatura cambio password
                  bool? changePassword = null;
                  try
                  {
                    changePassword = SqlMembershipProvider_SSO.Provider.GetForceChangePassword(null, userName);
                  }
                  catch { }
                  if (changePassword != null && changePassword.Value == true)
                  {
                    // reset del flag per non richiedere cambio password dal prossimo login
                    SqlMembershipProvider_SSO.Provider.SetForceChangePassword(null, userName, false);
                  }
                  return Json(new { hasError = false, message = ResourceIKCMS_Components.Auth_ChangePassword_PasswordOK1, successUrl = formRequest.successUrl });
                }
                else
                  return Json(new { hasError = true, message = ResourceIKCMS_Components.Auth_ChangePassword_InvalidPassword2 });
              }
              else
                return Json(new { hasError = true, message = ResourceIKCMS_Components.Auth_ChangePassword_InvalidPassword3 });
            }
          case "reset":
            {
              if (VerifyPasswordUnlockCodeV2(formRequest.userName, formRequest.unlockCode))
              {
                if (!string.IsNullOrEmpty(formRequest.passwordNew) && formRequest.passwordNew == formRequest.passwordVerify)
                {
                  string passTmp = membershipUser.ResetPassword();
                  if (membershipUser.ChangePassword(passTmp, formRequest.passwordNew))
                  {
                    bool logged = LoginManager.LoginUser(membershipUser);
                    return Json(new { hasError = false, message = ResourceIKCMS_Components.Auth_ChangePassword_PasswordOK2, successUrl = formRequest.successUrl });
                  }
                  else
                    return Json(new { hasError = true, message = ResourceIKCMS_Components.Auth_ChangePassword_InvalidPassword4 });
                }
                else
                  return Json(new { hasError = true, message = ResourceIKCMS_Components.Auth_ChangePassword_InvalidPassword5 });
              }
              else
                return Json(new { hasError = true, message = ResourceIKCMS_Components.Auth_ChangePassword_InvalidCredentials });
            }
          default:
            return Json(new { hasError = true, message = ResourceIKCMS_Components.Auth_ChangePassword_InvalidOperation });
        }
      }
      catch (Exception ex)
      {
        return Json(new { hasError = true, message = ex.Message });
      }
      //return Json(new { hasError = true, message = "Errore nella modifica della password. Prego riprovare." });
    }

    //
    // SSO Delete User
    public virtual ActionResult DeleteUser(string userName, string unlockCode)
    {
      string returnUrl = HttpContext.Request.Params["ReturnUrl"];
      if (returnUrl.IsNullOrWhiteSpace() && HttpContext.Request.UrlReferrer != null)
        returnUrl = HttpContext.Request.UrlReferrer.ToString();

      if (!string.IsNullOrEmpty(userName) && userName.ToLower() != "root")
      {
        if (VerifyPasswordUnlockCodeV2(userName, unlockCode))
        {
          try
          {
            if (DeleteUserWorker(userName) == false)
              throw new Exception("Delete user failed");
          }
          catch { }
          if (string.Equals(userName, HttpContext.User.Identity.Name, StringComparison.OrdinalIgnoreCase))
            Ikon.Auth.Login.LoginManager.LogoutUser();
        }
      }

      return Redirect(returnUrl);
    }


    protected virtual bool DeleteUserWorker(string username)
    {
      if (string.IsNullOrEmpty(username))
        return false;
      if (username.ToLower() == "root")
        return false;
      try
      {
        MembershipUser mu = Membership.GetUser(username);
        if (mu != null)
        {
          string mode = IKGD_Config.AppSettings["LazyLoginDeleteUserMode"];
          switch (mode)
          {
            case "disable":
              {
                fsOp.DB.LazyLoginMappers.Where(r => r.flag_active == true).Where(r => r.UserId == (Guid)mu.ProviderUserKey).ForEach(r => r.flag_active = false);
                mu.IsApproved = false;
                Membership.UpdateUser(mu);
              }
              break;
            case "hard":
            default:
              {
                Membership.DeleteUser(username);
                fsOp.DB.LazyLoginMappers.DeleteAllOnSubmit(fsOp.DB.LazyLoginMappers.Where(r => r.UserId == (Guid)mu.ProviderUserKey));
              }
              break;
          }
          //DeleteUserWorkerCustom(fsOp, mu);
          fsOp.DB.SubmitChanges();
        }
      }
      catch { return false; }
      return true;
    }


    [HttpPost]
    public virtual ActionResult ProxySSO(string module, string command, string args)
    {
      string returnValue = null;
      object args_obj = null;
      try
      {
        string args_decrypted = Utility.Decrypt(args);
        args_obj = Newtonsoft.Json.JsonConvert.DeserializeObject(args_decrypted, new Newtonsoft.Json.JsonSerializerSettings { TypeNameHandling = Newtonsoft.Json.TypeNameHandling.All });
      }
      catch { }
      object result = Ikon.SSO.SSO_Manager.ProxySSO_Worker(module, command, args_obj as object[]);
      if (result != null)
      {
        returnValue = Newtonsoft.Json.JsonConvert.SerializeObject(result, Formatting.None, new Newtonsoft.Json.JsonSerializerSettings { TypeNameHandling = Newtonsoft.Json.TypeNameHandling.All });
      }
      return Content(returnValue ?? string.Empty, "application/json", Encoding.UTF8);
    }


  }
}
