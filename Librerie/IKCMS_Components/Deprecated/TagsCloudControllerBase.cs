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

  public interface TagsCloudController_Interface
  {
    ActionResult Index(string id);
    ActionResult TagsCloud();
  }


  [Microsoft.Web.Mvc.ControllerSessionState(Microsoft.Web.Mvc.ControllerSessionState.ReadOnly)]
  public abstract class TagsCloudControllerBase : AutoStaticCMS_Controller, TagsCloudController_Interface
  {

    public virtual ActionResult Index(string id)
    {
      //ViewData["TagCloudHandler"] = TagCloudManager.GetHandler();
      try { ViewData["tagValue"] = Utility.StringBase64ToString(id); }
      catch { }
      return View();
    }


    public virtual ActionResult TagsCloud()
    {
      //ViewData.Model = TagCloudManager.GetHandler();
      return PartialView();
    }


  }


}
