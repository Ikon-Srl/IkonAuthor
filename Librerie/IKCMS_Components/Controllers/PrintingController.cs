using System;
using System.Collections;
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
using System.Web.Mvc;
using System.Web.Mvc.Ajax;
using LinqKit;

using Ikon;
using Ikon.Support;
using Ikon.GD;
using Ikon.IKCMS;
using Ikon.IKGD.Library.Resources;


namespace IkonWeb.Controllers
{


  [RobotsDeny()]
  public class PrintingController : VFS_Access_Controller
  {


    public ActionResult RenderAsPDF(string url, int? render_delay)
    {
      return null;
    }


    public static byte[] RenderAsPDF_Worker(string url, int? render_delay, int? screenWidthPx, bool? dump_html)
    {
      return null;
    }


    public static byte[] RenderAsPDF_Helper(string html, string baseUrl, int? screenWidthPx, int? render_delay)
    {
      return null;
    }


  }


}
