using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;
using System.Web;
using System.Web.Mvc;
using System.Web.Mvc.Ajax;
using System.IO;
using System.Web.Security;
using System.Net.Mail;
using System.Security.Cryptography;
using System.Text;
using System.Data;
using System.Data.Common;
using System.Transactions;
using Microsoft.Web.Mvc;
using Newtonsoft.Json;
using LinqKit;
using Autofac;

using Ikon;
using Ikon.Config;
using Ikon.GD;
using Ikon.IKCMS;
using Ikon.IKCMS.Library.Resources;
using Ikon.IKGD.Library.Resources;
using System.Web.Hosting;


namespace IkonWeb.Controllers
{

  [AuthorizeOR()]
  [RobotsDeny()]
  [ControllerSessionState(ControllerSessionState.ReadOnly)]
  //[SessionState(System.Web.SessionState.SessionStateBehavior.ReadOnly)]
  public class AdminCMSController : Controller
  {
    public FS_Operations fsOp_ro { get { return IKCMS_ManagerIoC.requestContainer.ResolveNamed<FS_Operations>("readonly"); } }
    public FS_Operations fsOp { get { return IKCMS_ManagerIoC.requestContainer.Resolve<FS_Operations>(); } }


    public ActionResult Index()
    {
      bool HasACL = false;
      ViewData["Language"] = IKGD_Language_Provider.Provider.LanguageNN;
      ViewData["VersionFrozen"] = Ikon.GD.FS_OperationsHelpers.VersionFrozenSession;
      ViewData["IsRoot"] = Ikon.GD.FS_OperationsHelpers.IsRoot;
      ViewData["CachedAreas"] = Utility.Implode(Ikon.GD.FS_OperationsHelpers.CachedAreasExtended.AreasAllowed, ", ");
      ViewData["HasACL"] = HasACL = FS_ACL_Reduced.HasOperatorACLs();

      if (HttpContext.User.Identity.IsAuthenticated)
      {
        return View("~/Views/AdminCMS/Index");
      }
      else
      {
        return PartialView("~/Views/AdminCMS/Info");
      }
    }


    public ActionResult Info()
    {
      bool HasACL = false;
      ViewData["Language"] = IKGD_Language_Provider.Provider.LanguageNN;
      ViewData["VersionFrozen"] = Ikon.GD.FS_OperationsHelpers.VersionFrozenSession;
      ViewData["IsRoot"] = Ikon.GD.FS_OperationsHelpers.IsRoot;
      ViewData["CachedAreas"] = Utility.Implode(Ikon.GD.FS_OperationsHelpers.CachedAreasExtended.AreasAllowed, ", ");
      ViewData["HasACL"] = HasACL = FS_ACL_Reduced.HasOperatorACLs();
      return PartialView("~/Views/AdminCMS/Info");
    }


    public ActionResult Panel()
    {
      return Redirect("~/Membership/Admin.aspx");
    }


    public ActionResult SetVersionFrozen(int version)
    {
      Ikon.GD.FS_OperationsHelpers.VersionFrozenSession = version;
      if (HttpContext.Request.UrlReferrer != null)
        return Redirect(HttpContext.Request.UrlReferrer.ToString());
      return RedirectToAction("Index");
    }


    public ActionResult Logout()
    {
      string returnToUrl = AuthControllerBase.LogoutWorker(null);
      return Redirect(returnToUrl);
    }


    public static string GetUrlStatistiche()
    {
      string url = null;
      if (!string.IsNullOrEmpty(IKGD_Config.AppSettings["WebStatsCode"]))
      {
        string hash = Utility.HashMD5(IKGD_Config.AppSettings["WebStatsCode"] + "cane");
        string baseUrl = IKGD_Config.AppSettings["WebStatsBaseUrl"] ?? "http://nlb1stats.ikon.it/cgi-bin/awstats.pl";
        url = string.Format("{2}?config={0}&auth_code={1}", IKGD_Config.AppSettings["WebStatsCode"], hash, baseUrl);
      }
      return url;
    }


    [AcceptVerbs(HttpVerbs.Get)]
    [AuthorizeLocal]
    [AuthorizeOR(Users = "root")]
    public ActionResult ConfigImport()
    {
      IKGD_Config.ImportXML();
      ViewData["message"] = "Configurazione del database importata.";
      return View("Index");
    }


    [AcceptVerbs(HttpVerbs.Get)]
    [AuthorizeLocal]
    [AuthorizeOR(Users = "root")]
    public ActionResult ConfigExport()
    {
      IKGD_Config.ExportXML();
      ViewData["message"] = "Configurazione del database esportata.";
      return View("Index");
    }


    [AcceptVerbs(HttpVerbs.Get)]
    [AuthorizeAclCMS(NoRedirectToAuth = true)]
    [AuthorizeLocalOrRoot(NoRedirectToAuth = true)]
    public ActionResult ResetCache()
    {
      IKGD_Config.Clear();
      var messages = Ikon.GD.FS_OperationsHelpers.CacheClear(null);
      messages.Add("CMS Cache reset completed.");
      //if (HttpContext.Request.UrlReferrer != null)
      //  return Redirect(HttpContext.Request.UrlReferrer.ToString());
      ViewData["message"] = Utility.Implode(messages, "<br/>\n");
      return View("Index");
    }


    [AcceptVerbs(HttpVerbs.Get)]
    [AuthorizeAclCMS(NoRedirectToAuth = true)]
    [AuthorizeLocalOrRoot(NoRedirectToAuth = true)]
    public ActionResult GCCollect()
    {
      List<string> messages = new List<string>();
      try
      {
        messages.Add(string.Format("Items in cache: {0}", HttpRuntime.Cache.Count));
        messages.Add(string.Format(FileSizeFormatProvider.Factory(), "Total managed memory in GC before cleaning: {0:fs}", System.GC.GetTotalMemory(false)));
        for (int i = 0; i <= System.GC.MaxGeneration; i++)
        {
          // chiamate multiple per consentire la promozione fino alla generazione massima e quindi effettivamente deallocare gli oggetti dal GC
          System.GC.Collect();
        }
        System.GC.Collect(System.GC.MaxGeneration);
        System.GC.WaitForPendingFinalizers();
        System.GC.Collect(System.GC.MaxGeneration);
        messages.Add(string.Format(FileSizeFormatProvider.Factory(), "Total managed memory in GC after collect: {0:fs}", System.GC.GetTotalMemory(false)));
      }
      catch (Exception ex) { messages.Add(ex.Message); }
      return Content(Utility.Implode(messages, "<br/>\n"), "text/html", Encoding.UTF8);
    }


    [AcceptVerbs(HttpVerbs.Get)]
    [AuthorizeLocalOrRoot]
    public ActionResult RecycleAppPoolIIS()
    {
      Ikon.GD.FS_OperationsHelpers.CacheClear(null);
      Ikon.Support.ApplicationPoolRecycle.RecycleCurrentApplicationPool();
      ViewData["message"] = "Web server riavviato.";
      return View("Index");
    }


    public ActionResult FlushHitLog()
    {
      IKCMS_HitLogger.Flush(true);
      if (HttpContext.Request.UrlReferrer != null)
        return Redirect(HttpContext.Request.UrlReferrer.ToString());
      ViewData["message"] = "HitLog Cache salvata su DB.";
      return Index();
    }


    public ActionResult UpdateHitsStats()
    {
      IKCMS_HitLogger.UpdateHitsStats(true);
      if (HttpContext.Request.UrlReferrer != null)
        return Redirect(HttpContext.Request.UrlReferrer.ToString());
      ViewData["message"] = "HitLog Cache salvata su DB e statistiche aggiornate.";
      return Index();
    }


    [AcceptVerbs(HttpVerbs.Get)]
    [AuthorizeLocalOrRoot]
    public ActionResult SQL_CacheInvalidationSetup()
    {
      List<string> messages = new List<string>();
      try
      {
        //
        // Standard VFS cache invalidation support
        //
        // NB:attenzione alla connection string che deve contenere il frammento:
        // Persist Security Info=False;
        // altrimenti fsOp.DB.Connection.ConnectionString viene passata senza la password e non si connette al DB!
        //
        messages.AddRange(Ikon.GD.IKGD_VFS_Helpers.RegisterSqlCacheDependencyInvalidation());
        //
        using (Ikon.GD.IKGD_DataContext DB = Ikon.GD.IKGD_DBH.GetDB())
        {
          string[] tables = Utility.Explode(IKGD_Config.AppSettings["DB_TablesEnabledForNotifications"], ",", " ", true).ToArray();
          //System.Web.Caching.SqlCacheDependencyAdmin.EnableNotifications(DB.Connection.ConnectionString);
          foreach (string table in tables)
          {
            try { System.Web.Caching.SqlCacheDependencyAdmin.EnableTableForNotifications(DB.Connection.ConnectionString, new string[] { table }); }
            catch (Exception ex) { messages.Add(ex.Message); }
          }
          messages.AddRange(System.Web.Caching.SqlCacheDependencyAdmin.GetTablesEnabledForNotifications(DB.Connection.ConnectionString));
        }
      }
      catch (Exception ex)
      {
        messages.Add(ex.Message);
      }
      return Content(Utility.Implode(messages.Distinct(), "<br/>\n"));
    }


    [AcceptVerbs(HttpVerbs.Get)]
    [AuthorizeLocalOrRoot()]
    public ActionResult SearchEngineUpdate()
    {
      try
      {
        using (Ikon.Indexer.LuceneIndexer indexer = new Ikon.Indexer.LuceneIndexer(true))
        {
          int res = indexer.IKGD_IndexUpdate();
          if (res < 0)
            throw new Exception("La funzione di update degli indici di ricerca ha fornito il codice di errore: " + res.ToString());
          ViewData["message"] = string.Format("Operazione completata con successo. \nSono state aggiornate {0} risorse.", res);
        }
      }
      catch (Exception ex)
      {
        ViewData["error"] = ex.Message;
      }
      return View("Index");
    }


    [AcceptVerbs(HttpVerbs.Get)]
    [AuthorizeLocalOrRoot()]
    public ActionResult SearchEngineRebuild() { return SearchEngineRebuildWorker(false); }
    [AcceptVerbs(HttpVerbs.Get)]
    [AuthorizeLocalOrRoot()]
    public ActionResult SearchEngineRebuildFull() { return SearchEngineRebuildWorker(true); }

    [AcceptVerbs(HttpVerbs.Get)]
    [AuthorizeLocalOrRoot()]
    public ActionResult SearchEngineRebuildWorker(bool? clearStreams)
    {
      try
      {
        using (Ikon.Indexer.LuceneIndexer indexer = new Ikon.Indexer.LuceneIndexer(true))
        {
          if (clearStreams.GetValueOrDefault(false))
            indexer.IKGD_ClearAllStreams();
          int res = indexer.IKGD_ReindexAll();
          ViewData["message"] = string.Format("Operazione completata con successo. \nSono state aggiornate {0} risorse.", res);
        }
      }
      catch (Exception ex)
      {
        ViewData["error"] = ex.Message;
      }
      return View("Index");
    }


    [AcceptVerbs(HttpVerbs.Get)]
    [AuthorizeLocalOrRoot()]
    public ActionResult SearchEngineOptimize()
    {
      try
      {
        using (Ikon.Indexer.LuceneIndexer indexer = new Ikon.Indexer.LuceneIndexer(true))
        {
          indexer.IKGD_IndexOptimize(true);
          ViewData["message"] = "Operazione completata con successo.";
        }
      }
      catch (Exception ex)
      {
        ViewData["error"] = ex.Message;
      }
      return View("Index");
    }


    [AcceptVerbs(HttpVerbs.Get)]
    [AuthorizeLocalOrRoot()]
    public ActionResult SearchEngineCleanDB()
    {
      try
      {
        using (Ikon.Indexer.LuceneIndexer indexer = new Ikon.Indexer.LuceneIndexer(true))
        {
          int res = indexer.IKGD_LuceneStreamsCleaner();
          ViewData["message"] = string.Format("Operazione completata con successo. \nSono stati eliminati {0} stream di lucene.", res);
        }
      }
      catch (Exception ex)
      {
        ViewData["error"] = ex.Message;
      }
      return View("Index");
    }


    [AcceptVerbs(HttpVerbs.Get)]
    [AuthorizeLocalOrRoot()]
    public ActionResult IKGD_QueueRun()
    {
      try
      {
        //
        bool result = IKGD_QueueManager.QueueMonitorWorker();
        //
      }
      catch (Exception ex)
      {
        ViewData["error"] = ex.Message;
      }
      return View("Index");
    }


    [AcceptVerbs(HttpVerbs.Get)]
    [AuthorizeLocalOrRoot()]
    public ActionResult IKGD_QueueNotify(int? delay)
    {
      try
      {
        //
        IKGD_QueueManager.NotifyNewEntry((double?)delay);
        //
      }
      catch (Exception ex)
      {
        ViewData["error"] = ex.Message;
      }
      return View("Index");
    }


    [AcceptVerbs(HttpVerbs.Get)]
    [AuthorizeLocalOrRoot()]
    public ActionResult FindDegeneratePathsCMS()
    {
      List<string> messages = new List<string>();
      try
      {
        //
        List<IKGD_Path> pathsAll = new List<IKGD_Path>();
        //
        var rNodes = fsOp.NodesActive<IKGD_VNODE>().Where(n => n.flag_folder).Select(n => n.rnode).Distinct().ToList();
        foreach (var slice in rNodes.Slice(100))
        {
          pathsAll.AddRange(fsOp.PathsFromNodesAuthor(null, slice, true, false, false, false).FilterPathsByRootsCMS());
        }
        //
        foreach (var pathsGrp in pathsAll.GroupBy(p => p.rNode))
        {
          if (pathsGrp.Select(p => Utility.Implode(p.Fragments.Select(f => f.rNode), ",")).Distinct().Count() <= 1)
            continue;
          if (pathsGrp.GroupBy(p => p.sNode).All(g => g.Count() == 1))
            continue;
          messages.Add("<hr/>\n");
          foreach (var path in pathsGrp)
            messages.Add(string.Format("{0}  [sNode={1}]", path.ToString(), path.sNode));
        }
      }
      catch (Exception ex)
      {
        messages.Add(ex.Message);
      }
      return Content(Utility.Implode(messages, "<br/>\n"));
    }


    [AcceptVerbs(HttpVerbs.Get)]
    [AuthorizeLocalOrRoot()]
    public ActionResult FindDisjointPathsCMS()
    {
      List<string> messages = new List<string>();
      try
      {
        //
        List<IKGD_Path> pathsAll = new List<IKGD_Path>();
        //
        var fsNodes =
          (from vNode in fsOp.DB.IKGD_VNODEs.Where(n => (n.flag_published || n.flag_current) && !n.flag_deleted)
           from vData in fsOp.DB.IKGD_VDATAs.Where(n => (n.flag_published || n.flag_current) && !n.flag_deleted)
           where (vNode.rnode == vData.rnode)
           where (vNode.flag_published == true && vData.flag_published == true) || (vNode.flag_current == true && vData.flag_current == true)
           select new { vNode.snode, vNode.rnode, flag_published = (vNode.flag_published == true && vData.flag_published == true), flag_current = (vNode.flag_current == true && vData.flag_current == true), vNode.flag_folder, vNode.parent, version_vnode = vNode.version, version_vdata = vData.version }).ToList();
        //
        var nodes_duplicated = fsNodes.GroupBy(r => new { r.snode, r.flag_published, r.flag_current }).Where(g => g.Select(r => new { r.version_vnode, r.version_vdata }).Count() > 1).SelectMany(g => g).OrderBy(r => r.snode).ThenBy(r => r.flag_published).ThenBy(r => r.flag_current).ToList();
        var nodes_mismatchedParent = fsNodes.GroupBy(r => r.snode).Where(g => g.Select(r => r.parent).Distinct().Count() > 1).SelectMany(g => g).OrderBy(r => r.snode).ToList();
        //
        messages.Add("Nodi con possibili duplicati (pubblicato o preview): {0}".FormatString(nodes_duplicated.Count));
        messages.AddRange(nodes_duplicated.Select(r => r.ToString()));
        messages.Add("Nodi con parents differenti tra pubblicato e preview: {0}".FormatString(nodes_mismatchedParent.Count));
        messages.AddRange(nodes_mismatchedParent.Select(r => r.ToString()));
        //
      }
      catch (Exception ex)
      {
        messages.Add(ex.Message);
      }
      return Content(Utility.Implode(messages, "<br/>\n"));
    }


    [AcceptVerbs(HttpVerbs.Get)]
    [AuthorizeLocalOrRoot()]
    public ActionResult GetResourcesUsage()
    {
      List<string> messages = new List<string>();
      try
      {
        //
        long size_site = IKGD_ExternalVFS_Support.GetSiteStorageUsedSpace();
        long size_extfs = IKGD_ExternalVFS_Support.GetExternalStorageUsedSpace();
        //
        messages.Add(string.Format(FileSizeFormatProvider.Factory(), "WebSite FileSystem Size: {0:fs}", size_site));
        messages.Add(string.Format(FileSizeFormatProvider.Factory(), "WebSite FileSystem External Storage Size: {0:fs}", size_extfs));
        //
        //SELECT physical_name,size*8192 as bytes FROM sys.database_files;
        var sizes_DB = Utility.DataTable2List(Utility.GetTableAutoSimple(IKGD_DBH.ConnectionStringName, "SELECT physical_name,size as bytes FROM sys.database_files"));
        foreach (var size_DB in sizes_DB)
        {
          string physical_name = (size_DB["physical_name"] ?? string.Empty).Split('/', '\\').LastOrDefault();
          long bytes = Utility.TryParse<long>(size_DB["bytes"]) * 8192;
          messages.Add(string.Format(FileSizeFormatProvider.Factory(), "Database File:{0} Size: {1:fs}", physical_name, bytes));
        }
        //
      }
      catch (Exception ex)
      {
        messages.Add(ex.Message);
      }
      return Content(Utility.Implode(messages, "<br/>\n"));
    }


    [AcceptVerbs(HttpVerbs.Get)]
    [AuthorizeLocalOrRoot()]
    public ActionResult ClearUnmappedExternalResources()
    {
      List<string> messages = new List<string>();
      try
      {
        using (IKGD_ExternalVFS_Support extFS = new IKGD_ExternalVFS_Support())
        {
          messages.AddRange(extFS.ClearUnmappedExternalResources(true, false));
        }
      }
      catch (Exception ex)
      {
        messages.Add(ex.Message);
      }
      return Content(Utility.Implode(messages, "<br/>\n"));
    }


    //
    // Testing delle funzionalita' DTC (Distributed Transaction Coordinator) dcomcnfg.exe
    //
    [AcceptVerbs(HttpVerbs.Get)]
    [AuthorizeLocalOrRoot()]
    public ActionResult TestDTC(bool? committ)
    {
      List<string> messages = new List<string>();
      try
      {
        messages.AddRange(TestDTC_Worker(committ.GetValueOrDefault(false)));
        messages.Add("TestDTC completato.");
      }
      catch (Exception ex)
      {
        messages.Add(ex.Message);
      }
      return Content(Utility.Implode(messages, "<br/>\n"));
    }


    [NonAction]
    public List<string> TestDTC_Worker(bool CommittTransactions)
    {
      List<string> messages = new List<string>();
      try
      {
        FS_Operations fsOp = null;
        using (fsOp = new FS_Operations(-1, false, true, true))
        {
          //
          fsOp.DB.CommandTimeout = 3600;
          fsOp.Language = null;
          fsOp.EnsureOpenConnection();
          //
          int fake01 = IKCAT_AttributeStorage.AttributesNoACL.Count(a => a.AttributeType == "test_dtc");
          //
          using (IKGD_DataContext DC = new IKGD_DataContext())
          {
            //
            DC.CommandTimeout = 3600;
            //
            using (System.Transactions.TransactionScope ts = IKGD_TransactionFactory.Transaction(3600))
            {
              IKCMS_ApplicationStatus.StatusSet("batch_progress", "processing product_main reading data");
              //
              {
                fsOp.DB.IKCAT_Attributes.InsertOnSubmit(new IKCAT_Attribute { AttributeType = "test_dtc", AttributeCode = Guid.NewGuid().ToString() });
                var chg = fsOp.DB.GetChangeSet();
                messages.Add(string.Format("TestDTC: changes={0}/{1}/{2}", chg.Inserts.Count, chg.Updates.Count, chg.Deletes.Count));
                fsOp.DB.SubmitChanges();
                var tmp01 = fsOp.DB.IKCAT_Attributes.Count(a => a.AttributeType == "test_dtc");
              }
              IKCAT_AttributeStorage.Reset();
              int fake02 = IKCAT_AttributeStorage.AttributesNoACL.Count(a => a.AttributeType == "test_dtc");
              {
                fsOp.DB.IKCAT_Attributes.InsertOnSubmit(new IKCAT_Attribute { AttributeType = "test_dtc", AttributeCode = Guid.NewGuid().ToString() });
                var chg = fsOp.DB.GetChangeSet();
                messages.Add(string.Format("TestDTC: changes={0}/{1}/{2}", chg.Inserts.Count, chg.Updates.Count, chg.Deletes.Count));
                fsOp.DB.SubmitChanges();
                var tmp02 = fsOp.DB.IKCAT_Attributes.Count(a => a.AttributeType == "test_dtc");
              }
              var fakeAttrs = fsOp.DB.IKCAT_Attributes.Count(a => a.AttributeType == "test_dtc");
              //
              if (CommittTransactions)
                ts.Committ();
              //
            }
          }
        }
      }
      catch (Exception ex)
      {
        messages.Add(ex.Message); messages.Add(ex.StackTrace);
        Elmah.ErrorSignal.FromCurrentContext().Raise(ex);
      }
      //
      try
      {
        FS_Operations fsOp = null;
        using (fsOp = new FS_Operations(-1, false, true, true))
        {
          var fakeAttrs = fsOp.DB.IKCAT_Attributes.Count(a => a.AttributeType == "test_dtc");
          messages.Add(string.Format("FINAL: fakeAttrs={0}", fakeAttrs));
        }
      }
      catch (Exception ex)
      {
        messages.Add(ex.Message); messages.Add(ex.StackTrace);
        Elmah.ErrorSignal.FromCurrentContext().Raise(ex);
      }
      return messages;
    }




  }
}
