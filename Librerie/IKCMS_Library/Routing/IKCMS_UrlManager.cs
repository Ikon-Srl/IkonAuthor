/*
 * 
 * Intranet Ikon
 * 
 * Copyright (C) 2010 Ikon Srl
 * Tutti i Diritti Riservati. All Rights Reserved
 * 
 */


using System;
using System.Data;
using System.Configuration;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Xml.Linq;
using System.Data.Linq;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Security;
using System.Linq.Expressions;
using System.Reflection;
using System.Data.Linq.Mapping;
using System.Diagnostics;
using System.Transactions;
using System.Web.Caching;
using System.Web.Mvc;
using System.Web.Mvc.Html;
using System.Web.Routing;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;  // assembly System.ServiceModel.Web
using Microsoft.Web;
using LinqKit;

using Ikon;
using Ikon.GD;


namespace Ikon.IKCMS
{


  public static class IKCMS_RouteUrlManager
  {
    public static bool DontUseQueryString { get; private set; }
    public static Regex StaticResourcePathRegEx { get; private set; }
    public static Regex ProxyPathRegEx { get; private set; }
    public static Regex NoCultureSetupPathRegEx { get; private set; }


    //
    // attenzione:
    // per la configurazione del formato d generazione delle url utilizzare
    // <add key="UrlGeneratorFormat" value="language_sNode_lastPathFragment" />
    //

    static IKCMS_RouteUrlManager()
    {
      DontUseQueryString = Utility.TryParse<bool>(IKGD_Config.AppSettings["ProxyVFS_DontUseQueryString"], false);
      StaticResourcePathRegEx = new Regex(@"\.(gif|jpg|jpeg|png|swf|js|css|xml|ico|ttf|eot|otf|woff|htc|fla|pdf|doc|bmp)$", RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.Singleline);
      ProxyPathRegEx = new Regex(@"/ProxyVFS", RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.Singleline);
      //
      if (IKGD_Config.AppSettings["NoCultureSetupPathRegEx"].IsNotNullOrWhiteSpace())
      {
        try { NoCultureSetupPathRegEx = new Regex(IKGD_Config.AppSettings["NoCultureSetupPathRegEx"], RegexOptions.Compiled | RegexOptions.Singleline | RegexOptions.IgnoreCase); }
        catch { }
      }
      //
    }


    public static bool IsCultureSetupAllowed(string urlPath)
    {
      if (urlPath.IsNullOrEmpty())
        urlPath = HttpContext.Current.Request.Path;
      try
      {
        if (StaticResourcePathRegEx.IsMatch(urlPath))
          return false;
        if (ProxyPathRegEx.IsMatch(urlPath))
          return false;
        if (NoCultureSetupPathRegEx != null && NoCultureSetupPathRegEx.IsMatch(urlPath))
          return false;
      }
      catch { }
      return true;
    }


    //
    // in alternativa e' possibile utilizzare
    // IKCMS_ModelCMS.GetUrlCanonical(rNode, sNode, sNodeRoot, Name) che crea una Url piu' SEO friendly
    //
    public static string GetMvcUrlGeneral(int? sNodeItem) { return GetMvcUrlGeneral(null, sNodeItem, null, true, false); }
    public static string GetMvcUrlGeneral(int? sNodeModule, int? sNodeItem) { return GetMvcUrlGeneral(sNodeModule, sNodeItem, null, true, false); }
    public static string GetMvcUrlGeneral(int? sNodeModule, int? sNodeItem, string subPath) { return GetMvcUrlGeneral(sNodeModule, sNodeItem, subPath, true, false); }
    public static string GetMvcUrlGeneral(int? sNodeModule, int? sNodeItem, string subPath, bool resolveUrl) { return GetMvcUrlGeneral(sNodeModule, sNodeItem, subPath, resolveUrl, false); }
    public static string GetMvcUrlGeneral(int? sNodeModule, int? sNodeItem, string subPath, bool resolveUrl, bool fullUrl)
    {
      string url = null;
      try
      {
        string fmtString = IKGD_Config.AppSettings["MVC_Route_sNode_UrlFormat"] ?? "~/code/{0}/{1}{2}";
        //url = string.Format(fmtString, sNodeItem, sNodeModule, Utility.UrlEncodeIndexPathForSEO(subPath)).TrimEnd('/');
        url = string.Format(fmtString, sNodeItem, sNodeModule, Utility.UrlEncodePath_IIS(subPath)).TrimEnd('/');
        if (resolveUrl)
          url = Utility.ResolveUrl(url);
        //url = Utility.UrlEncodePath_IIS(url, false);
        if (fullUrl)
          url = new Uri(HttpContext.Current.Request.Url, url).ToString();
      }
      catch { }
      return url;
    }


    public static string GetMvcUrlGeneralV2(int? sNodeItem) { return GetMvcUrlGeneralV2(IKGD_Language_Provider.Provider.Language, sNodeItem, null, null, false); }
    public static string GetMvcUrlGeneralV2(int? sNodeItem, int? sNodeModule) { return GetMvcUrlGeneralV2(IKGD_Language_Provider.Provider.Language, sNodeItem, sNodeModule, null, false); }
    public static string GetMvcUrlGeneralV2(int? sNodeItem, int? sNodeModule, string subPath) { return GetMvcUrlGeneralV2(IKGD_Language_Provider.Provider.Language, sNodeItem, sNodeModule, subPath, false); }
    public static string GetMvcUrlGeneralV2(int? sNodeItem, int? sNodeModule, string subPath, bool fullUrl) { return GetMvcUrlGeneralV2(IKGD_Language_Provider.Provider.Language, sNodeItem, sNodeModule, subPath, fullUrl); }
    public static string GetMvcUrlGeneralV2(string language, int? sNodeItem, int? sNodeModule, string subPath, bool fullUrl)
    {
      string url = null;
      try
      {
        string fmtString = "~/{0}/{1}/{2}{3}";
        //url = string.Format(fmtString, language.NullIfEmpty() ?? "code", sNodeItem, sNodeModule, Utility.UrlEncodeIndexPathForSEO(subPath)).TrimEnd('/');
        url = string.Format(fmtString, language.NullIfEmpty() ?? "code", sNodeItem, sNodeModule, Utility.UrlEncodePath_IIS(subPath)).TrimEnd('/');
        url = Utility.ResolveUrl(url);
        //url = Utility.UrlEncodePath_IIS(url, false);
        if (fullUrl)
          url = new Uri(HttpContext.Current.Request.Url, url).ToString();
      }
      catch { }
      return url;
    }

    public static string GetMvcUrlGeneralRNODEV2(string language, int? rNodeItem) { return GetMvcUrlGeneralRNODEV2(language, rNodeItem, null, null, false, null); }
    public static string GetMvcUrlGeneralRNODEV2(string language, int? rNodeItem, string subPath) { return GetMvcUrlGeneralRNODEV2(language, rNodeItem, null, subPath, false, null); }
    public static string GetMvcUrlGeneralRNODEV2(string language, int? rNodeItem, string subPath, bool fullUrl) { return GetMvcUrlGeneralRNODEV2(language, rNodeItem, null, subPath, fullUrl, null); }
    public static string GetMvcUrlGeneralRNODEV2(string language, int? rNodeItem, int? rNodeModule, string subPath, bool fullUrl, IEnumerable<int> sNodesHints)
    {
      string url = null;
      try
      {
        string fmtString = "~/{0}/{1}/{2}{3}";
        //url = string.Format(fmtString, language.NullIfEmpty() ?? "rnode", ((rNodeItem == null) ? null : ("r" + rNodeItem.ToString())), ((rNodeModule == null) ? null : ("r" + rNodeModule.ToString())), Utility.UrlEncodeIndexPathForSEO(subPath)).TrimEnd('/');
        url = string.Format(fmtString, language.NullIfEmpty() ?? "rnode", ((rNodeItem == null) ? null : ("r" + rNodeItem.ToString())), ((rNodeModule == null) ? null : ("r" + rNodeModule.ToString())), Utility.UrlEncodePath_IIS(subPath)).TrimEnd('/');
        url = Utility.ResolveUrl(url);
        //url = Utility.UrlEncodePath_IIS(url, false);
        if (sNodesHints != null && sNodesHints.Any())
        {
          url = Utility.UriSetQuery(url, "sNodeFragFilter", Utility.Implode(sNodesHints, ",", null, true, true));
        }
        if (fullUrl)
          url = new Uri(HttpContext.Current.Request.Url, url).ToString();
      }
      catch { }
      return url;
    }


    public static string GetMvcUrlGeneralRNODE(int? rNodeItem) { return GetMvcUrlGeneralRNODE(rNodeItem, null, true, false, null); }
    public static string GetMvcUrlGeneralRNODE(int? rNodeItem, string subPath) { return GetMvcUrlGeneralRNODE(rNodeItem, subPath, true, false, null); }
    public static string GetMvcUrlGeneralRNODE(int? rNodeItem, string subPath, bool resolveUrl) { return GetMvcUrlGeneralRNODE(rNodeItem, subPath, resolveUrl, false, null); }
    public static string GetMvcUrlGeneralRNODE(int? rNodeItem, string subPath, bool resolveUrl, bool fullUrl) { return GetMvcUrlGeneralRNODE(rNodeItem, subPath, resolveUrl, fullUrl, null); }
    public static string GetMvcUrlGeneralRNODE(int? rNodeItem, string subPath, bool resolveUrl, bool fullUrl, IEnumerable<int> sNodesHints)
    {
      string url = null;
      try
      {
        string fmtString = IKGD_Config.AppSettings["MVC_Route_rNode_UrlFormat"] ?? "~/rnode/{0}/{1}";
        //url = string.Format(fmtString, rNodeItem, Utility.UrlEncodeIndexPathForSEO(subPath)).TrimEnd('/');
        url = string.Format(fmtString, rNodeItem, Utility.UrlEncodePath_IIS(subPath)).TrimEnd('/');
        if (sNodesHints != null && sNodesHints.Any())
        {
          url = Utility.UriSetQuery(url, "sNodeFragFilter", Utility.Implode(sNodesHints, ",", null, true, true));
        }
        if (resolveUrl)
          url = Utility.ResolveUrl(url);
        //url = Utility.UrlEncodePath_IIS(url, false);
        if (fullUrl)
          url = new Uri(HttpContext.Current.Request.Url, url).ToString();
      }
      catch { }
      return url;
    }
    public static string GetMvcUrlGeneralRNODE_WithHints(int? rNodeItem, params int[] sNodesHints) { return GetMvcUrlGeneralRNODE(rNodeItem, null, true, false, sNodesHints); }


    public static string GetMvcActionUrl<TController>(Expression<Action<TController>> action) where TController : Controller { return GetMvcActionUrl<TController>(null, action, false); }
    public static string GetMvcActionUrl<TController>(RequestContext requestContext, Expression<Action<TController>> action, bool fullUrl) where TController : Controller
    {
      try
      {
        //
        RouteValueDictionary routeValuesFromExpression = Microsoft.Web.Mvc.Internal.ExpressionHelper.GetRouteValuesFromExpression<TController>(action);
        //
        RouteCollection routeCollection = RouteTable.Routes;
        VirtualPathData virtualPath = routeCollection.GetVirtualPath(requestContext, routeValuesFromExpression);
        if (virtualPath != null)
        {
          if (fullUrl)
            return new Uri(HttpContext.Current.Request.Url, virtualPath.VirtualPath).ToString();
          else
            return virtualPath.VirtualPath;
        }
      }
      catch { }
      return null;
    }


    /// <summary>
    /// generazione della url per per il ProxyVFS.axd da utilizzare nel codice .cs
    /// attenzione che non genera url con encoding per html attributes
    /// (in tal caso usare UrlHelperExtension.UrlProxyVFS)
    /// </summary>
    /// <param name="rNode"></param>
    /// <param name="sNode"></param>
    /// <param name="stream"></param>
    /// <param name="defaultResourceUrl"></param>
    /// <returns></returns>
    public static string GetUrlProxyVFS(int? rNode, int? sNode, string stream, string defaultResourceUrl) { return GetUrlProxyVFS(rNode, sNode, stream, defaultResourceUrl, null, false, null, false, null); }
    public static string GetUrlProxyVFS(int? rNode, int? sNode, string stream, string defaultResourceUrl, bool fullUrl) { return GetUrlProxyVFS(rNode, sNode, stream, defaultResourceUrl, null, fullUrl, null, false, null); }
    public static string GetUrlProxyVFS(int? rNode, int? sNode, string stream, string defaultResourceUrl, int? cacheOnBrowser) { return GetUrlProxyVFS(rNode, sNode, stream, defaultResourceUrl, null, false, cacheOnBrowser, false, null); }
    public static string GetUrlProxyVFS(int? rNode, int? sNode, string stream, string defaultResourceUrl, int? VersionFrozen, bool fullUrl, int? cacheOnBrowser) { return GetUrlProxyVFS(rNode, sNode, stream, defaultResourceUrl, VersionFrozen, fullUrl, cacheOnBrowser, false, null); }
    public static string GetUrlProxyVFS(int? rNode, int? sNode, string stream, string defaultResourceUrl, int? VersionFrozen, bool fullUrl, int? cacheOnBrowser, bool? forceDownload) { return GetUrlProxyVFS(rNode, sNode, stream, defaultResourceUrl, VersionFrozen, fullUrl, cacheOnBrowser, forceDownload, null); }
    public static string GetUrlProxyVFS(int? rNode, int? sNode, string stream, string defaultResourceUrl, int? VersionFrozen, bool fullUrl, int? cacheOnBrowser, bool? forceDownload, string extraPathInfo)
    {
      string url = Utility.ResolveUrl("~/ProxyVFS.axd");
      Dictionary<string, object> frags = new Dictionary<string, object>();
      //
      stream = stream.TrimSafe().Replace("|", ",");
      if (Ikon.Handlers.ProxyVFS2_Helper.Enabled)
      {
        url += "/{2}/{0}{1}".FormatString(rNode != null ? "r" : string.Empty, rNode ?? sNode, (stream.IsNullOrEmpty() || stream == ",") ? "null" : stream);
      }
      else
      {
        if (rNode != null)
          frags["rnode"] = rNode;
        else if (sNode != null)
          frags["snode"] = sNode;
        frags["stream"] = stream;
      }
      //
      frags["freeze"] = VersionFrozen;
      frags["cacheBrowser"] = cacheOnBrowser;
      if (forceDownload == true)
        frags["forceDownload"] = forceDownload;
      if (!string.IsNullOrEmpty(defaultResourceUrl))
        frags["default"] = defaultResourceUrl.StartsWith("~/") ? Utility.ResolveUrl(defaultResourceUrl) : defaultResourceUrl;
      //
      //if (DontUseQueryString)
      //{
      //  url += "/" + Utility.StringToBase64(Ikon.GD.IKGD_Serialization.SerializeToJSON(frags.Where(f => f.Value != null))) + "?";  // aggiungiamo il ? per poter forzare correttamente l'estensione del file con &ext=.jpg
      //}
      //cassini non supporta gli extra PathInfo con gli httphandlers
      if (!string.IsNullOrEmpty(extraPathInfo) && HttpRuntime.UsingIntegratedPipeline)
      {
        url += ("/" + Utility.UrlEncodeIndexPathForDownload(extraPathInfo.TrimStart(' ', '/'))).TrimEnd(' ', '/');
      }
      url += "?" + string.Join("&", frags.Where(f => f.Value != null).Select(f => string.Format("{0}={1}", f.Key, HttpUtility.UrlEncode(f.Value.ToString()))).ToArray());
      //
      if (fullUrl)
      {
        // attenzione che vaporizza l'eventuale encoding del |
        //url = new Uri(HttpContext.Current.Request.Url, url).ToString();
        url = new Uri(HttpContext.Current.Request.Url, url).ToString().Replace("|", "%7c");
      }
      //
      return url;
    }

    public static string GetUrlProxyVFS(string path, string stream, string defaultResourceUrl, int? VersionFrozen, bool fullUrl, int? cacheOnBrowser, bool? forceDownload)
    {
      if (string.IsNullOrEmpty(path))
        return null;
      List<string> frags = new List<string>();
      frags.Add("path=" + HttpUtility.UrlEncode(path));
      frags.Add("stream=" + HttpUtility.UrlEncode((stream ?? string.Empty)));
      if (VersionFrozen != null)
        frags.Add("freeze=" + VersionFrozen.ToString());
      if (!string.IsNullOrEmpty(defaultResourceUrl))
        frags.Add("default=" + HttpUtility.UrlEncode(Utility.ResolveUrl(defaultResourceUrl)));
      if (cacheOnBrowser != null)
        frags.Add("cacheBrowser=" + cacheOnBrowser.ToString());
      if (forceDownload == true)
        frags.Add("forceDownload=" + forceDownload.ToString());
      string url = Utility.ResolveUrl("~/ProxyVFS.axd?" + string.Join("&", frags.ToArray()));
      //
      if (fullUrl)
        url = new Uri(HttpContext.Current.Request.Url, url).ToString();
      return url;
    }


    public static string GetUrlProxyIKATT(int idAttr, string stream) { return GetUrlProxyIKATT(idAttr, stream, null, false, null, true); }
    public static string GetUrlProxyIKATT(int idAttr, string stream, bool fullUrl) { return GetUrlProxyIKATT(idAttr, stream, null, fullUrl, null, true); }
    public static string GetUrlProxyIKATT(int idAttr, string stream, string defaultResourceUrl, bool fullUrl) { return GetUrlProxyIKATT(idAttr, stream, defaultResourceUrl, fullUrl, null, true); }
    public static string GetUrlProxyIKATT(int idAttr, string stream, string defaultResourceUrl, bool fullUrl, int? cacheOnBrowser) { return GetUrlProxyIKATT(idAttr, stream, defaultResourceUrl, fullUrl, cacheOnBrowser, true); }
    public static string GetUrlProxyIKATT(int idAttr, string stream, string defaultResourceUrl, bool fullUrl, int? cacheOnBrowser, bool encodeUrl)
    {
      string url = GetUrlProxyATTR(true, idAttr, stream, defaultResourceUrl, fullUrl, cacheOnBrowser, false, null);
      return encodeUrl ? Utility.HtmlAttributeEncode(url) : url;
    }


    public static string GetUrlProxyIKCAT(int idAttr, string stream) { return GetUrlProxyIKCAT(idAttr, stream, null, false, null, true); }
    public static string GetUrlProxyIKCAT(int idAttr, string stream, bool fullUrl) { return GetUrlProxyIKCAT(idAttr, stream, null, fullUrl, null, true); }
    public static string GetUrlProxyIKCAT(int idAttr, string stream, string defaultResourceUrl, bool fullUrl) { return GetUrlProxyIKCAT(idAttr, stream, defaultResourceUrl, fullUrl, null, true); }
    public static string GetUrlProxyIKCAT(int idAttr, string stream, string defaultResourceUrl, bool fullUrl, int? cacheOnBrowser) { return GetUrlProxyIKCAT(idAttr, stream, defaultResourceUrl, fullUrl, cacheOnBrowser, true); }
    public static string GetUrlProxyIKCAT(int idAttr, string stream, string defaultResourceUrl, bool fullUrl, int? cacheOnBrowser, bool encodeUrl)
    {
      string url = GetUrlProxyATTR(false, idAttr, stream, defaultResourceUrl, fullUrl, cacheOnBrowser, false, null);
      return encodeUrl ? Utility.HtmlAttributeEncode(url) : url;
    }


    public static string GetUrlProxyATTR(bool isIKATTR, int idAttr, string stream, string defaultResourceUrl, bool fullUrl, int? cacheOnBrowser, bool? forceDownload, string extraPathInfo)
    {
      string url = Utility.ResolveUrl("~/ProxyIKATT.axd");
      Dictionary<string, object> frags = new Dictionary<string, object>();
      //
      stream = stream.TrimSafe().Replace("|", ",");
      url += "/{2}/{0}{1}".FormatString(isIKATTR ? "a" : "c", idAttr, (stream.IsNullOrEmpty() || stream == ",") ? "null" : stream);
      //
      frags["cacheBrowser"] = cacheOnBrowser;
      if (forceDownload == true)
        frags["forceDownload"] = forceDownload;
      if (!string.IsNullOrEmpty(defaultResourceUrl))
        frags["default"] = defaultResourceUrl.StartsWith("~/") ? Utility.ResolveUrl(defaultResourceUrl) : defaultResourceUrl;
      //
      //cassini non supporta gli extra PathInfo con gli httphandlers
      if (!string.IsNullOrEmpty(extraPathInfo) && HttpRuntime.UsingIntegratedPipeline)
      {
        url += ("/" + Utility.UrlEncodeIndexPathForDownload(extraPathInfo.TrimStart(' ', '/'))).TrimEnd(' ', '/');
      }
      url += "?" + string.Join("&", frags.Where(f => f.Value != null).Select(f => string.Format("{0}={1}", f.Key, HttpUtility.UrlEncode(f.Value.ToString()))).ToArray());
      //
      if (fullUrl)
      {
        // attenzione che vaporizza l'eventuale encoding del |
        //url = new Uri(HttpContext.Current.Request.Url, url).ToString();
        url = new Uri(HttpContext.Current.Request.Url, url).ToString().Replace("|", "%7c");
      }
      //
      return url;
    }


  }



}

