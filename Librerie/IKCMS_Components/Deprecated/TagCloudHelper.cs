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
using Ikon.IKGD.Library.Collectors;
using Ikon.IKGD.Library.Resources;
using Ikon.IKCMS.Pagers;


namespace Ikon.IKCMS
{

  public static class TagCloudManager
  {

    public static TagCloudHandler GetHandler() { return GetHandler(null, null, null, IKGD_ConfigVFS.Config.RootsCMS_PathsActive.Select(p => p.sNode)); }
    public static TagCloudHandler GetHandler(string tagVarName, string manager_type, string category, IEnumerable<int> sNodeRoots)
    {
      TagCloudHandler tagCloudHandler = null;
      try
      {
        TagCloudHandler.NormalizeParameters(ref tagVarName, ref manager_type, ref category);
        //
        string contextCacheKey = FS_OperationsHelpers.ContextHashNN(typeof(TagCloudHandler).Name, tagVarName, manager_type, category, (sNodeRoots != null) ? Utility.Implode(sNodeRoots, ",") : null);
        tagCloudHandler = FS_OperationsHelpers.CachedEntityWrapper(contextCacheKey, () =>
        {
          return new TagCloudHandler(tagVarName, manager_type, category, sNodeRoots);
        }, 3600, FS_OperationsHelpers.Const_CacheDependencyIKGD_vNode_vData_Property);
        //tagCloudHandler = new TagCloudHandler(tagVarName, manager_type, category, sNodeRoots);
      }
      catch { }
      //
      return tagCloudHandler;
    }

  }



  public class TagCloudHandler
  {
    public string TagVarName { get; protected set; }
    public string ManagerType { get; protected set; }
    public string Category { get; protected set; }
    public List<int> sNodeRoots { get; protected set; }
    //
    public Utility.DictionaryMV<string, TagCloudItem> TagsData { get; protected set; }
    //
    public PagerSimple<int> Pager { get; protected set; }
    //
    protected Expression<Func<IKGD_VNODE, bool>> vNodeFilterResources { get; private set; }
    protected Expression<Func<IKGD_VDATA, bool>> vDataFilterResources { get; private set; }
    //
    public Type TagsCloudControllerType { get; set; }
    //
    protected FS_Operations fsOp { get { return IKCMS_ManagerIoC.requestContainer.ResolveNamed<FS_Operations>("readonly"); } }
    //protected FS_Operations fsOp { get { return IKCMS_ManagerIoC.requestContainer.Resolve<FS_Operations>(); } }
    //

    protected TagCloudHandler() { }
    public TagCloudHandler(string tagVarName, string manager_type, string category, IEnumerable<int> sNodeRoots)
    {
      //
      NormalizeParameters(ref tagVarName, ref manager_type, ref category);
      //
      TagVarName = tagVarName;
      ManagerType = manager_type;
      Category = category;
      this.sNodeRoots = sNodeRoots != null ? sNodeRoots.ToList() : null;
      //
      Setup();
      //
    }


    public static void NormalizeParameters(ref string tagVarName, ref string manager_type, ref string category)
    {
      manager_type = manager_type.NullIfEmpty();
      category = (string.IsNullOrEmpty(manager_type) ? null : category).NullIfEmpty();
      tagVarName = tagVarName.NullIfEmpty() ?? IKGD_Config.AppSettings["ResourceTagsDefaultTagVarName"] ?? "tags";
    }


    protected void Setup()
    {
      TagsData = null;
      try
      {
        ManagerType = ManagerType.NullIfEmpty();
        Category = (string.IsNullOrEmpty(ManagerType) ? null : Category).NullIfEmpty();
        TagVarName = TagVarName.NullIfEmpty() ?? IKGD_Config.AppSettings["ResourceTagsDefaultTagVarName"] ?? "tags";
        //
        TagsCloudControllerType = Utility.FindTypesWithInterfaces(typeof(TagsCloudController_Interface)).Where(t => !t.IsAbstract && t.IsClass).FirstOrDefault();
        //
        // filtro sulla tipologia/category delle risorse (se specificati)
        Expression<Func<IKGD_VNODE, bool>> vNodeFilter = fsOp.Get_vNodeFilterACLv2(true);
        Expression<Func<IKGD_VDATA, bool>> vDataFilter = fsOp.Get_vDataFilterACLv2(false, true);
        if (!string.IsNullOrEmpty(ManagerType))
        {
          vDataFilter = vDataFilter.And(n => n.manager_type == ManagerType);
          if (!string.IsNullOrEmpty(Category))
          {
            vDataFilter = vDataFilter.And(n => n.category == Category);
          }
        }
        //
        // attiva sempre il filtro sul range di validita' degli elementi
        var dtNow = FS_OperationsHelpers.DateTimeSession;
        vDataFilter = vDataFilter.And(n => (n.date_activation == null || n.date_activation <= dtNow) && (n.date_expiry == null || dtNow <= n.date_expiry));
        //
        // filtro sulle sole risorse di tipo browse (non pagine web normali)
        //{
        //  var typesToScan = IKCMS_RegisteredTypes.Types_IKCMS_BrowsableIndexable_Interface.Select(t => t.Name).ToList();
        //  if (typesToScan.Any())
        //    vDataFilter = vDataFilter.And(n => typesToScan.Contains(n.manager_type));
        //}
        //
        List<int> folderSet = new List<int>();
        if (sNodeRoots != null && sNodeRoots.Any())
        {
          FS_Operations.FS_TreeNode<IKCMS_TreeBrowser_fsNodeElement_Interface> nodesTreeFullInfo = fsOp.Get_TreeDataShortGeneric<IKCMS_TreeBrowser_fsNodeElement_Interface>(null, sNodeRoots, vNodeFilter, vDataFilter, null, true, true);
          folderSet = nodesTreeFullInfo.RecurseOnTree.Where(n => n.Data != null).Select(n => n.Data.vNode.folder).Distinct().ToList();
        }
        //
        vNodeFilterResources = vNodeFilter;
        vDataFilterResources = vDataFilter;
        if (folderSet != null && folderSet.Any())
          vNodeFilterResources = vNodeFilterResources.And(n => folderSet.Contains(n.folder));  // scan di tutto il tree
        //
        //var tagsList =
        //  from vNode in fsOp.NodesActive<IKGD_VNODE>().Where(vNodeFilterResources)
        //  from vData in fsOp.NodesActive<IKGD_VDATA>().Where(vDataFilterResources).Where(n => n.rnode == vNode.rnode)
        //  from prop in fsOp.NodesActive<IKGD_PROPERTY>().Where(p => !p.flag_deleted).Where(p => string.Equals(p.name, TagVarName)).Where(n => n.rnode == vNode.rnode)
        //  select prop.value;
        //
        // non voglio i doppioni dei symlinks conteggiati
        var tagsList =
          from vData in fsOp.NodesActive<IKGD_VDATA>().Where(vDataFilterResources)
          from prop in fsOp.NodesActive<IKGD_PROPERTY>().Where(p => !p.flag_deleted).Where(p => string.Equals(p.name, TagVarName)).Where(n => n.rnode == vData.rnode)
          where fsOp.NodesActive<IKGD_VNODE>().Where(vNodeFilterResources).Any(n => n.rnode == vData.rnode)
          select prop.value;
        //
        TagsData = new Utility.DictionaryMV<string, TagCloudItem>(tagsList.GroupBy(t => t.ToLower()).ToDictionary(g => g.FirstOrDefault(), g => new TagCloudItem { Name = g.FirstOrDefault(), Count = g.Count() }), StringComparer.OrdinalIgnoreCase);
        //
        if (TagsData.Any())
        {
          double sum = TagsData.Sum(t => t.Value.Count);
          int max = TagsData.Max(t => t.Value.Count);
          int min = TagsData.Min(t => t.Value.Count);
          TagsData.Values.ForEach(t =>
          {
            t.Frequency = (sum > 0.0) ? (t.Count / sum) : 1.0;
            t.Scale = (max > min) ? (((double)(t.Count - min)) / (max - min)) : 0.0;
          });
        }
        //
        if (TagsCloudControllerType != null)
        {
          string baseUrl = Utility.ResolveUrl("~/{0}/Index/".FormatString(TagsCloudControllerType.Name.SubStringCutFromEnd("Controller".Length)));
          TagsData.Values.ForEach(t => t.Url = baseUrl + Utility.StringToBase64(t.Name));
        }
      }
      catch { }
    }


    public IEnumerable<int> NodesPre { get { return (Pager != null) ? Nodes.Take(Pager.PagerStartIndex.GetValueOrDefault(0)) : Enumerable.Empty<int>(); } }
    public IEnumerable<int> NodesPost { get { return (Pager != null) ? Nodes.Skip(Pager.PagerStartIndex.GetValueOrDefault(0) + Pager.PagerPageSize.Value) : Enumerable.Empty<int>(); } }
    public IEnumerable<int> NodesPage { get { return (Pager != null) ? Nodes.Skip(Pager.PagerStartIndex.GetValueOrDefault(0)).Take(Pager.PagerPageSize.Value) : Nodes; } }
    public List<int> Nodes { get; protected set; }


    //
    // generazione degli rNodes che corrispondono al tag specificato
    //
    public List<int> ScanForTag(string tagValue) { return ScanForTag(tagValue, null, null); }
    public List<int> ScanForTag(string tagValue, int? pagerPageSize, string pagingVarQueryString)
    {
      Nodes =
        (from vData in fsOp.NodesActive<IKGD_VDATA>().Where(vDataFilterResources)
         where fsOp.NodesActive<IKGD_VNODE>().Where(vNodeFilterResources).Any(n => n.rnode == vData.rnode)
         where fsOp.NodesActive<IKGD_PROPERTY>().Where(p => !p.flag_deleted).Where(n => n.rnode == vData.rnode).Any(p => string.Equals(p.name, TagVarName) && string.Equals(p.value, tagValue))
         orderby vData.date_node descending, vData.date_node_aux descending, vData.rnode descending
         select vData.rnode).ToList();
      //
      Pager = (pagerPageSize == null) ? null : Ikon.IKCMS.Pagers.PagingHelperExtensions.FactoryPagerSimple(Nodes, pagerPageSize, pagingVarQueryString);
      //
      return Nodes;
    }


    public IEnumerable<IKCMS_ModelCMS_Interface> Models
    {
      get
      {
        IKCMS_ModelCMS_ModelInfo_Interface itemModelInfo = null;
        bool modeExt = false;
        try { modeExt = IKCMS_ModelCMS_Provider.Provider.FindBestModelMatch(Utility.FindTypeCached(fsOp.PathsFromNodesExt(null, NodesPage.Take(1), false, false).FirstOrDefault().LastFragment.ManagerType)).Attributes.OfType<IKCMS_ModelCMS_fsNodeModeAttribute>().Select(a => a.vfsNodeFetchMode).Any(m => m == vfsNodeFetchModeEnum.vNode_vData_iNode_Extra); }
        catch { }
        try
        {
          var rNodesMissing = NodesPage.Except(IKCMS_ModelCMS_Provider.Provider.managerVFS.NodesVFS.Select(n => n.rNode)).ToList();
          if (modeExt)
            IKCMS_ModelCMS_Provider.Provider.managerVFS.FetchNodesT<FS_Operations.FS_NodeInfoExt>(vn => rNodesMissing.Contains(vn.rnode) || rNodesMissing.Contains(vn.folder), null);
          else
            IKCMS_ModelCMS_Provider.Provider.managerVFS.FetchNodesT<FS_Operations.FS_NodeInfo>(vn => rNodesMissing.Contains(vn.rnode) || rNodesMissing.Contains(vn.folder), null);
        }
        catch { }
        //TODO: verificare yield con inizializzazione pesante
        foreach (var rNode in NodesPage)
        {
          var fsNode = IKCMS_ModelCMS_Provider.Provider.managerVFS.GetVfsNode(null, rNode);
          if (fsNode != null)
          {
            itemModelInfo = itemModelInfo ?? IKCMS_ModelCMS_Provider.Provider.FindBestModelMatch(Utility.FindTypeCached(fsNode.vData.manager_type));
            yield return IKCMS_ModelCMS_Provider.Provider.ModelBuild(null, fsNode, itemModelInfo);
          }
        }
      }
    }



    public class TagCloudItem
    {
      public string Name { get; set; }
      public int Count { get; set; }
      public string Url { get; set; }
      public double Frequency { get; set; }
      public double Scale { get; set; }
    }


  }


}
