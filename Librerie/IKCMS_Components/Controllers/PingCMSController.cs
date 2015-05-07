using System;
using System.Collections;
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
using Newtonsoft.Json;
using LinqKit;

using Ikon;
using Ikon.Config;
using Ikon.GD;
using Ikon.IKCMS;


namespace IkonWeb.Controllers
{

  [Microsoft.Web.Mvc.ControllerSessionState(Microsoft.Web.Mvc.ControllerSessionState.ReadOnly)]
  public class PingCMSController : Controller
  {

    public ActionResult Index()
    {
      StringBuilder sb = new StringBuilder();
      try
      {
        sb.AppendLine("Ikon Author");
        sb.AppendLine();
        sb.AppendLine("Ping Utility");
        sb.AppendLine();
        sb.AppendLine("Ikon Author version: {0}".FormatString(Ikon.IKCMS.Library.IkonAuthor_Constants.Version));
        sb.AppendLine("IKCMS version: {0}".FormatString(Ikon.IKCMS.Library.IKCMS_Constants.Version));
        sb.AppendLine("IKGD VFS version: {0}".FormatString(Ikon.IKGD.Library.IKGD_Constants.Version));
        sb.AppendLine("Server name: {0}".FormatString(System.Environment.GetEnvironmentVariable("COMPUTERNAME")));
        sb.AppendLine("Client IP: {0}".FormatString(Utility.GetRequestAddressExt(null)));
        sb.AppendLine("Language: {0}".FormatString(IKGD_Language_Provider.Provider.Language));
        sb.AppendLine("VersionFrozen: {0}".FormatString(Ikon.GD.FS_OperationsHelpers.VersionFrozenSession));
        sb.AppendLine();
      }
      catch { }
      return Content(sb.ToString(), "text/plain", Encoding.UTF8);
    }

  }
}
