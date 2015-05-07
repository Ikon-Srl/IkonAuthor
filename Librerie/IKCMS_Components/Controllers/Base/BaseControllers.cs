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
using System.Reflection;
using Newtonsoft.Json;
using Autofac;

using Ikon;
using Ikon.GD;
using Ikon.IKCMS;
using Ikon.IKGD.Library.Resources;
using Ikon.IKCMS.Library.Resources;


namespace Ikon.IKCMS
{

  public abstract class DEBUG_Controller : Controller
  {
    public DEBUG_Controller()
    {
      IKCMS_ExecutionProfiler.AddMessage("DEBUG_Controller: CONSTRUCTOR");
    }

    protected override void OnActionExecuting(ActionExecutingContext filterContext)
    {
      IKCMS_ExecutionProfiler.AddMessage("DEBUG_Controller: OnActionExecuting START");
      base.OnActionExecuting(filterContext);
    }

    protected override void OnActionExecuted(ActionExecutedContext filterContext)
    {
      base.OnActionExecuted(filterContext);
      IKCMS_ExecutionProfiler.AddMessage("DEBUG_Controller: OnActionExecuted END");
    }

    protected override void OnResultExecuting(ResultExecutingContext filterContext)
    {
      IKCMS_ExecutionProfiler.AddMessage("DEBUG_Controller: OnResultExecuting START");
      base.OnResultExecuting(filterContext);
    }

    protected override void OnResultExecuted(ResultExecutedContext filterContext)
    {
      base.OnResultExecuted(filterContext);
      IKCMS_ExecutionProfiler.AddMessage("DEBUG_Controller: OnResultExecuted END");
    }
  }


  public abstract class VFS_Access_Controller : DEBUG_Controller
  {
    public static FS_Operations fsOp_ro { get { return IKCMS_ManagerIoC.requestContainer.ResolveNamed<FS_Operations>("readonly"); } }
    public static FS_Operations fsOp { get { return IKCMS_ManagerIoC.requestContainer.Resolve<FS_Operations>(); } }
    //
    // settings relativi all'autodetect dei devices
    // per la customizzazione dei detect assegnare AutoDetectDeviceWorkerSite e AutoDetectDeviceWorkerView nel global.asax
    //
    public bool? AutoDetectDeviceEnabledFlag { get; set; }
    public static readonly bool AutoDetectDeviceEnabled = Utility.TryParse<bool>(IKGD_Config.AppSettings["IKGD_SiteMode_AutoDetectDevice"], false);
    public static readonly bool? AutoDetectDeviceCheckForMobileEnabled = (ViewModeControllerBase.AutoDetectDeviceCheckForMobileRegEx != null);
    public static readonly bool? AutoDetectDeviceAllowRedirectEnabled = Utility.TryParse<bool?>(IKGD_Config.AppSettings["IKGD_SiteMode_AutoDetectDeviceAllowRedirectEnabled"]);
    public static Func<string> AutoDetectDeviceWorkerSite { get; set; }
    public static Func<string> AutoDetectDeviceWorkerView { get; set; }
    //


    protected override void OnActionExecuting(ActionExecutingContext filterContext)
    {
      base.OnActionExecuting(filterContext);
      if (AutoDetectDeviceEnabled && AutoDetectDeviceEnabledFlag.GetValueOrDefault(true))
      {
        ViewModeControllerBase.AutoDetectDeviceV2(AutoDetectDeviceWorkerSite, AutoDetectDeviceWorkerView, AutoDetectDeviceCheckForMobileEnabled, AutoDetectDeviceAllowRedirectEnabled);
      }
    }


    public virtual IKCMS_ModelCMS_Interface GetDefaultModel(params object[] args)
    {
      try { return new IKCMS_ModelCMS_Dumb(); }
      catch { }
      return null;
    }


    public virtual IKCMS_ModelCMS_Interface GetDefaultModelWithContext(params object[] args)
    {
      try
      {
        if (args != null && args.Length > 0)
          return AutoStaticCMS_Controller.GetDefaultModelHelper(args);
        else
        {
          var baseArgs = new object[] { null, null, null, null };
          return AutoStaticCMS_Controller.GetDefaultModelHelper(baseArgs.Concat(this.ControllerContext.RouteData.Values.Select(r => r.Value)).ToArray());
          //return AutoStaticCMS_Controller.GetDefaultModelHelper(null, null, null, null, this.ControllerContext);
        }
      }
      catch { }
      return null;
    }

  }


  public abstract class AutoStaticCMS_Controller : VFS_Access_Controller
  {
    public bool? AutoModelSetup { get; set; }


    //protected override void OnActionExecuting(ActionExecutingContext filterContext)
    //{
    //  base.OnActionExecuting(filterContext);
    //}


    protected override void OnActionExecuted(ActionExecutedContext filterContext)
    {
      base.OnActionExecuted(filterContext);
      //
      if (ViewData.Model == null && AutoModelSetup.GetValueOrDefault(true))
      {
        //if (filterContext != null && filterContext.Result != null && filterContext.Result is JsonResult)
        //  return;
        //if ((Request.AcceptTypes != null && Request.AcceptTypes.Contains("application/json")) || (Response.ContentType ?? string.Empty).IndexOf("json", StringComparison.OrdinalIgnoreCase) >= 0)
        //  return;
        if (filterContext != null && filterContext.Result != null && filterContext.Result is ViewResultBase)
        {
          ViewData.Model = GetDefaultModel();
        }
      }
    }


    //protected override void OnResultExecuting(ResultExecutingContext filterContext)
    //{
    //  base.OnResultExecuting(filterContext);
    //}


    //protected override void OnResultExecuted(ResultExecutedContext filterContext)
    //{
    //  base.OnResultExecuted(filterContext);
    //}


    public override IKCMS_ModelCMS_Interface GetDefaultModel(params object[] args)
    {
      if (args != null && args.Length > 0)
        return GetDefaultModelHelper(args);
      else
      {
        var baseArgs = new object[] { null, null, null, null };
        return GetDefaultModelHelper(baseArgs.Concat(this.ControllerContext.RouteData.Values.Select(r => r.Value)).ToArray());
        //return GetDefaultModelHelper(null, null, null, null, this.ControllerContext);
      }
    }


    public static IKCMS_ModelCMS_Interface GetDefaultModelHelper(ControllerContext ctrlCtx)
    {
      var baseArgs = new object[] { null, null, null, null };
      return GetDefaultModelHelper(baseArgs.Concat(ctrlCtx.RouteData.Values.Select(r => r.Value)).ToArray());
    }


    public static IKCMS_ModelCMS_Interface GetDefaultModelHelper(params object[] args)
    {
      IKCMS_ExecutionProfiler.AddMessage("GetDefaultModelHelper: START");
      IKCMS_ModelCMS_Interface modelCMS = null;

      string cacheKey = IKCMS_ModelCMS_Provider.Provider.GetCacheKey(IKCMS_ModelCMS_Provider.Provider.GetNormalizedArgs(args));
      cacheKey = cacheKey.NullIfEmpty() ?? "AutoStaticCMS_Controller.GetDefaultModelHelper";
      if (args != null && args.Length > 0)
      {
        modelCMS = HttpRuntime.Cache[cacheKey] as IKCMS_ModelCMS_Interface;
        try { modelCMS = modelCMS ?? IKCMS_ModelCMS_Provider.Provider.ModelBuildGeneric(args); }
        catch { }
      }
      if (modelCMS == null)
      {
        try
        {
          Func<IKCMS_ModelCMS_Interface> modelBuilder = () =>
          {
            IKCMS_ModelCMS_Interface model = null;
            if (model == null)
              model = IKCMS_ModelCMS_Provider.Provider.ModelBuildFromContext(false);
            if (model == null)
            {
              int? model_rNode = Utility.TryParse<int?>(IKGD_Config.AppSettings["rNodeForPagesNoCMS"]);
              int? model_sNode = Utility.TryParse<int?>(IKGD_Config.AppSettings["sNodeForPagesNoCMS"]);
              if (model == null && model_rNode.GetValueOrDefault(-1) > 0)
                model = IKCMS_ModelCMS_Provider.Provider.ModelBuildGenericByRNODE(model_rNode.Value);
              if (model == null && model_sNode.GetValueOrDefault(-1) > 0)
                model = IKCMS_ModelCMS_Provider.Provider.ModelBuildGeneric(model_sNode.Value);
              if (model == null && !string.IsNullOrEmpty(IKGD_Config.AppSettings["PathForPagesNoCMS"]))
                model = IKCMS_ModelCMS_Provider.Provider.ModelBuildGeneric(IKGD_Config.AppSettings["PathForPagesNoCMS"]);
              if (model != null)
              {
                // se il model e' stato costruito da "PathForPagesNoCMS" allora disabilitiamo le url canoniche standard
                model.UrlCanonicalEnabled = false;
              }
            }
            return model;
          };
          //
          if (Utility.TryParse<bool>(IKGD_Config.AppSettingsWeb["CachingIKCMS_ModelsEnabled"], true) && Utility.TryParse<bool>(System.Web.HttpContext.Current.Request.QueryString["cacheOff"]) == false)
          {
            modelCMS = FS_OperationsHelpers.CachedEntityWrapper(cacheKey, () =>
            {
              IKCMS_ExecutionProfiler.AddMessage(string.Format("GetDefaultModelHelper: BuildingModelForCacheKey={0}", cacheKey));
              return modelBuilder();
            }
            , m => m != null && !m.HasExceptionsTree
            , Utility.TryParse<int>(IKGD_Config.AppSettings["CachingIKCMS_Models"], 3600), null, FS_OperationsHelpers.Const_CacheDependencyIKGD_vNode_vData_iNode_Relation_Property);
          }
          else
          {
            modelCMS = modelBuilder();
          }
        }
        catch { }
      }
      //
      if (modelCMS != null && IKCMS_ModelCMS_Provider.Provider.ModelForContext == null)
        IKCMS_ModelCMS_Provider.Provider.ModelForContext = modelCMS;
      //
      IKCMS_ExecutionProfiler.AddMessage("GetDefaultModelHelper: END");
      return modelCMS;
    }

  }


  //
  // simulazione di parte delle funzionalita' WebAPI in.NET 3.5
  // ritorna automaticamente un ActionResult anche in caso di return di altri oggetti.
  // https://www.simple-talk.com/dotnet/asp.net/simulating-web-api---json-formatters-in-asp.net-mvc/?utm_source=simpletalk&utm_medium=email-main&utm_content=simulatingwebapi-20120820&utm_campaign=.NET
  //
  public class ApiController : Controller
  {
    public ApiController()
    {
      ActionInvoker = new SimulWebApiActionInvoker();
    }
  }


  public class SimulWebApiActionInvoker : ControllerActionInvoker
  {
    protected override ActionResult CreateActionResult(ControllerContext controllerContext, ActionDescriptor actionDescriptor, Object actionReturnValue)
    {
      if (actionReturnValue == null)
        return new EmptyResult();
      var actionResult = actionReturnValue as ActionResult;
      if (actionResult == null)
      {
        return new JsonResult { Data = actionReturnValue, JsonRequestBehavior = JsonRequestBehavior.AllowGet };
      }
      return base.CreateActionResult(controllerContext, actionDescriptor, actionReturnValue);
    }
  }


}
