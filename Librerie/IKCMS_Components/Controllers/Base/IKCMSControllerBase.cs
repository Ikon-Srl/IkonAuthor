using System;
using System.Collections;
using System.Collections.Specialized;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Linq;
using System.Web;
using System.Web.Security;
using System.Web.Configuration;
using System.Web.UI;
using System.Web.UI.HtmlControls;
using System.Web.UI.WebControls;
using System.Web.UI.WebControls.WebParts;
using System.Web.Caching;
using System.Xml.Linq;
using System.Linq.Expressions;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Reflection;
using System.Web.Mvc;
using System.Web.Mvc.Ajax;
//using Microsoft.Web.Mvc;
using Autofac;
using LinqKit;

using Ikon;
using Ikon.Support;
using Ikon.GD;
using Ikon.IKCMS;
using Ikon.IKGD.Library.Resources;
using Ikon.IKCMS.Library.Resources;
using Ikon.IKGD.Library.Collectors;


namespace Ikon.IKCMS
{


  //[CompressFilter()]
  [CacheFilter(86400)]
  //[OutputCache(CacheProfile = "CacheIKCMS")]
  //[Microsoft.Web.Mvc.ControllerSessionState(Microsoft.Web.Mvc.ControllerSessionState.ReadOnly)]
  public abstract class IKCMSControllerBase : DEBUG_Controller
  {
    // se settato non procede a fare le verifiche che sia stato settato il template e ulteriori postprocessing
    public bool RedirectionRequested { get; set; }
    public bool ForcePartialView { get; set; }

    public bool ResourceNotFoundCMS { get; set; }
    public bool ForceHandlingCMS404 { get; set; }
    public static bool? _HandlingCMS404Enabled;
    public static bool HandlingCMS404Enabled { get { return _HandlingCMS404Enabled ?? (_HandlingCMS404Enabled = Utility.TryParse<bool>(IKGD_Config.AppSettings["HandlingCMS404Enabled"], false)).Value; } }


    protected override void OnException(ExceptionContext filterContext)
    {
      if (HandlingCMS404Enabled || ForceHandlingCMS404)
      {
        filterContext.ExceptionHandled = true;
        if (filterContext.HttpContext.Response.StatusCode != 404 && filterContext.HttpContext.Request.HttpMethod != "HEAD")
        {
          filterContext.HttpContext.Response.StatusCode = 404;
        }
      }
      base.OnException(filterContext);
    }


    protected override void HandleUnknownAction(string actionName)
    {
      base.HandleUnknownAction(actionName);
    }


    protected override void OnActionExecuting(ActionExecutingContext filterContext)
    {
      base.OnActionExecuting(filterContext);
      //
      if (!RedirectionRequested)
      {
        if (VFS_Access_Controller.AutoDetectDeviceEnabled)
        {
          ViewModeControllerBase.AutoDetectDeviceV2(VFS_Access_Controller.AutoDetectDeviceWorkerSite, VFS_Access_Controller.AutoDetectDeviceWorkerView, VFS_Access_Controller.AutoDetectDeviceCheckForMobileEnabled, VFS_Access_Controller.AutoDetectDeviceAllowRedirectEnabled);
        }
        BuildModel(filterContext);
      }
      //
    }


    protected override void OnActionExecuted(ActionExecutedContext filterContext)
    {
      base.OnActionExecuted(filterContext);
      //
      if (HandlingCMS404Enabled && ResourceNotFoundCMS && filterContext.HttpContext.Request.HttpMethod != "HEAD")
      {
        //
        // attivazione response 404
        filterContext.HttpContext.Response.StatusCode = 404;
        return;
      }
      //
      if (!RedirectionRequested)
      {
        string PageToRedirectTo = null;
        if (string.IsNullOrEmpty(ViewData["ViewTemplate"] as string))
          PageToRedirectTo = "~/";
        //TODO: rinviare alla pagina di risorsa non trovata con search integrato
        if (!string.IsNullOrEmpty(PageToRedirectTo))
        {
          Response.Redirect(PageToRedirectTo, true);
        }
      }
    }


    public virtual void BuildModel(ActionExecutingContext filterContext)
    {
      try
      {
        string pagePath = (string)filterContext.ActionParameters.TryGetValueMV("pagePath");
        string indexPath = (string)filterContext.ActionParameters.TryGetValueMV("indexPath");
        string modulePath = (string)filterContext.ActionParameters.TryGetValueMV("modulePath");
        string moduleOp = (string)filterContext.ActionParameters.TryGetValueMV("moduleOp");
        int? sNodeModule = Utility.TryParse<int?>(filterContext.ActionParameters.TryGetValueMV("sNodeModule"), null);
        int? sNodeItem = Utility.TryParse<int?>(filterContext.ActionParameters.TryGetValueMV("sNodeItem"), null);
        int? rNodeModule = Utility.TryParse<int?>(filterContext.ActionParameters.TryGetValueMV("rNodeModule"), null);
        int? rNodeItem = Utility.TryParse<int?>(filterContext.ActionParameters.TryGetValueMV("rNodeItem"), null);
        string xNodeItem = (string)filterContext.ActionParameters.TryGetValueMV("xNodeItem");
        string xNodeModule = (string)filterContext.ActionParameters.TryGetValueMV("xNodeModule");
        string Language = ((string)filterContext.ActionParameters.TryGetValueMV("Language")).TrimSafe().ToLower();
        bool? MultiPageBackScan = Utility.TryParse<bool?>(filterContext.ActionParameters.TryGetValueMV("MultiPageBackScan"), null);
        //
        // selezione del model builder corretto per il tipo di richiesta
        //
        IKCMS_ModelCMS_Interface modelCMS = null;
        IKCMS_ModelCMS_Interface modelRoot = null;
        IKCMS_ModelCMS_Page_Interface modelPage = null;
        try
        {
          if (Language.IsNotEmpty())
          {
            if (Language.Length == 2 && IKGD_Language_Provider.Provider.LanguagesAvailable().Contains(Language))
            {
              if (IKGD_Language_Provider.Provider.Language != Language)
              {
                IKGD_Language_Provider.Provider.LanguageContext = Language;
              }
            }
          }
          if (xNodeItem.IsNotEmpty())
          {
            if (xNodeItem.StartsWith("r", StringComparison.OrdinalIgnoreCase))
            {
              rNodeItem = Utility.TryParse<int?>(xNodeItem.Substring(1));
            }
            else
            {
              if (Language == "rnode")
                rNodeItem = Utility.TryParse<int?>(xNodeItem);
              else
                sNodeItem = Utility.TryParse<int?>(xNodeItem);
            }
          }
          if (xNodeModule.IsNotEmpty())
          {
            if (xNodeModule.StartsWith("r", StringComparison.OrdinalIgnoreCase))
            {
              rNodeModule = Utility.TryParse<int?>(xNodeModule.Substring(1));
            }
            else
            {
              if (Language == "rnode")
                rNodeModule = Utility.TryParse<int?>(xNodeModule);
              else
                sNodeModule = Utility.TryParse<int?>(xNodeModule);
            }
          }
          if (MultiPageBackScan != null)
          {
            IKCMS_ModelCMS_Provider.Provider.IsMultiPageBackScanEnabled = MultiPageBackScan.Value;
          }
          //
          // il this.ControllerContext lo aggiungiamo alla lista dei parametri per distinguere i models costruiti dal controller del CMS da quelli istanziati nelle ricorsioni del modelbuilder
          //
          if (rNodeItem != null || rNodeModule != null)
          {
            modelCMS = IKCMS_ModelCMS_Provider.Provider.ModelBuildGenericByRNODE(rNodeItem.Value, (object)rNodeModule ?? (object)modulePath, moduleOp, indexPath, this.ControllerContext);
          }
          else
          {
            modelCMS = IKCMS_ModelCMS_Provider.Provider.ModelBuildGeneric((object)sNodeItem ?? (object)pagePath, (object)sNodeModule ?? (object)modulePath, moduleOp, indexPath, this.ControllerContext);
          }
          //
          // nel caso non sia stato possibile costruire il model
          // si tenta di caricare un default decente
          //
          if (modelCMS == null)
          {
            try
            {
              ResourceNotFoundCMS = true;
              IKCMS_ExecutionProfiler.AddMessage("IKCMSControllerBase: BuildModel modelCMS is NULL after first attempt");
              int? model_rNode = Utility.TryParse<int?>(IKGD_Config.AppSettings["rNodeForPagesNoCMS"]);
              int? model_sNode = Utility.TryParse<int?>(IKGD_Config.AppSettings["sNodeForPagesNoCMS"]);
              if (modelCMS == null && model_rNode.GetValueOrDefault() > 0)
                modelCMS = IKCMS_ModelCMS_Provider.Provider.ModelBuildGenericByRNODE(model_rNode.Value);
              if (modelCMS == null && model_sNode.GetValueOrDefault() > 0)
                modelCMS = IKCMS_ModelCMS_Provider.Provider.ModelBuildGeneric(model_sNode.Value);
              ResourceNotFoundCMS &= modelCMS == null;
              if (modelCMS == null && !string.IsNullOrEmpty(IKGD_Config.AppSettings["PathForPagesNoCMS"]))
                modelCMS = IKCMS_ModelCMS_Provider.Provider.ModelBuildGeneric(IKGD_Config.AppSettings["PathForPagesNoCMS"]);
              if (modelCMS != null)
              {
                // se il model e' stato costruito da "PathForPagesNoCMS" allora disabilitiamo le url canoniche standard
                modelCMS.UrlCanonicalEnabled = false;
              }
            }
            catch { }
          }
          //
          modelRoot = modelCMS.ModelRoot;
          modelPage = modelCMS.ModelRoot as IKCMS_ModelCMS_Page_Interface;
          if (ViewData["ViewTemplate"] == null && modelCMS != null && modelCMS is IKCMS_ModelCMS_HasTemplateInfo_Interface)
          {
            if (modelCMS.IsExpired)
            {
              try { ViewData["ViewTemplate"] = IKCMS_TemplatesTypeHelper.GetTemplate("Expired").ViewPath; }
              catch { }
            }
            else if ((modelCMS as IKCMS_ModelCMS_HasTemplateInfo_Interface).TemplateInfo != null)
            {
              try
              {
                ViewData["ViewTemplate"] = (modelCMS as IKCMS_ModelCMS_HasTemplateInfo_Interface).TemplateViewPath;
                // se stiamo caricando un model da Url e questo e' una risorsa non di tipo PageCMS ed e' definito un template di tipo "context" allora lo utilizziamo al posto di quello di default (che potrebbe essere di tipo "item")
                if (Request.Params["PartialViewTemplateCode"].IsNotNullOrWhiteSpace())
                {
                  if ((modelCMS as IKCMS_ModelCMS_HasTemplateInfo_Interface).TemplateInfo.ViewPaths.ContainsKey(Request.Params["PartialViewTemplateCode"]))
                  {
                    ViewData["ViewTemplate"] = (modelCMS as IKCMS_ModelCMS_HasTemplateInfo_Interface).TemplateInfo.ViewPaths[Request.Params["PartialViewTemplateCode"]];
                    ForcePartialView = true;
                  }
                }
                else if (Request.Params["ViewTemplateCode"].IsNotNullOrWhiteSpace())
                {
                  if ((modelCMS as IKCMS_ModelCMS_HasTemplateInfo_Interface).TemplateInfo.ViewPaths.ContainsKey(Request.Params["ViewTemplateCode"]))
                  {
                    ViewData["ViewTemplate"] = (modelCMS as IKCMS_ModelCMS_HasTemplateInfo_Interface).TemplateInfo.ViewPaths[Request.Params["ViewTemplateCode"]];
                  }
                }
                else if (Request.IsAjaxRequest() && Request.AcceptTypes.Any(r => string.Equals(r, "text/html", StringComparison.OrdinalIgnoreCase)))
                {
                  if ((modelCMS as IKCMS_ModelCMS_HasTemplateInfo_Interface).TemplateInfo.ViewPaths.ContainsKey("ajax_partial"))
                  {
                    ViewData["ViewTemplate"] = (modelCMS as IKCMS_ModelCMS_HasTemplateInfo_Interface).TemplateInfo.ViewPaths["ajax_partial"];
                    ForcePartialView = true;
                  }
                  else if ((modelCMS as IKCMS_ModelCMS_HasTemplateInfo_Interface).TemplateInfo.ViewPaths.ContainsKey("ajax"))
                  {
                    ViewData["ViewTemplate"] = (modelCMS as IKCMS_ModelCMS_HasTemplateInfo_Interface).TemplateInfo.ViewPaths["ajax"];
                  }
                }
                else if ((modelCMS as IKCMS_ModelCMS_HasTemplateInfo_Interface).TemplateInfo.ViewPaths.ContainsKey("context"))
                {
                  ViewData["ViewTemplate"] = (modelCMS as IKCMS_ModelCMS_HasTemplateInfo_Interface).TemplateInfo.ViewPaths["context"];
                }
              }
              catch { }
            }
          }
          if (ViewData["ViewTemplate"] == null && modelRoot is IKCMS_ModelCMS_HasTemplateInfo_Interface)
          {
            try { ViewData["ViewTemplate"] = (modelRoot as IKCMS_ModelCMS_HasTemplateInfo_Interface).TemplateViewPath; }
            catch { }
          }
          if (string.IsNullOrEmpty(ViewData["ViewTemplate"] as string))
          {
            // per avere sempre una view definita (sembra che anche in caso di redirect continui il processing della request)
            ViewData["ViewTemplateFake"] = IKGD_Config.AppSettings["MasterPageDefault"] ?? "~/Views/Layouts/Application";
          }
        }
        catch { }
        ViewData.Model = modelCMS;
        //
        // check delle ACL per forzatura del login
        //
        if (modelCMS != null)
        {
          bool redirectToLogin = false;
          try
          {
            var areas = FS_OperationsHelpers.CachedAreasExtended;
            modelCMS.BackRecurseOnModels.OfType<IKCMS_ModelCMS_VFS_Interface>().ForEach(mdl =>
            {
              if (!redirectToLogin)
              {
                redirectToLogin |= (mdl.PathVFS.Fragments.Any(f => (f.FlagsMenu & FlagsMenuEnum.LazyLoginNoAnonymous) == FlagsMenuEnum.LazyLoginNoAnonymous) && (MembershipHelper.IsAnonymous));
              }
              if (!redirectToLogin)
              {
                //redirectToLogin |= (mdl.PathVFS.Fragments.All(f => string.IsNullOrEmpty(f.Area) || areas.Contains(f.Area)) == false);
                redirectToLogin |= (mdl.PathVFS.Fragments.Any(f => !string.IsNullOrEmpty(f.Area) && areas.AreasDenied.Contains(f.Area)));
              }
              if (!redirectToLogin)
              {
                redirectToLogin |= (mdl.PathVFS.Fragments.Any(f => (f.FlagsMenu & FlagsMenuEnum.LoginRequired) == FlagsMenuEnum.LoginRequired) && (HttpContext.User == null || HttpContext.User.Identity.IsAuthenticated == false));
              }
            });
          }
          catch
          {
            //redirectToLogin = true;
          }
          if (redirectToLogin)
          {
            FormsAuthentication.RedirectToLoginPage();
            //Response.End();
            HttpContext.ApplicationInstance.CompleteRequest();  // da usare al posto di .Response.End();
            return;
          }
        }
        //
        // forzatura del redirect nel caso sia necessario per pagine non CMS
        //
        if (modelCMS is IKCMS_ModelCMS_PageStatic_Interface)
        {
          try
          {
            string url = (modelCMS.VFS_ResourceObject.ResourceSettingsObject as IKCMS_ResourceType_PageStatic.WidgetSettingsType).UrlExternal;
            if (!string.IsNullOrEmpty(url))
            {
              string urlToRedirect = Utility.ResolveUrl(url);
              try { urlToRedirect = Utility.UriMigrateQueryString(Request.Url.ToString(), urlToRedirect, true); }
              catch { }
              //
              // attenzione che probabilmente sara' comunque necessario gestire i ViewData nel controller o nella view (nel caso la pagina non venisse caricata da parh o controller CMS)
              ViewData["HitLogActionCode"] = ((int)Ikon.IKCMS.IKCMS_HitLogger.IKCMS_HitLogActionCodeEnum.CMS).ToString();
              ViewData["HitLogActionSubCode"] = ((int)Ikon.IKCMS.IKCMS_HitLogger.IKCMS_HitLogActionSubCodeEnum.PageStaticCMS).ToString();
              //
              Response.Redirect(urlToRedirect, true);
              //Response.End();
              HttpContext.ApplicationInstance.CompleteRequest();  // da usare al posto di .Response.End();
              return;
            }
          }
          catch { }
        }
        //
        IKCMS_ExecutionProfiler.AddMessage("BuildModelCustom: START");
        try { BuildModelCustom(filterContext); }
        catch { }
        IKCMS_ExecutionProfiler.AddMessage("BuildModelCustom: END");
        //
      }
      //catch (Exception ex)
      //{
      //  Elmah.ErrorSignal.FromCurrentContext().Raise(ex);
      //}
      catch { }
    }


    //
    // da ridefinire nella classe derivata
    //
    public virtual void BuildModelCustom(ActionExecutingContext filterContext)
    {
    }


    //
    // Action Handlers
    //


    public ActionResult Index()
    {
      return View();
    }


    //Regex(@"^(?<Language>([a-z]{2}|code|rnode))/(?<xNodeItem>[rR]{0,1}[\d]+)(/(?<xNodeModule>[rR]{0,1}[\d]+)){0,1}(?<indexPath>.*)$", RegexOptions.Compiled | RegexOptions.IgnoreCase)
    public ActionResult PageCMS_byCodeWithLanguage(string Language, string xNodeItem, string xNodeModule, string indexPath, string moduleOp)
    {
      if ((Request.AcceptTypes != null && Request.AcceptTypes.Contains("application/json")) || Request.QueryString["forceJsonOutput"] == "true")
      {
        return Content(IKGD_Serialization.SerializeToJSON(ViewData.Model), "application/json");
      }
      if (ForcePartialView)
      {
        return PartialView((ViewData["ViewTemplate"] ?? ViewData["ViewTemplateFake"]) as string);
      }
      else
      {
        return View((ViewData["ViewTemplate"] ?? ViewData["ViewTemplateFake"]) as string);
      }
    }


    // semplice wildcard "{*path}"
    public ActionResult PageCMS_byPathDirect(string pagePath)
    {
      if (Request.AcceptTypes != null && Request.AcceptTypes.Contains("application/json"))
      {
        return Content(IKGD_Serialization.SerializeToJSON(ViewData.Model), "application/json");
      }
      if (ForcePartialView)
      {
        return PartialView((ViewData["ViewTemplate"] ?? ViewData["ViewTemplateFake"]) as string);
      }
      else
      {
        return View((ViewData["ViewTemplate"] ?? ViewData["ViewTemplateFake"]) as string);
      }
    }


    //Regex(@"^CMS/(?<modulePath>.+)/(?<moduleOp>(index|item))/(?<indexPath>.*)$", RegexOptions.Compiled | RegexOptions.IgnoreCase)
    public ActionResult BrowserIndexCMS(string modulePath, string moduleOp, string indexPath)
    {
      if (Request.AcceptTypes != null && Request.AcceptTypes.Contains("application/json"))
      {
        return Content(IKGD_Serialization.SerializeToJSON(ViewData.Model), "application/json");
      }
      if (ForcePartialView)
      {
        return PartialView((ViewData["ViewTemplate"] ?? ViewData["ViewTemplateFake"]) as string);
      }
      else
      {
        return View((ViewData["ViewTemplate"] ?? ViewData["ViewTemplateFake"]) as string);
      }
    }


    //Regex(@"^CMS/(?<pagePath>.+?)$", RegexOptions.Compiled | RegexOptions.IgnoreCase)
    public ActionResult PageCMS_byPath(string pagePath)
    {
      if (Request.AcceptTypes != null && Request.AcceptTypes.Contains("application/json"))
      {
        return Content(IKGD_Serialization.SerializeToJSON(ViewData.Model), "application/json");
      }
      if (ForcePartialView)
      {
        return PartialView((ViewData["ViewTemplate"] ?? ViewData["ViewTemplateFake"]) as string);
      }
      else
      {
        return View((ViewData["ViewTemplate"] ?? ViewData["ViewTemplateFake"]) as string);
      }
    }


    //Regex(@"^code/(?<sNodeItem>[\d]*)/(?<sNodeModule>[\d]*)(?<indexPath>.*)$", RegexOptions.Compiled | RegexOptions.IgnoreCase)
    //public ActionResult PageCMS_byCode(int? sNodeItem, int? sNodeModule, string indexPath, string moduleOp)
    //{
    //  if ((Request.AcceptTypes != null && Request.AcceptTypes.Contains("application/json")) || Request.QueryString["forceJsonOutput"] == "true")
    //  {
    //    return Content(IKGD_Serialization.SerializeToJSON(ViewData.Model), "application/json");
    //  }
    //  if (ForcePartialView)
    //  {
    //    return PartialView((ViewData["ViewTemplate"] ?? ViewData["ViewTemplateFake"]) as string);
    //  }
    //  else
    //  {
    //    return View((ViewData["ViewTemplate"] ?? ViewData["ViewTemplateFake"]) as string);
    //  }
    //}


    //Regex(@"^rnode/(?<rNodeItem>[\d]*)/(?<rNodeModule>[\d]*)(?<indexPath>.*)$", RegexOptions.Compiled | RegexOptions.IgnoreCase)
    //public ActionResult PageCMS_byCodeRNODE(int? rNodeItem, int? rNodeModule, string indexPath, string moduleOp)
    //{
    //  if (Request.AcceptTypes != null && Request.AcceptTypes.Contains("application/json") || Request.QueryString["forceJsonOutput"] == "true")
    //  {
    //    return Content(IKGD_Serialization.SerializeToJSON(ViewData.Model), "application/json");
    //  }
    //  if (ForcePartialView)
    //  {
    //    return PartialView((ViewData["ViewTemplate"] ?? ViewData["ViewTemplateFake"]) as string);
    //  }
    //  else
    //  {
    //    return View((ViewData["ViewTemplate"] ?? ViewData["ViewTemplateFake"]) as string);
    //  }
    //}


    // per ottenere la url di default di un modulo browse
    public ActionResult BrowserModuleFor(string rNodeArchives, string indexPath, string moduleOp)
    {
      FS_Operations fsOp = IKCMS_ManagerIoC.requestContainer.ResolveNamed<FS_Operations>("readonly");
      var url = IKCMS_ModelCMS_ArchiveBrowserHelper.GetUrlForBrowserModule(fsOp, Utility.ExplodeT<int>(rNodeArchives, ",", " ", true).ToArray());
      if (!string.IsNullOrEmpty(url))
      {
        RedirectionRequested = true;
        return Redirect(url);
      }
      return null;
    }


    // per costruire direttamente un model (con eventuale forzatura della view) da usare, ad esempio, per delle popup di risorse CMS che non siano delle pagine normali
    public ActionResult ResourceWithView(int? sNode, int? rNode, string view) { return ResourceWithViewAndMaster(sNode, rNode, view, IKGD_Config.AppSettings["MasterPageNoAjax"]); }
    public ActionResult ResourceWithViewAndMaster(int? sNode, int? rNode, string view, string autoMaster)
    {
      try
      {
        if (sNode != null || rNode != null)
        {
          ViewData.Model = (rNode != null) ? IKCMS_ModelCMS_Provider.Provider.ModelBuildGenericByRNODE(rNode.Value) : IKCMS_ModelCMS_Provider.Provider.ModelBuildGeneric(sNode.Value);
          ResourceNotFoundCMS &= ViewData.Model == null;
          if (Request.AcceptTypes != null && Request.AcceptTypes.Contains("application/json"))
          {
            return Content(IKGD_Serialization.SerializeToJSON(ViewData.Model), "application/json");
          }
          if (ViewData.Model is IKCMS_ModelCMS_HasTemplateInfo_Interface)
            view = view ?? (ViewData.Model as IKCMS_ModelCMS_HasTemplateInfo_Interface).TemplateViewPath;
          if (ViewData.Model != null && !string.IsNullOrEmpty(view))
          {
            if (view.StartsWith("%"))
              view = HttpUtility.UrlDecode(view);
            ViewData["ViewTemplate"] = view;
            if (!Request.IsAjaxRequest() && !string.IsNullOrEmpty(autoMaster))
            {
              if (autoMaster.StartsWith("%"))
                autoMaster = HttpUtility.UrlDecode(autoMaster);
              return View(view, autoMaster);
            }
            return PartialView(view);
          }
        }
      }
      catch { }
      return null;
    }


    //
    // usage:
    // ~/StaticPage/{*pathInfo}  -->  carica la view (solo nome file e non path) cercandola nel folder ~/Views/Custom/Page
    //
    public ActionResult StaticPage(string pathInfo)
    {
      //
      IKCMS_ModelCMS_Provider.Provider.ModelForContext = null;
      IKCMS_ModelCMS_Provider.Provider.ModelBaseForContext = null;
      //
      var baseArgs = new object[] { null, null, null, null };
      ViewData.Model = AutoStaticCMS_Controller.GetDefaultModelHelper(baseArgs.Concat(this.ControllerContext.RouteData.Values.Select(r => r.Value)).ToArray());
      if (pathInfo.IsNotEmpty() && pathInfo.IndexOf(@"/") != 0 && pathInfo.IndexOf(@"..") < 0)
      {
        ResourceNotFoundCMS = false;
        ForceHandlingCMS404 = true;
        return View(string.Format("~/Views/Custom/Page/{0}", pathInfo));
      }
      return null;
    }


    //
    // usage:
    // ~/StaticPartial/{*pathInfo}  -->  carica la view (solo nome file e non path) cercandola nel folder ~/Views/Custom/Page
    //
    public ActionResult StaticPartial(string pathInfo)
    {
      //
      IKCMS_ModelCMS_Provider.Provider.ModelForContext = null;
      IKCMS_ModelCMS_Provider.Provider.ModelBaseForContext = null;
      //
      var baseArgs = new object[] { null, null, null, null };
      ViewData.Model = AutoStaticCMS_Controller.GetDefaultModelHelper(baseArgs.Concat(this.ControllerContext.RouteData.Values.Select(r => r.Value)).ToArray());
      if (pathInfo.IsNotEmpty() && pathInfo.IndexOf(@"/") != 0 && pathInfo.IndexOf(@"..") < 0)
      {
        ResourceNotFoundCMS = false;
        ForceHandlingCMS404 = true;
        return PartialView(string.Format("~/Views/Custom/Page/{0}", pathInfo));
      }
      return null;
    }


    //
    // usage:
    // ~/404
    //
    public ActionResult NotFound()
    {
      ResourceNotFoundCMS = true;
      ForceHandlingCMS404 = true;
      try
      {
        string reqOrig = HttpContext.Request.Url.Query.Split(";".ToCharArray(), 2).Skip(1).FirstOrDefault();
        Uri urlOrig = new Uri(Request.Url, reqOrig);
        ViewData["RequestUrl"] = urlOrig;
        ViewData["RequestPath"] = urlOrig.AbsolutePath;
        ViewData["RequestLastFrag"] = urlOrig.AbsolutePath.Split('/').LastOrDefault();
        ViewData["RequestSearch"] = Regex.Replace(HttpUtility.UrlDecode(urlOrig.AbsolutePath.Split('/').LastOrDefault() ?? string.Empty), @"[\s\p{P}\p{S}\p{M}\p{Z}\p{C}]|^r{0,1}\d+$", " ", RegexOptions.IgnoreCase | RegexOptions.Singleline).TrimSafe();
      }
      catch { }
      //viene gia' settato in OnActionExecuted
      //HttpContext.Response.StatusCode = 404;
      return View("~/Views/IKCMS/Templates/404");
    }


    public ActionResult WakeUp()
    {
      return null;
    }

  }
}
