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
using System.Text.RegularExpressions;
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

  [CacheFilter(180 * 86400)]
  [Microsoft.Web.Mvc.ControllerSessionState(Microsoft.Web.Mvc.ControllerSessionState.ReadOnly)]  // ci serve la sessione per gestire la data forzata per la sessione
  public class ProxyVFSController : Controller
  {


    //
    // attenzione che non funziona se viene aggiunto il path della richiesta contenente un estensione
    // inoltre sembra che ci sia un overhead non trascurabile nel processing di tutta la pipeline MVC
    // per cui conviene continuare ad usare il solito approccio con ProxyVFS.axd
    //
    public void Stream(string dataOrPath)
    {
      try
      {
        string[] frags = (dataOrPath ?? string.Empty).Split("/".ToCharArray(), 3);
        string node = frags.FirstOrDefault().TrimSafe(' ');
        string stream = frags.Skip(1).FirstOrDefault().TrimSafe(' ');
        string indexPath = frags.Skip(2).FirstOrDefault();
        var args = new Ikon.Handlers.ProxyVFS2_Helper.NodeVFS_Args();
        //
        if (Regex.IsMatch(node, @"^(r|s){0,1}\d+$", RegexOptions.Singleline | RegexOptions.IgnoreCase))
        {
          if (node.StartsWith("r", StringComparison.OrdinalIgnoreCase))
          {
            args.rNode = Utility.TryParse<int?>(node.Substring(1));
          }
          if (node.StartsWith("s", StringComparison.OrdinalIgnoreCase))
          {
            args.sNode = Utility.TryParse<int?>(node.Substring(1));
          }
          else
          {
            args.sNode = Utility.TryParse<int?>(node);
          }
          args.SourceKey = stream;
        }
        else
        {
          args.pathInfo = dataOrPath;
        }
        if (args.SourceKey == null && Request["stream"] != null)
        {
          args.SourceKey = Request["stream"];
        }
        if (Request["default"].IsNotEmpty())
        {
          string defaultResource = Request["default"].TrimSafe(' ', '/', '~');
          args.defaultResource = "~/" + defaultResource;
        }
        args.cacheDurationServer = Utility.TryParse<int?>(Request["cacheServer"]);
        args.cacheDurationBrowser = Utility.TryParse<int?>(Request["cacheBrowser"]);
        args.VersionFrozen = Utility.TryParse<int?>(Request["freeze"]);
        args.forceDownload = Utility.TryParse<bool?>(Request["forceDownload"]);
        //
        bool result = Ikon.Handlers.ProxyVFS2_Helper.ProcessStreamRequest(System.Web.HttpContext.Current, args);
        //
      }
      catch { }
    }


    public void Load(int? rNode, int? sNode, int? iNode, int? freeze, string relationType, string stream, string contentType, int? cacheDurationServer, int? cacheDurationBrowser, string defaultResource)
    {
      Ikon.Handlers.ProxyVFS_Helper.ProxyVFS_Request(System.Web.HttpContext.Current,
        null, rNode, sNode, iNode, freeze,
        relationType, stream, contentType,
        cacheDurationServer, cacheDurationBrowser, false,
        defaultResource);
    }


    public void LoadImage(int? sNode, int? freeze, string stream, int? cacheDurationServer, int? cacheDurationBrowser, string defaultResource)
    {
      defaultResource = defaultResource ?? VirtualPathUtility.ToAbsolute("~/Content/images/trasparente.gif");
      cacheDurationBrowser = cacheDurationBrowser ?? 86400;
      //
      Ikon.Handlers.ProxyVFS_Helper.ProxyVFS_Request(System.Web.HttpContext.Current,
        null, null, sNode, null, freeze,
        null, stream, null,
        cacheDurationServer, cacheDurationBrowser, false,
        defaultResource);
    }


    public void StreamIKCAT(int? attributeId, string stream, int? cacheDurationBrowser, string defaultResource)
    {
      try
      {
        if (attributeId != null)
        {
          defaultResource = defaultResource ?? VirtualPathUtility.ToAbsolute("~/Content/images/trasparente.gif");
          cacheDurationBrowser = cacheDurationBrowser ?? 86400 * 180;
          Ikon.Handlers.ProxyIKCAT_Helper.ProxyIKCAT_Request(System.Web.HttpContext.Current, attributeId.Value, stream, cacheDurationBrowser, defaultResource);
        }
      }
      catch { }
    }


  }
}
