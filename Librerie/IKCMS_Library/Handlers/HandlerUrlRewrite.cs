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
  //
  // per registrare i moduli automaticamente con .NET4 senza intervenire sul web.config
  // http://blog.davidebbo.com/2011/02/register-your-http-modules-at-runtime.html
  //


  //
  // per registrare estensioni al modulo di Url rewrite
  //
  public interface UrlRewriteSEO_Custom_Interface
  {
    bool Process(HttpApplication app);  // if return == false stop processing in the main handler/module
  }



  //
  // Url Rewrite Module per la gestione delle url da rimappare con codice http specifico
  // prima di far intervenire il routing engine di MVC
  // da registrare direttamente nel web.config
  // attenzione che HttpModule non funziona in quanto e' la parte terminale della richiesta
  // non e' possibile far continuare la pipeline
  // attenzione a non settare preCondition="managedHandler" perche' altrimenti non processa le richieste associate estensioni statiche (es .html)
  //
  //  <httpModules>
  //    <add name="RewriteUrlSEO" type="Ikon.Handlers.ModuleHandlerUrlRewriteSEO, IKCMS_Library"/>
  //  </httpModules>
  //
  public class ModuleHandlerUrlRewriteSEO : IHttpModule
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
      if (IKGD_Config.AppSettings["UrlRewrite_RegExSkip"].IsNotNullOrWhiteSpace())
      {
        try { regexSkipRewrite.Add(new Regex(IKGD_Config.AppSettings["UrlRewrite_RegExSkip"], RegexOptions.Compiled | RegexOptions.Singleline | RegexOptions.IgnoreCase)); }
        catch { }
      }
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
        string urlResolved = pq;
        if (urlResolved.StartsWith(HttpRuntime.AppDomainAppVirtualPath) && HttpRuntime.AppDomainAppVirtualPath.Length > 1)
          urlResolved = urlResolved.Substring(HttpRuntime.AppDomainAppVirtualPath.Length);
        //
        List<IKGD_SEO_Manager.Element> datas = IKGD_SEO_Manager.MapIncomingUrl(urlResolved).OrderBy(r => r.Priority).ToList();
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
        List<IKGD_SEO_Manager.Element> dataOut = IKGD_SEO_Manager.MapOutcomingSet(datas).OrderBy(r => datas.IndexOfSortable(r)).ThenBy(r => r.Priority).ToList();
        //
        if (dataOut.Any(r => r.Target_sNode != null || r.Target_rNode != null))
        {
          // cicciopizza: creare helper che non dipenda da fsOp istanziato ma che lo utilizzi solo on demand
          // fsOp ha un constructor non leggero e si puo' eliminare una creazione di connessione al DB
          // tutti i path accessibili, indipendentemente dalla lingua corrente
          var pathsAll = fsOp.PathsFromNodesExt(dataOut.Where(r => r.Target_sNode != null).Select(r => r.Target_sNode.Value).Distinct(), dataOut.Where(r => r.Target_rNode != null).Select(r => r.Target_rNode.Value).Distinct(), false, false, false).FilterCustom(new Func<IKGD_Path, bool>[] { IKGD_Path_Helper.FilterByRootCMS, IKGD_Path_Helper.FilterByActive, IKGD_Path_Helper.FilterByAreas, IKGD_Path_Helper.FilterByLanguageSingleOrNull }).ToList();
          var seo_map =
            (from seo in dataOut
             from path in pathsAll
             where (seo.Target_sNode == path.sNode || seo.Target_rNode == path.rNode) && (seo.Target_sNode == null || seo.Target_sNode == path.sNode) && (seo.Target_rNode == null || seo.Target_rNode == path.rNode)
             select new { seo, path }).ToList();
          //
          switch (IKGD_SEO_Manager.UrlRewriteMappingMode)
          {
            case IKGD_SEO_Manager.UrlRewriteMappingModeEnum.Legacy:
              {
                // proviamo prima a mappare il tutto solo sugli sNodes compatibili con la lingua corrente
                // in questo modo proviamo prima i mapping compatibili con la lingua corrente e solo nel caso non ci fosse
                // nessuna corrispondenza proviamo ad attivare i matches su lingue diverse
                //var sNodes = pathsAll.FilterPathsByLanguage().Select(p => p.sNode).Distinct().ToList();
                var sNodes = pathsAll.FilterPathsByLanguage().Select(p => p.sNode).Concat(datas.Where(r => r.Target_sNode != null).Select(r => r.Target_sNode.Value)).Distinct().ToList();
                dataOut = seo_map.Where(r => sNodes.Contains(r.path.sNode)).Select(r => r.seo).Distinct().OrderBy(r => datas.IndexOfSortable(r)).ThenBy(r => r.Priority).ToList();
                if (!dataOut.Any())
                {
                  dataOut = seo_map.Select(r => r.seo).Distinct().OrderBy(r => datas.IndexOfSortable(r)).ThenBy(r => r.Priority).ToList();
                }
              }
              break;
            case IKGD_SEO_Manager.UrlRewriteMappingModeEnum.StandardWithNoLanguageRemapping:
            case IKGD_SEO_Manager.UrlRewriteMappingModeEnum.Standard:
            default:
              {
                var sNodesForCurrentLanguage = pathsAll.FilterPathsByLanguage().Select(p => p.sNode).ToList();
                var sNodesFromData = datas.Where(r => r.Target_sNode != null).Select(r => r.Target_sNode.Value).ToList();
                List<IKGD_SEO_Manager.Element> dataOutFiltered = null;
                if (dataOutFiltered == null || !dataOutFiltered.Any())
                {
                  var sNodes = sNodesForCurrentLanguage.Intersect(sNodesFromData).Distinct().ToList();
                  dataOutFiltered = seo_map.Where(r => sNodes.Contains(r.path.sNode)).Select(r => r.seo).Distinct().OrderBy(r => datas.IndexOfSortable(r)).ThenBy(r => r.Priority).ToList();
                }
                if (dataOutFiltered == null || !dataOutFiltered.Any())
                {
                  var sNodes = sNodesForCurrentLanguage.Concat(sNodesFromData).Distinct().ToList();
                  dataOutFiltered = seo_map.Where(r => sNodes.Contains(r.path.sNode)).Select(r => r.seo).Distinct().OrderBy(r => datas.IndexOfSortable(r)).ThenBy(r => r.Priority).ToList();
                }
                if (dataOutFiltered == null || !dataOutFiltered.Any())
                {
                  dataOutFiltered = seo_map.Select(r => r.seo).Distinct().OrderBy(r => datas.IndexOfSortable(r)).ThenBy(r => r.Priority).ToList();
                }
                if (dataOutFiltered != null && dataOutFiltered.Any())
                {
                  dataOut = dataOutFiltered;
                }
              }
              break;
          }
          //
        }
        var data = dataOut.FirstOrDefault(m => m.IsCanonical && m.SEO_Url.IsNotEmpty());
        if (data != null && data.SEO_Url.IsNotEmpty())
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
        if ((data.Target_sNode != null || data.Target_rNode != null) && data.Target_Url.IsNotEmpty() && data.IsCanonical)
        {
          try
          {
            IKGD_Path path = null;
            if (data.Target_sNode != null)
            {
              path = fsOp.PathsFromNodeExt(data.Target_sNode.Value).FirstOrDefault();
            }
            else
            {
              path = fsOp.PathsFromNodesExt(null, new int[] { data.Target_rNode.Value }, false, false).FirstOrDefault();
            }
            if (path != null && string.Equals(path.LastFragment.ManagerType, "IKCMS_ResourceType_PageStatic", StringComparison.OrdinalIgnoreCase))
            {
              urlToRedirect = Utility.ResolveUrl(data.Target_Url);
              try { urlToRedirect = Utility.UriMigrateQueryString(app.Context.Request.Url.ToString(), urlToRedirect, true); }
              catch { }
            }
          }
          catch { }
        }
        if ((data.Target_sNode != null || data.Target_rNode != null) && urlToRedirect == null)
        {
          if (data.Target_sNode != null)
          {
            urlToRedirect = Ikon.IKCMS.IKCMS_RouteUrlManager.GetMvcUrlGeneralV2(data.Target_sNode);
          }
          else
          {
            urlToRedirect = Ikon.IKCMS.IKCMS_RouteUrlManager.GetMvcUrlGeneralRNODEV2(null, data.Target_rNode, null, null, false, null);
          }
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
