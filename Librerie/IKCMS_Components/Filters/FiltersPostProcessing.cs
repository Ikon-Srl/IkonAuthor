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

  public class ViewPostProcessAttribute : FilterAttribute, IResultFilter
  {
    public HttpContext existingContext { get; protected set; }
    public StringWriter writer { get; protected set; }


    public void OnResultExecuting(ResultExecutingContext filterContext)
    {
      // Replace the current context with a new context that writes to a string writer
      existingContext = System.Web.HttpContext.Current;
      writer = new StringWriter();
      HttpResponse response = new HttpResponse(writer);
      HttpContext context = new HttpContext(existingContext.Request, response) { User = existingContext.User };
      // Copy all items in the context (especially done for session availability in the component)
      foreach (var key in existingContext.Items.Keys)
      {
        context.Items[key] = existingContext.Items[key];
      }
      System.Web.HttpContext.Current = context;
    }

    public void OnResultExecuted(ResultExecutedContext filterContext)
    {
      //ViewResult viewResult = filterContext.Result as ViewResult;
      //filterContext.HttpContext.Response.Filter = 

      // Restore the old context
      System.Web.HttpContext.Current = existingContext;
      // Return rendererd data
      existingContext.Response.Write(writer.ToString());

    }

  }






  [AttributeUsage(AttributeTargets.Class, Inherited = true, AllowMultiple = false)]
  public class WhitespaceStripAttribute : ActionFilterAttribute
  {
    public override void OnActionExecuted(
        ActionExecutedContext ActionExecutedContext)
    {
      ActionExecutedContext.HttpContext.Response.Filter = new WhitespaceStream(ActionExecutedContext.HttpContext);
    }
  }


  internal class WhitespaceStream : MemoryStream
  {
    private readonly HttpContextBase HttpContext = null;
    private readonly Stream FilterStream = null;

    private readonly string[] ContentTypes = new string[1] { "text/html" };

    private static Regex WhitespaceRegex = new Regex("(<pre>[^<>]*(((?<Open><)[^<>]*)+((?<Close-Open>>)[^<>]*)+)*(?(Open)(?!))</pre>)|\\s\\s+|[\\t\\n\\r]", RegexOptions.Singleline | RegexOptions.Compiled);
    private static Regex CommentsRegex = new Regex("<!--.*?-->", RegexOptions.Singleline | RegexOptions.Compiled);

    public WhitespaceStream(HttpContextBase HttpContext)
    {
      this.HttpContext = HttpContext;
      this.FilterStream = HttpContext.Response.Filter;
    }

    public override void Write(byte[] Buffer, int Offset, int Count)
    {
      string Source = Encoding.UTF8.GetString(Buffer);

      if (this.ContentTypes.Any(ct => (ct == this.HttpContext.Response.ContentType)))
      {
        this.HttpContext.Response.ContentEncoding = Encoding.UTF8;
        Source = WhitespaceRegex.Replace(Source, "$1");
        Source = CommentsRegex.Replace(Source, string.Empty);
      };
      this.FilterStream.Write(Encoding.UTF8.GetBytes(Source), Offset, Encoding.UTF8.GetByteCount(Source));
    }
  }




}
