using System;
using System.Collections;
using System.Collections.Generic;
using System.Xml;
using System.Linq;
using System.Xml.Linq;
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
using Ikon.IKCMS.Library.Resources;


namespace IkonWeb.Controllers
{

  [AuthorizeOR()]
  //[AuthorizeAclCMS()]
  [ControllerSessionState(ControllerSessionState.ReadOnly)]
  //[SessionState(System.Web.SessionState.SessionStateBehavior.ReadOnly)]
  public class AdminController : AdminControllerBase
  {


  }

}
