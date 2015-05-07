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

  [RobotsDeny()]
  [ControllerSessionState(ControllerSessionState.ReadOnly)]
  //[SessionState(System.Web.SessionState.SessionStateBehavior.ReadOnly)]
  public class BatchCMSController : Controller
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

      return View("~/Views/AdminCMS/Index");
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



    public ActionResult BatchStatus()
    {
      return View("~/Views/AdminCMS/BatchStatus");
    }


    public ActionResult SetVersionFrozen(int version)
    {
      Ikon.GD.FS_OperationsHelpers.VersionFrozenSession = version;
      if (HttpContext.Request.UrlReferrer != null)
        return Redirect(HttpContext.Request.UrlReferrer.ToString());
      return Index();
    }


    public ActionResult SetDateTime(string datetime)
    {
      Ikon.GD.FS_OperationsHelpers.DateTimeSession = Utility.TryParse<DateTime>(datetime, DateTime.Now);
      if (Request.UrlReferrer != null)
        return Redirect(Request.UrlReferrer.ToString());
      return Content(Ikon.GD.FS_OperationsHelpers.DateTimeSession.ToString());
    }


    public ActionResult Logout()
    {
      string returnToUrl = AuthControllerBase.LogoutWorker(null);
      return Redirect(returnToUrl);
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


    public ActionResult UbdateDB_Daily(bool? fullRebuild, bool? clearElmah, bool? updateSiteMaps, int? maxCacheAgeSeconds, int? maxCacheArchiveSizeMB)
    {
      List<string> messages = new List<string>();
      try
      {
        //
        IKCMS_ApplicationStatus.StatusSet("batch_title", "UbdateDB_Daily");
        IKCMS_ApplicationStatus.StatusSet("batch_status", "started");
        IKCMS_ApplicationStatus.StopwatchResetAndStart("batch");
        //
        FS_Operations fsOp = null;
        //
        if (clearElmah.GetValueOrDefault(false))
        {
          try
          {
            using (fsOp = new FS_Operations(-1, false, true, true))
            {
              int rowsElmah = fsOp.DB.ExecuteCommand("TRUNCATE TABLE [ELMAH_Error]");
              messages.Add("items removed from ELMAH_Error: {0}".FormatString(rowsElmah));
            }
          }
          catch (Exception ex) { messages.Add(ex.Message); }
        }
        IKCMS_ApplicationStatus.StatusSet("batch_status", "IKCMS_HitLogger: START");
        try { IKCMS_HitLogger.Flush(true); }
        catch (Exception ex) { messages.Add(ex.Message); }
        try { IKCMS_HitLogger.UpdateHitsStats(true); }
        catch (Exception ex) { messages.Add(ex.Message); }
        IKCMS_ApplicationStatus.StatusSet("batch_status", "IKCMS_HitLogger: END");
        //
        IKCMS_ApplicationStatus.StatusSet("batch_status", "IKCMS_HitAcc Migration: START");
        using (System.Transactions.TransactionScope ts = IKGD_TransactionFactory.Transaction(3600))
        {
          using (fsOp = new FS_Operations(-1, false, true, true))
          {
            fsOp.DB.CommandTimeout = 3600;
            try
            {
              int? HitLogMax = null;
              try { HitLogMax = fsOp.DB.IKG_HITLOGs.OrderByDescending(r => r.id).Skip(Utility.TryParse<int>(IKGD_Config.AppSettings["IKG_HITLOG_MaxSize"], 500000)).Max(r => r.id); }
              catch { }
              if (HitLogMax != null)
              {
                int rowsTransferred = fsOp.DB.ExecuteCommand("INSERT INTO IKG_HITLOG_ARCHIVE SELECT * FROM IKG_HITLOG WHERE id < {0}".FormatString(HitLogMax.Value));
                int rowsDeleted = fsOp.DB.ExecuteCommand("DELETE FROM IKG_HITLOG WHERE id < {0}".FormatString(HitLogMax.Value));
                messages.Add("records transferred to IKG_HITLOG_ARCHIVE: {0}".FormatString(rowsTransferred));
                messages.Add("records removed from IKG_HITLOG: {0}".FormatString(rowsDeleted));
              }
              //
              int HitLogMaxDays = Utility.TryParse<int>(IKGD_Config.AppSettings["IKG_HITLOG_MaxDays"], 180);
              int HitLogMaxHitsPerUser = Utility.TryParse<int>(IKGD_Config.AppSettings["IKG_HITLOG_MaxHitsPerUser"], 100);
              List<int> recordIds = new List<int>();
              DateTime minDate = DateTime.Now.AddDays(-HitLogMaxDays).Date;
              recordIds.AddRange(fsOp.DB.IKG_HITLOGs.Where(r => r.ts < minDate).Select(r => r.id));
              recordIds.AddRange(fsOp.DB.IKG_HITLOGs.GroupBy(r => r.wID).SelectMany(g => g.OrderByDescending(r => r.id).Skip(HitLogMaxHitsPerUser).Select(r => r.id)));
              recordIds = recordIds.Distinct().ToList();
              foreach (var slice in recordIds.Slice(5000))
              {
                int rowsTransferred2 = fsOp.DB.ExecuteCommand("INSERT INTO IKG_HITLOG_ARCHIVE SELECT * FROM IKG_HITLOG WHERE id IN ({0})".FormatString(Utility.Implode(slice, ",")));
                int rowsDeleted2 = fsOp.DB.ExecuteCommand("DELETE FROM IKG_HITLOG WHERE id IN ({0})".FormatString(Utility.Implode(slice, ",")));
              }
              messages.Add("records transferred to IKG_HITLOG_ARCHIVE: {0}  [with user and date limits]".FormatString(recordIds.Count));
              messages.Add("records removed from IKG_HITLOG: {0}  [with user and date limits]".FormatString(recordIds.Count));
            }
            catch (Exception ex) { messages.Add(ex.Message); }
            fsOp.DB.SubmitChanges();
          }
          ts.Committ();
        }
        IKCMS_ApplicationStatus.StatusSet("batch_status", "IKCMS_HitAcc Migration: END");
        //
        IKCMS_ApplicationStatus.StatusSet("batch_status", "ClearDiskCache: START");
        try
        {
          long deletedBytes = Ikon.Handlers.ProxyVFS2_Helper.ClearDiskCache(maxCacheAgeSeconds, null, maxCacheArchiveSizeMB);
          messages.Add(string.Format(FileSizeFormatProvider.Factory(), "Deleted bytes: {0:fs}", deletedBytes));
        }
        catch (Exception ex) { messages.Add(ex.Message); }
        IKCMS_ApplicationStatus.StatusSet("batch_status", "ClearDiskCache: END");
        //
        IKCMS_ApplicationStatus.StatusSet("batch_status", "DeserializeOnVFS_UpdateAll: START");
        try
        {
          using (fsOp = new FS_Operations(-1, false, true, true))
          {
            messages.AddRange(fsOp.DeserializeOnVFS_UpdateAll(fullRebuild.GetValueOrDefault(false)));
          }
        }
        catch (Exception ex) { messages.Add(ex.Message); }
        IKCMS_ApplicationStatus.StatusSet("batch_status", "DeserializeOnVFS_UpdateAll: END");
        //
        IKCMS_ApplicationStatus.StatusSet("batch_status", "Lucene_IndexUpdate: START");
        try
        {
          using (Ikon.Indexer.LuceneIndexer indexer = new Ikon.Indexer.LuceneIndexer(true))
          {
            int res = indexer.IKGD_IndexUpdate();
            if (res < 0)
              throw new Exception("La funzione di update degli indici di ricerca ha fornito il codice di errore: " + res.ToString());
            messages.Add(string.Format("Operazione completata con successo. \nSono state aggiornate {0} risorse.", res));
          }
        }
        catch (Exception ex) { messages.Add(ex.Message); }
        IKCMS_ApplicationStatus.StatusSet("batch_status", "Lucene_IndexUpdate: END");
        //
        if (updateSiteMaps.GetValueOrDefault(true))
        {
          IKCMS_ApplicationStatus.StatusSet("batch_status", "SiteMaps: START");
          try
          {
            messages.AddRange(SiteMapXmlRebuildWorker(null, null));
          }
          catch (Exception ex) { messages.Add(ex.Message); }
          IKCMS_ApplicationStatus.StatusSet("batch_status", "SiteMaps: END");
        }
        //
        //
        messages.Add("DeserializeOnVFS_UpdateAll completata.");
        //
      }
      catch (Exception ex)
      {
        messages.Add(ex.Message);
        IKCMS_ApplicationStatus.StatusSet("batch_status", "exception");
        IKCMS_ApplicationStatus.StatusSet("batch_exception", ex.Message);
      }
      IKCMS_ApplicationStatus.StatusSet("batch_status", "finished");
      IKCMS_ApplicationStatus.StopwatchStop("batch");
      string message = Utility.Implode(messages, "<br/>\n");
      ViewData["message"] = message;
      //if (HttpContext.Request.UrlReferrer != null)
      //  return Redirect(HttpContext.Request.UrlReferrer.ToString());
      return Content(message);
    }



    [AcceptVerbs(HttpVerbs.Get)]
    public ActionResult ResetCache()
    {
      if (!(IKGD_Config.IsBatchRequestAllowedWrapper || FS_ACL_Reduced.HasOperatorACLs() || Utility.TryParse<bool>(IKGD_Config.AppSettings["ResetCacheNoAuthAllowed"], true)))
      {
        throw new Exception("Richiesta proveniente da una connessione non autorizzata.");
      }
      IKGD_Config.Clear();
      var messages = Ikon.GD.FS_OperationsHelpers.CacheClear(null);
      messages.Add("CMS Cache reset completed.");
      if (HttpContext.Request.UrlReferrer != null)
        return Redirect(HttpContext.Request.UrlReferrer.ToString());
      ViewData["message"] = Utility.Implode(messages, "<br/>\n");
      return Index();
    }


    [AcceptVerbs(HttpVerbs.Get)]
    public ActionResult GCCollect()
    {
      if (!(IKGD_Config.IsBatchRequestAllowedWrapper || FS_ACL_Reduced.HasOperatorACLs()))
      {
        throw new Exception("Richiesta proveniente da una connessione non autorizzata.");
      }
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
    [AuthorizeLocal()]
    public ActionResult RecycleAppPoolIIS()
    {
      Ikon.GD.FS_OperationsHelpers.CacheClear(null);
      Ikon.Support.ApplicationPoolRecycle.RecycleCurrentApplicationPool();
      ViewData["message"] = "Web server riavviato.";
      return Index();
    }


    //
    // questa action e' anche disponibile nel SearchController che non e' bloccato dai robots.txt
    // ~/SitemapCMS
    //
    [AcceptVerbs(HttpVerbs.Get)]
    public ActionResult SiteMapXml(string language, string siteMode)
    {
      SiteMapHelper.SiteMapXml(Response, language, siteMode ?? IKGD_SiteMode.SiteMode);
      return null;
    }


    [AcceptVerbs(HttpVerbs.Get)]
    [AuthorizeLocalOrRoot()]
    public ActionResult SiteMapXmlRebuild(string language, string siteMode)
    {
      var messages = SiteMapXmlRebuildWorker(language, siteMode ?? IKGD_SiteMode.SiteMode);
      return Content(Utility.Implode(messages, "<br/>\n"), "text/html", Encoding.UTF8);
    }


    public List<string> SiteMapXmlRebuildWorker(string language, string siteMode)
    {
      var messages = new List<string>();
      try
      {
        using (IKGD_ExternalVFS_Support shFS = new IKGD_ExternalVFS_Support(IKGD_Config.AppSettings["SharePath_ExternalVFS"] ?? IKGD_Config.AppSettings["SharePath_Lucene"] ?? "~/App_Data/ExternalVFS"))
        {
          XElement xSiteMap = SiteMapHelper.SiteMapBuild(siteMode ?? IKGD_SiteMode.SiteMode, language);
          if (xSiteMap != null)
          {
            string fName = language.IsNullOrEmpty() ? "Sitemap.xml" : "Sitemap.{0}.xml".FormatString(language);
            string fPath = shFS.ResolveFileName(fName);
            if (fPath.IsNotNullOrWhiteSpace())
            {
              //xSiteMap.Save(fName, SaveOptions.DisableFormatting);
              xSiteMap.Save(fPath);
              messages.Add("Sitemap.xml rebuilt with {0} entries.".FormatString(xSiteMap.Elements().Count(x => x.Name.LocalName == "url")));
            }
          }
        }
      }
      catch (Exception ex)
      {
        messages.Add(ex.Message);
      }
      return messages;
    }


    [AcceptVerbs(HttpVerbs.Get)]
    [AuthorizeLocal()]
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
      return Index();
    }



    [AcceptVerbs(HttpVerbs.Get)]
    [AuthorizeLocal()]
    public ActionResult SearchEngineRebuild() { return SearchEngineRebuildWorker(false); }

    [AcceptVerbs(HttpVerbs.Get)]
    [AuthorizeLocal()]
    public ActionResult SearchEngineRebuildFull() { return SearchEngineRebuildWorker(true); }


    [AcceptVerbs(HttpVerbs.Get)]
    [AuthorizeLocal()]
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
      return Index();
    }



    [AcceptVerbs(HttpVerbs.Get)]
    [AuthorizeLocal()]
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
      return Index();
    }



    [AcceptVerbs(HttpVerbs.Get)]
    [AuthorizeLocal()]
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
      return Index();
    }



    [AcceptVerbs(HttpVerbs.Get)]
    [AuthorizeLocal()]
    public ActionResult MigrateResourcesVFS(bool? committ)
    {
      //
      // per non introdurre una dipendenza dalla DLL dei moduli/componenti obsoleti utilizzo una chiamata tramite reflection
      //
      //var migrazione01 = new MigrazioneVFS01();
      //List<string> messages = migrazione01.ProcessAll(committ.GetValueOrDefault());
      Type ty = Utility.FindTypeCachedExt("MigrazioneVFS01", false);
      object migrazione01 = Activator.CreateInstance(ty);
      List<string> messages = ty.InvokeMember("ProcessAll", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.InvokeMethod | System.Reflection.BindingFlags.Instance, null, migrazione01, new object[] { committ.GetValueOrDefault() }) as List<string>;

      string text = Utility.Implode(messages, "<hr/>\n");

      return Content(text);
    }


    // /BatchCMS/OptimizeStatisticsOnDB?updatestats=false&shrink=false&freecache=false

    [AcceptVerbs(HttpVerbs.Get)]
    [AuthorizeLocal()]
    public ActionResult OptimizeStatisticsOnDB(bool? shrink, bool? freecache, bool? updatestats)
    {
      try
      {
        //
        IKGD_VFS_Helpers.OptimizeStatisticsOnDB(shrink, freecache, updatestats);
        //
        ViewData["message"] = "Operazione completata con successo.";
      }
      catch (Exception ex)
      {
        ViewData["error"] = ex.Message;
      }
      return Index();
    }



    [AcceptVerbs(HttpVerbs.Get)]
    [AuthorizeLocalOrRoot()]
    public ActionResult DeserializeOnVFS_UpdateAll(bool? fullClean)
    {
      List<string> messages = new List<string>();
      try
      {
        //
        IKCMS_ApplicationStatus.StatusSet("batch_title", "DeserializeOnVFS_UpdateAll");
        IKCMS_ApplicationStatus.StatusSet("batch_status", "started");
        IKCMS_ApplicationStatus.StopwatchResetAndStart("batch");
        //
        FS_Operations fsOp = null;
        using (fsOp = new FS_Operations(-1, false, true, true))
        {
          messages.AddRange(fsOp.DeserializeOnVFS_UpdateAll(fullClean.GetValueOrDefault(false)));
        }
        //
        IKCMS_ApplicationStatus.StatusSet("batch_status", "DeserializeOnVFS_UpdateAll: END");
        //
        messages.Add("DeserializeOnVFS_UpdateAll completata.");
        //
      }
      catch (Exception ex)
      {
        messages.Add(ex.Message);
        IKCMS_ApplicationStatus.StatusSet("batch_status", "exception");
        IKCMS_ApplicationStatus.StatusSet("batch_exception", ex.Message);
      }
      IKCMS_ApplicationStatus.StatusSet("batch_status", "finished");
      IKCMS_ApplicationStatus.StopwatchStop("batch");
      return Content(Utility.Implode(messages, "<br/>\n"));
    }



    [AcceptVerbs(HttpVerbs.Get)]
    [AuthorizeLocal()]
    public ActionResult MinimizeIncludes(bool? js, bool? css, bool? less)
    {
      List<string> messages = MinimizeIncludesHelper.ProcessMinifierXml(null, js, css, less);
      return Content(Utility.Implode(messages, "<br/>\n"));
    }



    [AcceptVerbs(HttpVerbs.Get)]
    public ActionResult DumpTemplatesInfo()
    {
      IKCMS_ResourceConverterHelpers helper = new IKCMS_ResourceConverterHelpers();
      string result = helper.DumpTemplatesInfo();
      return Content(result);
    }



    [AcceptVerbs(HttpVerbs.Get)]
    [AuthorizeLocal()]
    public ActionResult DumpMissingPlaceholders(int? version_frozen)
    {
      IKCMS_ResourceConverterHelpers helper = new IKCMS_ResourceConverterHelpers();
      string result = helper.DumpMissingPlaceholders(version_frozen);
      return Content(result);
    }



    [AcceptVerbs(HttpVerbs.Get)]
    [AuthorizeLocal()]
    public ActionResult ConvertCategoriesPlaceholdersVFS(bool? committ)
    {
      try
      {
        IKCMS_ResourceConverterHelpers helper = new IKCMS_ResourceConverterHelpers();
        string result = helper.ConvertCategoriesPlaceholdersVFS(committ);
        ViewData["message"] = result;
      }
      catch (Exception ex)
      {
        ViewData["error"] = ex.Message;
      }
      return Index();
    }



    [AcceptVerbs(HttpVerbs.Get)]
    [AuthorizeLocal()]
    public ActionResult ConvertTemplatesVFS(bool? committ, bool? verbose)
    {
      try
      {
        IKCMS_ResourceConverterHelpers helper = new IKCMS_ResourceConverterHelpers();
        string result = helper.ConvertTemplatesVFS(committ, verbose);
        ViewData["message"] = result;
      }
      catch (Exception ex)
      {
        ViewData["error"] = ex.Message;
      }
      return Index();
    }



    [AcceptVerbs(HttpVerbs.Get)]
    [AuthorizeLocal()]
    public ActionResult NormalizeCategoriesPlaceholdersVFS(bool? committ, int? version_frozen)
    {
      try
      {
        IKCMS_ResourceConverterHelpers helper = new IKCMS_ResourceConverterHelpers();
        string result = helper.NormalizeCategoriesPlaceholdersTemplatesVFS(committ, version_frozen);
        ViewData["message"] = result;
      }
      catch (Exception ex)
      {
        ViewData["error"] = ex.Message;
      }
      return Index();
    }



    [AcceptVerbs(HttpVerbs.Get)]
    [AuthorizeLocal()]
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



    [AcceptVerbs(HttpVerbs.Get)]
    [AuthorizeLocal()]
    public ActionResult ClearDiskCacheVFS(int? maxAgeSeconds, int? maxArchiveSizeMB)
    {
      List<string> messages = new List<string>();
      try
      {
        long deletedBytes = Ikon.Handlers.ProxyVFS2_Helper.ClearDiskCache(maxAgeSeconds, null, maxArchiveSizeMB);
        messages.Add(string.Format(FileSizeFormatProvider.Factory(), "Deleted bytes: {0:fs}", deletedBytes));
      }
      catch (Exception ex)
      {
        messages.Add(ex.Message);
      }
      return Content(Utility.Implode(messages, "<br/>\n"));
    }



    [AcceptVerbs(HttpVerbs.Get)]
    public ActionResult HashMD5(string input)
    {
      return Content(Utility.HashMD5(input));
    }



    [AcceptVerbs(HttpVerbs.Get)]
    public ActionResult HashSHA1(string input)
    {
      return Content(Utility.HashSHA1(input));
    }



    [AcceptVerbs(HttpVerbs.Get)]
    [AuthorizeLocal()]
    public ActionResult MigrateVDATA_data_field(bool? committ)
    {
      try
      {
        IKCMS_ResourceConverterHelpers helper = new IKCMS_ResourceConverterHelpers();
        string result = helper.MigrateVDATA_data_field(committ);
        ViewData["message"] = result;
      }
      catch (Exception ex)
      {
        ViewData["error"] = ex.Message;
      }
      return Index();
    }



    [AcceptVerbs(HttpVerbs.Get)]
    [AuthorizeLocalOrRoot()]
    public ActionResult Normalize_IKCMS_SEO()
    {
      List<string> messages = new List<string>();
      try
      {
        fsOp.DB.IKCMS_SEOs.Where(r => r.target_snode != null).Join(fsOp.DB.IKGD_SNODEs, r => r.target_snode.Value, n => n.code, (seo, node) => new { seo, node }).Where(r => r.seo.target_rnode == null || r.seo.target_rnode != r.node.rnode).ForEach(r => r.seo.target_rnode = r.node.rnode);
        var chg = fsOp.DB.GetChangeSet();
        messages.Add("IKCMS_SEO con target_rnode aggiornati: {0}".FormatString(chg.Updates.Count));
        fsOp.DB.SubmitChanges();
      }
      catch (Exception ex)
      {
        messages.Add(ex.Message);
      }
      return Content(Utility.Implode(messages, "<br/>\n"));
    }



  }
}
