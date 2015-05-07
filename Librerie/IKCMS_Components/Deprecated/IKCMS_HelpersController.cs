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
  public class IKCMS_HelpersController : VFS_Access_Controller
  {
    //
    // Action Handlers
    //


    //
    //handler per la gestione delle funzionalita' ajax dei calendari
    //
    public ActionResult GetEventsForCalendars(int? sNodeBrowserWidget, int? year, int? month)
    {
      return Ikon.IKCMS.Helpers.CalendarHelpers.GetEventsForCalendars(this.ControllerContext, sNodeBrowserWidget, year, month);
    }


  }
}
