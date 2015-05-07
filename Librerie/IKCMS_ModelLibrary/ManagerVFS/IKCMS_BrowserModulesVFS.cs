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
using System.Security;
using System.Linq.Expressions;
using System.Reflection;
using System.Data.Linq.Mapping;
using System.Diagnostics;
using System.Transactions;
using System.Web.Caching;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;  // assembly System.ServiceModel.Web
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
using Ikon.IKGD.Library;


namespace Ikon.IKCMS
{


  public class IKCMS_TreeModuleData<fsNodeT>
    where fsNodeT : class, FS_Operations.FS_NodeInfo_Interface
  {
    public FS_Operations.FS_NodeInfo fsNode_Module { get; set; }
    public IKGD_Path vfsPathModule { get; set; }
    public List<int> rNodeRoots { get; set; }
    public IKCMS_HasSerializationCMS_Interface moduleInfo { get; set; }
    public IKGD_Archive_Filter_Interface itemsFilter { get; set; }
    public IKGD_Archive_Collector_Interface<fsNodeT> itemsCollector { get; set; }
    public FS_Operations.FS_TreeNode<IKCMS_TreeBrowser_Element_Interface> nodesTree { get; set; }
    public bool IsInitialized { get; set; }
    //


    public IKCMS_TreeModuleData(int? sNode_module, string pathModule)
    {
      IsInitialized = false;
      using (FS_Operations fsOp = new FS_Operations())
      {
        Setup(fsOp, sNode_module, pathModule);
      }
    }

    public IKCMS_TreeModuleData(FS_Operations fsOp, int? sNode_module, string pathModule)
    {
      Setup(fsOp, sNode_module, pathModule);
    }


    public bool Setup(FS_Operations fsOp, int? sNode_module, string pathModule)
    {
      IsInitialized = false;
      bool res01 = LoadModule(fsOp, pathModule, sNode_module);
      bool res02 = BuildFullTree(fsOp);
      return IsInitialized;
    }

    //
    // setup delle info relative al modulo di browsing
    //
    protected bool LoadModule(FS_Operations fsOp, string pathModule, int? sNode_module)
    {
      if (sNode_module != null)
        vfsPathModule = fsOp.PathsFromNodeExt(sNode_module.Value).FirstOrDefault();
      else
      {
        var paths = fsOp.PathsFromString(pathModule, true);
        vfsPathModule = paths.FilterPathsByLanguage().FirstOrDefault() ?? paths.FirstOrDefault();
      }
      if (vfsPathModule == null)
        return false;
      fsNode_Module = fsOp.Get_NodeInfoACL(vfsPathModule.sNode, false, false);
      rNodeRoots = fsOp.Get_RELATIONs(fsNode_Module.vNode).Where(r => r.type == IKGD_Constants.IKGD_ArchiveRelationName).OrderBy(r => r.position).Select(r => r.rnode_dst).Distinct().ToList();
      //
      moduleInfo = IKCMS_RegisteredTypes.Deserialize_IKCMS_ResourceVFS(fsNode_Module);
      object moduleInfoSettings = moduleInfo.ResourceSettingsObject;
      string archiveFilterType = Utility.FindPropertySafe<string>(moduleInfoSettings, "ArchiveFilterType");
      string archiveCollectorType = Utility.FindPropertySafe<string>(moduleInfoSettings, "ArchiveCollectorType");
      //
      if (itemsFilter == null)
      {
        try { itemsFilter = (IKGD_Archive_Filter_Interface)Activator.CreateInstance(Utility.FindTypeGeneric(archiveFilterType, typeof(fsNodeT))); }
        catch { }
      }
      if (itemsFilter == null)
      {
        try { itemsFilter = (IKGD_Archive_Filter_Interface)Activator.CreateInstance(Utility.FindTypeGeneric(archiveFilterType)); }
        catch { }
      }
      try { itemsCollector = (IKGD_Archive_Collector_Interface<fsNodeT>)Activator.CreateInstance(Utility.FindTypeGeneric(archiveCollectorType, typeof(fsNodeT))); }
      catch { }
      itemsCollector = itemsCollector ?? new IKGD_Archive_Collector_NewsWithFoldersGeneral<fsNodeT>();
      itemsFilter = itemsFilter ?? new IKGD_Archive_Filter_NULL();
      //
      return true;
    }


    //
    // TODO:
    // bisogna aggiungere un nodo fittizio per raccogliere TUTTI i contenuti che non appartengono a subfolders
    // aggregandoli da tutti gli archivi. Il sistema attualmente implementato crea dei problemi nel rendering e filtering
    // dei nodi, non ha un level coerente e NON funziona correttamente per gli elementi in root quando si aggregano piu' archivi
    // soluzione:
    // aggiungere un nodo fittizio al tree per raccogliere tutti i contenuti di root
    // modificare il loop per saltare i nodi root dal processing e catturare il processing per il nuovo nodo speciale
    // modificare le funzioni di GetItemInfo e BuildBrowseTreeMenu (verificare anche la generazione dell'index)
    // nello scan eliminare i nodi del tree che non hanno nessun item attivo (folders vuoti / grouping con 0 elementi)
    //
    protected bool BuildFullTree(FS_Operations fsOp)
    {
      nodesTree = new FS_Operations.FS_TreeNode<IKCMS_TreeBrowser_Element_Interface>(null, default(IKCMS_TreeBrowser_Element_Interface));
      FS_Operations.FS_TreeNode<IKCMS_TreeBrowser_fsNodeElement_Interface> nodesTreeFullInfo = new FS_Operations.FS_TreeNode<IKCMS_TreeBrowser_fsNodeElement_Interface>(null, default(IKCMS_TreeBrowser_fsNodeElement_Interface));
      //
      if (itemsCollector == null)
        return false;
      int? maxRecursionLevel = itemsCollector.ScanSubTree ? (int?)null : 0;
      nodesTreeFullInfo = fsOp.Get_TreeDataShortGeneric<IKCMS_TreeBrowser_fsNodeElement_Interface>(rNodeRoots, null, itemsFilter.vNodeFilter, itemsFilter.vDataFilter, maxRecursionLevel, true, true);
      List<int> folderSet = nodesTreeFullInfo.RecurseOnTree.Where(n => n.Data != null).Select(n => n.Data.vNode.folder).Distinct().ToList();
      //
      AutoMapperWrapper.AutoRegister<fsNodeT, IKCMS_TreeBrowser_fsNodeElement_Interface>();
      //
      foreach (FS_Operations.FS_TreeNode<IKCMS_TreeBrowser_fsNodeElement_Interface> subTreeRoot in nodesTreeFullInfo.RecurseOnTree.Where(n => n.Data != null))
      {
        try { subTreeRoot.Data.frag = subTreeRoot.Data.fragString = subTreeRoot.Data.vNode.name; }
        catch { }
      }
      if (!itemsCollector.RenderTree)
      {
        // per lo scan di tutto il tree rendo not null il primo .Data
        // in modo che mi resti in testa al tree la struttura di directory che poi posso saltare nella generazione del tree
        // basta saltare i nodi che hanno settato .folderNode
        nodesTreeFullInfo.Data = AutoMapperWrapper.Map<IKCMS_TreeBrowser_fsNodeElement_Interface>(new FS_Operations.FS_NodeInfo());
      }
      // uso un .ToList() perche' nel loop altero il tree
      var firstArchiveNode = nodesTreeFullInfo.RecurseOnTree.Where(n => n.Data != null).FirstOrDefault(n => n.Level == 1);
      foreach (FS_Operations.FS_TreeNode<IKCMS_TreeBrowser_fsNodeElement_Interface> subTreeRoot in nodesTreeFullInfo.RecurseOnTree.Where(n => n.Data != null).ToList())
      {
        //
        // grouping ricorsivo degli elementi
        //
        Expression<Func<IKGD_VDATA, bool>> vDataFilterAll = fsOp.Get_vDataFilterACLv2();
        Expression<Func<IKGD_VNODE, bool>> vNodeFilterAll = fsOp.Get_vNodeFilterACLv2();
        //
        vNodeFilterAll = vNodeFilterAll.And(n => n.flag_folder == false);
        //
        if (itemsFilter != null)
        {
          if (itemsFilter.vNodeFilter != null)
          {
            vNodeFilterAll = vNodeFilterAll.And(itemsFilter.vNodeFilter);
          }
          if (itemsFilter.vDataFilter != null)
          {
            vDataFilterAll = vDataFilterAll.And(itemsFilter.vDataFilter);
          }
        }
        //
        if (itemsCollector.RenderTree)
        {
          if (subTreeRoot.Level == 1)
          {
            if (subTreeRoot == firstArchiveNode)
            {
              List<int> folderRootsSet = nodesTreeFullInfo.RecurseOnTree.Where(n => n.Data != null && n.Level == 1).Select(n => n.Data.vNode.folder).Distinct().ToList();
              vNodeFilterAll = vNodeFilterAll.And(n => folderRootsSet.Contains(n.folder));  // scan di tutte le rootdir
            }
            else
              continue;  // ho gia' processato tutti i root nodes
          }
          else
            vNodeFilterAll = vNodeFilterAll.And(n => n.folder == subTreeRoot.Data.vNode.folder);  // scan della singola subdir
        }
        else
        {
          // scan di tutti i folder tanto poi interrompo il loop con un break
          vNodeFilterAll = vNodeFilterAll.And(n => folderSet.Contains(n.folder));  // scan di tutto il tree
        }
        //
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
        IQueryable<fsNodeT> resources =
          from vNode in fsOp.NodesActive<IKGD_VNODE>().Where(vNodeFilterAll)
          from vData in fsOp.NodesActive<IKGD_VDATA>().Where(vDataFilterAll).Where(n => n.rnode == vNode.rnode)
          select new FS_Operations.FS_NodeInfo { vNode = vNode, vData = vData } as fsNodeT;
        //
        resources = itemsCollector.Sorter(resources);
        CollectorWorkerGeneric(subTreeRoot, resources, itemsCollector.AggregatorsOrderByDescendant ?? new List<bool>(), itemsCollector.Aggregators, itemsCollector.Formatters);
        //
        if (!itemsCollector.RenderTree)
        {
          // interruzione dello scan loop nel caso si processi tutto il tree globalmente
          subTreeRoot.Data = null;
          break;
        }
      }  //foreach
      if (firstArchiveNode != null)
      {
        firstArchiveNode.Parent = null;
        firstArchiveNode.Parent = nodesTreeFullInfo;
      }
      int fragsToSkipForInfoPath = itemsCollector.RenderTree ? 1 : 0;
      string langFallBack = vfsPathModule.FirstLanguageNN;
      nodesTreeFullInfo.RecurseOnTree.Where(n => n.Data != null).ForEach(n => n.Data.url = IKCMS_RouteUrlManager.GetMvcUrlGeneralV2(n.Data.vNode.language ?? langFallBack, n.Data.vNode.snode, vfsPathModule.sNode, n.GetInfoPath(fragsToSkipForInfoPath), false));
      //nodesTreeFullInfo.RecurseOnTree.Where(n => n.Data != null).ForEach(n => n.Data.url = IKCMS_RouteUrlManager.GetMvcUrlGeneral(vfsPathModule.sNode, n.Data.vNode.snode, n.GetInfoPath(fragsToSkipForInfoPath)));
      //var treeDBG = nodesTreeFullInfo.RecurseOnTree.ToList();
      //
      // copio il tree con le info VFS nel tree low weight (uso automapper)
      //
      AutoMapperWrapper.AutoRegister<IKCMS_TreeBrowser_fsNodeElement_Interface, IKCMS_TreeBrowser_Element_Interface>(m =>
      {
        m.ForMember(d => d.sNode, opt => opt.MapFrom(s => s.vNode.snode));
        m.ForMember(d => d.folderNode, opt => opt.MapFrom(s => s.vNode.flag_folder ? (int?)s.vNode.folder : null));
      });
      //
      Action<FS_Operations.FS_TreeNode<IKCMS_TreeBrowser_fsNodeElement_Interface>, FS_Operations.FS_TreeNode<IKCMS_TreeBrowser_Element_Interface>> treeMapper = null;
      treeMapper = (srcNode, dstNode) =>
      {
        foreach (var subNodeSrc in srcNode.Nodes)
        {
          FS_Operations.FS_TreeNode<IKCMS_TreeBrowser_Element_Interface> subNodeDst = new FS_Operations.FS_TreeNode<IKCMS_TreeBrowser_Element_Interface>(dstNode, AutoMapper.Mapper.Map<IKCMS_TreeBrowser_fsNodeElement_Interface, IKCMS_TreeBrowser_Element_Interface>(subNodeSrc.Data));
          treeMapper.Invoke(subNodeSrc, subNodeDst);
        }
      };
      treeMapper.Invoke(nodesTreeFullInfo, nodesTree);
      //
      // scan del tree per settare tutte le url a quelle della prima leaf
      //
      nodesTree.RecurseOnTree.Where(n => n.Data != null).Where(n => n.Nodes.Count > 0).ForEach(n =>
      {
        try { n.Data.url = n.RecurseOnTree.FirstOrDefault(tn => tn.Nodes.Count == 0).Data.url; }
        catch { }
      });
      //var treeLight = nodesTree.RecurseOnTree.ToList();
      //
      IsInitialized = true;
      return true;
    }


    protected void CollectorWorkerGeneric<treeNodeT>(FS_Operations.FS_TreeNode<treeNodeT> subTreeRoot, IQueryable<fsNodeT> activeNodes, IEnumerable<bool> aggregatorsOrderByDescendant, IEnumerable<Func<fsNodeT, object>> aggregators, IEnumerable<Func<fsNodeT, string>> formatters)
      where treeNodeT : IKCMS_TreeBrowser_fsNodeElement_Interface
    {
      if (aggregators == null || aggregators.Count() == 0)
        return;
      var groupedSet = (aggregatorsOrderByDescendant.FirstOrDefault()) ? activeNodes.GroupBy(aggregators.FirstOrDefault()).OrderByDescending(g => g.Key) : activeNodes.GroupBy(aggregators.FirstOrDefault()).OrderBy(g => g.Key);
      foreach (var frag in groupedSet)
      {
        fsNodeT firstItem = frag.FirstOrDefault();
        treeNodeT node;
        try { node = AutoMapper.Mapper.Map<fsNodeT, treeNodeT>(firstItem); }
        catch { node = AutoMapper.Mapper.DynamicMap<treeNodeT>(firstItem); }
        node.frag = frag.Key;
        try { node.fragString = (formatters.FirstOrDefault() != null) ? formatters.FirstOrDefault().Invoke(firstItem) : node.frag.ToString(); }
        catch { }
        node.fragString = node.fragString ?? string.Empty;
        FS_Operations.FS_TreeNode<treeNodeT> newNode = new FS_Operations.FS_TreeNode<treeNodeT>(subTreeRoot, node);
        if (aggregators.Count() > 1)
        {
          CollectorWorkerGeneric<treeNodeT>(newNode, frag.AsQueryable(), aggregatorsOrderByDescendant.Skip(1), aggregators.Skip(1), formatters.Skip(1));
        }
      }
    }



    public bool GetItemInfo<fsNodeT2>(FS_Operations fsOp, int? sNode_item, string infoPath,
      out FS_Operations.FS_TreeNode<IKCMS_TreeBrowser_Element_Interface> indexTreeNode,
      out fsNodeT2 fsNode_Item,
      out List<string> breadcrumbs)
      where fsNodeT2 : class, FS_Operations.FS_NodeInfo_Interface
    {
      fsNode_Item = null;
      indexTreeNode = null;
      breadcrumbs = new List<string>();
      //
      try
      {
        List<object> infoPathFrags = new List<object>();
        if (sNode_item != null)
        {
          //
          // costruzione dei path fragments dal sNode
          //
          fsNode_Item = (typeof(FS_Operations.FS_NodeInfoExt).IsAssignableFrom(typeof(fsNodeT2)) ? fsOp.Get_NodeInfoExtACL(sNode_item.Value, false) : fsOp.Get_NodeInfoACL(sNode_item.Value, false, false)) as fsNodeT2;
          if (itemsCollector.RenderTree)
          {
            int? folderNode = fsNode_Item.vNode.folder;
            var vfsNode = nodesTree.RecurseOnTree.Where(n => n.Data != null).FirstOrDefault(n => n.Data.folderNode == folderNode);
            vfsNode.BackRecurseOnData.Where(n => n != null).Reverse().Skip(1).ForEach(n => infoPathFrags.Add(n.frag));
          }
          foreach (var coll in itemsCollector.Aggregators)
            infoPathFrags.Add(coll.Invoke(fsNode_Item as fsNodeT));
        }
        else if (!string.IsNullOrEmpty(infoPath))
        {
          Utility.Explode(infoPath, "/").Skip(1).ForEach(f => infoPathFrags.Add(f));
        }
        //
        // normalizzazione del path: ricerca nel tree e selezione della leaf
        //
        try
        {
          var nodes = itemsCollector.RenderTree ? nodesTree.Nodes.AsEnumerable() : Enumerable.Repeat(nodesTree, 1);
          foreach (object frag in infoPathFrags)
          {
            object fragLocal = frag; // per i soliti problemi di LINQ con le variabili dei loop
            nodes = nodes.SelectMany(n => n.Nodes).Where(n => n.Data.frag.Equals(fragLocal) || fragLocal.ToString() == n.Data.frag.ToString());
          }
          indexTreeNode = nodes.FirstOrDefault();
        }
        catch { }
        indexTreeNode = indexTreeNode ?? nodesTree;
        // forzatura del nodo alla prima leaf
        //while (indexTreeNode.Nodes.Count > 0)
        //  indexTreeNode = indexTreeNode.Nodes.FirstOrDefault();
        // patch 2012/06/04 per problemi di gestione view index per archivi news aggregati multipli cha altrimenti ritornava il primo folder con le news
        indexTreeNode = indexTreeNode.RecurseOnTree.Where(n => n.Data != null && n.Data.folderNode == null && !n.Nodes.Any()).FirstOrDefault() ?? indexTreeNode;
        //
        // non e' stato trovato nessun nodo che soddisfi sNode o infoPath
        if (indexTreeNode == null || indexTreeNode.Data == null)
          return false;
        if (fsNode_Item == null)
          fsNode_Item = (typeof(FS_Operations.FS_NodeInfoExt).IsAssignableFrom(typeof(fsNodeT2)) ? fsOp.Get_NodeInfoExtACL(indexTreeNode.Data.sNode, false) : fsOp.Get_NodeInfoACL(indexTreeNode.Data.sNode, false, false)) as fsNodeT2;
        //if (fsNode_Item != null && typeof(FS_Operations.FS_NodeInfoExt).IsAssignableFrom(typeof(fsNodeT2)))
        //{
        //  (fsNode_Item as FS_Operations.FS_NodeInfoExt).Relations = fsOp.Get_RELATIONs(fsNode_Item.vNode).ToList();
        //  (fsNode_Item as FS_Operations.FS_NodeInfoExt).Properties = fsOp.Get_PROPERTies(fsNode_Item.vNode).ToList();
        //}
        int startLevel = itemsCollector.RenderTree ? 1 : 0;
        breadcrumbs = indexTreeNode.BackRecurseOnTree.Where(n => n.Data != null).Where(n => n.Level > startLevel).Reverse().Select(n => n.Data.fragString).ToList();
        //
        if (fsNode_Item != null)
          return true;
      }
      catch { }
      return false;
    }


    //
    // genera l'sNode corrispondente all'n-esimo livello di back (per default back normale)
    //
    public FS_Operations.FS_TreeNode<IKCMS_TreeBrowser_Element_Interface> GetBackIndex(FS_Operations.FS_TreeNode<IKCMS_TreeBrowser_Element_Interface> indexTreeNode, int? level)
    {
      level = level ?? 1;
      var node = indexTreeNode;
      try
      {
        // NB salto il primo livello di back perche' indexTreeNode si riferisce gia' all'index corrente
        for (int i = 1; i < level.Value; i++)
          node = node.Parent;
      }
      catch { node = nodesTree; }
      while (node.Nodes.Count > 0)
        node = node.Nodes.FirstOrDefault();
      return node;
    }


    public IQueryable<fsNodeT> GetIndexItems<fsNodeT2>(FS_Operations fsOp,
      int? sNode_item, string infoPath,
      ref FS_Operations.FS_TreeNode<IKCMS_TreeBrowser_Element_Interface> indexTreeNode,
      ref fsNodeT2 fsNode_Item,
      ref List<string> breadcrumbs)
      where fsNodeT2 : class, FS_Operations.FS_NodeInfo_Interface, new()
    {
      return GetIndexItems(fsOp, sNode_item, infoPath, (fsOpLbd, vNodeFilterAll, vDataFilterAll) =>
      {
        return
          from vNode in fsOpLbd.NodesActive<IKGD_VNODE>().Where(vNodeFilterAll)
          from vData in fsOpLbd.NodesActive<IKGD_VDATA>().Where(vDataFilterAll).Where(n => n.rnode == vNode.rnode)
          select new fsNodeT2 { vNode = vNode, vData = vData } as fsNodeT;
      },
      ref indexTreeNode, ref fsNode_Item, ref breadcrumbs);
    }

    public IQueryable<fsNodeT> GetIndexItems<fsNodeT2>(FS_Operations fsOp,
      int? sNode_item, string infoPath,
      Func<FS_Operations, Expression<Func<IKGD_VNODE, bool>>, Expression<Func<IKGD_VDATA, bool>>, IQueryable<fsNodeT>> fsNodeFetcher,
      ref FS_Operations.FS_TreeNode<IKCMS_TreeBrowser_Element_Interface> indexTreeNode,
      ref fsNodeT2 fsNode_Item,
      ref List<string> breadcrumbs)
      where fsNodeT2 : class, FS_Operations.FS_NodeInfo_Interface
    {
      IQueryable<fsNodeT> resources = null;
      //
      try
      {
        if (indexTreeNode == null)
        {
          if (sNode_item != null || !string.IsNullOrEmpty(infoPath))
            GetItemInfo(fsOp, sNode_item, infoPath, out indexTreeNode, out fsNode_Item, out breadcrumbs);
        }
        if (indexTreeNode == null)
          return null;
        //
        List<int> folderSet = null;
        if (itemsCollector.RenderTree)
        {
          try { folderSet = new List<int> { indexTreeNode.BackRecurseOnData.FirstOrDefault(n => n != null && n.folderNode != null).folderNode.Value }; }
          catch { }
          if (folderSet == null)
          {
            try { folderSet = new List<int> { nodesTree.RecurseOnData.FirstOrDefault(n => n != null && n.folderNode != null).folderNode.Value }; }
            catch { }
          }
        }
        else
          folderSet = nodesTree.RecurseOnTree.Where(n => n.Data != null).Where(n => n.Data.folderNode != null).Select(n => n.Data.folderNode.Value).Distinct().ToList();
        //
        List<object> groupingFrags = indexTreeNode.BackRecurseOnData.Where(n => n != null && n.folderNode == null).Select(n => n.frag).Reverse().ToList();
        //
        Expression<Func<IKGD_VDATA, bool>> vDataFilterAll = fsOp.Get_vDataFilterACLv2();
        Expression<Func<IKGD_VNODE, bool>> vNodeFilterAll = fsOp.Get_vNodeFilterACLv2();
        vNodeFilterAll = vNodeFilterAll.And(n => n.flag_folder == false);
        if (folderSet.Count == 1)
        {
          int folder = folderSet.FirstOrDefault();
          vNodeFilterAll = vNodeFilterAll.And(n => n.folder == folder);
        }
        else
          vNodeFilterAll = vNodeFilterAll.And(n => folderSet.Contains(n.folder));
        //
        if (itemsFilter.vNodeFilter != null)
          vNodeFilterAll = vNodeFilterAll.And(itemsFilter.vNodeFilter);
        if (itemsFilter.vDataFilter != null)
          vDataFilterAll = vDataFilterAll.And(itemsFilter.vDataFilter);
        //
        if (itemsFilter.FilterBrowsableItemsOnly)
        {
          var typesToScan = IKCMS_RegisteredTypes.Types_IKCMS_BrowsableIndexable_Interface.Select(t => t.Name).ToList();
          if (typesToScan.Any())
          {
            vDataFilterAll = vDataFilterAll.And(n => typesToScan.Contains(n.manager_type));
          }
        }
        //
        resources = fsNodeFetcher(fsOp, vNodeFilterAll, vDataFilterAll);
        //
        resources = itemsCollector.Sorter(resources);  // non posso ottimizzarlo spostandolo dopo il grouping altrimenti LINQ rugna
        foreach (var coll in itemsCollector.Aggregators)
        {
          object fragValue = groupingFrags.FirstOrDefault();
          groupingFrags = groupingFrags.Skip(1).ToList();
          resources = resources.GroupBy(coll).FirstOrDefault(n => n.Key.Equals(fragValue)).AsQueryable();
        }
      }
      catch { }
      //var itemsList = resources.ToList();
      //
      return resources;
    }


    public string BuildBrowseTreeMenu(
      FS_Operations.FS_TreeNode<IKCMS_TreeBrowser_Element_Interface> currentTreeNode,
      string mainClassCSS,
      bool fullMenuTree,
      int? levelMin, int? levelMax,
      Func<FS_Operations.FS_TreeNode<IKCMS_TreeBrowser_Element_Interface>, bool, string> itemFormatter
      )
    {
      levelMin = levelMin ?? 0;
      levelMax = levelMax ?? int.MaxValue;
      //
      // il currentTreeNode potrebbe venire da un'altra istanza di oggetti generati dall'automapper
      // devo rimapparlo sul tree corrente con le funzioni di compare per automapper in modo da ottenere riferimenti ad oggetti confrontabili
      if (currentTreeNode != null)
        currentTreeNode = nodesTree.RecurseOnTree.FirstOrDefault(n => IKCMS_Browser_Support.EqualAutoMapper(n, currentTreeNode));
      // forzatura di un nodo valido e di una leaf
      currentTreeNode = (currentTreeNode ?? nodesTree).RecurseOnTree.FirstOrDefault(n => n.Nodes.Count == 0);
      //
      var selectedNodes = currentTreeNode.BackRecurseOnTree.ToList();
      var itemTreeNode = nodesTree;
      //
      Func<FS_Operations.FS_TreeNode<IKCMS_TreeBrowser_Element_Interface>, string> treeFormatter = null;
      treeFormatter = (item) =>
      {
        bool nodeIsSelected = selectedNodes.Contains(item);
        StringBuilder sb = new StringBuilder();
        // nel caso si tratti di un archivio senza RenderTree non si devono visualizzare i nodi corrispondenti al treeVFS
        if (!itemsCollector.RenderTree && item.Data != null && item.Data.folderNode != null)
          return string.Empty;
        // nel caso si tratti di un archivio con RenderTree non si deve visualizzare il livello con le root dei tree aggregati
        if (itemsCollector.RenderTree && item.Level == 1)
        {
          item.Nodes.ForEach(n => sb.Append(treeFormatter(n)));
          return sb.ToString();
        }
        // filtro per bloccare l'espansione dei rami non selezionati
        if (fullMenuTree || nodeIsSelected)
        {
          if (item.Nodes.Count > 0 && item.Level < levelMax)
          {
            if (item.Level >= levelMin)
              sb.Append((item.Data == null) ? "<ul class=\"{0}\">".FormatString(mainClassCSS) : "<ul>");
            //item.Nodes.ForEach(n => sb.Append(treeFormatter(n)));
            foreach (var nn in item.Nodes)
              sb.Append(treeFormatter(nn));
            if (item.Level >= levelMin)
              sb.Append("</ul>");
          }
        }
        if (item.Data != null)
        {
          // filtro per bloccare l'espansione delle sezioni non visualizzabili
          if (levelMin <= item.Level && item.Level <= levelMax)
          {
            sb.Insert(0, itemFormatter.Invoke(item, nodeIsSelected));
            sb.Insert(0, "<li>");
            sb.Append("</li>\n");
          }
        }
        return sb.ToString();
      };
      //
      return treeFormatter(nodesTree);
    }



  }  //class IKCMS_TreeModuleData<fsNodeT>



}
