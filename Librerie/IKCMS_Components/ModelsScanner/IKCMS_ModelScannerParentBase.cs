using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Web;
using System.Xml.Linq;
using System.Web.Mvc;
using System.Web.Mvc.Html;
using System.Reflection;
using System.Diagnostics;
using LinqKit;
using Autofac;

using Ikon;
using Ikon.GD;
using Ikon.IKCMS.Library.Resources;
using Ikon.IKGD.Library.Collectors;
using Ikon.IKCMS.Pagers;
using Ikon.Indexer;
using Ikon.IKGD.Library;
using System.Web.Caching;


namespace Ikon.IKCMS
{

  //
  // usage:
  // IKCMS_ModelScannerParentBase modelScannerBack = IKCMS_ManagerIoC.applicationContainer.Resolve<IKCMS_ModelScannerParentBase>();
  // var parentModelSet = modelScannerBack.FindModels(pageModel);
  //
  public abstract class IKCMS_ModelScannerParentBase : IBootStrapperAutofacTask
  {
    //
    //public DataStorage DataStorageCached { get; }  // definita nel seguito
    //
    //public virtual FS_Operations fsOp { get { return IKCMS_ManagerIoC.requestContainer.ResolveNamed<FS_Operations>("readonly"); } }
    public virtual FS_Operations fsOp { get { return IKCMS_ManagerIoC.requestContainer.Resolve<FS_Operations>(); } }
    //
    // overridable properties to configure derived classes
    public abstract Expression<Func<IKGD_VNODE, bool>> FilterVNODE { get; }  // filtro per i nodi selezionabili
    public abstract Expression<Func<IKGD_VDATA, bool>> FilterVDATA { get; }  // filtro per i nodi selezionabili
    public virtual Expression<Func<IKGD_VDATA, bool>> FilterVDATA_NOSQL { get { return FilterVDATA; } }  // nel caso si utilizzino operatori validi solo in ambiente SQL (tipo LIKE)
    //
    // se true salva i models eventualmente provenienti da modelExternal (non e' un setting consigliato perche' il modelExternal generalmente ha dei .Models gia' filtrati e lo storage perderebbe di generalita')
    public virtual bool BuildModels { get { return true; } }
    public virtual bool StoreLeafsFromModelExternal { get { return false; } }
    public virtual bool FilterCompiled { get; set; }
    public virtual Func<IKGD_VNODE, bool> CompiledFilterVNODE { get; protected set; }
    public virtual Func<IKGD_VDATA, bool> CompiledFilterVDATA { get; protected set; }
    public virtual Func<IKGD_VDATA, bool> CompiledFilterVDATA_NOSQL { get; protected set; }
    //
    public IEnumerable<FS_Operations.FS_NodeInfo_Interface> fsNodes { get { return DataStorageCached.Models.Select(m => m.vfsNode); } }
    //
    private object _lock = new object();
    //


    public IKCMS_ModelScannerParentBase()
    {
    }


    // bootstrapper per autofac con implementazione del singleton pattern
    void IBootStrapperAutofacTask.ExecuteAutofac(ContainerBuilder builder)
    {
      Type type = this.GetType();
      builder.RegisterType(type);
      //builder.RegisterType(type).SingleInstance();  // da non utilizzare altrimenti riutilizza lo stesso oggetto senza reinizializzare i parametri passati al costruttore
    }


    protected string _CachingKey;
    public virtual string CachingKey { get { return _CachingKey ?? (_CachingKey = this.GetType().Name); } set { _CachingKey = value; } }


    public DataStorage DataStorageCached
    {
      get
      {
        string key = FS_OperationsHelpers.ContextHash(CachingKey);
        return FS_OperationsHelpers.CachedEntityWrapper(key, () => { return new DataStorage(); }
        , Utility.TryParse<int>(IKGD_Config.AppSettings["CachingIKCMS_Models"], 3600), FS_OperationsHelpers.Const_CacheDependencyIKGD_vNode_vData_iNode);
      }
    }


    public void EnsureCompiledFilters()
    {
      if (FilterCompiled)
        return;
      CompiledFilterVNODE = (FilterVNODE != null) ? FilterVNODE.Compile() : n => true;
      CompiledFilterVDATA = (FilterVDATA != null) ? FilterVDATA.Compile() : n => true;
      CompiledFilterVDATA_NOSQL = (FilterVDATA_NOSQL != null) ? FilterVDATA_NOSQL.Compile() : n => true;
      FilterCompiled = true;
    }


    public virtual IEnumerable<IKCMS_ModelCMS_Interface> FindModels(IKCMS_ModelCMS_Interface modelExternal)
    {
      List<IKCMS_ModelCMS_Interface> modelsFound = null;
      List<DataStorage.DataStorageNodeInfo> nodesFound = null;
      FindWorkerNoACL(modelExternal, out modelsFound, out nodesFound);
      if (modelsFound == null || !modelsFound.Any())
        yield break;
      foreach (IKCMS_ModelCMS_Interface model in modelsFound)
        yield return model;
      //Func<IKGD_VNODE, bool> vNodeFilter = fsOp.Get_vNodeFilterACLv2(true).Compile();
      //Func<IKGD_VDATA, bool> vDataFilter = fsOp.Get_vDataFilterACLv2(false, true).Compile();
      //foreach (IKCMS_ModelCMS_Interface model in modelsFound.Where(m => vNodeFilter(m.vfsNode.vNode) && vDataFilter(m.vfsNode.vData)))
      //  yield return model;
    }


    public virtual IEnumerable<DataStorage.DataStorageNodeInfo> FindNodes(IKCMS_ModelCMS_Interface modelExternal)
    {
      List<IKCMS_ModelCMS_Interface> modelsFound = null;
      List<DataStorage.DataStorageNodeInfo> nodesFound = null;
      FindWorkerNoACL(modelExternal, out modelsFound, out nodesFound);
      if (nodesFound == null || !nodesFound.Any())
        yield break;
      foreach (DataStorage.DataStorageNodeInfo node in nodesFound)
        yield return node;
    }


    public virtual IEnumerable<IKCMS_ModelCMS_Interface> FindAllParentModels(IKCMS_ModelCMS_Interface modelExternal)
    {
      List<IKCMS_ModelCMS_Interface> modelsFound = null;
      List<DataStorage.DataStorageNodeInfo> nodesFound = null;
      FindWorkerAllParentsNoACL(modelExternal, out modelsFound, out nodesFound);
      if (modelsFound == null || !modelsFound.Any())
        yield break;
      foreach (IKCMS_ModelCMS_Interface model in modelsFound)
        yield return model;
      //Func<IKGD_VNODE, bool> vNodeFilter = fsOp.Get_vNodeFilterACLv2(true).Compile();
      //Func<IKGD_VDATA, bool> vDataFilter = fsOp.Get_vDataFilterACLv2(false, true).Compile();
      //foreach (IKCMS_ModelCMS_Interface model in modelsFound.Where(m => vNodeFilter(m.vfsNode.vNode) && vDataFilter(m.vfsNode.vData)))
      //  yield return model;
    }


    public virtual IEnumerable<DataStorage.DataStorageNodeInfo> FindAllParentNodes(IKCMS_ModelCMS_Interface modelExternal)
    {
      List<IKCMS_ModelCMS_Interface> modelsFound = null;
      List<DataStorage.DataStorageNodeInfo> nodesFound = null;
      FindWorkerAllParentsNoACL(modelExternal, out modelsFound, out nodesFound);
      if (nodesFound == null || !nodesFound.Any())
        yield break;
      foreach (DataStorage.DataStorageNodeInfo node in nodesFound)
        yield return node;
    }



    public virtual void FindWorkerNoACL(IKCMS_ModelCMS_Interface modelExternal, out List<IKCMS_ModelCMS_Interface> modelsFound, out List<DataStorage.DataStorageNodeInfo> nodesFound)
    {
      modelsFound = null;
      nodesFound = null;
      lock (_lock)
      {
        try
        {
          //
          DataStorage DataStorageCachedLocal = DataStorageCached;
          //
          EnsureCompiledFilters();
          //modelsFound = modelExternal.Models.Where(m => CompiledFilterVNODE(m.vfsNode.vNode) && CompiledFilterVDATA_NOSQL(m.vfsNode.vData)).Where(m => (m.DateActivation.GetValueOrDefault(fsOp.DateTimeContext) <= fsOp.DateTimeContext) && (fsOp.DateTimeContext <= m.DateExpiry.GetValueOrDefault(fsOp.DateTimeContext))).ToList();
          modelsFound = modelExternal.Models.Where(m => CompiledFilterVNODE(m.vfsNode.vNode) && CompiledFilterVDATA_NOSQL(m.vfsNode.vData)).Where(m => !m.IsExpired).ToList();
          nodesFound = modelsFound.Select(m => new DataStorage.DataStorageNodeInfo(m)).ToList();
          if (nodesFound.Any())
          {
            if (StoreLeafsFromModelExternal)
            {
              if (BuildModels)
                DataStorageCachedLocal.Models.AddRange(modelsFound.Except(DataStorageCachedLocal.Models));
              DataStorageCachedLocal.Nodes.AddRange(nodesFound.Except(DataStorageCachedLocal.Nodes));
            }
            return;
          }
          else
          {
            if (StoreLeafsFromModelExternal)
            {
              DataStorageCachedLocal.rNodesFoldersVoid.Add(modelExternal.rNode);
            }
          }
          //
          List<IKGD_Path> paths = modelExternal.PathsVFS;
          //

          //bool debug = true;
          //if (debug)
          //{
          //  //
          //  var rNodesAtLevelDEBUG = paths.SelectMany(p => p.Fragments.Where(f => f.flag_folder && f.rNode != modelExternal.rNode).Reverse().Select((f, i) => new { index = i, rNode = f.rNode })).Distinct().OrderBy(p => p.index).ThenBy(p => p.rNode).ToList();
          //  var rNodesActiveDEBUG = rNodesAtLevelDEBUG.Where(r => !DataStorageCachedLocal.rNodesFoldersVoid.Contains(r.rNode)).ToList();
          //  var firstNodeDEBUG = rNodesActiveDEBUG.FirstOrDefault(r => DataStorageCachedLocal.Nodes.Any(m => m.Folder == r.rNode));
          //  //
          //  List<int> _FoldersToScan = paths.SelectMany(p => p.Fragments.Select(f => f.rNode)).Distinct().ToList();
          //  Expression<Func<IKGD_VNODE, bool>> _vNodeFilterAll = PredicateBuilder.True<IKGD_VNODE>();
          //  Expression<Func<IKGD_VDATA, bool>> _vDataFilterAll = PredicateBuilder.True<IKGD_VDATA>();
          //  if (FilterVNODE != null)
          //    _vNodeFilterAll = _vNodeFilterAll.And(FilterVNODE);
          //  if (FilterVDATA != null)
          //    _vDataFilterAll = _vDataFilterAll.And(FilterVDATA);
          //  _vNodeFilterAll = _vNodeFilterAll.And(n => _FoldersToScan.Contains(n.folder));
          //  var fsNodesDEBUG = fsOp.Get_NodesInfoFiltered(_vNodeFilterAll, _vDataFilterAll, null, true, false, false).ToList();
          //}

          //
          // rNodes organizzati per livello di backscan
          var rNodesAtLevel = paths.SelectMany(p => p.Fragments.Where(f => f.flag_folder && f.rNode != modelExternal.rNode).Reverse().Select((f, i) => new { index = i, rNode = f.rNode })).Distinct().OrderBy(p => p.index).ThenBy(p => p.rNode).ToList();
          // rNodes attivi (con models o con contenuti non ancora verificati)
          var rNodesActive = rNodesAtLevel.Where(r => !DataStorageCachedLocal.rNodesFoldersVoid.Contains(r.rNode)).ToList();
          // se il primo nodo con un model gia' mappato ha l'index minimo nella lista dei nodi attivi allora non eseguo lo scan su VFS
          var firstNode = rNodesActive.FirstOrDefault(r => DataStorageCachedLocal.Nodes.Any(m => m.Folder == r.rNode));
          if (firstNode != null && firstNode.index == rNodesActive.Min(r => r.index))
          {
            var rNodes = rNodesActive.Where(r => r.index == firstNode.index).Select(r => r.rNode).Distinct().ToList();
            foreach (var rNode in rNodes)
            {
              bool found = true;
              if (BuildModels)
              {
                try { modelsFound = DataStorageCachedLocal.Models.Where(m => rNode == m.vfsNode.Folder).OrderBy(m => m.Position).ThenBy(m => m.sNode).ToList(); }
                catch { }
                found &= modelsFound.Any();
              }
              nodesFound = DataStorageCachedLocal.Nodes.Where(m => rNode == m.Folder).OrderBy(m => m.Position).ThenBy(m => m.sNode).ToList();
              found &= nodesFound.Any();
              if (found)
                break;
            }
            return;
          }
          //
          List<int> FoldersToScan = rNodesActive.Select(r => r.rNode).Distinct().Except(DataStorageCachedLocal.Nodes.Select(m => m.Folder)).ToList();
          if (FoldersToScan.Any())
          {
            Expression<Func<IKGD_VNODE, bool>> vNodeFilterAll = PredicateBuilder.True<IKGD_VNODE>();
            if (FilterVNODE != null)
              vNodeFilterAll = vNodeFilterAll.And(FilterVNODE);
            vNodeFilterAll = vNodeFilterAll.And(n => FoldersToScan.Contains(n.folder));
            var fsNodesNew = fsOp.Get_NodesInfoFilteredExt2(vNodeFilterAll, FilterVDATA, FS_Operations.FiltersVFS_Default | FS_Operations.FilterVFS.Dates).ToList();
            //
            if (fsNodesNew.Any())
            {
              nodesFound = fsNodesNew.Select(n => new DataStorage.DataStorageNodeInfo(n)).ToList();
              DataStorageCachedLocal.Nodes.AddRange(nodesFound.Except(DataStorageCachedLocal.Nodes));
              DataStorageCachedLocal.rNodesFoldersVoid.AddRange(FoldersToScan.Except(nodesFound.Select(m => m.Folder).Distinct()));
              //
              if (BuildModels)
              {
                modelsFound = new List<IKCMS_ModelCMS_Interface>();
                bool savedStatus = IKCMS_ModelCMS_Provider.Provider.managerVFS.Enabled;
                try
                {
                  IKCMS_ModelCMS_Provider.Provider.managerVFS.Enabled = true;
                  IKCMS_ModelCMS_Provider.Provider.managerVFS.RegisterNodes(fsNodesNew);
                  foreach (var fsNode in fsNodesNew)
                  {
                    IKCMS_ModelCMS_Interface model = null;
                    try
                    {
                      IKCMS_ModelCMS_ModelInfo_Interface itemModelInfo = IKCMS_ModelCMS_Provider.Provider.FindBestModelMatch(Utility.FindTypeCached(fsNode.vData.manager_type));
                      model = IKCMS_ModelCMS_Provider.Provider.ModelBuild(null, fsNode, itemModelInfo);
                      if (model != null)
                        modelsFound.Add(model);
                    }
                    catch { }
                  }
                  DataStorageCachedLocal.Models.AddRange(modelsFound.Except(DataStorageCachedLocal.Models));
                }
                catch { }
                finally
                {
                  IKCMS_ModelCMS_Provider.Provider.managerVFS.Enabled = savedStatus;
                }
              }
            }
          }
          // ripetizione dello scan su firstNode dopo aver aggiornato le tabelle interne con lo scan dei fragments mancanti dai path
          firstNode = rNodesActive.FirstOrDefault(r => DataStorageCachedLocal.Nodes.Any(m => m.Folder == r.rNode));
          if (firstNode != null)
          {
            var rNodes = rNodesActive.Where(r => r.index == firstNode.index).Select(r => r.rNode).Distinct().ToList();
            foreach (var rNode in rNodes)
            {
              bool found = true;
              if (BuildModels)
              {
                try { modelsFound = DataStorageCachedLocal.Models.Where(m => rNode == m.vfsNode.Folder).OrderBy(m => m.Position).ThenBy(m => m.sNode).ToList(); }
                catch { }
                found &= modelsFound.Any();
              }
              nodesFound = DataStorageCachedLocal.Nodes.Where(m => rNode == m.Folder).OrderBy(m => m.Position).ThenBy(m => m.sNode).ToList();
              found &= nodesFound.Any();
              if (found)
                break;
            }
          }
        }
        catch { }
      }
      if (nodesFound != null && !nodesFound.Any())
        nodesFound = null;
      if (modelsFound != null && !modelsFound.Any())
        modelsFound = null;
    }


    //
    // fetch della lista completa dei parents per tutti i livelli
    // senza lo storage per il level corrente
    // attenzione che il level corrente ha i dati filtrati con le ACL attive mentre i dati dei level precedenti devono ancora essere filtrati
    //
    public virtual void FindWorkerAllParentsNoACL(IKCMS_ModelCMS_Interface modelExternal, out List<IKCMS_ModelCMS_Interface> modelsFound, out List<DataStorage.DataStorageNodeInfo> nodesFound)
    {
      modelsFound = null;
      nodesFound = null;
      lock (_lock)
      {
        try
        {
          //
          DataStorage DataStorageCachedLocal = DataStorageCached;
          //
          EnsureCompiledFilters();
          //modelsFound = modelExternal.Models.Where(m => CompiledFilterVNODE(m.vfsNode.vNode) && CompiledFilterVDATA_NOSQL(m.vfsNode.vData)).ToList();
          modelsFound = modelExternal.Models.Where(m => CompiledFilterVNODE(m.vfsNode.vNode) && CompiledFilterVDATA_NOSQL(m.vfsNode.vData)).Where(m => !m.IsExpired).ToList();
          nodesFound = modelsFound.Select(m => new DataStorage.DataStorageNodeInfo(m)).ToList();
          if (nodesFound.Any())
          {
            if (StoreLeafsFromModelExternal)
            {
              if (BuildModels)
                DataStorageCachedLocal.Models.AddRange(modelsFound.Except(DataStorageCachedLocal.Models));
              DataStorageCachedLocal.Nodes.AddRange(nodesFound.Except(DataStorageCachedLocal.Nodes));
            }
          }
          else
          {
            if (StoreLeafsFromModelExternal)
            {
              DataStorageCachedLocal.rNodesFoldersVoid.Add(modelExternal.rNode);
            }
          }
          //
          List<IKGD_Path> paths = modelExternal.PathsVFS;
          //
          // rNodes organizzati per livello di backscan
          var rNodesAtLevel = paths.SelectMany(p => p.Fragments.Where(f => f.flag_folder && f.rNode != modelExternal.rNode).Reverse().Select((f, i) => new { index = i, rNode = f.rNode })).Distinct().OrderBy(p => p.index).ThenBy(p => p.rNode).ToList();
          // rNodes attivi (con models o con contenuti non ancora verificati)
          var rNodesActive = rNodesAtLevel.Where(r => !DataStorageCachedLocal.rNodesFoldersVoid.Contains(r.rNode)).ToList();
          //
          List<int> FoldersToScan = rNodesActive.Select(r => r.rNode).Distinct().Except(DataStorageCachedLocal.Nodes.Select(m => m.Folder)).ToList();
          if (FoldersToScan.Any())
          {
            Expression<Func<IKGD_VNODE, bool>> vNodeFilterAll = PredicateBuilder.True<IKGD_VNODE>();
            if (FilterVNODE != null)
              vNodeFilterAll = vNodeFilterAll.And(FilterVNODE);
            vNodeFilterAll = vNodeFilterAll.And(n => FoldersToScan.Contains(n.folder));
            // si include il filtro per le date
            var fsNodesNew = fsOp.Get_NodesInfoFilteredExt2(vNodeFilterAll, FilterVDATA, FS_Operations.FilterVFS.NoACL | FS_Operations.FilterVFS.NoLanguage | FS_Operations.FilterVFS.Dates).ToList();
            //
            if (fsNodesNew.Any())
            {
              var nodesFoundNew = fsNodesNew.Select(n => new DataStorage.DataStorageNodeInfo(n)).ToList();
              DataStorageCachedLocal.Nodes.AddRange(nodesFoundNew.Except(DataStorageCachedLocal.Nodes));
              DataStorageCachedLocal.rNodesFoldersVoid.AddRange(FoldersToScan.Except(nodesFoundNew.Select(m => m.Folder).Distinct()));
              //
              if (BuildModels)
              {
                var modelsFoundNew = new List<IKCMS_ModelCMS_Interface>();
                bool savedStatus = IKCMS_ModelCMS_Provider.Provider.managerVFS.Enabled;
                try
                {
                  IKCMS_ModelCMS_Provider.Provider.managerVFS.Enabled = true;
                  IKCMS_ModelCMS_Provider.Provider.managerVFS.RegisterNodes(fsNodesNew);
                  foreach (var fsNode in fsNodesNew)
                  {
                    IKCMS_ModelCMS_Interface model = null;
                    try
                    {
                      IKCMS_ModelCMS_ModelInfo_Interface itemModelInfo = IKCMS_ModelCMS_Provider.Provider.FindBestModelMatch(Utility.FindTypeCached(fsNode.vData.manager_type));
                      model = IKCMS_ModelCMS_Provider.Provider.ModelBuild(null, fsNode, itemModelInfo);
                      if (model != null)
                        modelsFoundNew.Add(model);
                    }
                    catch { }
                  }
                  DataStorageCachedLocal.Models.AddRange(modelsFoundNew.Except(DataStorageCachedLocal.Models));
                }
                catch { }
                finally
                {
                  IKCMS_ModelCMS_Provider.Provider.managerVFS.Enabled = savedStatus;
                }
              }
            }
          }
          var rNodes = rNodesActive.Select(r => r.rNode).Distinct().ToList();
          if (BuildModels)
          {
            try { modelsFound.AddRange((DataStorageCachedLocal.Models.Where(m => rNodes.Contains(m.vfsNode.Folder)).Except(modelsFound)).OrderBy(m => rNodesAtLevel.FirstOrDefault(r => r.rNode == m.rNode).Return(r => r.index, -1)).ThenBy(m => m.Position).ThenBy(m => m.sNode)); }
            catch { }
          }
          nodesFound.AddRange(DataStorageCachedLocal.Nodes.Where(n => rNodes.Contains(n.Folder)).Except(nodesFound).OrderBy(n => rNodesAtLevel.FirstOrDefault(r => r.rNode == n.rNode).Return(r => r.index, -1)).ThenBy(n => n.Position).ThenBy(n => n.sNode));
        }
        catch { }
      }
      if (nodesFound != null && !nodesFound.Any())
        nodesFound = null;
      if (modelsFound != null && !modelsFound.Any())
        modelsFound = null;
    }


    public class DataStorage : IKGD_CachingHelper_CacheItemHasRemovedCallback_Interface
    {
      public List<IKCMS_ModelCMS_Interface> Models { get; protected set; }
      public List<DataStorageNodeInfo> Nodes { get; protected set; }
      public List<int> rNodesFoldersVoid { get; protected set; }


      public DataStorage()
      {
        rNodesFoldersVoid = new List<int>();
        Nodes = new List<DataStorageNodeInfo>();
        Models = new List<IKCMS_ModelCMS_Interface>();
      }


      public class DataStorageNodeInfo
      {
        public int sNode { get; set; }
        public int rNode { get; set; }
        public int Folder { get; set; }
        public double Position { get; set; }

        public DataStorageNodeInfo() { }
        public DataStorageNodeInfo(FS_Operations.FS_NodeInfo_Interface fsNode) { sNode = fsNode.sNode; rNode = fsNode.rNode; Folder = fsNode.Folder; Position = fsNode.Position; }
        public DataStorageNodeInfo(IKCMS_ModelCMS_Interface model) : this(model.vfsNode) { }
      }


      public CacheItemRemovedCallback CachingHelper_onRemoveCallback
      {
        get
        {
          return (key, value, reason) =>
          {
            try
            {
              (value as DataStorage).rNodesFoldersVoid.ClearCachingFriendly();
              (value as DataStorage).Nodes.ClearCachingFriendly();
              (value as DataStorage).Models.ClearCachingFriendly(m => m.Clear());
            }
            catch { }
          };
        }
      }

    }


  }  //class IKCMS_ModelScannerParentBase


}
