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




  public interface IKCMS_ModelCMS_TeaserNewsEventi_Interface : IKCMS_ModelCMS_Interface, IKCMS_ModelCMS_HasTemplateInfo_Interface, IKCMS_ModelCMS_GenericBrickInterface
  {
    //ereditati da IKCMS_ModelCMS_HasTemplateInfo_Interface
    //IKCMS_PageCMS_Template_Interface TemplateInfo { get; }
    //string TemplateViewPath { get; }
    string TemplateItemViewPath { get; }
    //string Title { get; }
    int? MaxItems { get; }
    int? TimeDelayFrame { get; }
    int? TimeTransition { get; }
    string CollectorType { get; }
    string TagManagerType { get; }
    //
    IEnumerable<IKCMS_ModelCMS_Interface> GetIndexItems(vfsNodeFetchModeEnum fetchMode);
    //
    //IEnumerable<IKCMS_ModelCMS_Interface> GetCalendarData(vfsNodeFetchModeEnum fetchMode, int year, int month, int? day);
    List<object> GetCalendarEventsAjax(int? year, int? month, int? maxItems);
    //
  }


  //
  // model per i teasers dei moduli news/eventi
  // sono dei widget che contengono altri widget + info sui templates da utilizzare per il rendering
  //
  [IKCMS_ModelCMS_ResourceTypes(typeof(IKCMS_ResourceType_TeaserNewsEventiKVT))]
  [IKCMS_ModelCMS_BootStrapperOpenGenerics(typeof(IKCMS_ResourceType_TeaserNewsEventiKVT))]
  [IKCMS_ModelCMS_RecursionMode(ModelRecursionModeEnum.RecursionNone)]
  [IKCMS_ModelCMS_fsNodeMode(vfsNodeFetchModeEnum.vNode_vData_iNode_Extra)]
  [IKCMS_ModelCMS_Priority(-2499000)]
  public class IKCMS_ModelCMS_TeaserNewsEventi<T> : IKCMS_ModelCMS_GenericBrickBase<T>, IKCMS_ModelCMS_TeaserNewsEventi_Interface, IKCMS_ModelCMS_GenericBrickSlotTeaserOrWidgetInterface
    where T : IKCMS_ResourceType_TeaserNewsEventiKVT, IKCMS_HasPropertiesKVT_Interface, IKCMS_HasSerializationCMS_Interface   // per usare la classe piu' estesa e' necessario specificare l'attributo IKCMS_ModelCMS_BootStrapperOpenGenerics
  {
    public string TemplateItemViewPath { get; set; }
    public int? MaxItems { get; set; }
    public int? TimeDelayFrame { get; set; }
    public int? TimeTransition { get; set; }
    public string CollectorType { get; set; }  //IKGD_Teaser_Collector_InterfaceNG
    public string TagManagerType { get; set; }  //ManagerTagFilterBase_Interface
    //
    public override bool HasLink { get { return LinkUrl.IsNotEmpty() && VFS_ResourceNoLanguageKVT("ArchiveLinkEnabled").TryGetValue<bool>(true); } }
    public override string LinkUrl { get; set; }
    //

    //
    // continuazione del setup del model dopo le features di base processate in IKCMS_ModelCMS
    //
    protected override void SetupFinalize(params object[] args)
    {
      base.SetupFinalize(args);
      //
      // inizializzazione dei parametri
      try
      {
        MaxItems = Utility.TryParse<int>(VFS_Resource.ResourceSettingsNoLanguageKVT("MaxItems").ValueString.NullIfEmpty() ?? VFS_Resource.ResourceSettingsKVT.Get("MaxItems").ValueString.NullIfEmpty(), 10);
        TimeDelayFrame = Utility.TryParse<int>(VFS_Resource.ResourceSettingsNoLanguageKVT("msDelay").ValueString.NullIfEmpty() ?? VFS_Resource.ResourceSettingsKVT.Get("msDelay").ValueString.NullIfEmpty());
        TimeTransition = Utility.TryParse<int>(VFS_Resource.ResourceSettingsNoLanguageKVT("msTransition").ValueString.NullIfEmpty() ?? VFS_Resource.ResourceSettingsKVT.Get("msTransition").ValueString.NullIfEmpty());
        //Title = VFS_Resource.ResourceSettingsKVT["Title"].ValueString;
        //MaxItems = VFS_Resource.ResourceSettingsKVT["MaxItems"].TryGetValue<int>(10);
        //TimeDelayFrame = VFS_Resource.ResourceSettingsKVT["msDelay"].ValueT<int>();
        //TimeTransition = VFS_Resource.ResourceSettingsKVT["msTransition"].ValueT<int>();
      }
      catch { }
      //
      // inizializzazione dei templates per gli elementi
      try
      {
        TemplateViewPath = TemplateViewPath ?? TemplateInfo.ViewPaths["container"] ?? TemplateInfo.ViewPath;
        TemplateItemViewPath = TemplateInfo.ViewPaths["item"] ?? TemplateInfo.ViewPath;
      }
      catch { }
      //
      // scan del VFS per il setup degli items
      try
      {
        CollectorType = VFS_Resource.ResourceSettings.CollectorType;
        TagManagerType = VFS_Resource.ResourceSettings.TagManagerType;
        //
        // la cacheKey dipende da rNode in modo che posso riciclare i dati in cache anche quando incontro
        // altri symlink equivalenti nel resto del VFS
        //
        List<IKCMS_ModelCMS_Interface> subModels = null;
        bool useCachedItems = false;
        //
        // supporto del TagsManager al posto del NewsBrowser
        if (TagManagerType.IsNotNullOrWhiteSpace())
        {
          try
          {
            ManagerTagFilterBase_Interface manager = null;
            try { manager = Activator.CreateInstance(Utility.FindTypeCached(TagManagerType), this) as ManagerTagFilterBase_Interface; }
            catch { }
            if (manager != null)
            {
              if (manager.Model != null)
              {
                if (manager.Model is IKCMS_ModelCMS_Page_Interface)
                {
                  LinkUrl = manager.Model.UrlCanonical;
                }
                else
                {
                  try { LinkUrl = IKCMS_ModelCMS_ArchiveBrowserHelper.GetUrlForBrowserModule(fsOp, manager.Model.RelationsOrdered.Where(r => r.type == IKGD_Constants.IKGD_ArchiveRelationName).Select(r => r.rnode_dst).Distinct().ToArray()); }
                  catch { }
                }
              }
              subModels = manager.GetModelsForTeasers(MaxItems ?? int.MaxValue, null).OfType<IKCMS_ModelCMS_Interface>().ToList();
              Models.AddRange(subModels);
            }
            return;
          }
          catch { }
        }
        //else if (CollectorType.IsNotNullOrWhiteSpace())
        else
        {
          LinkUrl = IKCMS_ModelCMS_ArchiveBrowserHelper.GetUrlForBrowserModule(fsOp, this.Relations.Select(r => r.rnode_dst).Distinct().ToArray());
          //
          if (useCachedItems)
          {
            string cacheKey = FS_OperationsHelpers.ContextHashNN(this.GetType(), this.rNode);
            subModels = FS_OperationsHelpers.CachedEntityWrapper(cacheKey, () =>
            {
              return GetIndexItems(vfsNodeFetchModeEnum.vNode_vData, this).ToList();
            }, 3600, FS_OperationsHelpers.Const_CacheDependencyIKGD_vNode_vData_iNode);
          }
          else
          {
            subModels = GetIndexItems(vfsNodeFetchModeEnum.vNode_vData, this).ToList();
          }
          //
          // non possiamo usare models cached se li reparentiamo...
          //subModels.ForEach(m => m.ModelParent = this);
          if (subModels != null)
          {
            Models.AddRange(subModels);
          }
        }
      }
      catch { }
      //
    }


    //
    // fetch degli items da visualizzare nell'index senza che siano aggiunti al Model
    //
    public IEnumerable<IKCMS_ModelCMS_Interface> GetIndexItems(vfsNodeFetchModeEnum fetchMode) { return GetIndexItems(fetchMode, null); }
    public IEnumerable<IKCMS_ModelCMS_Interface> GetIndexItems(vfsNodeFetchModeEnum fetchMode, IKCMS_ModelCMS_Interface modelReference)
    {
      IQueryable<FS_Operations.FS_NodeInfo_Interface> resources = null;
      try
      {
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
        IKGD_Teaser_Collector_Interface<FS_Operations.FS_NodeInfo_Interface> itemsCollector = null;
        try { itemsCollector = (IKGD_Teaser_Collector_Interface<FS_Operations.FS_NodeInfo_Interface>)Activator.CreateInstance(Utility.FindTypeGeneric(CollectorType, typeof(FS_Operations.FS_NodeInfo_Interface))); }
        catch { }
        itemsCollector = itemsCollector ?? new IKGD_Teaser_Collector_NewsEventsOrderVFS<FS_Operations.FS_NodeInfo_Interface>();
        //
        Expression<Func<IKGD_VDATA, bool>> vDataFilterMain = fsOp.Get_vDataFilterACLv2();
        Expression<Func<IKGD_VNODE, bool>> vNodeFilterMain = fsOp.Get_vNodeFilterACLv2();
        List<int> sNodeRoots = this.RelationsOrdered.Where(r => r.type == IKGD_Constants.IKGD_ArchiveRelationName).Select(r => r.snode_dst).ToList();
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
        IKCMS_ExceptionsManager.Add(new IKCMS_Exception_ModelBuilder(this.GetType().FullName + ".GetIndexItems", ex));
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
          }
          catch (Exception ex)
          {
            Elmah.ErrorSignal.FromCurrentContext().Raise(ex);
            IKCMS_ExceptionsManager.Add(new IKCMS_Exception_ModelBuilder(this.GetType().FullName + ".GetIndexItems[RECURSION]", ex));
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
        IKGD_Teaser_Collector_Interface<FS_Operations.FS_NodeInfo_Interface> itemsCollector = null;
        try { itemsCollector = (IKGD_Teaser_Collector_Interface<FS_Operations.FS_NodeInfo_Interface>)Activator.CreateInstance(Utility.FindTypeGeneric(CollectorType, typeof(FS_Operations.FS_NodeInfo_Interface))); }
        catch { }
        itemsCollector = itemsCollector ?? new IKGD_Teaser_Collector_NewsEventsOrderVFS<FS_Operations.FS_NodeInfo_Interface>();
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
        resources = itemsCollector.Sorter(resources).Take(maxItems.Value);
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
        //
      }
      catch { }
      return events;
    }


    //
    // generazione delle informazioni necessarie al rendering del calendar e dei relativi items
    //
    /*
    public virtual IEnumerable<IKCMS_ModelCMS_Interface> GetCalendarData(vfsNodeFetchModeEnum fetchMode, int year, int month, int? day)
    {
      IQueryable<FS_Operations.FS_NodeInfo_Interface> resources = null;
      //
      DateTime dateRef = FS_OperationsHelpers.DateTimeSession;
      try { dateRef = new DateTime(year, month, day.GetValueOrDefault(1)); }
      catch { }
      bool getFullMonth = day == null;
      //
      try
      {
        //
        Func<FS_Operations, Expression<Func<IKGD_VNODE, bool>>, Expression<Func<IKGD_VDATA, bool>>, IQueryable<FS_Operations.FS_NodeInfo_Interface>> fetcher = null;
        //
        switch (fetchMode)
        {
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
        IKGD_Teaser_Collector_Interface<FS_Operations.FS_NodeInfo_Interface> itemsCollector = null;
        try { itemsCollector = (IKGD_Teaser_Collector_Interface<FS_Operations.FS_NodeInfo_Interface>)Activator.CreateInstance(Utility.FindTypeGeneric(CollectorType, typeof(FS_Operations.FS_NodeInfo_Interface))); }
        catch { }
        itemsCollector = itemsCollector ?? new IKGD_Teaser_Collector_NewsEventsOrderVFS<FS_Operations.FS_NodeInfo_Interface>();
        //
        Expression<Func<IKGD_VDATA, bool>> vDataFilterMain = fsOp.Get_vDataFilterACLv2();
        Expression<Func<IKGD_VNODE, bool>> vNodeFilterMain = fsOp.Get_vNodeFilterACLv2();
        List<int> sNodeRoots = this.RelationsOrdered.Where(r => r.type == IKGD_Constants.IKGD_ArchiveRelationName).Select(r => r.snode_dst).ToList();
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
        DateTime dateRangeStart = getFullMonth == false ? dateRef.Date : dateMonthStart;
        DateTime dateRangeEnd = getFullMonth == false ? dateRef.Date.AddDays(1) : dateMonthEnd;
        //
        Expression<Func<IKGD_VDATA, bool>> vDataFilterMainRange = vDataFilterMain.And(n => n.date_node < dateRangeEnd && (n.date_node >= dateRangeStart || (n.date_node_aux != null && n.date_node_aux.Value >= dateRangeStart)));
        //
        IQueryable<FS_Operations.FS_NodeInfo_Interface> resourcesActive = fetcher(fsOp, vNodeFilterMain, vDataFilterMainRange);
        //
        // visto che si possono usare i symlink condenso tutto per rnode prima del take
        //var resources = resourcesActive.GroupBy(n => n.vNode.rnode).Select(g => g.First()).OrderBy(n => n.vData.date_node);
        resources = resourcesActive.GroupBy(n => n.vNode.rnode).Select(g => g.First());
        resources = itemsCollector.Sorter(resources);
        //
        if (MaxItems > 0)
          resources = resources.Take(MaxItems.Value);
        //
        if (itemsCollector is IKGD_Collector_ReverseResultsAfterTake)
          resources = resources.AsEnumerable().Reverse().AsQueryable();
        //
      }
      catch { yield break; }
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
          }
          catch { }
          if (model == null)
            continue;
          //TODO: verificare yield con inizializzazione pesante
          yield return model;
        }
      }
      //
    }
    */



    public class BrowserCalendarData
    {
      public DateTime? DateCurrent { get; set; }
      public DateTime? DateMin { get; set; }
      public DateTime? DateMax { get; set; }
      public List<IKCMS_ModelCMS_Interface> Items { get; set; }
      //
      public List<TupleW<DateTime, List<int>>> DatesActive { get; set; }
      public string DatesActiveString { get; set; }
    }


  }



}  //namespace
