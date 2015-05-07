using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using Autofac;
using LinqKit;

using Ikon;
using Ikon.Support;
using Ikon.GD;
using Ikon.IKCMS;
using Ikon.IKCMS.Library.Resources;


namespace Ikon.IKCMS
{

  [HandleError]
  //[CompressFilter()]
  [CacheFilter(86400)]
  //[OutputCache(CacheProfile = "CacheIKCMS")]
  //[Microsoft.Web.Mvc.ControllerSessionState(Microsoft.Web.Mvc.ControllerSessionState.ReadOnly)]
  public abstract class HomeControllerBase : AutoStaticCMS_Controller
  {

    //protected override void OnActionExecuted(ActionExecutedContext filterContext)
    //{
    //  base.OnActionExecuted(filterContext);
    //  try { Response.AppendToLog("ip-src-" + Ikon.Utility.GetRequestAddressExt(null)); }
    //  catch { }
    //}


    public virtual ActionResult Index()
    {
      if (!string.IsNullOrEmpty(Ikon.GD.IKGD_Config.AppSettings["Page_Home"]))
      {
        return Redirect(Ikon.GD.IKGD_Config.AppSettings["Page_Home"]);
      }
      return Home();
    }


    public virtual ActionResult Home()
    {
      ViewData.Model = GetDefaultModel("/Home");
      try { (ViewData.Model as IKCMS_ModelCMS_Interface).RegisterHit(ViewData, (int)IKCMS_HitLogger.IKCMS_HitLogActionCodeEnum.CMS, (int)IKCMS_HitLogger.IKCMS_HitLogActionSubCodeEnum.Home); }
      catch { }
      return View();
    }


    public virtual ActionResult SiteMap()
    {
      //ViewData.Model = IKCMS_ModelCMS_Provider.Provider.ModelBuildFromContext();
      ViewData.Model = GetDefaultModel();

      return View();
    }


  }
}
