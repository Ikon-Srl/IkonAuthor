using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Linq;
using System.Data.Common;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using System.Net.Mail;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Transactions;
using System.Web;
using System.Web.Mvc;
using System.Web.Mvc.Ajax;
using System.Web.Security;
using System.Reflection;
using Newtonsoft.Json;
using LinqKit;
using Autofac;

using Ikon;
using Ikon.Auth.Login;
using Ikon.GD;
using Ikon.IKCMS;
using Ikon.IKGD.Library.Resources;
using Ikon.IKCMS.Library.Resources;



namespace Custom.Controllers
{
  using SampleSiteWeb;
  using System.Linq.Expressions;
  using Newtonsoft.Json.Linq;


  //[CacheFilter(86400)]
  public class DebugController : VFS_Access_Controller
  {

    public ActionResult Debug()
    {

      //return View("~/Views/Debug/Debug");
      return null;
    }


  }
}
