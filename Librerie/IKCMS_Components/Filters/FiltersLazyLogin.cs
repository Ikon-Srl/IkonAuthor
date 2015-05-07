using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using System.Web.Mvc.Ajax;
using System.IO;
using System.Web.Security;
using System.Net.Mail;
using System.Security.Cryptography;
using System.Text;
using System.Data;
using System.Data.Common;
using System.Transactions;
using System.Security.Principal;
using Newtonsoft.Json;
using Autofac;

using Ikon;
using Ikon.GD;
using Ikon.Config;
using Ikon.Auth.Login;


namespace IkonWeb.Controllers
{

  public static class LazyLoginFilterHelpers
  {

    public static ILazyLoginDataContext GetDefaultDataContext()
    {
      string typeName = IKGD_Config.AppSettings["LazyLoginDataContextType"] ?? typeof(Ikon.GD.IKGD_DataContext).Name;
      ILazyLoginDataContext DC = Ikon.IKCMS.IKCMS_ManagerIoC.requestContainer.Resolve(Utility.FindTypeCachedExt(typeName, false)) as ILazyLoginDataContext;
      return DC;
    }

  }


  public abstract class AuthorizeAttributeBaseCMS : AuthorizeAttribute
  {
    public bool NoRedirectToAuth { get; set; }

    protected override void HandleUnauthorizedRequest(AuthorizationContext filterContext)
    {
      if (NoRedirectToAuth)
      {
        filterContext.Result = new EmptyResult();
        filterContext.HttpContext.ApplicationInstance.CompleteRequest();
        filterContext.HttpContext.Response.End();
        //throw new UnauthorizedAccessException();
        return;
      }
      filterContext.HttpContext.Response.Redirect(Utility.UriSetQuery(System.Web.Security.FormsAuthentication.LoginUrl, "ReturnUrl", filterContext.HttpContext.Request.Url.ToString()), false);
      filterContext.HttpContext.ApplicationInstance.CompleteRequest();
      base.HandleUnauthorizedRequest(filterContext);
    }

  }


  //
  // verifica ACL di accesso al CMS
  //
  public class AuthorizeAclCMS : AuthorizeAttributeBaseCMS
  {

    protected override bool AuthorizeCore(HttpContextBase httpContext)
    {
      //
      //return base.AuthorizeCore(httpContext);
      //
      if (httpContext == null)
        throw new ArgumentNullException("httpContext");
      if (httpContext.Items["AuthorizeAttributeOverride"] != null)
        return (bool)httpContext.Items["AuthorizeAttributeOverride"];
      return FS_ACL_Reduced.HasOperatorACLs();
    }

  }


  //
  // versione custom del filtro di autentifica per il supporto di una lista di user OR roles
  // da usare con [AuthorizeOFF(Order = 0)] per processarlo prima degli altri attributi
  //
  public class AuthorizeOFFAttribute : AuthorizeAttribute
  {
    protected override bool AuthorizeCore(HttpContextBase httpContext)
    {
      if (httpContext != null)
        httpContext.Items["AuthorizeAttributeOverride"] = true;
      //
      //return base.AuthorizeCore(httpContext);
      return true;
    }
  }


  //
  // versione custom del filtro di autentifica per il supporto di una lista di user OR roles
  //
  public class AuthorizeORAttribute : AuthorizeAttributeBaseCMS
  {
    public string Areas { get; set; }

    protected override bool AuthorizeCore(HttpContextBase httpContext)
    {
      //
      //return base.AuthorizeCore(httpContext);
      //
      if (httpContext == null)
        throw new ArgumentNullException("httpContext");
      if (httpContext.Items["AuthorizeAttributeOverride"] != null)
        return (bool)httpContext.Items["AuthorizeAttributeOverride"];
      IPrincipal user = httpContext.User;
      if (!user.Identity.IsAuthenticated)
        return false;
      if (string.IsNullOrEmpty(this.Users) && string.IsNullOrEmpty(this.Roles))
        return true;
      if (!string.IsNullOrEmpty(this.Users) && Utility.Explode(this.Users, ",", " ", true).Any(n => string.Equals(n, user.Identity.Name)))
        return true;
      if (!string.IsNullOrEmpty(this.Roles) && Utility.Explode(this.Roles, ",", " ", true).Intersect(MembershipHelper.Roles).Any())
        return true;
      if (!string.IsNullOrEmpty(this.Areas))
      {
        try { if (Utility.Explode(this.Areas, ",", " ", true).Intersect(MembershipHelper.Areas).Any()) return true; }
        catch { }
      }
      return false;
    }

  }


  public class AuthorizeNoAuthORAttribute : AuthorizeAttributeBaseCMS
  {
    public string Areas { get; set; }

    protected override bool AuthorizeCore(HttpContextBase httpContext)
    {
      //
      //return base.AuthorizeCore(httpContext);
      //
      if (httpContext == null)
        throw new ArgumentNullException("httpContext");
      if (httpContext.Items["AuthorizeAttributeOverride"] != null)
        return (bool)httpContext.Items["AuthorizeAttributeOverride"];
      IPrincipal user = httpContext.User;
      if (string.IsNullOrEmpty(this.Users) && string.IsNullOrEmpty(this.Roles))
        return true;
      if (!string.IsNullOrEmpty(this.Users) && Utility.Explode(this.Users, ",", " ", true).Any(n => string.Equals(n, user.Identity.Name)))
        return true;
      if (!string.IsNullOrEmpty(this.Roles) && Utility.Explode(this.Roles, ",", " ", true).Intersect(MembershipHelper.Roles).Any())
        return true;
      if (!string.IsNullOrEmpty(this.Areas))
      {
        try { if (Utility.Explode(this.Areas, ",", " ", true).Intersect(MembershipHelper.Areas).Any()) return true; }
        catch { }
      }
      return false;
    }

  }


  public class AuthorizeLocalAttribute : AuthorizeAttributeBaseCMS
  {
    protected override bool AuthorizeCore(HttpContextBase httpContext)
    {
      //
      //return base.AuthorizeCore(httpContext);
      //
      if (httpContext == null)
        throw new ArgumentNullException("httpContext");
      if (httpContext.Items["AuthorizeAttributeOverride"] != null)
        return (bool)httpContext.Items["AuthorizeAttributeOverride"];
      //
      if (IKGD_Config.IsLocalRequestWrapper)
      {
        return true;
      }
      throw new Exception("Richiesta proveniente da una connessione non autorizzata.");
      //return false;
    }

  }


  //
  // accesso consentito per richieste locali
  // oppure con user appartenente a user,roles specificati (se non sono stati specificati viene consentito l'accasso a root)
  //
  public class AuthorizeLocalOrRootAttribute : AuthorizeAttributeBaseCMS
  {
    public string Areas { get; set; }

    protected override bool AuthorizeCore(HttpContextBase httpContext)
    {
      //
      //return base.AuthorizeCore(httpContext);
      //
      if (httpContext == null)
        throw new ArgumentNullException("httpContext");
      if (httpContext.Items["AuthorizeAttributeOverride"] != null)
        return (bool)httpContext.Items["AuthorizeAttributeOverride"];
      //
      if (IKGD_Config.IsLocalRequestWrapper)
        return true;
      IPrincipal user = httpContext.User;
      if (user.Identity.IsAuthenticated && !MembershipHelper.IsAnonymous && FS_OperationsHelpers.IsRoot)
        return true;
      if (string.IsNullOrEmpty(this.Users) && string.IsNullOrEmpty(this.Roles))
        return true;
      if (!string.IsNullOrEmpty(this.Users) && Utility.Explode(this.Users, ",", " ", true).Any(n => string.Equals(n, user.Identity.Name)))
        return true;
      if (!string.IsNullOrEmpty(this.Roles) && Utility.Explode(this.Roles, ",", " ", true).Intersect(MembershipHelper.Roles).Any())
        return true;
      if (!string.IsNullOrEmpty(this.Areas))
      {
        try { if (Utility.Explode(this.Areas, ",", " ", true).Intersect(MembershipHelper.Areas).Any()) return true; }
        catch { }
      }
      return false;
    }

  }


  //
  // inizializza e rilascia la connessione al DB di LazyLogin
  // popola userName se non settato
  // se anonymous -> userName == null
  // inizializza il parametro lazyLoginMapper per utenti non anonimi
  //
  public class LazyLoginACLFilterAttribute : LazyLoginFilterAttribute
  {
    //public bool AutoSubmit { get; set; }  // ereditato
    public bool? AllowAnonymous { get; set; }
    public bool? AllowUnVerifiedMembership { get; set; }
    //TODO: aggiungere le tabelle correlate da caricare (come oggetti Type in una param list del costruttore del filtro)


    public override void OnActionExecuting(ActionExecutingContext filterContext)
    {
      base.OnActionExecuting(filterContext);
      //
      string userName = Ikon.GD.MembershipHelper.UserName;
      if ((!filterContext.ActionParameters.ContainsKey("userName") || filterContext.ActionParameters["userName"] == null) && userName != null)
        filterContext.ActionParameters["userName"] = userName;
      //
      if (Ikon.GD.MembershipHelper.IsAnonymous && AllowAnonymous == false)
      {
        throw new Exception("Anonymous access not permitted.");
      }
      if (!Ikon.GD.MembershipHelper.IsMembershipVerified && AllowUnVerifiedMembership == false)
      {
        throw new Exception("Unverified membership access not permitted.");
      }
      //
    }

  }


  public class LazyLoginFilterNoAnonymousAttribute : ActionFilterAttribute
  {
    public LazyLoginFilterNoAnonymousAttribute()
      : base()
    { }


    public override void OnActionExecuting(ActionExecutingContext filterContext)
    {
      if (MembershipHelper.IsAnonymous)
      {
        filterContext.HttpContext.Response.Redirect(Utility.UriSetQuery(System.Web.Security.FormsAuthentication.LoginUrl, "ReturnUrl", filterContext.HttpContext.Request.Url.ToString()), false);
        //filterContext.HttpContext.Response.End();
        filterContext.HttpContext.ApplicationInstance.CompleteRequest();  // da usare al posto di .Response.End();
      }
      base.OnActionExecuting(filterContext);
    }

  }


  public class LazyLoginFilterSimpleAttribute : ActionFilterAttribute
  {
    public LazyLoginFilterSimpleAttribute()
      : base()
    { }


    public override void OnActionExecuting(ActionExecutingContext filterContext)
    {
      base.OnActionExecuting(filterContext);
      //
      ILazyLoginMapper lazyLoginMapper = MembershipHelper.MembershipSession.EnsureLazyLoginMapper();
      filterContext.ActionParameters["lazyLoginMapper"] = lazyLoginMapper;
      //
    }

  }


  //
  // inizializza l'oggetto LazyLoginMapper con le eventuali dipendenze richieste
  // utilizza IoC con submitChanges al termine delle operazioni
  // opera direttamente sull'utente corrente
  // se non sono richieste dipendenze allora fornisce il valore salvato
  //
  public class LazyLoginFilterAttribute : ActionFilterAttribute
  {
    public bool AutoSubmit { get; set; }
    public Type[] ChildDependencies { get; set; }
    public ILazyLoginDataContext DC { get; set; }


    public LazyLoginFilterAttribute(params Type[] ChildDependencies)
      : base()
    {
      AutoSubmit = true;
      this.ChildDependencies = ChildDependencies;
    }


    public override void OnActionExecuting(ActionExecutingContext filterContext)
    {
      base.OnActionExecuting(filterContext);
      //
      try
      {
        DC = LazyLoginFilterHelpers.GetDefaultDataContext();
        ILazyLoginMapper lazyLoginMapper = MembershipHelper.MembershipSession.EnsureLazyLoginMapper(DC);
        if (lazyLoginMapper.GetType() != DC.GetLazyLoginMapperType())
          lazyLoginMapper = MembershipHelper.MembershipSession.LazyLoginMapperObject = LazyLoginFilterHelpers.GetDefaultDataContext().GetLazyLoginMapper(AutoSubmit);
        //
        filterContext.ActionParameters["lazyLoginMapper"] = lazyLoginMapper;
        //
        if (lazyLoginMapper != null && ChildDependencies != null && ChildDependencies.Length > 0)
        {
          lazyLoginMapper.EnsureChildDependencies(ChildDependencies);
        }
      }
      catch { }
      //
    }


    public override void OnResultExecuted(ResultExecutedContext filterContext)
    {
      base.OnResultExecuted(filterContext);
      //
      if (AutoSubmit != false && DC != null)
      {
        try
        {
          // le chiamate dirette senza usare una classe derivata non sembrano funzionare...
          //var chg = (DC as DataContext).GetChangeSet();
          //(DC as DataContext).SubmitChanges();
          //
          //var chg = DC.GetType().InvokeMember("GetChangeSet", System.Reflection.BindingFlags.InvokeMethod, null, DC, null);
          DC.GetType().InvokeMember("SubmitChanges", System.Reflection.BindingFlags.InvokeMethod, null, DC, null);
        }
        catch { }
      }
      //
    }
  }


}
