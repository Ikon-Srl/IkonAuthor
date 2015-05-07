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
using System.Collections.Specialized;
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
using System.Configuration.Provider;
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
using Autofac.Integration.Web;

using Ikon;
using Ikon.GD;
using Ikon.Config;
using Ikon.IKCMS;


namespace Ikon.IKCMS
{
  using Ikon.IKGD.Library.Resources;



  public class IKCMS_ModelCMS_ManagerVFS : IEnumerable<FS_Operations.VFS_TreeNode<FS_Operations.FS_NodeInfo_Interface>>, IBootStrapperAutofacTask
  {
    //
    private static object _lock = new object();
    public static readonly string keyItems = "IKCMS_ModelCMS_ManagerVFS_items";
    //
    public FS_Operations.VFS_TreeNode<FS_Operations.FS_NodeInfo_Interface> Tree { get; protected set; }
    public IEnumerable<FS_Operations.VFS_TreeNode<FS_Operations.FS_NodeInfo_Interface>> NodesTree { get { return mapper_snode.Values.OfType<FS_Operations.VFS_TreeNode<FS_Operations.FS_NodeInfo_Interface>>(); } }
    public IEnumerable<FS_Operations.FS_NodeInfo_Interface> NodesVFS { get { return mapper_snode.Values.OfType<FS_Operations.VFS_TreeNode<FS_Operations.FS_NodeInfo_Interface>>().Where(n => n.Data != null).Select(n => n.Data); } }
    public bool Enabled { get; set; }
    public bool HasExceptions { get; set; }
    public bool HasErrors { get; set; }  // non viene usato internamente ma puo' essere usato da codice esterno per mantenere flags condivisi per la request
    public static bool decouplingEnabled = Utility.TryParse<bool>(IKGD_Config.AppSettings["IKCMS_ModelCMS_ManagerVFS_DecouplingEnabled"], true);
    //
    protected Hashtable mapper_snode { get; private set; }
    protected Hashtable mapper_rnode { get; private set; }
    protected Hashtable mapper_folder { get; private set; }
    protected Hashtable mapper_parent { get; private set; }
    //
    public FS_Operations fsOp { get { return IKCMS_ManagerIoC.requestContainer.ResolveNamed<FS_Operations>("readonly"); } }
    //private FS_Operations _fsOp { get { return (HttpContext.Current != null) ? HttpContext.Current.Items["IKCMS_ModelCMS_ManagerVFS_fsOp"] as FS_Operations : null; } set { if (HttpContext.Current != null) HttpContext.Current.Items["IKCMS_ModelCMS_ManagerVFS_fsOp"] = value; } }
    //public FS_Operations fsOp { get { return _fsOp ?? (_fsOp = IKCMS_ManagerIoC.requestContainer.ResolveNamed<FS_Operations>("readonly")); } }
    //public FS_Operations fsOp { get { return _fsOp ?? (_fsOp = IKCMS_ManagerIoC.requestContainer.Resolve<FS_Operations>()); } }
    //
    public double CumulatedWaits { get; protected set; }
    public int CumulatedReads { get; protected set; }
    //


    public IKCMS_ModelCMS_ManagerVFS()
    {
      Tree = new FS_Operations.VFS_TreeNode<FS_Operations.FS_NodeInfo_Interface>(null, null);
      mapper_snode = new Hashtable();
      mapper_rnode = new Hashtable();
      mapper_folder = new Hashtable();
      mapper_parent = new Hashtable();
      Enabled = true;
      CumulatedWaits = 0.0;
      CumulatedReads = 0;
    }


    // registrazione automatica nel framework IoC
    public virtual void ExecuteAutofac(ContainerBuilder builder)
    {
      builder.RegisterType<IKCMS_ModelCMS_ManagerVFS>();
      builder.RegisterType<IKCMS_ModelCMS_ManagerVFS>().Named<IKCMS_ModelCMS_ManagerVFS>("request").InstancePerHttpRequest();
      //
      //IKCMS_ModelCMS_ManagerVFS repository = IKCMS_ManagerIoC.applicationContainer.Resolve<IKCMS_ModelCMS_ManagerVFS>();
    }


    //
    // factory class con approccio misto context/request per funzionare sia in un contesto IoC
    // che con ottimizzazione per renderaction in modo da evitare riletture inutili dal VFS
    //
    public static IKCMS_ModelCMS_ManagerVFS Factory()
    {
      if (HttpContext.Current != null)
      {
        IKCMS_ModelCMS_ManagerVFS _managerVFS = HttpContext.Current.Items[keyItems] as IKCMS_ModelCMS_ManagerVFS;
        if (_managerVFS != null)
          return _managerVFS;
        lock (_lock)
        {
          if (HttpContext.Current.Items[keyItems] == null)
          {
            HttpContext.Current.Items[keyItems] = _managerVFS = IKCMS_ManagerIoC.requestContainer.ResolveNamed<IKCMS_ModelCMS_ManagerVFS>("request");
            //HttpContext.Current.Items[keyItems] = _managerVFS = IKCMS_ManagerIoC.requestContainer.Resolve<IKCMS_ModelCMS_ManagerVFS>();
          }
          return _managerVFS;
        }
      }
      else
      {
        return IKCMS_ManagerIoC.requestContainer.ResolveNamed<IKCMS_ModelCMS_ManagerVFS>("request");
      }
    }


    public void Clear()
    {
      lock (_lock)
      {
        mapper_snode.Clear();
        mapper_rnode.Clear();
        mapper_folder.Clear();
        mapper_parent.Clear();
        Tree.Clear();  // per eliminare eventuali riferimenti ricorsivi che possono mettere in difficolta' il GC
        Tree = new FS_Operations.VFS_TreeNode<FS_Operations.FS_NodeInfo_Interface>(null, null);
      }
    }


    public FS_Operations.VFS_TreeNode<FS_Operations.FS_NodeInfo_Interface> this[int snode]
    {
      get { return mapper_snode[snode] as FS_Operations.VFS_TreeNode<FS_Operations.FS_NodeInfo_Interface>; }
    }


    public IEnumerator<FS_Operations.VFS_TreeNode<FS_Operations.FS_NodeInfo_Interface>> GetEnumerator() { return Tree.RecurseOnTreeOrdered.GetEnumerator(); }
    IEnumerator IEnumerable.GetEnumerator() { return Tree.RecurseOnTreeOrdered.GetEnumerator(); }


    //
    // registra un nodo nel repository aggiornando tutte le strutture relative
    // nel caso un nodo sia gia' registrato lo sovrascrive solo nel caso si tratti di un nodo con una classe piu' specializzata di quella esistente
    //
    public FS_Operations.VFS_TreeNode<FS_Operations.FS_NodeInfo_Interface> RegisterNode(FS_Operations.FS_NodeInfo_Interface vfsNode)
    {
      if (vfsNode != null)
      {
        try
        {
          FS_Operations.VFS_TreeNode<FS_Operations.FS_NodeInfo_Interface> node = mapper_snode[vfsNode.vNode.snode] as FS_Operations.VFS_TreeNode<FS_Operations.FS_NodeInfo_Interface>;
          if (node == null)
          {
            // duplicazione del nodo per disaccoppiarlo dal datacontext
            FS_Operations.FS_NodeInfo_Interface vfsNodeDup = null;
            if (decouplingEnabled)
            {
              vfsNodeDup = vfsNode.Clone() as FS_Operations.FS_NodeInfo_Interface;
            }
            //
            // il sistema non e' stato progettato per gestire situazioni con parents degeneri
            // attenzione che parentnode potrebbe essere piu' d'uno (es. costruzione di un model partendo da un rNode)
            //
            FS_Operations.VFS_TreeNode<FS_Operations.FS_NodeInfo_Interface> parentNode = null;
            try { parentNode = ((vfsNode.vNode.flag_folder ? mapper_rnode[vfsNode.vNode.parent] : mapper_rnode[vfsNode.vNode.folder]) as IEnumerable<FS_Operations.VFS_TreeNode<FS_Operations.FS_NodeInfo_Interface>>).FirstOrDefault(n => n.Data.vNode.flag_folder); }
            catch { }
            parentNode = parentNode ?? Tree;
            node = new FS_Operations.VFS_TreeNode<FS_Operations.FS_NodeInfo_Interface>(parentNode, vfsNodeDup ?? vfsNode);
            if (vfsNode.vNode.flag_folder)
            {
              try { if (mapper_folder.ContainsKey(vfsNode.vNode.rnode)) (mapper_folder[vfsNode.vNode.rnode] as IEnumerable<FS_Operations.VFS_TreeNode<FS_Operations.FS_NodeInfo_Interface>>).Where(n => n.Parent == null).ForEach(n => n.Parent = node); }
              catch { }
              try { if (mapper_parent.ContainsKey(vfsNode.vNode.rnode)) (mapper_parent[vfsNode.vNode.rnode] as IEnumerable<FS_Operations.VFS_TreeNode<FS_Operations.FS_NodeInfo_Interface>>).Where(n => n.Parent == null).ForEach(n => n.Parent = node); }
              catch { }
            }
            //
            mapper_snode[vfsNode.vNode.snode] = node;
            ((mapper_rnode[vfsNode.vNode.rnode] ?? (mapper_rnode[vfsNode.vNode.rnode] = new List<FS_Operations.VFS_TreeNode<FS_Operations.FS_NodeInfo_Interface>>())) as List<FS_Operations.VFS_TreeNode<FS_Operations.FS_NodeInfo_Interface>>).Add(node);
            ((mapper_folder[vfsNode.vNode.folder] ?? (mapper_folder[vfsNode.vNode.folder] = new List<FS_Operations.VFS_TreeNode<FS_Operations.FS_NodeInfo_Interface>>())) as List<FS_Operations.VFS_TreeNode<FS_Operations.FS_NodeInfo_Interface>>).Add(node);
            //
            if (vfsNode.vNode.parent != null)
              ((mapper_parent[vfsNode.vNode.parent] ?? (mapper_parent[vfsNode.vNode.parent] = new List<FS_Operations.VFS_TreeNode<FS_Operations.FS_NodeInfo_Interface>>())) as List<FS_Operations.VFS_TreeNode<FS_Operations.FS_NodeInfo_Interface>>).Add(node);
            //
          }
          else if (node.Data.GetType() != vfsNode.GetType())
          {
            if (node.Data.GetType().IsAssignableFrom(vfsNode.GetType()))
            {
              FS_Operations.FS_NodeInfo_Interface vfsNodeDup = null;
              if (decouplingEnabled)
              {
                // duplicazione del nodo per disaccoppiarlo dal datacontext
                vfsNodeDup = vfsNode.Clone() as FS_Operations.FS_NodeInfo_Interface;
              }
              node.Data = vfsNodeDup ?? vfsNode;
            }
          }
          //
          return node;
        }
        catch (Exception ex)
        {
          Elmah.ErrorSignal.FromCurrentContext().Raise(ex);
          IKCMS_ExceptionsManager.Add(new IKCMS_Exception_ManagerVFS("RegisterNode", ex));
          HasExceptions = true;
        }
      }
      return null;
    }


    public void RegisterNodes(IEnumerable<FS_Operations.FS_NodeInfo_Interface> vfsNodes)
    {
      try { vfsNodes.ForEach(n => RegisterNode(n)); }
      catch { }
    }


    public void RegisterNodes(IEnumerable<FS_Operations.FS_NodeInfoExt_Interface> vfsNodes)
    {
      try { vfsNodes.ForEach(n => RegisterNode(n)); }
      catch { }
    }


    public void RegisterNodes(IEnumerable<FS_Operations.FS_NodeInfoExt2_Interface> vfsNodes)
    {
      try { vfsNodes.ForEach(n => RegisterNode(n)); }
      catch { }
    }


    public void FetchNodesT<vfsNodeT>(Expression<Func<IKGD_VNODE, bool>> vNodeFilter, Expression<Func<IKGD_VDATA, bool>> vDataFilter) { FetchNodesT<vfsNodeT>(vNodeFilter, vDataFilter, null, false); }
    public void FetchNodesT<vfsNodeT>(Expression<Func<IKGD_VNODE, bool>> vNodeFilter, Expression<Func<IKGD_VDATA, bool>> vDataFilter, FS_Operations.FilterVFS? filters, bool force)
    {
      Stopwatch sw = Stopwatch.StartNew();
      try
      {
        if (Enabled || force)
        {
          if (typeof(FS_Operations.FS_NodeInfoExt2).IsAssignableFrom(typeof(vfsNodeT)))
          {
            fsOp.Get_NodesInfoFilteredExt3(vNodeFilter, vDataFilter, filters.GetValueOrDefault(FS_Operations.FiltersVFS_Default)).ForEach(n => RegisterNode(n));
          }
          else if (typeof(FS_Operations.FS_NodeInfoExt).IsAssignableFrom(typeof(vfsNodeT)))
          {
            fsOp.Get_NodesInfoFilteredExt2(vNodeFilter, vDataFilter, filters.GetValueOrDefault(FS_Operations.FiltersVFS_Default)).ForEach(n => RegisterNode(n));
          }
          else
          {
            fsOp.Get_NodesInfoFilteredExt1(vNodeFilter, vDataFilter, filters.GetValueOrDefault(FS_Operations.FiltersVFS_Default)).ForEach(n => RegisterNode(n));
          }
        }
      }
      catch (Exception ex)
      {
        Elmah.ErrorSignal.FromCurrentContext().Raise(ex);
        IKCMS_ExceptionsManager.Add(new IKCMS_Exception_ManagerVFS("FetchNodesT", ex));
        HasExceptions = true;
      }
      finally
      {
        CumulatedReads++;
        CumulatedWaits += sw.Elapsed.TotalSeconds;
      }
    }


    public void FetchNodes(Expression<Func<IKGD_VNODE, bool>> vNodeFilter, Expression<Func<IKGD_VDATA, bool>> vDataFilter, vfsNodeFetchModeEnum fetchMode) { FetchNodes(vNodeFilter, vDataFilter, fetchMode, null, false); }
    public void FetchNodes(Expression<Func<IKGD_VNODE, bool>> vNodeFilter, Expression<Func<IKGD_VDATA, bool>> vDataFilter, vfsNodeFetchModeEnum fetchMode, FS_Operations.FilterVFS? filters, bool force)
    {
      Stopwatch sw = Stopwatch.StartNew();
      try
      {
        if (Enabled || force)
        {
          switch (fetchMode)
          {
            case vfsNodeFetchModeEnum.vNode_vData_iNode_ExtraVariants:
              fsOp.Get_NodesInfoFilteredExt3(vNodeFilter, vDataFilter, filters.GetValueOrDefault(FS_Operations.FiltersVFS_Default)).ForEach(n => RegisterNode(n));
              break;
            case vfsNodeFetchModeEnum.vNode_vData_iNode_Extra:
              fsOp.Get_NodesInfoFilteredExt2(vNodeFilter, vDataFilter, filters.GetValueOrDefault(FS_Operations.FiltersVFS_Default)).ForEach(n => RegisterNode(n));
              break;
            case vfsNodeFetchModeEnum.vNode_vData_iNode:
            case vfsNodeFetchModeEnum.vNode_vData:
            default:
              fsOp.Get_NodesInfoFilteredExt1(vNodeFilter, vDataFilter, filters.GetValueOrDefault(FS_Operations.FiltersVFS_Default)).ForEach(n => RegisterNode(n));
              break;
          }
        }
      }
      catch (Exception ex)
      {
        Elmah.ErrorSignal.FromCurrentContext().Raise(ex);
        IKCMS_ExceptionsManager.Add(new IKCMS_Exception_ManagerVFS("FetchNodes", ex));
        HasExceptions = true;
      }
      finally
      {
        CumulatedReads++;
        CumulatedWaits += sw.Elapsed.TotalSeconds;
      }
    }


    public void EnsureNodes<vfsNodeT>(IEnumerable<FS_Operations.VFS_TreeNode<FS_Operations.FS_NodeInfo_Interface>> nodes) where vfsNodeT : FS_Operations.FS_NodeInfo_Interface
    {
      EnsureNodes<vfsNodeT>(nodes.Where(n => n.Data != null && !typeof(vfsNodeT).IsAssignableFrom(n.Data.GetType())).Select(n => n.Data.vNode.snode));
    }
    public void EnsureNodes<vfsNodeT>(IEnumerable<FS_Operations.FS_NodeInfo_Interface> nodes) where vfsNodeT : FS_Operations.FS_NodeInfo_Interface
    {
      EnsureNodes<vfsNodeT>(nodes.Where(n => n != null && !typeof(vfsNodeT).IsAssignableFrom(n.GetType())).Select(n => n.vNode.snode));
    }
    public void EnsureNodes<vfsNodeT>(params int[] sNodes) where vfsNodeT : FS_Operations.FS_NodeInfo_Interface { EnsureNodes<vfsNodeT>((IEnumerable<int>)sNodes, null, false); }
    public void EnsureNodes<vfsNodeT>(IEnumerable<int> sNodes) where vfsNodeT : FS_Operations.FS_NodeInfo_Interface { EnsureNodes<vfsNodeT>(sNodes, null, false); }
    public void EnsureNodes<vfsNodeT>(IEnumerable<int> sNodes, FS_Operations.FilterVFS? filters, bool force) where vfsNodeT : FS_Operations.FS_NodeInfo_Interface
    {
      List<int> sNodesToFetch = sNodes.Where(s => mapper_snode[s] == null || typeof(vfsNodeT).IsAssignableFrom((mapper_snode[s] as FS_Operations.VFS_TreeNode<FS_Operations.FS_NodeInfo_Interface>).Data.GetType())).ToList();
      if (sNodesToFetch.Any())
      {
        FetchNodesT<vfsNodeT>(vn => sNodesToFetch.Contains(vn.snode), null, filters, force);
      }
    }


    public void EnsureNodesRNODE<vfsNodeT>(IEnumerable<int> rNodes, bool force) where vfsNodeT : FS_Operations.FS_NodeInfo_Interface
    {
      List<int> rNodesToFetch = rNodes.Where(r => mapper_rnode[r] == null || !typeof(vfsNodeT).IsAssignableFrom((mapper_rnode[r] as IEnumerable<FS_Operations.VFS_TreeNode<FS_Operations.FS_NodeInfo_Interface>>).FirstOrDefault().Data.GetType())).ToList();
      if (rNodesToFetch.Any())
      {
        FetchNodesT<vfsNodeT>(vn => rNodesToFetch.Contains(vn.rnode), null, null, force);
      }
    }


    public FS_Operations.FS_NodeInfo_Interface EnsureNodeOrRegister<vfsNodeT>(FS_Operations.FS_NodeInfo_Interface node) where vfsNodeT : FS_Operations.FS_NodeInfo_Interface
    {
      try
      {
        if (node != null && node.GetType() == typeof(vfsNodeT))
        {
          return RegisterNode(node).Data;
        }
        else if (node != null)
        {
          FS_Operations.FS_NodeInfo_Interface res = RegisterNode(node).Data;
          if (!typeof(vfsNodeT).IsAssignableFrom(res.GetType()))
          {
            FetchNodesT<vfsNodeT>(vn => vn.snode == node.vNode.snode, null);
          }
          if (mapper_snode.ContainsKey(node.vNode.snode))
          {
            return this[node.vNode.snode].Data;
          }
        }
        else
        {
          //throw new ArgumentNullException("EnsureNodeOrRegister: node is NULL");
        }
        return (vfsNodeT)node;
      }
      catch (Exception ex)
      {
        Elmah.ErrorSignal.FromCurrentContext().Raise(ex);
        IKCMS_ExceptionsManager.Add(new IKCMS_Exception_ManagerVFS("EnsureNodeOrRegister", ex));
        HasExceptions = true;
        return node;
      }
    }


    //
    // tree read support
    //
    public void EnsureTrees<vfsNodeT>(int? maxRecursionLevel, bool? fetchRelations, params int[] rNodes) where vfsNodeT : FS_Operations.FS_NodeInfo_Interface
    {
      //
      Stopwatch sw = Stopwatch.StartNew();
      try
      {
        maxRecursionLevel = maxRecursionLevel ?? 10;  // per limitare la ricorsione
        //
        Expression<Func<IKGD_VNODE, bool>> vNodeFilter = fsOp.Get_vNodeFilterACLv2();
        Expression<Func<IKGD_VDATA, bool>> vDataFilter = fsOp.Get_vDataFilterACLv2();
        Expression<Func<IKGD_VNODE, bool>> vNodeFilterFolders = vNodeFilter.And(n => n.flag_folder);
        //
        List<int> foldersToRead = new List<int>();
        foreach (var rNode in rNodes)
        {
          List<int> foldersToScan = new List<int> { rNode };
          for (int i = 0; i < maxRecursionLevel.Value && foldersToScan.Count > 0; i++)
          {
            foldersToRead.AddRange(foldersToScan);
            List<int> newFolderSet = new List<int>();
            foreach (var foldersSlice in foldersToScan.Slice(100))
            {
              List<int> newSlicedFolderSet =
                (from vNode in fsOp.NodesActive<IKGD_VNODE>().Where(vNodeFilterFolders).Where(n => n.parent != null && foldersSlice.Contains(n.parent.Value))
                 from vData in fsOp.NodesActive<IKGD_VDATA>().Where(vDataFilter).Where(n => n.rnode == vNode.rnode)
                 select vNode.folder).ToList();
              if (newSlicedFolderSet != null && newSlicedFolderSet.Any())
              {
                newFolderSet.AddRange(newSlicedFolderSet);
              }
              IKCMS_ModelCMS_Provider.Provider.managerVFS.CumulatedReads++;
            }
            foldersToScan = newFolderSet.Distinct().Except(foldersToRead).ToList();
            if (!foldersToScan.Any())
              break;
          }
        }
        //
        List<int> relationsToRead = new List<int>();
        do
        {
          foreach (var rNodesSlice in foldersToRead.Distinct().Slice(250))
          {
            //Expression<Func<IKGD_VNODE, bool>> vNodeFilterBulk = vNodeFilter.And(n => rNodesSlice.Any(r => r == n.folder || r == n.rnode));  // attenzione linq2sql non compila questa query potenzialmente migliore
            Expression<Func<IKGD_VNODE, bool>> vNodeFilterBulk = vNodeFilter.And(n => rNodesSlice.Contains(n.folder) || rNodesSlice.Contains(n.rnode));
            if (typeof(FS_Operations.FS_NodeInfoExt2).IsAssignableFrom(typeof(vfsNodeT)))
            {
              var fsNodes = fsOp.Get_NodesInfoFilteredExt3(vNodeFilterBulk, vDataFilter).ToList();
              IKCMS_ModelCMS_Provider.Provider.managerVFS.RegisterNodes(fsNodes);
              IKCMS_ModelCMS_Provider.Provider.managerVFS.CumulatedReads += fsNodes.Count;
              if (fetchRelations.GetValueOrDefault(true))
              {
                relationsToRead.AddRange(fsNodes.Where(n => n.Relations != null).SelectMany(n => n.Relations.Where(r => r.type == Ikon.IKGD.Library.IKGD_Constants.IKGD_LinkRelationName).Select(r => r.rnode_dst)));
              }
            }
            else if (typeof(FS_Operations.FS_NodeInfoExt).IsAssignableFrom(typeof(vfsNodeT)))
            {
              var fsNodes = fsOp.Get_NodesInfoFilteredExt2(vNodeFilterBulk, vDataFilter).ToList();
              IKCMS_ModelCMS_Provider.Provider.managerVFS.RegisterNodes(fsNodes);
              IKCMS_ModelCMS_Provider.Provider.managerVFS.CumulatedReads += fsNodes.Count;
              if (fetchRelations.GetValueOrDefault(true))
              {
                relationsToRead.AddRange(fsNodes.Where(n => n.Relations != null).SelectMany(n => n.Relations.Where(r => r.type == Ikon.IKGD.Library.IKGD_Constants.IKGD_LinkRelationName).Select(r => r.rnode_dst)));
              }
            }
            else
            {
              var fsNodes = fsOp.Get_NodesInfoFilteredExt1(vNodeFilterBulk, vDataFilter).ToList();
              IKCMS_ModelCMS_Provider.Provider.managerVFS.RegisterNodes(fsNodes);
              IKCMS_ModelCMS_Provider.Provider.managerVFS.CumulatedReads += fsNodes.Count;
            }
          }
          //
          if (relationsToRead != null && relationsToRead.Any())
          {
            foldersToRead = relationsToRead.Distinct().Except(foldersToRead).ToList();
            relationsToRead.Clear();
          }
          else
          {
            foldersToRead.Clear();
          }
          //
        } while (relationsToRead.Any());
        //
      }
      catch (Exception ex)
      {
        Elmah.ErrorSignal.FromCurrentContext().Raise(ex);
        IKCMS_ExceptionsManager.Add(new IKCMS_Exception_ManagerVFS("EnsureTrees", ex));
        HasExceptions = true;
      }
      IKCMS_ModelCMS_Provider.Provider.managerVFS.CumulatedWaits += sw.Elapsed.TotalSeconds;
    }




    //
    // accessor methods
    //
    public FS_Operations.VFS_TreeNode<FS_Operations.FS_NodeInfo_Interface> GetTreeNode(int? sNode, int? rNode)
    {
      try
      {
        if (sNode != null && mapper_snode.ContainsKey(sNode.Value))
          return mapper_snode[sNode.Value] as FS_Operations.VFS_TreeNode<FS_Operations.FS_NodeInfo_Interface>;
        else if (rNode != null && mapper_rnode.ContainsKey(rNode.Value))
          return (mapper_rnode[rNode.Value] as IEnumerable<FS_Operations.VFS_TreeNode<FS_Operations.FS_NodeInfo_Interface>>).FirstOrDefault();
      }
      catch { }
      return null;
    }


    public FS_Operations.FS_NodeInfo_Interface GetVfsNode(int? sNode, int? rNode)
    {
      try
      {
        if (sNode != null && mapper_snode.ContainsKey(sNode.Value))
          return (mapper_snode[sNode.Value] as FS_Operations.VFS_TreeNode<FS_Operations.FS_NodeInfo_Interface>).Data;
        else if (rNode != null && mapper_rnode.ContainsKey(rNode.Value))
          return (mapper_rnode[rNode.Value] as IEnumerable<FS_Operations.VFS_TreeNode<FS_Operations.FS_NodeInfo_Interface>>).FirstOrDefault().Data;
      }
      catch { }
      return null;
    }


    //public IEnumerable<FS_Operations.VFS_TreeNode<FS_Operations.FS_NodeInfo_Interface>> GetTreeNodes(int? rNodeOrFolder, int? parentNode)
    //{
    //  try
    //  {
    //    if (rNodeOrFolder != null && mapper_rnode.ContainsKey(rNodeOrFolder.Value))
    //      return mapper_rnode[rNodeOrFolder.Value] as IEnumerable<FS_Operations.VFS_TreeNode<FS_Operations.FS_NodeInfo_Interface>>;
    //    if (parentNode != null && mapper_rnode.ContainsKey(parentNode.Value))
    //      return mapper_parent[parentNode.Value] as IEnumerable<FS_Operations.VFS_TreeNode<FS_Operations.FS_NodeInfo_Interface>>;
    //  }
    //  catch { }
    //  return null;
    //}


    //public IEnumerable<FS_Operations.FS_NodeInfo_Interface> GetVfsNodes(int? rNodeOrFolder, int? parentNode)
    //{
    //  try
    //  {
    //    if (rNodeOrFolder != null && mapper_rnode.ContainsKey(rNodeOrFolder.Value))
    //      return (mapper_rnode[rNodeOrFolder.Value] as IEnumerable<FS_Operations.VFS_TreeNode<FS_Operations.FS_NodeInfo_Interface>>).Select(n => n.Data);
    //    if (parentNode != null && mapper_rnode.ContainsKey(parentNode.Value))
    //      return (mapper_parent[parentNode.Value] as IEnumerable<FS_Operations.VFS_TreeNode<FS_Operations.FS_NodeInfo_Interface>>).Select(n => n.Data);
    //  }
    //  catch { }
    //  return null;
    //}



  }


}
