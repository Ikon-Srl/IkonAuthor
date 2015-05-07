using System;
using System.Text;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using System.IO;
using Newtonsoft.Json;


namespace Ikon.IKCMS
{


  /// <summary>
  /// Renders result as JSON and also wraps the JSON in a call
  /// to the callback function specified in "JsonpResult.Callback".
  /// http://blogorama.nerdworks.in/entry-EnablingJSONPcallsonASPNETMVC.aspx
  /// </summary>
  public class JsonpResult : JsonResult
  {
    /// <summary>
    /// Gets or sets the javascript callback function that is
    /// to be invoked in the resulting script output.
    /// </summary>
    /// <value>The callback function name.</value>
    public string Callback { get; set; }


    /// <summary>
    /// Enables processing of the result of an action method by a
    /// custom type that inherits from <see cref="T:System.Web.Mvc.ActionResult"/>.
    /// </summary>
    /// <param name="context">The context within which the
    /// result is executed.</param>
    public override void ExecuteResult(ControllerContext context)
    {
      if (context == null)
      {
        throw new ArgumentNullException("context");
      }
      //
      HttpResponseBase response = context.HttpContext.Response;
      if (!String.IsNullOrEmpty(ContentType))
      {
        response.ContentType = ContentType;
      }
      else
      {
        response.ContentType = "application/javascript";
      }
      if (ContentEncoding != null)
      {
        response.ContentEncoding = ContentEncoding;
      }
      if (Callback == null || Callback.Length == 0)
      {
        Callback = context.HttpContext.Request.QueryString["callback"];
      }
      if (Data != null)
      {
        response.Write(Callback);
        response.Write("(");
        response.Write(Ikon.GD.IKGD_Serialization.SerializeToJSON(Data));
        response.Write(");");
        /*
        // The JavaScriptSerializer type was marked as obsolete
        // prior to .NET Framework 3.5 SP1 
        #pragma warning disable 0618
        JavaScriptSerializer serializer = new JavaScriptSerializer();
        string ser = serializer.Serialize(Data);
        response.Write(Callback + "(" + ser + ");");
        #pragma warning restore 0618
        */
      }
    }


    public static JsonpResult Factory(object data)
    {
      JsonpResult result = new JsonpResult();
      result.Data = data;
      result.JsonRequestBehavior = JsonRequestBehavior.AllowGet;
      return result;
    }


  }


}
