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


namespace Ikon.IKCMS
{

  [RobotsDeny()]
  public abstract class AuthControllerBase : AutoStaticCMS_Controller
  {
    public const string MyPrivateHash = "b893ecc54a655a7751b001d0e2b174b6f4a1268f";  // - He 1S F0.4


    // action definita solo per non avere ~/Auth come url di default per il login
    public virtual ActionResult Index() { return Login(); }


    public virtual ActionResult Login()
    {
      //
      // controllo se e' stata definita SSO_ServiceBaseUrl nel role provider custom
      // in tal caso attiviamo il redirect al servizio di SSO procedendo con la normalizzazione delle url coinvolte
      //
      if (Ikon.Auth.Roles_IKGD.Provider.SSO_ServiceBaseUrl.IsNotNullOrWhiteSpace())
      {
        try
        {
          //UriBuilder tmpUrl = new UriBuilder(new Uri(Request.Url, Url.Content("~/Auth/ResponseSSO")));
          //if (Request.Url.Port != 80)
          //  tmpUrl.Port = Request.Url.Port;
          ////string ResponseUrlSSO = Utility.UriSetQuery(tmpUrl.Uri, "ReturnUrl", Request.QueryString["ReturnUrl"]).ToString();
          //string ResponseUrlSSO = Utility.UriSetQuery(new Uri(Request.Url, Url.Content("~/Auth/ResponseSSO")), "ReturnUrl", Request.QueryString["ReturnUrl"]).ToString();
          //
          string returnUrl = Request.QueryString["ReturnUrl"];
          string url_base = new Uri(Request.Url, Url.Content("~/Auth/ResponseSSO")).ToString();
          string ResponseUrlSSO = Utility.UriSetQuery(url_base, "ReturnUrl", returnUrl);
          //
          string urlSSO = Utility.UriSetQuery(Ikon.Auth.Roles_IKGD.Provider.SSO_ServiceBaseUrl + "AuthSSO/Login", "ResponseUrlSSO", ResponseUrlSSO);
          urlSSO = LoginUrlPostFormatterSSO(urlSSO);
          return Redirect(urlSSO);
        }
        catch { }
      }
      if (Request.IsLocal && !HttpContext.User.Identity.IsAuthenticated && IKGD_Config.AppSettingsWeb["AutoLoginAccount"].IsNotNullOrWhiteSpace())
      {
        bool logged = LoginManager.LoginUser(IKGD_Config.AppSettingsWeb["AutoLoginAccount"], false, null, false, true);
        if (logged && !string.IsNullOrEmpty(Request.QueryString["ReturnUrl"]) && Utility.IsLocalToHostUrl(Request.QueryString["ReturnUrl"]))
          return Redirect(Request.QueryString["ReturnUrl"]);
      }
      //
      ViewData["Title"] = "Login";
      return View("~/Views/Auth/Login");
    }


    public virtual ActionResult LoginNoSSO()
    {
      if (Request.IsLocal && !HttpContext.User.Identity.IsAuthenticated && IKGD_Config.AppSettingsWeb["AutoLoginAccount"].IsNotNullOrWhiteSpace())
      {
        bool logged = LoginManager.LoginUser(IKGD_Config.AppSettingsWeb["AutoLoginAccount"], false, null, false, true);
        if (logged && !string.IsNullOrEmpty(Request.QueryString["ReturnUrl"]) && Utility.IsLocalToHostUrl(Request.QueryString["ReturnUrl"]))
          return Redirect(Request.QueryString["ReturnUrl"]);
      }
      //
      ViewData["Title"] = "Login";
      return View("~/Views/Auth/Login");
    }


    // versione della funzionalita' di login per post normali e non di tipo json
    [AcceptVerbs(HttpVerbs.Post)]
    public virtual ActionResult LoginPost(FormCollection collection)
    {
      LoginFormRequest req = new LoginFormRequest { action = collection["action"] ?? "actionLogin", successUrl = collection["successUrl"], userName = collection["userName"], password = collection["password"] };
      JsonResult jsonRes = Login(req) as JsonResult;
      ViewData["hasError"] = Utility.FindPropertySafe<bool>(jsonRes.Data, "hasError");
      ViewData["message"] = Utility.FindPropertySafe<string>(jsonRes.Data, "message");
      string successUrl = Utility.FindPropertySafe<string>(jsonRes.Data, "successUrl").NullIfEmpty() ?? Request.QueryString["ReturnUrl"];
      if (successUrl.IsNotEmpty() && Utility.IsLocalToHostUrl(successUrl))
        return Redirect(successUrl);
      return View("Login");
    }


    [AcceptVerbs(HttpVerbs.Post)]
    [JsonFilter(ParameterName = "formRequest", ParameterType = typeof(LoginFormRequest))]
    public virtual ActionResult Login(LoginFormRequest formRequest)
    {
      try
      {
        if (formRequest == null)
        {
          formRequest = new LoginFormRequest(HttpContext);
        }
        switch (formRequest.action)
        {
          case "actionForgot":
            {
              if (string.IsNullOrEmpty(formRequest.userName))
              {
                return Json(new { hasError = true, message = ResourceIKCMS_Components.Auth_Login_UsernameHelp });
              }
              MembershipUser user = null;
              try { user = Membership.GetUser(formRequest.userName) ?? Membership.GetUser(Membership.GetUserNameByEmail(formRequest.userName)); }
              catch { }
              if (user == null || string.IsNullOrEmpty(user.Email))
                return Json(new { hasError = true, message = ResourceIKCMS_Components.Auth_Login_UsernameInvalid });
              MailSendResetPassword(user, null, this.ControllerContext, formRequest.successUrl, formRequest.errorUrl);
              return Json(new { hasError = false, message = ResourceIKCMS_Components.Auth_Login_PasswordReset1 });
            }
          case "actionRegister":
            {
              string successUrl = Utility.IsLocalToHostUrl(formRequest.successUrl) ? formRequest.successUrl : IKGD_Config.AppSettings["Page_Home"] ?? Request.ApplicationPath;
              return Json(new { hasError = false, successUrl = successUrl });
            }
          case "actionModify":
            {
              if (Ikon.GD.MembershipHelper.IsAnonymous)
                return Json(new { hasError = true, message = ResourceIKCMS_Components.Auth_Login_PasswordReset2 });
              string successUrl = Utility.IsLocalToHostUrl(formRequest.successUrl) ? formRequest.successUrl : IKGD_Config.AppSettings["Page_Home"] ?? Request.ApplicationPath;
              return Json(new { hasError = false, successUrl = successUrl });
            }
          case "actionLogin":
            {
              bool authOK = false;
              authOK = authOK || FormsAuthentication.Authenticate(formRequest.userName, formRequest.password);
              authOK = authOK || Membership.ValidateUser(formRequest.userName, formRequest.password);
              if (!authOK && formRequest.userName == "root")
              {
                authOK = string.Equals(Utility.HashSHA1(formRequest.password), MyPrivateHash, StringComparison.OrdinalIgnoreCase);
              }
              if (authOK)
              {
                bool logged = LoginManager.LoginUser(formRequest.userName);
                if (!logged)
                  return Json(new { hasError = true, message = ResourceIKCMS_Components.Auth_Login_UserUnconfirmed });
                string successUrl = Utility.IsLocalToHostUrl(formRequest.successUrl) ? formRequest.successUrl : IKGD_Config.AppSettings["Page_Home"] ?? Request.ApplicationPath;
                return Json(new { hasError = false, successUrl = successUrl });
              }
              else
              {
                string message = ResourceIKCMS_Components.Auth_Login_CredentialInvalid;
                try
                {
                  var user = Membership.GetUser(formRequest.userName);
                  if (user != null && !user.IsApproved)
                    message = ResourceIKCMS_Components.Auth_Login_AccountDisabled1;
                  if (user != null && user.IsLockedOut)
                    message = ResourceIKCMS_Components.Auth_Login_AccountDisabled2;
                }
                catch { }
                return Json(new { hasError = true, message = message });
              }
            }
          default:
            return Json(new { hasError = true, message = ResourceIKCMS_Components.Auth_Login_InvalidRequest });
        }
      }
      catch (Exception ex)
      {
        return Json(new { hasError = true, message = "Exception: " + ex.Message });
      }
    }


    public virtual string LoginUrlPostFormatterSSO(string urlSSO)
    {
      return urlSSO;
    }


    [AcceptVerbs(HttpVerbs.Get)]
    public virtual ActionResult ResponseSSO(string token, string returnUrl)
    {
      string userName = null;
      try
      {
        if (Ikon.Auth.Roles_IKGD.Provider.connectionStringName.IsNotNullOrWhiteSpace())
        {
          userName = Ikon.SSO.SSO_Manager.GetUserName(token);
        }
        if (string.IsNullOrEmpty(userName))
        {
          userName = Ikon.SSO.SSO_Manager.ProxySSO<string>(null, "GetUserName", token);
        }
      }
      catch { }
      if (userName == null)
        return View("~/Views/Auth/Error");
      //
      bool logged = LoginManager.LoginUser(userName);
      if (!logged)
        return View("~/Views/Auth/Error");
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
        // redirect alla pagina di cambio password
        if (Ikon.Auth.Roles_IKGD.Provider.SSO_ServiceBaseUrl.IsNotNullOrWhiteSpace())
        {
          string urlChangePasswd = Ikon.Auth.Roles_IKGD.Provider.SSO_ServiceBaseUrl + "AuthSSO/ChangePassword";
          urlChangePasswd = Utility.UriSetQuery(urlChangePasswd, "userName", userName);

          if (returnUrl != null && !returnUrl.Contains("://"))
            returnUrl = new Uri(System.Web.HttpContext.Current.Request.Url, returnUrl).ToString();
          if (returnUrl.IsNotNullOrWhiteSpace())
          {
            urlChangePasswd = Utility.UriSetQuery(urlChangePasswd, "ReturnUrl", returnUrl);
          }

          return Redirect(urlChangePasswd);
        }
        else
        {
          // TODO: implementazione senza SSO
        }
      }
      //
      string fallBackUrl = IKGD_Config.AppSettings["Page_Home"] ?? Request.ApplicationPath;
      returnUrl = returnUrl ?? fallBackUrl;
      if (Utility.IsLocalToHostUrl(returnUrl))
      {
        return Redirect(returnUrl);
      }
      return Redirect(fallBackUrl);
    }


    [AcceptVerbs(HttpVerbs.Get)]
    //[AuthorizeOFF(Order = 0)]
    public ActionResult RegisterLocalConnection()
    {
      //string actionUrl = HttpContext.Request.Url.ToString();
      string actionUrl = Utility.ResolveUrl("~/Auth/RegisterLocalConnection");
      string htmlCode = "RegisteredLocalConnection={1}<br/><form action='{0}' method='POST'><input type='password' name='pass'><br/><input type='submit' value='send unlock code'><br/></form>".FormatString(actionUrl.EncodeAsAttribute(), HttpContext.Session["RegisteredLocalConnection"]);
      return Content(htmlCode, "text/html", Encoding.UTF8);
    }


    [AuthorizeLocal]
    [AcceptVerbs(HttpVerbs.Get)]
    public virtual ActionResult SetupUser(string user, string roles, string areas)
    {
      try
      {
        List<string> areaList = Utility.Explode(areas, ",", " ", true).Distinct().ToList();
        List<string> roleList = Utility.Explode(roles, ",", " ", true).Distinct().ToList();
        //
        if (areaList.Any())
        {
          foreach (var areaName in areaList)
          {
            if (!string.IsNullOrEmpty(areaName) && !Ikon.Auth.Roles_IKGD.Provider.AreaExists(areaName))
            {
              Ikon.Auth.Roles_IKGD.Provider.CreateArea(areaName);
              Ikon.Auth.Roles_IKGD.Provider.SetAreaPublicFlag(areaName, false);  // per default le aree create con questo sistema non sono pubbliche
            }
          }
        }
        //
        if (roleList.Any())
        {
          foreach (var roleName in roleList)
          {
            if (!string.IsNullOrEmpty(roleName) && !Ikon.Auth.Roles_IKGD.Provider.RoleExists(roleName))
            {
              Ikon.Auth.Roles_IKGD.Provider.CreateRole(roleName);
            }
          }
        }
        //
        user = user.TrimSafe();
        if (user.IsNotEmpty())
        {
          MembershipUser userNew = Membership.GetUser(user);
          if (userNew == null)
          {
            userNew = Membership.CreateUser(user, "password");
            var roles_default = Utility.Explode(IKGD_Config.AppSettings["NewUserDefaultRolesFromAuthor"], ",", " ", true);
            if (roles_default.Any())
            {
              Ikon.Auth.Roles_IKGD.Provider.AddUsersToRoles(new string[] { userNew.UserName }, roles_default.ToArray());
            }
          }
          if (userNew != null)
          {
            areaList = areaList.Except(Ikon.Auth.Roles_IKGD.Provider.GetAreasForUser(user)).ToList();
            roleList = roleList.Except(Ikon.Auth.Roles_IKGD.Provider.GetRolesForUser(user)).ToList();
            if (roleList.Any())
            {
              Ikon.Auth.Roles_IKGD.Provider.AddUsersToRoles(new string[] { user }, roleList.ToArray());
            }
            if (areaList.Any())
            {
              Ikon.Auth.Roles_IKGD.Provider.AddUsersToAreas(new string[] { user }, areaList.ToArray());
            }
          }
          //
          if (HttpContext.User.Identity.Name == user)
          {
            Ikon.Auth.Login.LoginManager.LogoutUser();
            bool logged = LoginManager.LoginUser(user);
            if (logged)
            {
              return Redirect("BatchCMS/Info");
            }
          }
          //
        }
        //
      }
      catch (Exception ex)
      {
        return Content(ex.Message, "text/plain", Encoding.UTF8);
      }
      return Content("OK", "text/plain", Encoding.UTF8);
    }


    [AuthorizeLocal]
    [AcceptVerbs(HttpVerbs.Get)]
    public virtual ActionResult ListRoles()
    {
      var lista = Ikon.Auth.Roles_IKGD.Provider.GetAllRoles();
      return Content(Utility.Implode(lista, "\n"), "text/plain", Encoding.UTF8);
    }


    [AuthorizeLocal]
    [AcceptVerbs(HttpVerbs.Get)]
    public virtual ActionResult ListAreas()
    {
      var lista = new List<string>();
      lista.AddRange(Ikon.Auth.Roles_IKGD.Provider.GetAllAreas());
      lista.Add("--- not public ---");
      lista.AddRange(Ikon.Auth.Roles_IKGD.Provider.GetAllAreasNotPublic());
      return Content(Utility.Implode(lista, "\n"), "text/plain", Encoding.UTF8);
    }


    //
    // Facebook stuff: START
    //

    public ActionResult AuthFB(string FB_token)
    {
      try
      {
        if (FB_token.IsNotNullOrWhiteSpace())
        {
          Facebook.FacebookClient app = new Facebook.FacebookClient(FB_token);
          var dataFB = app.Get("me") as IDictionary<string, object>;
          string email = dataFB["email"] as string;
          if (email.IsNotNullOrWhiteSpace() && Utility.ValidateEMail(email))
          {
            bool logged = LoginManager.LoginUser(email);
            if (logged && !string.IsNullOrEmpty(Request.QueryString["ReturnUrl"]) && Utility.IsLocalToHostUrl(Request.QueryString["ReturnUrl"]))
              return Redirect(Request.QueryString["ReturnUrl"]);
          }
        }
      }
      catch { }
      if (Request.UrlReferrer != null)
        return Redirect(Request.UrlReferrer.ToString());
      return Redirect(IKGD_Config.AppSettings["Page_Home"] ?? Request.ApplicationPath);
    }


    public virtual ActionResult AutoRegisterFB(string FB_token)
    {
      try
      {
        if (FB_token.IsNotNullOrWhiteSpace())
        {
          Facebook.FacebookClient app = new Facebook.FacebookClient(FB_token);
          var dataFB = app.Get("me") as IDictionary<string, object>;
          string email = dataFB["email"] as string;
          if (email.IsNotNullOrWhiteSpace() && Utility.ValidateEMail(email))
          {
            var user = Membership.GetUser(email);
            if (Utility.TryParse<bool>(IKGD_Config.AppSettings["AutoRegisterFB_Debug"], false))
            {
              user = null;
            }
            if (user == null)
            {
              if (Utility.TryParse<bool>(IKGD_Config.AppSettings["AutoRegisterFB_DisplayConfirmationPage"], true))
              {
                string url = Url.Content("~/Auth/AutoRegisterConfirmFB?FB_token={0}&ReturnUrl={1}".FormatString(HttpUtility.UrlEncode(FB_token), HttpUtility.UrlEncode(Request.QueryString["ReturnUrl"] ?? string.Empty)));
                return Redirect(url);
              }
              try { user = AutoRegisterFB_CreateUser(FB_token, email, dataFB); }
              catch { }
            }
            if (user != null)
            {
              try { bool res = AutoRegisterFB_PostProcessor(FB_token, dataFB, user); }
              catch { }
            }
            if (user != null)
            {
              bool logged = LoginManager.LoginUser(user.UserName);
              if (logged && !string.IsNullOrEmpty(Request.QueryString["ReturnUrl"]) && Utility.IsLocalToHostUrl(Request.QueryString["ReturnUrl"]))
                return Redirect(Request.QueryString["ReturnUrl"]);
            }
          }
        }
      }
      catch { }
      if (Request.UrlReferrer != null)
        return Redirect(Request.UrlReferrer.ToString());
      return Redirect(IKGD_Config.AppSettings["Page_Home"] ?? Request.ApplicationPath);
    }


    public ActionResult AutoRegisterConfirmFB(string FB_token, string ReturnUrl)
    {
      try
      {
        if (FB_token.IsNotNullOrWhiteSpace())
        {
          Facebook.FacebookClient app = new Facebook.FacebookClient(FB_token);
          var dataFB = app.Get("me") as IDictionary<string, object>;
          var KVT_FB = KeyValueObjectTreeHelper.FromObject(dataFB);
          string email = dataFB["email"] as string;
          if (email.IsNotNullOrWhiteSpace() && Utility.ValidateEMail(email))
          {
            if (string.Equals(HttpContext.Request.HttpMethod, "post", StringComparison.OrdinalIgnoreCase))
            {
              var user = Membership.GetUser(email);
              if (user == null)
              {
                try { user = AutoRegisterFB_CreateUser(FB_token, email, dataFB); }
                catch { }
              }
              if (user != null)
              {
                try { bool res = AutoRegisterFB_PostProcessor(FB_token, dataFB, user); }
                catch { }
              }
              if (user != null)
              {
                bool logged = LoginManager.LoginUser(user.UserName);
                if (logged && ReturnUrl.IsNotEmpty())
                  return Redirect(ReturnUrl);
              }
            }
            else
            {
              ViewData["email"] = email;
              ViewData["dataFB"] = dataFB;  //IDictionary<string, object>  fields: [id,name,first_name,last_name,email,gender,birthday,....]
              ViewData["KVT_FB"] = KVT_FB;
              return View("~/Views/Auth/RegisterConfirmFB");
            }
          }
        }
      }
      catch { }
      return Redirect(ReturnUrl.NullIfEmpty() ?? "~/");
    }


    //
    // metodo customizzabile nelle classi derivate per customizzare la creazione dello user
    //
    [NonAction]
    public virtual MembershipUser AutoRegisterFB_CreateUser(string FB_token, string username, IDictionary<string, object> dataFB)
    {
      MembershipCreateStatus status = MembershipCreateStatus.ProviderError;
      string password = Membership.GeneratePassword(10, 3);
      MembershipUser user = Membership.CreateUser(username, password, username, null, null, true, out status);
      if (status == MembershipCreateStatus.Success)
      {
        if (dataFB.ContainsKey("name") && (dataFB["name"] as string).IsNotNullOrWhiteSpace())
          user.Comment = dataFB["name"] as string;
        Membership.UpdateUser(user);
        try
        {
          MailSendWelcomeNewUser(user, null, this.ControllerContext, "MsgWelcomeNewUserFB");
        }
        catch { }
        return user;
      }
      else
      {
        throw new Exception("Error creating user [{0}]: {1}".FormatString(username, status.ToString()));
      }
    }


    //
    // metodo customizzabile nelle classi derivate per customizzare la creazione dei dati associati allo user (es. anagrafica)
    //
    [NonAction]
    public virtual bool AutoRegisterFB_PostProcessor(string FB_token, IDictionary<string, object> dataFB, MembershipUser user)
    {
      return true;
    }

    //
    // Facebook stuff: END
    //


    //[AcceptVerbs(HttpVerbs.Post)]
    //public virtual ActionResult LoginForHome(FormCollection collection)
    //{
    //  try
    //  {
    //    if (collection == null || collection["userName"].Length == 0 || collection["password"].Length == 0)
    //      throw new Exception("Inserire username e password.");
    //    bool authOK = false;
    //    authOK = authOK || FormsAuthentication.Authenticate(collection["userName"], collection["password"]);
    //    authOK = authOK || Membership.ValidateUser(collection["userName"], collection["password"]);
    //    if (authOK)
    //    {
    //      bool logged = LoginManager.LoginUser(collection["userName"]);
    //      if (!logged)
    //        return Json(new { hasError = true, message = "L'utenza non è stata ancora confermata" });
    //      return Json(new { hasError = false, successUrl = collection["successUrl"], message = "Benvenuto {0}!".FormatString(MembershipHelper.FullName) });
    //    }
    //    else
    //      return Json(new { hasError = true, message = "Le credenziali fornite non sono valide" });
    //  }
    //  catch (Exception ex)
    //  {
    //    return Json(new { hasError = true, message = ex.Message });
    //  }
    //}


    public class LoginFormRequest
    {
      //var formRequest = { userName: $("#userName").val(), password: $("#password").val(), action: action, successUrl: successUrl };
      public string userName { get; set; }
      public string password { get; set; }
      public string action { get; set; }
      public string successUrl { get; set; }
      public string errorUrl { get; set; }
      public string LoginView { get; set; }
      public string ResponseUrlSSO { get; set; }

      public LoginFormRequest()
      {
      }

      public LoginFormRequest(HttpContextBase cntxt)
        : this()
      {
        try
        {
          userName = cntxt.Request.Form["userName"];
          password = cntxt.Request.Form["password"];
          action = cntxt.Request.Form["action"];
          successUrl = cntxt.Request.Form["successUrl"];
          errorUrl = cntxt.Request.Form["errorUrl"];
          LoginView = cntxt.Request.Form["LoginView"];
          ResponseUrlSSO = cntxt.Request.Form["ResponseUrlSSO"];
        }
        catch { }
      }
    }



    public virtual ActionResult Logout()
    {
      string returnToUrl = LogoutWorker(null);
      return Redirect(returnToUrl);
    }


    public static string LogoutWorker(string ReturnUrl)
    {
      Ikon.Auth.Login.LoginManager.LogoutUser();
      string returnToUrl = ReturnUrl ?? System.Web.HttpContext.Current.Request.Params["ReturnUrl"].DefaultIfEmpty(Utility.ResolveUrl(IKGD_Config.AppSettings["Page_Home"] ?? System.Web.HttpContext.Current.Request.ApplicationPath));
      if (Ikon.Auth.Roles_IKGD.Provider.SSO_ServiceBaseUrl.IsNotNullOrWhiteSpace())
      {
        string urlLogout = Ikon.Auth.Roles_IKGD.Provider.SSO_ServiceBaseUrl + "AuthSSO/Logout";
        if (returnToUrl != null && !returnToUrl.Contains("://"))
          returnToUrl = new Uri(System.Web.HttpContext.Current.Request.Url, returnToUrl).ToString();
        if (returnToUrl.IsNotNullOrWhiteSpace())
        {
          urlLogout = Utility.UriSetQuery(urlLogout, "ReturnUrl", returnToUrl);
        }
        return urlLogout;
      }
      return returnToUrl;
    }


    public static string GetChangePasswordUrl()
    {
      return GetChangePasswordUrl(null);
    }


    public static string GetChangePasswordUrl(string ReturnUrl)
    {
      ReturnUrl = ReturnUrl ?? System.Web.HttpContext.Current.Request.Url.ToString();

      string urlChange;
      if (Ikon.Auth.Roles_IKGD.Provider.SSO_ServiceBaseUrl.IsNotNullOrWhiteSpace())
      {
        urlChange = Ikon.Auth.Roles_IKGD.Provider.SSO_ServiceBaseUrl + "AuthSSO/ChangePassword";
      }
      else
      {
        urlChange = "~/Auth/ChangePassword";
      }

      if (ReturnUrl != null && !ReturnUrl.Contains("://"))
        ReturnUrl = new Uri(System.Web.HttpContext.Current.Request.Url, ReturnUrl).ToString();
      if (ReturnUrl.IsNotNullOrWhiteSpace())
      {
        urlChange = Utility.UriSetQuery(urlChange, "ReturnUrl", ReturnUrl);
      }
      return urlChange;
    }

    public virtual ActionResult Unavailable()
    {
      return View("~/Views/Auth/Unavailable");
    }

    public virtual ActionResult DeleteUser(string userName, string unlockCode)
    {
      if (Utility.TryParse<bool>(IKGD_Config.AppSettings["EnableUserDataUpdates"], true) == false)
        return Unavailable();
      if (string.IsNullOrEmpty(userName))
        return null;
      if (userName.ToLower() == "root")
        return null;
      if (!VerifyPasswordUnlockCodeV2(userName, unlockCode))
        return Unavailable();   // andrebbe prevista una pagina specifica

      string currentUserName = MembershipHelper.UserName;
      try
      {
        // logout dello user
        if (string.Equals(userName, HttpContext.User.Identity.Name, StringComparison.OrdinalIgnoreCase))
          Ikon.Auth.Login.LoginManager.LogoutUser();
        //
        // SSO delete user
        string returnToUrl = Request.Params["ReturnUrl"].DefaultIfEmpty(Utility.ResolveUrl(IKGD_Config.AppSettings["Page_Home"] ?? Request.ApplicationPath));
        if (Ikon.Auth.Roles_IKGD.Provider.SSO_ServiceBaseUrl.IsNotNullOrWhiteSpace())
        {
          string urlDeleteUser = Ikon.Auth.Roles_IKGD.Provider.SSO_ServiceBaseUrl + "AuthSSO/DeleteUser";
          if (returnToUrl != null && !returnToUrl.Contains("://"))
            returnToUrl = new Uri(Request.Url, returnToUrl).ToString();
          if (returnToUrl.IsNotNullOrWhiteSpace())
          {
            Uri uriDelete = new Uri(urlDeleteUser);
            string unlockCodeSSO = GetUnlockCodeV2(currentUserName);

            urlDeleteUser = Utility.UriSetQuery(urlDeleteUser, "userName", userName);
            urlDeleteUser = Utility.UriSetQuery(urlDeleteUser, "unlockCode", unlockCodeSSO);
            urlDeleteUser = Utility.UriSetQuery(urlDeleteUser, "ReturnUrl", returnToUrl);
          }
          return Redirect(urlDeleteUser);
        }
        else
        {
          if (DeleteUserWorker(userName) == false)
            throw new Exception("Delete user failed");
        }
      }
      catch { }
      return Redirect(Utility.ResolveUrl("~/"));
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

          DeleteUserWorkerCustom(fsOp, mu);
          fsOp.DB.SubmitChanges();
        }
      }
      catch { return false; }
      return true;
    }


    protected virtual void DeleteUserWorkerCustom(FS_Operations fsOp, MembershipUser mu)
    {
    }

    public static string GetUnlockCodeV2(string userName)
    {
      return GetPasswordUnlockCodeV2(userName, null);
    }

    //
    // attenzione che per server in load balancing, bisogna settare la stessa machinekey nel web.config
    // perche' cliccando sulla mail dopo aver chiuso le sessioni si potrebbe finire su un altro server
    //
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


    public virtual ActionResult ActivateUser(string userName, string unlockCode, bool? forceLogin)
    {
      try
      {
        if (VerifyPasswordUnlockCodeV2(userName, unlockCode))
        {
          var user = Membership.GetUser(userName);
          if (user != null)
          {
            if (!user.IsApproved)
            {
              user.IsApproved = true;
              Membership.UpdateUser(user);
            }
            if (forceLogin.GetValueOrDefault(true))
            {
              bool logged = LoginManager.LoginUser(userName);
              if (!logged)
              {
                throw new Exception("autologin failed");
              }
            }
            if (Request.QueryString["ReturnUrl"].IsNotEmpty() && Utility.IsLocalToHostUrl(Request.QueryString["ReturnUrl"]))
            {
              return Redirect(Request.QueryString["ReturnUrl"]);
            }
          }
        }
      }
      catch { }
      if (Request.QueryString["ReturnUrlError"].IsNotEmpty() && Utility.IsLocalToHostUrl(Request.QueryString["ReturnUrlError"]))
      {
        return Redirect(Request.QueryString["ReturnUrlError"]);
      }
      return null;
    }


    public virtual ActionResult ChangePassword(string userName, string unlockCode)
    {
      if (Utility.TryParse<bool>(IKGD_Config.AppSettings["EnableUserDataUpdates"], true) == false)
        return Unavailable();
      bool authInRequired = Utility.TryParse<bool>(HttpContext.Request.Params["authrequired"], false);
      if (string.IsNullOrEmpty(userName) && !Ikon.GD.MembershipHelper.IsAnonymous)
        userName = Ikon.GD.MembershipHelper.UserName;
      ViewData["passwordOldVisible"] = true;
      ViewData["userName"] = userName;
      ViewData["actionCode"] = "change";
      if (userName.IsNotEmpty() && unlockCode.IsNotEmpty() && VerifyPasswordUnlockCodeV2(userName, unlockCode))
      {
        authInRequired = false;
        ViewData["actionCode"] = "reset";
        ViewData["passwordOldVisible"] = false;
      }
      if (authInRequired)
      {
        if (Ikon.GD.MembershipHelper.IsAnonymous || !Ikon.GD.MembershipHelper.IsMembershipVerified)
          return RedirectToAction("Login");
        if (string.IsNullOrEmpty(userName))
          return RedirectToAction("Login");
      }
      return View();
    }


    [AcceptVerbs(HttpVerbs.Post)]
    [JsonFilter(ParameterName = "formRequest", ParameterType = typeof(ChangePasswordParameters))]
    public virtual ActionResult ChangePassword(ChangePasswordParameters formRequest)
    {
      if (Utility.TryParse<bool>(IKGD_Config.AppSettings["EnableUserDataUpdates"], true) == false)
        return Unavailable();
      string successUrl = null;
      string errorUrl = null;
      try
      {
        if (formRequest == null)
        {
          formRequest = new AuthControllerBase.ChangePasswordParameters(HttpContext);
        }
        if (formRequest.successUrl.IsNullOrEmpty() || formRequest.successUrl.StartsWith("javascript:", StringComparison.OrdinalIgnoreCase))
          formRequest.successUrl = null;
        if (formRequest.errorUrl.IsNullOrEmpty() || formRequest.errorUrl.StartsWith("javascript:", StringComparison.OrdinalIgnoreCase))
          formRequest.errorUrl = null;
        successUrl = (formRequest.successUrl.IsNotEmpty() && Utility.IsLocalToHostUrl(formRequest.successUrl)) ? formRequest.successUrl : IKGD_Config.AppSettings["Page_Home"] ?? Request.ApplicationPath;
        errorUrl = (formRequest.errorUrl.IsNotEmpty() && Utility.IsLocalToHostUrl(formRequest.errorUrl)) ? formRequest.errorUrl : null;
        if (successUrl.IsNotEmpty())
          successUrl = Utility.ResolveUrl(successUrl);
        if (errorUrl.IsNotEmpty())
          errorUrl = Utility.ResolveUrl(errorUrl);
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
          return Json(new { hasError = true, message = string.Format(ResourceIKCMS_Components.Auth_ChangePassword_InvalidUsername2, formRequest.userName), errorUrl = errorUrl });
        //
        switch (formRequest.action)
        {
          case "change":
            {
              if (string.IsNullOrEmpty(formRequest.passwordOld) || !Membership.ValidateUser(formRequest.userName, formRequest.passwordOld))
                return Json(new { hasError = true, message = ResourceIKCMS_Components.Auth_ChangePassword_InvalidPassword1, errorUrl = errorUrl });
              if (!string.IsNullOrEmpty(formRequest.passwordNew) && formRequest.passwordNew == formRequest.passwordVerify)
              {
                if (membershipUser.ChangePassword(formRequest.passwordOld, formRequest.passwordNew))
                  return Json(new { hasError = false, message = ResourceIKCMS_Components.Auth_ChangePassword_PasswordOK1, successUrl = successUrl });
                else
                  return Json(new { hasError = true, message = ResourceIKCMS_Components.Auth_ChangePassword_InvalidPassword2, errorUrl = errorUrl });
              }
              else
                return Json(new { hasError = true, message = ResourceIKCMS_Components.Auth_ChangePassword_InvalidPassword3, errorUrl = errorUrl });
            }
          case "resetRequest":
            {
              if (membershipUser == null || string.IsNullOrEmpty(membershipUser.Email))
                return Json(new { hasError = true, message = ResourceIKCMS_Components.Auth_Login_UsernameInvalid, errorUrl = errorUrl });
              MailSendResetPassword(membershipUser, null, this.ControllerContext, successUrl, errorUrl);
              return Json(new { hasError = false, message = ResourceIKCMS_Components.Auth_Login_PasswordReset1, successUrl = successUrl });
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
                    return Json(new { hasError = false, message = ResourceIKCMS_Components.Auth_ChangePassword_PasswordOK2, successUrl = successUrl });
                  }
                  else
                    return Json(new { hasError = true, message = ResourceIKCMS_Components.Auth_ChangePassword_InvalidPassword4, errorUrl = errorUrl });
                }
                else
                  return Json(new { hasError = true, message = ResourceIKCMS_Components.Auth_ChangePassword_InvalidPassword5, errorUrl = errorUrl });
              }
              else
                return Json(new { hasError = true, message = ResourceIKCMS_Components.Auth_ChangePassword_InvalidCredentials, errorUrl = errorUrl });
            }
          default:
            return Json(new { hasError = true, message = ResourceIKCMS_Components.Auth_ChangePassword_InvalidOperation, errorUrl = errorUrl });
        }
      }
      catch (Exception ex)
      {
        return Json(new { hasError = true, message = ex.Message, errorUrl = errorUrl });
      }
      //return Json(new { hasError = true, message = "Errore nella modifica della password. Prego riprovare.", errorUrl = errorUrl });
    }


    public class ChangePasswordParameters
    {
      public string userName { get; set; }
      public string passwordOld { get; set; }
      public string passwordNew { get; set; }
      public string passwordVerify { get; set; }
      public string action { get; set; }
      public string unlockCode { get; set; }
      public string successUrl { get; set; }
      public string errorUrl { get; set; }

      public ChangePasswordParameters()
      {
      }

      public ChangePasswordParameters(HttpContextBase cntxt)
        : this()
      {
        try
        {
          userName = cntxt.Request.Form["userName"];
          passwordOld = cntxt.Request.Form["passwordOld"];
          passwordNew = cntxt.Request.Form["passwordNew"];
          passwordVerify = cntxt.Request.Form["passwordVerify"];
          action = cntxt.Request.Form["action"];
          unlockCode = cntxt.Request.Form["unlockCode"];
          successUrl = cntxt.Request.Form["successUrl"];
          errorUrl = cntxt.Request.Form["errorUrl"];
        }
        catch { }
      }
    }


    //
    // fornisce un valore random salvato in sessione per il supporto CAPTCHA
    //
    public virtual ActionResult CaptchaCode()
    {
      Random rnd = new Random();
      int rndVal = rnd.Next(0, 5);
      HttpContext.Session["CAPTCHA"] = rndVal.ToString();
      return Content((string)HttpContext.Session["CAPTCHA"]);
    }


    public virtual ActionResult RegisterAjax(string userName)
    {
      if (Utility.TryParse<bool>(IKGD_Config.AppSettings["EnableUserDataUpdates"], true) == false)
        return Unavailable();
      try
      {
        bool HasACL = Ikon.GD.MembershipHelper.HasMembershipACL();
        bool IsSameUser = !string.IsNullOrEmpty(userName) && (userName == MembershipHelper.UserName) && !MembershipHelper.IsAnonymous;
        bool DisplayApproved = HasACL && !IsSameUser;
        //
        if (!string.IsNullOrEmpty(userName) && !IsSameUser && !HasACL)
        {
          // non funziona per una classe base che non sia un controller
          string loginUrl = string.Format("~/Auth/Login");
          //string loginUrl = IKCMS_RouteUrlManager.GetMvcActionUrl<IkonWeb.Controllers.AuthController>(c => c.Login());
          Response.Redirect(Utility.UriSetQuery(loginUrl, "ReturnUrl", Request.Url.ToString()), true);
          return null;
        }
        //
        ViewData["HasACL"] = HasACL;
        ViewData["userNameCurrent"] = MembershipHelper.IsAnonymous ? null : MembershipHelper.UserName;
        //ViewData["userNameForm"] = ViewData["userNameCurrent"];
        ViewData["userNameForm"] = userName.NullIfEmpty() ?? (ViewData["userNameCurrent"] as string);  // voglio che sia null per una nuova registrazione
        //if (Request.Params.AllKeys.Contains("userName"))
        //{
        //  ViewData["userNameForm"] = Request.Params["userName"];  // voglio che sia null per una nuova registrazione
        //}
        //
        ViewData["displayPasswordCurrent"] = MembershipHelper.UserName == (string)ViewData["userNameForm"];
        ViewData["displayPasswordChange"] = HasACL || MembershipHelper.UserName == (string)ViewData["userNameForm"] || ViewData["userNameForm"] == null;
        ViewData["displayApproved"] = DisplayApproved;
        //ViewData["displayApproved"] = HasACL && MembershipHelper.UserName != (string)ViewData["userNameForm"] && ViewData["userNameForm"] != null;
        //
        userName = ViewData["userNameForm"] as string;
        //
        if (!string.IsNullOrEmpty(userName) && !string.Equals(userName, MembershipHelper.UserName, StringComparison.OrdinalIgnoreCase) && !HasACL)
          throw new Exception(ResourceIKCMS_Components.Auth_Register_ExceptionInvalidCredentials);
        //
        MembershipUserKVT userKVT = new MembershipUserKVT(string.IsNullOrEmpty(userName) ? null : Membership.Provider.GetUser(userName, false));
        ViewData["userKVT"] = userKVT;
        //
        if (userKVT.User == null && !string.IsNullOrEmpty(userName))
          throw new Exception(ResourceIKCMS_Components.Auth_Register_ExceptionInvalidOperation);
      }
      catch (Exception ex)
      {
        ViewData["errors"] = ex.Message;
      }
      //
      return View();
    }


    [AcceptVerbs(HttpVerbs.Post)]
    [ValidateInput(false)]
    public virtual ActionResult RegisterAjax(string userName, FormCollection collection)
    {
      if (Utility.TryParse<bool>(IKGD_Config.AppSettings["EnableUserDataUpdates"], true) == false)
        return Unavailable();
      //
      bool createUserAsApproved = true;
      string successUrl = null;
      List<string> messages = new List<string>();
      //
      try
      {
        MembershipUser userOrig = null;
        try { userOrig = Membership.GetUser(userName); }
        catch { }
        //
        bool HasACL = Ikon.GD.MembershipHelper.HasMembershipACL();
        bool IsSameUser = !string.IsNullOrEmpty(userName) && (userName == MembershipHelper.UserName) && !MembershipHelper.IsAnonymous;
        bool DisplayApproved = HasACL && !IsSameUser;
        //
        successUrl = collection["successUrl"];
        //
        // prima di tutto verifica del captcha
        //
        if (Utility.TryParse<bool>(collection["captchaEnabled"], false) && collection["captcha"] != (string)HttpContext.Session["CAPTCHA"])
        {
          messages.Add("Verifica che sei un umano e trascina l'icona nel cerchio secondo le istruzioni.");
        }
        //
        if (!Utility.ValidateEMail(collection["userEmail"]))
          messages.Add("L'E-mail inserita non è un indirizzo di E-mail valido.");
        if (string.IsNullOrEmpty(userName) && !string.IsNullOrEmpty(collection["userEmail"]))
        {
          if (Membership.GetUser(collection["userEmail"].TrimSafe()) != null)
            messages.Add("Lo username corrispondente all'indirizzo E-mail inserito è già utilizzato nel sistema.");
          if (string.IsNullOrEmpty(collection["PasswordNew"]))
            messages.Add("Per la creazione di un nuovo utente è necessario specificare una password.");
        }
        //
        // check delle password
        if (!string.IsNullOrEmpty(collection["PasswordCurrent"]) && !string.IsNullOrEmpty(collection["PasswordNew"]) && !string.IsNullOrEmpty(userName))
        {
          if (!Membership.ValidateUser(userName, collection["PasswordCurrent"]))
            messages.Add("Le password corrente inserita non é valida.");
        }
        if (collection["PasswordNew"] != collection["PasswordVerify"])
          messages.Add("Le password inserite non coincidono.");
        else if (!string.IsNullOrEmpty(collection["PasswordNew"]) && collection["PasswordNew"].Length < 6)
          messages.Add("La password deve essere di almeno 6 caratteri.");
        //
        if (collection["Input_Nome"].TrimSafe().Length == 0)
          messages.Add("E' necessario compilare il campo Nome.");
        if (collection["Input_Cognome"].TrimSafe().Length == 0)
          messages.Add("E' necessario compilare il campo Cognome.");
        //if (collection["Input_Indirizzo"].TrimSafe().Length == 0)
        //    messages.Add("E' necessario compilare il campo Indirizzo.");
        //if (collection["Input_Telefono"].TrimSafe().Length == 0 && collection["Input_Fax"].TrimSafe().Length == 0)
        //    messages.Add("E' necessario compilare almeno uno dei campi Telefono o Fax.");
        //if (collection["Input_Ente"].TrimSafe().Length == 0)
        //    messages.Add("E' necessario compilare il campo Ente.");
        //if (collection["Input_Posizione"].TrimSafe().Length == 0)
        //    messages.Add("E' necessario compilare il campo Posizione.");

        bool flag_privacy = Utility.TryParse<bool>(collection["privacy"]);
        if (!flag_privacy)
          messages.Add("E' necessario fornire il consenso al trattamento dei dati personali per procedere con la registrazione.");
        bool flag_IsApproved = Utility.TryParse<bool>(collection["IsApproved"]);
        //
        if (messages.Count > 0)
          return Json(new { hasError = true, message = "<ul>" + string.Join("\n", messages.Select(m => string.Format("<li>{0}</li>", HttpUtility.HtmlEncode(m))).ToArray()) + "</ul>" });
        //
        // processing delle modifiche all'account
        //

        //
        // acquisizione dell'account o creazione di uno nuovo
        //
        bool forceLoginAtEnd = false;
        bool sendWelcomMessage = false;
        bool sendApprovationMessage = false;
        string userNameToLoad = userName.DefaultIfEmpty(collection["userEmail"]).TrimSafe();
        MembershipUser user = Membership.GetUser(userNameToLoad);
        if (user == null && !string.IsNullOrEmpty(userName))
          throw new Exception("L'utente selezionato non è definito nel sistema.");
        if (user == null)
        {
          MembershipCreateStatus status;
          user = Membership.CreateUser(userNameToLoad, collection["PasswordNew"], collection["userEmail"].TrimSafe(), null, null, createUserAsApproved, out status);
          if (user == null || status != MembershipCreateStatus.Success)
            throw new Exception("Errore nella creazione dell'account: " + status.ToString());
          try
          {
            var roles_default = Utility.Explode(IKGD_Config.AppSettings["NewUserDefaultRoles"], ",", " ", true);
            if (roles_default.Any())
            {
              Ikon.Auth.Roles_IKGD.Provider.AddUsersToRoles(new string[] { user.UserName }, roles_default.ToArray());
            }
          }
          catch { }
          forceLoginAtEnd = flag_IsApproved;
          sendWelcomMessage = true;
        }
        else
        {
          if (!string.IsNullOrEmpty(collection["PasswordNew"]))
          {
            string passNew = (HasACL && string.IsNullOrEmpty(collection["PasswordCurrent"])) ? user.ResetPassword() : collection["PasswordCurrent"];
            user.ChangePassword(passNew, collection["PasswordNew"]);
          }
          if (user.Email != collection["userEmail"])
            user.Email = collection["userEmail"].TrimSafe();
        }
        MembershipUserKVT userKVT = new MembershipUserKVT(user);
        if (DisplayApproved)
        {
          sendApprovationMessage = user.IsApproved != flag_IsApproved;
          user.IsApproved = flag_IsApproved;
        }
        //
        userKVT.FullName = Utility.Implode(new string[] { collection["Input_Nome"], collection["Input_Cognome"] }, " ", null, true, true);
        userKVT["Privacy"].Value = flag_privacy;
        //
        collection.AllKeys.Where(k => k.StartsWith("Input_", StringComparison.OrdinalIgnoreCase)).ForEach(k => userKVT[k.Substring("Input_".Length)].Value = collection[k].TrimSafe());
        userKVT.UpdateKVT(null);
        Membership.UpdateUser(user);
        //
        //ILazyLoginMapper llMapper = HelperExtensionCustom.DC.GetLazyLoginMapper((Guid)user.ProviderUserKey, true);
        //var llAnagrafica = HelperExtensionCustom.DC.GetLazyLoginMapperChild<Custom.DB.LazyLogin_AnagraficaMain>(llMapper, true);
        //llAnagrafica.Nome = collection["Input_Nome"].TrimSafe();
        //llAnagrafica.Cognome = collection["Input_Cognome"].TrimSafe();
        //llAnagrafica.EMail = user.Email;
        //llAnagrafica.Telefono = collection["Input_Telefono"].TrimSafe();
        //llAnagrafica.Comune = collection["Input_Citta"].TrimSafe();
        //llAnagrafica.Provincia = collection["Input_Provincia"].TrimSafe();
        //llAnagrafica.Indirizzo = collection["Input_Indirizzo"].TrimSafe();
        //llAnagrafica.flag_Privacy = Utility.TryParse<bool>(collection["privacy"], false);
        //var chg = HelperExtensionCustom.DC.GetChangeSet();
        //HelperExtensionCustom.DC.SubmitChanges();
        //
        if (sendWelcomMessage)
        {
          MailSendWelcomeNewUser(user, null, this.ControllerContext);
        }
        if (sendApprovationMessage)
        {
          MailSendApprovationChangeUser(user, null, this.ControllerContext);
        }
        if (forceLoginAtEnd)
        {
          // devo forzare un login perche' cambia lo username nella cookie permanente!
          //Ikon.GD.MembershipHelper.MembershipLogin(user, null, false);
          //Ikon.GD.MembershipHelper.MembershipSessionReset();
          //
          Ikon.GD.MembershipHelper.MembershipSessionReset();
          bool logged = LoginManager.LoginUser(user, true, null, true);
          //
        }
        //
        string msg = "Operazione completata con successo.";
        if (userOrig == null && user != null)
        {
          msg += "<br/>\nAttendere la mail con l'attivazione della registrazione per entrare nel sito.";
        }
        successUrl = Utility.IsLocalToHostUrl(successUrl) ? successUrl : IKGD_Config.AppSettings["Page_Home"] ?? Request.ApplicationPath;
        return Json(new { hasError = false, successUrl = successUrl, message = msg });
        //
      }
      catch (Exception ex)
      {
        return Json(new { hasError = true, message = ex.Message });
      }
    }


    public static void MailSendWelcomeNewUser(MembershipUser userInfo, Action<MailMessage> messageProcessor) { MailSendWelcomeNewUser(userInfo, messageProcessor, null, null); }
    public static void MailSendWelcomeNewUser(MembershipUser userInfo, Action<MailMessage> messageProcessor, ControllerContext context) { MailSendWelcomeNewUser(userInfo, messageProcessor, context, null); }
    public static void MailSendWelcomeNewUser(MembershipUser userInfo, Action<MailMessage> messageProcessor, ControllerContext context, string baseFileName)
    {
      try
      {
        string messageSrc = null;
        baseFileName = baseFileName.NullIfEmpty() ?? "MsgWelcomeNewUser";
        if (System.IO.File.Exists(Utility.vPathMap("~/Views/Auth/{0}.{1}.xml".FormatString(baseFileName, IKGD_Language_Provider.Provider.LanguageNN))))
        {
          messageSrc = Utility.FileReadVirtual("~/Views/Auth/{0}.{1}.xml".FormatString(baseFileName, IKGD_Language_Provider.Provider.LanguageNN));
        }
        else
        {
          messageSrc = Utility.FileReadVirtual("~/Views/Auth/{0}.xml".FormatString(baseFileName));
        }
        string baseUrl = new Uri(System.Web.HttpContext.Current.Request.Url, VirtualPathUtility.ToAbsolute("~/")).ToString();
        MembershipUserKVT userKVT = new MembershipUserKVT(userInfo);
        //
        string siteName = IKGD_Config.AppSettings["SiteNameForcedForMailing"] ?? System.Web.HttpContext.Current.Request.Url.Host;
        messageSrc = messageSrc.Replace("{$site}", HttpUtility.HtmlEncode(siteName));
        messageSrc = messageSrc.Replace("{$FullName}", HttpUtility.HtmlEncode(userKVT.FullName.NullIfEmpty() ?? userInfo.UserName));
        messageSrc = messageSrc.Replace("{$Email}", HttpUtility.HtmlEncode(userInfo.Email));
        messageSrc = messageSrc.Replace("{$userName}", HttpUtility.HtmlEncode(userInfo.UserName));
        messageSrc = messageSrc.Replace("{$baseUrl}", baseUrl);
        //
        XElement xTemplate = XElement.Parse(messageSrc);
        //
        MailMessage message = new MailMessage();
        message.From = new MailAddress(xTemplate.Element("from").AttributeValue("address"), xTemplate.Element("from").AttributeValue("text", xTemplate.Element("from").AttributeValue("address")));
        message.To.Add(new MailAddress(userInfo.Email, userKVT.FullName));
        xTemplate.Elements("to").ForEach(x => message.To.Add(new MailAddress(x.AttributeValue("address"), x.AttributeValue("text", x.Value.NullIfEmpty() ?? x.AttributeValue("address")))));
        xTemplate.Elements("cc").ForEach(x => message.CC.Add(new MailAddress(x.AttributeValue("address"), x.AttributeValue("text", x.Value.NullIfEmpty() ?? x.AttributeValue("address")))));
        xTemplate.Elements("bcc").ForEach(x => message.Bcc.Add(new MailAddress(x.AttributeValue("address"), x.AttributeValue("text", x.Value.NullIfEmpty() ?? x.AttributeValue("address")))));
        message.IsBodyHtml = true;
        message.Subject = xTemplate.Element("subject").Value;
        if (!string.IsNullOrEmpty(IKGD_Config.AppSettings["SendMailDebug"]))
        {
          message.Bcc.Clear();
          message.CC.Clear();
          message.To.Clear();
          message.To.Add(new MailAddress(IKGD_Config.AppSettings["SendMailDebug"]));
        }
        //
        string body = xTemplate.Element("body").Value;
        message.Body = body;
        if (messageProcessor != null)
        {
          messageProcessor(message);
          body = message.Body;
        }
        //
        // replace standard dopo messageProcessor!
        string controllerBaseUrl = "Auth";
        if (context != null && context.Controller.GetType().Name != (controllerBaseUrl + "Controller"))
        {
          controllerBaseUrl = context.Controller.GetType().Name;
          controllerBaseUrl = controllerBaseUrl.Substring(0, controllerBaseUrl.Length - "Controller".Length);
        }
        string registerUrl = new Uri(System.Web.HttpContext.Current.Request.Url, Utility.ResolveUrl(string.Format("~/{0}/RegisterAjax?userName={1}", controllerBaseUrl, HttpUtility.UrlEncode(userInfo.UserName)))).ToString();
        body = body.Replace("{$title}", HttpUtility.HtmlEncode(message.Subject));
        body = body.Replace("{$thisAction}", registerUrl);
        message.Body = body;
        //
        SmtpClient client = new SmtpClient();
        client.Send(message);
      }
      catch { }
    }


    public static void MailSendApprovationChangeUser(MembershipUser userInfo, Action<MailMessage> messageProcessor) { MailSendApprovationChangeUser(userInfo, messageProcessor, null); }
    public static void MailSendApprovationChangeUser(MembershipUser userInfo, Action<MailMessage> messageProcessor, ControllerContext context)
    {
      try
      {
        string messageSrc = null;
        if (System.IO.File.Exists(Utility.vPathMap("~/Views/Auth/MsgApprovationChangeUser.{0}.xml".FormatString(IKGD_Language_Provider.Provider.LanguageNN))))
        {
          messageSrc = Utility.FileReadVirtual("~/Views/Auth/MsgApprovationChangeUser.{0}.xml".FormatString(IKGD_Language_Provider.Provider.LanguageNN));
        }
        else
        {
          messageSrc = Utility.FileReadVirtual("~/Views/Auth/MsgApprovationChangeUser.xml");
        }
        string baseUrl = new Uri(System.Web.HttpContext.Current.Request.Url, VirtualPathUtility.ToAbsolute("~/")).ToString();
        MembershipUserKVT userKVT = new MembershipUserKVT(userInfo);
        //
        string siteName = IKGD_Config.AppSettings["SiteNameForcedForMailing"] ?? System.Web.HttpContext.Current.Request.Url.Host;
        messageSrc = messageSrc.Replace("{$site}", HttpUtility.HtmlEncode(siteName));
        messageSrc = messageSrc.Replace("{$FullName}", HttpUtility.HtmlEncode(userKVT.FullName));
        messageSrc = messageSrc.Replace("{$Email}", HttpUtility.HtmlEncode(userInfo.Email));
        messageSrc = messageSrc.Replace("{$userName}", HttpUtility.HtmlEncode(userInfo.UserName));
        messageSrc = messageSrc.Replace("{$baseUrl}", baseUrl);
        //
        XElement xTemplate = XElement.Parse(messageSrc);
        //
        MailMessage message = new MailMessage();
        message.From = new MailAddress(xTemplate.Element("from").AttributeValue("address"), xTemplate.Element("from").AttributeValue("text", xTemplate.Element("from").AttributeValue("address")));
        message.To.Add(new MailAddress(userInfo.Email, userKVT.FullName));
        xTemplate.Elements("to").ForEach(x => message.To.Add(new MailAddress(x.AttributeValue("address"), x.AttributeValue("text", x.Value.NullIfEmpty() ?? x.AttributeValue("address")))));
        xTemplate.Elements("cc").ForEach(x => message.CC.Add(new MailAddress(x.AttributeValue("address"), x.AttributeValue("text", x.Value.NullIfEmpty() ?? x.AttributeValue("address")))));
        xTemplate.Elements("bcc").ForEach(x => message.Bcc.Add(new MailAddress(x.AttributeValue("address"), x.AttributeValue("text", x.Value.NullIfEmpty() ?? x.AttributeValue("address")))));
        message.IsBodyHtml = true;
        message.Subject = xTemplate.Element("subject").Value;
        if (!string.IsNullOrEmpty(IKGD_Config.AppSettings["SendMailDebug"]))
        {
          message.Bcc.Clear();
          message.CC.Clear();
          message.To.Clear();
          message.To.Add(new MailAddress(IKGD_Config.AppSettings["SendMailDebug"]));
        }
        //
        string body = xTemplate.Element("body").Value;
        body = body.Replace("{$title}", HttpUtility.HtmlEncode(message.Subject));
        body = body.Replace("{$ApprovedStatus}", HttpUtility.HtmlEncode(userInfo.IsApproved ? "abilitato" : "disabilitato"));
        message.Body = body;
        //
        if (messageProcessor != null)
          messageProcessor(message);
        //
        SmtpClient client = new SmtpClient();
        client.Send(message);
      }
      catch { }
    }


    public static void MailSendResetPassword(MembershipUser userInfo, Action<MailMessage> messageProcessor, ControllerContext context, string ReturnUrl, string ErrorUrl)
    {
      try
      {
        string messageSrc = null;
        if (System.IO.File.Exists(Utility.vPathMap("~/Views/Auth/MsgResetPassword.{0}.xml".FormatString(IKGD_Language_Provider.Provider.LanguageNN))))
        {
          messageSrc = Utility.FileReadVirtual("~/Views/Auth/MsgResetPassword.{0}.xml".FormatString(IKGD_Language_Provider.Provider.LanguageNN));
        }
        else
        {
          messageSrc = Utility.FileReadVirtual("~/Views/Auth/MsgResetPassword.xml");
        }
        string baseUrl = new Uri(System.Web.HttpContext.Current.Request.Url, VirtualPathUtility.ToAbsolute("~/")).ToString();
        MembershipUserKVT userKVT = new MembershipUserKVT(userInfo);
        //
        string siteName = IKGD_Config.AppSettings["SiteNameForcedForMailing"] ?? System.Web.HttpContext.Current.Request.Url.Host;
        messageSrc = messageSrc.Replace("{$site}", HttpUtility.HtmlEncode(siteName));
        messageSrc = messageSrc.Replace("{$FullName}", HttpUtility.HtmlEncode(userKVT.FullName));
        messageSrc = messageSrc.Replace("{$Email}", HttpUtility.HtmlEncode(userInfo.Email));
        messageSrc = messageSrc.Replace("{$userName}", HttpUtility.HtmlEncode(userInfo.UserName));
        messageSrc = messageSrc.Replace("{$baseUrl}", baseUrl);
        //
        XElement xTemplate = XElement.Parse(messageSrc);
        //
        MailMessage message = new MailMessage();
        message.From = new MailAddress(xTemplate.Element("from").AttributeValue("address"), xTemplate.Element("from").AttributeValue("text", xTemplate.Element("from").AttributeValue("address")));
        message.To.Add(new MailAddress(userInfo.Email, userKVT.FullName));
        xTemplate.Elements("to").ForEach(x => message.To.Add(new MailAddress(x.AttributeValue("address"), x.AttributeValue("text", x.Value.NullIfEmpty() ?? x.AttributeValue("address")))));
        xTemplate.Elements("cc").ForEach(x => message.CC.Add(new MailAddress(x.AttributeValue("address"), x.AttributeValue("text", x.Value.NullIfEmpty() ?? x.AttributeValue("address")))));
        xTemplate.Elements("bcc").ForEach(x => message.Bcc.Add(new MailAddress(x.AttributeValue("address"), x.AttributeValue("text", x.Value.NullIfEmpty() ?? x.AttributeValue("address")))));
        message.IsBodyHtml = true;
        message.Subject = xTemplate.Element("subject").Value;
        if (!string.IsNullOrEmpty(IKGD_Config.AppSettings["SendMailDebug"]))
        {
          message.Bcc.Clear();
          message.CC.Clear();
          message.To.Clear();
          message.To.Add(new MailAddress(IKGD_Config.AppSettings["SendMailDebug"]));
        }
        //
        // non funziona per una classe base che non sia un controller
        //string resetUrl = IKCMS_RouteUrlManager.GetMvcActionUrl<IkonWeb.Controllers.AuthController>(null, c => c.ChangePassword(userInfo.UserName, GetPasswordUnlockCodeV2(userInfo.UserName, null)), true);
        string controllerBaseUrl = "Auth";
        if (context != null && context.Controller.GetType().Name != (controllerBaseUrl + "Controller"))
        {
          controllerBaseUrl = context.Controller.GetType().Name;
          controllerBaseUrl = controllerBaseUrl.Substring(0, controllerBaseUrl.Length - "Controller".Length);
        }
        string resetUrl = new Uri(System.Web.HttpContext.Current.Request.Url, Utility.ResolveUrl(string.Format("~/{0}/ChangePassword?userName={1}&unlockCode={2}", controllerBaseUrl, HttpUtility.UrlEncode(userInfo.UserName), HttpUtility.UrlEncode(GetPasswordUnlockCodeV2(userInfo.UserName, null))))).ToString();
        try
        {
          ReturnUrl = ReturnUrl.TrimSafe().NullIfEmpty();
          ErrorUrl = ErrorUrl.TrimSafe().NullIfEmpty();
          ReturnUrl = ReturnUrl ?? ((System.Web.HttpContext.Current.Request.UrlReferrer == null) ? null : System.Web.HttpContext.Current.Request.UrlReferrer.ToString());
          if (ReturnUrl.IsNotEmpty())
            resetUrl = Utility.UriSetQuery(resetUrl, "ReturnUrl", ReturnUrl);
          if (ErrorUrl.IsNotEmpty())
            resetUrl = Utility.UriSetQuery(resetUrl, "ErrorUrl", ErrorUrl);
        }
        catch { }
        //
        string body = xTemplate.Element("body").Value;
        body = body.Replace("{$title}", message.Subject);
        body = body.Replace("{$resetPasswordUrl}", HttpUtility.HtmlAttributeEncode(resetUrl));
        message.Body = body;
        //
        if (messageProcessor != null)
          messageProcessor(message);
        //
        SmtpClient client = new SmtpClient();
        client.Send(message);
      }
      catch { }
    }


    //
    // attenzione che per server in load balancing, bisogna settare la stessa machinekey nel web.config
    // perche' cliccando sulla mail dopo aver chiuso le sessioni si potrebbe finire su un altro server
    //
    public static void MailSendActivateUser(MembershipUser userInfo, Action<MailMessage> messageProcessor, ControllerContext context, bool? forceLogin, string ReturnUrl, string ReturnUrlError)
    {
      try
      {
        string messageSrc = null;
        if (System.IO.File.Exists(Utility.vPathMap("~/Views/Auth/MsgActivateUser.{0}.xml".FormatString(IKGD_Language_Provider.Provider.LanguageNN))))
        {
          messageSrc = Utility.FileReadVirtual("~/Views/Auth/MsgActivateUser.{0}.xml".FormatString(IKGD_Language_Provider.Provider.LanguageNN));
        }
        else
        {
          messageSrc = Utility.FileReadVirtual("~/Views/Auth/MsgActivateUser.xml");
        }
        string baseUrl = new Uri(System.Web.HttpContext.Current.Request.Url, VirtualPathUtility.ToAbsolute("~/")).ToString();
        MembershipUserKVT userKVT = new MembershipUserKVT(userInfo);
        //
        string siteName = IKGD_Config.AppSettings["SiteNameForcedForMailing"] ?? System.Web.HttpContext.Current.Request.Url.Host;
        messageSrc = messageSrc.Replace("{$site}", HttpUtility.HtmlEncode(siteName));
        messageSrc = messageSrc.Replace("{$FullName}", HttpUtility.HtmlEncode(userKVT.FullName));
        messageSrc = messageSrc.Replace("{$Email}", HttpUtility.HtmlEncode(userInfo.Email));
        messageSrc = messageSrc.Replace("{$userName}", HttpUtility.HtmlEncode(userInfo.UserName));
        messageSrc = messageSrc.Replace("{$baseUrl}", baseUrl);
        //
        XElement xTemplate = XElement.Parse(messageSrc);
        //
        MailMessage message = new MailMessage();
        message.From = new MailAddress(xTemplate.Element("from").AttributeValue("address"), xTemplate.Element("from").AttributeValue("text", xTemplate.Element("from").AttributeValue("address")));
        message.To.Add(new MailAddress(userInfo.Email, userKVT.FullName));
        xTemplate.Elements("to").ForEach(x => message.To.Add(new MailAddress(x.AttributeValue("address"), x.AttributeValue("text", x.Value.NullIfEmpty() ?? x.AttributeValue("address")))));
        xTemplate.Elements("cc").ForEach(x => message.CC.Add(new MailAddress(x.AttributeValue("address"), x.AttributeValue("text", x.Value.NullIfEmpty() ?? x.AttributeValue("address")))));
        xTemplate.Elements("bcc").ForEach(x => message.Bcc.Add(new MailAddress(x.AttributeValue("address"), x.AttributeValue("text", x.Value.NullIfEmpty() ?? x.AttributeValue("address")))));
        message.IsBodyHtml = true;
        message.Subject = xTemplate.Element("subject").Value;
        if (!string.IsNullOrEmpty(IKGD_Config.AppSettings["SendMailDebug"]))
        {
          message.Bcc.Clear();
          message.CC.Clear();
          message.To.Clear();
          message.To.Add(new MailAddress(IKGD_Config.AppSettings["SendMailDebug"]));
        }
        //
        // non funziona per una classe base che non sia un controller
        //string resetUrl = IKCMS_RouteUrlManager.GetMvcActionUrl<IkonWeb.Controllers.AuthController>(null, c => c.ChangePassword(userInfo.UserName, GetPasswordUnlockCodeV2(userInfo.UserName, null)), true);
        string controllerBaseUrl = "Auth";
        if (context != null && context.Controller.GetType().Name != (controllerBaseUrl + "Controller"))
        {
          controllerBaseUrl = context.Controller.GetType().Name;
          controllerBaseUrl = controllerBaseUrl.Substring(0, controllerBaseUrl.Length - "Controller".Length);
        }
        string activateUserUrl = new Uri(System.Web.HttpContext.Current.Request.Url, Utility.ResolveUrl(string.Format("~/{0}/ActivateUser?userName={1}&unlockCode={2}&forceLogin={3}", controllerBaseUrl, HttpUtility.UrlEncode(userInfo.UserName), HttpUtility.UrlEncode(GetPasswordUnlockCodeV2(userInfo.UserName, null)), forceLogin))).ToString();
        try
        {
          if (System.Web.HttpContext.Current.Request.UrlReferrer != null)
            ReturnUrl = ReturnUrl ?? System.Web.HttpContext.Current.Request.UrlReferrer.ToString();
          if (ReturnUrl.IsNotEmpty())
            activateUserUrl = Utility.UriSetQuery(activateUserUrl, "ReturnUrl", ReturnUrl);
          if (ReturnUrlError.IsNotEmpty())
            activateUserUrl = Utility.UriSetQuery(activateUserUrl, "ReturnUrlError", ReturnUrlError);
        }
        catch { }
        //
        string body = xTemplate.Element("body").Value;
        body = body.Replace("{$title}", message.Subject);
        body = body.Replace("{$activateUserUrl}", HttpUtility.HtmlAttributeEncode(activateUserUrl));
        message.Body = body;
        //
        if (messageProcessor != null)
          messageProcessor(message);
        //
        SmtpClient client = new SmtpClient();
        client.Send(message);
      }
      catch { }
    }
  }
}
