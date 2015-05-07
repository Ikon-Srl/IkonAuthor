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
using System.Transactions;
using System.Xml;
using System.Reflection;
using System.ServiceModel.Syndication;
using LinqKit;

using Ikon;
using Ikon.Support;
using Ikon.Log;
using Ikon.GD;
using Ikon.IKCMS;
using Ikon.IKGD.Library.Resources;
using Ikon.IKCMS.Library;
using Ikon.IKCMS.Library.Resources;
using Ikon.IKGD.Library.Collectors;


namespace Ikon.IKCMS
{

  public class RSSFeedGenerator
  {
    public SyndicationFeed FeedRSS { get; protected set; }
    public List<SyndicationItem> FeedItems { get; protected set; }


    public RSSFeedGenerator()
    {
      FeedItems = new List<SyndicationItem>();
      FeedRSS = new SyndicationFeed();
      //
      FeedRSS.Language = "it-IT";
      FeedRSS.Generator = "Ikon RSS Wizard";
      FeedRSS.Id = HttpContext.Current.Request.Url.ToString();
      //
      FeedRSS.Copyright = SyndicationContent.CreatePlaintextContent("Copyright (C) 2010 Ikon S.r.l.");
      FeedRSS.Authors.Add(new SyndicationPerson("info@ikon.it", "Ikon S.r.l.", "http://www.ikon.it/"));
      FeedRSS.BaseUri = new Uri(HttpContext.Current.Request.Url, VirtualPathUtility.ToAbsolute("~/"));
      FeedRSS.ImageUrl = new Uri(HttpContext.Current.Request.Url, VirtualPathUtility.ToAbsolute("~/Content/images/logo.jpg"));
      FeedRSS.LastUpdatedTime = new DateTimeOffset(Utility.DateTimeMinValueDB);
      FeedRSS.Title = SyndicationContent.CreatePlaintextContent("Titolo del Feed RSS");
      FeedRSS.Description = SyndicationContent.CreatePlaintextContent("Descrizione del Feed RSS");
      //
    }


    public string GetFeedRSS()
    {
      FeedRSS.Items = FeedItems;
      //
      XmlWriterSettings xws = new XmlWriterSettings();
      xws.Indent = false;
      xws.Encoding = Encoding.UTF8;
      //
      string feedString = string.Empty;
      using (MemoryStream ms = new MemoryStream())
      {
        using (XmlWriter wr = XmlWriter.Create(ms, xws))
        {
          FeedRSS.SaveAsRss20(wr);
        }
        feedString = Encoding.UTF8.GetString(ms.ToArray());
      }
      //pulizia del BOM
      if (feedString.IndexOf('<') > 0)
        feedString = feedString.Substring(feedString.IndexOf('<'));
      //try { feedString = XElement.Parse(feedString).ToString(); }
      //catch { }
      //
      return feedString;
    }


    public string GetFeedAtom()
    {
      FeedRSS.Items = FeedItems;
      //
      XmlWriterSettings xws = new XmlWriterSettings();
      xws.Indent = false;
      xws.Encoding = Encoding.UTF8;
      //
      string feedString = string.Empty;
      using (MemoryStream ms = new MemoryStream())
      {
        using (XmlWriter wr = XmlWriter.Create(ms, xws))
        {
          FeedRSS.SaveAsAtom10(wr);
        }
        feedString = Encoding.UTF8.GetString(ms.ToArray());
      }
      //pulizia del BOM
      if (feedString.IndexOf('<') > 0)
        feedString = feedString.Substring(feedString.IndexOf('<'));
      try { feedString = XElement.Parse(feedString).ToString(); }
      catch { }
      //
      return feedString;
    }


    public SyndicationItem AddItem(SyndicationItem feedItem)
    {
      if (feedItem != null)
      {
        foreach (string cat in feedItem.Categories.Select(c => c.Name).Except(FeedRSS.Categories.Select(c => c.Name)))
          FeedRSS.Categories.Add(new SyndicationCategory(cat));
        if (feedItem.LastUpdatedTime > FeedRSS.LastUpdatedTime)
          FeedRSS.LastUpdatedTime = feedItem.LastUpdatedTime;
        FeedItems.Add(feedItem);
      }
      return feedItem;
    }

    public SyndicationItem AddItem(object id, string title, string summary, string content, DateTime? publishDate, DateTime? lastUpdatedTime, string permalink, object categories)
    {
      SyndicationItem feedItem = new SyndicationItem();
      try
      {
        feedItem.Id = id.ToString();
        feedItem.Title = new TextSyndicationContent(title);
        feedItem.Summary = SyndicationContent.CreateXhtmlContent(summary);
        feedItem.Content = SyndicationContent.CreateXhtmlContent(content);
        feedItem.PublishDate = new DateTimeOffset(publishDate ?? DateTime.Now);
        feedItem.LastUpdatedTime = new DateTimeOffset(lastUpdatedTime ?? DateTime.Now);
        feedItem.AddPermalink(new Uri(HttpContext.Current.Request.Url, Utility.ResolveUrl(permalink)));
        if ((IEnumerable<string>)categories != null)
        {
          foreach (string cat in (IEnumerable<string>)categories)
            feedItem.Categories.Add(new SyndicationCategory(cat));
        }
        else if (categories != null && typeof(string).IsAssignableFrom(categories.GetType()))
        {
          feedItem.Categories.Add(new SyndicationCategory((string)categories));
        }
        AddItem(feedItem);
      }
      catch { return null; }
      return feedItem;
    }


    //
    // todo: leggere url item dai links
    // attaccare a routing
    //
    public bool BuildFeedRSS(int? sNode_module, string pathModule, int? maxItemsCount, bool? linkToRelations)
    {
      FS_Operations.FS_NodeInfo fsNode_Module;
      IKGD_Path vfsPathModule;
      List<int> sNodeRoots;
      IKCMS_HasSerializationCMS_Interface moduleInfo;
      //
      maxItemsCount = maxItemsCount ?? 50;
      //
      using (FS_Operations fsOp = new FS_Operations())
      {
        if (sNode_module != null)
          vfsPathModule = fsOp.PathsFromNodeExt(sNode_module.Value).FirstOrDefault();
        else
          vfsPathModule = fsOp.PathsFromString(pathModule, true).FirstOrDefault();
        if (vfsPathModule == null)
          return false;
        fsNode_Module = fsOp.Get_NodeInfoACL(vfsPathModule.sNode, false, false);
        sNodeRoots = fsOp.Get_RELATIONs(fsNode_Module.vNode).Where(r => r.type == Ikon.IKGD.Library.IKGD_Constants.IKGD_ArchiveRelationName).OrderBy(r => r.position).Select(r => r.snode_dst).ToList();
        //
        moduleInfo = IKCMS_RegisteredTypes.Deserialize_IKCMS_ResourceVFS(fsNode_Module);
        object moduleInfoSettings = moduleInfo.ResourceSettingsObject;
        //string archiveFilterType = Utility.FindPropertySafe<string>(moduleInfoSettings, "ArchiveFilterType");
        //string archiveCollectorType = Utility.FindPropertySafe<string>(moduleInfoSettings, "ArchiveCollectorType");
        //
        var author = FeedRSS.Authors.FirstOrDefault();
        string TitleHead = Utility.FindPropertySafe<string>(moduleInfoSettings, "TitleHead");
        string TitleH1 = Utility.FindPropertySafe<string>(moduleInfoSettings, "TitleH1");
        if (moduleInfo is IKCMS_HasPropertiesLanguageKVT_Interface)
        {
          TitleHead = (moduleInfo as IKCMS_HasPropertiesLanguageKVT_Interface).ResourceSettingsKVT.KeyFilterTry(fsNode_Module.Language, "TitleHead").ValueString;
          TitleH1 = (moduleInfo as IKCMS_HasPropertiesLanguageKVT_Interface).ResourceSettingsKVT.KeyFilterTry(fsNode_Module.Language, "TitleH1").ValueString;
          //
          author.Name = (moduleInfo as IKCMS_HasPropertiesLanguageKVT_Interface).ResourceSettingsKVT.KeyFilterTry(fsNode_Module.Language, "FeedAuthorName").ValueString ?? author.Name;
          author.Uri = (moduleInfo as IKCMS_HasPropertiesLanguageKVT_Interface).ResourceSettingsKVT.KeyFilterTry(fsNode_Module.Language, "FeedAuthorUrl").ValueString ?? author.Uri;
          author.Email = (moduleInfo as IKCMS_HasPropertiesLanguageKVT_Interface).ResourceSettingsKVT.KeyFilterTry(fsNode_Module.Language, "FeedAuthorEmail").ValueString ?? author.Email;
        }
        else if (moduleInfo is IKCMS_HasPropertiesKVT_Interface)
        {
          TitleHead = (moduleInfo as IKCMS_HasPropertiesKVT_Interface).ResourceSettingsKVT["TitleHead"].ValueString;
          TitleH1 = (moduleInfo as IKCMS_HasPropertiesKVT_Interface).ResourceSettingsKVT["TitleH1"].ValueString;
          //
          author.Name = (moduleInfo as IKCMS_HasPropertiesKVT_Interface).ResourceSettingsKVT["FeedAuthorName"].ValueString ?? author.Name;
          author.Uri = (moduleInfo as IKCMS_HasPropertiesKVT_Interface).ResourceSettingsKVT["FeedAuthorUrl"].ValueString ?? author.Uri;
          author.Email = (moduleInfo as IKCMS_HasPropertiesKVT_Interface).ResourceSettingsKVT["FeedAuthorEmail"].ValueString ?? author.Email;
        }
        TitleHead = TitleHead ?? TitleH1 ?? fsNode_Module.vNode.name;
        TitleH1 = TitleH1 ?? TitleHead ?? fsNode_Module.vNode.name;
        //
        FeedRSS.Title = SyndicationContent.CreatePlaintextContent(TitleHead);
        FeedRSS.Description = SyndicationContent.CreatePlaintextContent(TitleH1);
        //
        FeedRSS.Authors.Add(new SyndicationPerson("info@ikon.it", "Ikon S.r.l.", "http://www.ikon.it/"));

        //

        int? maxRecursionLevel = null;
        FS_Operations.FS_TreeNode<IKCMS_TreeBrowser_fsNodeElement_Interface> nodesTree = fsOp.Get_TreeDataShortGeneric<IKCMS_TreeBrowser_fsNodeElement_Interface>(null, sNodeRoots, null, null, maxRecursionLevel, true, true);
        List<int> folderSet = nodesTree.RecurseOnTree.Where(n => n.Data != null).Select(n => n.Data.vNode.folder).Distinct().ToList();
        //Dictionary<int, string> folderNames = nodesTree.RecurseOnTree.Where(n => n.Data != null).ToDictionary(n => n.Data.vNode.folder, n => n.Data.vNode.name);
        ILookup<int, string> folderNames = nodesTree.RecurseOnTree.Where(n => n.Data != null).ToLookup(n => n.Data.vNode.folder, n => n.Data.vNode.name);
        //
        Expression<Func<IKGD_VDATA, bool>> vDataFilterAll = fsOp.Get_vDataFilterACLv2();
        Expression<Func<IKGD_VNODE, bool>> vNodeFilterAll = fsOp.Get_vNodeFilterACLv2();
        vNodeFilterAll = vNodeFilterAll.And(n => n.flag_folder == false);
        vNodeFilterAll = vNodeFilterAll.And(n => folderSet.Contains(n.folder));
        //
        IQueryable<FS_Operations.FS_NodeInfoExt> resources =
          from vNode in fsOp.NodesActive<IKGD_VNODE>().Where(vNodeFilterAll)
          from vData in fsOp.NodesActive<IKGD_VDATA>().Where(vDataFilterAll).Where(n => n.rnode == vNode.rnode)
          select new FS_Operations.FS_NodeInfoExt
          {
            vNode = vNode,
            vData = vData,
            Relations = fsOp.NodesActive<IKGD_RELATION>().Where(r => r.rnode == vNode.rnode).ToList()
          };
        //
        resources = resources.OrderByDescending(n => (n.vNode.version_date >= n.vData.version_date) ? n.vNode.version_date : n.vData.version_date).ThenByDescending(n => n.vNode.snode);
        //resources = resources.OrderByDescending(n => n.vNode.version_date).ThenByDescending(n => n.vNode.snode);
        //
        resources = resources.Take(maxItemsCount.Value);
        //
        resources.ForEach(item =>
        {
          SyndicationItem feedItem = new SyndicationItem();
          //
          IKCMS_HasSerializationCMS_Interface itemData = IKCMS_RegisteredTypes.Deserialize_IKCMS_ResourceVFS(item);
          object itemSettings = itemData.ResourceSettingsObject;
          //
          feedItem.Id = item.vNode.snode.ToString();
          feedItem.Title = SyndicationContent.CreatePlaintextContent(Utility.FindPropertySafe<string>(itemSettings, "Title"));
          feedItem.Summary = SyndicationContent.CreateHtmlContent(Utility.FindPropertySafe<string>(itemSettings, "Abstract"));
          feedItem.Content = SyndicationContent.CreateHtmlContent(Utility.FindPropertySafe<string>(itemSettings, "Text"));
          feedItem.PublishDate = new DateTimeOffset(item.vData.date_node);
          feedItem.LastUpdatedTime = new DateTimeOffset((item.vNode.version_date >= item.vData.version_date) ? item.vNode.version_date : item.vData.version_date);
          if (folderNames.Contains(item.vNode.folder))
            feedItem.Categories.Add(new SyndicationCategory(folderNames[item.vNode.folder].FirstOrDefault()));
          //
          string url = IKCMS_RouteUrlManager.GetMvcUrlGeneral(fsNode_Module.vNode.snode, item.vNode.snode, null);
          if (linkToRelations == true)
          {
            try
            {
              var rel = item.Relations.Where(r => r.type == Ikon.IKGD.Library.IKGD_Constants.IKGD_LinkRelationName).FirstOrDefault();
              if (rel != null)
                url = IKCMS_RouteUrlManager.GetMvcUrlGeneral(null, rel.snode_dst, null);
            }
            catch { }
          }
          try { feedItem.AddPermalink(new Uri(HttpContext.Current.Request.Url, Utility.ResolveUrl(url))); }
          catch { }
          //
          AddItem(feedItem);
        });

      }

      return true;
    }


    //
    // todo: leggere url item dai links
    // attaccare a routing
    //
    public bool BuildFeedRSS_External(int? sNode_module, string pathModule, int? maxItemsCount, bool? addLinksToItem, System.Web.Mvc.ControllerContext context, string itemSparkViewPath) { return BuildFeedRSS_External(sNode_module, pathModule, maxItemsCount, addLinksToItem, context, itemSparkViewPath, null); }
    public bool BuildFeedRSS_External(int? sNode_module, string pathModule, int? maxItemsCount, bool? addLinksToItem, System.Web.Mvc.ControllerContext context, string itemSparkViewPath, int? vfsVersionForced)
    {
      FS_Operations.FS_NodeInfo fsNode_Module;
      IKGD_Path vfsPathModule;
      List<int> sNodeRoots;
      IKCMS_HasSerializationCMS_Interface moduleInfo;
      //
      maxItemsCount = maxItemsCount ?? 50;
      FeedRSS.ImageUrl = null;
      FeedRSS.Links.Add(new SyndicationLink(context.HttpContext.Request.Url, "self", null, null, 0));
      //
      // preparazione del supporto per le view custom degli items
      // per un esempio migliore di render su string vedere il metodo: HtmlHelperExtension.RenderViewToString
      //
      Spark.Web.Mvc.SparkView view = null;
      try
      {
        Spark.Web.Mvc.SparkViewFactory viewFactory = (Spark.Web.Mvc.SparkViewFactory)System.Web.Mvc.ViewEngines.Engines.FirstOrDefault(e => e is Spark.Web.Mvc.SparkViewFactory);
        //view = (Spark.Web.Mvc.SparkView)viewFactory.Engine.CreateInstance(viewFactory.CreateDescriptor(null, null, itemSparkViewPath, null, false));
        //view = (Spark.Web.Mvc.SparkView)viewFactory.Engine.CreateInstance(viewFactory.CreateDescriptor(context, itemSparkViewPath, null, false, null));
        //view = viewFactory.FindPartialView(context, itemSparkViewPath).View as Spark.Web.Mvc.SparkView;
        view = viewFactory.FindPartialView(context, itemSparkViewPath).View as Spark.Web.Mvc.SparkView;
      }
      catch { }
      //
      using (FS_Operations fsOp = new FS_Operations(vfsVersionForced))
      {
        if (sNode_module != null)
          vfsPathModule = fsOp.PathsFromNodeExt(sNode_module.Value).FirstOrDefault();
        else
          vfsPathModule = fsOp.PathsFromString(pathModule, true).FirstOrDefault();
        if (vfsPathModule == null)
          return false;
        fsNode_Module = fsOp.Get_NodeInfoACL(vfsPathModule.sNode, false, false);
        sNodeRoots = fsOp.Get_RELATIONs(fsNode_Module.vNode).Where(r => r.type == Ikon.IKGD.Library.IKGD_Constants.IKGD_ArchiveRelationName).OrderBy(r => r.position).Select(r => r.snode_dst).ToList();
        // nel caso non sia stato specificato un modulo valido come pagina di visualizzazione (es. un teaser news) ne disabilita l'uso nella url generata e delega il compito al ModelBuilder
        bool isValidBrowserModuleSelected = IKCMS_RegisteredTypes.Types_IKCMS_BrowsableModule_Interface.Select(t => t.Name).Contains(fsNode_Module.ManagerType);
        //
        moduleInfo = IKCMS_RegisteredTypes.Deserialize_IKCMS_ResourceVFS(fsNode_Module);
        object moduleInfoSettings = moduleInfo.ResourceSettingsObject;
        //
        string archiveFilterType = Utility.FindPropertySafe<string>(moduleInfoSettings, "ArchiveFilterType");
        string archiveCollectorType = Utility.FindPropertySafe<string>(moduleInfoSettings, "ArchiveCollectorType");
        IKGD_Archive_Filter_Interface itemsFilter = null;
        IKGD_Archive_Collector_Interface<FS_Operations.FS_NodeInfo_Interface> itemsCollector = null;
        try { itemsFilter = (IKGD_Archive_Filter_Interface)Activator.CreateInstance(Utility.FindTypeGeneric(archiveFilterType)); }
        catch { }
        try { itemsCollector = (IKGD_Archive_Collector_Interface<FS_Operations.FS_NodeInfo_Interface>)Activator.CreateInstance(Utility.FindTypeGeneric(archiveCollectorType, typeof(FS_Operations.FS_NodeInfo_Interface))); }
        catch { }
        itemsCollector = itemsCollector ?? new IKGD_Archive_Collector_NewsWithFoldersGeneral<FS_Operations.FS_NodeInfo_Interface>();
        itemsFilter = itemsFilter ?? new IKGD_Archive_Filter_DateRange();
        //itemsFilter = itemsFilter ?? new IKGD_Archive_Filter_NULL();
        //
        // TitleHead e TitleH1 sono definiti nei moduli tipo browse, Title e' definito nei moduli tipo teaser
        var author = FeedRSS.Authors.FirstOrDefault();
        string TitleHead = Utility.FindPropertySafe<string>(moduleInfoSettings, "TitleHead");
        string TitleH1 = Utility.FindPropertySafe<string>(moduleInfoSettings, "TitleH1");
        string Title = Utility.FindPropertySafe<string>(moduleInfoSettings, "Title");
        if (moduleInfo is IKCMS_HasPropertiesLanguageKVT_Interface)
        {
          TitleHead = (moduleInfo as IKCMS_HasPropertiesLanguageKVT_Interface).ResourceSettingsKVT.KeyFilterTry(fsNode_Module.Language, "TitleHead").ValueString;
          TitleH1 = (moduleInfo as IKCMS_HasPropertiesLanguageKVT_Interface).ResourceSettingsKVT.KeyFilterTry(fsNode_Module.Language, "TitleH1").ValueString;
          Title = (moduleInfo as IKCMS_HasPropertiesLanguageKVT_Interface).ResourceSettingsKVT.KeyFilterTry(fsNode_Module.Language, "Title").ValueString;
          //
          author.Name = (moduleInfo as IKCMS_HasPropertiesLanguageKVT_Interface).ResourceSettingsKVT.KeyFilterTry(fsNode_Module.Language, "FeedAuthorName").ValueString ?? author.Name;
          author.Uri = (moduleInfo as IKCMS_HasPropertiesLanguageKVT_Interface).ResourceSettingsKVT.KeyFilterTry(fsNode_Module.Language, "FeedAuthorUrl").ValueString ?? author.Uri;
          author.Email = (moduleInfo as IKCMS_HasPropertiesLanguageKVT_Interface).ResourceSettingsKVT.KeyFilterTry(fsNode_Module.Language, "FeedAuthorEmail").ValueString ?? author.Email;
        }
        else if (moduleInfo is IKCMS_HasPropertiesKVT_Interface)
        {
          TitleHead = (moduleInfo as IKCMS_HasPropertiesKVT_Interface).ResourceSettingsKVT["TitleHead"].ValueString;
          TitleH1 = (moduleInfo as IKCMS_HasPropertiesKVT_Interface).ResourceSettingsKVT["TitleH1"].ValueString;
          Title = (moduleInfo as IKCMS_HasPropertiesKVT_Interface).ResourceSettingsKVT["Title"].ValueString;
          //
          author.Name = (moduleInfo as IKCMS_HasPropertiesKVT_Interface).ResourceSettingsKVT["FeedAuthorName"].ValueString ?? author.Name;
          author.Uri = (moduleInfo as IKCMS_HasPropertiesKVT_Interface).ResourceSettingsKVT["FeedAuthorUrl"].ValueString ?? author.Uri;
          author.Email = (moduleInfo as IKCMS_HasPropertiesKVT_Interface).ResourceSettingsKVT["FeedAuthorEmail"].ValueString ?? author.Email;
        }
        TitleHead = TitleHead ?? TitleH1 ?? Title ?? fsNode_Module.vNode.name;
        TitleH1 = TitleH1 ?? TitleHead ?? Title ?? fsNode_Module.vNode.name;
        //
        FeedRSS.Title = SyndicationContent.CreatePlaintextContent(TitleHead);
        FeedRSS.Description = SyndicationContent.CreatePlaintextContent(TitleH1);
        //
        int? maxRecursionLevel = itemsCollector.ScanSubTree ? (int?)null : 0;
        FS_Operations.FS_TreeNode<IKCMS_TreeBrowser_fsNodeElement_Interface> nodesTree = fsOp.Get_TreeDataShortGeneric<IKCMS_TreeBrowser_fsNodeElement_Interface>(null, sNodeRoots, itemsFilter.vNodeFilter, itemsFilter.vDataFilter, maxRecursionLevel, true, true);
        List<int> folderSet = nodesTree.RecurseOnTree.Where(n => n.Data != null).Select(n => n.Data.vNode.folder).Distinct().ToList();
        //Dictionary<int, string> folderNames = nodesTree.RecurseOnTree.Where(n => n.Data != null).ToDictionary(n => n.Data.vNode.folder, n => n.Data.vNode.name);
        ILookup<int, string> folderNames = nodesTree.RecurseOnTree.Where(n => n.Data != null).ToLookup(n => n.Data.vNode.folder, n => n.Data.vNode.name);
        //
        Expression<Func<IKGD_VDATA, bool>> vDataFilterAll = fsOp.Get_vDataFilterACLv2();
        Expression<Func<IKGD_VNODE, bool>> vNodeFilterAll = fsOp.Get_vNodeFilterACLv2();
        //
        vNodeFilterAll = vNodeFilterAll.And(n => n.flag_folder == false);
        vNodeFilterAll = vNodeFilterAll.And(n => folderSet.Contains(n.folder));  // scan di tutto il tree
        if (itemsFilter.vNodeFilter != null)
          vNodeFilterAll = vNodeFilterAll.And(itemsFilter.vNodeFilter);
        if (itemsFilter.vDataFilter != null)
          vDataFilterAll = vDataFilterAll.And(itemsFilter.vDataFilter);
        //
        if (itemsFilter.FilterBrowsableItemsOnly)
        {
          var typesToScan = IKCMS_RegisteredTypes.Types_IKCMS_BrowsableIndexable_Interface.Select(t => t.Name).ToList();
          if (typesToScan.Any())
            vDataFilterAll = vDataFilterAll.And(n => typesToScan.Contains(n.manager_type));
        }
        //
        IQueryable<FS_Operations.FS_NodeInfo_Interface> resources =
          from vNode in fsOp.NodesActive<IKGD_VNODE>().Where(vNodeFilterAll)
          from vData in fsOp.NodesActive<IKGD_VDATA>().Where(vDataFilterAll).Where(n => n.rnode == vNode.rnode)
          select new FS_Operations.FS_NodeInfo { vNode = vNode, vData = vData } as FS_Operations.FS_NodeInfo_Interface;
        //
        // ignoriamo il sorter del collector e mostriamo le notizie solo in ordine di aggiornamento
        //resources = itemsCollector.Sorter(resources);
        resources = resources.OrderByDescending(n => n.vData.version_date).ThenByDescending(n => n.vNode.snode);
        resources = resources.Take(maxItemsCount.Value);
        //
        resources.ForEach(item =>
        {
          SyndicationItem feedItem = new SyndicationItem();
          //
          IKCMS_HasSerializationCMS_Interface itemData = IKCMS_RegisteredTypes.Deserialize_IKCMS_ResourceVFS(item);
          object itemSettings = itemData.ResourceSettingsObject;
          KeyValueObjectTree itemSettingsKVT = (itemData is IKCMS_HasPropertiesKVT_Interface) ? (itemData as IKCMS_HasPropertiesKVT_Interface).ResourceSettingsKVT : null;
          //
          //feedItem.Id = item.vNode.snode.ToString();
          feedItem.Id = IKCMS_RouteUrlManager.GetMvcUrlGeneral(fsNode_Module.vNode.snode, item.vNode.snode, null, true, true);
          //
          if (itemSettingsKVT != null)
          {
            feedItem.Title = SyndicationContent.CreatePlaintextContent(itemSettingsKVT["Title"].Value as string);
            feedItem.Summary = SyndicationContent.CreateHtmlContent(itemSettingsKVT["Abstract"].Value as string);
            feedItem.Content = SyndicationContent.CreateHtmlContent(itemSettingsKVT["Text"].Value as string);
          }
          else
          {
            feedItem.Title = SyndicationContent.CreatePlaintextContent(Utility.FindPropertySafe<string>(itemSettings, "Title"));
            feedItem.Summary = SyndicationContent.CreateHtmlContent(Utility.FindPropertySafe<string>(itemSettings, "Abstract"));
            feedItem.Content = SyndicationContent.CreateHtmlContent(Utility.FindPropertySafe<string>(itemSettings, "Text"));
          }
          //
          feedItem.PublishDate = new DateTimeOffset(item.vNode.IKGD_RNODE.date_creat);
          //feedItem.PublishDate = new DateTimeOffset(item.vData.date_node);
          //
          feedItem.LastUpdatedTime = new DateTimeOffset((item.vNode.version_date >= item.vData.version_date) ? item.vNode.version_date : item.vData.version_date);
          if (folderNames.Contains(item.vNode.folder))
          {
            feedItem.Categories.Add(new SyndicationCategory(folderNames[item.vNode.folder].FirstOrDefault()));
          }
          //
          if (addLinksToItem == true || true)
          {
            string url = IKCMS_RouteUrlManager.GetMvcUrlGeneral(isValidBrowserModuleSelected ? (int?)fsNode_Module.vNode.snode : null, item.vNode.snode, null);
            try { feedItem.AddPermalink(new Uri(HttpContext.Current.Request.Url, Utility.ResolveUrl(url))); }
            catch { }
          }
          //
          if (view != null)
          {
            try
            {
              view.ViewData = new System.Web.Mvc.ViewDataDictionary(item);
              view.ViewData["itemSettings"] = itemSettings;
              view.ViewData["itemSettingsKVT"] = itemSettingsKVT;
              view.Url = new System.Web.Mvc.UrlHelper(context.RequestContext);
              using (StringWriter writer = new StringWriter())
              {
                view.Html = new System.Web.Mvc.HtmlHelper(new System.Web.Mvc.ViewContext(context, new FakeView(), view.ViewData, new System.Web.Mvc.TempDataDictionary(), writer), new System.Web.Mvc.ViewPage());
                view.RenderView(writer);
                string html = writer.ToString();
                feedItem.Summary = SyndicationContent.CreateHtmlContent(html);
                feedItem.Content = null;
              }
            }
            catch { }
          }
          //
          AddItem(feedItem);
        });

      }

      return true;
    }


    /// <summary>Fake IView implementation, only used to instantiate an HtmlHelper.</summary> 
    public class FakeView : System.Web.Mvc.IView
    {
      public void Render(System.Web.Mvc.ViewContext viewContext, System.IO.TextWriter writer)
      {
        throw new NotImplementedException();
      }
    }




  }  //RSSFeedGenerator




}