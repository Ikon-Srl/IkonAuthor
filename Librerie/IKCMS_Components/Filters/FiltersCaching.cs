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
  // semplice filtro per la gestione dei caching headers
  //
  public class CacheFilterAttribute : ActionFilterAttribute
  {
    /// <summary>
    /// Gets or sets the cache duration in seconds. The default is null.
    /// Duration == 0 --> no header manipulation
    /// Duration > 0 --> set caching to Duration seconds
    /// Duration < 0 --> disable caching headers
    /// </summary>
    /// <value>The cache duration in seconds.</value>
    public int Duration { get; set; }


    public CacheFilterAttribute(int duration)
    {
      this.Duration = duration;
    }


    public CacheFilterAttribute(string configEntryName)
    {
      this.Duration = Utility.TryParse<int>(IKGD_Config.AppSettings[configEntryName], -1);
    }


    public override void OnActionExecuted(ActionExecutedContext filterContext)
    {
      base.OnActionExecuted(filterContext);
      //
      if (Utility.TryParse<bool>(IKGD_Config.AppSettings["CacheFilterAttribute"], true) == false)
        return;
      //
      if (Duration > 0)
      {
        HttpCachePolicyBase cache = filterContext.HttpContext.Response.Cache;
        TimeSpan cacheDuration = TimeSpan.FromSeconds(Duration);
        cache.SetCacheability(HttpCacheability.Public);
        cache.SetValidUntilExpires(true);
        cache.SetExpires(DateTime.Now.Add(cacheDuration));
        cache.SetMaxAge(cacheDuration);
        cache.AppendCacheExtension("must-revalidate, proxy-revalidate");
      }
      else if (Duration < 0)
      {
        HttpCachePolicyBase cache = filterContext.HttpContext.Response.Cache;
        cache.SetNoServerCaching();
        cache.SetNoStore();
        cache.SetMaxAge(TimeSpan.Zero);
        cache.AppendCacheExtension("must-revalidate, proxy-revalidate");
        cache.SetExpires(DateTime.Now.AddYears(-1));
      }
    }

  }


  public class CacheFilterForcedAttribute : ActionFilterAttribute
  {
    /// <summary>
    /// Gets or sets the cache duration in seconds. The default is null.
    /// Duration == 0 --> no header manipulation
    /// Duration > 0 --> set caching to Duration seconds
    /// Duration < 0 --> disable caching headers
    /// </summary>
    /// <value>The cache duration in seconds.</value>
    public int Duration { get; set; }


    public CacheFilterForcedAttribute(int duration)
    {
      this.Duration = duration;
    }


    public CacheFilterForcedAttribute(string configEntryName)
    {
      this.Duration = Utility.TryParse<int>(IKGD_Config.AppSettings[configEntryName], -1);
    }


    public override void OnActionExecuted(ActionExecutedContext filterContext)
    {
      base.OnActionExecuted(filterContext);
      //
      if (Duration > 0)
      {
        HttpCachePolicyBase cache = filterContext.HttpContext.Response.Cache;
        TimeSpan cacheDuration = TimeSpan.FromSeconds(Duration);
        cache.SetCacheability(HttpCacheability.Public);
        cache.SetValidUntilExpires(true);
        cache.SetExpires(DateTime.Now.Add(cacheDuration));
        cache.SetMaxAge(cacheDuration);
        cache.AppendCacheExtension("must-revalidate, proxy-revalidate");
      }
      else if (Duration < 0)
      {
        HttpCachePolicyBase cache = filterContext.HttpContext.Response.Cache;
        cache.SetNoServerCaching();
        cache.SetNoStore();
        cache.SetMaxAge(TimeSpan.Zero);
        cache.AppendCacheExtension("must-revalidate, proxy-revalidate");
        cache.SetExpires(DateTime.Now.AddYears(-1));
      }
    }

  }




  //
  // provvedere ad un attributo custom con settings gestiti da web.config
  // con management delle dipendenze sql, duration, ecc.
  //
  public class OutputCacheIKCMSAttribute : OutputCacheAttribute
  {

    public OutputCacheIKCMSAttribute()
      : base()
    {
    }

  }


  //
  // semplice filtro per la compressione dell'output
  //
  public class CompressFilter : ActionFilterAttribute
  {
    public override void OnActionExecuting(ActionExecutingContext filterContext)
    {
      base.OnActionExecuting(filterContext);
      //
      HttpRequestBase request = filterContext.HttpContext.Request;
      string acceptEncoding = request.Headers["Accept-Encoding"];
      if (string.IsNullOrEmpty(acceptEncoding))
        return;
      //
      acceptEncoding = acceptEncoding.ToUpperInvariant();
      HttpResponseBase response = filterContext.HttpContext.Response;
      //
      if (acceptEncoding.Contains("GZIP"))
      {
        response.AppendHeader("Content-encoding", "gzip");
        response.Filter = new GZipStream(response.Filter, CompressionMode.Compress);
      }
      else if (acceptEncoding.Contains("DEFLATE"))
      {
        response.AppendHeader("Content-encoding", "deflate");
        response.Filter = new DeflateStream(response.Filter, CompressionMode.Compress);
      }
    }
  }


}
