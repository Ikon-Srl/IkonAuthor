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
using System.Web.Configuration;
using System.Xml.Linq;
using System.Data.Linq;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
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

using Ikon;
using Ikon.GD;
using Ikon.GD.Config;
using Ikon.IKCMS;
using Ikon.Log;
using System.Threading;



namespace Ikon.GD
{



  public static class IKGD_QueueManager
  {
    //
    private static object _lockTimer = new object();
    private static object _lockWorker = new object();
    private static TimeSpan? SleepIntervalShort { get; set; }
    private static TimeSpan? SleepIntervalLong { get; set; }
    private static DateTime? NextTimeOut { get; set; }
    private static Thread monitorThread { get; set; }
    //
    public static bool Running { get; private set; }
    public static List<QueueProcessingHandler> ProcessingHandlers { get; set; }
    public static bool IsAsyncProcessingEnabled { get; private set; }
    public static int ApplicationIdHash { get; private set; }
    //


    static IKGD_QueueManager()
    {
      ProcessingHandlers = new List<QueueProcessingHandler>();
      Running = false;
      //
      ApplicationIdHash = System.Web.HttpContext.Current.Application.GetHashCode();
      IsAsyncProcessingEnabled = Utility.TryParse<bool>(IKGD_Config.AppSettings["IKCMS_AllowAsyncProcessing"], true);
      Setup();
    }



    public static void Setup()
    {
      if (monitorThread != null)
        return;
      if (IsAsyncProcessingEnabled == false)
        return;
      monitorThread = new Thread(QueueMonitor);
      monitorThread.Start();
    }


    public static void Stop()
    {
      if (monitorThread != null && monitorThread.IsAlive)
      {
        monitorThread.Abort();
        monitorThread = null;
      }
    }


    public static void NotifyNewEntry() { NotifyNewEntry(null); }
    public static void NotifyNewEntry(double? maxSecondsToWait)
    {
      var delay = SleepIntervalShort;
      if (maxSecondsToWait > 0 && maxSecondsToWait < SleepIntervalShort.Value.TotalSeconds)
        delay = TimeSpan.FromSeconds(maxSecondsToWait.Value);
      var nextCheck = DateTime.Now + delay;
      lock (_lockTimer)
      {
        if (NextTimeOut > nextCheck)
          NextTimeOut = nextCheck;
      }
      if (monitorThread != null)
      {
        monitorThread.Interrupt();
      }
    }


    public static void RegisterHandler(Func<FS_Operations, IEnumerable<OpHandlerCOW_OperationEnum>, IEnumerable<FS_Operations.FS_NodeInfo_Interface>, bool> processor, IEnumerable<OpHandlerCOW_OperationEnum> opHandlerCOWs, params string[] managerTypes)
    {
      try
      {
        QueueProcessingHandler handler = new QueueProcessingHandler { Processor = processor };
        if (managerTypes != null)
          handler.ManagerTypes = managerTypes.ToList();
        if (opHandlerCOWs != null)
          handler.OpTypesCOW = opHandlerCOWs.ToList();
        if (processor != null)
          ProcessingHandlers.Add(handler);
      }
      catch { }
    }


    private static void QueueMonitor(object obj)
    {
      //
      SleepIntervalShort = SleepIntervalShort ?? TimeSpan.FromSeconds(Utility.TryParse<double>(IKGD_Config.AppSettings["IKGD_QueueManager_SleepShort"], 120.0));
      SleepIntervalLong = SleepIntervalLong ?? TimeSpan.FromSeconds(Utility.TryParse<double>(IKGD_Config.AppSettings["IKGD_QueueManager_SleepLong"], 3600.0));
      //
      if (NextTimeOut != null && NextTimeOut < DateTime.Now)
        NextTimeOut = null;
      if (NextTimeOut == null)
        NextTimeOut = DateTime.Now.AddSeconds(SleepIntervalLong.Value.TotalSeconds * (1.0 + 0.20 * (new Random().NextDouble())));
      //
      for (; ; )
      {
        try
        {
          TimeSpan dt;
          lock (_lockTimer)
          {
            dt = NextTimeOut.Value - DateTime.Now;
          }
          if (dt.TotalMilliseconds > 0)
            Thread.Sleep(dt);
          // se il timer e' scaduto o lo sleep e' stato interrotto riavvio il loop, nel caso di thread interrotto esegue un nuovo sleep altrimenti esegue il worker
          lock (_lockTimer)
          {
            if (NextTimeOut.Value > DateTime.Now)
              continue;
          }
          //
          // calling worker process
          //
          bool result = QueueMonitorWorker();
          //
          // preparazione del timeout per il prossimo loop
          // utiliziamo un timeout elevato perche' in caso il thread viene svegliato automaticamente nel caso di una nuova notifica
          // 
          lock (_lockTimer)
          {
            if (NextTimeOut.Value <= DateTime.Now)
              NextTimeOut = DateTime.Now + (result ? SleepIntervalLong.Value : SleepIntervalLong.Value);
          }
        }
        catch { }
      }
      //
    }


    //
    // e' possibile anche chiamare questo metodo dall'esterno per forzare un run della queue
    //
    public static bool QueueMonitorWorker()
    {
      //
      if (Running)
        return false;
      lock (_lockWorker)
      {
        Running = true;
        //
        try
        {
          //
          DateTime maxProcessingTime = DateTime.Now.AddSeconds(-3600);
          //
          // fetch dei record dalla queue
          //
          List<IKGD_QueueMeta> queuedRecords = null;
          //
          // la transaction dovrebbe garantire il locking corretto per l'accesso alle risorse sul DB
          // usiamo sempre TransactionSerializable per evitare interferenze tra le eventuali chiamate asincrone concorrenti
          //
          using (TransactionScope ts = IKGD_TransactionFactory.TransactionSerializable(Utility.TryParse<int>(IKGD_Config.AppSettings["IKGD_QueueManager_SleepShort"], 120)))
          {
            using (FS_Operations fsOp = new FS_Operations(-1, false, true, true))
            {
              fsOp.EnsureOpenConnection();
              //var tmp01 = fsOp.DB.IKGD_QueueMetas.ToList();
              //var tmp02 = fsOp.DB.IKGD_QueueMetas.Where(r => r.Application == IKGD_Config.ApplicationName).ToList();
              //var tmp03 = fsOp.DB.IKGD_QueueMetas.Where(r => r.Application == IKGD_Config.ApplicationName).Where(r => (r.Status == (int)IKGD_QueueMetaStatusEnum.Queued)).ToList();
              //var tmp04 = fsOp.DB.IKGD_QueueMetas.Where(r => r.Application == IKGD_Config.ApplicationName).Where(r => (r.Status == (int)IKGD_QueueMetaStatusEnum.Processing && r.ProcessingDateTime < maxProcessingTime)).ToList();
              queuedRecords = fsOp.DB.IKGD_QueueMetas.Where(r => r.Application == IKGD_Config.ApplicationName).Where(r => (r.Status == (int)IKGD_QueueMetaStatusEnum.Queued) || (r.Status == (int)IKGD_QueueMetaStatusEnum.Processing && r.ProcessingDateTime < maxProcessingTime)).OrderBy(t => t.QueueID).ToList();
              //queuedRecords = fsOp.DB.IKGD_QueueMetas.Where(r => r.Application == IKGD_Config.ApplicationName).Where(r => ((r.Status & (int)IKGD_QueueMetaStatusEnum.Queued) == (int)IKGD_QueueMetaStatusEnum.Queued) || ((r.Status & (int)IKGD_QueueMetaStatusEnum.Processing) == (int)IKGD_QueueMetaStatusEnum.Processing && r.ProcessingDateTime < maxProcessingTime)).OrderBy(t => t.QueueID).ToList();
              queuedRecords.ForEach(r =>
              {
                r.Status = (int)IKGD_QueueMetaStatusEnum.Processing;
                r.ApplicationInstanceHash = ApplicationIdHash;
                r.ProcessingDateTime = DateTime.Now;
              });
              var chg = fsOp.DB.GetChangeSet();
              fsOp.DB.SubmitChanges();
              //
              var relatedObjects = queuedRecords.Select(r => new { qm = r, qd = r.IKGD_QueueData }).ToList();
              //
            }
            ts.Committ();
          }

          //
          // fetch degli fsNodes interessati e processing
          //
          IKGD_QueueMetaStatusEnum batchStatus = IKGD_QueueMetaStatusEnum.Processed;
          using (TransactionScope ts = IKGD_TransactionFactory.TransactionReadUncommitted(Utility.TryParse<int>(IKGD_Config.AppSettings["IKGD_QueueManager_SleepLong"], 3600)))
          {
            using (FS_Operations fsOp = new FS_Operations(-1, false, true, true))
            {
              //
              var qids = queuedRecords.Select(r => r.QueueID).ToList();
              var dataListAll = fsOp.DB.IKGD_QueueDatas.Where(r => qids.Contains(r.QueueID)).Select(r => IKGD_QueuedOpDataVFS.DeSerialize(r.Data)).ToList();
              var dataListPublish = dataListAll.Where(r => r.opType == OpHandlerCOW_OperationEnum.Publish).ToList();
              var dataListUnlink = dataListAll.Where(r => r.opType == OpHandlerCOW_OperationEnum.Unlink).ToList();
              var dataListUpdate = dataListAll.Where(r => r.opType == OpHandlerCOW_OperationEnum.Update || r.opType == OpHandlerCOW_OperationEnum.Undelete).ToList();
              //
              List<FS_Operations.FS_NodeInfo_Interface> fsNodesAll = null;
              List<FS_Operations.FS_NodeInfo_Interface> fsNodesPublished = null;
              if (dataListAll.Any())
              {
                var rNodes = dataListAll.SelectMany(r => r.rNodesAffected).Distinct().ToList();
                fsOp.VersionFrozen = -1;
                fsNodesAll = fsOp.Get_NodesInfoFilteredExt1(null, vd => rNodes.Contains(vd.rnode), FS_Operations.FilterVFS.Disabled | FS_Operations.FilterVFS.Deleted).ToList();
              }
              if (dataListPublish.Any())
              {
                var rNodes = dataListPublish.SelectMany(r => r.rNodesAffected).Distinct().ToList();
                fsOp.VersionFrozen = 0;
                fsNodesPublished = fsOp.Get_NodesInfoFilteredExt1(null, vd => rNodes.Contains(vd.rnode), FS_Operations.FilterVFS.Disabled | FS_Operations.FilterVFS.Deleted).ToList();
              }
              fsOp.VersionFrozen = -1;
              //
              var fsNodesCombined = (fsNodesAll ?? Enumerable.Empty<FS_Operations.FS_NodeInfo_Interface>()).Union((fsNodesPublished ?? Enumerable.Empty<FS_Operations.FS_NodeInfo_Interface>())).Distinct((n1, n2) => n1.vNode.version == n2.vNode.version && n1.vData.version == n2.vData.version && ((n1.iNode == null && n2.iNode == null) || (n1.iNode.version == n2.iNode.version))).ToList();
              //
              // loop sui process handlers
              //
              bool result = true;
              foreach (QueueProcessingHandler handler in ProcessingHandlers)
              {
                try
                {
                  //
                  IEnumerable<FS_Operations.FS_NodeInfo_Interface> fsNodes = fsNodesCombined;
                  if (handler.ManagerTypes.Any())
                    fsNodes = fsNodes.Where(n => handler.ManagerTypes.Contains(n.ManagerType));
                  if (handler.OpTypesCOW.Any() && fsNodes.Any())
                    fsNodes = fsNodes.Where(n => dataListAll.Where(d => handler.OpTypesCOW.Contains(d.opType)).Any(d => d.rNodesAffected.Contains(n.rNode)));
                  if (!fsNodes.Any())
                    continue;
                  var activeOpTypes = fsNodes.SelectMany(n => dataListAll.Where(d => d.rNodesAffected.Contains(n.rNode)).Select(d => d.opType)).Distinct().ToList();
                  //
                  result &= handler.Processor(fsOp, activeOpTypes, fsNodes);
                  //
                  var chg = fsOp.DB.GetChangeSet();
                  fsOp.DB.SubmitChanges();
                }
                catch (Exception ex)
                {
                  result = false;
                  batchStatus = IKGD_QueueMetaStatusEnum.Error;
                  Elmah.ErrorSignal.FromCurrentContext().Raise(ex);
                }
                //
                // TODO:
                // aggiungere logging degli errori di processing in caso di status == false
                // gestire oltre al logging anche lo status del queue record in caso di errori
                // 
              }
              //
            }
            ts.Committ();
          }
          //
          // update dei record della queue processati nel contesto corrente
          //
          using (TransactionScope ts = IKGD_TransactionFactory.TransactionSerializable(Utility.TryParse<int>(IKGD_Config.AppSettings["IKGD_QueueManager_SleepShort"], 120)))
          {
            using (FS_Operations fsOp = new FS_Operations(-1, false, true, true))
            {
              var qids = queuedRecords.Select(r => r.QueueID).ToList();
              foreach (var qids_chunk in qids.Slice(100))
              {
                var queuedRecordsAux = fsOp.DB.IKGD_QueueMetas.Where(r => qids_chunk.Contains(r.QueueID)).ToList();
                queuedRecordsAux.ForEach(r => r.Status = (int)batchStatus);
                var chg = fsOp.DB.GetChangeSet();
                fsOp.DB.SubmitChanges();
              }
            }
            ts.Committ();
          }
          //
          return true;
          //
        }
        catch (Exception ex)
        {
          Elmah.ErrorSignal.FromCurrentContext().Raise(ex);
        }
        finally { Running = false; }
      }
      //
      return false;
    }



    public class QueueProcessingHandler
    {
      public List<string> ManagerTypes { get; set; }
      public List<OpHandlerCOW_OperationEnum> OpTypesCOW { get; set; }
      public Func<FS_Operations, IEnumerable<OpHandlerCOW_OperationEnum>, IEnumerable<FS_Operations.FS_NodeInfo_Interface>, bool> Processor { get; set; }


      public QueueProcessingHandler()
      {
        ManagerTypes = new List<string>();
        OpTypesCOW = new List<OpHandlerCOW_OperationEnum>();
      }

    }


  }



}  //namespace
