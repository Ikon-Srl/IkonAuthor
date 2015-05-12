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
using System.Linq;
using System.Web;
using System.Xml.Linq;
using System.Data.Linq;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Security;
using System.Linq.Expressions;
using LinqKit;
using Autofac;

using System.Reflection;
using System.Data.Linq.Mapping;
using System.Diagnostics;
using System.Transactions;
using System.Web.Caching;
using System.Web.Mvc;
using System.Web.Mvc.Html;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;  // assembly System.ServiceModel.Web

using Ikon;
using Ikon.GD;


namespace Ikon.GD
{
  using Ikon.IKCMS;


  public static class IKGD_SEO_Manager
  {
    private static object _lock = new object();
    private static readonly string keyName = "cacheKey_IKGD_SEO_Manager";
    private static readonly string dumpPath = IKGD_Config.GetAuthorConfigFile("SEO_ManagerDB.{0}.xml");
    public static readonly string DisableUrlRewriteQS = "NoUrlRewrite";
    public static readonly Regex RegExToDisableUrlRewrite = new Regex(@"(\?|&)" + DisableUrlRewriteQS + @"(=|&|$)", RegexOptions.Compiled | RegexOptions.Singleline | RegexOptions.IgnoreCase);  // querystring per saltare il modulo di url rewrite
    //
    public enum UrlRewriteMappingModeEnum { Standard, StandardWithNoLanguageRemapping, Legacy }
    public static UrlRewriteMappingModeEnum UrlRewriteMappingMode { get; private set; }
    //
    private static FS_Operations fsOp { get { return IKCMS_ManagerIoC.requestContainer.ResolveNamed<FS_Operations>("readonly"); } }
    //private static FS_Operations fsOp { get { return IKCMS_ManagerIoC.requestContainer.Resolve<FS_Operations>(); } }
    //


    static IKGD_SEO_Manager()
    {
      UrlRewriteMappingMode = UrlRewriteMappingModeEnum.Standard;
      try { UrlRewriteMappingMode = (UrlRewriteMappingModeEnum)Enum.Parse(typeof(UrlRewriteMappingModeEnum), IKGD_Config.AppSettings["UrlRewriteMappingMode"] ?? UrlRewriteMappingMode.ToString()); }
      catch { }
    }


    public static void Clear()
    {
      lock (_lock)
      {
        try { HttpRuntime.Cache.Remove(keyName); }
        catch { }
      }
    }


    public static string ContextCanonicalUrl { get { return (string)HttpContext.Current.Items["CanonicalUrl"]; } set { HttpContext.Current.Items["CanonicalUrl"] = value; } }
    public static string ContextCanonicalUrlNN { get { return ContextCanonicalUrl ?? HttpContext.Current.Request.Url.PathAndQuery; } }
    // utilizzare UrlHelperExtension.WriteCanonicalUrl --> Html.WriteCanonicalUrl()
    //public static string WriteMetaCanonical() { return string.Format("<link rel='canonical' href='{0}' />", new Uri(HttpContext.Current.Request.Url, IKGD_SEO_Manager.ContextCanonicalUrlNN)); }
    //public static string WriteMetaCanonical(string virtualUrl) { return string.Format("<link rel='canonical' href='{0}' />", new Uri(HttpContext.Current.Request.Url, Utility.ResolveUrl(virtualUrl))); }


    //
    // attenzione a passare urlResolved = Utility.ResolveUrl(url);
    //
    public static IEnumerable<Element> MapIncomingUrl(string urlResolved)
    {
      try
      {
        int hash = Element.GetHashCode_Helper(urlResolved, null, null);
        if (!ElementsAccessor.Lookup_SEO.Contains(hash))
          hash = Element.GetHashCode_Helper(HttpUtility.UrlDecode(urlResolved), null, null);
        if (!ElementsAccessor.Lookup_SEO.Contains(hash) && urlResolved.IndexOf('?') > 0)
        {
          hash = Element.GetHashCode_Helper(urlResolved.Substring(0, urlResolved.IndexOf('?')), null, null);
          if (!ElementsAccessor.Lookup_SEO.Contains(hash))
            hash = Element.GetHashCode_Helper(HttpUtility.UrlDecode(urlResolved.Substring(0, urlResolved.IndexOf('?'))), null, null);
        }
        if (!ElementsAccessor.Lookup_SEO.Contains(hash))
          return Enumerable.Empty<Element>();
        // cerca il primo mapping corrispondente alla url richiesta
        var elements = ElementsAccessor.Lookup_SEO[hash].Where(r => r.Language == null || r.Language == IKGD_Language_Provider.Provider.LanguageNN).OrderBy(r => r.Priority).ToList();
        //Element element = elements.FirstOrDefault(e => e.IsCanonical) ?? elements.FirstOrDefault();
        return elements;
      }
      catch { }
      return Enumerable.Empty<Element>();
    }


    public static Element MapUnknownUrlToElement(string urlResolved, bool enableSkipQueryStringCheck) { return MapUnknownUrlToElements(urlResolved, enableSkipQueryStringCheck).FirstOrDefault(); }
    public static List<Element> MapUnknownUrlToElements(string urlResolved, bool enableSkipQueryStringCheck)
    {
      List<Element> elements = null;
      try
      {
        //urlResolved = Utility.ResolveUrl(urlResolved);
        int hash = Element.GetHashCode_Helper(urlResolved, null, null);
        if (!ElementsAccessor.Lookup_SEO.Contains(hash) && !ElementsAccessor.Lookup_TargetOnly.Contains(hash))
          hash = Element.GetHashCode_Helper(HttpUtility.UrlDecode(urlResolved), null, null);
        if (enableSkipQueryStringCheck && !ElementsAccessor.Lookup_SEO.Contains(hash) && !ElementsAccessor.Lookup_TargetOnly.Contains(hash) && urlResolved.IndexOf('?') > 0)
        {
          hash = Element.GetHashCode_Helper(urlResolved.Substring(0, urlResolved.IndexOf('?')), null, null);
          if (!ElementsAccessor.Lookup_SEO.Contains(hash) && !ElementsAccessor.Lookup_TargetOnly.Contains(hash))
            hash = Element.GetHashCode_Helper(HttpUtility.UrlDecode(urlResolved.Substring(0, urlResolved.IndexOf('?'))), null, null);
        }
        if (!ElementsAccessor.Lookup_SEO.Contains(hash) && !ElementsAccessor.Lookup_TargetOnly.Contains(hash))
          return new List<Element>();
        // cerca il primo mapping corrispondente alla url richiesta
        elements = ElementsAccessor.Lookup_SEO[hash].Where(r => r.Language == null || r.Language == IKGD_Language_Provider.Provider.LanguageNN).OrderBy(r => r.Priority).ToList();
        if (!elements.Any())
          elements = ElementsAccessor.Lookup_TargetOnly[hash].Where(r => r.Language == null || r.Language == IKGD_Language_Provider.Provider.LanguageNN).OrderBy(r => r.Priority).ToList();
        elements = elements.Where(e => e.IsCanonical).Concat(elements.Where(e => !e.IsCanonical)).ToList();
      }
      catch { }
      return elements ?? new List<Element>();
    }




    public static string MapOutcomingUrl(string url) { return MapOutcomingUrl(url, null); }
    public static string MapOutcomingUrl(string url, bool? checkForValidPaths)
    {
      try
      {
        string urlResolved = Utility.ResolveUrl(url);
        var elements = ElementsAccessor.Lookup_Target[Element.GetHashCode_Helper(null, null, urlResolved)].Where(r => r.Language == null || r.Language == IKGD_Language_Provider.Provider.LanguageNN).OrderBy(r => r.Priority);
        if (!elements.Any())
        {
          elements = ElementsAccessor.Lookup_SEO[Element.GetHashCode_Helper(urlResolved, null, null)].Where(r => r.Language == null || r.Language == IKGD_Language_Provider.Provider.LanguageNN).OrderBy(r => r.Priority);
          if (elements.Any())
            elements = MapOutcomingSet(elements).OrderBy(r => r.Priority);
        }
        // mapping corretto della url con check dei nodi per il supporto multilingua come nell'handler del rewrite
        if (checkForValidPaths.GetValueOrDefault(false))
        {
          var sNodesActive = elements.Where(r => r.Target_sNode != null).Select(r => r.Target_sNode.Value).Distinct().ToList();
          if (sNodesActive.Count > 1)  // check attivato solo per nodi con mapping degenere
          {
            // si aggiunge un ulteriore filtro per il language: necessariamente sul path della risorsa
            var paths = fsOp.PathsFromNodesExt(sNodesActive).FilterCustom(new Func<IKGD_Path, bool>[] { IKGD_Path_Helper.FilterByRootCMS, IKGD_Path_Helper.FilterByLanguage, IKGD_Path_Helper.FilterByActive, IKGD_Path_Helper.FilterByAreas }).ToList();
            elements = elements.Where(r => paths.Any(p => p.sNode == r.Target_sNode)).OrderBy(r => r.Priority);
          }
        }
        Element element = elements.FirstOrDefault(e => e.IsCanonical) ?? elements.FirstOrDefault();
        if (element != null && !string.IsNullOrEmpty(element.SEO_Url))
          return Utility.ResolveUrl(element.SEO_Url);
        return urlResolved;
      }
      catch { }
      return url;
    }


    //
    // versione che non consente la visializzazione del symlink corretto in caso di lingua diversa da quella del mapping
    // sembra che non sia mai usato
    //
    public static string MapOutcomingUrl(int? sNode, int? rNode)
    {
      string urlMapped = null;
      try
      {
        Element element = null;
        if (sNode != null)
        {
          element = ElementsAccessor.Lookup_sNode[sNode.Value].Where(r => r.Language == null || r.Language == IKGD_Language_Provider.Provider.LanguageNN).OrderBy(r => r.Priority).FirstOrDefault(e => e.IsCanonical);
        }
        if (rNode != null && element == null)
        {
          element = ElementsAccessor.Lookup_rNode[rNode.Value].Where(r => r.Language == null || r.Language == IKGD_Language_Provider.Provider.LanguageNN).Where(r => r.Target_sNode == null || sNode == null || r.Target_sNode == sNode).OrderBy(r => r.Priority).FirstOrDefault(e => e.IsCanonical);
        }
        if (element != null && element.SEO_Url.IsNotEmpty())
        {
          urlMapped = Utility.ResolveUrl(element.SEO_Url).NullIfEmpty();
        }
        urlMapped = (urlMapped != null && urlMapped.StartsWith("/")) ? HttpRuntime.AppDomainAppVirtualPath.TrimEnd('/') + urlMapped : urlMapped;
      }
      catch { }
      return urlMapped;
    }


    public static string MapOutcomingUrl(int? sNode, int? rNode, string language)
    {
      string urlMapped = null;
      try
      {
        //
        List<IKGD_SEO_Manager.Element> datas = null;
        if (sNode != null)
        {
          datas = ElementsAccessor.Lookup_sNode[sNode.Value].Where(r => r.Language == null || r.Language == IKGD_Language_Provider.Provider.LanguageNN).OrderBy(r => r.Priority).ToList();
        }
        if (rNode != null && (datas == null || !datas.Any()))
        {
          datas = ElementsAccessor.Lookup_rNode[rNode.Value].Where(r => r.Language == null || r.Language == IKGD_Language_Provider.Provider.LanguageNN).Where(r => r.Target_sNode == null || sNode == null || r.Target_sNode == sNode).OrderBy(r => r.Priority).ToList();
        }
        List<IKGD_SEO_Manager.Element> dataOut = null;
        //
        switch (IKGD_SEO_Manager.UrlRewriteMappingMode)
        {
          case UrlRewriteMappingModeEnum.Legacy:
          case UrlRewriteMappingModeEnum.StandardWithNoLanguageRemapping:
            {
              // manteniamo datas inalterato
            }
            break;
          case UrlRewriteMappingModeEnum.Standard:
          default:
            {
              if (datas != null && datas.Any())
              {
                //
                // questo blocco di codice sembra impattare significativamente sulle performance
                // bisogna cachare i risultati, generamente viene usato nel generatore di menu
                //
                dataOut = IKGD_SEO_Manager.MapOutcomingSet(datas).OrderBy(r => datas.IndexOfSortable(r)).ThenBy(r => r.Priority).ToList();
                var pathsAll = fsOp.PathsFromNodesExt(dataOut.Where(r => r.Target_sNode != null).Select(r => r.Target_sNode.Value).Distinct(), dataOut.Where(r => r.Target_rNode != null).Select(r => r.Target_rNode.Value).Distinct(), false, false, false).FilterCustom(new Func<IKGD_Path, bool>[] { IKGD_Path_Helper.FilterByRootCMS, IKGD_Path_Helper.FilterByActive, IKGD_Path_Helper.FilterByAreas, IKGD_Path_Helper.FilterByLanguageSingleOrNull }).ToList();
                var seo_map =
                  (from seo in dataOut
                   from path in pathsAll
                   where (seo.Target_sNode == path.sNode || seo.Target_rNode == path.rNode) && (seo.Target_sNode == null || seo.Target_sNode == path.sNode) && (seo.Target_rNode == null || seo.Target_rNode == path.rNode)
                   select new { seo, path });
                var sNodesForCurrentLanguage = pathsAll.FilterPathsByLanguage(language ?? IKGD_Language_Provider.Provider.LanguageNN).Select(p => p.sNode).ToList();
                List<IKGD_SEO_Manager.Element> dataOutFiltered = seo_map.Where(r => sNodesForCurrentLanguage.Contains(r.path.sNode)).Select(r => r.seo).Distinct().OrderBy(r => datas.IndexOfSortable(r)).ThenBy(r => r.Priority).ToList();
                //if (dataOutFiltered != null && dataOutFiltered.Any())
                //{
                //  datas = dataOutFiltered;
                //}
                // se non ho nessun mapping per la lingua selezionata annullo datas in modo da non generare una url che sia valida solo per le risorse con url rewrite definito
                datas = dataOutFiltered;
              }
            }
            break;
        }
        //
        if (datas != null && datas.Any())
        {
          var data = datas.FirstOrDefault(m => m.IsCanonical && m.SEO_Url.IsNotEmpty());
          if (data != null && data.SEO_Url.IsNotEmpty())
          {
            urlMapped = data.SEO_Url;
          }
        }
        urlMapped = (urlMapped != null && urlMapped.StartsWith("/")) ? HttpRuntime.AppDomainAppVirtualPath.TrimEnd('/') + urlMapped : urlMapped;
      }
      catch { }
      return urlMapped;
    }


    /*
    public static string MapOutcomingUrlOrRedirect(int sNode, bool doRedirectAction)
    {
      string urlCanonical = null;
      try
      {
        // cerca il primo mapping corrispondente alla url richiesta
        var elements = ElementsAccessor.Lookup_sNode[sNode].Where(r => r.Language == null || r.Language == IKGD_Language_Provider.Provider.LanguageNN).OrderBy(r => r.Priority);
        Element element = elements.FirstOrDefault(e => e.IsCanonical) ?? elements.FirstOrDefault();
        urlCanonical = Utility.ResolveUrl(element.SEO_Url).NullIfEmpty();
        IKGD_SEO_Manager.ContextCanonicalUrl = urlCanonical;
        if (doRedirectAction && !string.IsNullOrEmpty(urlCanonical))
        {
          string urlResolved = Utility.ResolveUrl(HttpContext.Current.Request.Url.PathAndQuery);
          int? statusCode = element.HttpCode ?? Utility.TryParse<int?>(IKGD_Config.AppSettings["UrlRewriteDefaultHttpCode"], null);
          string url01 = HttpUtility.UrlDecode(urlResolved);
          string url02 = HttpUtility.UrlDecode(urlCanonical);
          if (!url01.Equals(url02, StringComparison.OrdinalIgnoreCase) && statusCode > 0)
          {
            HttpContext.Current.Response.StatusCode = statusCode.Value;
            HttpContext.Current.Response.Redirect(urlCanonical, true);
            //HttpContext.Current.Response.End();
            HttpContext.Current.ApplicationInstance.CompleteRequest();  // da usare al posto di .Response.End();
          }
        }
      }
      catch { }
      return urlCanonical;
    }
    */


    public static IEnumerable<IKGD_SEO_Manager.Element> MapOutcomingSet(IEnumerable<IKGD_SEO_Manager.Element> elements)
    {
      try
      {
        var set1 = elements.Where(m => m.Target_sNode != null).SelectMany(m => ElementsAccessor.Lookup_sNode[m.Target_sNode.Value]);
        var set2 = elements.Where(m => m.Target_rNode != null).SelectMany(m => ElementsAccessor.Lookup_rNode[m.Target_rNode.Value]);
        return set1.Concat(set2).Distinct().OrderByDescending(m => m.IsCanonical).ThenBy(m => m.Priority);
        //return elements.Where(m => m.Target_sNode != null).SelectMany(m => ElementsAccessor.Lookup_sNode[m.Target_sNode.Value]).OrderByDescending(m => m.IsCanonical).ThenBy(m => m.Priority);
      }
      catch { }
      return null;
    }


    public static ElementsContainer ElementsAccessor
    {
      get
      {
        lock (_lock)
        {
          ElementsContainer data = (ElementsContainer)HttpRuntime.Cache[keyName];
          try
          {
            if (data == null)
            {
              using (Ikon.Config.DataContext DB = Ikon.Config.DataContext.Factory())
              {
                //
                DB.ObjectTrackingEnabled = false;
                //
                ElementsContainer dataBuilder = new ElementsContainer();
                dataBuilder.Elements = DB.IKCMS_SEOs.Where(r => r.application == IKGD_Config.ApplicationName).Where(r => r.active).OrderBy(r => r.SEO_url).ThenBy(r => r.priority).ThenBy(r => r.id).Select(r => new Element { SEO_Url = r.SEO_url, Target_sNode = r.target_snode, Target_rNode = r.target_rnode, Target_Url = r.target_url, Language = r.language, IsCanonical = r.canonical, HttpCode = r.http_code, Priority = r.priority }).ToList();
                //
                dataBuilder.Elements.RemoveAll(r => string.IsNullOrEmpty(r.SEO_Url));
                dataBuilder.Elements.Where(r => r.SEO_Url.StartsWith("~/")).ForEach(r => r.SEO_Url = Utility.ResolveUrl(r.SEO_Url));
                dataBuilder.Elements.Where(r => !r.SEO_Url.StartsWith("/")).ForEach(r => r.SEO_Url = "/" + r.SEO_Url);
                dataBuilder.Elements.Where(r => r.Target_Url != null).Where(r => r.Target_Url.StartsWith("~/")).ForEach(r => r.Target_Url = Utility.ResolveUrl(r.Target_Url));
                dataBuilder.Elements.Where(r => r.SEO_Url.EndsWith("/")).ForEach(r => r.SEO_Url = r.SEO_Url.TrimEnd('/', ' '));
                dataBuilder.Elements.Where(r => r.SEO_Url.IsNullOrWhiteSpace()).ForEach(r => r.SEO_Url = "/");
                //var tmp01 = dataBuilder.Elements.Select(r => r.SEO_Url).ToList();
                //
                // TODO:
                // per tutte le url che hanno definita una url canonical con un codice di rewrite devo aggiungere un mapping fittizio
                // per le url canoniche del CMS del tipo /code/12345 per attivare l'url rewrite anche in questo caso
                // il sistema dovrebbe venire poi accoppiato con un'estensione in ModuleHandlerUrlRewriteSEO per riconoscere le url tico /code/12345/continuazione-della-url-custom
                //
                if (Utility.TryParse<bool>(IKGD_Config.AppSettings["UrlRewriteAutoAppendCmsUrlOnCanonicalRewrite"], true))
                {
                  var hashes_seo = dataBuilder.Elements.Select(r => r.GetHashCode_SEO()).Distinct().ToList();
                  List<Element> fakeElements = new List<Element>();
                  foreach (var grp in dataBuilder.Elements.Where(r => r.Target_sNode != null).GroupBy(r => new { sNode = r.Target_sNode, lang = r.Language }))
                  {
                    if (!grp.Any(r => r.IsCanonical && r.HttpCode != null))
                      continue;
                    var canonical = grp.FirstOrDefault(r => r.IsCanonical && r.HttpCode != null);
                    Element elem1 = new Element() { HttpCode = canonical.HttpCode, Language = canonical.Language, Priority = double.MaxValue, Target_sNode = canonical.Target_sNode, Target_Url = canonical.Target_Url };
                    elem1.SEO_Url = IKCMS_RouteUrlManager.GetMvcUrlGeneral(canonical.Target_sNode.Value);
                    if (!hashes_seo.Contains(elem1.GetHashCode_SEO()))
                      fakeElements.Add(elem1);
                    //Element elem2 = new Element() { HttpCode = canonical.HttpCode, Language = canonical.Language, Priority = double.MaxValue, Target_sNode = canonical.Target_sNode, Target_Url = canonical.Target_Url };
                    //elem2.SEO_Url = IKCMS_RouteUrlManager.GetMvcUrlGeneral(canonical.Target_sNode.Value) + "/" + Utility.UrlEncodeIndexPathForSEO(node.Data.vNode.name);
                  }
                  dataBuilder.Elements.AddRange(fakeElements);
                }
                //
                //var tmp = dataBuilder.Elements.ToDictionary(r => r.SEO_Url, r => r.GetHashCode_SEO());
                dataBuilder.Lookup_SEO = dataBuilder.Elements.ToLookup(r => r.GetHashCode_SEO());
                dataBuilder.Lookup_Target = dataBuilder.Elements.ToLookup(r => r.GetHashCode_Target());
                dataBuilder.Lookup_TargetOnly = dataBuilder.Elements.ToLookup(r => r.GetHashCode_TargetOnly());
                dataBuilder.Lookup_sNode = dataBuilder.Elements.Where(r => r.Target_sNode != null).ToLookup(r => r.Target_sNode.Value);
                dataBuilder.Lookup_rNode = dataBuilder.Elements.Where(r => r.Target_rNode != null).ToLookup(r => r.Target_rNode.Value);
                //
                data = dataBuilder;
              }
              if (data != null)
              {
                AggregateCacheDependency sqlDeps = new AggregateCacheDependency();
                sqlDeps.Add(new SqlCacheDependency("GDCS", "IKCMS_SEO"));
                HttpRuntime.Cache.Insert(keyName, data, sqlDeps, Cache.NoAbsoluteExpiration, Cache.NoSlidingExpiration, CacheItemPriority.High, (key, value, reason) => { (value as ElementsContainer).Clear(); });
              }
            }
          }
          catch { }
          return data;
        }
      }
    }


    public static XElement ExportXML() { return ExportXML(Utility.vPathMap(string.Format(dumpPath, "export"))); }
    public static XElement ExportXML(string fileNameToSave)
    {
      // accesso alle funzionalita' di import export solo da root e con connessioni locali
      if (!HttpContext.Current.Request.IsLocal || !HttpContext.Current.User.Identity.IsAuthenticated || HttpContext.Current.User.Identity.Name != "root")
        throw new UnauthorizedAccessException("Utilizzo di ExportXML() non autorizzato.");
      XElement xRoot = new XElement("IKCMS_SEO");
      xRoot.SetAttributeValue("clear", true);
      using (Ikon.Config.DataContext DB = Ikon.Config.DataContext.Factory())
      {
        var elements = DB.IKCMS_SEOs;
        foreach (var app_inst in elements.GroupBy(r => r.application).OrderBy(r => r.Key))
        {
          XElement xApp = new XElement("Application");
          xRoot.Add(xApp);
          xApp.SetAttributeValue("application", app_inst.Key);
          xApp.SetAttributeValue("clear", true);
          foreach (var elem in app_inst.OrderBy(r => r.SEO_url).ThenBy(r => r.priority).ThenBy(r => r.id))
          {
            XElement xUrl = new XElement("item");
            xApp.Add(xUrl);
            xUrl.SetAttributeValue("SEO_Url", elem.SEO_url);
            xUrl.SetAttributeValue("Target_sNode", elem.target_snode);
            xUrl.SetAttributeValue("Target_Url", elem.target_url);
            xUrl.SetAttributeValue("Language", elem.language);
            xUrl.SetAttributeValue("HttpCode", elem.http_code);
            xUrl.SetAttributeValue("Priority", elem.priority.ToString(System.Globalization.CultureInfo.InvariantCulture));
            // output solo nel caso sia differente dal default
            if (elem.active == false)
              xUrl.SetAttributeValue("active", elem.active);
            if (elem.canonical == true)
              xUrl.SetAttributeValue("canonical", elem.canonical);
          }
        }
      }
      try
      {
        if (!string.IsNullOrEmpty(fileNameToSave))
          xRoot.Save(fileNameToSave);
      }
      catch { }
      return xRoot;
    }


    public static bool ImportXML() { return ImportXML(Utility.vPathMap(string.Format(dumpPath, "import"))); }
    public static bool ImportXML(string fileNameToRead) { return ImportXML(Utility.FileReadXml(fileNameToRead)); }
    public static bool ImportXML(XElement xRoot)
    {
      // accesso alle funzionalita' di import export solo da root e con connessioni locali
      if (!HttpContext.Current.Request.IsLocal || !HttpContext.Current.User.Identity.IsAuthenticated || HttpContext.Current.User.Identity.Name != "root")
        throw new UnauthorizedAccessException("Utilizzo di ExportXML() non autorizzato.");
      try
      {
        if (xRoot == null || xRoot.Name.LocalName != "IKCMS_SEO")
          return false;
        lock (_lock)
        {
          using (Ikon.Config.DataContext DB = Ikon.Config.DataContext.Factory())
          {
            using (TransactionScope ts = IKGD_TransactionFactory.Transaction(null))
            {
              if (Utility.TryParse<bool>(xRoot.AttributeValue("clear"), false))
              {
                DB.IKCMS_SEOs.DeleteAllOnSubmit(DB.IKCMS_SEOs);
              }
              foreach (XElement xApp in xRoot.Elements("Application"))
              {
                string applicationName = xApp.AttributeValue("application");
                if (applicationName == null)
                  throw new Exception("Config Block with application==null");
                if (Utility.TryParse<bool>(xApp.AttributeValue("clear"), false))
                {
                  DB.IKCMS_SEOs.DeleteAllOnSubmit(DB.IKCMS_SEOs.Where(r => r.application == applicationName));
                }
                //
                double priorityMax = 0.0;
                try { priorityMax = DB.IKCMS_SEOs.Where(r => r.application == applicationName).Max(r => r.priority); }
                catch { }
                foreach (XElement xItem in xApp.Elements().Where(x => x.Name.LocalName.ToLower() == "item"))
                {
                  double priorityNew = Utility.TryParse<double>(xItem.AttributeValue("Priority"), priorityMax + 1.0);
                  priorityMax = Math.Max(priorityMax, priorityNew);
                  Ikon.Config.IKCMS_SEO item = new Ikon.Config.IKCMS_SEO { application = applicationName };
                  item.active = Utility.TryParse<bool>(xItem.AttributeValue("active"), true);
                  item.canonical = Utility.TryParse<bool>(xItem.AttributeValue("canonical"), false);
                  item.SEO_url = xItem.AttributeValue("SEO_Url");
                  item.target_snode = Utility.TryParse<int?>(xItem.AttributeValue("Target_sNode"), null);
                  item.target_url = xItem.AttributeValue("Target_Url");
                  item.language = xItem.AttributeValue("Language");
                  item.http_code = Utility.TryParse<int?>(xItem.AttributeValue("HttpCode"), null);
                  item.priority = priorityNew;
                  DB.IKCMS_SEOs.InsertOnSubmit(item);
                }
              }
              var chg = DB.GetChangeSet();
              DB.SubmitChanges();
              ts.Committ();
            }  // using transaction
          }  // using DB
        }  // lock
        Clear();
        return true;
      }
      catch (Exception ex)
      {
        HttpContext.Current.Trace.Write("Exception", ex.Message);
      }
      return false;
    }


    public class ElementsContainer
    {
      public List<Element> Elements { get; set; }
      public ILookup<int, Element> Lookup_SEO { get; set; }
      public ILookup<int, Element> Lookup_Target { get; set; }
      public ILookup<int, Element> Lookup_TargetOnly { get; set; }
      public ILookup<int, Element> Lookup_sNode { get; set; }
      public ILookup<int, Element> Lookup_rNode { get; set; }

      public void Clear()
      {
        Lookup_SEO = null;
        Lookup_Target = null;
        Lookup_TargetOnly = null;
        Lookup_sNode = null;
        Lookup_rNode = null;
        Elements.Clear();
        Elements = null;
      }
    }


    public class Element
    {
      public string SEO_Url { get; set; }
      public int? Target_sNode { get; set; }
      public int? Target_rNode { get; set; }
      public string Target_Url { get; set; }
      public bool IsCanonical { get; set; }
      public string Language { get; set; }
      public int? HttpCode { get; set; }
      public double Priority { get; set; }
      //

      public static int GetHashCode_Helper(string SEO_Url, int? Target_sNode, string Target_Url)
      {
        if (SEO_Url != null)
          return SEO_Url.ToLower().GetHashCode();
        //ordine di precedenza per il target: sNode, rNode, Url
        if (Target_sNode != null)
          return ("sNode_" + Target_sNode.Value.ToString()).GetHashCode();
        //if (Target_rNode != null)
        //  return ("rNode_" + Target_rNode.Value.ToString()).GetHashCode();
        return Target_Url.ToLower().GetHashCode();
      }

      public int GetHashCode_SEO() { return GetHashCode_Helper(SEO_Url, null, null); }
      public int GetHashCode_Target() { return GetHashCode_Helper(null, Target_sNode, Target_Url); }
      public int GetHashCode_TargetOnly() { return GetHashCode_Helper(Target_Url, null, null); }

      public override string ToString()
      {
        return string.Format("{0} [sNode={1}][rNode={2}] [can:{3}] [http:{4}] [priority:{5}]", SEO_Url, Target_sNode, Target_rNode, IsCanonical, HttpCode, Priority);
      }

    }

  }

}
