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
using System.Web.UI;
using System.Web.Security;
using System.Xml.Linq;
using System.Data.Linq;
using System.IO;
using System.Text;
using System.Linq.Expressions;
using System.Reflection;
using System.Data.Linq.Mapping;
using System.Diagnostics;
using System.Linq.Dynamic;
using System.Transactions;
using System.Web.Caching;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;  // assembly System.ServiceModel.Web

using LinqKit;
using Autofac;
using Autofac.Core;
using Autofac.Builder;
using Autofac.Features;

using Ikon;
using Ikon.IKCMS;


namespace Ikon.IKCMS
{
  using Ikon.Config;
  using Ikon.GD;
  using Ikon.IKGD.Library.Resources;
  using Ikon.IKGD.Library.Collectors;
  using Ikon.IKCMS.Library.Resources;
  using Ikon.IKGD.Library;



  public interface IKCMS_ModelCMS_ArchiveBrowserItem_Interface : IKCMS_ModelCMS_GenericBrickInterface, IKCMS_ModelCMS_EnsureParentModel_Interface
  {
    IKCMS_ModelCMS_Interface ModelContainerUnBinded { get; set; }
  }


  //
  // model per gli items di un modulo news (nella index view),
  // sono quasi equivalenti ai widget ma includono anche l'autogenerazione della url per il dettaglio
  //
  [IKCMS_ModelCMS_ResourceTypes(typeof(IKCMS_Widget_Interface), typeof(IKCMS_HasSerializationCMS_Interface))]
  [IKCMS_ModelCMS_ResourceTypes(typeof(IKCMS_ResourceType_NewsKVT))]
  [IKCMS_ModelCMS_RecursionMode(ModelRecursionModeEnum.RecursionNone)]
  [IKCMS_ModelCMS_fsNodeMode(vfsNodeFetchModeEnum.vNode_vData_iNode_Extra)]
  [IKCMS_ModelCMS_Priority(-2499999)]
  public class IKCMS_ModelCMS_ArchiveBrowserItem<T> : IKCMS_ModelCMS_GenericBrickBase<T>, IKCMS_ModelCMS_ArchiveBrowserItem_Interface, IKCMS_ModelCMS_EnsureParentModel_Interface
    where T : class, IKCMS_HasGenericBrick_Interface
  {
    public IKCMS_ModelCMS_Interface ModelContainerUnBinded { get; set; }
    //public string Url { get; set; }
    //

    public override bool IsExpired
    {
      get
      {
        bool expired = base.IsExpired && !Utility.TryParse<bool>(IKGD_Config.AppSettings["IKCMS_IgnoreExpiredStatusForBrowse"], false);
        if (!expired && (ModelParent != null && ModelParent is IKCMS_ModelCMS_Page_Interface))
        {
          expired |= (ModelParent as IKCMS_ModelCMS_Page_Interface).IsExpired;
        }
        return expired;
      }
      set { base.IsExpired = value; }
    }


    public override string Url
    {
      get
      {
        if (_Url == null)
        {
          _Url = IKCMS_RouteUrlManager.GetMvcUrlGeneralV2(this.Language ?? ModelContainerUnBinded.Return(m => m.Language) ?? IKGD_Language_Provider.Provider.LanguageNN, this.sNode, ModelContainerUnBinded.Return(m => (int?)m.sNode), null, false);
          //_Url = IKCMS_RouteUrlManager.GetMvcUrlGeneral(ModelContainerUnBinded == null ? null : (int?)ModelContainerUnBinded.sNode, this.sNode, null, true, false);
        }
        return _Url;
      }
      protected set { _Url = value; }
    }


    public override string UrlCanonical
    {
      get { return _UrlCanonical ?? Url; }
      protected set { _UrlCanonical = value; }
    }

    //
    // continuazione del setup del model dopo le features di base processate in IKCMS_ModelCMS
    //
    protected override void SetupFinalize(params object[] args)
    {
      base.SetupFinalize(args);
      //
      // adesso IKCMS_ResourceType_NewsKVT eredita da bricks e quindi elimino il supporto per il template nel caso sia stato definito un parent
      if (ModelParent != null && ModelParent is IKCMS_ModelCMS_HasTemplateInfo_Interface && (ModelParent as IKCMS_ModelCMS_HasTemplateInfo_Interface).TemplateInfo != null)
      {
        this.TemplateInfo = null;
        this.TemplateViewPath = null;
      }
      //
      try
      {
        //try { Url = IKCMS_RouteUrlManager.GetMvcUrlGeneral((ModelContainerUnBinded ?? this.ModelRoot).sNode, this.sNode, null, true, false); }
        //catch { }
        //
      }
      catch { }
      //
    }


    //
    // per i moduli tipo news e' fondamentale che siano associati ad un viewer nel ModelParent
    // ma questo deve accadere solo nel caso il Model sia stato creato da un Controller per una View
    // (quando creiamo dei submodel e poi li attacchiamo ad un model esistente, come avviene in GetIndexItems, non voglio settare il ModelParent)
    // per distinguere i due casi e' possibile verificare che negli args non ci sia un Context
    //
    public virtual IKCMS_ModelCMS_Interface EnsureParentModel(FS_Operations.FS_NodeInfo_Interface fsNode, IKCMS_ModelCMS_ModelInfo_Interface modelInfo, params object[] args)
    {
      IKCMS_ModelCMS_Interface modelParentNew = null;
      try
      {
        //
        if (args != null && args.OfType<System.Web.Mvc.ControllerContext>().Any())
        {
          //
          // esiste un ControllerContext negli args quindi il metodo e' stato chiamato da un controller IKCMS
          // e dobbiamo creare il parent model cercandolo tra le risorse che hanno una correlazione di tipo "archive" con la risorsa selezionata
          // il nodo correlato non deve necessariamente essere di tipo IKCMS_FolderType_ArchiveRoot ma potrebbe essere un folder qualsiasi
          // IKCMS_FolderType_ArchiveRoot diventa necessario solo per definire la root di un archivio quando usiamo i menu' con le categorie
          //
          modelParentNew = IKCMS_ModelCMS_ArchiveBrowserHelper.EnsureParentModel_Worker(fsOp, fsNode, this, modelInfo, null, null, args);
          //
        }
      }
      catch { }
      return modelParentNew;
    }


    /*
    public virtual IKCMS_ModelCMS_Interface EnsureParentModel_OLD(FS_Operations.FS_NodeInfo_Interface fsNode, IKCMS_ModelCMS_ModelInfo_Interface modelInfo, params object[] args)
    {
      IKCMS_ModelCMS_Interface modelParentNew = null;
      try
      {
        //
        if (args == null || !args.OfType<System.Web.Mvc.ControllerContext>().Any())
          return modelParentNew;
        //
        // esiste un ControllerContext negli args quindi il metodo e' stato chiamato da un controller IKCMS
        // e dobbiamo creare il parent model cercandolo tra le risorse che hanno una correlazione di tipo "archive" con la risorsa selezionata
        // il nodo correlato non deve necessariamente essere di tipo IKCMS_FolderType_ArchiveRoot ma potrebbe essere un folder qualsiasi
        // IKCMS_FolderType_ArchiveRoot diventa necessario solo per definire la root di un archivio quando usiamo i menu' con le categorie
        //
        // cerchiamo tutte le pagine che abbiano un target rnode presente nella lista dei folder del path della risorsa corrente
        // selezionando solo le risorse di tipo Types_IKCMS_BrowsableModule_Interface (e quindi niente teaser tipo news) quindi scegliamo quello con il path
        // che punta ad un archive root o piu' "vicino" alla risorsa selezionata (come step) quindi con l'ordine normale dei path
        //
        var manager_types = IKCMS_RegisteredTypes.Types_IKCMS_BrowsableModule_Interface.Select(t => t.Name).ToList();
        var item_paths = fsOp.PathsFromNodesExt(null, new int[] { fsNode.rNode }, false, false);
        List<int> folders_rNodes = item_paths.SelectMany(p => p.Fragments.Where(f => f.flag_folder && f.Parent > 0).Select(f => f.rNode)).Distinct().ToList();  // qualsiasi folder escluse le roots
        List<int> archive_rNodes = item_paths.SelectMany(p => p.Fragments.Where(f => f.ManagerType == typeof(IKCMS_FolderType_ArchiveRoot).Name).Select(f => f.rNode)).Distinct().ToList();
        var fsNodeModules = fsOp.Get_NodesInfoFilteredExt2(vn => vn.flag_folder == true, vd => manager_types.Contains(vd.manager_type), null, r => r.type == IKGD_Constants.IKGD_ArchiveRelationName && folders_rNodes.Contains(r.rnode_dst), false, true, true, false).Where(n => n.Relations.Any()).ToList();
        var paths = fsOp.PathsFromNodesExt(fsNodeModules.Select(n => n.sNode), null, false, true, false).FilterPathsByRootsCMS().FilterPathsByLanguage().FilterPathsByExpiry().FilterPathsByACL().ToList();
        paths = paths.FilterFallback(new Func<IKGD_Path, bool>[] { IKGD_Path_Helper.FilterByRootCMS, IKGD_Path_Helper.FilterByLanguage, IKGD_Path_Helper.FilterByActive, IKGD_Path_Helper.FilterByAreas }).OrderByACL().ToList();
        IKGD_Path path = null;
        //
        // precedenza ai moduli che puntano direttamente al folder dell'item selezionato
        path = path ?? paths.FirstOrDefault(p => fsNodeModules.Any(n => n.rNode == p.rNode && n.Relations.Where(r => r.type == IKGD_Constants.IKGD_ArchiveRelationName).Any(r => r.rnode_dst == fsNode.Folder)));
        //
        // precedenza ai moduli che puntano ai nodi piu' vicini di livello al nodo selezionato
        if (path == null)
        {
          try
          {
            var pathsAux = paths.OrderBy(p => p.Fragments.Count - p.Fragments.LastIndexOf(p.Fragments.LastOrDefault(f => fsNodeModules.Where(n => n.rNode == p.rNode).SelectMany(n => n.Relations.Where(r => r.type == IKGD_Constants.IKGD_ArchiveRelationName).Select(r => r.rnode_dst)).Contains(f.rNode)))).ThenBy(p => p).ToList();
            path = pathsAux.FirstOrDefault();
          }
          catch { }
        }
        //
        if (path == null && archive_rNodes.Any())
        {
          // diamo la precedenza ai nodi di tipo archivio
          path = path ?? paths.FirstOrDefault(p => fsNodeModules.Any(n => n.rNode == p.rNode && n.Relations.Where(r => r.type == IKGD_Constants.IKGD_ArchiveRelationName).Any(r => archive_rNodes.Contains(r.rnode_dst))));
        }
        //
        path = path ?? paths.FirstOrDefault();
        modelParentNew = IKCMS_ModelCMS_Provider.Provider.ModelBuild(null, path.sNode, null, args);
      }
      catch { }
      return modelParentNew;
    }
    */


  }


  public static class IKCMS_ModelCMS_ArchiveBrowserHelper
  {

    //
    // model e modelInfo non sono attualmente usati, sono presenti per estensioni future
    public static IKCMS_ModelCMS_Interface EnsureParentModel_Worker(FS_Operations fsOp, FS_Operations.FS_NodeInfo_Interface fsNode, IKCMS_ModelCMS_Interface model, IKCMS_ModelCMS_ModelInfo_Interface modelInfo, bool? scanBrowserModulesOnly, bool? scanArchiveRootsOnly, params object[] args)
    {
      return EnsureParentModel_WorkerWithFilter(fsOp, fsNode, model, modelInfo, scanBrowserModulesOnly, scanArchiveRootsOnly, null, args);
    }


    //
    // model e modelInfo non sono attualmente usati, sono presenti per estensioni future
    public static IKCMS_ModelCMS_Interface EnsureParentModel_WorkerWithFilter(FS_Operations fsOp, FS_Operations.FS_NodeInfo_Interface fsNode, IKCMS_ModelCMS_Interface model, IKCMS_ModelCMS_ModelInfo_Interface modelInfo, bool? scanBrowserModulesOnly, bool? scanArchiveRootsOnly, Func<IKGD_Path_Fragment, bool> filter, params object[] args)
    {
      IKCMS_ModelCMS_Interface modelParentNew = null;
      try
      {
        //
        // cerchiamo tutte le pagine che abbiano un target rnode presente nella lista dei folder del path della risorsa corrente
        // selezionando solo le risorse di tipo Types_IKCMS_BrowsableModule_Interface (e quindi niente teaser tipo news) quindi scegliamo quello con il path
        // che punta ad un archive root o piu' "vicino" alla risorsa selezionata (come step) quindi con l'ordine normale dei path
        //
        scanBrowserModulesOnly = scanBrowserModulesOnly ?? Utility.TryParse<bool>(IKGD_Config.AppSettings["ModelBuilder_EnsureParentModel_ScanBrowserModulesOnly"], false);
        scanArchiveRootsOnly = scanArchiveRootsOnly ?? Utility.TryParse<bool>(IKGD_Config.AppSettings["ModelBuilder_EnsureParentModel_ScanArchiveRootsOnly"], false);
        //
        var manager_types_page = IKCMS_RegisteredTypes.Types_IKCMS_Page_Interface.Select(t => t.Name).ToList();
        var manager_types_browser = IKCMS_RegisteredTypes.Types_IKCMS_BrowsableModule_Interface.Select(t => t.Name).ToList();
        var item_paths = fsOp.PathsFromNodesExt(null, new int[] { fsNode.rNode }, false, false, true);  // Model.PathsVFS  non e' ancora disponibile...
        List<int> folders_rNodes = item_paths.SelectMany(p => p.Fragments.Where(f => f.flag_folder && f.Parent > 0).Select(f => f.rNode)).Distinct().ToList();  // qualsiasi folder escluse le roots
        List<int> archive_rNodes = item_paths.SelectMany(p => p.Fragments.Where(f => f.ManagerType == typeof(IKCMS_FolderType_ArchiveRoot).Name).Select(f => f.rNode)).Distinct().ToList();
        //
        var manager_types = scanBrowserModulesOnly.GetValueOrDefault(true) ? manager_types_browser : manager_types_page;
        var mapped_rNodes = scanArchiveRootsOnly.GetValueOrDefault(false) ? archive_rNodes : folders_rNodes;
        //
        // TODO: passare a del codice piu' ottimizzato utilizzando i frags del path anche come metodologia di scan
        // tutte le pagine che hanno almeno una relation che punta ad uno degli archivi presenti nel path della risorsa
        var fsNodeModules = fsOp.Get_NodesInfoFilteredExt2(vn => vn.flag_folder == true, vd => manager_types.Contains(vd.manager_type), null, r => r.type == IKGD_Constants.IKGD_ArchiveRelationName && mapped_rNodes.Contains(r.rnode_dst), FS_Operations.FiltersVFS_Default).Where(n => n.Relations.Any()).ToList();
        var paths = fsOp.PathsFromNodesExt(fsNodeModules.Select(n => n.sNode), null, true, false, true).FilterPathsByRootsCMS().FilterPathsByExpiry().FilterPathsByACL().ToList();
        //
        // filtrazione ordinata dei path
        //
        var paths_filtered = paths.FilterFallback(new Func<IKGD_Path, bool>[] { IKGD_Path_Helper.FilterByRootCMS, IKGD_Path_Helper.FilterByLanguage, IKGD_Path_Helper.FilterByActive, IKGD_Path_Helper.FilterByAreas }).OrderByACL().ToList();
        if (filter != null && paths_filtered.Count > 0)
        {
          try { paths_filtered = paths_filtered.Where(p => p.Fragments.Any(filter)).ToList(); }
          catch { }
        }
        if (paths_filtered.Count > 1)
        {
          // precedenza ai moduli che puntano direttamente al folder dell'item selezionato
          try { paths_filtered = paths_filtered.GroupBy(p => fsNodeModules.Any(n => n.rNode == p.rNode && n.Relations.Where(r => r.type == IKGD_Constants.IKGD_ArchiveRelationName).Any(r => r.rnode_dst == fsNode.Folder))).OrderByDescending(g => g).FirstOrDefault().ToList(); }
          catch { }
        }
        if (paths_filtered.Count > 1 && archive_rNodes.Any())
        {
          // precedenza ai nodi di tipo archivio
          try { paths_filtered = paths_filtered.GroupBy(p => fsNodeModules.Any(n => n.rNode == p.rNode && n.Relations.Where(r => r.type == IKGD_Constants.IKGD_ArchiveRelationName).Any(r => archive_rNodes.Contains(r.rnode_dst)))).OrderByDescending(g => g).FirstOrDefault().ToList(); }
          catch { }
        }
        if (paths_filtered.Count > 1 && fsNode is FS_Operations.FS_NodeInfoExt_Interface)
        {
          // ordinamento per il match sulla corrispondenza dei tags
          try
          {
            var props = (fsNode as FS_Operations.FS_NodeInfoExt_Interface).Properties;
            var tags = ((props != null) ? props.Where(p => p.attributeId != null).Select(p => p.attributeId.Value) : Enumerable.Empty<int>()).Distinct().ToList();
            paths_filtered = paths_filtered.GroupBy(p => fsNodeModules.FirstOrDefault(n => n.rNode == p.rNode).Properties.Where(r => r.attributeId != null).Select(r => r.attributeId.Value).Intersect(tags).Count()).OrderByDescending(g => g).FirstOrDefault().ToList();
          }
          catch { }
        }
        if (paths_filtered.Count > 1)
        {
          // precedenza ai moduli che puntano ai nodi piu' vicini di livello al nodo selezionato
          try { paths_filtered = paths_filtered.GroupBy(p => fsNodeModules.Where(n => n.rNode == p.rNode).SelectMany(n => n.Relations.Where(r => r.type == IKGD_Constants.IKGD_ArchiveRelationName)).Max(r => folders_rNodes.IndexOf(r.rnode_dst))).OrderByDescending(g => g).FirstOrDefault().ToList(); }
          catch { }
        }
        //
        IKGD_Path path = paths_filtered.FirstOrDefault() ?? paths.FirstOrDefault();
        modelParentNew = IKCMS_ModelCMS_Provider.Provider.ModelBuild(null, path.sNode, null, args);
        //
      }
      catch { }
      return modelParentNew;
    }


    public static IKGD_Path GetPathForBrowserModule(FS_Operations fsOp, bool? BrowsableModulesOnly, params int[] rNodeArchives)
    {
      IKGD_Path path = null;
      try
      {
        var manager_types = (BrowsableModulesOnly.GetValueOrDefault(false) ? IKCMS_RegisteredTypes.Types_IKCMS_BrowsableModule_Interface : IKCMS_RegisteredTypes.Types_IKCMS_Page_Interface).Where(t => !t.IsAbstract).Select(t => t.Name).ToList();
        var archives_paths = fsOp.PathsFromNodesExt(null, rNodeArchives, false, false);
        List<int> folders_rNodes = archives_paths.SelectMany(p => p.Fragments.Where(f => f.flag_folder && f.Parent > 0).Select(f => f.rNode)).Distinct().ToList();  // qualsiasi folder escluse le roots
        List<int> archive_rNodes = archives_paths.SelectMany(p => p.Fragments.Where(f => f.ManagerType == typeof(IKCMS_FolderType_ArchiveRoot).Name).Select(f => f.rNode)).Distinct().ToList();
        var fsNodeModules = fsOp.Get_NodesInfoFilteredExt2(vn => vn.flag_folder == true, vd => manager_types.Contains(vd.manager_type), null, r => r.type == IKGD_Constants.IKGD_ArchiveRelationName && folders_rNodes.Contains(r.rnode_dst), FS_Operations.FiltersVFS_Default).Where(n => n.Relations.Any()).ToList();
        var paths = fsOp.PathsFromNodesExt(fsNodeModules.Select(n => n.sNode), null, false, true, false);
        paths = paths.FilterFallback(new Func<IKGD_Path, bool>[] { IKGD_Path_Helper.FilterByRootCMS, IKGD_Path_Helper.FilterByLanguage, IKGD_Path_Helper.FilterByActive, IKGD_Path_Helper.FilterByAreas }).OrderByACL().ToList();
        //
        // precedenza ai moduli che puntano direttamente al folder dell'item selezionato
        int folder = archives_paths.FirstOrDefault().FolderFragment.rNode;
        path = path ?? paths.FirstOrDefault(p => fsNodeModules.Any(n => n.rNode == p.rNode && n.Relations.Where(r => r.type == IKGD_Constants.IKGD_ArchiveRelationName).Any(r => r.rnode_dst == folder)));
        //
        // precedenza ai moduli che puntano ai nodi piu' vicini di livello al nodo selezionato
        if (path == null)
        {
          try
          {
            var pathsAux = paths.OrderBy(p => p.Fragments.Count - p.Fragments.LastIndexOf(p.Fragments.LastOrDefault(f => fsNodeModules.Where(n => n.rNode == p.rNode).SelectMany(n => n.Relations.Where(r => r.type == IKGD_Constants.IKGD_ArchiveRelationName).Select(r => r.rnode_dst)).Contains(f.rNode)))).ThenBy(p => p).ToList();
            path = pathsAux.FirstOrDefault();
          }
          catch { }
        }
        //
        if (path == null && archive_rNodes.Any())
        {
          // diamo la precedenza ai nodi di tipo archivio
          path = path ?? paths.FirstOrDefault(p => fsNodeModules.Any(n => n.rNode == p.rNode && n.Relations.Where(r => r.type == IKGD_Constants.IKGD_ArchiveRelationName).Any(r => archive_rNodes.Contains(r.rnode_dst))));
        }
        //
        path = path ?? paths.FirstOrDefault();
        //
      }
      catch { }
      return path;
    }


    public static string GetUrlForBrowserModule(FS_Operations fsOp, params int[] rNodeArchives)
    {
      IKGD_Path path = null;
      string url = null;
      try
      {
        path = GetPathForBrowserModule(fsOp, null, rNodeArchives);
        if (path != null)
        {
          url = IKCMS_RouteUrlManager.GetMvcUrlGeneralV2(path.FirstLanguageNN, path.sNode, null, "/" + Utility.UrlEncodeIndexPathForSEO(path.LastFragment.Name), false);
        }
      }
      catch { }
      return url;
    }

  }



  public interface IKCMS_ModelCMS_ArchiveBrowser_Interface : IKCMS_ModelCMS_Page_Interface
  {
    IKCMS_TreeModuleData<FS_Operations.FS_NodeInfo_Interface> moduleCollectorManager { get; }
    //
    IQueryable<FS_Operations.FS_NodeInfo_Interface> module_Items { get; }
    List<IKCMS_ModelCMS_ArchiveBrowserItem_Interface> module_ItemsWidgets { get; }
    Dictionary<int, FS_Operations.FS_NodeInfo_Interface> module_ItemRelations { get; }
    //
    List<string> module_BreadCrumbs { get; }
    //
    string UrlFirstNode { get; }
    string UrlCurrent { get; }
    //
    string BuildBrowseMenu(string mainClassCSS, bool fullMenuTree, Func<FS_Operations.FS_TreeNode<IKCMS_TreeBrowser_Element_Interface>, bool, string> itemFormatter);
    string BuildBrowseMenu(string mainClassCSS, bool fullMenuTree, int? levelMin, int? levelMax, Func<FS_Operations.FS_TreeNode<IKCMS_TreeBrowser_Element_Interface>, bool, string> itemFormatter);
    IEnumerable<IKCMS_ModelCMS_ArchiveBrowserItem_Interface> GetIndexItems(vfsNodeFetchModeEnum fetchMode, int? pagerPageSize);
    IEnumerable<IKCMS_ModelCMS_ArchiveBrowserItem_Interface> LoadIndexItems(vfsNodeFetchModeEnum fetchMode, int? pagerPageSize);
    //
    List<IKCMS_ModelCMS_Interface> GetHomeItems_Latest(int? MaxItems);
    List<IKCMS_ModelCMS_Interface> GetHomeItems_Next(int? MaxItems);
    List<IKCMS_ModelCMS_Interface> GetHomeItems(IKGD_Teaser_Collector_Interface<FS_Operations.FS_NodeInfo_Interface> itemsCollector, vfsNodeFetchModeEnum fetchMode, IKCMS_ModelCMS_Interface modelReference, int? MaxItems);
    IEnumerable<IKCMS_ModelCMS_Interface> GetHomeItemsWorker(IKGD_Teaser_Collector_Interface<FS_Operations.FS_NodeInfo_Interface> itemsCollector, vfsNodeFetchModeEnum fetchMode, IKCMS_ModelCMS_Interface modelReference, int? MaxItems);
    //
    List<object> GetCalendarEventsAjax(int? year, int? month, int? maxItems);
    //
  }



  [IKCMS_ModelCMS_ResourceTypes(typeof(IKCMS_BrowsableModule_Interface))]
  [IKCMS_ModelCMS_ResourceTypes(typeof(Ikon.IKCMS.Library.Resources.IKCMS_ResourceType_BrowserModuleKVT))]
  [IKCMS_ModelCMS_RecursionMode(ModelRecursionModeEnum.RecursionOnResources)]  // ovviamente si riferisce al template di visualizzazione e non all'archive item
  [IKCMS_ModelCMS_fsNodeMode(vfsNodeFetchModeEnum.vNode_vData_iNode_Extra)]
  [IKCMS_ModelCMS_fsNodeModeRecurse(vfsNodeFetchModeEnum.vNode_vData)]
  [IKCMS_ModelCMS_Priority(-1499999)]
  public class IKCMS_ModelCMS_ArchiveBrowser<T> : IKCMS_ModelCMS_PageCMS<T>, IKCMS_ModelCMS_ArchiveBrowser_Interface, IKCMS_ModelCMS_HasPostFinalizeMethod_Interface, IKCMS_ModelCMS_VFS_Interface
    where T : class, IKCMS_HasGenericBrick_Interface  //ricordarsi di utilizzare anche l'attribute IKCMS_ModelCMS_BootStrapperOpenGenericsAttribute
  {
    //
    public FS_Operations.FS_TreeNode<IKCMS_TreeBrowser_Element_Interface> module_IndexTreeNode;
    public FS_Operations.FS_NodeInfoExt module_fsNode_Item;
    public IQueryable<FS_Operations.FS_NodeInfo_Interface> module_Items { get; set; }
    public List<IKCMS_ModelCMS_ArchiveBrowserItem_Interface> module_ItemsWidgets { get; set; }
    public Dictionary<int, FS_Operations.FS_NodeInfo_Interface> module_ItemRelations { get; set; }
    //
    public string UrlFirstNode { get { return Utility.ResolveUrl(moduleCollectorManager.nodesTree.RecurseOnData.Where(n => n != null).Select(n => n.url).FirstOrDefault()) ?? "javascript:;"; } }
    public string UrlCurrent { get { try { return Utility.ResolveUrl(module_IndexTreeNode.Data.url); } catch { return "javascript:;"; } } }
    //
    private List<string> _module_BreadCrumbs;
    public List<string> module_BreadCrumbs { get { return _module_BreadCrumbs; } }
    //


    public string contextCacheKey { get; protected set; }
    public IKCMS_TreeModuleData<FS_Operations.FS_NodeInfo_Interface> moduleCollectorManager
    {
      get
      {
        return FS_OperationsHelpers.CachedEntityWrapper(contextCacheKey, () =>
        {
          return new IKCMS_TreeModuleData<FS_Operations.FS_NodeInfo_Interface>(fsOp, this.sNode, null);
        }, 3600, FS_OperationsHelpers.Const_CacheDependencyIKGD_vNode_vData_Relation);
      }
    }


    protected override void SetupInstance(FS_Operations.FS_NodeInfo_Interface fsNode, IKCMS_ModelCMS_Interface modelParent, IKCMS_ModelCMS_ModelInfo_Interface modelInfo, params object[] args)
    {
      base.SetupInstance(fsNode, modelParent, modelInfo, args);
      //
      contextCacheKey = FS_OperationsHelpers.ContextHash("moduleCollectorManager", this.GetType(), this.sNode);
    }


    protected override void SetupFinalize(params object[] args)
    {
      base.SetupFinalize(args);
    }


    public override void SetupFinalizePost(IKCMS_ModelCMS_Interface subModel, params object[] args)
    {
      try
      {
        //
        int? sNodeItem = (subModel != null) ? (int?)subModel.sNode : null;
        //
        object moduleSettingsRoot = null;
        try { moduleSettingsRoot = ModelRoot.VFS_ResourceObject.ResourceSettingsObject; }
        catch { }
        //
        // se non ho un nodo valido forzo la visualizzazione a index
        //
        if (subModel == null || subModel.vfsNode == null)
        {
          moduleOp = string.IsNullOrEmpty(TemplateInfo.ViewPaths["home"]) ? "index" : "home";
        }
        if (moduleOp == "auto")
        {
          // quando arrivo da un modulo browser indexPath e' empty oppure comincia con /
          // mentre quando uso url tio code/12345/seo-friendly-url indexPath non comincia con /
          moduleOp = ((string.IsNullOrEmpty(indexPath) || !indexPath.StartsWith("/")) ? "item" : "index");
        }
        //
        // nel caso non ci sia un nodo valido dovrebbe ritornare il primo elemento dell'aggregato
        //
        switch (moduleOp)
        {
          case "item":
          case "index":
          case "auto":
            {
              bool res01 = moduleCollectorManager.GetItemInfo(fsOp, sNodeItem, indexPath, out module_IndexTreeNode, out module_fsNode_Item, out _module_BreadCrumbs);
              try { UrlBack = Utility.ResolveUrl(moduleCollectorManager.GetBackIndex(module_IndexTreeNode, null).Data.url); }
              catch { }  // utilizzare con href='${Model.Url_Back.DefaultIfEmpty("javascript:;")}'
              UrlModuleHome = IKCMS_RouteUrlManager.GetMvcUrlGeneral(this.sNode);
            }
            break;
          case "home":
            {
              // la preparazione dei dati per la lista in home e' demandata alla view (per la gestione del paging eventuale)
              // utilizzare la API: [TODO]
            }
            break;
        }
        //
        // completamento delle breadcrumbs con i nodi relativi all'index corrente del modulo browse
        //
        if (_module_BreadCrumbs != null && _module_BreadCrumbs.Any())
        {
          // TODO: sistemare la gestione delle url, e dell'sNode corretto da mappare (si dovrebbe prendere quello del primo nodo relativo all'index corrispondente)
          _module_BreadCrumbs.ForEach(b => BreadCrumbs.Add(new IKCMS_ModelCMS_BreadCrumbsElement(null, null, b)));
        }
        //
        // selezione del template
        //
        if (moduleOp == "item")
        {
          TemplateViewPath = TemplateInfo.ViewPaths["detail"] ?? TemplateInfo.ViewPath;
        }
        else if (moduleOp == "home")
        {
          TemplateViewPath = TemplateInfo.ViewPaths["home"] ?? TemplateInfo.ViewPaths["index"] ?? TemplateInfo.ViewPath;
        }
        else if (moduleOp == "index")
        {
          TemplateViewPath = TemplateInfo.ViewPaths["index"] ?? TemplateInfo.ViewPath;
        }
        else
        {
          TemplateViewPath = TemplateInfo.ViewPaths["index"] ?? TemplateInfo.ViewPath;
        }
      }
      catch { }
    }



    //
    // helper method per la generazione del menu senza dover specificare il nodo corrente
    //
    public string BuildBrowseMenu(string mainClassCSS, bool fullMenuTree, Func<FS_Operations.FS_TreeNode<IKCMS_TreeBrowser_Element_Interface>, bool, string> itemFormatter)
    {
      return moduleCollectorManager.BuildBrowseTreeMenu(module_IndexTreeNode, mainClassCSS, fullMenuTree, null, null, itemFormatter);
    }
    public string BuildBrowseMenu(string mainClassCSS, bool fullMenuTree, int? levelMin, int? levelMax, Func<FS_Operations.FS_TreeNode<IKCMS_TreeBrowser_Element_Interface>, bool, string> itemFormatter)
    {
      return moduleCollectorManager.BuildBrowseTreeMenu(module_IndexTreeNode, mainClassCSS, fullMenuTree, levelMin, levelMax, itemFormatter);
    }


    //
    // fetch degli items da visualizzare nell'index senza che siano aggiunti al Model (per problemi di caching con il pager)
    //
    public IEnumerable<IKCMS_ModelCMS_ArchiveBrowserItem_Interface> GetIndexItems(vfsNodeFetchModeEnum fetchMode, int? pagerPageSize)
    {
      module_Items = null;
      try
      {
        int? sNodeItem = null;
        try { sNodeItem = module_fsNode_Item.vNode.snode; }
        catch { }
        //
        Func<FS_Operations, Expression<Func<IKGD_VNODE, bool>>, Expression<Func<IKGD_VDATA, bool>>, IQueryable<FS_Operations.FS_NodeInfo_Interface>> fetcher = null;
        //
        switch (fetchMode)
        {
          case vfsNodeFetchModeEnum.vNode_vData_iNode_ExtraVariants:
            {
              fetcher = (fsOpLbd, vNodeFilterAll, vDataFilterAll) =>
              {
                return
                  from vNode in fsOpLbd.NodesActive<IKGD_VNODE>().Where(vNodeFilterAll)
                  from vData in fsOpLbd.NodesActive<IKGD_VDATA>().Where(vDataFilterAll).Where(n => n.rnode == vNode.rnode)
                  from iNode in fsOpLbd.NodesActive<IKGD_INODE>().Where(n => n.rnode == vNode.rnode).DefaultIfEmpty()
                  join rel in fsOpLbd.NodesActive<IKGD_RELATION>() on vNode.rnode equals rel.rnode into rels
                  join prp in fsOpLbd.NodesActive<IKGD_PROPERTY>() on vNode.rnode equals prp.rnode into prps
                  join vrs in fsOpLbd.DB.IKATT_AttributeMappings on vNode.rnode equals vrs.rNode into variants
                  select new FS_Operations.FS_NodeInfoExt2 { vNode = vNode, vData = vData, iNode = iNode, Relations = rels.ToList(), Properties = prps.ToList(), Variants = variants.ToList() } as FS_Operations.FS_NodeInfo_Interface;
              };
            }
            break;
          case vfsNodeFetchModeEnum.vNode_vData_iNode_Extra:
            {
              fetcher = (fsOpLbd, vNodeFilterAll, vDataFilterAll) =>
              {
                return
                  from vNode in fsOpLbd.NodesActive<IKGD_VNODE>().Where(vNodeFilterAll)
                  from vData in fsOpLbd.NodesActive<IKGD_VDATA>().Where(vDataFilterAll).Where(n => n.rnode == vNode.rnode)
                  from iNode in fsOpLbd.NodesActive<IKGD_INODE>().Where(n => n.rnode == vNode.rnode).DefaultIfEmpty()
                  join rel in fsOpLbd.NodesActive<IKGD_RELATION>() on vNode.rnode equals rel.rnode into rels
                  join prp in fsOpLbd.NodesActive<IKGD_PROPERTY>() on vNode.rnode equals prp.rnode into prps
                  select new FS_Operations.FS_NodeInfoExt { vNode = vNode, vData = vData, iNode = iNode, Relations = rels.ToList(), Properties = prps.ToList() } as FS_Operations.FS_NodeInfo_Interface;
              };
            }
            break;
          case vfsNodeFetchModeEnum.vNode_vData_iNode:
            {
              fetcher = (fsOpLbd, vNodeFilterAll, vDataFilterAll) =>
              {
                return
                  from vNode in fsOpLbd.NodesActive<IKGD_VNODE>().Where(vNodeFilterAll)
                  from vData in fsOpLbd.NodesActive<IKGD_VDATA>().Where(vDataFilterAll).Where(n => n.rnode == vNode.rnode)
                  from iNode in fsOpLbd.NodesActive<IKGD_INODE>().Where(n => n.rnode == vNode.rnode).DefaultIfEmpty()
                  select new FS_Operations.FS_NodeInfo { vNode = vNode, vData = vData, iNode = iNode } as FS_Operations.FS_NodeInfo_Interface;
              };
            }
            break;
          case vfsNodeFetchModeEnum.vNode_vData:
          default:
            {
              fetcher = (fsOpLbd, vNodeFilterAll, vDataFilterAll) =>
              {
                return
                  from vNode in fsOpLbd.NodesActive<IKGD_VNODE>().Where(vNodeFilterAll)
                  from vData in fsOpLbd.NodesActive<IKGD_VDATA>().Where(vDataFilterAll).Where(n => n.rnode == vNode.rnode)
                  select new FS_Operations.FS_NodeInfo { vNode = vNode, vData = vData } as FS_Operations.FS_NodeInfo_Interface;
              };
            }
            break;
        }
        //
        module_Items = moduleCollectorManager.GetIndexItems<FS_Operations.FS_NodeInfoExt>(this.fsOp, sNodeItem, indexPath, fetcher, ref module_IndexTreeNode, ref module_fsNode_Item, ref _module_BreadCrumbs);
        //
        if (pagerPageSize.GetValueOrDefault(0) > 0)
        {
          // TODO: paging support
        }
        //
      }
      catch { yield break; }
      //
      if (module_Items != null)
      {
        IKCMS_ModelCMS_Provider.Provider.managerVFS.RegisterNodes(module_Items.AsEnumerable());
        bool status = IKCMS_ModelCMS_Provider.Provider.managerVFS.Enabled;
        // se non vogliamo attivare la ricorsione nel build degli index dei moduli news
        IKCMS_ModelCMS_Provider.Provider.managerVFS.Enabled = Utility.TryParse<bool>(IKGD_Config.AppSettings["IKCMS_EnableRecursionForArchiveBrowserItems"], false);
        //
        IKCMS_ModelCMS_ModelInfo_Interface itemModelInfo = null;
        foreach (var fsNode in module_Items)
        {
          itemModelInfo = IKCMS_ModelCMS_Provider.Provider.FindBestModelMatch(Utility.FindTypeCached(fsNode.vData.manager_type));
          IKCMS_ModelCMS_ArchiveBrowserItem_Interface model = IKCMS_ModelCMS_Provider.Provider.ModelBuild(null, fsNode, itemModelInfo) as IKCMS_ModelCMS_ArchiveBrowserItem_Interface;
          if (model == null)
            continue;
          model.ModelContainerUnBinded = this;
          //TODO: verificare yield con inizializzazione pesante
          yield return model;
        }
        IKCMS_ModelCMS_Provider.Provider.managerVFS.Enabled = status;
      }
    }


    //
    // load degli items da visualizzare nell'index
    //
    public IEnumerable<IKCMS_ModelCMS_ArchiveBrowserItem_Interface> LoadIndexItems(vfsNodeFetchModeEnum fetchMode, int? pagerPageSize)
    {
      foreach (IKCMS_ModelCMS_ArchiveBrowserItem_Interface m in GetIndexItems(fetchMode, pagerPageSize))
      {
        if (m.ModelParent == null)
        {
          m.ModelParent = this;
          m.ModelContainerUnBinded = null;
        }
        //TODO: verificare yield con inizializzazione pesante
        yield return m;
      }
    }


    //
    // generazione delle informazioni necessarie al rendering del calendar e dei relativi items
    //
    public virtual List<object> GetCalendarEventsAjax(int? year, int? month, int? maxItems)
    {
      //
      List<object> events = new List<object>();
      //
      maxItems = maxItems ?? int.MaxValue;
      DateTime dateRef = FS_OperationsHelpers.DateTimeSession;
      try { dateRef = new DateTime(year.Value, month.Value, 1); }
      catch { }
      //
      try
      {
        //
        Func<FS_Operations, Expression<Func<IKGD_VNODE, bool>>, Expression<Func<IKGD_VDATA, bool>>, IQueryable<FS_Operations.FS_NodeInfo_Interface>> fetcher = (fsOpLbd, vNodeFilterAll, vDataFilterAll) =>
        {
          return
            from vNode in fsOpLbd.NodesActive<IKGD_VNODE>().Where(vNodeFilterAll)
            from vData in fsOpLbd.NodesActive<IKGD_VDATA>().Where(vDataFilterAll).Where(n => n.rnode == vNode.rnode)
            select new FS_Operations.FS_NodeInfo { vNode = vNode, vData = vData } as FS_Operations.FS_NodeInfo_Interface;
        };
        //
        // implementazione con FS_Operations.FS_NodeInfo_Interface, si dovrebbe usare un generic coerente con fetchMode
        IKGD_Archive_Filter_Interface itemsCollector = null;
        try { itemsCollector = moduleCollectorManager.itemsFilter; }
        catch { }
        itemsCollector = itemsCollector ?? new IKGD_Archive_Filter_DateRange();
        //
        // TODO: ottimizzare per IKCMS_ModelCMS_Provider.Provider.managerVFS, sembra che non funzioni correttamente l'update delle risorse da FS_Operations.FS_NodeInfo_Interface a FS_Operations.FS_NodeInfoExt_Interface
        //IKCMS_ModelCMS_Provider.Provider.managerVFS.EnsureNodes<FS_Operations.FS_NodeInfoExt>(this.vfsNode.sNode);
        //FS_Operations.FS_NodeInfoExt_Interface fsNodeRel = IKCMS_ModelCMS_Provider.Provider.managerVFS.GetVfsNode(this.vfsNode.sNode, null) as FS_Operations.FS_NodeInfoExt_Interface;
        FS_Operations.FS_NodeInfoExt_Interface fsNodeRel = (this.vfsNode as FS_Operations.FS_NodeInfoExt_Interface) == null ? fsOp.Get_NodeInfoExtACL(this.vfsNode.sNode) : this.vfsNode as FS_Operations.FS_NodeInfoExt_Interface;
        //
        Expression<Func<IKGD_VDATA, bool>> vDataFilterMain = fsOp.Get_vDataFilterACLv2();
        Expression<Func<IKGD_VNODE, bool>> vNodeFilterMain = fsOp.Get_vNodeFilterACLv2();
        List<int> sNodeRoots = fsNodeRel.Relations.Where(r => r.type == IKGD_Constants.IKGD_ArchiveRelationName).OrderBy(r => r.position).Select(r => r.snode_dst).ToList();
        if (sNodeRoots.Any())
        {
          int maxRecursionLevel = 10;
          var nodesTreeFullInfo = fsOp.Get_TreeDataShortGeneric<IKCMS_TreeBrowser_fsNodeElement_Interface>(null, sNodeRoots, itemsCollector.vNodeFilter, itemsCollector.vDataFilter, maxRecursionLevel, true, true);
          List<int> folderSet = nodesTreeFullInfo.RecurseOnTree.Where(n => n.Data != null).Select(n => n.Data.vNode.folder).Distinct().ToList();
          vNodeFilterMain = vNodeFilterMain.And(n => folderSet.Contains(n.folder));
        }
        else
        {
          // nessun filtro aggiuntivo: scan di tutto il VFS
        }
        if (itemsCollector.FilterBrowsableItemsOnly)
        {
          // filtro sulle risorse di tipo browsable
          var typesToScan = IKCMS_RegisteredTypes.Types_IKCMS_BrowsableIndexable_Interface.Select(t => t.Name).ToList();
          if (typesToScan.Any())
            vDataFilterMain = vDataFilterMain.And(n => typesToScan.Contains(n.manager_type));
        }
        if (itemsCollector.vNodeFilter != null)
          vNodeFilterMain = vNodeFilterMain.And(itemsCollector.vNodeFilter);
        if (itemsCollector.vDataFilter != null)
          vDataFilterMain = vDataFilterMain.And(itemsCollector.vDataFilter);
        //
        DateTime dateMonthStart = new DateTime(dateRef.Year, dateRef.Month, 1);
        DateTime dateMonthEnd = dateMonthStart.AddMonths(1);
        Expression<Func<IKGD_VDATA, bool>> vDataFilterMainMonth = vDataFilterMain.And(n => n.date_node < dateMonthEnd && (n.date_node >= dateMonthStart || (n.date_node_aux != null && n.date_node_aux.Value >= dateMonthStart)));
        //
        IQueryable<FS_Operations.FS_NodeInfo_Interface> resourcesAll = fetcher(fsOp, vNodeFilterMain, vDataFilterMainMonth);
        var resources = resourcesAll.GroupBy(n => n.vNode.rnode).Select(g => g.First());
        //
        try { resources = moduleCollectorManager.itemsCollector.Sorter(resources).Take(maxItems.Value); }
        catch { }
        //
        foreach (var fsNode in resources)
        {
          string title = null;
          try
          {
            IKCMS_HasSerializationCMS_Interface data = IKCMS_RegisteredTypes.Deserialize_IKCMS_ResourceVFS(fsNode);
            if (data is IKCMS_ResourceType_NewsKVT)
            {
              try { title = (data as IKCMS_ResourceType_NewsKVT).ResourceSettings.Values["Title"].Value.ToString(); }
              catch { }
            }
          }
          catch { }
          //
          events.Add(new { id = fsNode.sNode, title = title ?? fsNode.vNode.name, start = fsNode.vData.date_node.ToString(@"yyyy-MM-dd HH\:mm\:ss"), finish = (fsNode.vData.date_node_aux ?? fsNode.vData.date_node).ToString(@"yyyy-MM-dd HH\:mm\:ss") });
        }
      }
      catch { }
      return events;
    }


    public List<IKCMS_ModelCMS_Interface> GetHomeItems_Latest(int? MaxItems)
    {
      return GetHomeItems(new IKGD_Teaser_Collector_NewsEventsDateDesc<FS_Operations.FS_NodeInfo_Interface>(), vfsNodeFetchModeEnum.vNode_vData_iNode, this, MaxItems);
    }
    public List<IKCMS_ModelCMS_Interface> GetHomeItems_Next(int? MaxItems)
    {
      return GetHomeItems(new IKGD_Teaser_Collector_NewsEventsNext<FS_Operations.FS_NodeInfo_Interface>(), vfsNodeFetchModeEnum.vNode_vData_iNode, this, MaxItems);
    }


    public List<IKCMS_ModelCMS_Interface> GetHomeItems(IKGD_Teaser_Collector_Interface<FS_Operations.FS_NodeInfo_Interface> itemsCollector, vfsNodeFetchModeEnum fetchMode, IKCMS_ModelCMS_Interface modelReference, int? MaxItems)
    {
      List<IKCMS_ModelCMS_Interface> itemsModels = null;
      if (Utility.TryParse<bool>(IKGD_Config.AppSettingsWeb["CachingIKCMS_ModelsEnabled"], true) && Utility.TryParse<bool>(System.Web.HttpContext.Current.Request.QueryString["cacheOff"]) == false)
      {
        string cacheKey = FS_OperationsHelpers.ContextHash(this.GetType().Name, "GetHomeItems", this.rNode, itemsCollector.GetType().Name, fetchMode, ((modelReference != null) ? (int?)modelReference.rNode : null), MaxItems);
        itemsModels = FS_OperationsHelpers.CachedEntityWrapper(cacheKey, () =>
        {
          return GetHomeItemsWorker(itemsCollector, fetchMode, modelReference, MaxItems).ToList();
        }
        , Utility.TryParse<int>(IKGD_Config.AppSettings["CachingIKCMS_Models"], 3600), FS_OperationsHelpers.Const_CacheDependencyIKGD_vNode_vData_iNode_Relation);
      }
      else
      {
        itemsModels = GetHomeItemsWorker(itemsCollector, fetchMode, modelReference, MaxItems).ToList();
      }
      return itemsModels;
    }


    //
    // es.  itemsCollector = new IKGD_Teaser_Collector_NewsEventsOrderVFS<FS_Operations.FS_NodeInfo_Interface>()
    //
    public IEnumerable<IKCMS_ModelCMS_Interface> GetHomeItemsWorker(IKGD_Teaser_Collector_Interface<FS_Operations.FS_NodeInfo_Interface> itemsCollector, vfsNodeFetchModeEnum fetchMode, IKCMS_ModelCMS_Interface modelReference, int? MaxItems)
    {
      IQueryable<FS_Operations.FS_NodeInfo_Interface> resources = null;
      try
      {
        //
        MaxItems = MaxItems ?? 10;
        //
        Func<FS_Operations, Expression<Func<IKGD_VNODE, bool>>, Expression<Func<IKGD_VDATA, bool>>, IQueryable<FS_Operations.FS_NodeInfo_Interface>> fetcher = null;
        //
        switch (fetchMode)
        {
          case vfsNodeFetchModeEnum.vNode_vData_iNode_ExtraVariants:
            {
              fetcher = (fsOpLbd, vNodeFilterAll, vDataFilterAll) =>
              {
                return
                  from vNode in fsOpLbd.NodesActive<IKGD_VNODE>().Where(vNodeFilterAll)
                  from vData in fsOpLbd.NodesActive<IKGD_VDATA>().Where(vDataFilterAll).Where(n => n.rnode == vNode.rnode)
                  from iNode in fsOpLbd.NodesActive<IKGD_INODE>().Where(n => n.rnode == vNode.rnode).DefaultIfEmpty()
                  join rel in fsOpLbd.NodesActive<IKGD_RELATION>() on vNode.rnode equals rel.rnode into rels
                  join prp in fsOpLbd.NodesActive<IKGD_PROPERTY>() on vNode.rnode equals prp.rnode into prps
                  join vrs in fsOpLbd.DB.IKATT_AttributeMappings on vNode.rnode equals vrs.rNode into variants
                  select new FS_Operations.FS_NodeInfoExt2 { vNode = vNode, vData = vData, iNode = iNode, Relations = rels.ToList(), Properties = prps.ToList(), Variants = variants.ToList() } as FS_Operations.FS_NodeInfo_Interface;
              };
            }
            break;
          case vfsNodeFetchModeEnum.vNode_vData_iNode_Extra:
            {
              fetcher = (fsOpLbd, vNodeFilterAll, vDataFilterAll) =>
              {
                return
                  from vNode in fsOpLbd.NodesActive<IKGD_VNODE>().Where(vNodeFilterAll)
                  from vData in fsOpLbd.NodesActive<IKGD_VDATA>().Where(vDataFilterAll).Where(n => n.rnode == vNode.rnode)
                  from iNode in fsOpLbd.NodesActive<IKGD_INODE>().Where(n => n.rnode == vNode.rnode).DefaultIfEmpty()
                  join rel in fsOpLbd.NodesActive<IKGD_RELATION>() on vNode.rnode equals rel.rnode into rels
                  join prp in fsOpLbd.NodesActive<IKGD_PROPERTY>() on vNode.rnode equals prp.rnode into prps
                  select new FS_Operations.FS_NodeInfoExt { vNode = vNode, vData = vData, iNode = iNode, Relations = rels.ToList(), Properties = prps.ToList() } as FS_Operations.FS_NodeInfo_Interface;
              };
            }
            break;
          case vfsNodeFetchModeEnum.vNode_vData_iNode:
            {
              fetcher = (fsOpLbd, vNodeFilterAll, vDataFilterAll) =>
              {
                return
                  from vNode in fsOpLbd.NodesActive<IKGD_VNODE>().Where(vNodeFilterAll)
                  from vData in fsOpLbd.NodesActive<IKGD_VDATA>().Where(vDataFilterAll).Where(n => n.rnode == vNode.rnode)
                  from iNode in fsOpLbd.NodesActive<IKGD_INODE>().Where(n => n.rnode == vNode.rnode).DefaultIfEmpty()
                  select new FS_Operations.FS_NodeInfo { vNode = vNode, vData = vData, iNode = iNode } as FS_Operations.FS_NodeInfo_Interface;
              };
            }
            break;
          case vfsNodeFetchModeEnum.vNode_vData:
          default:
            {
              fetcher = (fsOpLbd, vNodeFilterAll, vDataFilterAll) =>
              {
                return
                  from vNode in fsOpLbd.NodesActive<IKGD_VNODE>().Where(vNodeFilterAll)
                  from vData in fsOpLbd.NodesActive<IKGD_VDATA>().Where(vDataFilterAll).Where(n => n.rnode == vNode.rnode)
                  select new FS_Operations.FS_NodeInfo { vNode = vNode, vData = vData } as FS_Operations.FS_NodeInfo_Interface;
              };
            }
            break;
        }
        //
        // implementazione con FS_Operations.FS_NodeInfo_Interface, si dovrebbe usare un generic coerente con fetchMode
        itemsCollector = itemsCollector ?? new IKGD_Teaser_Collector_NewsEventsOrderVFS<FS_Operations.FS_NodeInfo_Interface>();
        //
        Expression<Func<IKGD_VDATA, bool>> vDataFilterMain = fsOp.Get_vDataFilterACLv2();
        Expression<Func<IKGD_VNODE, bool>> vNodeFilterMain = fsOp.Get_vNodeFilterACLv2();
        FS_Operations.FS_NodeInfoExt_Interface fsNodeRel = (this.vfsNode as FS_Operations.FS_NodeInfoExt_Interface) == null ? fsOp.Get_NodeInfoExtACL(this.vfsNode.sNode) : this.vfsNode as FS_Operations.FS_NodeInfoExt_Interface;
        List<int> sNodeRoots = fsNodeRel.Relations.Where(r => r.type == IKGD_Constants.IKGD_ArchiveRelationName).OrderBy(r => r.position).Select(r => r.snode_dst).ToList();
        if (sNodeRoots.Any())
        {
          int maxRecursionLevel = 10;
          var nodesTreeFullInfo = fsOp.Get_TreeDataShortGeneric<IKCMS_TreeBrowser_fsNodeElement_Interface>(null, sNodeRoots, itemsCollector.vNodeFilter, itemsCollector.vDataFilter, maxRecursionLevel, true, true);
          List<int> folderSet = nodesTreeFullInfo.RecurseOnTree.Where(n => n.Data != null).Select(n => n.Data.vNode.folder).Distinct().ToList();
          vNodeFilterMain = vNodeFilterMain.And(n => folderSet.Contains(n.folder));
        }
        else
        {
          // nessun filtro aggiuntivo: scan di tutto il VFS
        }
        if (itemsCollector.FilterBrowsableItemsOnly)
        {
          // filtro sulle risorse di tipo browsable
          var typesToScan = IKCMS_RegisteredTypes.Types_IKCMS_BrowsableIndexable_Interface.Select(t => t.Name).ToList();
          if (typesToScan.Any())
            vDataFilterMain = vDataFilterMain.And(n => typesToScan.Contains(n.manager_type));
        }
        if (itemsCollector.vNodeFilter != null)
          vNodeFilterMain = vNodeFilterMain.And(itemsCollector.vNodeFilter);
        if (itemsCollector.vDataFilter != null)
          vDataFilterMain = vDataFilterMain.And(itemsCollector.vDataFilter);
        //
        resources = fetcher(fsOp, vNodeFilterMain, vDataFilterMain);
        //var tmp01 = resources.ToList();
        //
        // visto che si possono usare i symlink ragruppo tutto per rnode prima del take
        resources = resources.GroupBy(n => n.vNode.rnode).Select(g => g.First());
        resources = itemsCollector.Sorter(resources);
        //
        if (MaxItems > 0)
          resources = resources.Take(MaxItems.Value);
        //
        if (itemsCollector is IKGD_Collector_ReverseResultsAfterTake)
          resources = resources.AsEnumerable().Reverse().AsQueryable();
        //
      }
      catch (Exception ex)
      {
        Elmah.ErrorSignal.FromCurrentContext().Raise(ex);
        IKCMS_ExceptionsManager.Add(new IKCMS_Exception_ModelBuilder(this.GetType().FullName + ".GetHomeItemsWorker", ex));
        if (modelReference != null)
          modelReference.HasExceptions |= true;
        yield break;
      }
      //
      if (resources != null)
      {
        IKCMS_ModelCMS_ModelInfo_Interface itemModelInfo = null;
        foreach (var fsNode in resources)
        {
          IKCMS_ModelCMS_Interface model = null;
          try
          {
            itemModelInfo = IKCMS_ModelCMS_Provider.Provider.FindBestModelMatch(Utility.FindTypeCached(fsNode.vData.manager_type));
            model = IKCMS_ModelCMS_Provider.Provider.ModelBuild(null, fsNode, itemModelInfo);
            if (model is IKCMS_ModelCMS_ArchiveBrowserItem_Interface)
              (model as IKCMS_ModelCMS_ArchiveBrowserItem_Interface).ModelContainerUnBinded = this;
          }
          catch (Exception ex)
          {
            Elmah.ErrorSignal.FromCurrentContext().Raise(ex);
            IKCMS_ExceptionsManager.Add(new IKCMS_Exception_ModelBuilder(this.GetType().FullName + ".GetHomeItemsWorker[RECURSION]", ex));
            if (modelReference != null)
              modelReference.HasExceptions |= true;
          }
          if (model == null)
            continue;
          //TODO: verificare yield con inizializzazione pesante
          yield return model;
        }
      }
      //
    }

  }  //class IKCMS_ModelCMS_ArchiveBrowser<T>




  //
  // TODO:
  // rimuovere completamente dalla libreria IKCMS_ModelCMS_Event4PageItem_Interface, IKCMS_ModelCMS_Event4PageItem<T>, IKCMS_ResourceType_Event4PageKVT
  // che sono usati solo da vecchi siti
  // ci sono riferimenti a questa interface/classe in IKCMS_Components: CalendarHelpers, IKCMS_HelpersController
  // poi altri riferimenti vari in files spark sparsi nei vari progetti.
  // select count(*) from ikgd_vdata where (flag_current=1 or flag_published=1) and manager_type='IKCMS_ResourceType_Event4PageKVT'
  //
  public interface IKCMS_ModelCMS_Event4PageItem_Interface : IKCMS_ModelCMS_Interface, IKCMS_ModelCMS_Widget_Interface, IKCMS_ModelCMS_HasTemplateInfo_Interface
  {
  }


  //
  // model per gli items degli eventi associati a pagine CMS e non a moduli di browsing
  // sono equivalenti ad una pagina ma non sono un folder, inoltre prevedono che come submodel vengano inizializzate
  // la/le pagine che contengono un reference all'item/event selezionato
  //
  [IKCMS_ModelCMS_ResourceTypes(typeof(IKCMS_Widget_Interface), typeof(IKCMS_HasSerializationCMS_Interface))]
  [IKCMS_ModelCMS_ResourceTypes(typeof(IKCMS_ResourceType_Event4PageKVT))]
  [IKCMS_ModelCMS_RecursionMode(ModelRecursionModeEnum.RecursionNone)]
  [IKCMS_ModelCMS_fsNodeMode(vfsNodeFetchModeEnum.vNode_vData_iNode_Extra)]
  [IKCMS_ModelCMS_Priority(-2499900)]
  public class IKCMS_ModelCMS_Event4PageItem<T> : IKCMS_ModelCMS_WidgetCMS<T>, IKCMS_ModelCMS_Event4PageItem_Interface
    where T : class, IKCMS_HasSerializationCMS_Interface
  {
    //
    // gestione della view e del template di visualizzazione
    // da utilizzare direttamente nella action e nella view
    //
    public IKCMS_PageCMS_Template_Interface TemplateInfo { get; set; }
    public string TemplateViewPath { get; set; }
    //

    //
    // continuazione del setup del model dopo le features di base processate in IKCMS_ModelCMS
    //
    protected override void SetupFinalize(params object[] args)
    {
      base.SetupFinalize(args);
      //
      // se il model non ha nessun parent e' stata richiesta la pagina dell'evento
      // altrimenti si tratta di eventi presenti in una pagina CMS
      // se non ho parent devo caricare dei model con le pagine alle quali e' collegato l'evento
      //
      if (ModelParent == null)
      {
        managerVFS.FetchNodes(vn => vn.flag_folder && vn.folder == this.vfsNode.Folder, null, vfsNodeFetchModeEnum.vNode_vData_iNode, null, false);
        var parentNodes = managerVFS.NodesVFS.Where(n => n.vNode.flag_folder && n.vNode.folder == this.vfsNode.Folder).ToList();
        parentNodes.ForEach(n => Models.Add(new IKCMS_ModelCMS_Dumb(this, n)));
      }
      //
      try
      {
        // assegnazione dei templates solo per la pagine principali (non per widget e subPages del catalogo)
        // TODO: da riverificare questa gestione dei templates che mi convince poco
        //string resourceTemplateType = Utility.FindPropertySafe<string>(VFS_ResourceObjectData, "PageTemplateType");
        string resourceTemplateType = this.TemplateVnode.NullIfEmpty() ?? Utility.FindPropertySafe<string>(VFS_ResourceObjectData, "PageTemplateType");
        TemplateInfo = IKCMS_TemplatesTypeHelper.GetTemplateForType(this.VFS_Resource.GetType(), resourceTemplateType, this.Category, this.Placeholder);
        TemplateViewPath = TemplateInfo.ViewPath;
      }
      catch { }
      //
    }


  }


}  //namespace
