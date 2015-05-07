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
using LinqKit;
using Autofac;

using Ikon;
using Ikon.GD;
using Ikon.IKCMS.Library.Resources;
using Ikon.IKGD.Library.Resources;
using Ikon.IKGD.Library;


namespace Ikon.IKCMS
{

  public static class SiteMapHelper
  {
    public static FS_Operations fsOp { get { return IKCMS_ManagerIoC.requestContainer.ResolveNamed<FS_Operations>("readonly"); } }
    //public static FS_Operations fsOp { get { return IKCMS_ManagerIoC.requestContainer.Resolve<FS_Operations>(); } }


    //
    // gestione del sitemode
    //
    public static bool SiteMapXml(HttpResponseBase Response, string language, string siteMode)
    {
      try
      {
        using (IKGD_ExternalVFS_Support shFS = new IKGD_ExternalVFS_Support(IKGD_Config.AppSettings["SharePath_ExternalVFS"] ?? IKGD_Config.AppSettings["SharePath_Lucene"] ?? "~/App_Data/ExternalVFS"))
        {
          var frags = new List<string> { siteMode, language }.Where(s => s.IsNotEmpty()).Distinct(StringComparer.OrdinalIgnoreCase).Concat(Enumerable.Repeat(string.Empty, 1)).ToList();
          for (int idx = 0; idx < frags.Count; idx++)
          {
            string middle = Utility.Implode(frags.Skip(idx), ".");
            string fName = "Sitemap.{0}xml".FormatString(middle);
            if (shFS.FileExists(fName))
            {
              Response.ContentType = "text/xml";
              Response.ContentEncoding = Encoding.UTF8;
              if (shFS.DownloadExternalStream(System.Web.HttpContext.Current.Response, fName))
              {
                Response.Flush();
                Response.End();
              }
            }
          }
          //
          //string fName = language.IsNullOrEmpty() ? "Sitemap.xml" : "Sitemap.{0}.xml".FormatString(language);
          //if (shFS.FileExists(fName))
          //{
          //  Response.ContentType = "text/xml";
          //  Response.ContentEncoding = Encoding.UTF8;
          //  if (shFS.DownloadExternalStream(System.Web.HttpContext.Current.Response, fName))
          //  {
          //    Response.Flush();
          //    Response.End();
          //  }
          //}
          //string fName = shFS.ResolveFileName("Sitemap.xml");
          //if (fName.IsNotNullOrWhiteSpace() && System.IO.File.Exists(fName))
          //{
          //  return File(fName, "text/xml");
          //}
          return true;
        }
      }
      catch { }
      return false;
    }


    //
    // generatore automatizzato delle Sitemap.xml a partire dai contenuti del CMS
    //
    public static XElement SiteMapBuild(string siteModeForcedSite, string languageFilter)
    {
      string xSitemapNS = "http://www.sitemaps.org/schemas/sitemap/0.9";
      XElement xSitemap = new XElement(XName.Get("urlset", xSitemapNS));
      try
      {
        //
        int maxRecursionLevel = Utility.TryParse<int>(IKGD_Config.AppSettings["IKGD_Path_MaxRecursionLevels"], 25);
        string forcedAppVirtualPath = IKGD_Config.AppSettings["SiteMapXml_ForcedUrlBase"].NullIfEmpty() ?? new Uri(HttpContext.Current.Request.Url, Utility.ResolveUrl("~/")).ToString();
        var forcedAppVirtualPaths = new Utility.DictionaryMV<string, string>() { { string.Empty, forcedAppVirtualPath } };
        var languages = IKGD_Language_Provider.Provider.LanguagesAvailable().ToList();
        languages.ForEach(r => forcedAppVirtualPaths.Add(r, IKGD_Config.AppSettings["SiteMapXml_ForcedUrlBase_{0}".FormatString(r)].TrimSafe().NullIfEmpty()));
        //
        using (FS_Operations fsOp = new FS_Operations(0, true, true, true))
        {
          Expression<Func<IKGD_VNODE, bool>> vNodeFilter = PredicateBuilder.True<IKGD_VNODE>();
          if (languageFilter != null)
            vNodeFilter = vNodeFilter.And(n => n.language == null || string.Equals(n.language, languageFilter));
          var fsNodesAll =
            (from vNode in fsOp.NodesActive<IKGD_VNODE>().Where(vNodeFilter)
             from vData in fsOp.NodesActive<IKGD_VDATA>().Where(n => n.rnode == vNode.rnode)
             from iNode in fsOp.NodesActive<IKGD_INODE>().Where(n => n.rnode == vNode.rnode).DefaultIfEmpty()
             select new FS_Operations.FS_TreeNode<IKCMS_SiteMapNode>(null, new IKCMS_SiteMapNode { sNode = vNode.snode, rNode = vNode.rnode, parentNode = vNode.parent ?? vNode.folder, Name = vNode.name, Language = vNode.language, ManagerType = vData.manager_type, Category = vData.category, Flags = (FlagsMenuEnum)vData.flags_menu, Date_vNode = vNode.version_date, Date_vData = vData.version_date, Date_iNode = (iNode == null) ? null : (DateTime?)iNode.version_date })).ToList();
          //
          var roots = IKGD_ConfigVFS.ConfigExt.RootsCMS_folders;
          var rootNode = new FS_Operations.FS_TreeNode<IKCMS_SiteMapNode>(null, null);
          fsNodesAll.Where(r => roots.Contains(r.Data.rNode)).ForEach(r => r.Parent = rootNode);
          //
          for (int iter = 0; iter < maxRecursionLevel; iter++)
          {
            int reparented = 0;
            fsNodesAll.Where(r => r.Parent == null).Join(fsNodesAll, r => r.Data.parentNode, r => r.Data.rNode, (node, parent) => new { node, parent }).ForEach(r => { r.node.Parent = r.parent; reparented++; });
            if (reparented == 0)
              break;
          }
          //
          // valutatore ricorsivo della validita' del path per la lingua
          //Func<FS_Operations.FS_TreeNode<SiteMapNode>, string, bool> IsPathValid = null;
          //IsPathValid = (node, lang) => { return (node.Data != null && (lang == null || node.Data.Language == null || node.Data.Language == lang)) && (node.Parent == null || IsPathValid(node.Parent, node.Data.Language ?? lang)); };
          //
          // selezione delle leafs e test della validita' dei path
          for (int iter = 0; iter < maxRecursionLevel; iter++)
          {
            int purged = 0;
            rootNode.RecurseOnTree.Skip(1).Where(r => !r.Nodes.Any()).Where(r => r.BackRecurseOnData.Where(d => d != null && d.Language != null).Select(d => d.Language).Distinct().Count() > 1).Reverse().ForEach(r => { r.Data = null; r.Parent = null; purged++; });
            if (purged == 0)
              break;
          }
          //
          rootNode.RecurseOnTree.Skip(1).Where(r => r.Data == null).Reverse().ForEach(r => r.Parent = null);
          //
          // populating with SEO urls when availables
          rootNode.RecurseOnData.Skip(1).Where(r => r != null && r.Url.IsNullOrWhiteSpace()).ForEach(r => r.Url = IKGD_SEO_Manager.MapOutcomingUrl(r.sNode, r.rNode, languageFilter));
          //
          // populating with IKCMS_ResourceType_PageStatic
          try
          {
            var pageTypeNames = IKCMS_RegisteredTypes.Types_IKCMS_ResourceWithUrl_Interface.Where(t => t.IsAssignableTo(typeof(IKCMS_ResourceType_PageStatic))).Select(t => t.Name).Distinct().ToList();
            var nodesExternal = rootNode.RecurseOnData.Skip(1).Where(r => r != null && r.Url.IsNullOrWhiteSpace() && pageTypeNames.Contains(r.ManagerType)).ToList();
            var rNodes = nodesExternal.Select(r => r.rNode).Distinct().ToList();
            foreach (var slice in rNodes.Slice(100))
            {
              var vDatas = fsOp.NodesActive<IKGD_VDATA>().Where(n => slice.Contains(n.rnode));
              foreach (var vData in vDatas)
              {
                string url = (IKCMS_RegisteredTypes.Deserialize_IKCMS_ResourceVFS(vData) as IKCMS_ResourceType_PageStatic).ResourceSettings.UrlExternal;
                if (url.IsNotNullOrWhiteSpace())
                {
                  nodesExternal.Where(r => r.rNode == vData.rnode).ForEach(r => r.Url = url);
                }
              }
            }
          }
          catch { }
          //
          // customizzazione delle sitemap con codice custom per funzionalita' extra CMS
          //
          if (IKGD_Config.AppSettings["SiteMapHelper_CustomProcessor"].IsNotEmpty())
          {
            try
            {
              Type ty = Utility.FindTypeCachedExt(IKGD_Config.AppSettings["SiteMapHelper_CustomProcessor"]);
              if (ty != null && ty.GetInterfaces().Any(t => t == typeof(SiteMapHelper_CustomProcessor_Interface)))
              {
                SiteMapHelper_CustomProcessor_Interface processor = Activator.CreateInstance(ty) as SiteMapHelper_CustomProcessor_Interface;
                if (processor != null)
                {
                  int nodesCount = processor.BuildNodes(rootNode, fsOp, forcedAppVirtualPaths);
                }
              }
            }
            catch { }
          }
          //
          // normalizzazione delle Url su url assolute
          {
            //
            // per non mappare una risorsa custom basta associare l'interface IKCMS_ResourceWithOutUrl_Interface alla risorsa CMS
            //
            var pageTypes = IKCMS_RegisteredTypes.Types_IKCMS_ResourceWithUrl_Interface.Select(t => t.Name).Distinct().ToList();
            var resourceSet = rootNode.RecurseOnData.Skip(1).Where(r => r != null && r.Url.IsNotNullOrWhiteSpace()).GroupBy(r => new { r.ManagerType, r.Category }).Select(g => new { g.Key.ManagerType, g.Key.Category }).ToList();
            //
            // TODO
            // abbiamo dei problemi con le configurazioni prodotti che vengono mappate su url senza averne
            // si potrebbero utilizzare i mapping dei templates, magari con un'estensione NoPage=true nel config.xml
            // verificare se vengono mappati gli eventi e le news oppure i pacchetti per TFVG
            // aggiungere il supporto per la customizzazione dei filtri:
            // - supporto per altre risorse tipo immagini o bricks particolari (magari bricks con una lista di categories...)
            // - supporto per filtri sui flags (es. pagine disabilitate nel menu, ...)
            //
            string languageFirst = languages.FirstOrDefault();
            // per mantenere la lista dei nodi senza lingua da duplicare su url con lingua
            List<FS_Operations.FS_TreeNode<IKCMS_SiteMapNode>> nodesWithNoLanguage = new List<FS_Operations.FS_TreeNode<IKCMS_SiteMapNode>>();
            rootNode.RecurseOnTree.Skip(1).Where(r => r.Data != null && pageTypes.Contains(r.Data.ManagerType) && r.Data.Url.IsNullOrWhiteSpace()).ForEach(r =>
            {
              string langRec = r.BackRecurseOnData.Where(n => n != null && n.Language != null).Select(n => n.Language).FirstOrDefault().NullIfEmpty();
              if (langRec == null)
              {
                nodesWithNoLanguage.Add(r);
                return;
              }
              r.Data.Url = IKCMS_TreeStructureVFS.UrlFormatterWorkerBase(r.Data.sNode, r.Data.rNode, langRec, r.Data.Name, r.Data.Flags);
            });
            //processing dei nodi da duplicare per generare url con lingua sui nodi senza lingua associata
            if (nodesWithNoLanguage.Any())
            {
              foreach (var node in nodesWithNoLanguage)
              {
                foreach (var langRec in languages)
                {
                  FS_Operations.FS_TreeNode<IKCMS_SiteMapNode> nodeNew = new FS_Operations.FS_TreeNode<IKCMS_SiteMapNode>(node.Parent, new IKCMS_SiteMapNode(node.Data));
                  nodeNew.Data.Url = IKCMS_TreeStructureVFS.UrlFormatterWorkerBase(node.Data.sNode, node.Data.rNode, langRec, node.Data.Name, node.Data.Flags);
                }
              }
            }
          }
          //
          // normalizzazione delle Url su url assolute
          rootNode.RecurseOnData.Skip(1).Where(r => r != null && r.Url.IsNotNullOrWhiteSpace()).ForEach(r =>
          {
            try { r.Url = Utility.ResolveUrl(r.Url, forcedAppVirtualPaths[r.Language ?? string.Empty] ?? forcedAppVirtualPath); }
            catch { }
          });
          //
          // generazione della sitemap.xml
          //rootNode.RecurseOnTree.Skip(1).Where(r => r.Data != null && r.Data.Url.IsNotNullOrWhiteSpace()).OrderBy(r => languages.IndexOfSortable(r.Data.Language)).ThenBy(r => r.Data.Url).Distinct((r1, r2) => string.Equals(r1.Data.Url, r2.Data.Url, StringComparison.OrdinalIgnoreCase)).ForEach(r =>
          rootNode.RecurseOnTree.Skip(1).Where(r => r.Data != null && r.Data.Url.IsNotNullOrWhiteSpace()).Distinct((r1, r2) => string.Equals(r1.Data.Url, r2.Data.Url, StringComparison.OrdinalIgnoreCase)).ForEach(r =>
          {
            DateTime lastMod = (new DateTime[] { r.Data.LastUpdate }).Concat(r.Nodes.Where(n => n.Data != null).Select(n => n.Data.LastUpdate)).Max();
            XElement xUrl = new XElement(XName.Get("url", xSitemapNS),
              new XElement(XName.Get("loc", xSitemapNS), r.Data.Url.TrimSafe()),
              new XElement(XName.Get("lastmod", xSitemapNS), lastMod.ToUniversalTime().ToString("s") + "Z"));
            xSitemap.Add(xUrl);
          });
          //
          if (HttpContext.Current.IsDebuggingEnabled)
          {
            //var urls = rootNode.RecurseOnData.Skip(1).Where(r => r != null && r.Url.IsNotNullOrWhiteSpace()).Select(r => r.Url).Distinct().ToList();
            //var items = rootNode.RecurseOnData.Skip(1).Where(r => r != null && r.Url.IsNullOrWhiteSpace()).ToList();
            var report = rootNode.RecurseOnData.Skip(1).Where(r => r != null && r.Url.IsNotNullOrWhiteSpace()).GroupBy(r => new { r.ManagerType, r.Category }).Select(g => new { g.Key.ManagerType, g.Key.Category, count = g.Count() }).ToList();
          }
        }
        //
        return xSitemap;
      }
      catch { throw; }
    }





    public class IKCMS_SiteMapNode
    {
      public int sNode { get; set; }
      public int rNode { get; set; }
      public int parentNode { get; set; }
      public string Name { get; set; }
      public string Language { get; set; }
      public string ManagerType { get; set; }
      public string Category { get; set; }
      public string Url { get; set; }
      public FlagsMenuEnum Flags { get; set; }
      public DateTime Date_vNode { get; set; }
      public DateTime Date_vData { get; set; }
      public DateTime? Date_iNode { get; set; }
      //
      public DateTime LastUpdate { get { return Date_iNode != null ? Utility.MaxAll(Date_vNode, Date_vData, Date_iNode.Value) : Utility.Max(Date_vNode, Date_vData); } }
      //

      public IKCMS_SiteMapNode() { }

      public IKCMS_SiteMapNode(IKCMS_SiteMapNode node)
      {
        if (node != null)
        {
          sNode = node.sNode;
          rNode = node.rNode;
          parentNode = node.parentNode;
          Name = node.Name;
          Language = node.Language;
          ManagerType = node.ManagerType;
          Category = node.Category;
          Url = node.Url;
          Flags = node.Flags;
          Date_vNode = node.Date_vNode;
          Date_vData = node.Date_vData;
          Date_iNode = node.Date_iNode;
        }
      }
    }


    public interface SiteMapHelper_CustomProcessor_Interface
    {
      int BuildNodes(FS_Operations.FS_TreeNode<SiteMapHelper.IKCMS_SiteMapNode> rootNode, FS_Operations fsOp, Utility.DictionaryMV<string, string> forcedAppVirtualPaths);
    }


  }
}
