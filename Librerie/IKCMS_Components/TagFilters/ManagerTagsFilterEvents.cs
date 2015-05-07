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


namespace Ikon.IKCMS
{


  public abstract class ManagerTagFilterEventsBase : ManagerTagFilterBase
  {
    public override ManagerTagFilterBase.FetchModeEnum FetchMode { get { return FetchModeEnum.rNodeFetch; } }
    public override bool? FilteredResourcesAreFolders { get { return false; } }
    //
    public virtual string ViewMode_Home { get { return IKGD_Config.AppSettings["ManagerTagFilterEvents_ViewModeHome"].NullIfEmpty() ?? ViewMode_Next; } }
    public virtual string ViewMode_Next { get { return "next"; } }
    public virtual string ViewMode_Month { get { return "month"; } }
    public virtual string ViewMode_Day { get { return "day"; } }
    public virtual string ViewMode_Detail { get { return "detail"; } }
    public virtual string ViewMode_Teaser { get { return "teaser"; } }
    //
    public virtual int? MaxNextItems { get; set; }


    public ManagerTagFilterEventsBase(IKCMS_ModelCMS_Interface model)
      : base(model)
    {
      AllowedTypeNames = new List<string> { typeof(IKCMS_ResourceType_NewsKVT).Name };
      AllowedCategories = new List<string> { "Event" };
      AllowEmptyFilterAndArchiveSet = false;
      UseModelFolderAsArchive = true;
      UseGenericModelBuild = true;
    }



    protected List<NodeInfo> GetItemsInRange(DateTime? dt1, DateTime? dt2, bool? snapToDayLimit, bool? includeEndDate, int? maxItems)
    {
      List<NodeInfo> results = new List<NodeInfo>();
      try
      {
        DateTime valueFirst = Utility.Max(Utility.DateTimeMinValueDB, Utility.MinAll(dt1.GetValueOrDefault(Utility.DateTimeMinValueDB), dt2.GetValueOrDefault(Utility.DateTimeMinValueDB)));
        DateTime valueLast = Utility.Min(Utility.DateTimeMaxValueDB, Utility.MaxAll(dt1.GetValueOrDefault(Utility.DateTimeMaxValueDB), dt2.GetValueOrDefault(Utility.DateTimeMaxValueDB)));
        if (snapToDayLimit.GetValueOrDefault(true))
        {
          try { valueFirst = valueFirst.Date; }
          catch { }
          try { valueLast = valueLast.Date.AddSeconds(86400); }
          catch { }
        }
        //
        EnsureFilters();
        //
        Expression<Func<IKGD_VDATA, bool>> vDataFilter = PredicateBuilder.True<IKGD_VDATA>();
        //
        // ricerca su intervallo [)
        vDataFilter = vDataFilter.And(n => (n.date_node_aux == null && n.date_node >= valueFirst) || (n.date_node_aux != null && n.date_node_aux.Value >= valueFirst));
        if (includeEndDate.GetValueOrDefault(false))
          vDataFilter = vDataFilter.And(n => (n.date_node_aux == null && n.date_node <= valueLast) || (n.date_node_aux != null && n.date_node <= valueLast));
        else
          vDataFilter = vDataFilter.And(n => (n.date_node_aux == null && n.date_node < valueLast) || (n.date_node_aux != null && n.date_node < valueLast));
        //
        var items = Query_vDatasFiltered.Where(vDataFilter).Join(Query_vNodesVFS, vd => vd.rnode, vn => vn.rnode, (vd, vn) => new { vn, vd }).Select(r => new NodeInfo
        {
          sNode = r.vn.snode,
          rNode = r.vd.rnode,
          manager_type = r.vd.manager_type,
          category = r.vd.category,
          date_node = r.vd.date_node,
          date_node_aux = r.vd.date_node_aux
        }).OrderBy(r => r.date_node).ThenBy(r => r.date_node_aux ?? r.date_node).ThenBy(r => r.rNode).ThenBy(r => r.sNode);
        //
        // supporto del filtro per lucene (se specificato)
        if (ArgsSet[ParameterNameLucene].IsNotNullOrWhiteSpace())
        {
          if (FetchMode == FetchModeEnum.sNodeFetch)
          {
            results = items.AsEnumerable().Join(Nodes, r => r.sNode, n => n, (r, n) => r).Take(maxItems.GetValueOrDefault(int.MaxValue)).ToList();
          }
          else
          {
            results = items.AsEnumerable().Join(Nodes, r => r.rNode, n => n, (r, n) => r).Take(maxItems.GetValueOrDefault(int.MaxValue)).ToList();
          }
        }
        else
        {
          results = items.Take(maxItems.GetValueOrDefault(int.MaxValue)).ToList();
        }
        //
        //results = Query_vDatasFiltered.Where(vDataFilter).Join(Query_vNodesVFS, vd => vd.rnode, vn => vn.rnode, (vd, vn) => new { vn, vd }).Select(r => new NodeInfo
        //{
        //  sNode = r.vn.snode,
        //  rNode = r.vd.rnode,
        //  manager_type = r.vd.manager_type,
        //  category = r.vd.category,
        //  date_node = r.vd.date_node,
        //  date_node_aux = r.vd.date_node_aux
        //}).OrderBy(r => r.date_node).ThenBy(r => r.date_node_aux ?? r.date_node).ThenBy(r => r.rNode).ThenBy(r => r.sNode).Take(maxItems.GetValueOrDefault(int.MaxValue)).ToList();
        //
        //if (findFirstNotEmptyInterval.GetValueOrDefault(false) && !results.Any())
        //{
        //  try
        //  {
        //    vDataFilter = PredicateBuilder.True<IKGD_VDATA>();
        //    vDataFilter = vDataFilter.And(n => (n.date_node_aux == null && n.date_node >= valueFirst) || (n.date_node_aux != null && n.date_node_aux.Value >= valueFirst));
        //    var dateFirst = Query_vDatasFiltered.Where(vDataFilter).Select(n => n.date_node).OrderBy(n => n).FirstOrDefault();
        //    // TODO:
        //    // completare con rescan per il nuovo intervallo
        //    // attenzione a salvare i metadati necessari per la generazione del calendario relativo (attenzione ad eventuali context non sincronizzati!)
        //  }
        //  catch { }
        //}
        //
        if (FilterNodesByValidPathRequired || FilterNodesByValidPath.GetValueOrDefault(Utility.TryParse<bool>(IKGD_Config.AppSettings["ManagerTagFilter_ValidatePath"], false)))
        {
          var rNodes = results.Select(n => n.rNode).Distinct().ToList();
          results = results.Join(fsOp.PathsFromNodesExt(null, rNodes, false, false).FilterPathsByRootsCMS(), n => n.sNode, p => p.sNode, (n, p) => n).Distinct((n1, n2) => n1.rNode == n2.rNode).OrderBy(r => r.date_node).ThenBy(r => r.date_node_aux ?? r.date_node).ThenBy(r => r.rNode).ThenBy(r => r.sNode).ToList();
        }
        else
        {
          results = results.Distinct((n1, n2) => n1.rNode == n2.rNode).ToList();
        }
        //
        //results = Query_vDatasFiltered.Where(vDataFilter).Distinct().Select(n => new NodeInfo
        //{
        //  rNode = n.rnode,
        //  manager_type = n.manager_type,
        //  category = n.category,
        //  date_node = n.date_node,
        //  date_node_aux = n.date_node_aux
        //}).OrderBy(r => r.date_node).ThenBy(r => r.date_node_aux ?? r.date_node).ThenBy(r => r.rNode).ToList();
        //
      }
      catch { }
      return results;
    }


    public List<NodeInfo> GetItemsInDates(DateTime? dt1, DateTime? dt2, bool? snapToDayLimit, bool? includeEndDate, int? maxItems)
    {
      if (dt1 == null && dt2 == null)
      {
        dt1 = dt2 = Ikon.GD.FS_OperationsHelpers.DateTimeSession.Date;
        snapToDayLimit = true;
      }
      if (dt1 != null && dt2 != null && dt1.Value.Date == dt2.Value.Date)
      {
        dt1 = dt2 = dt1.Value.Date;
        snapToDayLimit = true;
        includeEndDate = false;
      }
      int cachingTime = Utility.TryParse<int>(IKGD_Config.AppSettings["ManagerTagFilterEvents_CachingTime"], 900);
      //string cacheKey = FS_OperationsHelpers.ContextHashNN(this.GetType().Name, Model.sNode, "GetItemsInDates", dt1, dt2, snapToDayLimit, includeEndDate);
      string cacheKey = FS_OperationsHelpers.ContextHashNN(CachingKey, "GetItemsInDates", dt1, dt2, snapToDayLimit, includeEndDate);
      return FS_OperationsHelpers.CachedEntityWrapper(cacheKey, () =>
      {
        List<NodeInfo> items = null;
        try { items = GetItemsInRange(dt1, dt2, snapToDayLimit, includeEndDate, maxItems); }
        catch { }
        return items ?? new List<NodeInfo>();
      }, cachingTime, FS_OperationsHelpers.Const_CacheDependencyIKGD_vNode_vData_iNode_Relation_Property);
    }


    public ILookup<DateTime, NodeInfo> GetItemsInDatesGrouped(DateTime? dt1, DateTime? dt2)
    {
      if (dt1 != null && dt2 != null && Math.Abs((dt2.Value - dt1.Value).TotalDays) < 100)
      {
        DateTime dt_start = Utility.Min(dt1.Value, dt2.Value).Date;
        DateTime dt_end = Utility.Max(dt1.Value, dt2.Value).Date;
        List<DateTime> dates = new List<DateTime>();
        for (DateTime dt = dt_start; dt <= dt_end; dt = dt.AddDays(1.01).Date)
          dates.Add(dt);
        var items = GetItemsInDates(dt_start, dt_end, true, false, null);
        ILookup<DateTime, NodeInfo> results =
          (from date in dates
           from item in items
           where (Utility.Min(item.date_node, item.date_node_aux ?? item.date_node).Date <= date) && (date <= Utility.Max(item.date_node, item.date_node_aux ?? item.date_node).Date)
           select new { date, item }).ToLookup(r => r.date, r => r.item);
        return results;
      }
      return GetItemsInDates(dt1, dt2, true, false, null).ToLookup(r => r.date_node.Date);
    }


    public DateTime? FakeDate { get; set; }
    public DateTime GetCurrentDate() { return GetCurrentDate(null); }
    public DateTime GetCurrentDate(DateTime? fallBackDate)
    {
      DateTime? forcedDate = fallBackDate;
      //if (ModelInput != null && ModelInput != Model)
      if (ModelInput != null && IsViewMode_Detail)
        forcedDate = forcedDate ?? ModelInput.DateNode;
      return Utility.TryParse<DateTime>(ArgsSet[ParameterNameDateActive], forcedDate ?? FakeDate ?? Ikon.GD.FS_OperationsHelpers.DateTimeSession.Date).Date;
    }


    public DateTime GetDateMonth()
    {
      var date = GetCurrentDate();
      return new DateTime(date.Year, date.Month, 1);
    }


    public DateTime? GetDateMonthPrev()
    {
      var date = GetCurrentDate();
      DateTime? date_target = new DateTime(date.Year, date.Month, 1).AddMonths(-1);
      if (DataStorageCached != null && DataStorageCached.customData != null && DataStorageCached.customData is CustomDataEvents)
      {
        if ((DataStorageCached.customData as CustomDataEvents).dateMin != null && date_target < (DataStorageCached.customData as CustomDataEvents).dateMinMonth)
          return null;
      }
      return date_target;
    }


    public DateTime? GetDateMonthNext()
    {
      var date = GetCurrentDate();
      DateTime? date_target = new DateTime(date.Year, date.Month, 1).AddMonths(+1);
      if (DataStorageCached != null && DataStorageCached.customData != null && DataStorageCached.customData is CustomDataEvents)
      {
        if ((DataStorageCached.customData as CustomDataEvents).dateMax != null && date_target > (DataStorageCached.customData as CustomDataEvents).dateMaxMonth)
          return null;
      }
      return date_target;
    }


    protected string _UrlBase = null;
    public string UrlBase
    {
      get
      {
        if (_UrlBase == null)
        {
          try
          {
            if (Model == ModelInput)
            {
              _UrlBase = HttpContext.Current.Request.Url.ToString();
            }
            else if (Model != null)
            {
              _UrlBase = Model.UrlCanonical;
              try
              {
                string qs = HttpContext.Current.Request.Url.Query;
                if (qs.IsNotNullOrWhiteSpace())
                {
                  _UrlBase = Utility.UriMigrateQueryString(HttpContext.Current.Request.Url.ToString(), _UrlBase, false);
                }
              }
              catch { }
            }
          }
          catch
          {
            _UrlBase = HttpContext.Current.Request.Url.ToString();
          }
          if (_UrlBase.IsNotNullOrWhiteSpace())
          {
            _UrlBase = Utility.UriDelQueryVars(_UrlBase, ParameterNameViewMode, ParameterNameDateActive);
          }
        }
        return _UrlBase;
      }
    }


    public string UrlHome { get { return UrlBase; } }
    public string UrlMonth { get { return Utility.UriSetQuery(Utility.UriSetQuery(UrlBase, ParameterNameDateActive, GetDateMonth().ToString("yyyy-MM-dd")), ParameterNameViewMode, ViewMode_Month); } }
    public string UrlMonthPrev { get { var date_target = GetDateMonthPrev(); return date_target == null ? null : Utility.UriSetQuery(Utility.UriSetQuery(UrlBase, ParameterNameDateActive, date_target.Value.ToString("yyyy-MM-dd")), ParameterNameViewMode, ViewMode_Month); } }
    public string UrlMonthNext { get { var date_target = GetDateMonthNext(); return date_target == null ? null : Utility.UriSetQuery(Utility.UriSetQuery(UrlBase, ParameterNameDateActive, date_target.Value.ToString("yyyy-MM-dd")), ParameterNameViewMode, ViewMode_Month); } }


    public string GetCurrentModeNN { get { return (IsViewMode_Detail ? ViewMode_Detail : GetCurrentMode) ?? ViewMode_Home; } }
    public bool IsViewMode_Detail
    {
      get
      {
        bool isDetail = (GetCurrentMode == ViewMode_Detail);
        if (!isDetail && ArgsSet[ParameterNameViewMode].IsNullOrEmpty())
        {
          // usiamo le info sul model corrente solo se non e' stato specificato niente nei params
          isDetail |= (Model != ModelInput && Model != null && Model is IKCMS_ModelCMS_Page_Interface) || (Model == ModelInput && ModelInput != null && !(ModelInput is IKCMS_ModelCMS_Page_Interface));
        }
        return isDetail;
      }
    }


    public ILookup<DateTime, NodeInfo> GetNodesForCalendar(DateTime? forcedDate)
    {
      //
      DateTime dt_start = GetCurrentDate(forcedDate);
      DateTime dt_month_start = new DateTime(dt_start.Year, dt_start.Month, 1);
      DateTime dt_month_end = dt_month_start.AddMonths(1);
      //
      List<DateTime> dates = new List<DateTime>();
      for (DateTime dt = dt_month_start; dt < dt_month_end; dt = dt.AddDays(1.01).Date)
        dates.Add(dt);
      var items = GetItemsInDates(dt_month_start, dt_month_end, false, false, null);
      ILookup<DateTime, NodeInfo> results =
        (from date in dates
         from item in items
         where (Utility.Min(item.date_node, item.date_node_aux ?? item.date_node).Date <= date) && (date <= Utility.Max(item.date_node, item.date_node_aux ?? item.date_node).Date)
         select new { date, item }).ToLookup(r => r.date, r => r.item);
      return results;
    }


    public override List<IKCMS_ModelCMS_GenericBrickInterface> GetModelsForTeasers(int maxNodes, string forcedSortingMode)
    {
      ArgSetClear();
      forcedSortingMode = forcedSortingMode ?? "+Date";
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
      var submodels = Models.OfType<IKCMS_ModelCMS_GenericBrickInterface>().ToList();
      if (!(Model is IKCMS_ModelCMS_TeaserNewsEventi_Interface))
      {
        Models.OfType<IKCMS_ModelCMS_ArchiveBrowserItem_Interface>().ForEach(m => m.ModelContainerUnBinded = Model);
      }
      return submodels;
    }


    public override void ScanVFS_ScanPost()
    {
      base.ScanVFS_ScanPost();
      //
      try
      {
        if (GetCurrentMode != "teaser")
        {
          if (DataStorageCached.customData == null)
          {
            var data = new CustomDataEvents { dateMin = DateTime.MaxValue, dateMax = DateTime.MinValue };
            // trucco per min/max con una singola query
            var res = Query_vDatasFiltered.GroupBy(r => 0).Select(g => new { dateMin = g.Min(r => r.date_node), dateMax1 = g.Max(r => r.date_node), dateMax2 = g.Max(r => r.date_node_aux ?? r.date_node) }).FirstOrDefault();
            if (res != null)
            {
              data = new CustomDataEvents { dateMin = Utility.MinAll(res.dateMin, res.dateMax1, res.dateMax2), dateMax = Utility.MaxAll(res.dateMin, res.dateMax1, res.dateMax2) };
            }
            data.dateMinMonth = new DateTime(data.dateMin.Value.Year, data.dateMin.Value.Month, 1);
            data.dateMaxMonth = new DateTime(data.dateMax.Value.Year, data.dateMax.Value.Month, 1);
            DataStorageCached.customData = data;
          }
        }
      }
      catch { }
      //
    }


    public override void ScanVFS_WorkerProcessorPre()
    {
      base.ScanVFS_WorkerProcessorPre();
      if (GetCurrentMode == "teaser")
      {
        DateTime dt_start = GetCurrentDate();
        Query_vDatasFiltered = Query_vDatasFiltered.Where(n => (n.date_node_aux ?? n.date_node) >= dt_start);
      }
    }


    //public override void ScanVFS_WorkerProcessorFinalize()
    //{
    //  base.ScanVFS_WorkerProcessorFinalize();
    //  if (GetCurrentMode == "teaser")
    //  {
    //    DateTime dt_start = GetCurrentDate();
    //    Query_vDatasFiltered = Query_vDatasFiltered.Where(n => (n.date_node_aux ?? n.date_node) >= dt_start);
    //  }
    //}


    protected List<IKCMS_ModelCMS_GenericBrickInterface> _EventModels;
    public virtual IEnumerable<IKCMS_ModelCMS_GenericBrickInterface> EventModels
    {
      get
      {
        try
        {
          if (_EventModels == null)
          {
            //
            ScanVFS();  // per assicurarsi che siano stati inizializzati tutti i dati
            //
            if (IsViewMode_Detail && Model == ModelInput)
            {
              _EventModels = new List<IKCMS_ModelCMS_GenericBrickInterface> { this.ModelInput as IKCMS_ModelCMS_GenericBrickInterface };
              return _EventModels;
            }
            //
            DateTime dt_start = GetCurrentDate();
            DateTime dt_end = dt_start.AddDays(1);  // partiamo con gia' il default per ViewMode_Day
            DateTime dt_month_start = new DateTime(dt_start.Year, dt_start.Month, 1);
            DateTime dt_month_end = dt_month_start.AddMonths(1);
            int? maxItems = null;
            bool autoUpdateDateStart = false;
            //
            if (GetCurrentMode == ViewMode_Day)
            {
            }
            else if (GetCurrentMode == ViewMode_Month)
            {
              dt_start = dt_month_start;
              dt_end = dt_month_end;
            }
            else if (Model != ModelInput)
            {
              // eventi del giorno
            }
            else if (GetCurrentModeNN == ViewMode_Next)
            {
              dt_end = Utility.DateTimeMaxValueDB.Date;
              maxItems = PagerSimple<object>.DefaultPagerPageSize;
              //maxItems = int.MaxValue;
              autoUpdateDateStart = true;
            }
            else // ViewMode_Home o ViewMode_Next
            {
              dt_end = dt_month_end;
            }
            //
            var nodeInfos = GetItemsInDates(dt_start, dt_end, false, false, maxItems);
            var rNodes = nodeInfos.Select(r => r.rNode).Distinct().ToList();
            //
            if (autoUpdateDateStart && nodeInfos.Any())
            {
              try
              {
                var dateNew = nodeInfos.Select(r => r.date_node.Date).FirstOrDefault();
                FakeDate = Utility.Max(GetCurrentDate(), dateNew);
              }
              catch { }
            }
            //
            //IKCMS_ModelCMS_ModelInfo_Interface itemModelInfo = null;
            bool modeExt = false;
            try { modeExt = AllowedTypeNames.Select(t => IKCMS_ModelCMS_Provider.Provider.FindBestModelMatch(Utility.FindTypeCached(t))).Where(mi => mi != null).SelectMany(mi => mi.Attributes.OfType<IKCMS_ModelCMS_fsNodeModeAttribute>().Select(a => a.vfsNodeFetchMode)).Any(m => m == vfsNodeFetchModeEnum.vNode_vData_iNode_Extra); }
            catch { }
            //
            if (FetchMode == FetchModeEnum.rNodeFetch)
            {
              rNodes.RemoveAll(n => DataStorageCached.brokenNodes.Contains(n));
            }
            _EventModels = rNodes.Select(n =>
            {
              IKCMS_ModelCMS_GenericBrickInterface mdl = IKCMS_ModelCMS_Provider.Provider.ModelBuildGenericByRNODE(n) as IKCMS_ModelCMS_GenericBrickInterface;
              if (mdl == null)
              {
                DataStorageCached.brokenNodes.Add(n);
              }
              //TODO: non e' il massimo come sistem se poi resta tutto in cache...
              if (Model is IKCMS_ModelCMS_Page_Interface)
              {
                if (mdl is IKCMS_ModelCMS_ArchiveBrowserItem_Interface && mdl.ModelParent == null)
                {
                  (mdl as IKCMS_ModelCMS_ArchiveBrowserItem_Interface).ModelContainerUnBinded = Model;
                }
              }
              return mdl;
            }).Where(m => m != null).OrderBy(m => m.DateNode).ThenBy(m => m.DateNodeAux ?? m.DateNode).ThenBy(m => m.Position).ThenBy(m => m.sNode).ToList();
            //
          }
        }
        catch { }
        return _EventModels;
      }
    }

    public virtual IEnumerable<IKCMS_ModelCMS_GenericBrickInterface> EventModelsWithSelectedItemAsFirst
    {
      get
      {
        if (ModelInput != null && ModelInput != Model && EventModels != null)
        {
          int node = ModelInput.sNode;
          return EventModels.Where(m => m.sNode == node).Concat(EventModels.Where(m => m.sNode != node));
        }
        return EventModels;
      }
    }


    [Flags]
    public enum RenderCalendarFlagsEnum { Default = 1 + 2 + 8, AppendUrlBack = 1 << 0, AjaxUpdate = 1 << 1, ShortMonthInHeader = 1 << 2, BlockCrawlers = 1 << 3 }

    public virtual MvcHtmlString RenderCalendar(string cssClass) { return RenderCalendar(cssClass, null); }
    public virtual MvcHtmlString RenderCalendar(string cssClass, RenderCalendarFlagsEnum? flags)
    {
      StringBuilder result = new StringBuilder();
      try
      {
        //
        flags = flags ?? RenderCalendarFlagsEnum.Default;
        var today = Ikon.GD.FS_OperationsHelpers.DateTimeSession.Date;
        //
        string extraCssClass = string.Empty;
        string extraCssClassHeader = string.Empty;
        if ((flags & RenderCalendarFlagsEnum.AjaxUpdate) == RenderCalendarFlagsEnum.AjaxUpdate)
        {
          extraCssClass += " ajaxUpdate";
          extraCssClassHeader += " ajaxUpdate";
        }
        if ((flags & RenderCalendarFlagsEnum.AppendUrlBack) == RenderCalendarFlagsEnum.AppendUrlBack)
        {
          extraCssClass += " append_UrlBack";
        }
        //
        DateTime? forcedDate = null;
        if (ModelInput != null && ModelInput != Model)
          forcedDate = ModelInput.DateNode;
        var daycurrent = GetCurrentDate(forcedDate);
        result.Append("<div class='{0}'>".FormatString(cssClass));
        //header
        result.Append("<div class='header'>");
        try
        {
          var url_prev = UrlMonthPrev;
          var url_next = UrlMonthNext;
          string blockCrawlers = ((flags & RenderCalendarFlagsEnum.BlockCrawlers) == RenderCalendarFlagsEnum.BlockCrawlers) ? " rel='nofollow'" : string.Empty;
          result.Append("<a href='{0}' class='prev{1}'{2}></a>".FormatString((url_prev ?? "javascript:;").EncodeAsAttributeUrl(), url_prev.IsNotEmpty() ? extraCssClassHeader : string.Empty, blockCrawlers));
          result.Append("<div class='mese'>" + daycurrent.ToString("MMMM yyyy") + "</div>");
          result.Append("<a href='{0}' class='next{1}'{2}></a>".FormatString((url_next ?? "javascript:;").EncodeAsAttributeUrl(), url_next.IsNotEmpty() ? extraCssClassHeader : string.Empty, blockCrawlers));
        }
        catch { }
        result.Append("</div><div class='clearfloat'></div>");
        //calendario
        var indexGiorni = 1;
        result.Append("<div class='calendario_giorni'><table>");
        result.Append("<tr class='header_giorni'>");
        var lun = System.Globalization.CultureInfo.CurrentCulture.DateTimeFormat.DayNames[1];
        result.Append("<td><span>" + char.ToUpper(lun[0]) + lun[1] + "</span></td>");
        var mar = System.Globalization.CultureInfo.CurrentCulture.DateTimeFormat.DayNames[2];
        result.Append("<td><span>" + char.ToUpper(mar[0]) + mar[1] + "</span></td>");
        var mer = System.Globalization.CultureInfo.CurrentCulture.DateTimeFormat.DayNames[3];
        result.Append("<td><span>" + char.ToUpper(mer[0]) + mer[1] + "</span></td>");
        var gio = System.Globalization.CultureInfo.CurrentCulture.DateTimeFormat.DayNames[4];
        result.Append("<td><span>" + char.ToUpper(gio[0]) + gio[1] + "</span></td>");
        var ven = System.Globalization.CultureInfo.CurrentCulture.DateTimeFormat.DayNames[5];
        result.Append("<td><span>" + char.ToUpper(ven[0]) + ven[1] + "</span></td>");
        var sab = System.Globalization.CultureInfo.CurrentCulture.DateTimeFormat.DayNames[6];
        result.Append("<td><span>" + char.ToUpper(sab[0]) + sab[1] + "</span></td>");
        var dom = System.Globalization.CultureInfo.CurrentCulture.DateTimeFormat.DayNames[0];
        result.Append("<td><span>" + char.ToUpper(dom[0]) + dom[1] + "</span></td>");
        result.Append("</tr>");
        //
        try
        {
          int? snode_parent = null;
          if (Model != null && Model is IKCMS_ModelCMS_Page_Interface)
          {
            snode_parent = Model.sNode;
          }
          var daysData = GetNodesForCalendar(forcedDate);
          for (var i = 0; i < 42; i++)
          {
            if (i % 7 == 0) result.Append("<tr>");
            result.Append("<td>");
            if (indexGiorni <= DateTime.DaysInMonth(daycurrent.Year, daycurrent.Month))
            {
              var giorno = new DateTime(daycurrent.Year, daycurrent.Month, indexGiorni);
              if ((int)giorno.DayOfWeek == (i + 1) % 7 || (i == 6 && (int)giorno.DayOfWeek == 0))
              {
                string url = null;
                var vclass = string.Empty;
                var dayData = daysData.Where(d => d.Key.Date == giorno.Date).FirstOrDefault();
                int count = 0;
                if (dayData != null)
                {
                  vclass = "active";
                  url = null;
                  count = dayData.Count();
                  if (dayData.Count() > 1)
                  {
                    url = Utility.UriSetQuery(Utility.UriSetQuery(UrlBase, ParameterNameDateActive, giorno.ToString("yyyy-MM-dd")), ParameterNameViewMode, ViewMode_Day);
                  }
                  else
                  {
                    url = IKCMS_RouteUrlManager.GetMvcUrlGeneralV2(null, dayData.FirstOrDefault().sNode, snode_parent, null, false);
                  }
                }
                result.Append(string.Format("<a href='{0}' class='giorno {1}{4}{6}' data='{2}' title='{5}'>{3}</a>", (url ?? "javascript:;").EncodeAsAttributeUrl(), vclass, giorno.ToUniversalTime(), indexGiorni, url.IsNotEmpty() ? extraCssClass : string.Empty, count, giorno.Date == today ? " today" : ""));
                indexGiorni++;
              }
            }
            result.Append("</td>");
            if (i % 7 == 6) result.Append("</tr>");
          }
        }
        catch { }
        //
        result.Append("</table></div>");
        result.Append("</div>");
        //
      }
      catch { }
      return MvcHtmlString.Create(result.ToString());
    }



    public class NodeInfo
    {
      public int sNode { get; set; }
      public int rNode { get; set; }
      public string manager_type { get; set; }
      public string category { get; set; }
      public DateTime date_node { get; set; }
      public DateTime? date_node_aux { get; set; }
    }


    //
    // min/max dates from filtered data
    //
    public class CustomDataEvents
    {
      public DateTime? dateMin { get; set; }
      public DateTime? dateMax { get; set; }
      public DateTime? dateMinMonth { get; set; }
      public DateTime? dateMaxMonth { get; set; }
    }

  }


  //
  // Gestione eventi di base
  //
  [DescriptionAttribute("Aggregatore di eventi standard con filtro a Tags")]
  public class ManagerTagFilterEventsSimple : ManagerTagFilterEventsBase
  {
    public ManagerTagFilterEventsSimple(IKCMS_ModelCMS_Interface model)
      : base(model)
    {
      AllowedCategories = new List<string> { "Event" };
    }
  }


}
