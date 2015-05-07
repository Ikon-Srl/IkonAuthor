using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using System.Web.Mvc.Ajax;
using System.Web.Security;
using System.IO;
using System.Net.Mail;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Data;
using System.Data.Common;
using System.IO.Compression;

using Ikon;
using Ikon.Config;
using Ikon.GD;



namespace Ikon.IKCMS
{

  //
  // semplice filtro per il blocco dei robots
  //
  public class RobotsDenyAttribute : ActionFilterAttribute
  {
    protected Regex rxBot { get; set; }

    public RobotsDenyAttribute()
    {
    }

    public RobotsDenyAttribute(string regExBot)
      : this()
    {
      rxBot = new Regex(regExBot, RegexOptions.IgnoreCase | RegexOptions.Singleline);
    }


    public override void OnActionExecuting(ActionExecutingContext filterContext)
    {
      if (Utility.CheckIfBOT())
      {
        filterContext.RequestContext.HttpContext.Response.End();
        //HttpContext.Current.ApplicationInstance.CompleteRequest();  // se la uso in questo contesto continua comunque il processing della action.
      }
      base.OnActionExecuting(filterContext);
    }

  }


}
