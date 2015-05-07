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
  using Winnovative.WnvHtmlConvert;


  [RobotsDeny()]
  public class PrintingController : VFS_Access_Controller
  {


    //
    // stampa pagina come PDF
    //
    public ActionResult RenderAsPDF(string url, int? render_delay)
    {
      byte[] pdfBytes = null;
      try
      {
      }
      catch { }
      return null;
    }


    //
    // stampa pagina come PDF
    //
    public static byte[] RenderAsPDF_Worker(string url, int? render_delay, int? screenWidthPx, bool? dump_html)
    {
      if (string.IsNullOrEmpty(url) || Utility.CheckIfBOT())
        return null;
      try
      {
      }
      //catch (Exception ex)
      //{
      //  Elmah.ErrorSignal.FromCurrentContext().Raise(ex);
      //}
      catch { }
      return null;
    }


    //
    // stampa pagina come PDF
    // per abilitare la modalita' IE9 invece di IE7
    // http://stackoverflow.com/questions/10984599/browser-emulation-using-winnovative-html-to-pdf
    // regkeys:
    // DWORD value, Name=WebDev.WebServer.exe, Value=9000 (decimal)
    // DWORD value, Name=w3wp.exe, Value=9000 (decimal)
    // HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Internet Explorer\MAIN\FeatureControl\FEATURE_BROWSER_EMULATION
    // HKEY_LOCAL_MACHINE\SOFTWARE\Wow6432Node\Microsoft\Internet Explorer\MAIN\FeatureControl\FEATURE_BROWSER_EMULATION
    //
    public static byte[] RenderAsPDF_Helper(string html, string baseUrl, int? screenWidthPx, int? render_delay)
    {
      byte[] pdfBytes = null;
      try
      {

      }
      //catch (Exception ex)
      //{
      //  Elmah.ErrorSignal.FromCurrentContext().Raise(ex);
      //}
      catch { }
      return pdfBytes;
    }


  }


}
