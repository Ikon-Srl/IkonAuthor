/*
 * 
 * Intranet Ikon
 * 
 * Copyright (C) 2009 Ikon Srl
 * Tutti i Diritti Riservati. All Rights Reserved
 * 
 */


using System;
using System.Data;
using System.Configuration;
using System.Collections;
using System.Collections.Generic;
using System.Web;
using System.Web.Caching;
using System.Web.Security;
using System.Linq;
using System.Xml.Linq;
using System.Data.Linq;
using System.Data.Linq.Mapping;
using System.Linq.Expressions;
using System.Net;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using LinqKit;
using Autofac;

using Ikon;
using Ikon.GD;
using Ikon.Log;
using Ikon.IKCMS;



namespace Ikon.Handlers
{
  public class ModuleHandlerUrlRewriteSEO_OLD : IHttpModule
  {
    public List<Regex> regexSkipRewrite { get; set; }
    public List<Regex> regexRedirectByTransferRequest { get; set; }
    //
    public UrlRewriteSEO_Custom_Interface CustomProcessor { get; protected set; }
    //
    public FS_Operations fsOp { get { return IKCMS_ManagerIoC.requestContainer.ResolveNamed<FS_Operations>("readonly"); } }
    //public FS_Operations fsOp { get { return IKCMS_ManagerIoC.requestContainer.Resolve<FS_Operations>(); } }
    //

    public void Dispose() { }

    public void Init(HttpApplication app)
    {
      //
      regexSkipRewrite = new List<Regex>();
      //regexSkipRewrite.Add(new Regex(@".*\?{0,0}.*\.(gif|jpg|jpeg|png|swf|js|css|xml|ico|ttf|eot|otf|htc|fla|pdf|doc|bmp)(\?|$)", RegexOptions.Compiled | RegexOptions.IgnoreCase));  // immagini (ma non nelle query string) attenzione che in questa forma e' molto lenta in presenza di querystring lunghe (il problema sembra essere nel .* iniziale)
      //regexSkipRewrite.Add(new Regex(@"\?{0,0}.*\.(gif|jpg|jpeg|png|swf|js|css|xml|ico|ttf|eot|otf|htc|fla|pdf|doc|bmp)(\?|$)", RegexOptions.Compiled | RegexOptions.Singleline | RegexOptions.IgnoreCase));  // immagini (ma non nelle query string)
      regexSkipRewrite.Add(new Regex(@"[^?]*\.(gif|jpg|jpeg|png|swf|js|css|xml|ico|ttf|eot|otf|htc|fla|pdf|doc|bmp)(\?|$)", RegexOptions.Compiled | RegexOptions.Singleline | RegexOptions.IgnoreCase));  // immagini (ma non nelle query string)
      regexSkipRewrite.Add(new Regex(@"/ProxyVFS", RegexOptions.Compiled | RegexOptions.Singleline | RegexOptions.IgnoreCase));  // route per il proxyVFS
      regexSkipRewrite.Add(new Regex(@"/.+\.axd(/|\?)", RegexOptions.Compiled | RegexOptions.Singleline | RegexOptions.IgnoreCase));  // httphandlers
      regexSkipRewrite.Add(IKGD_SEO_Manager.RegExToDisableUrlRewrite);  // querystring per saltare il modulo di url rewrite
      //
      regexRedirectByTransferRequest = new List<Regex>();
      regexRedirectByTransferRequest.Add(new Regex(@"\.(html|htm)(\?|$)", RegexOptions.Compiled | RegexOptions.IgnoreCase));
      //
      if (!string.IsNullOrEmpty(IKGD_Config.AppSettings["UrlRewriteSEO_CustomProcessorType"]))
      {
        try
        {
          Type CustomProcessorType = Utility.FindTypeCachedExt(IKGD_Config.AppSettings["UrlRewriteSEO_CustomProcessorType"], false);
          if (CustomProcessorType.IsAssignableTo(typeof(UrlRewriteSEO_Custom_Interface)))
          {
            CustomProcessor = Activator.CreateInstance(CustomProcessorType) as UrlRewriteSEO_Custom_Interface;
          }
        }
        catch { }
      }
      //
      app.BeginRequest += new EventHandler(app_BeginRequest);
      //app.EndRequest += new EventHandler(app_EndRequest);
    }


    void app_EndRequest(object sender, EventArgs e)
    {
      IKCMS_ExecutionProfiler.AddMessage("ModuleHandlerUrlRewriteSEO EndRequest");
    }


    void app_BeginRequest(object sender, EventArgs e)
    {
      IKCMS_ExecutionProfiler.AddMessage("ModuleHandlerUrlRewriteSEO BeginRequest");
      HttpApplication app = ((HttpApplication)(sender));
      try
      {
        string pq = app.Context.Request.Url.PathAndQuery;
        if (regexSkipRewrite.Any(r => r.IsMatch(pq)))
          return;
        //IKCMS_ExecutionProfiler.AddMessage("ModuleHandlerUrlRewriteSEO regexSkipRewrite:");
        //
        // ricerca delle eventuali info nella tabella dei mapping SEO
        //
        string urlResolved = app.Context.Request.Url.PathAndQuery;
        if (urlResolved.StartsWith(HttpRuntime.AppDomainAppVirtualPath) && HttpRuntime.AppDomainAppVirtualPath.Length > 1)
          urlResolved = urlResolved.Substring(HttpRuntime.AppDomainAppVirtualPath.Length);
        //
        List<IKGD_SEO_Manager.Element> datas = IKGD_SEO_Manager.MapIncomingUrl(urlResolved).ToList();
        if (datas == null || !datas.Any())
        {
          urlResolved = app.Context.Request.Url.ToString();
          datas = IKGD_SEO_Manager.MapIncomingUrl(urlResolved).ToList();
        }
        if (datas == null || !datas.Any())
        {
          if (CustomProcessor != null)
          {
            IKCMS_ExecutionProfiler.AddMessage("ModuleHandlerUrlRewriteSEO CustomProcessor: CALLED");
            if (CustomProcessor.Process(app) == false)
            {
              return;
            }
          }
          IKCMS_ExecutionProfiler.AddMessage("ModuleHandlerUrlRewriteSEO BeginRequest: END1");
          return;
        }
        //
        int? httpCode = datas != null ? datas.Select(r => r.HttpCode).FirstOrDefault(c => c != null) : (int?)null;
        var dataOut = IKGD_SEO_Manager.MapOutcomingSet(datas);
        //
        var sNodesActive = dataOut.Where(r => r.Target_sNode != null).Select(r => r.Target_sNode.Value).Distinct().ToList();
        if (sNodesActive.Count > 1)
        {
          //var lang = IKGD_Language_Provider.Provider.Language;
          //using (FS_Operations fsOp = new FS_Operations())
          //{
          //
          // si aggiunge un ulteriore filtro per il language: necessariamente sul path della risorsa
          var paths = fsOp.PathsFromNodesExt(sNodesActive).FilterCustom(new Func<IKGD_Path, bool>[] { IKGD_Path_Helper.FilterByRootCMS, IKGD_Path_Helper.FilterByActive, IKGD_Path_Helper.FilterByAreas, IKGD_Path_Helper.FilterByLanguage }).ToList();
          dataOut = dataOut.Where(r => paths.Any(p => p.sNode == r.Target_sNode));
          //}
        }
        var data = dataOut.FirstOrDefault(m => m.IsCanonical && !string.IsNullOrEmpty(m.SEO_Url));
        if (data != null)
        {
          string urlNew = data.SEO_Url.StartsWith("/") ? HttpRuntime.AppDomainAppVirtualPath.TrimEnd('/') + data.SEO_Url : data.SEO_Url;
          try { urlNew = Utility.UriMigrateQueryString(app.Context.Request.Url.ToString(), urlNew, false); }
          catch { }
          IKGD_SEO_Manager.ContextCanonicalUrl = urlNew;
        }
        //
        // se la url e' diversa da quella canonica/mappata ed e' stato specificato un codice http (nel db o come default nel web.config)
        // eseguiamo il redirect della request senza continuare il processing
        //
        data = data ?? dataOut.FirstOrDefault() ?? datas.OrderBy(m => m.Priority).FirstOrDefault();
        if (data != null && !string.IsNullOrEmpty(data.SEO_Url) && (data.HttpCode > 0 || httpCode > 0))
        {
          httpCode = data.HttpCode ?? httpCode;
          //int? statusCode = data.HttpCode ?? Utility.TryParse<int?>(IKGD_Config.AppSettings["UrlRewriteDefaultHttpCode"], null);
          string url01 = HttpUtility.UrlDecode(urlResolved);
          string url02 = HttpUtility.UrlDecode(data.SEO_Url);
          if (url01.IndexOf('?') >= 0)
            url01 = url01.Substring(0, url01.IndexOf('?'));
          if (url02.IndexOf('?') >= 0)
            url02 = url01.Substring(0, url02.IndexOf('?'));
          if (!url01.Equals(url02, StringComparison.OrdinalIgnoreCase))
          {
            string urlNew = data.SEO_Url.StartsWith("/") ? HttpRuntime.AppDomainAppVirtualPath.TrimEnd('/') + data.SEO_Url : data.SEO_Url;
            try { urlNew = Utility.UriMigrateQueryString(app.Context.Request.Url.ToString(), urlNew, false); }
            catch { }
            app.Context.Response.StatusCode = httpCode.Value;
            app.Context.Response.Redirect(urlNew, false);
            app.CompleteRequest();
            return;
          }
        }
        //
        // in un contesto di rewrite, provvedo a non modificare la url visualizzata
        // ma a processare la url fornita con una url normalizzata in forma CMS
        // generazione della url rimappata
        //
        string urlToRedirect = null;
        if (data.Target_sNode != null && !string.IsNullOrEmpty(data.Target_Url) && data.IsCanonical)
        {
          try
          {
            //using (FS_Operations fsOp = new FS_Operations())
            //{
            // e' piu' facile che il path sia in cache piuttosto che fare sempre il fetch del fsNode, poi comunque servira' il path per la costrzione del model
            var path = fsOp.PathsFromNodeExt(data.Target_sNode.Value).FirstOrDefault();
            if (path != null && string.Equals(path.LastFragment.ManagerType, "IKCMS_ResourceType_PageStatic", StringComparison.OrdinalIgnoreCase))
            {
              urlToRedirect = Utility.ResolveUrl(data.Target_Url);
              try { urlToRedirect = Utility.UriMigrateQueryString(app.Context.Request.Url.ToString(), urlToRedirect, true); }
              catch { }
            }
            //var fsNode = fsOp.Get_NodeInfo(data.Target_sNode.Value, false);
            //if (fsNode != null && fsNode.vData.manager_type == "IKCMS_ResourceType_PageStatic")
            //{
            //  urlToRedirect = Utility.ResolveUrl(data.Target_Url);
            //  try { urlToRedirect = Utility.UriMigrateQueryString(app.Context.Request.Url.ToString(), urlToRedirect, true); }
            //  catch { }
            //}
            //}
          }
          catch { }
        }
        if (data.Target_sNode != null && urlToRedirect == null)
        {
          urlToRedirect = Ikon.IKCMS.IKCMS_RouteUrlManager.GetMvcUrlGeneral(data.Target_sNode);
          try { urlToRedirect = Utility.UriMigrateQueryString(app.Context.Request.Url.ToString(), urlToRedirect, true); }
          catch { }
        }
        if (!string.IsNullOrEmpty(data.Target_Url) && urlToRedirect == null)
        {
          // mapping diretto fornito dall'utente
          urlToRedirect = data.Target_Url;
          urlToRedirect = Utility.UriDelQueryVars(urlToRedirect, app.Context.Request.QueryString.AllKeys.Distinct().ToArray());
          try { urlToRedirect = Utility.UriMigrateQueryString(app.Context.Request.Url.ToString(), urlToRedirect, true); }
          catch { }
          urlToRedirect = urlToRedirect.StartsWith("/") ? HttpRuntime.AppDomainAppVirtualPath.TrimEnd('/') + urlToRedirect : urlToRedirect;
        }
        //
        if (!string.IsNullOrEmpty(urlToRedirect))
        {
          try
          {
            //supporto per il modulo di extensionless url routing
            //app.Context.Items["IKCMS_RedirectedUrl"] = urlToRedirect;
            if (app.Context.Request.Url.AbsolutePath.IndexOf('.') != -1)
            {
              app.Context.Server.TransferRequest(urlToRedirect, true);
            }
            else
            {
              app.Context.RewritePath(urlToRedirect, true);
            }
          }
          catch { }
        }
      }
      catch { }
      IKCMS_ExecutionProfiler.AddMessage("ModuleHandlerUrlRewriteSEO BeginRequest: END2");
    }


  }


}
