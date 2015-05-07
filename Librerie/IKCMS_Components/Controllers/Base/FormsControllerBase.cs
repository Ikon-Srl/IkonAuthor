using System;
using System.Collections.Generic;
using System.Data;
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
using Autofac;
using LinqKit;

using Ikon;
using Ikon.Auth.Login;
using Ikon.GD;
using Ikon.IKCMS;
using Ikon.IKGD.Library.Resources;
using Ikon.IKCMS.Library.Resources;


namespace Ikon.IKCMS
{

  [HandleError]
  //[Microsoft.Web.Mvc.ControllerSessionState(Microsoft.Web.Mvc.ControllerSessionState.Required)]
  public abstract class FormsControllerBase : AutoStaticCMS_Controller
  {


    //protected override void OnActionExecuted(ActionExecutedContext filterContext)
    //{
    //  base.OnActionExecuted(filterContext);
    //  try { Response.AppendToLog("ip-src-" + Ikon.Utility.GetRequestAddressExt(null)); }
    //  catch { }
    //}

    //
    // TODO:
    // completare la virtualizzazione e la customizzabilita' dei form
    // creare degli helper ben configurabili per la generazione di mail
    //

    public virtual ActionResult Contatto()
    {
      //ViewData.Model = IKCMS_ModelCMS_Provider.Provider.ModelBuildFromContext();
      ViewData.Model = GetDefaultModel();
      return View();
    }


    [AcceptVerbs(HttpVerbs.Post)]
    [ValidateInput(false)]
    public virtual ActionResult Contatto(FormCollection collection)
    {
      List<string> messages = new List<string>();
      try
      {
        if (collection == null || collection.Count == 0)
          throw new Exception("Form non compilato.");
        if (!Utility.TryParse<bool>(collection["privacy"], false))
          messages.Add("E' necessario fornire il consenso al trattamento dei dati personali per poter inviare la richiesta.");

        Dictionary<string, string> fieldsList = new Dictionary<string, string> {
           {"*Input_Nome", "Nome"},
           {"*Input_Cognome", "Cognome"},
           {"*Input_Indirizzo", "Indirizzo"},
           {"*Input_NumeroCivico", "Numero Civico"},
           {"*Input_CAP", "CAP"},
           {"*Input_Citta", "Citta"},
           {"*Input_Provincia", "Provincia"},
           {"*Input_Nazione", "Nazione"},
           {"Input_Telefono", "Telefono"},
           {"Input_Fax", "Fax"},
           {"*Input_Email", "Email"},
           {"Input_Testo", "Testo"},
        };
        foreach (KeyValuePair<string, string> kv in fieldsList)
        {
          string fld = kv.Key.Trim(' ', '*');
          if (kv.Key.StartsWith("*") && string.IsNullOrEmpty(collection[fld]))
            messages.Add("Il campo {0} è obbligatorio.".FormatString(kv.Value));
        }
        //
        // TODO:
        // validazione email e date
        //
        if (!Regex.IsMatch(collection["Input_Email"].TrimSafe(), @"^[_a-zA-Z0-9-]+(\.[_a-zA-Z0-9-]+)*@[a-zA-Z0-9-]+(\.[a-zA-Z0-9-]+)*\.(([0-9]{1,3})|([a-zA-Z]{2,3})|(aero|coop|info|museum|name))$", RegexOptions.IgnoreCase))
          messages.Add("L'E-mail inserita non è un indirizzo di E-mail valido.");
        //
        if (!messages.Any())
        {
          XElement xTemplate = Utility.FileReadXmlVirtual("~/Content/MailTemplates/MailContatto.xml");
          string body = xTemplate.ElementValueNN("body");
          //
          MailMessage message = new MailMessage();
          message.From = new MailAddress(collection["Input_Email"].TrimSafe());
          message.Subject = xTemplate.ElementValueNN("subject");
          message.IsBodyHtml = true;
          //
          xTemplate.Elements("to").ForEach(x => message.To.Add(new MailAddress(x.AttributeValue("address"), x.AttributeValue("text", x.Value.NullIfEmpty() ?? x.AttributeValue("address")))));
          xTemplate.Elements("cc").ForEach(x => message.CC.Add(new MailAddress(x.AttributeValue("address"), x.AttributeValue("text", x.Value.NullIfEmpty() ?? x.AttributeValue("address")))));
          xTemplate.Elements("bcc").ForEach(x => message.Bcc.Add(new MailAddress(x.AttributeValue("address"), x.AttributeValue("text", x.Value.NullIfEmpty() ?? x.AttributeValue("address")))));
          //
          if (!string.IsNullOrEmpty(IKGD_Config.AppSettingsWeb["SendMailDebug"]))
          {
            message.Bcc.Clear();
            message.CC.Clear();
            message.To.Clear();
            message.To.Add(new MailAddress(IKGD_Config.AppSettings["SendMailDebug"]));
          }
          //
          foreach (KeyValuePair<string, string> kv in fieldsList)
          {
            string fld = kv.Key.Trim(' ', '*');
            body = body.Replace("{{${0}}}".FormatString(fld), HttpUtility.HtmlEncode(collection[fld]));
          }
          string baseUrl = new Uri(System.Web.HttpContext.Current.Request.Url, VirtualPathUtility.ToAbsolute("~/")).ToString();
          body = body.Replace("{$baseUrl}", baseUrl);
                      
          message.Body = body;
          //
          SmtpClient client = new SmtpClient();
          client.Send(message);
        }
        //
        if (messages.Any())
          throw new Exception("<ul class='errorPopup'>" + Utility.Implode(messages.Select(m => "<li>{0}</li>".FormatString(HttpUtility.HtmlEncode(m))), "\n") + "</ul>");
        //
        return Json(new { hasError = false, successUrl = collection["successUrl"], message = "Form inviato correttamente." });
      }
      catch (Exception ex)
      {
        return Json(new { hasError = true, message = ex.Message });
      }
    }



  }
}
