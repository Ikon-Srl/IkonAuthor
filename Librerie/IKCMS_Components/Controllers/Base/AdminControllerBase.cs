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
using Microsoft.Web.Mvc;
using Newtonsoft.Json;
using LinqKit;
using Autofac;

using Ikon;
using Ikon.Config;
using Ikon.GD;
using Ikon.IKCMS;
using IkonWeb.Controllers;


namespace Ikon.IKCMS
{

  [RobotsDeny()]
  [AuthorizeOR()]
  [ControllerSessionState(ControllerSessionState.ReadOnly)]
  //[SessionState(System.Web.SessionState.SessionStateBehavior.ReadOnly)]
  public abstract class AdminControllerBase : VFS_Access_Controller
  {

    public ActionResult Index()
    {
      ViewData["Language"] = IKGD_Language_Provider.Provider.LanguageNN;
      ViewData["VersionFrozen"] = Ikon.GD.FS_OperationsHelpers.VersionFrozenSession;
      ViewData["IsRoot"] = Ikon.GD.FS_OperationsHelpers.IsRoot;
      ViewData["CachedAreas"] = Utility.Implode(Ikon.GD.FS_OperationsHelpers.CachedAreasExtended.AreasAllowed, ", ");
      return View("~/Views/Admin/Index");
    }

  }
}
