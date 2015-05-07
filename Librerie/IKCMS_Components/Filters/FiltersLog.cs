using System;
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
using Autofac;

using Ikon;
using Ikon.GD;
using Ikon.Config;
using Ikon.Auth.Login;
using Ikon.IKCMS;


namespace IkonWeb.Controllers
{

  //
  // inizializza e rilascia la connessione al DB di LazyLogin
  // popola userName se non settato
  // se anonymous -> userName == null
  // inizializza il parametro lazyLoginMapper per utenti non anonimi
  //
  public class HitLogFilterAttribute : LazyLoginFilterAttribute
  {
    public int rNode { get; set; }
    public int? ActionCode { get; set; }
    public int? ActionSubCode { get; set; }


    public HitLogFilterAttribute(int rNode, int? ActionCode, int? ActionSubCode)
      : base()
    {
      this.rNode = rNode;
      this.ActionCode = ActionCode;
      this.ActionSubCode = ActionSubCode;
    }


    public override void OnActionExecuting(ActionExecutingContext filterContext)
    {
      base.OnActionExecuting(filterContext);
      //
      IKCMS_HitLogger.ProcessHitLL(this.rNode, this.ActionCode.GetValueOrDefault(0), this.ActionSubCode.GetValueOrDefault(0));
      //
    }

  }


}
