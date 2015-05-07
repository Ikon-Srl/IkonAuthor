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
using System.Reflection;
using System.Data.Linq.Mapping;
using System.Diagnostics;
using System.Transactions;
using System.Web.Caching;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;  // assembly System.ServiceModel.Web
using LinqKit;


using Ikon;
using Ikon.GD;
using Ikon.IKCMS.Library.Resources;
using Ikon.IKGD.Library.Resources;


namespace Ikon.IKCMS
{
  //
  public enum UrlGeneratorFormatEnum
  {
    code_sNode,
    rnode_rNode,
    code_sNode_lastPathFragment,
    rnode_rNode_lastPathFragment,
    language_sNode_lastPathFragment,
    language_rNode_lastPathFragment,
    CMS_optimizedPath
  };
  //


  public class TreeNodeInfoVFS : FS_Operations.FS_NodeInfo
  {
    //public enum NodeTypes { None, NoAction, CMS_Page, UrlInternal, UrlExternal }
    //
    //public IKGD_VNODE vNode { get; set; }
    //public IKGD_VDATA vData { get; set; }
    //public IKGD_INODE iNode { get; set; }
    //
    public string Url { get; set; }
    public string Target { get; set; }
    //public NodeTypes NodeType { get; set; }
    // altri attributi per il nodo stanno in vData.Attributes
    //
    public override string ToString()
    {
      try { return string.Format("[{0}/{1}] {2} {{{3}}}", vNode.snode, vNode.rnode, vNode.name, Url); }
      catch { return "NULL"; }
    }
  }


  public static class IKCMS_TreeStructureVFS
  {
    public static readonly string baseKeyCache = "IKCMS_TreeStructureVFS_";
    public static List<string> FolderManagerTypesAllowed { get; private set; }
    public static List<string> FolderManagerTypesDenied { get; private set; }
    public static bool PurgeMultipleLanguagesFromTree { get; private set; }
    //
    public static UrlGeneratorFormatEnum UrlGeneratorFormat { get; private set; }
    //


    static IKCMS_TreeStructureVFS()
    {
      FolderManagerTypesAllowed = new List<string>();
      // non dobbiamo includere le cartelle IKCMS_FolderType_ArchiveRoot nei menu'
      FolderManagerTypesDenied = new List<string>() { typeof(IKCMS_FolderType_ArchiveRoot).Name };
      //
      UrlGeneratorFormat = (UrlGeneratorFormatEnum)Enum.Parse(typeof(UrlGeneratorFormatEnum), IKGD_Config.AppSettings["UrlGeneratorFormat"] ?? UrlGeneratorFormatEnum.language_sNode_lastPathFragment.ToString());
      //UrlGeneratorFormat = (UrlGeneratorFormatEnum)Enum.Parse(typeof(UrlGeneratorFormatEnum), IKGD_Config.AppSettings["UrlGeneratorFormat"] ?? UrlGeneratorFormatEnum.code_sNode_lastPathFragment.ToString());
      //
      PurgeMultipleLanguagesFromTree = Utility.TryParse<bool>(IKGD_Config.AppSettings["IKCMS_TreeNavigationVFS_PurgeMultipleLanguages"], true);
    }



    public static string HashBase { get { return baseKeyCache + FS_OperationsHelpers.ContextHash().GetHashCode().ToString("x") + "_"; } }
    public static string HashTreeStorage(string subKey, IEnumerable<int> sNodesRoot)
    {
      return Utility.Implode(new string[] { HashBase, subKey ?? string.Empty, Utility.Implode(sNodesRoot ?? Enumerable.Empty<int>(), "|") }, "_");
    }
    public static string HashTree(IEnumerable<string> pathsRoot, IEnumerable<int> sNodesRoot)
    {
      return Utility.Implode(new string[] { HashBase, Utility.Implode(sNodesRoot ?? Enumerable.Empty<int>(), "|"), Utility.Implode(pathsRoot ?? Enumerable.Empty<string>(), "|") }, "_");
    }



    //
    // TODO:
    // generare i campi Url e Target mancanti nei nodi del tree
    // supporto corretto per il caching
    // creare FS_Operations con un costruttore di copia
    //
    public static FS_Operations.FS_TreeNode<TreeNodeInfoVFS> TreeDataBuildCached(IEnumerable<int> sNodesRoot, int? sNodeSubMenu) { return TreeDataBuildCached(sNodesRoot, sNodeSubMenu, false, null, false); }
    public static FS_Operations.FS_TreeNode<TreeNodeInfoVFS> TreeDataBuildCached(IEnumerable<int> sNodesRoot, int? sNodeSubMenu, bool includeRoots) { return TreeDataBuildCached(sNodesRoot, sNodeSubMenu, includeRoots, null, false); }
    public static FS_Operations.FS_TreeNode<TreeNodeInfoVFS> TreeDataBuildCached(IEnumerable<int> sNodesRoot, int? sNodeSubMenu, bool includeRoots, bool? removeInactiveLeaves, bool siteMapMode)
    {
      //TODO: questo caching forse non e' corretto, sNodeSubMenu non devrebbe essere nella key
      string cacheKey = FS_OperationsHelpers.ContextHashNN("FS_TreeNode<TreeNodeInfoVFS>", Utility.Implode(sNodesRoot, ","), sNodeSubMenu, includeRoots, removeInactiveLeaves, siteMapMode);
      FS_Operations.FS_TreeNode<TreeNodeInfoVFS> treeRoot = FS_OperationsHelpers.CachedEntityWrapper<FS_Operations.FS_TreeNode<TreeNodeInfoVFS>>(
        cacheKey, () =>
        {
          return TreeDataBuild(sNodesRoot, sNodeSubMenu, includeRoots, removeInactiveLeaves, siteMapMode);
        }
        , m => m != null && !IKCMS_ExceptionsManager.ExceptionsAnyOf(typeof(IKCMS_Exception_TreeBuilder))
        , Utility.TryParse<int?>(IKGD_Config.AppSettings["CachingMenu"], 3600), null, FS_OperationsHelpers.Const_CacheDependencyIKGD_vNode_vData);
      return treeRoot;
    }


    public static FS_Operations.FS_TreeNode<TreeNodeInfoVFS> TreeDataBuild(IEnumerable<int> sNodesRoot, int? sNodeSubTree) { return TreeDataBuild(sNodesRoot, sNodeSubTree, false, null, false, null, null, null); }
    public static FS_Operations.FS_TreeNode<TreeNodeInfoVFS> TreeDataBuild(IEnumerable<int> sNodesRoot, int? sNodeSubTree, bool includeRoots) { return TreeDataBuild(sNodesRoot, sNodeSubTree, includeRoots, null, false, null, null, null); }
    public static FS_Operations.FS_TreeNode<TreeNodeInfoVFS> TreeDataBuild(IEnumerable<int> sNodesRoot, int? sNodeSubTree, bool includeRoots, bool? removeInactiveLeaves, bool siteMapMode) { return TreeDataBuild(sNodesRoot, sNodeSubTree, includeRoots, removeInactiveLeaves, siteMapMode, null, null, null); }
    public static FS_Operations.FS_TreeNode<TreeNodeInfoVFS> TreeDataBuild(IEnumerable<int> sNodesRoot, int? sNodeSubTree, bool includeRoots, bool? removeInactiveLeaves, bool siteMapMode, FlagsMenuEnum? menuFlagsMask, bool? enableRootSearch, int? maxRecursionLevel)
    {
      FS_Operations.FS_TreeNode<TreeNodeInfoVFS> RootNode = new FS_Operations.FS_TreeNode<TreeNodeInfoVFS>(null, null);
      //
      IEnumerable<int> sNodesRootAux = sNodesRoot ?? Enumerable.Empty<int>();
      if (sNodeSubTree == null && !sNodesRootAux.Any())
        return RootNode;
      //
      IKCMS_ExecutionProfiler.AddMessage("TreeDataBuild: START");
      int reads = 0;
      menuFlagsMask = menuFlagsMask ?? (FlagsMenuEnum)(0xffff);  // per default tutti i flags attivi, per disattivare il processing di tutti i flags passare 0
      //menuFlagsMask = menuFlagsMask ?? (FlagsMenuEnum.HiddenNode | FlagsMenuEnum.BreakRecurse | FlagsMenuEnum.BreakSiteMapRecurse | FlagsMenuEnum.FindFirstValidNode | FlagsMenuEnum.LazyLoginNoAnonymous | FlagsMenuEnum.SubTreeRoot | FlagsMenuEnum.SkipBreadCrumbs | FlagsMenuEnum.UnSelectableNode | FlagsMenuEnum.VisibleWithoutACL);
      List<int> foldersToExpand = new List<int>();
      List<IKGD_Path> pathsRoots = null;
      removeInactiveLeaves = removeInactiveLeaves ?? Utility.TryParse<bool>(IKGD_Config.AppSettings["TreeDataBuild_RemoveInactiveLeaves"], false);
      DateTime dateCMS = Ikon.GD.FS_OperationsHelpers.DateTimeSession;
      // non attiviamo il filtro per aree che viene gestito separatamente
      var pathFilter = new Func<IKGD_Path, bool>[] { IKGD_Path_Helper.FilterByRootCMS, IKGD_Path_Helper.FilterByActive, IKGD_Path_Helper.FilterByExpiry };
      //
      using (FS_Operations fsOp = new FS_Operations(true))
      {
        //fsOp.DB.Log = new LINQ_Logger();
        try
        {
          //string cacheSubKey = null;
          if (sNodeSubTree != null)
          {
            IKGD_Path_Fragment frag = null;
            foreach (IKGD_Path path in fsOp.PathsFromNodeExt(sNodeSubTree.Value, true, true, true).FilterCustom(pathFilter))
            {
              if (enableRootSearch.GetValueOrDefault(true))
              {
                //if ((frag = path.Fragments.LastOrDefault(f => f.flag_treeRecurse == true)) != null)
                if ((frag = path.Fragments.LastOrDefault(f => (f.FlagsMenu & FlagsMenuEnum.SubTreeRoot) == FlagsMenuEnum.SubTreeRoot)) != null)
                {
                  //cacheSubKey = "subTree";
                  sNodesRootAux = new List<int> { frag.sNode };
                  break;
                }
              }
            }
          }
          if (sNodesRootAux.Any())
          {
            //
            // uso il sistema di gestione dei path per ottenere le root in modo da sfruttare meglio la cache
            // se ci sono dei problemi si puo' sempre tornare ad usare lo scan diretto del VFS
            //
            pathsRoots = fsOp.PathsFromNodesExt(sNodesRootAux, null, true, true, true).FilterCustom(pathFilter).ToList();
            if (enableRootSearch.GetValueOrDefault(true))
            {
              //var maskAnd = FlagsMenuEnum.HiddenNode | FlagsMenuEnum.SubTreeRoot;
              var maskAnd = FlagsMenuEnum.SubTreeRoot;
              var maskVal = FlagsMenuEnum.SubTreeRoot;
              pathsRoots.RemoveAll(p => (p.LastFragment.FlagsMenu & maskAnd) != maskVal);
            }
            foldersToExpand = pathsRoots.Select(n => n.LastFragment.rNode).ToList();
            sNodesRootAux = sNodesRootAux.Where(n => pathsRoots.Any(p => p.sNode == n));
            //
            // attenzione non uso il .Distinct per mantenere l'ordinamento dei nodi
            //foldersToExpand = fsOp.Get_NodesInfoACL(sNodesRoot, false, false).Where(n => n.vNode.flag_folder)
            //  .Where(n => n.vData.flag_menu.GetValueOrDefault(true))
            //  .Where(n => n.vData.flag_treeRecurse.GetValueOrDefault(false))
            //  .OrderBy(n => n.vNode.position).ThenBy(n => n.vNode.name).ThenBy(n => n.vNode.snode)
            //  .Select(n => n.vNode.folder).ToList();
          }
          if (foldersToExpand.Count == 0)
          {
            Elmah.ErrorSignal.FromCurrentContext().Raise(new Exception("TreeDataBuild [foldersToExpand is void] --> sNodeSubTree={0} sNodesRoot={1} sNodesRootAux={2}".FormatString(sNodeSubTree, Utility.Implode(sNodesRoot ?? Enumerable.Empty<int>(), ","), Utility.Implode(sNodesRootAux, ","))));
            return RootNode;
          }
          //
          //string cacheKey = HashTreeStorage(cacheSubKey, foldersToExpand);
          //
          List<FS_Operations.FS_TreeNode<TreeNodeInfoVFS>> treeNodesActive = new List<FS_Operations.FS_TreeNode<TreeNodeInfoVFS>>();
          //
          int BreakRecurseFlag = siteMapMode ? (int)FlagsMenuEnum.BreakSiteMapRecurse : (int)FlagsMenuEnum.BreakRecurse;
          if (((int)menuFlagsMask.Value & BreakRecurseFlag) != BreakRecurseFlag)
          {
            BreakRecurseFlag = 0;
          }
          //
          // scan progressivo del menu' un livello alla volta con scansione del DB in chunks
          //
          Expression<Func<IKGD_VNODE, bool>> vNodeFilter = fsOp.Get_vNodeFilterACLv2(true);
          Expression<Func<IKGD_VDATA, bool>> vDataFilter = fsOp.Get_vDataFilterACLv2(false, false);  // la gestione delle aree e' diversa dai soliti scan e viene gestita nel seguito
          //
          vNodeFilter = vNodeFilter.And(n => n.flag_folder == true);
          if ((menuFlagsMask.Value & FlagsMenuEnum.HiddenNode) == FlagsMenuEnum.HiddenNode)
          {
            vDataFilter = vDataFilter.And(n => (n.flags_menu & (int)FlagsMenuEnum.HiddenNode) != (int)FlagsMenuEnum.HiddenNode);
          }
          //
          if (FolderManagerTypesAllowed.Any())
          {
            vDataFilter = vDataFilter.And(n => n.manager_type == null || FolderManagerTypesAllowed.Contains(n.manager_type));
          }
          if (FolderManagerTypesDenied.Any())
          {
            vDataFilter = vDataFilter.And(n => n.manager_type == null || !FolderManagerTypesDenied.Contains(n.manager_type));
          }
          //
          vDataFilter = vDataFilter.And(n => (n.date_activation == null || n.date_activation <= dateCMS) && (n.date_expiry == null || dateCMS <= n.date_expiry));
          //
          if (!fsOp.IsRoot)
          {
            //TODO:ACL
            //vDataFilter = vDataFilter.And(n => ((n.flags_menu & (int)FlagsMenuEnum.VisibleWithoutACL) == (int)FlagsMenuEnum.VisibleWithoutACL) || fsOp.CurrentAreas.Contains(n.area));
            //vDataFilter = vDataFilter.And(n => ((n.flags_menu & (int)FlagsMenuEnum.VisibleWithoutACL) == (int)FlagsMenuEnum.VisibleWithoutACL) || fsOp.CurrentAreasExtended.AreasAllowed.Contains(n.area));
            if (fsOp.CurrentAreasExtended.AreaMatchMode == FS_Areas_Extended.AreaMatchModeEnum.FilterByAllowed)
              vDataFilter = vDataFilter.And(n => fsOp.CurrentAreasExtended.AreasAllowed.Contains(n.area) || ((n.flags_menu & (int)FlagsMenuEnum.VisibleWithoutACL) == (int)FlagsMenuEnum.VisibleWithoutACL));
            else if (fsOp.CurrentAreasExtended.AreaMatchMode == FS_Areas_Extended.AreaMatchModeEnum.FilterByDenied)
              vDataFilter = vDataFilter.And(n => !fsOp.CurrentAreasExtended.AreasDenied.Contains(n.area) || ((n.flags_menu & (int)FlagsMenuEnum.VisibleWithoutACL) == (int)FlagsMenuEnum.VisibleWithoutACL));
          }
          var dtNow = FS_OperationsHelpers.DateTimeSession;
          vDataFilter = vDataFilter.And(n => (n.date_activation == null || n.date_activation <= dtNow) && (n.date_expiry == null || dtNow <= n.date_expiry));
          //
          // normalmente le roots non vengono incluse nei menu' ma potrebbero essere necessarie nei submenu
          if (includeRoots)
          {
            try
            {
              var fsNodeRoots = fsOp.Get_NodesInfoACL(sNodesRootAux, false, false, false).ToList();
              fsNodeRoots.ForEach(n => new FS_Operations.FS_TreeNode<TreeNodeInfoVFS>(RootNode, new TreeNodeInfoVFS { vNode = n.vNode, vData = n.vData }));
              treeNodesActive.AddRange(RootNode.Nodes);
              if (sNodesRootAux.Except(fsNodeRoots.Select(n => n.sNode)).Any())
              {
                var nodes_missing = sNodesRootAux.Except(fsNodeRoots.Select(n => n.sNode)).ToList();
                Elmah.ErrorSignal.FromCurrentContext().Raise(new Exception("TreeDataBuild [includeRoots: roots not found] {0}".FormatString(Utility.Implode(nodes_missing, ","))));
              }
            }
            catch (Exception ex)
            {
              Elmah.ErrorSignal.FromCurrentContext().Raise(ex);
              IKCMS_ExceptionsManager.Add(new IKCMS_Exception_TreeBuilder("TreeDataBuild [includeRoots problem] " + ex.Message, ex));
            }
          }
          //
          List<int> rNodesProcessedExtra = new List<int>();
          var activePageTypes = IKCMS_RegisteredTypes.Types_IKCMS_Page_Interface.Where(t => !t.IsAbstract && !t.IsGenericType).Select(t => t.Name).Distinct().ToList();
          //
          int maxExtLoopCounter = 3;  // massimo numero di ricorsioni per terminare di risolvere i nodi con FlagsMenuEnum.FindFirstValidNode
          maxRecursionLevel = maxRecursionLevel ?? Utility.TryParse<int>(IKGD_Config.AppSettings["IKGD_Path_MaxRecursionLevels"], 25);
          for (int extLoopCounter = 0, loopCounter = maxRecursionLevel.Value; extLoopCounter <= maxExtLoopCounter && (foldersToExpand.Count > 0) && (loopCounter >= 0); extLoopCounter++, loopCounter--)
          {
            while (foldersToExpand.Count > 0)
            {
              List<TreeNodeInfoVFS> foldersActive = new List<TreeNodeInfoVFS>();
              List<FS_Operations.FS_TreeNode<TreeNodeInfoVFS>> treeNodesActiveNext = new List<FS_Operations.FS_TreeNode<TreeNodeInfoVFS>>();
              //
              // costruzione in chunk della lista dei nodi del livello successivo
              // che viene eseguita a blocchi per evitare di ritrovarsi con troppi parametri nella query
              // a causa dell'espansione di .Contains(n.folder)
              //
              int chunkSize = 100;  // eventualmente da ottimizzare
              for (int chunkStart = 0; chunkStart < foldersToExpand.Count; chunkStart += chunkSize)
              {
                List<int> foldersToFetch = foldersToExpand.Skip(chunkStart).Take(chunkSize).ToList();
                reads++;
                var nodes =
                  (from vNode in fsOp.NodesActive<IKGD_VNODE>().Where(vNodeFilter).Where(n => foldersToFetch.Contains(n.parent.Value))
                   from vData in fsOp.NodesActive<IKGD_VDATA>().Where(vDataFilter).Where(n => n.rnode == vNode.rnode)
                   select new
                   {
                     snode = vNode.snode,
                     rnode = vNode.rnode,
                     folder = vNode.folder,
                     parent = vNode.parent,
                     name = vNode.name,
                     position = vNode.position,
                     flag_folder = vNode.flag_folder,
                     placeholder = vNode.placeholder,
                     template = vNode.template,
                     language = vNode.language,
                     manager_type = vData.manager_type,
                     category = vData.category,
                     key = vData.key,
                     area = vData.area,
                     date_node = vData.date_node,
                     date_node_aux = vData.date_node_aux,
                     flag_unstructured = vData.flag_unstructured,
                     flags_menu = vData.flags_menu
                   });  // ottimizzazione SQL per non dover leggere i dati serializzati e altre info non utili per la generazione dei menu
                foldersActive.AddRange(nodes.AsEnumerable().Select(r => new TreeNodeInfoVFS
                {
                  vNode = new IKGD_VNODE
                  {
                    snode = r.snode,
                    rnode = r.rnode,
                    folder = r.folder,
                    parent = r.parent,
                    name = r.name,
                    position = r.position,
                    flag_folder = r.flag_folder,
                    placeholder = r.placeholder,
                    template = r.template,
                    language = r.language
                  },
                  vData = new IKGD_VDATA
                  {
                    rnode = r.rnode,
                    manager_type = r.manager_type,
                    category = r.category,
                    key = r.key,
                    area = r.area,
                    date_node = r.date_node,
                    date_node_aux = r.date_node_aux,
                    flag_unstructured = r.flag_unstructured,
                    flags_menu = r.flags_menu
                  }
                }));
                // vecchio codice non ottimizzato SQL con lettura completa dei vData
                //foldersActive.AddRange(
                //  (from vNode in fsOp.NodesActive<IKGD_VNODE>().Where(vNodeFilter).Where(n => foldersToFetch.Contains(n.parent.Value))
                //   from vData in fsOp.NodesActive<IKGD_VDATA>().Where(vDataFilter).Where(n => n.rnode == vNode.rnode)
                //   select new TreeNodeInfoVFS { vNode = vNode, vData = vData })
                //  );
              }
              if (treeNodesActive.Count == 0)
              {
                // siamo appena partiti con il root node
                treeNodesActive.Add(RootNode);
                var foldersActiveSorted = foldersActive.OrderBy(n => foldersToExpand.IndexOf(n.vNode.parent.Value)).ThenBy(n => n.vNode.position).ThenBy(n => n.vNode.name).ThenBy(n => n.vNode.snode);
                foldersActiveSorted.ForEach(n => treeNodesActiveNext.Add(new FS_Operations.FS_TreeNode<TreeNodeInfoVFS>(RootNode, n)));
              }
              else
              {
                // aggiungo i nuovi nodi al livello precedente
                var foldersActiveSorted = foldersActive.OrderBy(n => n.vNode.position).ThenBy(n => n.vNode.name).ThenBy(n => n.vNode.snode);
                foreach (TreeNodeInfoVFS fsNode in foldersActiveSorted.Where(n => n.vNode.parent != null))
                  treeNodesActive.Where(n => n.Data.vNode.folder == fsNode.vNode.parent.Value).ForEach(tn => treeNodesActiveNext.Add(new FS_Operations.FS_TreeNode<TreeNodeInfoVFS>(tn, fsNode)));
              }
              //
              // filtraggio da treeNodesActiveNext 
              //
              treeNodesActive = treeNodesActiveNext.Where(n => n.Data != null).Where(n => ((n.Data.vData.flags_menu & BreakRecurseFlag) != BreakRecurseFlag) || (BreakRecurseFlag == 0)).ToList();
              foldersToExpand = treeNodesActive.Select(n => n.Data.vNode.folder).Distinct().ToList();
              //
              // gestione dei problemi di risorse con ricorsione e filtraggio dei nodi da riespandere
              //
              if (foldersToExpand.Any())
              {
                var recursionsFound = foldersToExpand.Intersect(treeNodesActive.SelectMany(r => r.BackRecurseOnTree.Where(n => n.Data != null && n.Data.ParentFolder != null).Select(n => n.Data.ParentFolder.Value)).Distinct()).ToList();
                if (recursionsFound.Any())
                {
                  foldersToExpand = foldersToExpand.Except(recursionsFound).ToList();
                }
              }
              //
            }
            //
            // gestione dei nodi con l'attributo FlagsMenuEnum.FindFirstValidNode attivo
            // bisogna assicurarsi che ci sia un nodo sul quale mappare la url
            // eventualmente si continua con il fetch/binding di nuovi nodi marchiandoli con NodeTypes.TemporaryNode per rimouverli poi durante la rinormalizzazione
            //
            if ((menuFlagsMask.Value & FlagsMenuEnum.FindFirstValidNode) == FlagsMenuEnum.FindFirstValidNode)
            {
              foldersToExpand = RootNode.RecurseOnTree.Where(n => n.Data != null).Where(n => (n.Data.vData.FlagsMenu & FlagsMenuEnum.FindFirstValidNode) == FlagsMenuEnum.FindFirstValidNode).Where(n => !activePageTypes.Contains(n.Data.ManagerType) || (n.Data.vData.FlagsMenu & FlagsMenuEnum.UnSelectableNode) == FlagsMenuEnum.UnSelectableNode).Where(tn => !tn.RecurseOnTree.Any(sn => activePageTypes.Contains(sn.Data.ManagerType) && (sn.Data.vData.FlagsMenu & FlagsMenuEnum.UnSelectableNode) != FlagsMenuEnum.UnSelectableNode)).SelectMany(tn => tn.RecurseOnTree.Where(sn => sn.Nodes.Count == 0).Select(sn => sn.Data.rNode)).Except(rNodesProcessedExtra).Distinct().ToList();
              if (foldersToExpand.Any())
              {
                treeNodesActive = RootNode.RecurseOnTree.Where(n => n.Data != null).Where(n => foldersToExpand.Contains(n.Data.rNode)).ToList();
                var recursionsFound = foldersToExpand.Intersect(treeNodesActive.SelectMany(r => r.BackRecurseOnTree.Where(n => n.Data != null && n.Data.ParentFolder != null).Select(n => n.Data.ParentFolder.Value)).Distinct()).ToList();
                if (recursionsFound.Any())
                {
                  foldersToExpand = foldersToExpand.Except(recursionsFound).ToList();
                }
              }
              rNodesProcessedExtra.AddRange(foldersToExpand);
            }
            //
          }
          //
          // eliminazione delle leafs che non sono visibili nel menu' per lingua non accessibile o flags
          //
          {
            // non dovrebbe essere necessario visto che abbiamo usato i filtri nello scanvfs
            string language = fsOp.Language;
            foreach (var node in RootNode.RecurseOnTree.Where(n => n.Data != null).Where(n => !n.Data.IsActive || !n.Data.LanguageCheck(language)).Reverse())
              node.Parent = null;
          }
          //
          // se un nodo e' definito senza lingua e con lingua lo facciamo comparire una volta sola
          //
          if (PurgeMultipleLanguagesFromTree)
          {
            try
            {
              //correzione effettuata il 28/11/2013 a causa di una subricorsione su tutto il tree invece che sui soli figli diretti
              var nodesWithDoubleLanguage = RootNode.RecurseOnTree.Where(n => n.Data != null && n.Nodes.Any()).SelectMany(n => n.Nodes.GroupBy(r => r.Data.rNode).SelectMany(g => g.OrderByDescending(r => r.Data.Language).ThenByDescending(r => r.Data.Position).ThenByDescending(r => r.Data.sNode).Skip(1))).ToList();
              if (nodesWithDoubleLanguage.Any())
              {
                nodesWithDoubleLanguage.AsEnumerable().Reverse().ForEach(n => n.Parent = null);
              }
            }
            catch { }
          }
          //
          // lettura dei settings per tutti i nodi di tipo IKCMS_ResourceType_PageStatic
          //
          var nodesWithSettings = RootNode.RecurseOnTree.Where(n => n.Data != null).Where(n => n.Data.ManagerType == typeof(IKCMS_ResourceType_PageStatic).Name).ToList();
          if (nodesWithSettings.Any())
          {
            var rnodes = nodesWithSettings.Select(n => n.Data.rNode).Distinct().ToList();
            fsOp.NodesActive<IKGD_VDATA>().Where(n => rnodes.Contains(n.rnode)).Select(n => new { n.rnode, n.settings }).AsEnumerable().Join(nodesWithSettings, r => r.rnode, r => r.Data.rNode, (data, node) => new { data, node }).ForEach(r => r.node.Data.vData.settings = r.data.settings);
          }
          //
          // assegno a tutti i nodi la url e il target
          //
          //RootNode.RecurseOnTree.Where(tn => tn.Data != null).ForEach(tn => MenuFormatterWorker(tn, includeRoots ? pathsRoots : null));
          RootNode.RecurseOnTree.Where(tn => tn.Data != null).ForEach(tn => MenuFormatterWorker(tn, pathsRoots));
          //
          // gestione dei nodi con l'attributo FlagsMenuEnum.FindFirstValidNode attivo
          //
          foreach (var treeNode in RootNode.RecurseOnTree.Where(n => n.Data != null).Where(n => (n.Data.vData.FlagsMenu & FlagsMenuEnum.FindFirstValidNode) == FlagsMenuEnum.FindFirstValidNode).Where(n => n.Data.Url == null || (n.Data.vData.FlagsMenu & FlagsMenuEnum.UnSelectableNode) == FlagsMenuEnum.UnSelectableNode))
          {
            var treeNodeUrl = treeNode.RecurseOnTree.FirstOrDefault(tn => !string.IsNullOrEmpty(tn.Data.Url) && tn.Data.Url != "javascript:;" && (tn.Data.vData.FlagsMenu & FlagsMenuEnum.UnSelectableNode) != FlagsMenuEnum.UnSelectableNode);
            if (treeNodeUrl != null)
              treeNode.Data.Url = treeNodeUrl.Data.Url;
          }
          //
          // eliminazione delle leafs che non sono visibili nel menu'
          //
          if (removeInactiveLeaves == true)
          {
            foreach (var node in RootNode.RecurseOnTree.Where(n => n.Data != null && n.Data.Url == null).Reverse())
            {
              if (node.Data.Url == null && node.Nodes.Count == 0)
                node.Parent = null;
            }
          }
          //
          // eliminazione delle leafs che non sono visibili nel menu'
          //
          int minLevelBreak = includeRoots ? 2 : 1;  // se uno dei root nodes e' stato marcato per interrompere l'espasione e il rendering dei root nodes e' attivato ignoriamo il flag
          int minLevelHidden = includeRoots ? 1 : 1;  // nel caso di nodi hidden invece consideriamo comunque il flag
          if ((menuFlagsMask.Value & FlagsMenuEnum.HiddenNode) == FlagsMenuEnum.HiddenNode || (((int)menuFlagsMask.Value & BreakRecurseFlag) == BreakRecurseFlag && BreakRecurseFlag != 0))
          {
            RootNode.RecurseOnTree.Where(n => n.Data != null && n.Level >= minLevelHidden).Where(n => (n.Data.vData.flags_menu & (int)FlagsMenuEnum.HiddenNode) == (int)FlagsMenuEnum.HiddenNode).Reverse().ForEach(tn => tn.Parent = null);
            foreach (var node in RootNode.RecurseOnTree.Where(n => n.Data != null && n.Level >= minLevelBreak).Where(n => (n.Data.vData.flags_menu & BreakRecurseFlag) == BreakRecurseFlag && BreakRecurseFlag != 0).Reverse())
              node.Nodes.AsEnumerable().Reverse().ForEach(n => n.Parent = null);
          }
          //
        }
        catch (Exception ex)
        {
          Elmah.ErrorSignal.FromCurrentContext().Raise(ex);
          IKCMS_ExceptionsManager.Add(new IKCMS_Exception_TreeBuilder("TreeDataBuild" + ex.Message, ex));
          //throw;  // ripetiamo l'exception per fare in modo di disabilitare il caching in caso di errore
        }
      }
      IKCMS_ExecutionProfiler.AddMessage("TreeDataBuild: END: reads={0}".FormatString(reads));
      return RootNode;
    }


    public static FS_Operations.FS_TreeNode<TreeNodeInfoVFS> TreeDataBuildFolderedDocuments(int rNodeMain, IEnumerable<int> rNodesRoot, bool hideRoots, bool mixFilesAndFolders)
    {
      Expression<Func<IKGD_VDATA, bool>> vDataFilterFolders = vd => vd.manager_type == typeof(IKCMS_FolderType_ArchiveRoot).Name;
      Expression<Func<IKGD_VDATA, bool>> vDataFilterFiles = vd => vd.manager_type == typeof(IKGD_ResourceTypeDocument).Name;
      return TreeDataBuildFoldersAndFiles(rNodeMain, rNodesRoot, hideRoots, mixFilesAndFolders, vDataFilterFolders, vDataFilterFiles);
    }


    public static FS_Operations.FS_TreeNode<TreeNodeInfoVFS> TreeDataBuildFoldersAndFiles(int rNodeMain, IEnumerable<int> rNodesRoot, bool hideRoots, bool mixFilesAndFolders, Expression<Func<IKGD_VDATA, bool>> vDataFilterFolders, Expression<Func<IKGD_VDATA, bool>> vDataFilterFiles)
    {
      FS_Operations.FS_TreeNode<TreeNodeInfoVFS> RootNode = new FS_Operations.FS_TreeNode<TreeNodeInfoVFS>(null, null);
      //
      rNodesRoot = rNodesRoot ?? Enumerable.Empty<int>();
      List<int> foldersToExpand = new List<int>();
      List<IKGD_Path> pathsRoots = null;
      DateTime dateCMS = Ikon.GD.FS_OperationsHelpers.DateTimeSession;
      // non attiviamo il filtro per aree che viene gestito separatamente
      var pathFilter = new Func<IKGD_Path, bool>[] { IKGD_Path_Helper.FilterByRootCMS, IKGD_Path_Helper.FilterByActive, IKGD_Path_Helper.FilterByExpiry };
      //
      using (FS_Operations fsOp = new FS_Operations(true))
      {
        try
        {
          if (!rNodesRoot.Any())
          {
            // lettura di tutti i folder validi presenti nel folder rNodeMain se non ho specificato nessun folderRoot
            var nodes = fsOp.Get_NodesInfoFiltered(vn => vn.flag_folder == true && vn.parent == rNodeMain, vDataFilterFolders, false);
            foldersToExpand = nodes.Select(n => n.rNode).Distinct().ToList();
            pathsRoots = fsOp.PathsFromNodesExt(null, foldersToExpand, true, false, true).FilterCustom(pathFilter).ToList();
            foldersToExpand = pathsRoots.Select(n => n.LastFragment.rNode).Distinct().ToList();
          }
          else
          {
            pathsRoots = fsOp.PathsFromNodesExt(null, rNodesRoot, true, false, true).FilterCustom(pathFilter).ToList();
            foldersToExpand = pathsRoots.Select(n => n.LastFragment.rNode).Distinct().ToList();
          }
          if (foldersToExpand.Count == 0)
          {
            return RootNode;
          }
          //
          //string cacheKey = FS_OperationsHelpers.ContextHash(baseKeyCache + "F&F_", foldersToExpand);
          //
          List<FS_Operations.FS_TreeNode<TreeNodeInfoVFS>> treeNodesActive = new List<FS_Operations.FS_TreeNode<TreeNodeInfoVFS>>();
          //
          // scan progressivo del menu' un livello alla volta con scansione del DB in chunks
          //
          Expression<Func<IKGD_VNODE, bool>> vNodeFilter = fsOp.Get_vNodeFilterACLv2(true);
          Expression<Func<IKGD_VDATA, bool>> vDataFilter = fsOp.Get_vDataFilterACLv2(false, true);
          Expression<Func<IKGD_VDATA, bool>> vDataFilterNodes = fsOp.Get_vDataFilterACLv2(false, true);
          //
          vNodeFilter = vNodeFilter.And(n => n.flag_folder == true);
          if (vDataFilterFolders != null)
            vDataFilter = vDataFilter.And(vDataFilterFolders);
          if (vDataFilterFiles != null)
            vDataFilterNodes = vDataFilterNodes.And(vDataFilterFiles);
          //
          vDataFilter = vDataFilter.And(n => (n.date_activation == null || n.date_activation <= dateCMS) && (n.date_expiry == null || dateCMS <= n.date_expiry));
          vDataFilterNodes = vDataFilterNodes.And(n => (n.date_activation == null || n.date_activation <= dateCMS) && (n.date_expiry == null || dateCMS <= n.date_expiry));
          //
          try
          {
            var fsNodeRoots = fsOp.Get_NodesInfoFiltered(vn => vn.flag_folder == true && foldersToExpand.Contains(vn.rnode), null, false).ToList();
            fsNodeRoots.ForEach(n => new FS_Operations.FS_TreeNode<TreeNodeInfoVFS>(RootNode, new TreeNodeInfoVFS { vNode = n.vNode, vData = n.vData }));
            treeNodesActive.AddRange(RootNode.Nodes);
          }
          catch { }
          //
          while (foldersToExpand.Count > 0)
          {
            List<TreeNodeInfoVFS> foldersActive = new List<TreeNodeInfoVFS>();
            List<FS_Operations.FS_TreeNode<TreeNodeInfoVFS>> treeNodesActiveNext = new List<FS_Operations.FS_TreeNode<TreeNodeInfoVFS>>();
            //
            // costruzione in chunk della lista dei nodi del livello successivo
            // che viene eseguita a blocchi per evitare di ritrovarsi con troppi parametri nella query
            // a causa dell'espansione di .Contains(n.folder)
            //
            int chunkSize = 100;  // TODO: ottimizzare
            for (int chunkStart = 0; chunkStart < foldersToExpand.Count; chunkStart += chunkSize)
            {
              List<int> foldersToFetch = foldersToExpand.Skip(chunkStart).Take(chunkSize).ToList();
              foldersActive.AddRange(
                (from vNode in fsOp.NodesActive<IKGD_VNODE>().Where(n => n.flag_folder == true && foldersToFetch.Contains(n.parent.Value))
                 from vData in fsOp.NodesActive<IKGD_VDATA>().Where(vDataFilter).Where(n => n.rnode == vNode.rnode)
                 from iNode in fsOp.NodesActive<IKGD_INODE>().Where(n => n.rnode == vNode.rnode).DefaultIfEmpty()  // solo per gestire correttamente il nome del file nelle url generate per i download
                 select new TreeNodeInfoVFS { vNode = vNode, vData = vData, iNode = iNode })
                );
            }
            if (treeNodesActive.Count == 0)
            {
              // siamo appena partiti con il root node
              treeNodesActive.Add(RootNode);
              var foldersActiveSorted = foldersActive.OrderBy(n => foldersToExpand.IndexOf(n.vNode.parent.Value)).ThenBy(n => n.vNode.position).ThenBy(n => n.vNode.name).ThenBy(n => n.vNode.snode);
              foldersActiveSorted.ForEach(n => treeNodesActiveNext.Add(new FS_Operations.FS_TreeNode<TreeNodeInfoVFS>(RootNode, n)));
            }
            else
            {
              // aggiungo i nuovi nodi al livello precedente
              var foldersActiveSorted = foldersActive.OrderBy(n => n.vNode.position).ThenBy(n => n.vNode.name).ThenBy(n => n.vNode.snode);
              foreach (TreeNodeInfoVFS fsNode in foldersActiveSorted.Where(n => n.vNode.parent != null))
                treeNodesActive.Where(n => n.Data.vNode.folder == fsNode.vNode.parent.Value).ForEach(tn => treeNodesActiveNext.Add(new FS_Operations.FS_TreeNode<TreeNodeInfoVFS>(tn, fsNode)));
            }
            //
            // filtraggio da treeNodesActiveNext 
            //
            //treeNodesActive = treeNodesActiveNext.Where(n => n.Data != null).Where(n => n.Data.vData.flag_treeRecurse.GetValueOrDefault(true)).ToList();
            treeNodesActive = treeNodesActiveNext.Where(n => n.Data != null).ToList();
            foldersToExpand = treeNodesActive.Select(n => n.Data.vNode.folder).Distinct().ToList();
          }  // while (foldersToExpand.Count > 0)
          //
          // eliminazione delle leafs che non sono visibili nel menu' per lingua non accessibile o flags
          //
          string language = fsOp.Language;
          foreach (var node in RootNode.RecurseOnTree.Where(n => n.Data != null).Where(n => !n.Data.IsActive || !n.Data.LanguageCheck(language)).Reverse())
            node.Parent = null;
          //
          List<int> foldersToRead = RootNode.RecurseOnTree.Where(n => n.Data != null).Select(n => n.Data.rNode).Distinct().ToList();
          List<TreeNodeInfoVFS> nodesActive =
            (from vNode in fsOp.NodesActive<IKGD_VNODE>().Where(n => foldersToRead.Contains(n.folder))
             from vData in fsOp.NodesActive<IKGD_VDATA>().Where(vDataFilterNodes).Where(n => n.rnode == vNode.rnode)
             from iNode in fsOp.NodesActive<IKGD_INODE>().Where(n => n.rnode == vNode.rnode).DefaultIfEmpty()  // solo per gestire correttamente il nome del file nelle url generate per i download
             select new TreeNodeInfoVFS { vNode = vNode, vData = vData, iNode = iNode }).ToList();
          //
          var nodesToDelete = foldersToRead.Except(nodesActive.Select(n => n.Folder)).ToList();
          if (nodesToDelete.Any())
            foreach (var node in RootNode.RecurseOnTree.Where(n => n.Data != null).Where(n => nodesToDelete.Contains(n.Data.rNode)).Reverse())
              node.Parent = null;
          foreach (var node in RootNode.RecurseOnTree.Where(n => n.Data != null).ToList())
            nodesActive.Where(n => n.Folder == node.Data.Folder).OrderBy(n => n.Position).ThenBy(n => n.sNode).ForEach(n => new FS_Operations.FS_TreeNode<TreeNodeInfoVFS>(node, n));
          //
          if (mixFilesAndFolders)
          {
            try
            {
              RootNode.RecurseOnTree.Where(n => n.Nodes.Any()).Reverse().ForEach(tn =>
              {
                List<FS_Operations.FS_TreeNode<TreeNodeInfoVFS>> nodes = null;
                if (tn.Level == 0 && rNodesRoot != null && rNodesRoot.Any())
                  nodes = tn.Nodes.OrderBy(n => rNodesRoot.ToList().IndexOf(n.Data.rNode)).ThenBy(n => n.Data.Position).ThenBy(n => n.Data.sNode).ToList();
                else
                  nodes = tn.Nodes.OrderBy(n => n.Data.Position).ThenBy(n => n.Data.sNode).ToList();
                tn.Nodes.Clear();
                nodes.ForEach(n => tn.Nodes.Add(n));
              });
            }
            catch { }
          }
          if (hideRoots)
          {
            var nodesToRelevel = RootNode.Nodes.Where(n => n.Nodes.Any()).ToList();
            nodesToRelevel.SelectMany(n => n.Nodes).ToList().ForEach(n => n.Parent = RootNode);
            nodesToRelevel.ForEach(n => n.Parent = null);
          }
          //
          // assegno a tutti i nodi la url e il target
          //
          RootNode.RecurseOnTree.Where(tn => tn.Data != null).ForEach(tn => MenuFormatterWorker(tn, null));
          //
        }
        catch { throw; }  // ripetiamo l'exception per fare in modo di disabilitare il caching in caso di errore
      }
      return RootNode;
    }


    public static void MenuFormatterWorker(FS_Operations.FS_TreeNode<TreeNodeInfoVFS> node, List<IKGD_Path> pathsRoots)
    {
      if (node == null || node.Data == null)
        return;
      try
      {
        //Type pageType = IKCMS_RegisteredTypes.Types_IKCMS_PageBase_Interface.FirstOrDefault(t => t.Name == node.Data.vData.manager_type);
        Type pageType = IKCMS_RegisteredTypes.Types_IKCMS_ResourceWithUrl_Interface.FirstOrDefault(t => t.Name == node.Data.vData.manager_type);
        node.Data.Target = ((node.Data.vData.FlagsMenu & FlagsMenuEnum.TargetBlank) == FlagsMenuEnum.TargetBlank) ? "_blank" : null;
        if (node.Data.vData.flag_unstructured)
        {
          //node.Data.Url = IKCMS_RouteUrlManager.GetUrlProxyVFS(node.Data.vNode.rnode, null, null, null);
          node.Data.Url = UrlDownloadDefaultStream(node.Data.vNode, node.Data.vData, node.Data.iNode, string.Empty);
          return;
        }
        if (pageType == null)
        {
          node.Data.Url = null;
          return;
        }
        else if (pageType.IsAssignableTo(typeof(IKCMS_ResourceType_PageStatic)))
        {
          node.Data.Url = (IKCMS_RegisteredTypes.Deserialize_IKCMS_ResourceVFS(node.Data) as IKCMS_ResourceType_PageStatic).ResourceSettings.UrlExternal ?? node.Data.Url;
          return;
        }
        node.Data.Url = UrlFormatterWorkerBase(node.Data.sNode, node.Data.rNode, node.Data.Language, node.Data.Name, node.Data.vData.FlagsMenu, null, node, pathsRoots);
      }
      catch { }
      finally
      {
        if (node.Data.Url != null)
        {
          try { node.Data.Url = IKGD_SEO_Manager.MapOutcomingUrl(node.Data.vNode.snode, node.Data.vNode.rnode, null) ?? node.Data.Url; }
          catch { }
          if (node.Data.Url != null && node.Data.Url.StartsWith("~/"))
          {
            try { node.Data.Url = Utility.ResolveUrl(node.Data.Url); }
            catch { }
          }
        }
      }
    }


    public static string MenuFormatterWorker(FS_Operations.FS_NodeInfo_Interface node)
    {
      string url = null;
      if (node == null)
        return url;
      //
      try
      {
        Type pageType = IKCMS_RegisteredTypes.Types_IKCMS_ResourceWithUrl_Interface.FirstOrDefault(t => t.Name == node.vData.manager_type);
        //var target = ((node.vData.FlagsMenu & FlagsMenuEnum.TargetBlank) == FlagsMenuEnum.TargetBlank) ? "_blank" : null;
        if (node.vData.flag_unstructured)
        {
          url = UrlDownloadDefaultStream(node.vNode, node.vData, node.iNode, string.Empty);
          return url;
        }
        if (pageType == null)
        {
          return null;
        }
        if (pageType.IsAssignableTo(typeof(IKCMS_ResourceType_PageStatic)))
        {
          url = (IKCMS_RegisteredTypes.Deserialize_IKCMS_ResourceVFS(node) as IKCMS_ResourceType_PageStatic).ResourceSettings.UrlExternal;
          if (url.IsNotEmpty())
          {
            return url;
          }
        }
        url = UrlFormatterWorkerBase(node.sNode, node.rNode, node.Language, node.Name, node.vData.FlagsMenu);
      }
      catch { }
      finally
      {
        if (url != null)
        {
          try { url = IKGD_SEO_Manager.MapOutcomingUrl(node.vNode.snode, node.vNode.rnode, null) ?? url; }
          catch { }
          if (url != null && url.StartsWith("~/"))
          {
            try { url = Utility.ResolveUrl(url); }
            catch { }
          }
        }
      }
      return url;
    }


    public static string UrlFormatterWorkerBase(int? sNode, int? rNode, string language, string fName, FlagsMenuEnum flags) { return UrlFormatterWorkerBase(sNode, rNode, language, fName, flags, null, null, null); }
    public static string UrlFormatterWorkerBase(int? sNode, int? rNode, string language, string fName, FlagsMenuEnum flags, UrlGeneratorFormatEnum? fmtMode, FS_Operations.FS_TreeNode<TreeNodeInfoVFS> treeNode, List<IKGD_Path> pathsRoots)
    {
      string url = null;
      string urlFallBack = null;
      fmtMode = fmtMode ?? UrlGeneratorFormat;
      if ((flags & FlagsMenuEnum.UseCodeCmsForUrl) == FlagsMenuEnum.UseCodeCmsForUrl)
        fmtMode = UrlGeneratorFormatEnum.code_sNode;
      if (sNode != null && url.IsNullOrEmpty())
      {
        switch (fmtMode)
        {
          case UrlGeneratorFormatEnum.code_sNode_lastPathFragment:
            url = IKCMS_RouteUrlManager.GetMvcUrlGeneralV2("code", sNode.GetValueOrDefault(), null, "/" + Utility.UrlEncodeIndexPathForSEO(fName), false);
            break;
          case UrlGeneratorFormatEnum.language_sNode_lastPathFragment:
            url = IKCMS_RouteUrlManager.GetMvcUrlGeneralV2(language, sNode.GetValueOrDefault(), null, "/" + Utility.UrlEncodeIndexPathForSEO(fName), false);
            break;
          case UrlGeneratorFormatEnum.code_sNode:
            url = IKCMS_RouteUrlManager.GetMvcUrlGeneral(sNode.GetValueOrDefault());
            break;
          default:
            urlFallBack = IKCMS_RouteUrlManager.GetMvcUrlGeneralV2(language, sNode.GetValueOrDefault(), null, "/" + Utility.UrlEncodeIndexPathForSEO(fName), false);
            break;
        }
      }
      if (rNode != null && url.IsNullOrEmpty())
      {
        switch (fmtMode)
        {
          case UrlGeneratorFormatEnum.rnode_rNode:
            url = "~/rnode/" + rNode.ToString();
            break;
          case UrlGeneratorFormatEnum.rnode_rNode_lastPathFragment:
            url = "~/rnode/" + rNode.ToString() + "/" + Utility.UrlEncodeIndexPathForSEO(fName);
            break;
          case UrlGeneratorFormatEnum.language_rNode_lastPathFragment:
            url = IKCMS_RouteUrlManager.GetMvcUrlGeneralRNODEV2(language, rNode.GetValueOrDefault(), null, "/" + Utility.UrlEncodeIndexPathForSEO(fName), false, null);
            break;
          default:
            urlFallBack = IKCMS_RouteUrlManager.GetMvcUrlGeneralRNODEV2(language, rNode.GetValueOrDefault(), null, "/" + Utility.UrlEncodeIndexPathForSEO(fName), false, null);
            break;
        }
      }
      if (url.IsNullOrEmpty())
      {
        switch (fmtMode)
        {
          case UrlGeneratorFormatEnum.CMS_optimizedPath:
            if (treeNode != null)
            {
              url = "~/CMS" + treeNode.GetNodePathVFS(pathsRoots);
            }
            break;
        }
      }
      if (url.IsNullOrWhiteSpace())
      {
        url = urlFallBack;
      }
      if (url.IsNullOrWhiteSpace())
      {
        if (sNode != null)
        {
          url = IKCMS_RouteUrlManager.GetMvcUrlGeneral(sNode.GetValueOrDefault());
        }
        else if (rNode != null)
        {
          url = "~/rnode/" + rNode.ToString();
        }
      }
      return url;
    }


    public static string UrlDownloadDefaultStream(FS_Operations.FS_NodeInfo_Interface fsNode) { return UrlDownloadDefaultStream(fsNode.vNode, fsNode.vData, fsNode.iNode, string.Empty); }
    public static string UrlDownloadDefaultStream(FS_Operations.FS_NodeInfo_Interface fsNode, string stream) { return UrlDownloadDefaultStream(fsNode.vNode, fsNode.vData, fsNode.iNode, stream); }
    public static string UrlDownloadDefaultStream(IKGD_VNODE vNode, IKGD_VDATA vData, IKGD_INODE iNode, string stream)
    {
      string url = null;
      try
      {
        if (vNode != null)
        {
          string fileName = vNode.name;
          string fileOrig = null;
          if (iNode != null)
          {
            fileOrig = Utility.PathGetFileNameSanitized(iNode.filename);
          }
          string fileExt = Utility.PathGetExtensionSanitized(fileOrig ?? string.Empty);
          if (!string.IsNullOrEmpty(fileExt) && !fileName.EndsWith(fileExt, StringComparison.OrdinalIgnoreCase))
          {
            fileName = Utility.PathGetFileNameSanitized(fileName.Trim(' ', '.') + fileExt);
          }
          url = IKCMS_RouteUrlManager.GetUrlProxyVFS(vNode.rnode, null, stream, null, null, false, null, false, fileName);
          if (!string.IsNullOrEmpty(fileExt))
          {
            url = Utility.UriAddQuery(url, "ext", fileExt);
          }
        }
      }
      catch { }
      return url;
    }


    //
    // dovrebbe essere piu' configurabile per poter selezionare quale frazione del root path includere
    // probabilmente la url non deve venire definita in fase di fetch dal DB, deve essere il controller
    // a creare la URL corretta interpretando le pagine, il fetch da DB dovrebbe popolare solamente
    // le url NON CMS deserializzando l'oggetto, poi si deve distinguere tra nodi navigabili e non
    // navigabili (url null o javascript:;)
    //
    public static string GetNodePathVFS(this FS_Operations.FS_TreeNode<TreeNodeInfoVFS> treeNode, List<IKGD_Path> pathsRoots)
    {
      try
      {
        IEnumerable<string> frags = treeNode.BackRecurseOnData.Where(f => f != null).Select(f => f.vNode.name).Reverse();
        if (pathsRoots != null)
        {
          try
          {
            int rNode = treeNode.BackRecurseOnData.LastOrDefault(f => f != null).ParentFolder.Value;
            var fragsPre = pathsRoots.FirstOrDefault(p => p.LastFragment.rNode == rNode).Fragments;
            frags = fragsPre.Skip(fragsPre.FindLastIndex(f => IKGD_ConfigVFS.Config.RootsCMS_folders.Contains(f.rNode)) + 1).Select(f => f.Name).Concat(frags);
            //frags = pathsRoots.FirstOrDefault(p => p.LastFragment.rNode == rNode).Fragments.Where(f => f.Parent != 0).Select(f => f.Name).Concat(frags);
          }
          catch { }
        }
        frags = Utility.UrlEncodePathFragments_IIS(frags);
        return "/" + string.Join("/", frags.ToArray());
      }
      catch { }
      return string.Empty;
    }


    /*
    public static string GetNodePathVFS_OLD(this FS_Operations.FS_TreeNode<TreeNodeInfoVFS> treeNode, List<IKGD_Path> pathsRoots)
    {
      int rNode = -1;
      StringBuilder sb = new StringBuilder();
      while (treeNode != null && treeNode.Data != null && treeNode.Parent != null)
      {
        rNode = treeNode.Data.vNode.parent.Value;
        sb.Insert(0, string.Format("/{0}", treeNode.Data.vNode.name));
        treeNode = treeNode.Parent;
      }
      if (pathsRoots != null)
      {
        try { sb.Insert(0, pathsRoots.FirstOrDefault(p => p.LastFragment.rNode == rNode).Path); }
        catch { }
      }
      //TODO: taroccamento per saltare i primi due livelli di menu'
      string path = sb.ToString();
      if (path.StartsWith("/Root/", StringComparison.OrdinalIgnoreCase))
        path = "/" + string.Join("/", path.Split('/').Skip(3).ToArray()).TrimStart('/');
      //
      path = Utility.UrlEncodePath_IIS(path, false);
      //
      return path;
    }
    */



    public class IKCMS_TreeStructureVFS_CacheElement
    {
      public static readonly string baseKeyCache = "IKCMS_TreeStructureVFS_";
      public List<int> sNodesRoot { get; set; }
      public List<string> pathsRoot { get; set; }
      public string cacheKeyForData { get; set; }
      //
      public byte[] data { get { return (string.IsNullOrEmpty(cacheKeyForData)) ? null : (byte[])HttpRuntime.Cache[cacheKeyForData]; } }
      //


      public static string HashFactory(IEnumerable<string> pathsRoot, IEnumerable<int> sNodesRoot)
      {
        return FS_OperationsHelpers.ContextHash(baseKeyCache, Utility.Implode(sNodesRoot ?? Enumerable.Empty<int>(), "|"), Utility.Implode(pathsRoot ?? Enumerable.Empty<string>(), "|"));
      }


      public static AggregateCacheDependency CacheDependencyFactory(int? rnode, int? snode, int? inode, string relationType)
      {
        AggregateCacheDependency sqlDeps = new AggregateCacheDependency();
        FS_OperationsHelpers.Const_CacheDependencyIKGD_vNode_vData.ForEach(t => sqlDeps.Add(new SqlCacheDependency("GDCS", t)));
        return sqlDeps;
      }

    }

  }

}
