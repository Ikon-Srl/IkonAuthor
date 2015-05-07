using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Web;
using System.Xml.Linq;
using System.Web.Mvc;
using System.Web.Mvc.Html;
using System.Reflection;
//using Microsoft.Web.Mvc;

using Ikon;
using Ikon.GD;
using Ikon.IKGD.Library.Resources;


namespace Ikon.IKCMS
{

  public static class UrlHelperExtension
  {


    /// <summary>
    /// generazione della url per per il ProxyVFS.axd da utilizzare nel codice .spark/.as?x
    /// attenzione che genera url con encoding per html attributes
    /// (per ottenere le url senza encoding usare IKCMS_RouteUrlManager.GetUrlProxyVFS)
    /// </summary>
    /// <param name="rNode"></param>
    /// <param name="sNode"></param>
    /// <param name="stream"></param>
    /// <returns></returns>
    public static string UrlProxyVFS(int? rNode, int? sNode, string stream) { return UrlProxyVFS(rNode, sNode, stream, null, false, null, true); }
    public static string UrlProxyVFS(int? rNode, int? sNode, string stream, bool fullUrl) { return UrlProxyVFS(rNode, sNode, stream, null, fullUrl, null, true); }
    public static string UrlProxyVFS(int? rNode, int? sNode, string stream, string defaultResourceUrl, bool fullUrl) { return UrlProxyVFS(rNode, sNode, stream, defaultResourceUrl, fullUrl, null, true); }
    public static string UrlProxyVFS(int? rNode, int? sNode, string stream, string defaultResourceUrl, bool fullUrl, int? cacheOnBrowser) { return UrlProxyVFS(rNode, sNode, stream, defaultResourceUrl, fullUrl, cacheOnBrowser, true); }
    public static string UrlProxyVFS(int? rNode, int? sNode, string stream, string defaultResourceUrl, bool fullUrl, int? cacheOnBrowser, bool encodeUrl)
    {
      string url = IKCMS_RouteUrlManager.GetUrlProxyVFS(rNode, sNode, stream, defaultResourceUrl, null, fullUrl, cacheOnBrowser);
      return encodeUrl ? url.EncodeAsAttribute() : url;
    }
    public static string UrlProxyVFS(IKCMS_ModelCMS_Interface model, string stream) { return UrlProxyVFS(model, stream, null, false, null, true); }
    public static string UrlProxyVFS(IKCMS_ModelCMS_Interface model, string stream, bool fullUrl) { return UrlProxyVFS(model, stream, null, fullUrl, null, true); }
    public static string UrlProxyVFS(IKCMS_ModelCMS_Interface model, string stream, string defaultResourceUrl) { return UrlProxyVFS(model, stream, defaultResourceUrl, false, null, true); }
    public static string UrlProxyVFS(IKCMS_ModelCMS_Interface model, string stream, string defaultResourceUrl, bool fullUrl) { return UrlProxyVFS(model, stream, defaultResourceUrl, fullUrl, null, true); }
    public static string UrlProxyVFS(IKCMS_ModelCMS_Interface model, string stream, string defaultResourceUrl, bool fullUrl, int? cacheOnBrowser) { return UrlProxyVFS(model, stream, defaultResourceUrl, fullUrl, cacheOnBrowser, true); }
    public static string UrlProxyVFS(IKCMS_ModelCMS_Interface model, string stream, string defaultResourceUrl, bool fullUrl, int? cacheOnBrowser, bool encodeUrl)
    {
      try { return UrlProxyVFS(model.rNode, null, stream, defaultResourceUrl, fullUrl, cacheOnBrowser, encodeUrl); }  // e' gia' stato codificato come attribute
      catch { return "javascript:;"; }
    }


    public static string GetMvcActionUrl<TController>(this UrlHelper helper, Expression<Action<TController>> action) where TController : Controller { return IKCMS_RouteUrlManager.GetMvcActionUrl<TController>(helper.RequestContext, action, false); }
    public static string GetMvcActionUrl<TController>(this UrlHelper helper, Expression<Action<TController>> action, bool fullUrl) where TController : Controller { return IKCMS_RouteUrlManager.GetMvcActionUrl<TController>(helper.RequestContext, action, fullUrl); }
    public static string GetUrlProxyVFS(this UrlHelper helper, int? rNode, int? sNode, string stream, string defaultResourceUrl, int? VersionFrozen, bool fullUrl, int? cacheOnBrowser) { return IKCMS_RouteUrlManager.GetUrlProxyVFS(rNode, sNode, stream, defaultResourceUrl, VersionFrozen, fullUrl, cacheOnBrowser, false); }


    // per l'encoding di stringhe da inserire come attributi nel codice html (es. src o href)
    public static string EncodeAsAttribute(this string str)
    {
      return Utility.HtmlAttributeEncode(str ?? string.Empty);
    }
    public static string EncodeAsAttributeSQ(this string str)
    {
      return Utility.HtmlAttributeEncode(EncodeAsJavaScript(str) ?? string.Empty);
      //return HttpUtility.HtmlAttributeEncode(str ?? string.Empty).QuotingSingle4JS();
      //return HttpUtility.HtmlAttributeEncode(str ?? string.Empty).Replace("'", @"\'");
    }
    public static string EncodeAsAttribute4JS(this string str)
    {
      return Utility.HtmlAttributeEncode(EncodeAsJavaScript(str) ?? string.Empty);
    }

    // per l'encoding di stringhe da inserire come attributi nel codice html (es. src o href)
    public static string EncodeAsAttributeUrl(this string url)
    {
      return EncodeAsAttributeUrl(url, "javascript:;");
    }

    public static string EncodeAsAttributeUrl(this string url, string defaultValue)
    {
      return Utility.HtmlAttributeEncode(url.NullIfEmpty() ?? defaultValue);
    }
    //public static string EncodeAsAttributeUrlSQ(this string url, string defaultValue)
    //{
    //  //return HttpUtility.HtmlAttributeEncode(url.NullIfEmpty() ?? defaultValue).Replace("'", @"\'");
    //  return HttpUtility.HtmlAttributeEncode(url.NullIfEmpty() ?? defaultValue).QuotingSingle4JS();
    //}

    public static string EncodeAsAttributeUrl(this Uri url)
    {
      return EncodeAsAttributeUrl(url, "javascript:;");
    }

    public static string EncodeAsAttributeUrl(this Uri url, string defaultValue)
    {
      return Utility.HtmlAttributeEncode((url != null) ? url.ToString() : defaultValue);
    }
    //public static string EncodeAsAttributeUrlSQ(this Uri url, string defaultValue)
    //{
    //  //return HttpUtility.HtmlAttributeEncode((url != null) ? url.ToString() : defaultValue).Replace("'", @"\'");
    //  return HttpUtility.HtmlAttributeEncode((url != null) ? url.ToString() : defaultValue).QuotingSingle4JS();
    //}

    public static string EncodeAsHtml(this string html)
    {
      return HttpUtility.HtmlEncode(html ?? string.Empty);
    }

    public static string EncodeAsJavaScript(this string str)
    {
      if (string.IsNullOrEmpty(str))
      {
        return str;
      }
      //
      // .NET4 da sostituire con Encoder.JavaScriptEncode
      return Utility.JavaScriptEncode(str, false);
      //return Microsoft.Security.Application.AntiXss.JavaScriptEncode(str, false);
    }

    //
    // normalizzazione dei virtual path analogamente a Page.ResolveUrl con gestione corretta delle querystring
    // per utilizzare il resolve nei controller utilizzare:
    // this.Url.Content("~/virtualUrl");
    // this.Url.Action("actionName", new { code = "Code01", params... });
    //
    public static string ResolveVirtualUrl(this Controller controller, string url)
    {
      return new UrlHelper(controller.ControllerContext.RequestContext).Content(url);
    }


    public static string ToFullUrl(string url)
    {
      return new Uri(HttpContext.Current.Request.Url, url).ToString();
    }


    public static string ContentFullUrl(this UrlHelper helper, string url)
    {
      return new Uri(HttpContext.Current.Request.Url, helper.Content(url)).ToString();
    }


    public static string WriteCanonicalUrl(this HtmlHelper helper)
    {
      string canonicalUrl = IKGD_SEO_Manager.ContextCanonicalUrl;
      if (helper.ViewData.Model is IKCMS_ModelCMS_Interface)
        canonicalUrl = canonicalUrl ?? (helper.ViewData.Model as IKCMS_ModelCMS_Interface).UrlCanonical;
      //canonicalUrl = canonicalUrl ?? helper.ViewContext.HttpContext.Request.Url.PathAndQuery;  // dovremmo generarla solo se serve a qualcosa
      if (!string.IsNullOrEmpty(canonicalUrl))
      {
        if (canonicalUrl.StartsWith("~/"))
          canonicalUrl = Utility.ResolveUrl(canonicalUrl);
        return string.Format("<link rel='canonical' href='{0}' />", Utility.HtmlAttributeEncode(new Uri(HttpContext.Current.Request.Url, canonicalUrl).ToString()));
      }
      return null;
    }




    public static string StyleSheetIncludeSet(this UrlHelper helper, IEnumerable<string> releaseCSS, IEnumerable<string> debugCSS)
    {
      try
      {
        IEnumerable<string> listCSS = HttpContext.Current.IsDebuggingEnabled ? debugCSS : releaseCSS;
        listCSS = listCSS ?? debugCSS ?? releaseCSS;
        StringBuilder sb = new StringBuilder();
        foreach (string url in listCSS)
        {
          try
          {
            string aurl = url.StartsWith("~/") ? helper.Content(url) : url;
            string block = string.Format("<link href='{0}' rel='stylesheet' type='text/css' />", aurl);
            var builder = new TagBuilder("link");
            builder.MergeAttribute("rel", "stylesheet");
            builder.MergeAttribute("type", "text/css");
            builder.MergeAttribute("href", aurl);
            sb.Append(builder.ToString(TagRenderMode.SelfClosing));
          }
          catch { }
        }
        return sb.ToString();
      }
      catch { }
      return string.Empty;
    }


    public static string StyleSheetInclude(this UrlHelper helper, string fileName) { return helper.StyleSheetInclude(fileName, null); }
    public static string StyleSheetInclude(this UrlHelper helper, string fileName, string media)
    {
      //<link rel="stylesheet" type="text/css" href="~/Content/css/fileName" media="screen" />
      var builder = new TagBuilder("link");
      builder.MergeAttribute("rel", "stylesheet");
      builder.MergeAttribute("type", "text/css");
      if (fileName.StartsWith("~/"))
        builder.MergeAttribute("href", helper.Content(fileName));
      else
        builder.MergeAttribute("href", helper.Content("~/Content/css/" + fileName));
      if (!string.IsNullOrEmpty(media))
        builder.MergeAttribute("media", media);
      return builder.ToString(TagRenderMode.SelfClosing);
    }


    public static string JavaScriptInclude(this UrlHelper helper, string fileName)
    {
      //<script type="text/javascript" src="~/Scripts/fileName"></script>
      var builder = new TagBuilder("script");
      builder.MergeAttribute("type", "text/javascript");
      if (fileName.StartsWith("~/"))
        builder.MergeAttribute("src", helper.AutoVersioning(fileName));
      else if (fileName.StartsWith("/"))
        builder.MergeAttribute("src", helper.AutoVersioning(fileName));
      else
        builder.MergeAttribute("src", helper.AutoVersioning("~/Scripts/" + fileName));
      return builder.ToString(TagRenderMode.Normal);
    }


    public static string AutoVersioning(this UrlHelper helper, string virtualPath)
    {
      try
      {
        if (HttpRuntime.Cache["AutoVersioningPath_" + virtualPath] == null)
        {
          var version = virtualPath;
          try
          {
            //var physicalPath = HttpContext.Current.Server.MapPath(virtualPath);
            //version = VirtualPathUtility.ToAbsolute(virtualPath) + "?v=" + new System.IO.FileInfo(physicalPath).LastWriteTime.ToString("yyyyMMddhhmmss");
            version = VirtualPathUtility.ToAbsolute(virtualPath) + "?v=" + EmbeddedResourcesVPP_Helper.GetResourceOrFileLastWriteTime(virtualPath);
          }
          catch { }
          HttpRuntime.Cache.Add("AutoVersioningPath_" + virtualPath, version, null, DateTime.Now.AddSeconds(3600), TimeSpan.Zero, System.Web.Caching.CacheItemPriority.Normal, null);
          return version;
        }
        else
        {
          return HttpRuntime.Cache["AutoVersioningPath_" + virtualPath] as string;
        }
      }
      catch { }
      return virtualPath;
    }


    public static string StaticImage(this UrlHelper helper, string fileName)
    {
      return helper.Content("~/Content/Images/{0}".FormatString(fileName));
    }


    public static string TransparentImage(this UrlHelper helper)
    {
      return StaticImage(helper, "trasparente.gif");
    }


    public static string CustomTagMainHtml()
    {
      StringBuilder sb = new StringBuilder();
      string lang = "it";
      try { lang = IKGD_Language_Provider.Provider.LanguageMeta; }
      catch { }
      sb.AppendLine("<!--[if lt IE 7]><html class='no-js lt-ie9 lt-ie8 lt-ie7' xml:lang='{0}' lang='{0}'><![endif]-->".FormatString(lang));
      sb.AppendLine("<!--[if IE 7]><html class='no-js lt-ie9 lt-ie8' xml:lang='{0}' lang='{0}'><![endif]-->".FormatString(lang));
      sb.AppendLine("<!--[if IE 8]><html class='no-js lt-ie9' xml:lang='{0}' lang='{0}'><![endif]-->".FormatString(lang));
      sb.AppendLine("<!--[if gt IE 8]><!--><html class='no-js' xml:lang='{0}' lang='{0}'><!--<![endif]-->".FormatString(lang));
      return sb.ToString();
    }

  }
}
