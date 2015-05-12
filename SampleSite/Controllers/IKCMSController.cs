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
using Microsoft.Web.Mvc;
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
  public class IKCMSController : IKCMSControllerBase
  {
    public override void BuildModelCustom(ActionExecutingContext filterContext)
    {
      try { (ViewData.Model as IKCMS_ModelCMS_Interface).RegisterHit(ViewData, (int)IKCMS_HitLogger.IKCMS_HitLogActionCodeEnum.CMS, (int)IKCMS_HitLogger.IKCMS_HitLogActionSubCodeEnum.PageCMS); }
      catch { }
    }
  }


  [CacheFilter(86400)]
  public class SearchController : SearchControllerBase
  {
    public override ActionResult SearchCMS(string searchCMS)
    {
      //
      //ViewData.Model = IKCMS_ModelCMS_Provider.Provider.ModelBuildFromContext();
      ViewData.Model = GetDefaultModel();
      try { (ViewData.Model as IKCMS_ModelCMS_Interface).RegisterHit(ViewData, (int)IKCMS_HitLogger.IKCMS_HitLogActionCodeEnum.CMS, (int)IKCMS_HitLogger.IKCMS_HitLogActionSubCodeEnum.SearchCMS); }
      catch { }
      //
      // chiamata del metodo di libreria
      ActionResult result = base.SearchCMS(searchCMS);
      //return result;
      //
      ViewData["ViewTemplate"] = "~/Views/Search/Search";
      return View(ViewData["ViewTemplate"] as string);
    }
  }
}
