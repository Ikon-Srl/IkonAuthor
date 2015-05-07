using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Text.RegularExpressions;
using System.Web;
using System.Xml.Linq;
using System.Web.Mvc;
using System.Web.Mvc.Html;
using System.Reflection;
using System.Diagnostics;
using System.Data.Linq.SqlClient;
using System.ComponentModel;
using LinqKit;
using Autofac;

using Ikon;
using Ikon.GD;
using Ikon.IKCMS.Library.Resources;
using Ikon.IKGD.Library.Collectors;
using Ikon.IKCMS.Pagers;
using Ikon.Indexer;
using Ikon.IKGD.Library;
using Ikon.IKGD.Library.Resources;
using System.Collections;


namespace Ikon.IKCMS
{

  //
  // creare un covered index per IKGD_PROPERTY con i seguenti fields
  // rnode,flag_*,name,attributeId
  // saltare quindi (version, value, data e tutto il resto)
  // creare anche un cluster index?
  // modificare la PK rnode,version?
  //
  //



  public abstract class ManagerTagFilterBase : ManagerTagFilterBase_Interface
  {
    public enum FetchModeEnum { rNodeFetch, sNodeFetch }
    //
    public List<int> TagsAll { get { return DataStorageCached.TagsFilter; } }
    public bool hasFilterSet { get; set; }
    //
    public PagerSimple<int> Pager { get; protected set; }
    //
    public abstract FetchModeEnum FetchMode { get; }
    public abstract bool? FilteredResourcesAreFolders { get; }
    //
    public virtual bool? UseReadOnlyFsOp { get; set; }
    public virtual FS_Operations fsOp { get { return UseReadOnlyFsOp.GetValueOrDefault(true) ? IKCMS_ManagerIoC.requestContainer.ResolveNamed<FS_Operations>("readonly") : IKCMS_ManagerIoC.requestContainer.Resolve<FS_Operations>(); } }
    //public virtual FS_Operations fsOp { get { return IKCMS_ManagerIoC.requestContainer.Resolve<FS_Operations>(); } }
    //
    public virtual IEnumerable<string> CachingTableDependencies { get { return FS_OperationsHelpers.Const_CacheDependencyIKGD_vNode_vData_Relation_Property_Tags; } }
    //
    // overridable properties to configure derived classes
    public virtual bool? AllowEmptyFilterAndArchiveSet { get; set; }
    public virtual bool? UseModelFolderAsArchive { get; set; }
    public virtual bool? GetArchiveFolderFromRelations { get; set; }
    public virtual bool? AllowArchiveSelection { get; set; }
    public virtual bool? UseGenericModelBuild { get; set; }
    public virtual bool? RefineActiveTagsAfterLuceneFiltering { get; set; }
    public virtual bool? IgnoreExpiryDates { get; set; }
    public virtual bool? AllowTagsFromModel { get; set; }
    public virtual bool? SelectTagsWithVarNameOnly { get; set; }  //default=true permette di filtare solo le properties con name=IKGD_Constants.IKCAT_TagPropertyName
    public virtual bool? MixedAndOrMode { get; set; }  //default=true
    public virtual bool? LikeIsUnordered { get; set; }  //default=true spezza la stringa di ricerca in tokens e genera una condizione di and tra tutti i frammenti senza vincoli di ordine
    public virtual bool? FilterNodesByValidPath { get; set; }
    public virtual bool? AutoSetModelBaseForContext { get; set; }
    public virtual bool? AutoFindCurrentPageFromModel { get; set; }
    public virtual bool? PagerAllowOverrideBaseUrl { get; set; }
    public virtual bool? SearchForLinkedArchivesInConstructor { get; set; }
    public virtual bool? AutoFindCurrentBaseModelCached { get; set; }
    public virtual bool FilterNodesByValidPathRequired { get; set; }
    public virtual int? MaxScanRecursionLevel { get; set; }
    public virtual bool? FilterKvtNullValuesAllowed { get; set; }
    //
    public List<string> AllowedTypeNames { get; set; }
    public List<string> AllowedCategories { get; set; }
    //
    public virtual Expression<Func<IKGD_VNODE, bool>> FilterVNODE { get; set; }  // filtro per i nodi selezionabili
    public virtual Expression<Func<IKGD_VDATA, bool>> FilterVDATA { get; set; }  // filtro per i nodi selezionabili
    public virtual Expression<Func<IKGD_VDATA, bool>> TreeScanFilterVDATA { get; set; }  // filtro per escludere contenuti dallo scan ricorsivo del tree per filtare quelli che saranno i folder da filtrare con i tags
    public virtual Expression<Func<IKGD_VNODE, bool>> ExtraFilterVNODE { get; set; }  // filtro extra per i nodi selezionabili
    public virtual Expression<Func<IKGD_VDATA, bool>> ExtraFilterVDATA { get; set; }  // filtro extra per i nodi selezionabili
    //
    protected NameValueCollection _ArgsSet;  // per renderlo modificabile
    public virtual NameValueCollection ArgsSet { get { return _ArgsSet ?? HttpContext.Current.Request.Params; } set { _ArgsSet = value; } }  // la sorgente per tutti i parametri dei filtri/sort
    public virtual void ArgSetDup() { _ArgsSet = new NameValueCollection(ArgsSet); }
    public virtual void ArgSetClear() { _ArgsSet = new NameValueCollection(); }
    //
    public virtual string ParameterNameFilter { get { return "Filter"; } }
    public virtual string ParameterNameFilterArr { get { return "Filter[]"; } }
    public virtual string ParameterNameLucene { get { return "Lucene"; } }
    public virtual string ParameterNameFilterValueBase { get { return "FilterValue_"; } }
    public virtual string ParameterNameFilterLikeBase { get { return "FilterLike_"; } }
    public virtual string ParameterNameSorter { get { return "SortMode"; } }
    public virtual string ParameterNamePager { get { return "PagerStartIndex"; } }
    public virtual string ParameterNameDateMin { get { return "FilterDateMin"; } }
    public virtual string ParameterNameDateMax { get { return "FilterDateMax"; } }
    public virtual string ParameterNameDateExt { get { return "FilterDateExt"; } }
    public virtual string ParameterNameDateExpiryMin { get { return "FilterDateExpMin"; } }
    public virtual string ParameterNameDateExpiryMax { get { return "FilterDateExpMax"; } }
    public virtual string ParameterNameFolders { get { return "FilterFolders"; } }
    public virtual string ParameterNameFoldersTree { get { return "FilterFoldersTree"; } }
    public virtual string ParameterNameFilterKVTsBase { get { return "FilterKvt"; } }
    public virtual string ParameterNameFilterKVTsRangeBase { get { return "FilterKvtRange"; } }
    //
    public virtual string ParameterNameFilterKVTsString { get { return ParameterNameFilterKVTsBase + "S_"; } }
    public virtual string ParameterNameFilterKVTsLike { get { return ParameterNameFilterKVTsBase + "L_"; } }
    public virtual string ParameterNameFilterKVTsInt { get { return ParameterNameFilterKVTsBase + "I_"; } }
    public virtual string ParameterNameFilterKVTsFloat { get { return ParameterNameFilterKVTsBase + "F_"; } }
    public virtual string ParameterNameFilterKVTsDate { get { return ParameterNameFilterKVTsBase + "D_"; } }
    public virtual Regex ParameterNameFilterKVTsRangeRx { get { return new Regex(@"^FilterKvtRange(?<type>.+)(?<pos>[1,2])_(?<key>.+)$", RegexOptions.Singleline); } }
    //
    public virtual string ParameterNameDateActive { get { return "ActiveDate"; } }
    public virtual string ParameterNameViewMode { get { return "ViewTemplateCode"; } }
    //
    // prefix utilizzato per la negazione del parametro in modo da poter cancellare Tags provenienti da altre sorgenti (es Tags del Model)
    public virtual string ParameterNegationPrefix { get { return "-"; } }
    //
    public virtual int DefaultCachingTimeMain { get; set; }
    public virtual int DefaultCachingTimeData { get; set; }
    public virtual int DefaultCachingTimeLucene { get; set; }
    //
    private string _CurrentMode;
    public string GetCurrentMode { get { return _CurrentMode ?? ArgsSet[ParameterNameViewMode]; } set { _CurrentMode = value; } }
    //


    public ManagerTagFilterBase()
    {
      hasFilterSet = false;
      FilterNodesByValidPathRequired = true;
      DefaultCachingTimeMain = Utility.TryParse<int>(IKGD_Config.AppSettings["CachingIKCMS_Filters"] ?? IKGD_Config.AppSettings["CachingIKCMS_Models"], 3600);
      DefaultCachingTimeData = DefaultCachingTimeMain;
      DefaultCachingTimeLucene = Utility.TryParse<int>(IKGD_Config.AppSettings["CachingIKCMS_Lucene"], DefaultCachingTimeMain / 5);
    }


    public ManagerTagFilterBase(IKCMS_ModelCMS_Interface model)
      : this()
    {
      Model = model;
    }


    public static string RequestStorageVarName { get { return "ManagerTagFilterBaseRequestStorage_"; } }
    public static T Factory<T>(ViewDataDictionary viewData) where T : ManagerTagFilterBase { return Factory<T>(viewData, RequestStorageVarName + typeof(T).Name); }
    public static T Factory<T>(ViewDataDictionary viewData, string storageVarName) where T : ManagerTagFilterBase
    {
      T manager = null;
      if (storageVarName.IsNotNullOrWhiteSpace())
      {
        try
        {
          if (viewData[storageVarName] != null && viewData[storageVarName] is T)
          {
            manager = viewData[storageVarName] as T;
          }
          if (manager == null)
          {
            manager = Activator.CreateInstance(typeof(T), viewData.Model as IKCMS_ModelCMS_Interface) as T;
            if (manager != null)
            {
              viewData[storageVarName] = manager;
            }
          }
        }
        catch { }
      }
      return manager;
    }



    public virtual void ClearAllCachedObjetForType(string baseKeyForced)
    {
      try
      {
        baseKeyForced = baseKeyForced ?? ("_" + this.GetType().Name + "_");
        HttpRuntime.Cache.OfType<DictionaryEntry>().Select(c => c.Key as string).Where(k => k != null && (baseKeyForced == null || k.Contains(baseKeyForced))).ToArray().ForEach(k => HttpRuntime.Cache.Remove(k));
        //HttpRuntime.Cache.OfType<DictionaryEntry>().Select(c => c.Key as string).Where(k => k != null && (baseKeyForced == null || k.StartsWith(baseKeyForced))).ToArray().ForEach(k => HttpRuntime.Cache.Remove(k));
      }
      catch { }
    }


    public virtual void ClearCache()
    {
      if (_CachingKey.IsNotEmpty())
      {
        HttpRuntime.Cache.Remove(CachingKey);
        _CachingKey = null;
      }
    }


    public virtual void Clear()
    {
      ClearCache();
      //
      //_ArgsSet = null;
      _fsNodes = null;
      _fsNodesExt = null;
      _TagsExternal = null;
      _TagsValuedExternal = null;
      _KVTsExternal = null;
      if (FiltersCustom != null)
        FiltersCustom.Clear();
      _DataStorageCached = null;
      //
      // TODO: ci sarebbe da pulire anche la cache di lucene
      //
    }


    protected List<KeyValuePair<int, int>> NodePairs { get; set; }  // solo per uso interno (integrazione con Lucene)
    public IEnumerable<int> NodesPre { get { return (Pager != null) ? Nodes.Take(Pager.PagerStartIndex.GetValueOrDefault(0)) : Enumerable.Empty<int>(); } }
    public IEnumerable<int> NodesPost { get { return (Pager != null) ? Nodes.Skip(Pager.PagerStartIndex.GetValueOrDefault(0) + Pager.PagerPageSize.Value) : Enumerable.Empty<int>(); } }
    public IEnumerable<int> NodesPage { get { return (Pager != null) ? Nodes.Skip(Pager.PagerStartIndex.GetValueOrDefault(0)).Take(Pager.PagerPageSize.Value) : Nodes; } }
    public List<int> Nodes
    {
      get
      {
        if (!DataStorageCached.Processed)
          ScanVFS();
        return (FetchMode == FetchModeEnum.sNodeFetch) ? DataStorageCached.sNodes : DataStorageCached.rNodes;
      }
    }


    public List<int> TagsActiveSet
    {
      get
      {
        if (!DataStorageCached.Processed)
          ScanVFS();
        return DataStorageCached.Tags ?? (DataStorageCached.Tags = GetTagsActiveSetWithPostFilter());
        //return DataStorageCached.Tags ?? (DataStorageCached.Tags = Query_TagsActiveSetV1.ToList());
        //return DataStorageCached.Tags ?? (DataStorageCached.Tags = Query_TagsActiveSetV2.ToList());
      }
    }


    public virtual IEnumerable<IKCAT_Attribute> AttributesActive { get { return IKCAT_AttributeStorage.GetTags(TagsActiveSet); } }


    protected List<int> GetTagsActiveSetWithPostFilter()
    {
      if (!DataStorageCached.Processed)
        ScanVFS();
      if (RefineActiveTagsAfterLuceneFiltering != false && DataStorageCached.FilteredByLucene == true)
      {
        if (FetchMode == FetchModeEnum.sNodeFetch)
          NodePairs = NodePairs ?? Query_rNodesFilteredNOvData.Join(Query_vNodesVFS, r => r, vn => vn.rnode, (r, vn) => vn).OrderBy(vn => vn.name).ThenBy(vn => vn.folder).ThenBy(vn => vn.position).ThenBy(vn => vn.rnode).ThenBy(vn => vn.snode).Select(g => new KeyValuePair<int, int>(g.rnode, g.snode)).ToList();
        return Query_TagsActiveSetWithRNODES.ToList().Join(DataStorageCached.rNodes ?? NodePairs.Select(r => r.Key), a => a.Key, r => r, (a, r) => a.Value).Distinct().ToList();
      }
      //
      return Query_TagsActiveSetV2 != null ? Query_TagsActiveSetV2.ToList() : new List<int>();
      //return (Query_TagsActiveSetV1 != null)? Query_TagsActiveSetV1.ToList(): null;
    }


    protected List<int> _TagsFromModel;
    public List<int> TagsFromModel
    {
      get
      {
        if (_TagsFromModel == null)
        {
          if (Model != null && AllowTagsFromModel.GetValueOrDefault(true))
          {
            try { _TagsFromModel = Model.TagsIds.Distinct().ToList(); }
            catch { }
            //try { _TagsFromModel = Model.Properties.Where(p => p.name == IKGD_Constants.IKCAT_TagPropertyName && p.attributeId != null).Select(p => p.attributeId.Value).Distinct().ToList(); }
            //catch { }
          }
          if (_TagsFromModel == null)
          {
            _TagsFromModel = new List<int>();
          }
        }
        return _TagsFromModel;
      }
      protected set { _TagsFromModel = value; }
    }


    //
    public IKCMS_ModelCMS_Interface ModelInput { get; protected set; }
    //
    protected IKCMS_ModelCMS_Interface _Model;
    public IKCMS_ModelCMS_Interface Model
    {
      get { return _Model; }
      protected set
      {
        ModelInput = value;
        _Model = GetManagerModel(value);
        //
        // modificato TagsFromModel per effettuare un late binding dopo l'assegnazione del model
        //TagsFromModel = new List<int>();
        //if (_Model != null && AllowTagsFromModel.GetValueOrDefault(true))
        //{
        //  try { TagsFromModel = _Model.TagsIds.Distinct().ToList(); }
        //  catch { }
        //  //try { TagsFromModel = _Model.Properties.Where(p => p.name == IKGD_Constants.IKCAT_TagPropertyName && p.attributeId != null).Select(p => p.attributeId.Value).Distinct().ToList(); }
        //  //catch { }
        //}
      }
    }


    //
    // given a model finds the reference model to use as manger for the search process
    // to be overridden in derived class calling GetManagerModelWorker with a custom filter
    //
    public virtual IKCMS_ModelCMS_Interface GetManagerModel(IKCMS_ModelCMS_Interface model)
    {
      // esempio di chiamata con filtro per le customizzazioni
      //var mdl = GetManagerModelWorker(model, false, (f) => f.ManagerType == typeof(IKCMS_ResourceType_PageCMS).Name);
      var mdl = GetManagerModelWorker(model, SearchForLinkedArchivesInConstructor.GetValueOrDefault(true), null);
      if (AutoSetModelBaseForContext.GetValueOrDefault(false) && mdl != null)
      {
        IKCMS_ModelCMS_Provider.Provider.ModelBaseForContext = mdl;
      }
      return mdl;
    }


    public IKCMS_ModelCMS_Interface GetManagerModelWorker(IKCMS_ModelCMS_Interface model, bool? searchForLinkedArchives, Func<IKGD_Path_Fragment, bool> filter)
    {
      IKCMS_ModelCMS_Interface mdl = model;
      try
      {
        //
        // gestione di model associato a teaser tag/eventi
        if (model is IKCMS_ModelCMS_TeaserNewsEventi_Interface)
        {
          var rNodesArchives1 = model.RelationsOrdered.Where(r => r.type == IKGD_Constants.IKGD_ArchiveRelationName).Select(r => r.rnode_dst).Distinct().ToList();
          if (rNodesArchives1 != null && rNodesArchives1.Any() && rNodesArchives1.Count == 1)
          {
            //
            Func<IKCMS_ModelCMS_Interface> finder1 = () =>
            {
              var paths = fsOp.PathsFromNodesExt(null, rNodesArchives1, true, false, true);
              if (filter != null && paths.Count > 0)
              {
                try { paths = paths.Where(p => p.Fragments.Any(filter)).ToList(); }
                catch { }
              }
              var path = paths.FirstOrDefault();
              if (path != null)
              {
                if (IKCMS_RegisteredTypes.Types_IKCMS_Page_Interface.Any(t => t.Name == path.LastFragment.ManagerType))
                {
                  return IKCMS_ModelCMS_Provider.Provider.ModelBuildGeneric(path.LastFragment.sNode);
                }
              }
              return null;
            };
            //
            // se il model passato e' un TeaserNewsEventi verifichiamo che la relation non punti ad una pagina
            // piuttosto che un archivio, in tal caso usa quella pagina come model di riferimento
            if (AutoFindCurrentBaseModelCached.GetValueOrDefault(true))
            {
              string cachingKey = FS_OperationsHelpers.ContextHashNN(this.GetType().Name, "GetManagerModel1", rNodesArchives1, (filter != null ? (object)filter.GetHashCode() : null));
              mdl = FS_OperationsHelpers.CachedEntityWrapper(cachingKey, finder1, null, DefaultCachingTimeMain, DefaultCachingTimeMain / 2, FS_OperationsHelpers.Const_CacheDependencyIKGD_vNode_vData_iNode_Relation_Property) ?? model;
            }
            else
            {
              mdl = finder1() ?? model;
            }
            //
            try { TagsExternal = model.TagsIds.Except(mdl.TagsIds).Distinct().ToList(); }
            catch { }
            //
          }
          return mdl ?? model;
        }
        //
        // il model e' uno degli elementi da ricercare (si deve quindi trovare il manager associato all'item)
        if (model != null && (AllowedTypeNames == null || AllowedTypeNames.Contains(model.ManagerType)) && (AllowedCategories == null || AllowedCategories.Contains(model.Category)))
        {
          mdl = null;
          if (model != model.ModelRootOrContext && model.ModelRootOrContext != null)
          {
            mdl = model.ModelRootOrContext;
          }
          else if (filter != null)
          {
            var frag = model.PathVFS.Fragments.LastOrDefault(filter);
            if (frag != null)
            {
              mdl = IKCMS_ModelCMS_Provider.Provider.ModelBuildGeneric(frag.sNode);
            }
          }
          if (mdl == null && searchForLinkedArchives.GetValueOrDefault(false))
          {
            //
            Func<IKCMS_ModelCMS_Interface> finder2 = () =>
            {
              //vecchia modalita' senza il supporto per i filtri
              //return IKCMS_ModelCMS_ArchiveBrowserHelper.EnsureParentModel_Worker(fsOp, model.vfsNode, model, null, false, true);
              return IKCMS_ModelCMS_ArchiveBrowserHelper.EnsureParentModel_WorkerWithFilter(fsOp, model.vfsNode, model, null, false, true, filter);
            };
            //
            if (AutoFindCurrentBaseModelCached.GetValueOrDefault(true))
            {
              var rNodesArchives2 = Utility.Implode(model.PathsVFS.SelectMany(p => p.Fragments.Where(f => f.ManagerType == typeof(IKCMS_FolderType_ArchiveRoot).Name).Select(f => f.rNode)).Distinct().OrderBy(r => r), ",").NullIfEmpty() ?? model.rNode.ToString();
              string cachingKey = FS_OperationsHelpers.ContextHashNN(this.GetType().Name, "GetManagerModel2", rNodesArchives2, (filter != null ? (object)filter.GetHashCode() : null));
              mdl = FS_OperationsHelpers.CachedEntityWrapper(cachingKey, finder2, null, DefaultCachingTimeMain, DefaultCachingTimeMain / 2, FS_OperationsHelpers.Const_CacheDependencyIKGD_vNode_vData_iNode_Relation_Property);
            }
            else
            {
              mdl = finder2();
            }
            //
          }
        }
        if (mdl == null)
          mdl = model;
        if (mdl is IKCMS_ModelCMS_Page_Interface)
        {
          return mdl;
        }
      }
      catch { }
      return mdl ?? model;
    }


    protected List<int> _TagsExternal;
    public virtual List<int> TagsExternal
    {
      get
      {
        if (_TagsExternal == null)
        {
          //try { _TagsExternal = ArgsSet.GetValues(ParameterNameFilter).SelectMany(s => Utility.ExplodeT<int>(s, " ,|", " ", true)).Distinct().ToList(); }
          //catch { _TagsExternal = new List<int>(); }
          try { _TagsExternal = (ArgsSet.GetValues(ParameterNameFilter) ?? new string[0]).Concat(ArgsSet.GetValues(ParameterNameFilterArr) ?? new string[0]).SelectMany(s => Utility.ExplodeT<int>(s, " ,|", " ", true)).Distinct().ToList(); }
          catch { _TagsExternal = new List<int>(); }
        }
        return _TagsExternal;
      }
      set { _TagsExternal = value; }
    }


    protected List<KeyValuePair<string, string>> _TagsValuedExternal;
    public virtual List<KeyValuePair<string, string>> TagsValuedExternal
    {
      get
      {
        if (_TagsValuedExternal == null)
        {
          try { _TagsValuedExternal = ArgsSet.AllKeys.Where(k => k.StartsWith(ParameterNameFilterValueBase) || k.StartsWith(ParameterNameFilterLikeBase)).SelectMany(k => ArgsSet.GetValues(k).Select(v => new KeyValuePair<string, string>(k, v))).Distinct().ToList(); }
          catch { _TagsValuedExternal = new List<KeyValuePair<string, string>>(); }
        }
        return _TagsValuedExternal;
      }
      set { _TagsValuedExternal = value; }
    }


    protected List<KeyValuePair<string, string>> _KVTsExternal;
    public virtual List<KeyValuePair<string, string>> KVTsExternal
    {
      get
      {
        if (_KVTsExternal == null)
        {
          try { _KVTsExternal = ArgsSet.AllKeys.Where(k => k.StartsWith(ParameterNameFilterKVTsBase)).SelectMany(k => ArgsSet.GetValues(k).Select(v => new KeyValuePair<string, string>(k, v))).Distinct().ToList(); }
          catch { _KVTsExternal = new List<KeyValuePair<string, string>>(); }
        }
        return _KVTsExternal;
      }
      set { _KVTsExternal = value; }
    }


    public virtual bool? IgnoreFiltersCustom { get; set; }
    public virtual List<KeyValuePair<string, string>> FiltersCustom { get; set; }


    // attenzione ritorna tutto il set comprese le copie multiple dei symlinks
    public IQueryable<FS_Operations.FS_NodeInfo> fsNodesAll(IEnumerable<int> TagsExtra)
    {
      if (Query_vDatasFiltered == null)
        ScanVFS_Worker(TagsExtra);
      var results =
        (from vData in Query_vDatasFiltered
         from vNode in Query_vNodesVFS.Where(n => n.rnode == vData.rnode)
         from iNode in fsOp.NodesActive<IKGD_INODE>().Where(n => n.rnode == vData.rnode).DefaultIfEmpty()
         select new FS_Operations.FS_NodeInfo { vNode = vNode, vData = vData, iNode = iNode });
      return results;
    }


    // attenzione ritorna tutto il set comprese le copie multiple dei symlinks
    public IQueryable<FS_Operations.FS_NodeInfoExt> fsNodesExtAll(IEnumerable<int> TagsExtra)
    {
      if (Query_vDatasFiltered == null)
        ScanVFS_Worker(TagsExtra);
      var results =
        (from vData in Query_vDatasFiltered
         from vNode in Query_vNodesVFS.Where(n => n.rnode == vData.rnode)
         from iNode in fsOp.NodesActive<IKGD_INODE>().Where(n => n.rnode == vData.rnode).DefaultIfEmpty()
         join rel in fsOp.NodesActive<IKGD_RELATION>() on vData.rnode equals rel.rnode into rels
         join prp in fsOp.NodesActive<IKGD_PROPERTY>() on vData.rnode equals prp.rnode into prps
         select new FS_Operations.FS_NodeInfoExt { vNode = vNode, vData = vData, iNode = iNode, Relations = rels.ToList(), Properties = prps.ToList() });
      return results;
    }


    public List<FS_Operations.FS_NodeInfo> _fsNodes;
    public IEnumerable<FS_Operations.FS_NodeInfo> fsNodes
    {
      get
      {
        if (_fsNodes == null)
        {
          var codes = NodesPage.ToList();
          if (FetchMode == FetchModeEnum.sNodeFetch)
          {
            IKCMS_ModelCMS_Provider.Provider.managerVFS.EnsureNodes<FS_Operations.FS_NodeInfo>(codes);
            _fsNodes = IKCMS_ModelCMS_Provider.Provider.managerVFS.NodesVFS.Where(n => codes.Contains(n.sNode)).OrderBy(n => codes.IndexOf(n.sNode)).OfType<FS_Operations.FS_NodeInfo>().ToList();
          }
          else
          {
            IKCMS_ModelCMS_Provider.Provider.managerVFS.EnsureNodesRNODE<FS_Operations.FS_NodeInfo>(codes, false);
            _fsNodes = IKCMS_ModelCMS_Provider.Provider.managerVFS.NodesVFS.Where(n => codes.Contains(n.rNode)).Distinct((n1, n2) => n1.rNode == n2.rNode).OrderBy(n => codes.IndexOf(n.rNode)).OfType<FS_Operations.FS_NodeInfo>().ToList();
          }
        }
        return _fsNodes;
      }
    }


    public List<FS_Operations.FS_NodeInfoExt> _fsNodesExt;
    public IEnumerable<FS_Operations.FS_NodeInfoExt> fsNodesExt
    {
      get
      {
        if (_fsNodesExt == null)
        {
          var codes = NodesPage.ToList();
          if (FetchMode == FetchModeEnum.sNodeFetch)
          {
            IKCMS_ModelCMS_Provider.Provider.managerVFS.EnsureNodes<FS_Operations.FS_NodeInfoExt>(codes);
            _fsNodesExt = IKCMS_ModelCMS_Provider.Provider.managerVFS.NodesVFS.Where(n => codes.Contains(n.sNode)).OrderBy(n => codes.IndexOf(n.sNode)).OfType<FS_Operations.FS_NodeInfoExt>().ToList();
          }
          else
          {
            IKCMS_ModelCMS_Provider.Provider.managerVFS.EnsureNodesRNODE<FS_Operations.FS_NodeInfoExt>(codes, false);
            _fsNodesExt = IKCMS_ModelCMS_Provider.Provider.managerVFS.NodesVFS.Where(n => codes.Contains(n.rNode)).Distinct((n1, n2) => n1.rNode == n2.rNode).OrderBy(n => codes.IndexOf(n.rNode)).OfType<FS_Operations.FS_NodeInfoExt>().ToList();
          }
        }
        return _fsNodesExt;
      }
    }


    public List<FS_Operations.FS_NodeInfoExt2> _fsNodesExt2;
    public IEnumerable<FS_Operations.FS_NodeInfoExt2> fsNodesExt2
    {
      get
      {
        if (_fsNodesExt2 == null)
        {
          var codes = NodesPage.ToList();
          if (FetchMode == FetchModeEnum.sNodeFetch)
          {
            IKCMS_ModelCMS_Provider.Provider.managerVFS.EnsureNodes<FS_Operations.FS_NodeInfoExt2>(codes);
            _fsNodesExt2 = IKCMS_ModelCMS_Provider.Provider.managerVFS.NodesVFS.Where(n => codes.Contains(n.sNode)).OrderBy(n => codes.IndexOf(n.sNode)).OfType<FS_Operations.FS_NodeInfoExt2>().ToList();
          }
          else
          {
            IKCMS_ModelCMS_Provider.Provider.managerVFS.EnsureNodesRNODE<FS_Operations.FS_NodeInfoExt2>(codes, false);
            _fsNodesExt2 = IKCMS_ModelCMS_Provider.Provider.managerVFS.NodesVFS.Where(n => codes.Contains(n.rNode)).Distinct((n1, n2) => n1.rNode == n2.rNode).OrderBy(n => codes.IndexOf(n.rNode)).OfType<FS_Operations.FS_NodeInfoExt2>().ToList();
          }
        }
        return _fsNodesExt2;
      }
    }


    protected List<IKCMS_ModelCMS_Interface> _Models;
    public virtual IEnumerable<IKCMS_ModelCMS_Interface> Models
    {
      get
      {
        try
        {
          if (_Models == null)
          {
            IKCMS_ModelCMS_ModelInfo_Interface itemModelInfo = null;
            var modeExtNew = vfsNodeFetchModeEnum.vNode_vData_iNode;
            if (AllowedTypeNames != null)
            {
              try { modeExtNew = (vfsNodeFetchModeEnum)AllowedTypeNames.Select(t => IKCMS_ModelCMS_Provider.Provider.FindBestModelMatch(Utility.FindTypeCached(t))).Where(mi => mi != null).SelectMany(mi => mi.Attributes.OfType<IKCMS_ModelCMS_fsNodeModeAttribute>().Select(a => a.vfsNodeFetchMode)).Max(m => (int)m); }
              catch { }
            }
            //
            // prefetch dei nodi
            List<FS_Operations.FS_NodeInfo_Interface> fsNodesActive = new List<FS_Operations.FS_NodeInfo_Interface>();
            if (UseGenericModelBuild == false && FetchMode == FetchModeEnum.rNodeFetch)
            {
              var nodes01 = NodesPage.ToList();
              var nodes02 = IKCMS_ModelCMS_Provider.Provider.managerVFS.NodesVFS.Select(n => n.rNode).ToList();
              var rNodesMissing = nodes01.Except(nodes02).ToList();  // se non assegno prima le variabili separatamente con un ToList() e uso l'espressione combinata e ottimizzata senza passare per le liste fa BUM!
              IKCMS_ModelCMS_Provider.Provider.managerVFS.FetchNodesT<FS_Operations.FS_NodeInfoExt>(vn => rNodesMissing.Contains(vn.rnode) || rNodesMissing.Contains(vn.folder), null);
            }
            //
            //bool modeExt = false;
            //try { modeExt = AllowedTypeNames.Select(t => IKCMS_ModelCMS_Provider.Provider.FindBestModelMatch(Utility.FindTypeCached(t))).Where(mi => mi != null).SelectMany(mi => mi.Attributes.OfType<IKCMS_ModelCMS_fsNodeModeAttribute>().Select(a => a.vfsNodeFetchMode)).Any(m => m == vfsNodeFetchModeEnum.vNode_vData_iNode_Extra); }
            //catch { }
            //IEnumerable<FS_Operations.FS_NodeInfo_Interface> nodesTmp = (modeExt ? fsNodesExt.OfType<FS_Operations.FS_NodeInfo_Interface>() : fsNodes.OfType<FS_Operations.FS_NodeInfo_Interface>());
            IEnumerable<FS_Operations.FS_NodeInfo_Interface> nodesTmp = null;
            if (modeExtNew <= vfsNodeFetchModeEnum.vNode_vData_iNode)
            {
              nodesTmp = fsNodes.OfType<FS_Operations.FS_NodeInfo_Interface>();
            }
            else if (modeExtNew == vfsNodeFetchModeEnum.vNode_vData_iNode_ExtraVariants)
            {
              nodesTmp = fsNodesExt2.OfType<FS_Operations.FS_NodeInfo_Interface>();
            }
            else
            {
              nodesTmp = fsNodesExt.OfType<FS_Operations.FS_NodeInfo_Interface>();
            }
            //
            if (DataStorageCached.brokenNodes != null && DataStorageCached.brokenNodes.Any())
            {
              if (FetchMode == FetchModeEnum.rNodeFetch)
              {
                nodesTmp = nodesTmp.Where(n => !DataStorageCached.brokenNodes.Contains(n.rNode));
              }
              else
              {
                nodesTmp = nodesTmp.Where(n => !DataStorageCached.brokenNodes.Contains(n.sNode));
              }
            }
            _Models = nodesTmp.Select(n =>
            {
              IKCMS_ModelCMS_Interface mdl = null;
              if (UseGenericModelBuild == true)
              {
                mdl = IKCMS_ModelCMS_Provider.Provider.ModelBuildGeneric(n.vNode.snode);
              }
              else
              {
                itemModelInfo = IKCMS_ModelCMS_Provider.Provider.FindBestModelMatch(Utility.FindTypeCached(n.vData.manager_type));
                mdl = IKCMS_ModelCMS_Provider.Provider.ModelBuild(null, n, itemModelInfo);
              }
              if (mdl == null)
              {
                DataStorageCached.brokenNodes.Add((FetchMode == FetchModeEnum.rNodeFetch) ? n.vNode.rnode : n.vNode.snode);
              }
              return mdl;
            }).Where(m => m != null).ToList();
            //
          }
        }
        catch { }
        return _Models;
      }
    }


    public virtual List<IKCMS_ModelCMS_GenericBrickInterface> GetModelsForTeasers(int maxNodes, string forcedSortingMode)
    {
      ArgSetClear();
      //forcedSortingMode = forcedSortingMode ?? "+Date";
      if (forcedSortingMode.IsNotNullOrWhiteSpace())
      {
        ArgsSet[ParameterNameSorter] = forcedSortingMode;
      }
      GetCurrentMode = "teaser";
      if (ModelInput != null && ModelInput != Model)
      {
        TagsExternal = ModelInput.TagsIds.Except(TagsFromModel).Distinct().ToList();
      }
      //
      this.UseGenericModelBuild = false;
      //
      ScanVFS(null, null, null, maxNodes);
      PageItems(maxNodes, Guid.NewGuid().ToString());
      return Models.OfType<IKCMS_ModelCMS_GenericBrickInterface>().ToList();
    }


    //
    // metodi da utilizzare per i widget tipo teaser con search su archivio con caching dell'output html
    //
    public virtual List<int> GetFirstNodesNoCache(int maxNodes)
    {
      if (!DataStorageCached.Processed)
      {
        ScanVFS_Worker(null);
        ScanVFS_Sorter_Worker(null);
        ScanVFS_Lucene_Worker(null);
        //
        // in mancanza di sort specificati utilizziamo il nome della risorsa, quindi rNode, quindi sNode
        //
        var nodeSet = Query_rNodesFilteredNOvData.Join(Query_vNodesVFS, r => r, vn => vn.rnode, (r, vn) => vn);
        if (FetchMode == FetchModeEnum.rNodeFetch)
          nodeSet = nodeSet.GroupBy(r => r.rnode).Select(g => g.First());
        var nodeSetOrdered = nodeSet.OrderBy(vn => vn.name).ThenBy(vn => vn.folder).ThenBy(vn => vn.position).ThenBy(vn => vn.rnode);
        if (FetchMode == FetchModeEnum.sNodeFetch)
        {
          return ScanVFS_PathFilter(nodeSetOrdered.ThenBy(vn => vn.snode).Select(vn => vn.snode).ToList()).Take(maxNodes).ToList();
          //return nodeSetOrdered.ThenBy(vn => vn.snode).Select(vn => vn.snode).Take(maxNodes).ToList();
        }
        else
        {
          return ScanVFS_PathFilter(nodeSetOrdered.Select(vn => vn.rnode).ToList()).Take(maxNodes).ToList();
          //return nodeSetOrdered.Select(vn => vn.rnode).Take(maxNodes).ToList();
        }
      }
      return ((FetchMode == FetchModeEnum.sNodeFetch) ? DataStorageCached.sNodes : DataStorageCached.rNodes).Take(maxNodes).ToList();
    }


    public virtual void EnsureFilters()
    {
      if (Query_vDatasFiltered == null)
      {
        ScanVFS_Worker(null);
      }
    }


    public virtual void ScanVFS() { ScanVFS(null, null, null, null); }
    public virtual void ScanVFS(int? maxNodes) { ScanVFS(null, null, null, maxNodes); }
    public virtual void ScanVFS(IEnumerable<int> TagsExtra, string sortModeOverride, string luceneStringOverride) { ScanVFS(TagsExtra, sortModeOverride, luceneStringOverride, null); }
    public virtual void ScanVFS(IEnumerable<int> TagsExtra, string sortModeOverride, string luceneStringOverride, int? maxNodes)
    {
      if (DataStorageCached.Processed)
        return;
      try
      {
        DataStorageCached.brokenNodes = new List<int>();
        ScanVFS_Worker(TagsExtra);
        ScanVFS_Sorter_Worker(sortModeOverride);
        ScanVFS_Lucene_Worker(luceneStringOverride);
        ScanVFS_Sorter_Finalize(maxNodes);
        ScanVFS_PathFilter(null);
        ScanVFS_ScanPost();
      }
      catch { }
    }


    public virtual PagerSimpleInterface ScanVFS_Paged(int? pagerPageSize) { return ScanVFS_Paged(pagerPageSize, null, null, null, null); }
    public virtual PagerSimpleInterface ScanVFS_Paged(int? pagerPageSize, string pagingVarQueryString, string sortModeOverride, string luceneStringOverride) { return ScanVFS_Paged(pagerPageSize, pagingVarQueryString, sortModeOverride, luceneStringOverride, null); }
    public virtual PagerSimpleInterface ScanVFS_Paged(int? pagerPageSize, string pagingVarQueryString, string sortModeOverride, string luceneStringOverride, IEnumerable<int> TagsExtra)
    {
      ScanVFS(TagsExtra, sortModeOverride, luceneStringOverride);
      return PageItems(pagerPageSize, pagingVarQueryString);
    }


    public virtual PagerSimpleInterface PageItems(int? pagerPageSize) { return PageItems(pagerPageSize, null); }
    public virtual PagerSimpleInterface PageItems(int? pagerPageSize, string pagingVarQueryString)
    {
      //per evitare il reprocessing se accediamo in una PartialView all'oggetto gia' inizializzato
      if (Pager == null)
      {
        try
        {
          // la modalita' di scan viene attivata per default solo se Model != ModelInput
          if (AutoFindCurrentPageFromModel.GetValueOrDefault(Model != ModelInput) && ModelInput != null)
          {
            int idx = FindCurrentPageFromModel(pagerPageSize, pagingVarQueryString ?? ParameterNamePager);
          }
          string pagerBaseUrl = null;
          try
          {
            if (Model != null && Model != ModelInput && PagerAllowOverrideBaseUrl.GetValueOrDefault(true))
              pagerBaseUrl = Model.UrlCanonical;
          }
          catch { }
          Pager = Ikon.IKCMS.Pagers.PagingHelperExtensions.FactoryPagerSimple(Nodes, pagerPageSize, pagingVarQueryString ?? ParameterNamePager, ArgsSet, pagerBaseUrl);
        }
        catch { }
      }
      return Pager;
    }


    public virtual int FindCurrentPageFromModel(int? pagerPageSize, string pagingVarQueryString)
    {
      try
      {
        if (ArgsSet[pagingVarQueryString ?? ParameterNamePager].IsNullOrEmpty())  //si deve forse usare semplicemente null? (PagerStartIndex= e' valido?)
        {
          int node = FetchMode == FetchModeEnum.rNodeFetch ? ModelInput.rNode : ModelInput.sNode;
          int idx = Nodes.IndexOf(node);
          if (idx >= 0)
          {
            if (pagerPageSize > 0)
            {
              idx -= idx % pagerPageSize.Value;  // normalizzazione dell'offset al primo elemento della pagina
            }
            // proviamo a gestire il riassegnamento delle variabili in maniera piu' robusta visto che se si usa HttpContext.Current.Request.Params e' readonly
            try
            {
              ArgsSet[pagingVarQueryString ?? ParameterNamePager] = idx.ToString();
            }
            catch
            {
              try
              {
                ArgsSet = new NameValueCollection(ArgsSet);
                ArgsSet[pagingVarQueryString ?? ParameterNamePager] = idx.ToString();
              }
              catch { }
            }
            return idx;
          }
        }
      }
      catch { }
      return 0;
    }


    //
    // query precostruite da utilizzare nelle customizzazioni dei filtri/sorter
    //
    protected IQueryable<IKGD_VNODE> Query_vNodesVFS { get; set; }
    protected IQueryable<IKGD_VDATA> Query_vDatasVFS { get; set; }
    protected IQueryable<IKGD_VDATA> Query_vDatasVFS_NoDynFilters { get; set; }
    protected IQueryable<IKGD_PROPERTY> Query_PropsVFS { get; set; }
    protected IQueryable<IKGD_PROPERTY> Query_PropsVFSNN { get; set; }
    protected IQueryable<IKGD_PROPERTY> Query_PropsFilter { get; set; }
    protected IQueryable<IKGD_PROPERTY> Query_PropsValuedVFS { get; set; }
    protected IQueryable<IKGD_VDATA_KEYVALUE> Query_DeserializedFilter { get; set; }
    protected IQueryable<IKGD_VDATA_KEYVALUE> Query_DeserializedVFS { get; set; }
    protected IQueryable<IKGD_VDATA> Query_vDatasFiltered { get; set; }
    protected IQueryable<int> Query_rNodesFilteredNOvData { get; set; }
    protected IQueryable<int> Query_rNodesFilteredUnique { get; set; }
    protected IQueryable<int> Query_rNodesFilteredJoin { get; set; }
    protected IQueryable<int> Query_TagsActiveSetV1 { get; set; }
    protected IQueryable<int> Query_TagsActiveSetV2 { get; set; }
    protected IQueryable<KeyValuePair<int, int>> Query_TagsActiveSetWithRNODES { get; set; }
    //
    // attenzione ai parametri passati con TagsExtra che non vengono inclusi nella CacheKey: eventualmente utilizzare TagsExternal
    protected virtual void ScanVFS_Worker(IEnumerable<int> TagsExtra)
    {
      //
      // generazione della lista degli attributi da ricercare
      //
      List<int> TagsOK = new List<int>();
      if (TagsFromModel != null && TagsFromModel.Any())
        TagsOK.AddRange(TagsFromModel.Except(TagsOK));
      if (TagsExternal != null && TagsExternal.Any())
      {
        TagsOK.AddRange(TagsExternal.Except(TagsOK));
        DefaultCachingTimeData = Math.Min(DefaultCachingTimeMain / 10, DefaultCachingTimeData);
      }
      if (TagsValuedExternal != null && TagsValuedExternal.Any())
      {
        DefaultCachingTimeData = Math.Min(DefaultCachingTimeMain / 10, DefaultCachingTimeData);
      }
      if (KVTsExternal != null && KVTsExternal.Any())
      {
        DefaultCachingTimeData = Math.Min(DefaultCachingTimeMain / 10, DefaultCachingTimeData);
      }
      if (FiltersCustom != null && FiltersCustom.Any() && !IgnoreFiltersCustom.GetValueOrDefault(false))
      {
        DefaultCachingTimeData = Math.Min(DefaultCachingTimeMain / 10, DefaultCachingTimeData);
      }
      if (TagsExtra != null && TagsExtra.Any())
      {
        TagsOK.AddRange(TagsExtra.Except(TagsOK));
        DefaultCachingTimeData = Math.Min(DefaultCachingTimeMain / 10, DefaultCachingTimeData);
      }
      //
      // rimozione di tutti i tagsId specificati negativi (serve per poter eliminare i Tags associati al Model specificando nella queryString un tag con Id negativo)
      TagsOK = TagsOK.Where(t => t >= 0).Except(TagsOK.Where(t => t < 0).Select(t => Math.Abs(t))).ToList();
      TagsAll.Clear();
      TagsAll.AddRange(TagsOK);
      //
      Expression<Func<IKGD_VNODE, bool>> vNodeFilter = fsOp.Get_vNodeFilterACLv2(true);
      Expression<Func<IKGD_VDATA, bool>> vDataFilter = fsOp.Get_vDataFilterACLv2(false, true);
      //
      // setup dei filtri sui nodi VFS
      //
      DateTime dateCMS = FS_OperationsHelpers.DateTimeSession;
      if (IgnoreExpiryDates.GetValueOrDefault(false) == false)
      {
        vDataFilter = vDataFilter.And(n => (n.date_activation == null || n.date_activation.Value <= dateCMS) && (n.date_expiry == null || dateCMS <= n.date_expiry.Value));
      }
      //
      hasFilterSet |= TagsOK.Any();
      if (Model != null)
      {
        //
        // scan dei soli archivi collegati nel caso questi siano stati specificati
        //
        try
        {
          // fetch dei folder dove leggere le risorse
          List<int> rNodeArchives = new List<int>();
          if (GetArchiveFolderFromRelations.GetValueOrDefault(true))
          {
            rNodeArchives = Model.RelationsOrdered.Where(r => r.type == IKGD_Constants.IKGD_ArchiveRelationName).Select(r => r.rnode_dst).Distinct().ToList();
          }
          if (UseModelFolderAsArchive == true && !rNodeArchives.Any() && this.Model != null && !TagsFromModel.Any())
          {
            // se UseModelFolderAsArchive e' stato specificato e il model non ha nessun archivio correlato uso il model stesso come archivio
            rNodeArchives = new List<int> { this.Model.rNode };
          }
          if (((rNodeArchives != null && rNodeArchives.Any()) && AllowArchiveSelection.GetValueOrDefault(true)) || ArgsSet[ParameterNameFolders].IsNotNullOrWhiteSpace() || ArgsSet[ParameterNameFoldersTree].IsNotNullOrWhiteSpace())
          {
            List<int> folderSet = null;
            if (ArgsSet[ParameterNameFolders].IsNotNullOrWhiteSpace())
            {
              folderSet = Utility.ExplodeT<int>(ArgsSet[ParameterNameFolders], ",", " ", true).Distinct().ToList();
            }
            else if (ArgsSet[ParameterNameFoldersTree].IsNotNullOrWhiteSpace())
            {
              var roots = Utility.ExplodeT<int>(ArgsSet[ParameterNameFoldersTree], ",", " ", true).Distinct().ToList();
              //FS_Operations.FS_TreeNode<IKCMS_TreeBrowser_fsNodeElement_Interface> nodesTreeFullInfo = fsOp.Get_TreeDataShortGeneric<IKCMS_TreeBrowser_fsNodeElement_Interface>(roots, null, TreeScanFilterVDATA, vNodeFilter, vDataFilter, MaxScanRecursionLevel, true, true, FilteredResourcesAreFolders.GetValueOrDefault(false));
              FS_Operations.FS_TreeNode<FS_Operations.FS_NodeInfo> nodesTreeFullInfo = fsOp.Get_TreeDataShortGeneric<FS_Operations.FS_NodeInfo>(roots, null, TreeScanFilterVDATA, vNodeFilter, vDataFilter, MaxScanRecursionLevel, true, true, FilteredResourcesAreFolders.GetValueOrDefault(false));
              // aggiungiamo almeno la lista dei nodi di partenza degli archivi che potrebbero essere stati filtrati dal blocco della ricorsione
              folderSet = nodesTreeFullInfo.RecurseOnTree.Where(n => n.Data != null).Select(n => n.Data.vNode.folder).Distinct().Union(roots).ToList();
            }
            else
            {
              //FS_Operations.FS_TreeNode<IKCMS_TreeBrowser_fsNodeElement_Interface> nodesTreeFullInfo = fsOp.Get_TreeDataShortGeneric<IKCMS_TreeBrowser_fsNodeElement_Interface>(rNodeArchives, null, TreeScanFilterVDATA, vNodeFilter, vDataFilter, MaxScanRecursionLevel, true, true, FilteredResourcesAreFolders.GetValueOrDefault(false));
              FS_Operations.FS_TreeNode<FS_Operations.FS_NodeInfo> nodesTreeFullInfo = fsOp.Get_TreeDataShortGeneric<FS_Operations.FS_NodeInfo>(rNodeArchives, null, TreeScanFilterVDATA, vNodeFilter, vDataFilter, MaxScanRecursionLevel, true, true, FilteredResourcesAreFolders.GetValueOrDefault(false));
              // aggiungiamo almeno la lista dei nodi di partenza degli archivi che potrebbero essere stati filtrati dal blocco della ricorsione
              folderSet = nodesTreeFullInfo.RecurseOnTree.Where(n => n.Data != null).Select(n => n.Data.vNode.folder).Distinct().Union(rNodeArchives).ToList();
            }
            //
            if (folderSet != null && folderSet.Any())
            {
              //vNodeFilter = vNodeFilter.And(n => (n.flag_folder == false && folderSet.Contains(n.folder)) || (n.flag_folder == true && folderSet.Contains(n.parent.Value)));  // scan di tutto il sub tree
              //vNodeFilter = vNodeFilter.And(n => folderSet.Contains(n.parent ?? n.folder));  // scan di tutto il sub tree con shortcut per il fetch di nodi o folder nel folderset
              if (FilteredResourcesAreFolders == true)
              {
                vNodeFilter = vNodeFilter.And(n => n.flag_folder == true && folderSet.Contains(n.parent.Value));
              }
              else if (FilteredResourcesAreFolders == false)
              {
                vNodeFilter = vNodeFilter.And(n => n.flag_folder == false && folderSet.Contains(n.folder));
              }
              else
              {
                vNodeFilter = vNodeFilter.And(n => (n.flag_folder == false && folderSet.Contains(n.folder)) || (n.flag_folder == true && folderSet.Contains(n.parent.Value)));
              }
              hasFilterSet |= true;
              FilterNodesByValidPathRequired = false;
            }
          }
        }
        catch { }
      }
      //
      if (FilterVNODE != null)
        vNodeFilter = vNodeFilter.And(FilterVNODE);
      if (FilterVDATA != null)
      {
        vDataFilter = vDataFilter.And(FilterVDATA);
      }
      else
      {
        if (AllowedTypeNames != null && AllowedTypeNames.Any() && AllowedCategories != null && AllowedCategories.Any())
          vDataFilter = vDataFilter.And(n => AllowedTypeNames.Contains(n.manager_type) && AllowedCategories.Contains(n.category));
        else if (AllowedTypeNames != null && AllowedTypeNames.Any())
          vDataFilter = vDataFilter.And(n => AllowedTypeNames.Contains(n.manager_type));
        else if (AllowedCategories != null && AllowedCategories.Any())
          vDataFilter = vDataFilter.And(n => AllowedCategories.Contains(n.category));
      }
      //
      // gestione dei filtri extra (eg. price)
      if (ExtraFilterVNODE != null)
      {
        vNodeFilter = vNodeFilter.And(ExtraFilterVNODE.Expand());
      }
      if (ExtraFilterVDATA != null)
      {
        vDataFilter = vDataFilter.And(ExtraFilterVDATA.Expand());
      }
      //
      // per operare query aggiuntive sul set di vData senza filtri dinamici ma solo con i filtri per folder e VFS standard
      var vDataFilterNoDynamic = vDataFilter;
      Query_vDatasVFS_NoDynFilters = fsOp.NodesActive<IKGD_VDATA>().Where(vDataFilterNoDynamic);
      //
      // gestione dei filtri per data
      //
      DateTime? FilterDateMin = Utility.TryParse<DateTime?>(ArgsSet[ParameterNameDateMin], null);
      DateTime? FilterDateMax = Utility.TryParse<DateTime?>(ArgsSet[ParameterNameDateMax], null);
      if (ArgsSet[ParameterNameDateExt].IsNotEmpty() && FilterDateMin != null && FilterDateMax == null)
      {
        var str = ArgsSet[ParameterNameDateExt].ToLower();
        int val = Utility.TryParse<int>(str.TrimEnd("ymdhs".ToCharArray()));
        if (str.EndsWith("y"))
        {
          FilterDateMax = FilterDateMin.Value.AddYears(val);
        }
        else if (str.EndsWith("m"))
        {
          FilterDateMax = FilterDateMin.Value.AddMonths(val);
        }
        else if (str.EndsWith("d"))
        {
          FilterDateMax = FilterDateMin.Value.AddDays(val);
        }
        else if (str.EndsWith("h"))
        {
          FilterDateMax = FilterDateMin.Value.AddHours(val);
        }
        else
        {
          FilterDateMax = FilterDateMin.Value.AddSeconds(val);
        }
        if (val > 0)
        {
          FilterDateMax = FilterDateMax.Value.AddSeconds(-1);
        }
      }
      DateTime? FilterDateExpiryMin = Utility.TryParse<DateTime?>(ArgsSet[ParameterNameDateExpiryMin], null);
      DateTime? FilterDateExpiryMax = Utility.TryParse<DateTime?>(ArgsSet[ParameterNameDateExpiryMax], null);
      if (FilterDateMin != null && FilterDateMax != null && FilterDateMax < FilterDateMin)
        Utility.Swap(FilterDateMin, FilterDateMax);
      if (FilterDateExpiryMin != null && FilterDateExpiryMax != null && FilterDateExpiryMax < FilterDateExpiryMin)
        Utility.Swap(FilterDateExpiryMin, FilterDateExpiryMax);
      if (FilterDateMin != null)
      {
        vDataFilter = vDataFilter.And(n => (n.date_node_aux == null && n.date_node >= FilterDateMin.Value) || (n.date_node_aux != null && n.date_node_aux.Value >= FilterDateMin.Value));
        DefaultCachingTimeData = Math.Min(DefaultCachingTimeMain / 20, DefaultCachingTimeData);
      }
      if (FilterDateMax != null)
      {
        vDataFilter = vDataFilter.And(n => (n.date_node_aux == null && n.date_node <= FilterDateMax.Value) || (n.date_node_aux != null && n.date_node <= FilterDateMax.Value));
        DefaultCachingTimeData = Math.Min(DefaultCachingTimeMain / 20, DefaultCachingTimeData);
      }
      //
      Query_vNodesVFS = fsOp.NodesActive<IKGD_VNODE>().Where(vNodeFilter);
      Query_vDatasVFS = fsOp.NodesActive<IKGD_VDATA>().Where(vDataFilter);
      Query_PropsVFS = fsOp.NodesActive<IKGD_PROPERTY>();
      Query_PropsVFSNN = fsOp.NodesActive<IKGD_PROPERTY>().Where(p => p.attributeId != null);
      Query_PropsFilter = null;   // filtro per le properties attive
      Query_PropsValuedVFS = fsOp.NodesActive<IKGD_PROPERTY>();
      Query_DeserializedFilter = null;   // filtro per le properties deserializzate
      Query_DeserializedVFS = fsOp.DeserializedVDATA_WithLanguage();
      //
      ScanVFS_WorkerProcessorInit();
      //
      Query_vDatasFiltered = Query_vDatasVFS;
      Query_rNodesFilteredNOvData = Query_vDatasVFS.Select(vd => vd.rnode);
      Query_rNodesFilteredUnique = Query_vDatasVFS.Select(vd => vd.rnode).Where(r => Query_vNodesVFS.Any(vn => vn.rnode == r));  // rNodes unici
      Query_rNodesFilteredJoin = Query_vDatasVFS.Select(vd => vd.rnode).Join(Query_vNodesVFS, r => r, vn => vn.rnode, (r, vd) => r);  // rNodes multipli uno per ciascun symlink
      //
      if (SelectTagsWithVarNameOnly.GetValueOrDefault(true))
      {
        Query_PropsVFS = Query_PropsVFS.Where(p => p.name == IKGD_Constants.IKCAT_TagPropertyName);
        Query_PropsVFSNN = Query_PropsVFSNN.Where(p => p.name == IKGD_Constants.IKCAT_TagPropertyName);
      }
      //
      ScanVFS_WorkerProcessorPre();
      //
      // supporto per la generazione di queries differenziate nel caso si utilizzino tag combinati tutti con AND
      // oppure con AND tra categorie/type differenti e con OR all'interno della stessa categoria
      // per default e' attiva la modalita' mista AND/OR
      //
      foreach (var tag_grp in IKCAT_AttributeStorage.GetTags(TagsOK).GroupBy(a => MixedAndOrMode.GetValueOrDefault(true) ? a.AttributeType : a.AttributeId.ToString()))
      {
        Expression<Func<IKGD_PROPERTY, bool>> prop_predicate = null;
        if (tag_grp.Count() > 1)
        {
          List<int> tagsMatch = tag_grp.Select(a => a.AttributeId).Distinct().ToList();
          prop_predicate = p => tagsMatch.Contains(p.attributeId.Value);
        }
        else
        {
          int tagMatch = tag_grp.FirstOrDefault().AttributeId;
          prop_predicate = p => p.attributeId.Value == tagMatch;
        }
        if (Query_PropsFilter == null)
        {
          Query_PropsFilter = Query_PropsVFS.Where(prop_predicate);
        }
        else
        {
          Query_PropsFilter = Query_PropsFilter.Join(Query_PropsVFS.Where(prop_predicate), p => p.rnode, p => p.rnode, (po, pi) => po);
        }
        Query_vDatasFiltered = Query_vDatasFiltered.Join(Query_PropsVFS.Where(prop_predicate), vd => vd.rnode, p => p.rnode, (vd, pi) => vd);
        Query_rNodesFilteredNOvData = Query_rNodesFilteredNOvData.Join(Query_PropsVFS.Where(prop_predicate), r => r, p => p.rnode, (r, p) => r);
        Query_rNodesFilteredUnique = Query_rNodesFilteredUnique.Join(Query_PropsVFS.Where(prop_predicate), r => r, p => p.rnode, (r, p) => r);
        Query_rNodesFilteredJoin = Query_rNodesFilteredJoin.Join(Query_PropsVFS.Where(prop_predicate), r => r, p => p.rnode, (r, p) => r);
      }
      //
      foreach (var keyValue in TagsValuedExternal)
      {
        Expression<Func<IKGD_PROPERTY, bool>> prop_predicate = null;
        string var_name = null;
        string var_value = keyValue.Value;
        if (keyValue.Key != null && keyValue.Key.StartsWith(ParameterNameFilterValueBase))
        {
          var_name = keyValue.Key.Substring(ParameterNameFilterValueBase.Length);
          if (!string.IsNullOrEmpty(var_name) && !string.IsNullOrEmpty(var_value))
          {
            prop_predicate = p => p.name == var_name && string.Equals(p.value, var_value);
          }
        }
        if (keyValue.Key != null && keyValue.Key.StartsWith(ParameterNameFilterLikeBase))
        {
          var_name = keyValue.Key.Substring(ParameterNameFilterLikeBase.Length);
          if (!string.IsNullOrEmpty(var_name) && !string.IsNullOrEmpty(var_value))
          {
            var tokens = var_value.Replace("%", " ").Replace("_", " ").Split(' ', '-', '.', ',', ':', ';', '\'').Select(f => f.ToLower()).Distinct().ToList();
            if (tokens.Any())
            {
              if (LikeIsUnordered.GetValueOrDefault(true))
              {
                // modalita' and non ordinato
                //prop_predicate = PredicateBuilder.True<IKGD_PROPERTY>();
                prop_predicate = p => p.name == var_name && p.value != null;
                tokens.OrderBy(s => s).ForEach(t => prop_predicate = prop_predicate.And(p => SqlMethods.Like(p.value, "%" + t + "%")));
              }
              else
              {
                // modalita' and ordinato
                string like_str = "%" + Utility.Implode(tokens, "%", null, true, true) + "%";
                prop_predicate = p => p.name == var_name && p.value != null && SqlMethods.Like(p.value, like_str);
              }
            }
          }
        }
        if (prop_predicate == null)
          continue;
        //
        if (Query_PropsFilter == null)
        {
          Query_PropsFilter = Query_PropsValuedVFS.Where(prop_predicate);
        }
        else
        {
          Query_PropsFilter = Query_PropsFilter.Join(Query_PropsValuedVFS.Where(prop_predicate), p => p.rnode, p => p.rnode, (po, pi) => po);
        }
        Query_vDatasFiltered = Query_vDatasFiltered.Join(Query_PropsValuedVFS.Where(prop_predicate), vd => vd.rnode, p => p.rnode, (vd, pi) => vd);
        Query_rNodesFilteredNOvData = Query_rNodesFilteredNOvData.Join(Query_PropsValuedVFS.Where(prop_predicate), r => r, p => p.rnode, (r, p) => r);
        Query_rNodesFilteredUnique = Query_rNodesFilteredUnique.Join(Query_PropsValuedVFS.Where(prop_predicate), r => r, p => p.rnode, (r, p) => r);
        Query_rNodesFilteredJoin = Query_rNodesFilteredJoin.Join(Query_PropsValuedVFS.Where(prop_predicate), r => r, p => p.rnode, (r, p) => r);
      }
      //
      foreach (var keyValue in KVTsExternal)
      {
        Expression<Func<IKGD_VDATA_KEYVALUE, bool>> keyvalue_predicate = null;
        string var_name = null;
        string var_value = keyValue.Value;
        if (keyValue.Key.IsNullOrWhiteSpace() || !keyValue.Key.StartsWith(ParameterNameFilterKVTsBase) || keyValue.Key.StartsWith(ParameterNameFilterKVTsRangeBase))
        {
          continue;
        }
        if (FilterKvtNullValuesAllowed.GetValueOrDefault(false) == false && var_value.IsNullOrEmpty())
        {
          continue;
        }
        var_name = keyValue.Key.Split("_".ToCharArray(), 2).Skip(1).FirstOrDefault().DefaultIfEmptyTrim(null);
        if (var_name.IsNullOrWhiteSpace())
        {
          continue;
        }
        if (keyValue.Key.StartsWith(ParameterNameFilterKVTsInt))
        {
          int? value = Utility.TryParse<int?>(var_value);
          keyvalue_predicate = r => string.Equals(r.Key, var_name) && r.ValueInt == value;
        }
        else if (keyValue.Key.StartsWith(ParameterNameFilterKVTsFloat))
        {
          double? value = Utility.TryParse<double?>(var_value);
          keyvalue_predicate = r => string.Equals(r.Key, var_name) && r.ValueDouble == value;
        }
        else if (keyValue.Key.StartsWith(ParameterNameFilterKVTsDate))
        {
          DateTime? value = Utility.TryParse<DateTime?>(var_value);
          if (value != null && value < Utility.DateTimeMinValueDB)
            value = Utility.DateTimeMinValueDB;
          if (value != null && value > Utility.DateTimeMaxValueDB)
            value = Utility.DateTimeMaxValueDB;
          keyvalue_predicate = r => string.Equals(r.Key, var_name) && r.ValueDate == value;
        }
        else if (keyValue.Key.StartsWith(ParameterNameFilterKVTsString))
        {
          keyvalue_predicate = r => string.Equals(r.Key, var_name) && string.Equals(r.ValueString, var_value);
        }
        else if (keyValue.Key.StartsWith(ParameterNameFilterKVTsLike))
        {
          // consentiamo anche di specificare la var_name come wildcard "*" in modo da ricercare su tutti i testi deserializzati come una specie di mini Lucene
          if (!string.IsNullOrEmpty(var_name) && !string.IsNullOrEmpty(var_value))
          {
            var tokens = var_value.Replace("%", " ").Replace("_", " ").Split(' ', '-', '.', ',', ':', ';', '\'').Select(f => f.ToLower()).Distinct().ToList();
            if (tokens.Any())
            {
              if (LikeIsUnordered.GetValueOrDefault(true))
              {
                // modalita' and non ordinato
                if (var_name == "*")
                  keyvalue_predicate = r => r.ValueString != null;
                else
                  keyvalue_predicate = r => string.Equals(r.Key, var_name) && r.ValueString != null;
                tokens.OrderBy(s => s).ForEach(t => keyvalue_predicate = keyvalue_predicate.And(r => SqlMethods.Like(r.ValueString, "%" + t + "%")));
              }
              else
              {
                // modalita' and ordinato
                string like_str = "%" + Utility.Implode(tokens, "%", null, true, true) + "%";
                if (var_name == "*")
                  keyvalue_predicate = r => r.ValueString != null && SqlMethods.Like(r.ValueString, like_str);
                else
                  keyvalue_predicate = r => string.Equals(r.Key, var_name) && r.ValueString != null && SqlMethods.Like(r.ValueString, like_str);
              }
            }
          }
        }
        if (keyvalue_predicate == null)
          continue;
        //
        if (Query_DeserializedFilter == null)
        {
          Query_DeserializedFilter = Query_DeserializedVFS.Where(keyvalue_predicate);
        }
        else
        {
          Query_DeserializedFilter = Query_DeserializedFilter.Join(Query_DeserializedVFS.Where(keyvalue_predicate), p => p.rNode, p => p.rNode, (po, pi) => po);
        }
        Query_vDatasFiltered = Query_vDatasFiltered.Join(Query_DeserializedVFS.Where(keyvalue_predicate), vd => vd.rnode, p => p.rNode, (vd, pi) => vd);
        Query_rNodesFilteredNOvData = Query_rNodesFilteredNOvData.Join(Query_DeserializedVFS.Where(keyvalue_predicate), r => r, p => p.rNode, (r, p) => r);
        Query_rNodesFilteredUnique = Query_rNodesFilteredUnique.Join(Query_DeserializedVFS.Where(keyvalue_predicate), r => r, p => p.rNode, (r, p) => r);
        Query_rNodesFilteredJoin = Query_rNodesFilteredJoin.Join(Query_DeserializedVFS.Where(keyvalue_predicate), r => r, p => p.rNode, (r, p) => r);
        //
      }
      //
      var complexParams = KVTsExternal.Where(r => r.Key.StartsWith(ParameterNameFilterKVTsRangeBase)).Select(r => new { Key = r.Key, Value = r.Value, Rx = ParameterNameFilterKVTsRangeRx.Match(r.Key) }).Where(r => r.Rx.Success).GroupBy(r => r.Rx.Groups["key"].Value);
      foreach (var complexParam in complexParams)
      {
        try
        {
          var first = complexParam.FirstOrDefault(r => r.Rx.Groups["pos"].Value == "1");
          var last = complexParam.FirstOrDefault(r => r.Rx.Groups["pos"].Value == "2");
          string var_name = (first ?? last).Rx.Groups["key"].Value.Trim();
          string var_type = (first ?? last).Rx.Groups["type"].Value.Trim();
          if (var_name.IsNullOrWhiteSpace())
          {
            continue;
          }
          if (FilterKvtNullValuesAllowed.GetValueOrDefault(false) == false && first.Value.IsNullOrEmpty() && last.Value.IsNullOrEmpty())
          {
            continue;
          }
          Expression<Func<IKGD_VDATA_KEYVALUE, bool>> keyvalue_predicate = null;
          if (var_type == "D" || var_type == "DT")
          {
            DateTime valueFirst = Utility.TryParse<DateTime>(first != null ? first.Value : null, Utility.DateTimeMinValueDB);
            DateTime valueLast = Utility.TryParse<DateTime>(last != null ? last.Value : null, Utility.DateTimeMaxValueDB);
            if (valueFirst > valueLast)
            {
              var tmp01 = valueFirst;
              valueFirst = valueLast;
              valueLast = tmp01;
            }
            if (var_type == "D")
            {
              try { valueFirst = valueFirst.Date; }
              catch { }
              try { valueLast = valueLast.Date.AddSeconds(86400); }
              catch { }
            }
            keyvalue_predicate = r => (r.ValueDate == null || r.ValueDate < valueLast) && (r.ValueDate == null || r.ValueDate >= valueFirst);
          }
          if (var_type == "DR" || var_type == "DRT")  // sovrapposizione di range di date
          {
            DateTime valueFirst = Utility.TryParse<DateTime>(first != null ? first.Value : null, Utility.DateTimeMinValueDB);
            DateTime valueLast = Utility.TryParse<DateTime>(last != null ? last.Value : null, Utility.DateTimeMaxValueDB);
            if (valueFirst > valueLast)
            {
              var tmp01 = valueFirst;
              valueFirst = valueLast;
              valueLast = tmp01;
            }
            if (var_type == "DR")
            {
              try { valueFirst = valueFirst.Date; }
              catch { }
              try { valueLast = valueLast.Date.AddSeconds(86400); }
              catch { }
            }
            //il confronto tra range di date viene attivato solo se sono definite sia r.ValueDate che r.ValueDateExt, altrimenti viene usato un confronto normale su r.ValueDate
            keyvalue_predicate = r => (r.ValueDate != null && r.ValueDateExt != null) ? (((r.ValueDate < valueFirst) ? valueFirst : r.ValueDate) <= ((r.ValueDateExt < valueLast) ? r.ValueDateExt : valueLast)) : (r.ValueDate == null || r.ValueDate < valueLast) && (r.ValueDate == null || r.ValueDate >= valueFirst);
          }
          else if (var_type == "F")
          {
            double valueFirst = Utility.TryParse<double>(first != null ? first.Value : null, double.MinValue);
            double valueLast = Utility.TryParse<double>(last != null ? last.Value : null, double.MaxValue);
            if (valueFirst > valueLast)
            {
              var tmp01 = valueFirst;
              valueFirst = valueLast;
              valueLast = tmp01;
            }
            keyvalue_predicate = r => (r.ValueDouble == null || r.ValueDouble <= valueLast) && (r.ValueDouble == null || r.ValueDouble >= valueFirst);
          }
          else if (var_type == "I")
          {
            int valueFirst = Utility.TryParse<int>(first != null ? first.Value : null, int.MinValue);
            int valueLast = Utility.TryParse<int>(last != null ? last.Value : null, int.MaxValue);
            if (valueFirst > valueLast)
            {
              var tmp01 = valueFirst;
              valueFirst = valueLast;
              valueLast = tmp01;
            }
            keyvalue_predicate = r => (r.ValueInt == null || r.ValueInt <= valueLast) && (r.ValueInt == null || r.ValueInt >= valueFirst);
          }
          //
          if (keyvalue_predicate != null)
          {
            var valuesKVT = Query_DeserializedVFS.Where(r => string.Equals(r.Key, var_name));
            Query_vDatasFiltered = Query_vDatasFiltered.Where(outer => !valuesKVT.Any(r => outer.rnode == r.rNode) || valuesKVT.Where(r => outer.rnode == r.rNode).Any(keyvalue_predicate));
            Query_rNodesFilteredNOvData = Query_rNodesFilteredNOvData.Where(outer => !valuesKVT.Any(r => outer == r.rNode) || valuesKVT.Where(r => outer == r.rNode).Any(keyvalue_predicate));
            Query_rNodesFilteredUnique = Query_rNodesFilteredUnique.Where(outer => !valuesKVT.Any(r => outer == r.rNode) || valuesKVT.Where(r => outer == r.rNode).Any(keyvalue_predicate));
            Query_rNodesFilteredJoin = Query_rNodesFilteredJoin.Where(outer => !valuesKVT.Any(r => outer == r.rNode) || valuesKVT.Where(r => outer == r.rNode).Any(keyvalue_predicate));
          }
          //
        }
        catch { }
      }

      //
      ScanVFS_WorkerProcessorPost();
      //
      // questo genera la lista di attributi attivi che viene costruita in due modi: verificare quello piu' efficiente da usare per il fetch effettivo on-demand
      Query_TagsActiveSetV1 = Query_rNodesFilteredJoin.Join(Query_PropsVFSNN, r => r, p => p.rnode, (r, p) => p.attributeId.Value).Distinct();
      Query_TagsActiveSetV2 = Query_rNodesFilteredUnique.Join(Query_PropsVFSNN, r => r, p => p.rnode, (r, p) => p.attributeId.Value).Distinct();
      Query_TagsActiveSetWithRNODES = Query_rNodesFilteredUnique.Join(Query_PropsVFSNN, r => r, p => p.rnode, (r, p) => new KeyValuePair<int, int>(r, p.attributeId.Value));
      //
      ScanVFS_WorkerProcessorFinalize();
      //
    }


    //
    // override these methos to further customize your queries
    //
    public virtual void ScanVFS_WorkerProcessorInit() { }
    //
    public virtual void ScanVFS_WorkerProcessorPre() { }
    //
    public virtual void ScanVFS_WorkerProcessorPost() { }
    //
    public virtual void ScanVFS_WorkerProcessorFinalize() { }
    //
    public virtual void ScanVFS_ScanPost() { }
    //


    protected virtual void ScanVFS_Sorter_Worker(string sortMode)
    {
      if (Utility.TryParse<bool>(IKGD_Config.AppSettings["SortingLinksEnabled"], true) == false)
        return;
      try
      {
        //
        ScanVFS_Sorter_CustomPre(sortMode);
        //
        if (!DataStorageCached.Processed)
        {
          //
          sortMode = sortMode ?? ArgsSet[ParameterNameSorter].NullIfEmpty() ?? string.Empty;
          string sortModeAux = sortMode.Trim(" -+".ToCharArray());
          //
          if (sortModeAux.StartsWith("KVT."))  // ascending/descending
          {
            //TODO: eseguire il fetch completo dei VDATA, deserializzare e processare l'elemento KVT come KVT con language. Quindi ritornare la lista di rNodes o sNodes
          }
          else if (sortModeAux.StartsWith("KVTs"))  // ascending/descending
          {
            //
            // sorter per le proprieta' KVT deserializzate
            string var_name = sortMode.Split(".".ToCharArray(), 2).Skip(1).FirstOrDefault();
            if (sortModeAux.StartsWith("KVTsI"))  // integer
            {
              var nodeSet =
                (from rnode in Query_rNodesFilteredNOvData
                 join vn in Query_vNodesVFS on rnode equals vn.rnode
                 join kv in Query_DeserializedVFS.Where(r => string.Equals(r.Key, var_name)) on rnode equals kv.rNode into keyvalues
                 select new { vNode = vn, valueMin = keyvalues.Min(r => r.ValueInt), valueMax = keyvalues.Max(r => r.ValueInt) });
              //
              if (FetchMode == FetchModeEnum.rNodeFetch)
                nodeSet = nodeSet.GroupBy(r => r.vNode.rnode).Select(g => g.First());
              var nodeSetOrdered = ((sortMode[0] != '-') ? nodeSet.OrderBy(vn => vn.valueMin) : nodeSet.OrderByDescending(vn => vn.valueMax)).ThenBy(vn => vn.vNode.name).ThenBy(vn => vn.vNode.position).ThenBy(vn => vn.vNode.rnode);
              if (FetchMode == FetchModeEnum.sNodeFetch)
              {
                NodePairs = nodeSetOrdered.ThenBy(g => g.vNode.snode).Select(g => new KeyValuePair<int, int>(g.vNode.rnode, g.vNode.snode)).ToList();
                DataStorageCached.sNodes = NodePairs.Select(p => p.Value).ToList();
              }
              else
                DataStorageCached.rNodes = nodeSetOrdered.Select(g => g.vNode.rnode).ToList();
            }
            else if (sortModeAux.StartsWith("KVTsF"))  // double
            {
              var nodeSet =
                (from rnode in Query_rNodesFilteredNOvData
                 join vn in Query_vNodesVFS on rnode equals vn.rnode
                 join kv in Query_DeserializedVFS.Where(r => string.Equals(r.Key, var_name)) on rnode equals kv.rNode into keyvalues
                 select new { vNode = vn, valueMin = keyvalues.Min(r => r.ValueDouble), valueMax = keyvalues.Max(r => r.ValueDouble) });
              //
              if (FetchMode == FetchModeEnum.rNodeFetch)
                nodeSet = nodeSet.GroupBy(r => r.vNode.rnode).Select(g => g.First());
              var nodeSetOrdered = ((sortMode[0] != '-') ? nodeSet.OrderBy(vn => vn.valueMin) : nodeSet.OrderByDescending(vn => vn.valueMax)).ThenBy(vn => vn.vNode.name).ThenBy(vn => vn.vNode.position).ThenBy(vn => vn.vNode.rnode);
              if (FetchMode == FetchModeEnum.sNodeFetch)
              {
                NodePairs = nodeSetOrdered.ThenBy(g => g.vNode.snode).Select(g => new KeyValuePair<int, int>(g.vNode.rnode, g.vNode.snode)).ToList();
                DataStorageCached.sNodes = NodePairs.Select(p => p.Value).ToList();
              }
              else
                DataStorageCached.rNodes = nodeSetOrdered.Select(g => g.vNode.rnode).ToList();
            }
            else if (sortModeAux.StartsWith("KVTsD"))  // datetime
            {
              var nodeSet =
                (from rnode in Query_rNodesFilteredNOvData
                 join vn in Query_vNodesVFS on rnode equals vn.rnode
                 join kv in Query_DeserializedVFS.Where(r => string.Equals(r.Key, var_name)) on rnode equals kv.rNode into keyvalues
                 select new { vNode = vn, valueMin = keyvalues.Min(r => r.ValueDate), valueMax = keyvalues.Max(r => r.ValueDate) });
              //
              if (FetchMode == FetchModeEnum.rNodeFetch)
                nodeSet = nodeSet.GroupBy(r => r.vNode.rnode).Select(g => g.First());
              var nodeSetOrdered = ((sortMode[0] != '-') ? nodeSet.OrderBy(vn => vn.valueMin) : nodeSet.OrderByDescending(vn => vn.valueMax)).ThenBy(vn => vn.vNode.name).ThenBy(vn => vn.vNode.position).ThenBy(vn => vn.vNode.rnode);
              if (FetchMode == FetchModeEnum.sNodeFetch)
              {
                NodePairs = nodeSetOrdered.ThenBy(g => g.vNode.snode).Select(g => new KeyValuePair<int, int>(g.vNode.rnode, g.vNode.snode)).ToList();
                DataStorageCached.sNodes = NodePairs.Select(p => p.Value).ToList();
              }
              else
                DataStorageCached.rNodes = nodeSetOrdered.Select(g => g.vNode.rnode).ToList();
            }
            else  // string
            {
              var nodeSet =
                (from rnode in Query_rNodesFilteredNOvData
                 join vn in Query_vNodesVFS on rnode equals vn.rnode
                 join kv in Query_DeserializedVFS.Where(r => string.Equals(r.Key, var_name)) on rnode equals kv.rNode into keyvalues
                 select new { vNode = vn, valueMin = keyvalues.Min(r => r.ValueString), valueMax = keyvalues.Max(r => r.ValueString) });
              //
              if (FetchMode == FetchModeEnum.rNodeFetch)
                nodeSet = nodeSet.GroupBy(r => r.vNode.rnode).Select(g => g.First());
              var nodeSetOrdered = ((sortMode[0] != '-') ? nodeSet.OrderBy(vn => vn.valueMin) : nodeSet.OrderByDescending(vn => vn.valueMax)).ThenBy(vn => vn.vNode.name).ThenBy(vn => vn.vNode.position).ThenBy(vn => vn.vNode.rnode);
              if (FetchMode == FetchModeEnum.sNodeFetch)
              {
                NodePairs = nodeSetOrdered.ThenBy(g => g.vNode.snode).Select(g => new KeyValuePair<int, int>(g.vNode.rnode, g.vNode.snode)).ToList();
                DataStorageCached.sNodes = NodePairs.Select(p => p.Value).ToList();
              }
              else
                DataStorageCached.rNodes = nodeSetOrdered.Select(g => g.vNode.rnode).ToList();
            }
            //
          }
          else if (sortMode.Contains("TagVal."))
          {
            // sorter per properties associate ad un valore
            string var_name = sortMode.Split(".".ToCharArray(), 2).Skip(1).FirstOrDefault();
            var nodeSet =
              (from rnode in Query_rNodesFilteredNOvData
               join vn in Query_vNodesVFS on rnode equals vn.rnode
               join pr in Query_PropsValuedVFS.Where(p => p.name == var_name) on rnode equals pr.rnode into props
               select new { vNode = vn, valueMin = props.Min(r => r.value), valueMax = props.Max(r => r.value) });
            //
            if (FetchMode == FetchModeEnum.rNodeFetch)
              nodeSet = nodeSet.GroupBy(r => r.vNode.rnode).Select(g => g.First());
            var nodeSetOrdered = ((sortMode[0] != '-') ? nodeSet.OrderBy(vn => vn.valueMin) : nodeSet.OrderByDescending(vn => vn.valueMax)).ThenBy(vn => vn.vNode.name).ThenBy(vn => vn.vNode.position).ThenBy(vn => vn.vNode.rnode);
            if (FetchMode == FetchModeEnum.sNodeFetch)
            {
              NodePairs = nodeSetOrdered.ThenBy(g => g.vNode.snode).Select(g => new KeyValuePair<int, int>(g.vNode.rnode, g.vNode.snode)).ToList();
              DataStorageCached.sNodes = NodePairs.Select(p => p.Value).ToList();
            }
            else
              DataStorageCached.rNodes = nodeSetOrdered.Select(g => g.vNode.rnode).ToList();
          }
          else if (sortMode.Contains("TagType."))
          {
            // sorter per categorie di tags, ordina per AttributeCode all'interno di una categoria di tags
            string tag_type = sortMode.Split(".".ToCharArray(), 2).Skip(1).FirstOrDefault();
            var nodeSet =
              (from rnode in Query_rNodesFilteredNOvData
               join vn in Query_vNodesVFS on rnode equals vn.rnode
               join pr in Query_PropsVFS.Where(p => p.IKCAT_Attribute != null && p.IKCAT_Attribute.AttributeType == tag_type) on rnode equals pr.rnode into props
               select new { vNode = vn, valueMin = props.Min(r => r.IKCAT_Attribute.AttributeCode), valueMax = props.Max(r => r.IKCAT_Attribute.AttributeCode) });
            //
            if (FetchMode == FetchModeEnum.rNodeFetch)
              nodeSet = nodeSet.GroupBy(r => r.vNode.rnode).Select(g => g.First());
            var nodeSetOrdered = ((sortMode[0] != '-') ? nodeSet.OrderBy(vn => vn.valueMin) : nodeSet.OrderByDescending(vn => vn.valueMax)).ThenBy(vn => vn.vNode.name).ThenBy(vn => vn.vNode.position).ThenBy(vn => vn.vNode.rnode);
            if (FetchMode == FetchModeEnum.sNodeFetch)
            {
              NodePairs = nodeSetOrdered.ThenBy(g => g.vNode.snode).Select(g => new KeyValuePair<int, int>(g.vNode.rnode, g.vNode.snode)).ToList();
              DataStorageCached.sNodes = NodePairs.Select(p => p.Value).ToList();
            }
            else
              DataStorageCached.rNodes = nodeSetOrdered.Select(g => g.vNode.rnode).ToList();
          }
          else if (sortMode.Contains("Name"))  // ascending/descending
          {
            var nodeSet = Query_rNodesFilteredNOvData.Join(Query_vNodesVFS, r => r, vn => vn.rnode, (r, vn) => vn);
            if (FetchMode == FetchModeEnum.rNodeFetch)
              nodeSet = nodeSet.GroupBy(r => r.rnode).Select(g => g.First());
            var nodeSetOrdered = ((sortMode[0] != '-') ? nodeSet.OrderBy(vn => vn.name) : nodeSet.OrderByDescending(vn => vn.name)).ThenBy(vn => vn.folder).ThenBy(vn => vn.position).ThenBy(vn => vn.rnode);
            if (FetchMode == FetchModeEnum.sNodeFetch)
            {
              NodePairs = nodeSetOrdered.ThenBy(g => g.snode).Select(g => new KeyValuePair<int, int>(g.rnode, g.snode)).ToList();
              DataStorageCached.sNodes = NodePairs.Select(p => p.Value).ToList();
            }
            else
              DataStorageCached.rNodes = nodeSetOrdered.Select(g => g.rnode).ToList();
          }
          else if (sortMode.Contains("Position"))  // ascending/descending
          {
            var nodeSet = Query_rNodesFilteredNOvData.Join(Query_vNodesVFS, r => r, vn => vn.rnode, (r, vn) => vn);
            if (FetchMode == FetchModeEnum.rNodeFetch)
              nodeSet = nodeSet.GroupBy(r => r.rnode).Select(g => g.First());
            var nodeSetOrdered = ((sortMode[0] != '-') ? nodeSet.OrderBy(vn => vn.position).ThenBy(vn => vn.name) : nodeSet.OrderByDescending(vn => vn.position).ThenByDescending(vn => vn.name)).ThenBy(vn => vn.rnode);
            if (FetchMode == FetchModeEnum.sNodeFetch)
            {
              NodePairs = nodeSetOrdered.ThenBy(g => g.snode).Select(g => new KeyValuePair<int, int>(g.rnode, g.snode)).ToList();
              DataStorageCached.sNodes = NodePairs.Select(p => p.Value).ToList();
            }
            else
              DataStorageCached.rNodes = nodeSetOrdered.Select(g => g.rnode).ToList();
          }
          else if (sortMode.Contains("Viewed"))  // solo descending
          {
            // solo per riferimento se si volesse usare IKG_HITLOG
            //var nodeSet =
            //  (from rnode in Query_rNodesFilteredNOvData
            //   join vn in Query_vNodesVFS on rnode equals vn.rnode
            //   join hit in fsOp.DB.IKG_HITLOGs on rnode equals hit.resID into hits  // attenzione alla gestione degli hits mancanti!
            //   select new { vNode = vn, count = hits.Count() });
            //
            // implementazione precedente con IKG_HITACC
            //var nodeSet =
            //  (from rnode in Query_rNodesFilteredNOvData
            //   join vn in Query_vNodesVFS on rnode equals vn.rnode
            //   join hit in fsOp.DB.IKG_HITACCs.Where(r => r.Category == 0) on rnode equals hit.rNode into hits  // attenzione alla gestione degli hits mancanti!
            //   select new { vNode = vn, count = hits.Max(r => r.Hits).GetValueOrDefault() });
            //
            // TODO: versione senza il join into che forse si ottimizza meglio (non ancora testata)
            var nodeSet = Query_rNodesFilteredNOvData.Join(Query_vNodesVFS, r => r, vn => vn.rnode, (r, vn) => vn).Select(vn => new { vNode = vn, count = fsOp.DB.IKG_HITACCs.Where(r => r.Category == 0 && r.rNode == vn.rnode).Max(r => r.Hits).GetValueOrDefault() });
            //
            if (FetchMode == FetchModeEnum.rNodeFetch)
              nodeSet = nodeSet.GroupBy(r => r.vNode.rnode).Select(g => g.First());
            var nodeSetOrdered = nodeSet.OrderByDescending(g => g.count).ThenBy(g => g.vNode.name).ThenBy(g => g.vNode.folder).ThenBy(g => g.vNode.position).ThenBy(g => g.vNode.rnode);
            if (FetchMode == FetchModeEnum.sNodeFetch)
            {
              NodePairs = nodeSetOrdered.ThenBy(g => g.vNode.snode).Select(g => new KeyValuePair<int, int>(g.vNode.rnode, g.vNode.snode)).ToList();
              DataStorageCached.sNodes = NodePairs.Select(p => p.Value).ToList();
            }
            else
              DataStorageCached.rNodes = nodeSetOrdered.Select(g => g.vNode.rnode).ToList();
          }
          else if (sortMode.Contains("Voted"))  // solo descending
          {
            //var nodeSet =
            //  (from rnode in Query_rNodesFilteredNOvData
            //   join vn in Query_vNodesVFS on rnode equals vn.rnode
            //   join hit in fsOp.DB.LazyLogin_Votes.Where(v => v.Category == 0) on rnode equals hit.rNode into hits
            //   select new { vNode = vn, count = hits.Count() });
            var nodeSet =
              (from rnode in Query_rNodesFilteredNOvData
               join vn in Query_vNodesVFS on rnode equals vn.rnode
               join hit in fsOp.DB.IKG_HITACCs.Where(r => r.Category == 1) on rnode equals hit.rNode into hits  // attenzione alla gestione degli hits mancanti!
               select new { vNode = vn, count = hits.Max(r => r.Hits).GetValueOrDefault() });
            //
            if (FetchMode == FetchModeEnum.rNodeFetch)
              nodeSet = nodeSet.GroupBy(r => r.vNode.rnode).Select(g => g.First());
            var nodeSetOrdered = nodeSet.OrderByDescending(g => g.count).ThenBy(g => g.vNode.name).ThenBy(g => g.vNode.folder).ThenBy(g => g.vNode.position).ThenBy(g => g.vNode.rnode);
            if (FetchMode == FetchModeEnum.sNodeFetch)
            {
              NodePairs = nodeSetOrdered.ThenBy(g => g.vNode.snode).Select(g => new KeyValuePair<int, int>(g.vNode.rnode, g.vNode.snode)).ToList();
              DataStorageCached.sNodes = NodePairs.Select(p => p.Value).ToList();
            }
            else
              DataStorageCached.rNodes = nodeSetOrdered.Select(g => g.vNode.rnode).ToList();
          }
          else if (sortMode.Contains("Date"))  // ascending/descending
          {
            var nodeSet = Query_vDatasFiltered.Join(Query_vNodesVFS, vd => vd.rnode, vn => vn.rnode, (vd, vn) => new { vNode = vn, vData = vd });
            if (FetchMode == FetchModeEnum.rNodeFetch)
              nodeSet = nodeSet.GroupBy(r => r.vData.rnode).Select(g => g.First());
            var nodeSetOrdered = ((sortMode[0] != '-') ? nodeSet.OrderBy(g => g.vData.date_node).ThenBy(g => g.vData.date_node_aux ?? g.vData.date_node) : nodeSet.OrderByDescending(g => g.vData.date_node).ThenByDescending(g => g.vData.date_node_aux ?? g.vData.date_node)).ThenBy(g => g.vNode.folder).ThenBy(g => g.vNode.position).ThenBy(g => g.vNode.rnode);
            if (FetchMode == FetchModeEnum.sNodeFetch)
            {
              NodePairs = nodeSetOrdered.ThenBy(g => g.vNode.snode).Select(g => new KeyValuePair<int, int>(g.vNode.rnode, g.vNode.snode)).ToList();
              DataStorageCached.sNodes = NodePairs.Select(p => p.Value).ToList();
            }
            else
              DataStorageCached.rNodes = nodeSetOrdered.Select(g => g.vNode.rnode).ToList();
          }
          else if (sortMode.Contains("Creat"))  // ascending/descending (non usiamo la vera data di creazione ma direttamente il valore di rnode)
          {
            var nodeSet = Query_vDatasFiltered.Join(Query_vNodesVFS, vd => vd.rnode, vn => vn.rnode, (vd, vn) => new { vNode = vn, vData = vd });
            if (FetchMode == FetchModeEnum.rNodeFetch)
              nodeSet = nodeSet.GroupBy(r => r.vData.rnode).Select(g => g.First());
            var nodeSetOrdered = ((sortMode[0] != '-') ? nodeSet.OrderBy(g => g.vData.rnode) : nodeSet.OrderByDescending(g => g.vData.rnode)).ThenBy(g => g.vNode.folder).ThenBy(g => g.vNode.position);
            if (FetchMode == FetchModeEnum.sNodeFetch)
            {
              NodePairs = nodeSetOrdered.ThenBy(g => g.vNode.snode).Select(g => new KeyValuePair<int, int>(g.vNode.rnode, g.vNode.snode)).ToList();
              DataStorageCached.sNodes = NodePairs.Select(p => p.Value).ToList();
            }
            else
              DataStorageCached.rNodes = nodeSetOrdered.Select(g => g.vNode.rnode).ToList();
          }
        }
        //
        ScanVFS_Sorter_Custom(sortMode);
        //
        DataStorageCached.Processed = ((FetchMode == FetchModeEnum.sNodeFetch) ? DataStorageCached.sNodes : DataStorageCached.rNodes) != null;
        DataStorageCached.SortMode = sortMode;
        //
      }
      catch { }
    }


    protected virtual void ScanVFS_Sorter_CustomPre(string sortMode) { }


    //
    // metodo per supportare modalita' di sort customizzate
    //
    protected virtual void ScanVFS_Sorter_Custom(string sortMode)
    {
      //
      // esempio di sort per prezzo
      //
      //string sortModeAux = sortMode.Trim(" -+".ToCharArray());
      //if (sortModeAux == "Price")  // ascending/descending
      //{
      //  var nodeSet = Query_vDatasFiltered.Join(Query_vNodesVFS, vd => vd.rnode, vn => vn.rnode, (vd, vn) => new { vNode = vn, vData = vd });
      //  if (FetchMode == FetchModeEnum.rNodeFetch)
      //    nodeSet = nodeSet.GroupBy(r => r.vData.rnode).Select(g => g.First());
      //  //
      //  var nodeSetOrdered = ((sortMode[0] != '-') ? nodeSet.OrderBy(g => g.vData.geoRangeM) : nodeSet.OrderByDescending(g => g.vData.geoRangeM)).ThenBy(g => g.vNode.folder).ThenBy(g => g.vNode.position).ThenBy(g => g.vNode.rnode);
      //  //
      //  if (FetchMode == FetchModeEnum.sNodeFetch)
      //  {
      //    NodePairs = nodeSetOrdered.ThenBy(g => g.vNode.snode).Select(g => new KeyValuePair<int, int>(g.vNode.rnode, g.vNode.snode)).ToList();
      //    DataStorageCached.sNodes = NodePairs.Select(p => p.Value).ToList();
      //  }
      //  else
      //    DataStorageCached.rNodes = nodeSetOrdered.Select(g => g.vNode.rnode).ToList();
      //}
    }


    protected virtual void ScanVFS_Sorter_Finalize(int? maxNodes)
    {
      // controllo se e' gia' stato effettuato il sort
      DataStorageCached.Processed = ((FetchMode == FetchModeEnum.sNodeFetch) ? DataStorageCached.sNodes : DataStorageCached.rNodes) != null;
      if (DataStorageCached.Processed)
        return;
      try
      {
        //
        // in mancanza di sort specificati utilizziamo il nome della risorsa, quindi rNode, quindi sNode
        //
        var nodeSet = Query_rNodesFilteredNOvData.Join(Query_vNodesVFS, r => r, vn => vn.rnode, (r, vn) => vn);
        if (FetchMode == FetchModeEnum.rNodeFetch)
          nodeSet = nodeSet.GroupBy(r => r.rnode).Select(g => g.First());
        var nodeSetOrdered = nodeSet.OrderBy(vn => vn.name).ThenBy(vn => vn.folder).ThenBy(vn => vn.position).ThenBy(vn => vn.rnode);
        if (FetchMode == FetchModeEnum.sNodeFetch)
        {
          if (maxNodes != null)
            DataStorageCached.sNodes = nodeSetOrdered.ThenBy(vn => vn.snode).Select(vn => vn.snode).Take(maxNodes.Value).ToList();
          else
            DataStorageCached.sNodes = nodeSetOrdered.ThenBy(vn => vn.snode).Select(vn => vn.snode).ToList();
          //
          //DataStorageCached.sNodes = nodeSetOrdered.ThenBy(vn => vn.snode).Select(vn => vn.snode).Distinct().ToList();
        }
        else
        {
          if (maxNodes != null)
            DataStorageCached.rNodes = nodeSetOrdered.Select(vn => vn.rnode).Take(maxNodes.Value).ToList();
          else
            DataStorageCached.rNodes = nodeSetOrdered.Select(vn => vn.rnode).ToList();
        }
        DataStorageCached.SortMode = "+Name";
      }
      catch { }
      //
      DataStorageCached.Processed = (((FetchMode == FetchModeEnum.sNodeFetch) ? DataStorageCached.sNodes : DataStorageCached.rNodes) != null);
      //
    }


    protected virtual List<int> ScanVFS_PathFilter(List<int> nodes_src)
    {
      bool updateDataStorage = nodes_src == null;
      List<int> result = nodes_src ?? ((FetchMode == FetchModeEnum.sNodeFetch) ? DataStorageCached.sNodes : DataStorageCached.rNodes);
      //
      if (FilterNodesByValidPathRequired == false || FilterNodesByValidPath.GetValueOrDefault(Utility.TryParse<bool>(IKGD_Config.AppSettings["ManagerTagFilter_ValidatePath"], false)) == false)
        return result;
      if (DataStorageCached.FilteredByPath)
        return result;
      //DateTime t0 = DateTime.Now;
      if (FetchMode == FetchModeEnum.sNodeFetch)
      {
        try
        {
          result = DataStorageCached.sNodes.Join(fsOp.PathsFromNodesExt(result, null, false, false).FilterPathsByRootsCMS(), n => n, p => p.sNode, (n, p) => n).ToList();
          if (updateDataStorage)
            DataStorageCached.sNodes = result;
        }
        catch { }
      }
      else
      {
        //IKCMS_ExecutionProfiler.AddMessage("ScanVFS_PathFilter: nodes before={0}".FormatString(DataStorageCached.rNodes.Count));
        try
        {
          result = DataStorageCached.rNodes.Join(fsOp.PathsFromNodesExt(null, result, false, false).FilterPathsByRootsCMS(), n => n, p => p.rNode, (n, p) => n).ToList();
          if (updateDataStorage)
            DataStorageCached.rNodes = result;
        }
        catch { }
        //IKCMS_ExecutionProfiler.AddMessage("ScanVFS_PathFilter: nodes after={0}".FormatString(DataStorageCached.rNodes.Count));
      }
      if (updateDataStorage)
      {
        DataStorageCached.FilteredByPath = true;
      }
      //DateTime t1 = DateTime.Now;
      //double dt = (t1 - t0).TotalMilliseconds;
      return result;
    }


    protected virtual void ScanVFS_Lucene_Worker(string luceneQuery)
    {
      luceneQuery = (luceneQuery ?? ArgsSet[ParameterNameLucene].NullIfEmpty() ?? string.Empty).Trim();
      if (string.IsNullOrEmpty(luceneQuery))
        return;
      //
      DefaultCachingTimeData = Math.Min(DefaultCachingTimeMain / 10, DefaultCachingTimeData);
      //
      Func<List<TupleW<int, int>>> worker = () =>
      {
        using (Ikon.Indexer.LuceneIndexer indexer = new Ikon.Indexer.LuceneIndexer(false))
        {
          return indexer.IKGD_SearchLuceneRaw(luceneQuery, null, null, IKGD_Language_Provider.Provider.Language, null, null, null, -1).Select(d => new TupleW<int, int>(d.folder, d.rNode)).Distinct().ToList();
        }
      };
      //
      List<TupleW<int, int>> nodesLucene = null;
      //
      try
      {
        if (Utility.TryParse<bool>(IKGD_Config.AppSettingsWeb["CachingIKCMS_LuceneEnabled"], true))
        {
          List<object> frags = new List<object>();
          //frags.Add(this.GetType().Name);
          frags.Add(this.GetType().Name);
          frags.Add(luceneQuery);
          //frags.AddRange(AllowedTypeNames.OfType<object>());
          frags.Add(FetchMode);
          frags.Add(FS_OperationsHelpers.VersionFrozenSession);
          string cacheKey = FS_OperationsHelpers.ContextHashNN(frags.ToArray());
          nodesLucene = FS_OperationsHelpers.CachedEntityWrapper(cacheKey, worker, DefaultCachingTimeLucene, FS_OperationsHelpers.Const_CacheDependencyIKGD_vNode_vData_iNode);
        }
        else
        {
          nodesLucene = worker();
        }
      }
      catch { }
      //
      if (nodesLucene != null)
      {
        var nodesVFS = (FetchMode == FetchModeEnum.sNodeFetch) ? DataStorageCached.sNodes : DataStorageCached.rNodes;
        if (nodesVFS == null)
        {
          if (FetchMode == FetchModeEnum.sNodeFetch)
          {
            NodePairs = Query_rNodesFilteredNOvData.Join(Query_vNodesVFS, r => r, vn => vn.rnode, (r, vn) => vn).OrderBy(vn => vn.name).ThenBy(vn => vn.folder).ThenBy(vn => vn.position).ThenBy(vn => vn.rnode).ThenBy(vn => vn.snode).Select(g => new KeyValuePair<int, int>(g.rnode, g.snode)).ToList();
          }
          else
          {
            nodesVFS = Query_rNodesFilteredUnique.ToList();
          }
        }
        //
        if (FetchMode == FetchModeEnum.sNodeFetch)
        {
          //DataStorageCached.sNodes = nodesLucene.Join(NodePairs, r => r, p => p.Key, (r, p) => p.Value).ToList();
          DataStorageCached.rNodes =
            (from nodeLucene in nodesLucene
             from node in NodePairs
             where (node.Key == nodeLucene.Item1 || node.Key == nodeLucene.Item2)
             select node.Value).Distinct().ToList();
        }
        else
        {
          //DataStorageCached.rNodes = nodesLucene.Intersect(nodesVFS).ToList();
          DataStorageCached.rNodes =
            (from nodeLucene in nodesLucene
             from rNode in nodesVFS
             where (rNode == nodeLucene.Item1 || rNode == nodeLucene.Item2)
             select rNode).Distinct().ToList();
        }
        //
        DataStorageCached.FilteredByLucene = true;
        //
      }
      //
      DataStorageCached.Processed |= ((FetchMode == FetchModeEnum.sNodeFetch) ? DataStorageCached.sNodes : DataStorageCached.rNodes) != null;
      //
    }


    public virtual string GetNoPagerUrl(string url)
    {
      if (url.IsNullOrEmpty())
      {
        url = HttpContext.Current.Request.Url.ToString();
      }
      if (url.IsNullOrEmpty())
      {
        return url;
      }
      url = Utility.UriDelQueryVars(url, ParameterNamePager);
      return url;
    }


    public virtual string GetStrippedTagsUrl(string url, params object[] values)
    {
      if (url.IsNullOrEmpty())
      {
        url = HttpContext.Current.Request.Url.ToString();
      }
      if (url.IsNullOrEmpty() || values == null || !values.Any())
      {
        return url;
      }
      foreach (object val in values)
      {
        url = Utility.UriDelQueryVar(url, ParameterNameFilter, val.ToString());
      }
      return url;
    }


    //
    // examples:
    // GetSortingUrl("+Name", true)
    // GetSortingUrl("-Date", true)
    // GetSortingUrl("Viewed", false)  --> sorting senza switch di direzione
    //
    public virtual string GetSortingUrl(string sortingVarValueWithDefault, bool? allowDirectionSwitch) { return GetSortingUrl(sortingVarValueWithDefault, allowDirectionSwitch, null, null); }
    public virtual string GetSortingUrl(string sortingVarValueWithDefault, bool? allowDirectionSwitch, bool? resetPagination, bool? encodeAsAttribute)
    {
      string url = "javascript:;";
      try
      {
        if (Utility.TryParse<bool>(IKGD_Config.AppSettings["SortingLinksEnabled"], true) == false)
          return url;
        string sortingVarValue = sortingVarValueWithDefault.TrimSafe(' ', '+', '-');
        string currentValue = null;
        //for (int i = ArgsSet.Count - 1; i >= 0; i--)
        //{
        //  if (ArgsSet.Keys[i] == ParameterNameSorter && ArgsSet[i].TrimSafe(' ', '+', '-') == sortingVarValue)
        //  {
        //    currentValue = ArgsSet[i];
        //    break;
        //  }
        //}
        if (DataStorageCached != null)
          currentValue = DataStorageCached.SortMode;
        bool isActive = currentValue.TrimSafe(' ', '+', '-') == sortingVarValue;
        string nextValue = null;
        if (isActive && allowDirectionSwitch.GetValueOrDefault(true))
        {
          int? nextDirection = currentValue.StartsWith("-") ? +1 : -1;  // attenzione che il + nelle query string viene convertito in spazio
          nextValue = (nextDirection < 0 ? "-" : "+") + sortingVarValue;
        }
        else
        {
          nextValue = sortingVarValueWithDefault;
        }
        if (nextValue == currentValue)
          return url;
        //
        //url = HttpContext.Current.Request.Url.ToString();  // attenzione ritorna la url dopo l'applicazione dell'urlrewrite
        url = HttpContext.Current.Request.RawUrl;
        //
        if (resetPagination.GetValueOrDefault(true))
          url = Utility.UriDelQueryVars(url, ParameterNamePager);
        url = Utility.UriSetQuery(url, ParameterNameSorter, nextValue);
        if (encodeAsAttribute.GetValueOrDefault(true))
          url = url.EncodeAsAttribute();
      }
      catch { }
      return url;
    }


    //
    // examples:
    // GetSortingCssClass("+Name", "active", "asc", "desc")
    // GetSortingCssClass("-Date", "active", "asc", "desc")
    // GetSortingCssClass("Viewed", "active", "desc", "desc")
    //
    public virtual string GetSortingCssClass(string sortingVarValueWithDefault, string cssClassActive, string cssClassASC, string cssClassDESC)
    {
      List<string> cssFrags = new List<string>();
      try
      {
        string sortingVarValue = sortingVarValueWithDefault.TrimSafe(' ', '+', '-');
        string currentValue = null;
        if (DataStorageCached != null)
          currentValue = DataStorageCached.SortMode;
        bool isActive = currentValue.TrimSafe(' ', '+', '-') == sortingVarValue;
        if (isActive)
        {
          cssFrags.Add(cssClassActive);
          currentValue = currentValue.NullIfEmpty() ?? sortingVarValueWithDefault ?? string.Empty;
        }
        else
        {
          currentValue = sortingVarValueWithDefault ?? string.Empty;
        }
        int? currentDirection = currentValue.StartsWith("-") ? -1 : +1;  // attenzione che il + nelle query string viene convertito in spazio
        if (currentDirection != null)
          cssFrags.Add((currentDirection < 0) ? cssClassDESC : cssClassASC);
      }
      catch { }
      bool useSpace = !cssFrags.Any(f => f.IsNotEmpty() && (f.StartsWith("_") || f.EndsWith("_")));
      return Utility.Implode(cssFrags, (useSpace ? " " : string.Empty), null, useSpace, true);
    }


    public virtual List<SelectListItem> GetActiveFilterSubset(string AttributeType, string language) { return GetActiveFilterSubset(AttributeType, language, false); }
    public virtual List<SelectListItem> GetActiveFilterSubset(string AttributeType, string language, bool includeCurrentFilters)
    {
      return IKCAT_AttributeStorage.GetTags(includeCurrentFilters ? TagsAll.Concat(TagsActiveSet).Distinct() : TagsActiveSet).Where(a => string.Equals(a.AttributeType, AttributeType)).Select(a => new SelectListItem() { Value = a.AttributeId.ToString(), Text = a.Labels.KeyFilterTry(language).ValueString ?? a.Labels.ValueString, Selected = TagsAll.Contains(a.AttributeId) }).ToList();
    }


    public virtual List<TupleW<string, string, string, List<SelectListItem>>> GetActiveFilterSets(string language, bool hideModelTags, params string[] hiddenAttributeTypes)
    {
      List<TupleW<string, string, string, List<SelectListItem>>> results = new List<TupleW<string, string, string, List<SelectListItem>>>();
      try
      {
        List<int> modelIds = (hideModelTags && this.Model != null && this.Model.Properties != null) ? this.Model.Properties.Where(p => p.name == IKGD_Constants.IKCAT_TagPropertyName && p.attributeId != null).Select(p => p.attributeId.Value).Distinct().ToList() : new List<int>();
        //var data = AttributesActive.GroupBy(a => a.AttributeType).ToList().Where(g => !hiddenAttributeTypes.Contains(g.Key) && !(hideModelTags && g.Any(a => modelIds.Contains(a.AttributeId)))).OrderBy(g => g.Key);
        var data = AttributesActive.GroupBy(a => a.AttributeType).ToList().Where(g => !hiddenAttributeTypes.Contains(g.Key) && !(hideModelTags && g.Any(a => modelIds.Contains(a.AttributeId)))).OrderBy(g => IKCAT_AttributeStorage.GetTagCategory(null, g.Key), IKCAT_AttributeStorage.Comparer);
        var activeTypes = data.Select(g => g.Key).Distinct().ToList();
        //var labelTags = fsOp.DB.IKCAT_Attributes.Where(a => activeTypes.Contains(a.AttributeType) && a.AttributeCode == null).ToList();
        var labelTags = AttributesActive.Select(a => a.AttributeType).Distinct().Select(a => IKCAT_AttributeStorage.GetTagCategory(null, a)).ToList();
        foreach (var grp in data)
        {
          var lbl = labelTags.FirstOrDefault(a => a.AttributeType == grp.Key) ?? grp.OrderBy(a => a.AttributeCode).FirstOrDefault();
          var sli = grp.Where(a => a.AttributeCode != null).OrderBy(a => a.AttributeCode).Select(a => new SelectListItem() { Value = a.AttributeId.ToString(), Text = a.Labels.KeyFilterTry(language).ValueString ?? a.Labels.ValueString, Selected = TagsAll.Contains(a.AttributeId) }).OrderBy(s => s.Text).ToList();
          string removeTagCode = sli.Any(r => r.Selected) ? ParameterNegationPrefix + sli.FirstOrDefault(r => r.Selected).Value : null;
          var item = new TupleW<string, string, string, List<SelectListItem>>(grp.Key, lbl.Labels.KeyFilterTry(language).ValueString ?? lbl.Labels.ValueString, removeTagCode, sli);
          results.Add(item);
        }
      }
      catch { }
      return results;
    }


    //
    // E' possibile specificare una lista di categorie in EnsureTagCategories
    // per fare in modo di avere un result set vuoto nel caso non ci siano attributi mappati nel set
    // per non far sparire le DDL di ricerca
    //
    public virtual List<TupleW<string, string, string, List<SelectListItem>>> GetActiveFilterSetsWithForcedCategories(string language, bool hideModelTags, IEnumerable<string> EnsureTagCategories, params string[] hiddenAttributeTypes)
    {
      List<TupleW<string, string, string, List<SelectListItem>>> results = new List<TupleW<string, string, string, List<SelectListItem>>>();
      try
      {
        List<int> modelIds = (hideModelTags && this.Model != null && this.Model.Properties != null) ? this.Model.Properties.Where(p => p.name == IKGD_Constants.IKCAT_TagPropertyName && p.attributeId != null).Select(p => p.attributeId.Value).Distinct().ToList() : new List<int>();
        //var data = AttributesActive.GroupBy(a => a.AttributeType).ToList().Where(g => !hiddenAttributeTypes.Contains(g.Key) && !(hideModelTags && g.Any(a => modelIds.Contains(a.AttributeId)))).OrderBy(g => g.Key);
        var data = AttributesActive.GroupBy(a => a.AttributeType).ToList().Where(g => !hiddenAttributeTypes.Contains(g.Key) && !(hideModelTags && g.Any(a => modelIds.Contains(a.AttributeId)))).OrderBy(g => IKCAT_AttributeStorage.GetTagCategory(null, g.Key), IKCAT_AttributeStorage.Comparer);
        var activeTypes = data.Select(g => g.Key).Distinct().ToList();
        //var labelTags = fsOp.DB.IKCAT_Attributes.Where(a => activeTypes.Contains(a.AttributeType) && a.AttributeCode == null).ToList();
        var labelTags = AttributesActive.Select(a => a.AttributeType).Distinct().Select(a => IKCAT_AttributeStorage.GetTagCategory(null, a)).ToList();
        foreach (var grp in data)
        {
          var lbl = labelTags.FirstOrDefault(a => a.AttributeType == grp.Key) ?? grp.OrderBy(a => a.AttributeCode).FirstOrDefault();
          var sli = grp.Where(a => a.AttributeCode != null).OrderBy(a => a.AttributeCode).Select(a => new SelectListItem() { Value = a.AttributeId.ToString(), Text = a.Labels.KeyFilterTry(language).ValueString ?? a.Labels.ValueString, Selected = TagsAll.Contains(a.AttributeId) }).OrderBy(s => s.Text).ToList();
          string removeTagCode = sli.Any(r => r.Selected) ? ParameterNegationPrefix + sli.FirstOrDefault(r => r.Selected).Value : null;
          var item = new TupleW<string, string, string, List<SelectListItem>>(grp.Key, lbl.Labels.KeyFilterTry(language).ValueString ?? lbl.Labels.ValueString, removeTagCode, sli);
          results.Add(item);
        }
        var missingCategories = EnsureTagCategories.Where(a => !labelTags.Any(t => string.Equals(t.AttributeType, a, StringComparison.OrdinalIgnoreCase))).ToList();
        foreach (var attributeType in missingCategories)
        {
          var lbl = IKCAT_AttributeStorage.GetTagCategory(null, attributeType);
          var item = new TupleW<string, string, string, List<SelectListItem>>(attributeType, lbl.Labels.KeyFilterTry(language).ValueString ?? lbl.Labels.ValueString, ParameterNegationPrefix + "1", new List<SelectListItem>());
          results.Add(item);
        }
      }
      catch { }
      return results;
    }


    public virtual TupleW<string, string, List<SelectListItem>> GetAllFilterForCategory(string AttributeType, string language, params string[] hiddenAttributeTypes)
    {
      var lbl = IKCAT_AttributeStorage.GetTagCategory(null, AttributeType);
      var sli = IKCAT_AttributeStorage.Attributes.Where(a => a.AttributeCode != null && a.AttributeType == AttributeType).OrderBy(a => a.AttributeCode).Select(a => new SelectListItem() { Value = a.AttributeId.ToString(), Text = a.Labels.KeyFilterTry(language).ValueString ?? a.Labels.ValueString, Selected = TagsAll.Contains(a.AttributeId) }).OrderBy(s => s.Text).ToList();
      string removeTagCode = sli.Any(r => r.Selected) ? ParameterNegationPrefix + sli.FirstOrDefault(r => r.Selected).Value : null;
      return new TupleW<string, string, List<SelectListItem>>(lbl != null ? (lbl.Labels.KeyFilterTry(language).ValueString ?? lbl.Labels.ValueString) : string.Empty, removeTagCode, sli);
    }



    protected bool? _IsAdministrator;
    public virtual bool IsAdministrator
    {
      get
      {
        if (_IsAdministrator == null)
        {
          _IsAdministrator = false;
          if (HttpContext.Current.User.Identity.IsAuthenticated && Model != null)
            _IsAdministrator = ((int)(FS_ACL_Reduced.GetUserACLData(fsOp.DB)[Model.Area]) >= (int)FS_ACL_Reduced.AclType.Write);
        }
        return _IsAdministrator.GetValueOrDefault(false);
      }
    }


    //NB: e' possibile passare il model anche direttamente in viewData
    public virtual IKCMS_ModelCMS_Interface GetItemModel(ViewDataDictionary viewData, int? sNode, int? rNode, bool? getHits, bool? getVotes)
    {
      try
      {
        //
        viewData.Model = viewData.Model ?? ((rNode != null) ? IKCMS_ModelCMS_Provider.Provider.ModelBuildGenericByRNODE(rNode.Value) : IKCMS_ModelCMS_Provider.Provider.ModelBuildGeneric(sNode.Value));
        IKCMS_ModelCMS_Interface mdl = viewData.Model as IKCMS_ModelCMS_Interface;
        //
        if (getHits == true)
        {
          //int pageHits = fsOp.DB.IKG_HITLOGs.Count(h => h.resID == mdl.rNode && h.action == (int)IKCMS_HitLogger.IKCMS_HitLogActionSubCodeEnum.PageCMS);
          int pageHits = fsOp.DB.IKG_HITACCs.Where(r => r.rNode == mdl.rNode && r.Category == 0).Max(r => r.Hits).GetValueOrDefault();
          viewData["pageHits"] = pageHits;
        }
        if (getVotes == true)
        {
          //int votesCount = fsOp.DB.LazyLogin_Votes.Count(r => r.rNode == mdl.rNode && r.Value > 0 && r.Category == 0);
          int votesCount = fsOp.DB.IKG_HITACCs.Where(r => r.rNode == mdl.rNode && r.Category == 1).Max(r => r.Hits).GetValueOrDefault();
          viewData["votesCount"] = votesCount;
          //
          //bool alreadyVoted = false;
          //try { alreadyVoted = fsOp.DB.LazyLogin_Votes.Where(r => r.IdLL == MembershipHelper.LazyLoginMapperObject.Id).Any(r => r.rNode == mdl.rNode && r.Value > 0 && r.Category == 0); }
          //catch { }
          //viewData["alreadyVoted"] = alreadyVoted;
          //
        }
      }
      catch { }
      return viewData.Model as IKCMS_ModelCMS_Interface;
    }


    public XElement DumpStatsXml()
    {
      UseReadOnlyFsOp = false;
      XElement xDump = new XElement("records");
      var fsNodesItemsAux = fsNodesAll(null).OrderByDescending(r => r.vData.IKGD_RNODE.IKG_HITACCs.Where(h => h.Category == 1).Max(h => h.Hits).GetValueOrDefault()).ThenBy(r => r.vData.IKGD_RNODE.IKG_HITACCs.Where(h => h.Category == 0).Max(h => h.Hits).GetValueOrDefault()).ThenByDescending(r => r.vNode.rnode).Distinct((n1, n2) => n1.rNode == n2.rNode);
      //
      IKCMS_ModelCMS_ModelInfo_Interface itemModelInfo = null;
      foreach (var fsNode in fsNodesItemsAux)
      {
        try
        {
          itemModelInfo = IKCMS_ModelCMS_Provider.Provider.FindBestModelMatch(Utility.FindTypeCached(fsNode.vData.manager_type));
          var mdl = IKCMS_ModelCMS_Provider.Provider.ModelBuild(null, fsNode, itemModelInfo) as IKCMS_ModelCMS_WidgetCMS_ImageCMS;
          XElement xImage = new XElement("image");
          //
          xImage.Add(new XElement("codeCMS", mdl.sNode));
          xImage.Add(new XElement("nameCMS", mdl.Name));
          //xImage.Add(new XElement("pathCMS", (mdl as IKCMS_ModelCMS_VFS_Interface).PathVFS.ToString()));
          xImage.Add(new XElement("Title", mdl.VFS_Resource.ResourceSettings.Title));
          xImage.Add(new XElement("Text", mdl.VFS_Resource.ResourceSettings.Text));
          xImage.Add(new XElement("Alt", mdl.VFS_Resource.ResourceSettings.Alt));
          xImage.Add(new XElement("Author", mdl.VFS_Resource.ResourceSettings.Author));
          xImage.Add(new XElement("Votes", fsNode.vData.IKGD_RNODE.IKG_HITACCs.Where(r => r.Category == 1).Max(r => r.Hits).GetValueOrDefault()));
          xImage.Add(new XElement("Views", fsNode.vData.IKGD_RNODE.IKG_HITACCs.Where(r => r.Category == 0).Max(r => r.Hits).GetValueOrDefault()));
          //
          xDump.Add(xImage);
        }
        catch { }
      }
      //
      return xDump;
    }

    //
    // utilizzata per compatibilità vecchi siti (dove manca la IKG_HITACC nel DB)
    public XElement DumpStatsXmlLazyLogin()
    {
      UseReadOnlyFsOp = false;
      XElement xDump = new XElement("records");
      var fsNodesItemsAux = fsNodesAll(null).OrderByDescending(r => r.vData.IKGD_RNODE.LazyLogin_Votes.Count(v => v.Category == 0)).ThenBy(r => r.vData.IKGD_RNODE.IKG_HITLOGs.Count).ThenByDescending(r => r.vNode.rnode).Distinct((n1, n2) => n1.rNode == n2.rNode);
      //
      IKCMS_ModelCMS_ModelInfo_Interface itemModelInfo = null;
      foreach (var fsNode in fsNodesItemsAux)
      {
        try
        {
          itemModelInfo = IKCMS_ModelCMS_Provider.Provider.FindBestModelMatch(Utility.FindTypeCached(fsNode.vData.manager_type));
          var mdl = IKCMS_ModelCMS_Provider.Provider.ModelBuild(null, fsNode, itemModelInfo) as IKCMS_ModelCMS_WidgetCMS_ImageCMS;
          XElement xImage = new XElement("image");
          //
          xImage.Add(new XElement("codeCMS", mdl.sNode));
          xImage.Add(new XElement("nameCMS", mdl.Name));
          //xImage.Add(new XElement("pathCMS", (mdl as IKCMS_ModelCMS_VFS_Interface).PathVFS.ToString()));
          xImage.Add(new XElement("Title", mdl.VFS_Resource.ResourceSettings.Title));
          xImage.Add(new XElement("Text", mdl.VFS_Resource.ResourceSettings.Text));
          xImage.Add(new XElement("Alt", mdl.VFS_Resource.ResourceSettings.Alt));
          xImage.Add(new XElement("Author", mdl.VFS_Resource.ResourceSettings.Author));
          xImage.Add(new XElement("Votes", fsNode.vData.IKGD_RNODE.LazyLogin_Votes.Count(r => r.Category == 0)));
          xImage.Add(new XElement("Views", fsNode.vData.IKGD_RNODE.IKG_HITLOGs.Count));
          //
          xDump.Add(xImage);
        }
        catch { }
      }
      //
      return xDump;
    }


    protected string _CachingKey;
    public virtual string CachingKey
    {
      get
      {
        if (_CachingKey == null)
        {
          List<object> frags = new List<object>();
          frags.Add(this.GetType().Name);
          //
          CachingKeyCustomPreProcessor(frags);
          //
          frags.Add(Model != null ? Model.rNode : 0);
          for (int i = 0; i < ArgsSet.Count; i++)
            if (ArgsSet.Keys[i] == ParameterNameFilter || ArgsSet.Keys[i] == ParameterNameFilterArr)
              frags.Add(ArgsSet[i]);
          frags.Add(ArgsSet[ParameterNameSorter]);
          frags.Add(ArgsSet[ParameterNameLucene]);
          //frags.Add(ArgsSet[ParameterNamePager]);  // se viene usato disabilita il caching durante il paging
          //frags.Add(ArgsSet[ParameterNameDateActive]);
          frags.Add(ArgsSet[ParameterNameDateMin]);
          frags.Add(ArgsSet[ParameterNameDateMax]);
          frags.Add(ArgsSet[ParameterNameDateExt]);
          frags.Add(ArgsSet[ParameterNameDateExpiryMin]);
          frags.Add(ArgsSet[ParameterNameDateExpiryMax]);
          frags.Add(ArgsSet[ParameterNameFolders]);
          frags.Add(ArgsSet[ParameterNameFoldersTree]);
          //frags.Add(GetCurrentMode);
          if (TagsExternal != null)
            frags.AddRange(TagsExternal.Select(t => (object)t));
          if (TagsValuedExternal != null)
            frags.AddRange(TagsValuedExternal.Select(t => (object)string.Format("{0}={1}", t.Key, t.Value)));
          if (KVTsExternal != null)
            frags.AddRange(KVTsExternal.Select(t => (object)string.Format("{0}={1}", t.Key, t.Value)));
          if (FiltersCustom != null && !IgnoreFiltersCustom.GetValueOrDefault(false))
            frags.AddRange(FiltersCustom.Select(t => (object)string.Format("{0}={1}", t.Key, t.Value)));
          //
          //_CachingKey = FS_OperationsHelpers.ContextHashNN(frags.ToArray());
          _CachingKey = CachingKeyCustomPostProcessor(frags);
        }
        return _CachingKey;
      }
    }


    public virtual void CachingKeyReset()
    {
      _CachingKey = null;
      _DataStorageCached = null;
    }


    //
    // custom preprocessor e postprocessor for cache key generation
    // customizable to account for cache dependecies on custom filters
    //
    public virtual void CachingKeyCustomPreProcessor(List<object> frags)
    {
    }
    public virtual string CachingKeyCustomPostProcessor(List<object> frags)
    {
      return FS_OperationsHelpers.ContextHashNN(frags.ToArray());
    }


    protected DataStorage _DataStorageCached;
    public virtual DataStorage DataStorageCached
    {
      get
      {
        return _DataStorageCached ?? (_DataStorageCached = FS_OperationsHelpers.CachedEntityWrapper(CachingKey, () => { return new DataStorage(); }, DefaultCachingTimeData, CachingTableDependencies));
      }
    }


    public class DataStorage
    {
      public List<int> rNodes { get; set; }
      public List<int> sNodes { get; set; }
      public List<int> Tags { get; set; }
      public bool Processed { get; set; }
      public List<int> TagsFilter { get; set; }
      public bool FilteredByLucene { get; set; }
      public bool FilteredByPath { get; set; }
      public List<int> brokenNodes { get; set; }
      public string SortMode { get; set; }
      public object customData { get; set; }


      public DataStorage()
      {
        Processed = false;
        TagsFilter = new List<int>();
        FilteredByLucene = false;
        FilteredByPath = false;
      }

    }

  }


  //
  // filtro base per semplici moduli tipo catalog senza tanti fronzoli
  //
  public class ManagerTagFilterCatalogSimpleBase : ManagerTagFilterBase
  {
    public override ManagerTagFilterBase.FetchModeEnum FetchMode { get { return FetchModeEnum.rNodeFetch; } }
    public override bool? FilteredResourcesAreFolders { get { return true; } }

    public ManagerTagFilterCatalogSimpleBase(IKCMS_ModelCMS_Interface model)
      : base(model)
    {
      AllowedTypeNames = new List<string> { typeof(IKCMS_ResourceType_PageCMS).Name };
      AllowedCategories = new List<string> { };
      MaxScanRecursionLevel = 1;  // non vogliamo la ricorsione completa nei subfolders
      AllowEmptyFilterAndArchiveSet = true;
      AllowTagsFromModel = false;
      UseModelFolderAsArchive = true;
      UseGenericModelBuild = true;
    }

  }



}
