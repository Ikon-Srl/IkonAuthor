/*
 * 
 * Intranet Ikon
 * 
 * Copyright (C) 2008 Ikon Srl
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
using System.Threading;
using LinqKit;

using Ikon;
using Ikon.GD;


namespace Ikon.IKCMS
{

  public static class IKCMS_HitLogger
  {
    // i codici < 1000 sono riservati per il sistema
    // per logging custom utilizzare valori > 1000
    public enum LoggerActions { Exception = 1, Login = 2, LoginRefresh = 3, Logout = 4, LoginError = 5, SearchQuery = 6 };
    //
    // definizioni dei codici HitLog standard per IKCMS
    //
    public enum IKCMS_HitLogActionCodeEnum { CMS = 1, CustomBase = 1000 };
    public enum IKCMS_HitLogActionSubCodeEnum { Home = 0, PageCMS = 1, SearchCMS = 2, PageResourceCMS = 3, PageBrowserResourceCMS = 10, PageBrowserIndexCMS = 11, PageCatalogSimpleCMS = 20, PageCatalogTagsCMS = 21, PageCatalogExtendedCMS = 22, PageStaticCMS = 100, CustomBase = 1000 };
    // PageFromResourceCMS viene usato per la visualizzazione di pagine derivate da resources non folder diverse dalle news (es. popup delle photogallery)
    // ModuleCMS viene usato per moduli complessi come ricerca sui cataloghi o browsing per tags
    //
    private static object _lock = new object();
    private static DateTime LastUpdateTime;
    private static TimeSpan MaxBufferAge;
    private static DateTime LastStatsUpdateTime;
    private static TimeSpan MaxStatsAge;
    private static List<IKG_LOGGER> Buffer_IKG_LOGGER;
    private static List<IKG_HITLOG> Buffer_IKG_HITLOG;
    //


    static IKCMS_HitLogger()
    {
      LastUpdateTime = DateTime.Now;
      LastStatsUpdateTime = DateTime.Now;
      MaxBufferAge = TimeSpan.FromSeconds(Utility.TryParse<int>(IKGD_Config.AppSettings["IKCMS_HitLogger_MaxAge"], 3600));
      MaxStatsAge = TimeSpan.FromSeconds(Utility.TryParse<int>(IKGD_Config.AppSettings["IKCMS_HitLogger_MaxStatsAge"], 3600));
      Buffer_IKG_LOGGER = new List<IKG_LOGGER>(Utility.TryParse<int>(IKGD_Config.AppSettings["IKCMS_HitLogger_LoggerSize"], 100));
      Buffer_IKG_HITLOG = new List<IKG_HITLOG>(Utility.TryParse<int>(IKGD_Config.AppSettings["IKCMS_HitLogger_HitLogSize"], 100));
    }


    public static IEnumerable<IKG_HITLOG> Get_IKG_HITLOG { get { return Buffer_IKG_HITLOG.EnumeratorLocked(_lock); } }
    public static IEnumerable<IKG_LOGGER> Get_IKG_LOGGER { get { return Buffer_IKG_LOGGER.EnumeratorLocked(_lock); } }


    public static void Flush(bool syncUpdate)
    {
      //
      List<IKG_LOGGER> aux_logger = null;
      List<IKG_HITLOG> aux_hitlog = null;
      lock (_lock)
      {
        if (Buffer_IKG_LOGGER.Any())
        {
          aux_logger = Buffer_IKG_LOGGER.ToList();
          Buffer_IKG_LOGGER.Clear();
        }
        if (Buffer_IKG_HITLOG.Any())
        {
          aux_hitlog = Buffer_IKG_HITLOG.ToList();
          Buffer_IKG_HITLOG.Clear();
        }
      }
      //
      LastUpdateTime = DateTime.Now;
      //
      if (aux_logger == null && aux_hitlog == null)
        return;

      //
      // lambda expression con il worker per l'update asincrono
      // la lambda expression dovrebbe funzionare correttamente perche' le closures
      // garantiscono che le variabili aux_logger e aux_hitlog non vengono cancellate dal GC all'uscita del context
      // premettendo un'esecuzone corretta del thread dopo la chiusura del context
      //
      ParameterizedThreadStart worker = (obj) =>
      {
        using (IKGD_DataContext DB = IKGD_DBH.GetDB())
        {
          using (TransactionScope ts = IKGD_TransactionFactory.TransactionReadUncommitted(Math.Max(Buffer_IKG_HITLOG.Capacity, 120)))
          {
            //
            if (aux_logger != null)
            {
              foreach (IKG_LOGGER rec in aux_logger)
              {
                try { DB.ExecuteCommand("INSERT INTO [IKG_LOGGER] ([username],[action],[data],[ts],[sessionHash]) VALUES ({0},{1},{2},{3},{4})", rec.username, rec.action, rec.data, rec.ts, rec.sessionHash); }
                catch { }
              }
            }
            //
            if (aux_hitlog != null)
            {
              foreach (IKG_HITLOG rec in aux_hitlog)
              {
                try { DB.ExecuteCommand("INSERT INTO [IKG_HITLOG] ([wID],[resID],[action],[code],[ts],[sessionHash]) VALUES ({0},{1},{2},{3},{4},{5})", rec.wID, rec.resID, rec.action ?? 0, rec.code ?? 0, rec.ts, rec.sessionHash); }
                catch { }
              }
            }
            //
            ts.Committ();
            //
          }
        }
      };


      if (syncUpdate || !IKGD_QueueManager.IsAsyncProcessingEnabled)
      {
        worker(null);
      }
      else
      {
        Thread thr = new Thread(worker);
        thr.Start();
      }

    }


    public static void UpdateHitsStats(bool syncUpdate)
    {
      //
      LastStatsUpdateTime = DateTime.Now;
      LastUpdateTime = DateTime.Now; // per evitare ricorsioni con il flush asincrono
      //
      ParameterizedThreadStart worker = (obj) => { UpdateHitsStatsSync(); };
      //
      if (syncUpdate || !IKGD_QueueManager.IsAsyncProcessingEnabled)
      {
        worker(null);
      }
      else
      {
        Thread thr = new Thread(worker);
        thr.Start();
      }
    }


    public static void UpdateHitsStatsSync()
    {
      lock (_lock)
      {
        try
        {
          Flush(true);
          //
          using (IKGD_DataContext DB = IKGD_DBH.GetDB())
          {
            using (TransactionScope ts = IKGD_TransactionFactory.TransactionReadUncommitted(Math.Max(Buffer_IKG_HITLOG.Capacity, 120)))
            {
              // utilizziamo IKG_HITLOG.action=1 che e' il default per il CMS e viene mappato su IKG_HITACC.Category=0
              // IKG_HITACC.Value e' sempre 0
              // creazione degli eventuali record mancanti nella tabella degli hits accumulati
              // non e' necessario eseguirla sempre perche' la query successiva funziona correttamente anche in caso di record assenti
              // in tal caso vengono saltati e saranno aggiornati negli update successivi
              int records1 = DB.ExecuteCommand(
@"INSERT INTO IKG_HITACC(rNode,Category,Hits,Value,LastUpdate)
SELECT rNode,0 as Category,0 as Hits,0 as Value,'1900-01-01' as LastUpdate
FROM ((SELECT DISTINCT resID as rNode FROM IKG_HITLOG WHERE action=1) EXCEPT (SELECT DISTINCT rNode as rNode FROM IKG_HITACC WHERE Category=0)) t
WHERE rNode IN (SELECT code FROM IKGD_RNODE)");
              int records2 = DB.ExecuteCommand(
@"UPDATE IKG_HITACC SET Hits = Hits + th.hits_count, LastUpdate = th.tsmax
FROM (SELECT t1.resID,COUNT(*) as hits_count,MAX(ts) as tsmax FROM IKG_HITLOG t1 JOIN IKG_HITACC t2 ON (t2.rNode=t1.resID) WHERE (t1.action=1 AND t2.Category=0 AND t1.ts>t2.LastUpdate) GROUP BY resID) th
WHERE Category=0 AND IKG_HITACC.rNode=th.resID");
              //
              // utilizziamo LazyLogin_Vote.Category=1 che e' il default per il CMS e viene mappato su IKG_HITACC.Category=1
              // IKG_HITACC.Hits: numero di votazioni
              // IKG_HITACC.Value: valore medio delle votazioni (non abbiamo problemi di divisioni per 0)
              // creazione degli eventuali record mancanti nella tabella degli hits accumulati
              // non e' necessario eseguirla sempre perche' la query successiva funziona correttamente anche in caso di record assenti
              // in tal caso vengono saltati e saranno aggiornati negli update successivi
              string extraQuery01 = Utility.TryParse<bool>(IKGD_Config.AppSettings["LazyLogin_Vote_OnRNodesOnly"], false) ? "JOIN IKGD_RNODE t3 ON (t3.code=t1.rNode)" : string.Empty;
              int records3 = DB.ExecuteCommand(
@"INSERT INTO IKG_HITACC(rNode,Category,Hits,Value,LastUpdate)
SELECT rNode,1 as Category,0 as Hits,0 as Value,'1900-01-01' as LastUpdate
FROM (SELECT DISTINCT t1.rNode FROM (SELECT * FROM LazyLogin_Vote WHERE Category=0) t1 LEFT OUTER JOIN (SELECT * FROM IKG_HITACC WHERE Category=1) t2 ON (t2.rNode=t1.rNode) {0} WHERE t2.rNode is null) th".FormatString(extraQuery01));
              int records4 = DB.ExecuteCommand(
@"UPDATE IKG_HITACC SET Value = (Value*Hits + votes)/(Hits + th.hits_count), Hits = Hits + th.hits_count, LastUpdate = th.tsmax
FROM (SELECT t1.rNode,COUNT(*) as hits_count,SUM(t1.Value) as votes,MAX(t1.Date) as tsmax FROM LazyLogin_Vote t1 JOIN IKG_HITACC t2 ON (t2.rNode=t1.rNode) WHERE (t1.Category=0 AND t2.Category=1 AND (t1.Value IS NOT NULL) AND t1.Date>t2.LastUpdate) GROUP BY t1.rNode) th
WHERE Category=1 AND IKG_HITACC.rNode=th.rNode");
              //
              // salvataggio di dati extra per funzionalita' custom
              //
              if (Utility.TryParse<bool>(IKGD_Config.AppSettings["IKG_HITACC_EnableCustomAccumulation"]))
              {
                // accumulazione di hits per moduli custom non dipendenti dal CMS
                // bisogna attivare la funzionalita' da web.config e registrare gli hitlog
                // con action>=1000
                int records5 = DB.ExecuteCommand(
@"INSERT INTO IKG_HITACC(rNode,Category,Hits,Value,LastUpdate)
SELECT rNode,Category,0 as Hits,0 as Value,'1900-01-01' as LastUpdate
FROM ((SELECT DISTINCT resID as rNode,[action] as Category FROM IKG_HITLOG WHERE [action]>=1000) EXCEPT (SELECT DISTINCT rNode as rNode,Category FROM IKG_HITACC WHERE Category>=1000)) t");
                int records6 = DB.ExecuteCommand(
@"UPDATE IKG_HITACC SET Hits = Hits + th.hits_count, LastUpdate = th.tsmax
FROM (SELECT t1.resID,t1.action,COUNT(*) as hits_count,MAX(ts) as tsmax FROM IKG_HITLOG t1
JOIN IKG_HITACC t2 ON (t2.rNode=t1.resID)
WHERE (t1.action=t2.Category AND t2.Category>=1000 AND t1.ts>t2.LastUpdate) GROUP BY t1.resID,t1.action) th
WHERE IKG_HITACC.Category=th.action AND IKG_HITACC.rNode=th.resID");
              }
              //
              ts.Committ();
              //
            }
          }
          //
        }
        catch { }
      }
    }


    //
    // funzione per l'accumulazione di log generici (login/logout, search, ...)
    //
    public static void ProcessLogger(string username, LoggerActions action, string data) { ProcessLogger(username, (int)action, data); }
    public static void ProcessLogger(string username, int action, string data)
    {
      try
      {
        username = Utility.StringTruncate(username ?? Ikon.GD.MembershipHelper.UserName, 100);
        data = Utility.StringTruncate(data ?? string.Empty, 250);
        int sessionHash = 0;
        try { sessionHash = HttpContext.Current.Session.SessionID.GetHashCode(); }
        catch { }
        IKG_LOGGER rec = new IKG_LOGGER { username = username, action = action, data = data, ts = DateTime.Now, sessionHash = sessionHash };
        lock (_lock)
        {
          Buffer_IKG_LOGGER.Add(rec);
          if (Buffer_IKG_LOGGER.Count >= Buffer_IKG_LOGGER.Capacity || (rec.ts - LastUpdateTime) > MaxBufferAge)
            Flush(false);
        }
      }
      catch { }
    }


    //
    // UserId: UserId from LazyLogin
    // ResourceCode: sNode o rNode della risorsa
    // ActionCode: tipo di action loggata (es: page hit, ajax operation, ....)
    // ActionSubCode: sub_type della action (es: page type (catalog, ambienti, ...), oppure ajax action type, ...)
    //
    public static void ProcessHit(int UserId, int ResourceCode, int? ActionCode, int? ActionSubCode)
    {
      try
      {
        int sessionHash = 0;
        try { if (HttpContext.Current.Session != null) sessionHash = HttpContext.Current.Session.SessionID.GetHashCode(); }
        catch { }
        IKG_HITLOG rec = new IKG_HITLOG { IKCMS_UserIdLL = UserId, IKCMS_ResourceCode = ResourceCode, IKCMS_ActionCode = ActionCode, IKCMS_ActionSubCode = ActionSubCode, ts = DateTime.Now, sessionHash = sessionHash };
        lock (_lock)
        {
          Buffer_IKG_HITLOG.Add(rec);
          if (DateTime.Now - LastStatsUpdateTime > MaxStatsAge)
          {
            UpdateHitsStats(false);
          }
          else if (Buffer_IKG_HITLOG.Count >= Buffer_IKG_HITLOG.Capacity || (rec.ts - LastUpdateTime) > MaxBufferAge)
          {
            Flush(false);
          }
        }
      }
      catch { }
    }


    public static void ProcessHitLL(int ResourceCode, int? ActionCode, int? ActionSubCode)
    {
      ProcessHit(MembershipHelper.LazyLoginMapperObject.Id, ResourceCode, ActionCode, ActionSubCode);
    }


  }


  //
  // migrazione dei dati relativi al logging buffer
  //
  public class IKCMS_HitLogger_AnonymousDataMigration : I_IKGD_MembershipAnonymousDataMigration
  {
    public static int MigrateAnonymousData(IKGD_DataContext DB, MembershipUser userOld, ILazyLoginMapper UserLL_Old, MembershipUser userNew, ILazyLoginMapper UserLL_New)
    {
      try
      {
        if (UserLL_Old != null && UserLL_New != null)
          IKCMS_HitLogger.Get_IKG_HITLOG.Where(r => r.IKCMS_UserIdLL == UserLL_Old.Id).ForEach(r => r.IKCMS_UserIdLL = UserLL_New.Id);
        if (userOld != null && userNew != null)
          IKCMS_HitLogger.Get_IKG_LOGGER.Where(r => r.username == userOld.UserName).ForEach(r => r.username = userNew.UserName);
      }
      catch { }
      return 0;
    }
  }


}




