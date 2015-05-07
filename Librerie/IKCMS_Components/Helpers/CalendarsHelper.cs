using System;
using System.Collections;
using System.Collections.Specialized;
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
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Reflection;
using System.Web.Mvc;
using System.Web.Mvc.Ajax;
//using Microsoft.Web.Mvc;
using Autofac;
using LinqKit;

using Ikon;
using Ikon.Support;
using Ikon.GD;
using Ikon.IKCMS;
using Ikon.IKGD.Library.Resources;
using Ikon.IKCMS.Library.Resources;
using Ikon.IKGD.Library.Collectors;


namespace Ikon.IKCMS.Helpers
{

  public static class CalendarHelpers
  {


    public static JsonResult GetEventsForCalendars(ControllerContext context, int? sNodeBrowserWidget, int? year, int? month)
    {
      // costruzione completa del model per attivare il build completo anche del parent se non specificato negli args
      IKCMS_ModelCMS_Interface model = null;
      List<object> events = null;
      if (sNodeBrowserWidget != null)
        model = IKCMS_ModelCMS_Provider.Provider.ModelBuildGeneric(sNodeBrowserWidget.Value, null, null, null, context);
      if (model is IKCMS_ModelCMS_TeaserNewsEventi_Interface)
        events = (model as IKCMS_ModelCMS_TeaserNewsEventi_Interface).GetCalendarEventsAjax(year, month, null);
      else if (model is IKCMS_ModelCMS_ArchiveBrowser_Interface)
        events = (model as IKCMS_ModelCMS_ArchiveBrowser_Interface).GetCalendarEventsAjax(year, month, null);
      else if (model != null && model.ModelRoot is IKCMS_ModelCMS_ArchiveBrowser_Interface)
        events = (model.ModelRoot as IKCMS_ModelCMS_ArchiveBrowser_Interface).GetCalendarEventsAjax(year, month, null);
      else if (sNodeBrowserWidget == null)
        events = GetCalendarEvents4PageAjax(year, month, null);
      //
      JsonResult result = new JsonResult();
      result.JsonRequestBehavior = JsonRequestBehavior.AllowGet;
      result.Data = new { entries = events }; ;
      //string tmp01 = Newtonsoft.Json.JsonConvert.SerializeObject(result.Data);
      return result;
    }



    //
    // generazione delle informazioni necessarie al rendering degli eventi per il calendario
    // per i moduli tipo IKCMS_ModelCMS_Event4PageItem_Interface
    //
    public static List<IKCMS_ModelCMS_Event4PageItem_Interface> GetCalendarItemsEvents4Page(DateTime? dateRef, int? maxItems, IEnumerable<int> sNodesItems)
    {
      //
      List<IKCMS_ModelCMS_Event4PageItem_Interface> events = new List<IKCMS_ModelCMS_Event4PageItem_Interface>();
      //
      try
      {
        //
        FS_Operations fsOp = IKCMS_ManagerIoC.requestContainer.ResolveNamed<FS_Operations>("readonly");
        //FS_Operations fsOp = IKCMS_ManagerIoC.requestContainer.Resolve<FS_Operations>();
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
        IKGD_Archive_Filter_Interface itemsCollector = new IKGD_Archive_Filter_DateRange();
        //
        Expression<Func<IKGD_VDATA, bool>> vDataFilterMain = fsOp.Get_vDataFilterACLv2();
        Expression<Func<IKGD_VNODE, bool>> vNodeFilterMain = fsOp.Get_vNodeFilterACLv2();
        //
        vDataFilterMain = vDataFilterMain.And(n => n.manager_type == typeof(IKCMS_ResourceType_Event4PageKVT).Name);
        //
        if (sNodesItems != null)
        {
          // solo gli eventi selezionati
          vNodeFilterMain = vNodeFilterMain.And(n => sNodesItems.Contains(n.snode));
        }
        else if (dateRef != null)
        {
          // eventi del giorno
          maxItems = int.MaxValue;
          DateTime dateStart = dateRef.Value;
          DateTime dateEnd = dateStart.AddDays(1);
          vDataFilterMain = vDataFilterMain.And(n => n.date_node < dateEnd && (n.date_node >= dateStart || (n.date_node_aux != null && n.date_node_aux.Value >= dateStart)));
        }
        else
        {
          // solo gli eventi in corso o prossimi (max 10 per default)
          maxItems = maxItems ?? 10;
          DateTime dateStart = FS_OperationsHelpers.DateTimeSession;
          vDataFilterMain = vDataFilterMain.And(n => n.date_node >= dateStart || (n.date_node_aux != null && n.date_node_aux.Value >= dateStart));
        }
        //
        maxItems = maxItems ?? int.MaxValue;
        IQueryable<FS_Operations.FS_NodeInfo_Interface> resourcesAll = fetcher(fsOp, vNodeFilterMain, vDataFilterMain);
        var resources = resourcesAll.GroupBy(n => n.vNode.rnode).Select(g => g.First());
        resources = resources.OrderBy(n => n.vData.date_node).ThenBy(n => n.vData.date_node_aux ?? n.vData.date_node).ThenBy(n => n.vNode.snode).Take(maxItems.Value);
        //
        IKCMS_ModelCMS_ModelInfo_Interface itemModelInfo = null;
        foreach (var fsNode in resources)
        {
          itemModelInfo = itemModelInfo ?? IKCMS_ModelCMS_Provider.Provider.FindBestModelMatch(Utility.FindTypeCached(fsNode.vData.manager_type));
          IKCMS_ModelCMS_Event4PageItem_Interface model = IKCMS_ModelCMS_Provider.Provider.ModelBuild(null, fsNode, itemModelInfo) as IKCMS_ModelCMS_Event4PageItem_Interface;
          if (model != null)
            events.Add(model);
        }
        //
      }
      catch { }
      return events;
    }


    //
    // generazione delle informazioni necessarie al rendering del calendar e dei relativi items
    // per i moduli tipo IKCMS_ModelCMS_Event4PageItem_Interface
    //
    public static List<object> GetCalendarEvents4PageAjax(int? year, int? month, int? maxItems)
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
        //FS_Operations fsOp = IKCMS_ManagerIoC.requestContainer.ResolveNamed<FS_Operations>("readonly");
        FS_Operations fsOp = IKCMS_ManagerIoC.requestContainer.Resolve<FS_Operations>();
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
        IKGD_Archive_Filter_Interface itemsCollector = new IKGD_Archive_Filter_DateRange();
        //
        Expression<Func<IKGD_VDATA, bool>> vDataFilterMain = fsOp.Get_vDataFilterACLv2();
        Expression<Func<IKGD_VNODE, bool>> vNodeFilterMain = fsOp.Get_vNodeFilterACLv2();
        //
        vDataFilterMain = vDataFilterMain.And(n => n.manager_type == typeof(IKCMS_ResourceType_Event4PageKVT).Name);
        //
        DateTime dateMonthStart = new DateTime(dateRef.Year, dateRef.Month, 1);
        DateTime dateMonthEnd = dateMonthStart.AddMonths(1);
        Expression<Func<IKGD_VDATA, bool>> vDataFilterMainMonth = vDataFilterMain.And(n => n.date_node < dateMonthEnd && (n.date_node >= dateMonthStart || (n.date_node_aux != null && n.date_node_aux.Value >= dateMonthStart)));
        //
        IQueryable<FS_Operations.FS_NodeInfo_Interface> resourcesAll = fetcher(fsOp, vNodeFilterMain, vDataFilterMainMonth);
        var resources = resourcesAll.GroupBy(n => n.vNode.rnode).Select(g => g.First());
        resources = resources.OrderBy(n => n.vData.date_node).ThenBy(n => n.vData.date_node_aux ?? n.vData.date_node).ThenBy(n => n.vNode.snode).Take(maxItems.Value);
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

  }

}