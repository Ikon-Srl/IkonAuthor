using System;
using System.Collections;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Linq;
using System.Web;
using System.Web.Security;
using System.Web.Configuration;
using System.Web.UI;
using System.Web.UI.HtmlControls;
using System.Web.UI.WebControls;
using System.Web.UI.WebControls.WebParts;
using System.Web.Caching;
using System.Xml.Linq;
using System.Linq.Expressions;
using System.Runtime.Serialization.Json;
using System.IO;
using System.Text;
using System.Web.Mvc;
using System.Web.Mvc.Ajax;
using System.ServiceModel.Syndication;
using LinqKit;

using Ikon;
using Ikon.Support;
using Ikon.GD;
using Ikon.IKCMS;
using Ikon.IKGD.Library.Resources;
using Ikon.IKCMS.Library.Resources;
using Ikon.IKGD.Library.Collectors;


namespace Ikon.IKCMS
{

  [Microsoft.Web.Mvc.ControllerSessionState(Microsoft.Web.Mvc.ControllerSessionState.ReadOnly)]
  //public class RSS_GeneratorController : VFS_Access_Controller
  public class RSS_GeneratorController : AutoStaticCMS_Controller
  {

    public ActionResult Index()
    {
      ViewData["pathsVFS"] = GetAvailableFeeds();
      return View();
    }


    public ActionResult Mini(int? moduleCode, int? maxItems, bool? linkToRelations)
    {
      return Full(moduleCode, maxItems, linkToRelations);
    }


    public ActionResult Full(int? moduleCode, int? maxItems, bool? linkToRelations)
    {
      try
      {
        if (moduleCode.GetValueOrDefault(0) > 0)
        {
          if (maxItems.GetValueOrDefault(0) <= 0)
            maxItems = 50;
          //
          string contextCacheKey = FS_OperationsHelpers.ContextHash(this.GetType(), moduleCode, maxItems, linkToRelations);
          string feedStr = FS_OperationsHelpers.CachedEntityWrapper(contextCacheKey, () =>
          {
            RSSFeedGenerator feedModule = new RSSFeedGenerator();
            bool res01 = feedModule.BuildFeedRSS(moduleCode, null, maxItems, linkToRelations);
            return feedModule.GetFeedRSS();
          }, 3600, FS_OperationsHelpers.Const_CacheDependencyIKGD_vNode_vData);
          //
          return this.Content(feedStr, "application/xml", Encoding.UTF8);
        }
      }
      catch { }
      return null;
    }



    [NonAction]
    public List<LinkData> GetAvailableFeeds()
    {
      List<string> browsableTypes = IKCMS_RegisteredTypes.Types_IKCMS_BrowsableModule_Interface.Select(t => t.Name).ToList();
      using (FS_Operations fsOp = new FS_Operations())
      {
        List<int> roots = IKGD_ConfigVFS.Config.RootsCMS_sNodes;
        var fsNodes = fsOp.Get_NodesInfoFiltered(null, vd => browsableTypes.Contains(vd.manager_type), false, FS_Operations.FilterVFS.ACL | FS_Operations.FilterVFS.Language).ToList();
        var paths = fsOp.PathsFromNodesExt(fsNodes.Select(n => n.vNode.snode)).FilterPathsByRootsVFS();
        List<LinkData> links = paths.Select(p => new LinkData { Path = p, Text = p.Path, Url = IKCMS_RouteUrlManager.GetMvcActionUrl<RSS_GeneratorController>(c => c.FeedAtom(p.sNode, null, null, null)) }).ToList();
        foreach (var link in links)
        {
          if (!link.Path.Fragments.Any(f => roots.Contains(f.sNode)))
          {
            link.Url = Utility.UriSetQuery(link.Url, "linkToRelations", "true");
            link.Text += " [extra Menù]";
          }
          else
          {
            //link.Path.Fragments.Skip(link.Path.Fragments.FindLastIndex(f=>roots.Contains(f.sNode)))
            link.Text = "/" + string.Join("/", link.Path.Fragments.Skip(link.Path.Fragments.FindLastIndex(f => roots.Contains(f.sNode)) + 1).Select(f => f.Name).ToArray());
          }
          try
          {
            var fsNode = fsOp.Get_NodeInfo(link.Path.sNode, false);
            IKCMS_HasSerializationCMS_Interface data = IKCMS_RegisteredTypes.Deserialize_IKCMS_ResourceVFS(fsNode);
            string TitleMenu = fsNode.vNode.name;
            string TitleHead = Utility.FindPropertySafe<string>(data.ResourceSettingsObject, "TitleHead");
            string TitleH1 = Utility.FindPropertySafe<string>(data.ResourceSettingsObject, "TitleH1");
            if (data is IKCMS_HasPropertiesLanguageKVT_Interface)
            {
              TitleHead = (data as IKCMS_HasPropertiesLanguageKVT_Interface).ResourceSettingsKVT.KeyFilterTry(fsNode.Language, "TitleHead").ValueString;
              TitleH1 = (data as IKCMS_HasPropertiesLanguageKVT_Interface).ResourceSettingsKVT.KeyFilterTry(fsNode.Language, "TitleH1").ValueString;
            }
            else if (data is IKCMS_HasPropertiesKVT_Interface)
            {
              TitleHead = (data as IKCMS_HasPropertiesKVT_Interface).ResourceSettingsKVT["TitleHead"].ValueString;
              TitleH1 = (data as IKCMS_HasPropertiesKVT_Interface).ResourceSettingsKVT["TitleH1"].ValueString;
            }
            string Title = TitleH1 ?? TitleHead ?? TitleMenu;
            link.Text = Title;
          }
          catch { }
        }
        return links;
      }
    }


    //
    // supporto per la generazione del feed RSS
    //
    public ActionResult FeedAtom(int? moduleCode, string modulePath, int? maxItems, string viewName)
    {
      try
      {
        //
        if (maxItems.GetValueOrDefault(0) <= 0)
          maxItems = 50;
        string itemSparkViewPath = viewName ?? "~/Views/RSS_Generator/ItemRSS";
        string feedStr = null;
        //
        if (Utility.TryParse<bool>(IKGD_Config.AppSettingsWeb["CachingIKCMS_FeedsEnabled"], true))
        {
          string contextCacheKey = FS_OperationsHelpers.ContextHash(this.GetType(), moduleCode, modulePath, maxItems, viewName);
          feedStr = FS_OperationsHelpers.CachedEntityWrapper(contextCacheKey, () =>
          {
            return FeedAtomWorker(moduleCode, modulePath, maxItems.GetValueOrDefault(), itemSparkViewPath);
          }, 3600, FS_OperationsHelpers.Const_CacheDependencyIKGD_vNode_vData_iNode);
        }
        else
        {
          feedStr = FeedAtomWorker(moduleCode, modulePath, maxItems.GetValueOrDefault(), itemSparkViewPath);
        }
        //
        return this.Content(feedStr, "application/xml", Encoding.UTF8);
      }
      catch { }
      return null;
    }


    protected string FeedAtomWorker(int? moduleCode, string modulePath, int maxItems, string itemSparkViewPath)
    {
      IKGD_Path path = null;
      if (path == null && moduleCode != null)
        path = fsOp.PathsFromNodeExt(moduleCode.Value).FirstOrDefault();
      if (path == null && !string.IsNullOrEmpty(modulePath))
        path = fsOp.PathsFromString(modulePath).FirstOrDefault();
      RSSFeedGenerator feedModule = new RSSFeedGenerator();
      bool resultOp = feedModule.BuildFeedRSS_External(path.sNode, null, maxItems, false, this.ControllerContext, itemSparkViewPath);
      string feedStr = feedModule.GetFeedAtom();
      return feedStr;
    }



    public class LinkData
    {
      public string Url { get; set; }
      public string Text { get; set; }
      public IKGD_Path Path { get; set; }
    }


  }

}
