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

  [Microsoft.Web.Mvc.ControllerSessionState(Microsoft.Web.Mvc.ControllerSessionState.ReadOnly)]  // necessaria per il tracking del session hash negli hitlog
  public class LazyLoginController : VFS_Access_Controller
  {

    protected override void OnActionExecuting(ActionExecutingContext filterContext)
    {
      if (string.Equals(filterContext.ActionDescriptor.ActionName, "HitLog", StringComparison.OrdinalIgnoreCase))
      {
        this.AutoDetectDeviceEnabledFlag = false;
      }
      base.OnActionExecuting(filterContext);
    }


    public ActionResult AjaxRegisterVote(int rNode, int? Category, int? Vote, string Text) { return AjaxRegisterVoteExt(rNode, Category, Vote, Text, true); }
    public ActionResult AjaxRegisterVoteNoMessage(int rNode, int? Category, int? Vote, string Text) { return AjaxRegisterVoteExt(rNode, Category, Vote, Text, false); }
    public ActionResult AjaxRegisterVoteExt(int rNode, int? Category, int? Vote, string Text, bool showMessage) { return AjaxRegisterVoteExt2(rNode, Category, Vote, Text, null, showMessage); }
    public ActionResult AjaxRegisterVoteExt2(int rNode, int? Category, int? Vote, string Text, string successUrl, bool showMessage)
    {
      try
      {
        bool res = LazyLoginHelperExtensions.RegisterVote(rNode, Category, Vote, Text);
        if (res)
        {
          string message = "Dati salvati.";
          if (Vote != null && Text != null)
            message = "Dati salvati.";
          else if (Vote != null)
            message = "Votazione salvata.";
          else if (Text != null)
            message = "Testo salvato.";
          if (showMessage == false)
            message = null;
          if (Request.IsAjaxRequest())
            return Json(new { hasError = false, message = message, successUrl = successUrl }, JsonRequestBehavior.AllowGet);
        }
        else
          throw new Exception("Dati non salvati.");
      }
      catch (Exception ex)
      {
        string message = ex.Message;
        if (showMessage == false)
          message = null;
        successUrl = null;
        if (Request.IsAjaxRequest())
          return Json(new { hasError = true, message = message }, JsonRequestBehavior.AllowGet);
      }
      if (!string.IsNullOrEmpty(successUrl))
        return Redirect(successUrl);
      else if (Request.UrlReferrer != null)
        return Redirect(Request.UrlReferrer.ToString());
      return null;
    }

    public ActionResult AjaxRegisterVoteExt3(int? rNode, int? Category, int? Vote, string Message, bool? showMessage)
    {
      if (rNode == null)
        return null;

      string successUrl = "";
      showMessage = showMessage ?? true;
      Vote = Vote ?? 0;

      try
      {
        bool res = LazyLoginHelperExtensions.RegisterVote(rNode.Value, Category, Vote, null);
        if (res)
        {
          string message = Message ?? "Dati salvati.";
          if (showMessage == false)
            message = null;
          if (Request.IsAjaxRequest())
            return Json(new { hasError = false, message = message, successUrl = successUrl }, JsonRequestBehavior.AllowGet);
        }
        else
          throw new Exception("Dati non salvati.");
      }
      catch (Exception ex)
      {
        string message = ex.Message;
        if (showMessage == false)
          message = null;
        successUrl = null;
        if (Request.IsAjaxRequest())
          return Json(new { hasError = true, message = message }, JsonRequestBehavior.AllowGet);
      }
      if (!string.IsNullOrEmpty(successUrl))
        return Redirect(successUrl);
      else if (Request.UrlReferrer != null)
        return Redirect(Request.UrlReferrer.ToString());
      return null;
    }


    public ActionResult HitLog(int? ResourceCode, int? ActionCode, int? ActionSubCode)
    {
      if (ResourceCode != null)
      {
        IKCMS_HitLogger.ProcessHitLL(ResourceCode.Value, ActionCode, ActionSubCode);
      }
      return Json(new { ResourceCode = ResourceCode }, JsonRequestBehavior.AllowGet);
    }



    public ActionResult RedirectForced(string url)
    {
      Response.ClearHeaders();
      Response.ClearContent();
      Response.StatusCode = 302;
      Response.RedirectLocation = url;
      //Response.RedirectLocation = IKCMS_RouteUrlManager.GetMvcActionUrl<LazyLoginController>(c => c.DomainRedirector(ReturnUrl));
      Response.Flush();
      Response.End();
      return null;
      //return Redirect(IKCMS_RouteUrlManager.GetMvcActionUrl<LazyLoginController>(c => c.DomainRedirector(ReturnUrl)));
    }


  }
}
