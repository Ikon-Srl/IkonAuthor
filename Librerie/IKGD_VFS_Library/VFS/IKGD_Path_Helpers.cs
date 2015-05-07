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
using System.Web.UI;
using System.Web.Security;
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
using System.Data.Linq.SqlClient;  // per usare SqlMethods
using LinqKit;


using Ikon;
using Ikon.Log;


namespace Ikon.GD
{
  using Ikon.GD.Config;


  //
  // metodi per la gestione centralizzata e ottimizzata dei path
  //
  // TODO:
  // gestione del garbage collector per il caching dei path fragments
  // refactoring della api per non avere conflitti con la vecchia path api
  // armonizzare gli elementi della vecchia api con la nuova per facilitare a migrazione
  // VERIFICARE: modificando il DB con modifiche ad un nodo gia' in cache sembra che non venga invalidata la cache locale
  //
  //
  public static class IKGD_Path_Helper
  {
    public static object _lock = new object();
    public static object _lockGC = new object();
    private static readonly string baseKeyCache = "IKGD_Path_";
    //
    private static List<IKGD_Path_Fragment> _PathsFragsCollection;
    public static List<IKGD_Path_Fragment_Missing> FragmentsMissing { get; private set; }  // lista dei nodi non risolvibili nello scan ricorsivo dei path
    public static int vNodeVersionLast { get; set; }
    public static int vDataVersionLast { get; set; }
    public static bool IsTaintedVFS { get { return (bool)(HttpRuntime.Cache[baseKeyCache + "IsTaintedVFS"] ?? true); } }
    public static bool IsTaintedSnapshot { get { return (bool)(HttpRuntime.Cache[baseKeyCache + "IsTaintedSnapshot"] ?? true); } }
    //
    public static int GC_CallCounter { get; private set; }
    public static DateTime GC_LastShrinkTS { get; private set; }
    //
    private static int GC_CallCounterLimit { get; set; }
    private static int GC_MaxAgeSeconds { get; set; }
    //
    public static bool TraceDebugFlag { get; private set; }
    //


    static IKGD_Path_Helper()
    {
      //
      GC_CallCounterLimit = Utility.TryParse<int>(IKGD_Config.AppSettings["IKGD_Path_GC_CallCounterLimit"], int.MaxValue / 2);
      GC_MaxAgeSeconds = Utility.TryParse<int>(IKGD_Config.AppSettings["IKGD_Path_GC_MaxAgeSeconds"], 3600 * 2);
      //
      TraceDebugFlag = Utility.TryParse<bool>(IKGD_Config.AppSettings["IKGD_Path_TraceDebugFlag"], false);
      //
      GarbageCollectorWorker(true);
    }


    public static List<IKGD_Path_Fragment> PathsFragsCollection(FS_Operations fsOp)
    {
      lock (_lock)
      {
        if (IsTaintedVFS || IsTaintedSnapshot)
          PathsUpdateFrags(fsOp);
        return _PathsFragsCollection;
      }
    }


    //
    // attenzione che questo metodo viene utilizzato anche all'esterno del PathHelper (ModelBuild del ModelBuildProvider)
    //
    public static IEnumerable<IKGD_Path_Fragment> PathsFragsCollectionContext(this FS_Operations fsOp) { return fsOp.PathsFragsCollectionContext(null, null, false); }
    public static IEnumerable<IKGD_Path_Fragment> PathsFragsCollectionContext(this FS_Operations fsOp, int? version_frozen, string language, bool fullAccess)
    {
      IEnumerable<IKGD_Path_Fragment> frags = PathsFragsCollection(fsOp);
      version_frozen = version_frozen ?? fsOp.VersionFrozen;
      if (version_frozen == 0)
        frags = frags.Where(n => n.flag_published);
      else if (version_frozen == -1)
        frags = frags.Where(n => n.flag_current);
      else
      {
        // TODO: non so bene se funziona, per il momento lo proviamo cosi' (tanto con version specifica non si usano i path ottimizzati)
        frags = frags.Where(n => (n.flag_published && n.vNodeVersionFrozen < version_frozen && n.vDataVersionFrozen < version_frozen) || (n.vNodeVersionFrozen == version_frozen || n.vDataVersionFrozen == version_frozen));
      }
      if (language != null && language != FS_Operations.LanguageNoFilterCode)
        frags = frags.Where(n => n.Language == null || n.Language == language);
      if (!fullAccess)
      {
        //var areas = fsOp.CurrentAreas;
        //frags = frags.Where(n => string.IsNullOrEmpty(n.Area) || areas.Contains(n.Area));
        if (fsOp.CurrentAreasExtended.AreaMatchMode == FS_Areas_Extended.AreaMatchModeEnum.FilterByAllowed)
          frags = frags.Where(n => fsOp.CurrentAreasExtended.AreasAllowed.Contains(n.Area));
        else if (fsOp.CurrentAreasExtended.AreaMatchMode == FS_Areas_Extended.AreaMatchModeEnum.FilterByDenied)
          frags = frags.Where(n => !fsOp.CurrentAreasExtended.AreasDenied.Contains(n.Area));
      }
      return frags;
    }


    //
    // garbage collector
    //
    public static int GarbageCollectorWorker(bool fullClean)
    {
      lock (_lockGC)
      {
        try
        {
          if (fullClean)
          {
            int size1 = -1;
            int size2 = -1;
            //
            lock (_lock)
            {
              // proviamo prima un Clear che forse interagisce meglio con il GC
              try { size1 = _PathsFragsCollection.Count; _PathsFragsCollection.Clear(); }
              catch { }
              try { size2 = FragmentsMissing.Count; FragmentsMissing.Clear(); }
              catch { }
              _PathsFragsCollection = new List<IKGD_Path_Fragment>();
              FragmentsMissing = new List<IKGD_Path_Fragment_Missing>();
            }
            Elmah.ErrorSignal.FromCurrentContext().Raise(new Exception("GarbageCollectorWorker [CALLED] GC_CallCounter={0}/{1} sizes={2},{3}-->{4},{5}".FormatString(GC_CallCounter, GC_CallCounterLimit, size1, size2, _PathsFragsCollection.Count, FragmentsMissing.Count)));
            //
            vNodeVersionLast = -1;
            vDataVersionLast = -1;
            GC_CallCounter = 0;
            GC_LastShrinkTS = DateTime.Now;
            return -1;
          }
          //
          GC_CallCounter++;
          if (GC_CallCounter < GC_CallCounterLimit && (DateTime.Now - GC_LastShrinkTS).TotalSeconds < GC_MaxAgeSeconds)
            return 0;
          //
          // TODO: sviluppare una metrica da utilizzare per il garbage collector
          // utilizzare il metodo: .GC_Metric();
          // e un sistema di partizionamento stabile con mediane/percentili
          // che non porti ad un annullamento progressivo della cache
          return 0;
        }
        catch (Exception ex)
        {
          Elmah.ErrorSignal.FromCurrentContext().Raise(ex);
        }
        return 0;
      }
    }


    //
    // esegue la pulizia dei nodi gestiti e aggiorna gli oggetti per la dipendenza dalla cache
    // viene chiamata solo da PathsFragsCollection
    //
    private static void PathsUpdateFrags(FS_Operations fsOp)
    {
      lock (_lock)
      {
        try
        {
          int vNodeVersionLastNew;
          int vDataVersionLastNew;
          PathsUpdateFragsClearUpdatedNodes(fsOp, IsTaintedSnapshot, out vNodeVersionLastNew, out vDataVersionLastNew);
          vNodeVersionLast = vNodeVersionLastNew;
          vDataVersionLast = vDataVersionLastNew;
          //
          // aggiornamento degli handler per cache/dependencies
          //
          if (HttpRuntime.Cache[baseKeyCache + "IsTaintedVFS"] == null)
          {
            AggregateCacheDependency sqlDeps = new AggregateCacheDependency();
            sqlDeps.Add(new SqlCacheDependency("GDCS", "IKGD_VNODE"), new SqlCacheDependency("GDCS", "IKGD_VDATA"));
            HttpRuntime.Cache.Insert(baseKeyCache + "IsTaintedVFS", false, sqlDeps, Cache.NoAbsoluteExpiration, Cache.NoSlidingExpiration, CacheItemPriority.High, null);
          }
          else
            HttpRuntime.Cache[baseKeyCache + "IsTaintedVFS"] = false;
          //
          if (HttpRuntime.Cache[baseKeyCache + "IsTaintedSnapshot"] == null)
          {
            AggregateCacheDependency sqlDeps = new AggregateCacheDependency();
            sqlDeps.Add(new SqlCacheDependency("GDCS", "IKGD_SNAPSHOT"));
            HttpRuntime.Cache.Insert(baseKeyCache + "IsTaintedSnapshot", false, sqlDeps, Cache.NoAbsoluteExpiration, Cache.NoSlidingExpiration, CacheItemPriority.High, null);
          }
          else
            HttpRuntime.Cache[baseKeyCache + "IsTaintedSnapshot"] = false;
          //
        }
        catch (Exception ex)
        {
          Elmah.ErrorSignal.FromCurrentContext().Raise(ex);
        }
      }  // lock
    }


    //
    // scan del VFS per ottenere la lista di tutti i nodi che hanno subito modifiche dall'ultimo UpdatePathFrags
    // viene chiamata solo da PathsUpdateFrags
    //
    private static void PathsUpdateFragsClearUpdatedNodes(FS_Operations fsOp, bool clearForSnapshots, out int vNodeVersionLastNew, out int vDataVersionLastNew)
    {
      vNodeVersionLastNew = vNodeVersionLast;
      vDataVersionLastNew = vDataVersionLast;
      //
      try
      {
        vNodeVersionLastNew = fsOp.DB.IKGD_VNODEs.Max(n => n.version);
        vDataVersionLastNew = fsOp.DB.IKGD_VDATAs.Max(n => n.version);
        //
        int maxNodes = 100;
        List<int> nodesToPurge = null;
        if (vNodeVersionLast > 0 && vDataVersionLast > 0)
        {
          //nodesToPurge =
          //  (from vNode in fsOp.DB.IKGD_VNODEs.Where(n => n.flag_folder)
          //   from vData in fsOp.DB.IKGD_VDATAs.Where(n => n.rnode == vNode.rnode)
          //   where (vNode.version > vNodeVersionLast || vData.version > vDataVersionLast)
          //   select vNode.rnode).Distinct().Take(100).ToList();
          nodesToPurge = fsOp.DB.IKGD_VNODEs.Where(n => n.version > vNodeVersionLast).Select(n => n.rnode).Union(fsOp.DB.IKGD_VDATAs.Where(n => n.version > vDataVersionLast).Select(n => n.rnode)).Distinct().Take(maxNodes).ToList();
        }
        //
        // se le modifiche sono proprio troppe e' meglio vaporizzare tutto e cominciare da capo
        // per evitare di perdere qualche dato buono
        //
        lock (_lock)
        {
          if (nodesToPurge == null || nodesToPurge.Count >= maxNodes)
          {
            _PathsFragsCollection.Clear();
          }
          else
          {
            if (clearForSnapshots)
              _PathsFragsCollection.RemoveAll(n => n.flag_current != true && n.flag_published != true);
            _PathsFragsCollection.RemoveAll(f => nodesToPurge.Contains(f.rNode));
          }
          if (clearForSnapshots)
          {
            // i nodi pubblicati possono cambiare solo dopo una pubblicazione
            // visto che sono stati alterati gli snapshots ne aprofitto per fare un po' di garbage collector
            FragmentsMissing.RemoveAll(n => n.VersionVFS >= 0);
          }
          // i nodi preview possono cambiare in qualsiasi momento
          FragmentsMissing.RemoveAll(n => n.VersionVFS < 0);
        }
      }
      catch (Exception ex)
      {
        Elmah.ErrorSignal.FromCurrentContext().Raise(ex);
      }
    }


    private static List<IKGD_Path_Fragment> PathsUpdateFragsWorkerDB(FS_Operations fsOp, int? VersionFrozenOverride, IEnumerable<int> sNodes, IEnumerable<int> rNodes, bool? foldersOnly, bool fullAccess, string language, bool includeDeleted, out List<FS_Operations.FS_NodeInfo> nodesVFS)
    {
      List<IKGD_Path_Fragment> newFragments = new List<IKGD_Path_Fragment>();
      nodesVFS = null;
      try
      {
        if (!(sNodes != null && sNodes.Any()) && !(rNodes != null && rNodes.Any()))
          return newFragments;
        //
        // lettura dei nodi selezionati negli argomenti
        //
        Expression<Func<IKGD_VNODE, bool>> vNodeFilterScan = fsOp.Get_vNodeFilterACLv2(false);
        Expression<Func<IKGD_VNODE, bool>> vNodeFilter = fsOp.Get_vNodeFilterACLv2(language);
        Expression<Func<IKGD_VDATA, bool>> vDataFilter = fsOp.Get_vDataFilterACLv2(fullAccess, !fullAccess);
        //
        // TODO:
        // bisogna fare in modo che fullAccess si riferisca sempre allo scan sulle aree
        // mentre il flag disabled deve essere SEMPRE ignorato quando uso le chiamate tipo Path*Author*
        // bisogna controllare per bene tutti i pattern ed eventualmente rivedere i nomi dei parametri della API per fare maggiore chiarezza
        //
        if (foldersOnly == true)
          vNodeFilter = vNodeFilter.And(n => n.flag_folder);
        if (sNodes != null && sNodes.Any())
          vNodeFilterScan = vNodeFilterScan.And(n => sNodes.Contains(n.snode));
        if (rNodes != null && rNodes.Any())
          vNodeFilterScan = vNodeFilterScan.And(n => rNodes.Contains(n.rnode));
        //
        // TODO: gestire meglio i filtri per leggere direttamente tutti i dati di published, preview
        // al posto di fsOp.NodesActive<IKGD_XDATA>
        // la gestione degli snapshot e' problematica, in piu' si puo' pensare di bypassare il caching e utilizzarlo
        // solo per published/preview, quindi l'accessor del PathFragsCollectionACL diventa un IQuerable direttamente su DB
        //
        int version = VersionFrozenOverride ?? fsOp.VersionFrozen;
        newFragments =
          (from vNode in fsOp.NodesActive<IKGD_VNODE>(version, includeDeleted).Where(vNodeFilter)  // senza filtri per espandere tutti i symlinks
           from vData in fsOp.NodesActive<IKGD_VDATA>(version, includeDeleted).Where(vDataFilter).Where(n => n.rnode == vNode.rnode)
           where fsOp.NodesActive<IKGD_VNODE>(version, includeDeleted).Where(n => n.rnode == vData.rnode && n.flag_folder == vNode.flag_folder).Any(vNodeFilterScan)  // per attivare il filtro vNodeFilter senza avere nodi degeneri nei risultati
           select new IKGD_Path_Fragment(
              vNode.parent ?? vNode.folder,
              vNode.snode,
              vNode.rnode,
              vNode.version,
              vData.version,
              vNode.version_frozen,
              vData.version_frozen,
              (vNode.flag_published || vData.flag_published),
              (vNode.flag_current || vData.flag_current),
              vNode.flag_folder,
              vData.flag_unstructured,
              ((vNode.flag_deleted || vData.flag_inactive || vData.flag_deleted) == false),
              vData.flags_menu,
              vNode.name,
              vNode.position,
              vData.area,
              vNode.language,
              vData.manager_type,
              vData.category,
              vData.date_activation,
              vData.date_expiry)).ToList();
        //
      }
      catch (Exception ex)
      {
        Elmah.ErrorSignal.FromCurrentContext().Raise(ex);
      }
      return newFragments;
    }


    //
    // la lista in return contiene anche eventuali nodi non folder che non saranno inclusi in _PathFragsCollection
    //
    private static List<IKGD_Path_Fragment> PathsUpdateFragsWorker_OLD(FS_Operations fsOp, IEnumerable<int> sNodes, IEnumerable<int> rNodes, bool? foldersOnly, bool fullAccess, string language)
    {
      List<IKGD_Path_Fragment> newFragments = new List<IKGD_Path_Fragment>();
      try
      {
        List<FS_Operations.FS_NodeInfo> nodesVFS;
        newFragments = PathsUpdateFragsWorkerDB(fsOp, null, sNodes, rNodes, foldersOnly, fullAccess, language, false, out nodesVFS);
        if (fsOp.VersionFrozen > 0)
          return newFragments;
        //
        // aggiornamento dei nodi non trovati su VFS (solo per i folder)
        // TODO: aggiungere in cache anche i nodi non folder in funzione di un flag nel web.config (letto come static property del PathHelper)
        // cosi' si potra' gestire meglio il caching dei path sito per sito
        //
        if (rNodes != null && rNodes.Any())
        {
          lock (_lock)
          {
            rNodes.Except(nodesVFS.Select(n => n.vNode.rnode).Distinct()).Except(FragmentsMissing.Where(f => f.VersionVFS == fsOp.VersionFrozen && f.sNode == 0).Select(f => f.rNode)).ForEach(n => FragmentsMissing.Add(new IKGD_Path_Fragment_Missing { VersionVFS = fsOp.VersionFrozen, rNode = n }));
          }
        }
        if (sNodes != null && sNodes.Any())
        {
          lock (_lock)
          {
            sNodes.Except(nodesVFS.Select(n => n.vNode.snode).Distinct()).Except(FragmentsMissing.Where(f => f.VersionVFS == fsOp.VersionFrozen && f.rNode == 0).Select(f => f.sNode)).ForEach(n => FragmentsMissing.Add(new IKGD_Path_Fragment_Missing { VersionVFS = fsOp.VersionFrozen, sNode = n }));
          }
        }
        //
        // pulizia dei frammenti che devono essere sovrascritti dai nuovi valori appena letti
        //
        lock (_lock)
        {
          var fragsToPurge =
            (from node in nodesVFS.Where(n => n.vNode.flag_folder)
             from frag in _PathsFragsCollection.Where(f => f.sNode == node.vNode.snode)
             where (node.vNode.version_frozen == frag.vNodeVersionFrozen && node.vData.version_frozen == frag.vDataVersionFrozen)
               || ((node.vNode.flag_published || node.vData.flag_published) == true && frag.flag_published == true)
               || ((node.vNode.flag_current || node.vData.flag_current) == true && frag.flag_current == true)
             select frag).ToList();
          fragsToPurge.ForEach(f => _PathsFragsCollection.Remove(f));
          _PathsFragsCollection.AddRange(newFragments.Where(n => n.flag_folder));
        }
      }
      catch (Exception ex)
      {
        Elmah.ErrorSignal.FromCurrentContext().Raise(ex);
      }
      return newFragments;
    }


    private static List<IKGD_Path_Fragment> PathsUpdateFragsWorker(FS_Operations fsOp, IEnumerable<int> sNodes, IEnumerable<int> rNodes, bool? foldersOnly, bool fullAccess, string language)
    {
      List<IKGD_Path_Fragment> newFragments = new List<IKGD_Path_Fragment>();
      try
      {
        if (!(sNodes != null && sNodes.Any()) && !(rNodes != null && rNodes.Any()))
          return newFragments;
        //
        if (sNodes == null)
          sNodes = new List<int>();
        if (rNodes == null)
          rNodes = new List<int>();
        //
        newFragments =
          (from vNode in fsOp.DB.IKGD_VNODEs.Where(n => n.flag_deleted == false)  // senza filtri per espandere tutti i symlinks
           from vData in fsOp.DB.IKGD_VDATAs.Where(n => n.flag_deleted == false && n.flag_inactive == false).Where(n => n.rnode == vNode.rnode && ((n.flag_current == true && vNode.flag_current == true) || (n.flag_published == true && vNode.flag_published == true)))
           where fsOp.DB.IKGD_VNODEs.Where(n => n.rnode == vData.rnode && ((n.flag_current == true && vData.flag_current == true) || (n.flag_published == true && vData.flag_published == true))).Any(n => sNodes.Contains(n.snode) || rNodes.Contains(n.rnode))
           orderby vNode.version descending, vData.version descending  // per evitare i problemi di path raddoppiati in caso di nodi farlocchi degeneri: prendiamo le risorse piu' nuove
           select new IKGD_Path_Fragment(
              vNode.parent ?? vNode.folder,
              vNode.snode,
              vNode.rnode,
              vNode.version,
              vData.version,
              vNode.version_frozen,
              vData.version_frozen,
              (vNode.flag_published || vData.flag_published),
              (vNode.flag_current || vData.flag_current),
              vNode.flag_folder,
              vData.flag_unstructured,
              ((vNode.flag_deleted || vData.flag_inactive || vData.flag_deleted) == false),
              vData.flags_menu,
              vNode.name,
              vNode.position,
              vData.area,
              vNode.language,
              vData.manager_type,
              vData.category,
              vData.date_activation,
              vData.date_expiry)
             ).AsEnumerable().Distinct((r1, r2) => r1.sNode == r2.sNode).ToList();  // per evitare i problemi di path raddoppiati in caso di nodi farlocchi degeneri
        //
        if (rNodes != null && rNodes.Any())
        {
          lock (_lock)
          {
            rNodes.Except(newFragments.Select(n => n.rNode).Distinct()).Except(FragmentsMissing.Where(f => f.VersionVFS == fsOp.VersionFrozen && f.sNode == 0).Select(f => f.rNode)).ForEach(n => FragmentsMissing.Add(new IKGD_Path_Fragment_Missing { VersionVFS = fsOp.VersionFrozen, rNode = n }));
          }
        }
        if (sNodes != null && sNodes.Any())
        {
          lock (_lock)
          {
            sNodes.Except(newFragments.Select(n => n.sNode).Distinct()).Except(FragmentsMissing.Where(f => f.VersionVFS == fsOp.VersionFrozen && f.rNode == 0).Select(f => f.sNode)).ForEach(n => FragmentsMissing.Add(new IKGD_Path_Fragment_Missing { VersionVFS = fsOp.VersionFrozen, sNode = n }));
          }
        }
        //
        // pulizia dei frammenti che devono essere sovrascritti dai nuovi valori appena letti
        //
        var rNodesToPurge = newFragments.Select(n => n.rNode).Distinct().ToList();
        if (rNodesToPurge.Any())
        {
          lock (_lock)
          {
            _PathsFragsCollection.RemoveAll(r => rNodesToPurge.Contains(r.rNode));
            _PathsFragsCollection.AddRange(newFragments);
          }
        }
        //
        if (newFragments.Any())
        {
          if (foldersOnly == true)
          {
            newFragments.RemoveAll(f => f.flag_folder == false);
          }
          if (!fullAccess && !fsOp.IsRoot)
          {
            if (fsOp.CurrentAreasExtended.AreaMatchMode == FS_Areas_Extended.AreaMatchModeEnum.FilterByAllowed)
              newFragments.RemoveAll(f => !fsOp.CurrentAreasExtended.AreasAllowed.Contains(f.Area));
            else if (fsOp.CurrentAreasExtended.AreaMatchMode == FS_Areas_Extended.AreaMatchModeEnum.FilterByDenied)
              newFragments.RemoveAll(f => fsOp.CurrentAreasExtended.AreasDenied.Contains(f.Area));
          }
          string lang = language ?? fsOp.LanguageNN;
          if (lang != FS_Operations.LanguageNoFilterCode)
          {
            newFragments.RemoveAll(f => f.Language != null && !string.Equals(f.Language, lang));
          }
        }
      }
      catch (Exception ex)
      {
        Elmah.ErrorSignal.FromCurrentContext().Raise(ex);
      }
      return newFragments;
    }


    public static List<IKGD_Path_Fragment> PathsGetStartFragments(FS_Operations fsOp, int? VersionFrozenOverride, IEnumerable<int> sNodes, IEnumerable<int> rNodes, bool? foldersOnly, bool fullAccess, string language, bool includeDeleted, bool? forceNoOpt, bool? allEquivalentNodes)
    {
      List<IKGD_Path_Fragment> frags = null;
      if (sNodes != null && !sNodes.Any())
        sNodes = null;
      if (rNodes != null && !rNodes.Any())
        rNodes = null;
      allEquivalentNodes = allEquivalentNodes.GetValueOrDefault(false);
      if (forceNoOpt != true)
      {
        if (allEquivalentNodes.Value == true && sNodes != null && sNodes.Any())
        {
          var fragsTmp = fsOp.PathsFragsCollectionContext(VersionFrozenOverride, null, true).Where(f => sNodes.Any(n => n == f.sNode));
          var rNodesTmp = fragsTmp.Select(f => f.rNode).Distinct().ToList();
          if (rNodesTmp.Any())
          {
            rNodes = (rNodes ?? Enumerable.Empty<int>()).Union(rNodesTmp).ToList();
            // annulliamo sNodes solo se abbiamo trovato TUTTI i riferimenti nella cache e non un subset solo per alcuni rnodes
            if (rNodesTmp.Count != sNodes.Distinct().Count())
            {
              var sNodesFromFrags = fragsTmp.Select(f => f.sNode).ToList();
              if (sNodes.All(r => sNodesFromFrags.Contains(r)))
              {
                sNodes = null;
              }
            }
          }
        }
        frags = fsOp.PathsFragsCollectionContext(VersionFrozenOverride, null, true).Where(f => (sNodes != null && sNodes.Any(n => n == f.sNode)) || (rNodes != null && rNodes.Any(n => n == f.rNode))).ToList();
        if ((sNodes == null || sNodes.All(n => frags.Any(f => f.sNode == n))) && (rNodes == null || rNodes.All(n => frags.Any(f => f.rNode == n))))
        {
          IEnumerable<IKGD_Path_Fragment> frags_active = frags.Where(f => f.flag_active);
          if (foldersOnly.GetValueOrDefault(false))
            frags_active = frags_active.Where(f => f.flag_folder);
          if (language != FS_Operations.LanguageNoFilterCode)
            frags_active = frags_active.Where(f => f.Language == null || f.Language == language);
          if (!fullAccess && !fsOp.IsRoot)
          {
            //frags_active = frags_active.Where(f => fsOp.CurrentAreasExtended.AreasAllowed.Contains(f.Area));
            if (fsOp.CurrentAreasExtended.AreaMatchMode == FS_Areas_Extended.AreaMatchModeEnum.FilterByAllowed)
              frags_active = frags_active.Where(f => fsOp.CurrentAreasExtended.AreasAllowed.Contains(f.Area));
            else if (fsOp.CurrentAreasExtended.AreaMatchMode == FS_Areas_Extended.AreaMatchModeEnum.FilterByDenied)
              frags_active = frags_active.Where(f => !fsOp.CurrentAreasExtended.AreasDenied.Contains(f.Area));
          }
          frags = frags_active.ToList();
        }
        else
          frags = null;
      }
      if (frags == null && ((sNodes != null && sNodes.Any()) || (rNodes != null && rNodes.Any())))
      {
        if (allEquivalentNodes.Value == true)
        {
          // saltiamo il post filter sui nodes perche' rNodes potrebbe non essere completo, in ogni caso PathsUpdateFragsWorker ritorna il set corretto per questo scan
          frags = PathsUpdateFragsWorker(fsOp, sNodes, rNodes, foldersOnly, fullAccess, language).ToList();
          //frags = PathsUpdateFragsWorker(fsOp, sNodes, rNodes, foldersOnly, fullAccess, language).Where(f => rNodes == null || rNodes.Any(n => n == f.rNode)).ToList();  // broken version
        }
        else
        {
          frags = PathsUpdateFragsWorker(fsOp, sNodes, rNodes, foldersOnly, fullAccess, language).Where(f => (sNodes != null && sNodes.Any(n => n == f.sNode)) || (rNodes != null && rNodes.Any(n => n == f.rNode))).ToList();
        }
      }
      if (frags != null && !frags.Any())
        frags = null;
      return frags;
    }


    public static List<IKGD_Path> PathsRefineV3(this FS_Operations fsOp, List<IKGD_Path> paths, bool foldersOnly, bool fullAccess, string language, bool sortPaths, bool? forceNoOpt, int? VersionFrozenOverride)
    {
      try
      {
        //
        // per gli snapshot non ho uno schema valido di gestione del caching/invalidazione delle informazioni
        //
        forceNoOpt |= Utility.TryParse<bool>(IKGD_Config.AppSettings["IKGD_Path_forceNoOpt"]);
        if (forceNoOpt == true || fsOp.VersionFrozen > 0)
          return fsOp.PathsRefineNoOpt(VersionFrozenOverride, paths, foldersOnly, false, sortPaths, fullAccess, language, false);
        //
        // gestione del garbage collector
        //
        GarbageCollectorWorker(false);
        //
        // scan iniziale dei path per inizializzare le risorse prima della procedura iterativa
        //
        List<IKGD_Path> pathsWorker = new List<IKGD_Path>(paths);
        paths.Clear();
        List<int> sNodes = pathsWorker.Where(p => !p.Fragments.Any()).Select(p => p.sNode).ToList();
        if (sNodes.Any())
        {
          lock (_lock)
          {
            foreach (IKGD_Path_Fragment frag in fsOp.PathsFragsCollectionContext(VersionFrozenOverride, language, fullAccess).Where(f => sNodes.Contains(f.sNode)))
              pathsWorker.Where(p => p.sNode == frag.sNode && !p.Fragments.Any()).ForEach(p => p.InsertFragment(frag));
          }
          //
          // verifichiamo che non ci siano ancora nodi da recuperare
          //
          List<int> sNodesToLoad = pathsWorker.Where(p => !p.Fragments.Any()).Select(p => p.sNode).Distinct().ToList();
          if (sNodesToLoad.Any())
          {
            List<IKGD_Path_Fragment> newFrags = PathsUpdateFragsWorker(fsOp, sNodesToLoad, null, foldersOnly, fullAccess, language);
            foreach (IKGD_Path_Fragment frag in newFrags)
              pathsWorker.Where(p => p.sNode == frag.sNode && !p.Fragments.Any()).ForEach(p => p.InsertFragment(frag));
          }
          //
          // spostamento dei path non risolvibili in pathOut e aggiornamento dei nodi non risolvibili
          //
          var pathsBroken = pathsWorker.Where(p => !p.Fragments.Any()).ToList();
          pathsBroken.ForEach(p => pathsWorker.Remove(p));
          paths.AddRange(pathsBroken);
          lock (_lock)
          {
            pathsBroken.Where(r => !FragmentsMissing.Any(f => f.sNode == r.sNode && f.rNode == 0 && f.VersionVFS == fsOp.VersionFrozen)).ForEach(p => FragmentsMissing.Add(new IKGD_Path_Fragment_Missing { sNode = p.sNode, VersionVFS = fsOp.VersionFrozen }));
          }
        }
        //
        // ricostruzione iterativa del path
        //
        int outerLoopMax = Utility.TryParse<int>(IKGD_Config.AppSettings["IKGD_Path_MaxRecursionLevels"], 25);
        int innerLoopMax = outerLoopMax;
        for (int outerLoop = 0; outerLoop < outerLoopMax; outerLoop++)
        {
          List<int> rNodesToLoad = new List<int>();
          for (int innerLoop = 0; innerLoop < innerLoopMax; innerLoop++)
          {
            paths.AddRange(pathsWorker.Where(p => p.IsRooted));
            pathsWorker.RemoveAll(p => p.IsRooted);
            if (!pathsWorker.Any())
              break;
            //
            lock (_lock)
            {
              var pathsWithBrokenNodes =
                (from fm in FragmentsMissing.Where(f => f.VersionVFS == fsOp.VersionFrozen)
                 from bp in pathsWorker.Where(p => (p.FirstFragment.rNode == fm.rNode && fm.sNode == 0) || (p.FirstFragment.sNode == fm.sNode && fm.rNode == 0))
                 select bp).ToList();
              paths.AddRange(pathsWithBrokenNodes);
              pathsWithBrokenNodes.ForEach(p => pathsWorker.Remove(p));
            }
            if (!pathsWorker.Any())
              break;
            //
            List<ParentFragPair> parentingData = null;
            lock (_lock)
            {
              parentingData =
                (from path in pathsWorker
                 from frag in fsOp.PathsFragsCollectionContext(VersionFrozenOverride, language, fullAccess).Where(f => f.flag_folder && f.rNode == path.FirstFragment.Parent).DefaultIfEmpty()
                 where frag == null || !path.Fragments.Any(f => f.rNode == frag.rNode)  // per evitare le ricorsioni nei path e i risultati non piu' espandibili
                 select new ParentFragPair { path = path, frag = frag }).ToList();
            }
            rNodesToLoad.AddRange(parentingData.Where(r => r.frag == null).Select(r => r.path.FirstFragment.Parent).Distinct());
            rNodesToLoad.RemoveAll(r => FragmentsMissing.Any(f => f.rNode == r && f.VersionVFS == fsOp.VersionFrozen));
            if (!parentingData.Any(r => r.frag != null))
              break;
            //
            foreach (var gPath in parentingData.Where(r => r.frag != null).GroupBy(r => r.path))
            {
              var gPath_active = gPath.Where(fp => fp.frag.Language == null || fp.path.IsLanguageAccessible(fp.frag.Language));
              var gPath_active_first = gPath_active.FirstOrDefault();
              if (gPath_active_first != null)
              {
                gPath_active.Skip(1).ForEach(r =>
                {
                  try
                  {
                    IKGD_Path p = r.path.Clone() as IKGD_Path;
                    p.InsertFragment(r.frag);
                    pathsWorker.Add(p);
                  }
                  catch (Exception ex)
                  {
                    Elmah.ErrorSignal.FromCurrentContext().Raise(ex);
                  }
                });
                gPath_active_first.path.InsertFragment(gPath_active_first.frag);
              }
            }
          }  // innerLoop
          if (!pathsWorker.Any())
            break;
          //
          if (rNodesToLoad.Any())
          {
            List<IKGD_Path_Fragment> newFrags = PathsUpdateFragsWorker(fsOp, null, rNodesToLoad.Distinct(), true, fullAccess, language);
            lock (_lock)
            {
              rNodesToLoad.Except(newFrags.Select(f => f.rNode)).Distinct().Where(r => !FragmentsMissing.Any(f => f.rNode == r && f.sNode == 0 && f.VersionVFS == fsOp.VersionFrozen)).ForEach(r => FragmentsMissing.Add(new IKGD_Path_Fragment_Missing { rNode = r, VersionVFS = fsOp.VersionFrozen }));
            }
          }
          else
          {
            break;
          }
          if ((outerLoop + 1) == outerLoopMax)
          {
            IKCMS_ExecutionProfiler.AddMessage("PathsRefineV2: OOPS! ----- maximum recursion level reached: problem with recursion -----");
            Elmah.ErrorSignal.FromCurrentContext().Raise(new Exception("PathsRefineV2: OOPS! maximum recursion level reached: problem with recursion:  rNodesToLoad:{0}  pathsWorker:{1}  paths:{2}".FormatString(Utility.Implode(rNodesToLoad, ","), Utility.Implode(pathsWorker.Select(p => p.sNode), ","), Utility.Implode(paths.Select(p => p.sNode), ","))));
          }
        }  // outerLoop
      }
      catch (Exception ex)
      {
        Elmah.ErrorSignal.FromCurrentContext().Raise(ex);
        //
        // in caso di eccezioni nel build del path, forzo una pulizia del GarbageCollector e forzo la chiamata della versione non ottimizzata
        //
        GarbageCollectorWorker(true);
        return fsOp.PathsRefineNoOpt(VersionFrozenOverride, paths, foldersOnly, false, sortPaths, fullAccess, language, false);
      }
      //
      try
      {
        paths.RemoveAll(p => !p.IsValid || !p.IsRooted);
        if (sortPaths)
          paths.Sort();
      }
      catch (Exception ex)
      {
        Elmah.ErrorSignal.FromCurrentContext().Raise(ex);
      }
      //
      return paths;
    }


    public static List<IKGD_Path> PathsRefineV2(this FS_Operations fsOp, List<IKGD_Path> paths, bool foldersOnly, bool fullAccess, string language, bool sortPaths, bool? forceNoOpt, int? VersionFrozenOverride)
    {
      try
      {
        //
        // per gli snapshot non ho uno schema valido di gestione del caching/invalidazione delle informazioni
        //
        forceNoOpt |= Utility.TryParse<bool>(IKGD_Config.AppSettings["IKGD_Path_forceNoOpt"]);
        //
        if (forceNoOpt == true || fsOp.VersionFrozen > 0)
          return fsOp.PathsRefineNoOpt(VersionFrozenOverride, paths, foldersOnly, false, sortPaths, fullAccess, language, false);
        //
        // gestione del garbage collector
        //
        GarbageCollectorWorker(false);
        //
        // scan iniziale dei path per inizializzare le risorse prima della procedura iterativa
        //
        List<IKGD_Path> pathsWorker = new List<IKGD_Path>(paths);
        paths.Clear();
        List<int> sNodes = pathsWorker.Where(p => !p.Fragments.Any()).Select(p => p.sNode).ToList();
        if (sNodes.Any())
        {
          lock (_lock)
          {
            foreach (IKGD_Path_Fragment frag in fsOp.PathsFragsCollectionContext(VersionFrozenOverride, language, fullAccess).Where(f => sNodes.Contains(f.sNode)))
              pathsWorker.Where(p => p.sNode == frag.sNode && !p.Fragments.Any()).ForEach(p => p.InsertFragment(frag));
          }
          //
          // verifichiamo che non ci siano ancora nodi da recuperare
          //
          List<int> sNodesToLoad = pathsWorker.Where(p => !p.Fragments.Any()).Select(p => p.sNode).Distinct().ToList();
          if (sNodesToLoad.Any())
          {
            List<IKGD_Path_Fragment> newFrags = PathsUpdateFragsWorker(fsOp, sNodesToLoad, null, foldersOnly, fullAccess, language);
            foreach (IKGD_Path_Fragment frag in newFrags)
              pathsWorker.Where(p => p.sNode == frag.sNode && !p.Fragments.Any()).ForEach(p => p.InsertFragment(frag));
          }
          //
          // spostamento dei path non risolvibili in pathOut e aggiornamento dei nodi non risolvibili
          //
          var pathsBroken = pathsWorker.Where(p => !p.Fragments.Any()).ToList();
          pathsBroken.ForEach(p => pathsWorker.Remove(p));
          paths.AddRange(pathsBroken);
          //
          // con la gestione delle ACL potrebbe crearsi qualche casino...
          //lock (_lock)
          //{
          //  pathsBroken.ForEach(p => FragmentsMissing.Add(new IKGD_Path_Fragment_Missing { sNode = p.sNode, VersionVFS = fsOp.VersionFrozen }));
          //}
          if (foldersOnly)
          {
            pathsWorker.RemoveAll(p => !p.IsFolder);
          }
          if (language != null && language != FS_Operations.LanguageNoFilterCode)
          {
            pathsWorker.RemoveAll(p => !p.IsLanguageAccessible(language));
          }
          if (!fullAccess && !fsOp.IsRoot)
          {
            //pathsWorker.RemoveAll(p => p.Fragments.Any(f => !fsOp.CurrentAreasExtended.AreasAllowed.Contains(f.Area)));
            if (fsOp.CurrentAreasExtended.AreaMatchMode == FS_Areas_Extended.AreaMatchModeEnum.FilterByAllowed)
              pathsWorker.RemoveAll(p => p.Fragments.Any(f => !fsOp.CurrentAreasExtended.AreasAllowed.Contains(f.Area)));
            else if (fsOp.CurrentAreasExtended.AreaMatchMode == FS_Areas_Extended.AreaMatchModeEnum.FilterByDenied)
              pathsWorker.RemoveAll(p => p.Fragments.Any(f => fsOp.CurrentAreasExtended.AreasDenied.Contains(f.Area)));
          }
        }
        //
        // ricostruzione iterativa del path
        //
        List<int> rNodesToLoadLast = null;
        List<int> rNodesToLoadLast2 = null;
        List<IKGD_Path_Fragment_Missing> FragmentsMissingLocal = new List<IKGD_Path_Fragment_Missing>();
        int outerLoopMax = Utility.TryParse<int>(IKGD_Config.AppSettings["IKGD_Path_MaxRecursionLevels"], 25);
        int innerLoopMax = outerLoopMax;
        for (int outerLoop = 0; outerLoop < outerLoopMax; outerLoop++)
        {
          List<int> rNodesToLoad = new List<int>();
          for (int innerLoop = 0; innerLoop < innerLoopMax; innerLoop++)
          {
            paths.AddRange(pathsWorker.Where(p => p.IsRooted));
            pathsWorker.RemoveAll(p => p.IsRooted);
            if (!pathsWorker.Any())
              break;
            //
            //TODO: a volte rimane bloccato e genera System.Threading.ThreadAbortException: Thread was being aborted.
            lock (_lock)
            {
              var pathsWithBrokenNodes =
                (from fm in FragmentsMissing.Concat(FragmentsMissingLocal).Where(f => f.VersionVFS == fsOp.VersionFrozen)
                 from bp in pathsWorker.Where(p => (p.FirstFragment.rNode == fm.rNode && fm.sNode == 0) || (p.FirstFragment.sNode == fm.sNode && fm.rNode == 0))
                 select bp).ToList();
              paths.AddRange(pathsWithBrokenNodes);
              pathsWithBrokenNodes.ForEach(p => pathsWorker.Remove(p));
            }
            if (!pathsWorker.Any())
              break;
            //
            List<ParentFragPair> parentingData = null;
            //TODO: a volte rimane bloccato e genera System.Threading.ThreadAbortException: Thread was being aborted.
            lock (_lock)
            {
              parentingData =
                (from path in pathsWorker
                 from frag in fsOp.PathsFragsCollectionContext(VersionFrozenOverride, language, fullAccess).Where(f => f.flag_folder && f.rNode == path.FirstFragment.Parent).DefaultIfEmpty()
                 where frag == null || !path.Fragments.Any(f => f.rNode == frag.rNode)  // per evitare le ricorsioni nei path e i risultati non piu' espandibili
                 select new ParentFragPair { path = path, frag = frag }).ToList();
            }
            lock (_lock)
            {
              pathsWorker.RemoveAll(r => !r.IsRooted && FragmentsMissing.Any(f => f.rNode == r.FirstFragment.Parent && f.sNode == 0 && f.VersionVFS == fsOp.VersionFrozen));
              if (FragmentsMissingLocal.Any())
              {
                pathsWorker.RemoveAll(r => !r.IsRooted && FragmentsMissingLocal.Any(f => f.rNode == r.FirstFragment.Parent && f.sNode == 0 && f.VersionVFS == fsOp.VersionFrozen));
              }
            }
            rNodesToLoad.AddRange(parentingData.Where(r => r.frag == null).Select(r => r.path.FirstFragment.Parent).Distinct());
            lock (_lock)
            {
              rNodesToLoad.RemoveAll(r => FragmentsMissing.Any(f => f.rNode == r && f.sNode == 0 && f.VersionVFS == fsOp.VersionFrozen));
              if (FragmentsMissingLocal.Any())
              {
                rNodesToLoad.RemoveAll(r => FragmentsMissingLocal.Any(f => f.rNode == r && f.sNode == 0 && f.VersionVFS == fsOp.VersionFrozen));
              }
            }
            if (!parentingData.Any(r => r.frag != null))
              break;
            //
            foreach (var gPath in parentingData.Where(r => r.frag != null).GroupBy(r => r.path))
            {
              var gPath_active = gPath.Where(fp => fp.frag.Language == null || fp.path.IsLanguageAccessible(fp.frag.Language));
              var gPath_active_first = gPath_active.FirstOrDefault();
              if (gPath_active_first != null)
              {
                gPath_active.Skip(1).ForEach(r =>
                {
                  try
                  {
                    IKGD_Path p = r.path.Clone() as IKGD_Path;
                    p.InsertFragment(r.frag);
                    pathsWorker.Add(p);
                  }
                  catch (Exception ex)
                  {
                    Elmah.ErrorSignal.FromCurrentContext().Raise(ex);
                  }
                });
                gPath_active_first.path.InsertFragment(gPath_active_first.frag);
              }
            }
            //
            if ((innerLoop + 1) == innerLoopMax)
            {
              //IKCMS_ExecutionProfiler.AddMessage("PathsRefineV2: OOPS! ----- maximum inner recursion level reached: maybe a problem with recursion -----");
              //Elmah.ErrorSignal.FromCurrentContext().Raise(new Exception("PathsRefineV2: OOPS! maximum inner recursion level reached: maybe a problem with recursion:  rNodesToLoad:{0}  pathsWorker:{1}  paths:{2}".FormatString(Utility.Implode(rNodesToLoad, ","), Utility.Implode(pathsWorker.Select(p => p.sNode), ","), Utility.Implode(paths.Select(p => p.sNode), ","))));
            }
          }  // innerLoop
          if (!pathsWorker.Any())
            break;
          //
          // siccome PathsFragsCollectionContext ritorna dei null per nodi presenti ma non accessibili (es. lingua, acl, version ecc.)
          // manteniamo una history delle richieste di load e consideriamo un nodo broken solo dopo due richieste non soddisfatte
          // questo consente di gestire almeno parzialmente updates paralleli a PathsFragsCollectionContext
          // e gestire correttamente il termine della ricorsione
          // 
          if (rNodesToLoadLast != null && rNodesToLoadLast2 != null && rNodesToLoad.Intersect(rNodesToLoadLast).Intersect(rNodesToLoadLast2).Any())
          {
            var brokenNodes = rNodesToLoad.Intersect(rNodesToLoadLast).Intersect(rNodesToLoadLast2).ToList();
            rNodesToLoad = rNodesToLoad.Except(brokenNodes).ToList();
            pathsWorker.RemoveAll(p => !p.IsRooted && brokenNodes.Contains(p.FirstFragment.Parent));
            if (!pathsWorker.Any())
              break;
          }
          //
          if (rNodesToLoad.Any())
          {
            //var nodes2load = rNodesToLoad.Distinct().ToList();
            //IKCMS_ExecutionProfiler.AddMessage("PathsRefineV2: outerLoop={0}  nodes2load={1} --> {2}".FormatString(outerLoop, nodes2load.Count, Utility.Implode(nodes2load, ",")));
            List<IKGD_Path_Fragment> newFrags = PathsUpdateFragsWorker(fsOp, null, rNodesToLoad.Distinct(), true, fullAccess, language);
            //IKCMS_ExecutionProfiler.AddMessage("PathsRefineV2: newFrags={0} --> {1}".FormatString(newFrags.Count, Utility.Implode(newFrags.Select(f => f.rNode), ",")));
            lock (_lock)
            {
              // attenzione che in questo contesto stiamo gestendo dei frags filtrati per ACL e altri flags, non possiamo registrare i missing frags
              // nel contenitore globale altrimenti si compromettono i paths relativi ad aree riservate
              rNodesToLoad.Except(newFrags.Select(f => f.rNode)).Distinct().Where(r => !FragmentsMissing.Concat(FragmentsMissingLocal).Any(f => f.rNode == r && f.sNode == 0 && f.VersionVFS == fsOp.VersionFrozen)).ForEach(r => FragmentsMissingLocal.Add(new IKGD_Path_Fragment_Missing { rNode = r, VersionVFS = fsOp.VersionFrozen }));
              //rNodesToLoad.Except(newFrags.Select(f => f.rNode)).Distinct().Where(r => !FragmentsMissing.Any(f => f.rNode == r && f.sNode == 0 && f.VersionVFS == fsOp.VersionFrozen)).ForEach(r => FragmentsMissing.Add(new IKGD_Path_Fragment_Missing { rNode = r, VersionVFS = fsOp.VersionFrozen }));
            }
            //IKCMS_ExecutionProfiler.AddMessage("PathsRefineV2: FragmentsMissing={0} --> {1}".FormatString(FragmentsMissing.Where(f => f.sNode == 0).Count(), Utility.Implode(FragmentsMissing.Where(f => f.sNode == 0).Select(f => f.rNode), ",")));
          }
          else
          {
            break;
          }
          rNodesToLoadLast2 = rNodesToLoadLast;
          rNodesToLoadLast = rNodesToLoad;
          if ((outerLoop + 1) == outerLoopMax)
          {
            IKCMS_ExecutionProfiler.AddMessage("PathsRefineV2: OOPS! ----- maximum recursion level reached: problem with recursion -----");
            Elmah.ErrorSignal.FromCurrentContext().Raise(new Exception("PathsRefineV2: OOPS! maximum recursion level reached: problem with recursion:  rNodesToLoad:{0}  pathsWorker:{1}  paths:{2}".FormatString(Utility.Implode(rNodesToLoad, ","), Utility.Implode(pathsWorker.Select(p => p.sNode), ","), Utility.Implode(paths.Select(p => p.sNode), ","))));
          }
        }  // outerLoop
      }
      catch (Exception ex)
      {
        Elmah.ErrorSignal.FromCurrentContext().Raise(ex);
      }
      //
      paths.RemoveAll(p => !p.IsValid || !p.IsRooted);
      //
      if (sortPaths)
        paths.Sort();
      //
      return paths;
    }


    //
    // versione non ottimizzata da utilizzare all'occorrenza, oppure quando FS_OperationsHelpers.VersionFrozenSession > 0, oppure per lettura in blocco di grossi volumi di paths
    //
    public static List<IKGD_Path> PathsRefineNoOpt(this FS_Operations fsOp, int? VersionFrozenOverride, List<IKGD_Path> paths, bool foldersOnly, bool allowUnRooted, bool sortPaths, bool fullAccess, string language, bool includeDeleted) { return PathsRefineNoOpt(fsOp, VersionFrozenOverride, paths, foldersOnly, allowUnRooted, sortPaths, fullAccess, language, includeDeleted, false); }
    public static List<IKGD_Path> PathsRefineNoOpt(this FS_Operations fsOp, int? VersionFrozenOverride, List<IKGD_Path> paths, bool foldersOnly, bool allowUnRooted, bool sortPaths, bool fullAccess, string language, bool includeDeleted, bool unconstrained)
    {
      List<IKGD_Path> pathsWorker = null;
      try
      {
        pathsWorker = new List<IKGD_Path>(paths);
        paths.Clear();
        //
        // scan iniziale dei path per inizializzare le risorse prima della procedura iterativa
        //
        List<FS_Operations.FS_NodeInfo> nodesVFS;
        List<IKGD_Path_Fragment> scanFrags = new List<IKGD_Path_Fragment>();
        List<int> sNodes = pathsWorker.Where(p => !p.Fragments.Any()).Select(p => p.sNode).Distinct().ToList();
        if (sNodes.Any())
        {
          scanFrags.AddRange(PathsUpdateFragsWorkerDB(fsOp, VersionFrozenOverride, sNodes, null, foldersOnly, fullAccess, language, includeDeleted, out nodesVFS));
          foreach (IKGD_Path_Fragment frag in scanFrags)
            pathsWorker.Where(p => p.sNode == frag.sNode && !p.Fragments.Any()).ForEach(p => p.InsertFragment(frag));
          //
          // spostamento dei path non risolvibili in pathOut e aggiornamento dei nodi non risolvibili
          //
          var pathsBroken = pathsWorker.Where(p => !p.Fragments.Any()).ToList();  // conversione a lista perche' poi la sequence deve venire alterata
          pathsBroken.ForEach(p => pathsWorker.Remove(p));
          paths.AddRange(pathsBroken);
        }
        //
        // ricostruzione iterativa del path
        //
        int innerLoopMax = Utility.TryParse<int>(IKGD_Config.AppSettings["IKGD_Path_MaxRecursionLevels"], 25);
        for (int innerLoop = 0; innerLoop < innerLoopMax; innerLoop++)
        {
          paths.AddRange(pathsWorker.Where(p => p.IsRooted));
          pathsWorker.RemoveAll(p => p.IsRooted);
          if (!pathsWorker.Any())
            break;
          //
          List<int> rNodesNew = pathsWorker.Select(p => p.FirstFragment.Parent).Except(scanFrags.Select(f => f.rNode)).Distinct().ToList();
          if (rNodesNew.Any())
            scanFrags.AddRange(PathsUpdateFragsWorkerDB(fsOp, VersionFrozenOverride, null, rNodesNew, true, fullAccess, language, includeDeleted, out nodesVFS));
          var parents = pathsWorker.Select(p => p.FirstFragment.Parent).Distinct().ToList();
          var parentingData =
            (from path in pathsWorker
             from frag in scanFrags.Where(f => parents.Contains(f.rNode)).Where(f => f.flag_folder && f.rNode == path.FirstFragment.Parent)
             where !path.Fragments.Any(f => f.rNode == frag.rNode)  // per evitare le ricorsioni nei path e i risultati non piu' espandibili
             select new { path = path, frag = frag }).ToList();
          if (!parentingData.Any())
            break;
          //
          foreach (var gPath in parentingData.Where(r => r.frag != null).GroupBy(r => r.path))
          {
            var gPath_active = unconstrained ? gPath : gPath.Where(fp => fp.frag.Language == null || fp.path.IsLanguageAccessible(fp.frag.Language));
            var gPath_active_first = gPath_active.FirstOrDefault();
            if (gPath_active_first != null)
            {
              gPath_active.Skip(1).ForEach(r =>
              {
                try
                {
                  IKGD_Path p = r.path.Clone() as IKGD_Path;
                  p.InsertFragment(r.frag);
                  pathsWorker.Add(p);
                }
                catch (Exception ex)
                {
                  Elmah.ErrorSignal.FromCurrentContext().Raise(ex);
                }
              });
              gPath_active_first.path.InsertFragment(gPath_active_first.frag);
            }
          }
        }  // innerLoop
      }
      catch (Exception ex)
      {
        Elmah.ErrorSignal.FromCurrentContext().Raise(ex);
      }
      //
      try
      {
        paths.RemoveAll(p => !p.IsValid);
        if (allowUnRooted)
        {
          if (sortPaths)
            paths.Sort();
          var nodes = paths.Select(p => p.sNode).Distinct().ToList();
          paths.AddRange(pathsWorker.Where(p => p.IsValid && !nodes.Contains(p.sNode)));
        }
        else
        {
          paths.RemoveAll(p => !p.IsRooted);
          if (sortPaths)
            paths.Sort();
        }
      }
      catch (Exception ex)
      {
        Elmah.ErrorSignal.FromCurrentContext().Raise(ex);
      }
      //
      return paths;
    }


    public static List<IKGD_Path> PathsFromNodeExt(this FS_Operations fsOp, int sNode) { return PathsFromNodesExt(fsOp, new int[] { sNode }, null, false, false, fsOp.LanguageNN, false, null); }
    public static List<IKGD_Path> PathsFromNodeExt(this FS_Operations fsOp, int sNode, bool foldersOnly, bool fullAccess, bool filterLanguage) { return PathsFromNodesExt(fsOp, new int[] { sNode }, null, foldersOnly, fullAccess, filterLanguage ? IKGD_Language_Provider.Provider.LanguageNoFilterCode : FS_Operations.LanguageNoFilterCode, false, null); }
    public static List<IKGD_Path> PathsFromNodeExt(this FS_Operations fsOp, int sNode, bool foldersOnly, bool fullAccess, bool filterLanguage, bool? forceNoOpt) { return PathsFromNodesExt(fsOp, new int[] { sNode }, null, foldersOnly, fullAccess, filterLanguage ? IKGD_Language_Provider.Provider.LanguageNoFilterCode : FS_Operations.LanguageNoFilterCode, forceNoOpt, null); }
    public static List<IKGD_Path> PathsFromNodeExt(this FS_Operations fsOp, int sNode, bool foldersOnly, bool fullAccess, string language, bool? forceNoOpt) { return PathsFromNodesExt(fsOp, new int[] { sNode }, null, foldersOnly, fullAccess, language, forceNoOpt, null); }


    public static List<IKGD_Path> PathsFromNodesExt(this FS_Operations fsOp, IEnumerable<int> sNodes) { return PathsFromNodesExt(fsOp, sNodes, null, false, false, fsOp.LanguageNN, false, null); }
    public static List<IKGD_Path> PathsFromNodesExt(this FS_Operations fsOp, IEnumerable<int> sNodes, IEnumerable<int> rNodes, bool foldersOnly, bool fullAccess) { return PathsFromNodesExt(fsOp, sNodes, rNodes, foldersOnly, fullAccess, fsOp.LanguageNN, false, null); }
    public static List<IKGD_Path> PathsFromNodesExt(this FS_Operations fsOp, IEnumerable<int> sNodes, IEnumerable<int> rNodes, bool foldersOnly, bool fullAccess, bool filterLanguage) { return PathsFromNodesExt(fsOp, sNodes, rNodes, foldersOnly, fullAccess, filterLanguage ? IKGD_Language_Provider.Provider.LanguageNoFilterCode : FS_Operations.LanguageNoFilterCode, false, null); }
    public static List<IKGD_Path> PathsFromNodesExt(this FS_Operations fsOp, IEnumerable<int> sNodes, IEnumerable<int> rNodes, bool foldersOnly, bool fullAccess, bool filterLanguage, bool? forceNoOpt) { return PathsFromNodesExt(fsOp, sNodes, rNodes, foldersOnly, fullAccess, filterLanguage ? IKGD_Language_Provider.Provider.LanguageNoFilterCode : FS_Operations.LanguageNoFilterCode, forceNoOpt, null); }
    public static List<IKGD_Path> PathsFromNodesExt(this FS_Operations fsOp, IEnumerable<int> sNodes, IEnumerable<int> rNodes, bool foldersOnly, bool fullAccess, bool filterLanguage, bool? forceNoOpt, bool? allEquivalentNodes) { return PathsFromNodesExt(fsOp, sNodes, rNodes, foldersOnly, fullAccess, filterLanguage ? IKGD_Language_Provider.Provider.LanguageNoFilterCode : FS_Operations.LanguageNoFilterCode, forceNoOpt, allEquivalentNodes); }
    public static List<IKGD_Path> PathsFromNodesExt(this FS_Operations fsOp, IEnumerable<int> sNodes, IEnumerable<int> rNodes, bool foldersOnly, bool fullAccess, string language, bool? forceNoOpt, bool? allEquivalentNodes)
    {
      //
      // TODO:
      // per testare un po' di situazioni problematiche utilizzare TFVG sulla url: /SearchPacchetti
      // il delay viene accumulato in maniera significativa nell'handler di URL rewrite al check del path e poi via via nei vari model build
      //
      System.Diagnostics.Stopwatch timer = null;
      List<IKGD_Path_Fragment> frags = null;
      try
      {
        if (TraceDebugFlag)
        {
          timer = System.Diagnostics.Stopwatch.StartNew();
          IKCMS_ExecutionProfiler.AddMessage("PathsFromNodesExt: START: snodes:{0} rnodes:{1}".FormatString(Utility.Implode(sNodes ?? Enumerable.Empty<int>(), ","), Utility.Implode(rNodes ?? Enumerable.Empty<int>(), ",")));
        }
        //
        // usiamo l'analogo di PathsUpdateFragsWorkerDB ma senza lo scan forzato sul VFS per evitare accessi inutili al DB
        frags = PathsGetStartFragments(fsOp, null, sNodes, rNodes, foldersOnly, fullAccess, language, fullAccess, forceNoOpt, allEquivalentNodes);
        if (TraceDebugFlag) IKCMS_ExecutionProfiler.AddMessage("PathsFromNodesExt: PathsGetStartFragments COMPLETED: fragments={0}".FormatString((frags != null && frags.Any()) ? frags.Count : 0));
        if (frags != null && frags.Any())
        {
          return fsOp.PathsRefineV2(frags.Select(f => new IKGD_Path(f)).ToList(), foldersOnly, fullAccess, language, true, forceNoOpt, null);
        }
        //else
        //  return new List<IKGD_Path>();
      }
      catch { }
      finally
      {
        if (TraceDebugFlag)
        {
          timer.Stop();
          HttpContext.Current.Items["PathsFromNodesExtDebug"] = (double)(HttpContext.Current.Items["PathsFromNodesExtDebug"] ?? (double)0) + timer.Elapsed.TotalMilliseconds;
          IKCMS_ExecutionProfiler.AddMessage("PathsFromNodesExt: END: fragments={0}".FormatString(frags.Count));
          IKCMS_ExecutionProfiler.AddMessage("PathsFromNodesExt: CUMULATED msec={0}".FormatString(HttpContext.Current.Items["PathsFromNodesExtDebug"]));
        }
      }
      return new List<IKGD_Path>();
    }



    public static List<IKGD_Path> PathsFromNodeAuthor(this FS_Operations fsOp, int sNode) { return fsOp.PathsFromNodesAuthor(new int[] { sNode }, null, true, false, false, true, false, false); }
    public static List<IKGD_Path> PathsFromNodeAuthor(this FS_Operations fsOp, int sNode, bool fullAccess) { return fsOp.PathsFromNodesAuthor(new int[] { sNode }, null, fullAccess, false, false, true, false, false); }
    public static List<IKGD_Path> PathsFromNodeAuthor(this FS_Operations fsOp, int sNode, bool fullAccess, bool filterLanguage, bool includeDeleted) { return fsOp.PathsFromNodesAuthor(new int[] { sNode }, null, fullAccess, filterLanguage, includeDeleted, true, false, false); }
    public static List<IKGD_Path> PathsFromNodesAuthor(this FS_Operations fsOp, IEnumerable<int> sNodes, IEnumerable<int> rNodes) { return fsOp.PathsFromNodesAuthor(sNodes, rNodes, true, false, false, true, false, false); }
    public static List<IKGD_Path> PathsFromNodesAuthor(this FS_Operations fsOp, IEnumerable<int> sNodes, IEnumerable<int> rNodes, bool fullAccess) { return fsOp.PathsFromNodesAuthor(sNodes, rNodes, fullAccess, false, false, true, false, false); }
    public static List<IKGD_Path> PathsFromNodesAuthor(this FS_Operations fsOp, IEnumerable<int> sNodes, IEnumerable<int> rNodes, bool fullAccess, bool filterLanguage, bool includeDeleted) { return fsOp.PathsFromNodesAuthor(sNodes, rNodes, fullAccess, filterLanguage, includeDeleted, true, false, false); }
    public static List<IKGD_Path> PathsFromNodesAuthor(this FS_Operations fsOp, IEnumerable<int> sNodes, IEnumerable<int> rNodes, bool fullAccess, bool filterLanguage, bool includeDeleted, bool filterBySpecifiedNodes) { return fsOp.PathsFromNodesAuthor(sNodes, rNodes, fullAccess, filterLanguage, includeDeleted, filterBySpecifiedNodes, false, false); }
    public static List<IKGD_Path> PathsFromNodesAuthor(this FS_Operations fsOp, IEnumerable<int> sNodes, IEnumerable<int> rNodes, bool fullAccess, bool filterLanguage, bool includeDeleted, bool filterBySpecifiedNodes, bool unconstrained, bool allowunrooted)
    {
      List<FS_Operations.FS_NodeInfo> nodesVFS;
      string language = filterLanguage ? IKGD_Language_Provider.Provider.LanguageAuthorNoFilterCode : FS_Operations.LanguageNoFilterCode;
      List<IKGD_Path_Fragment> frags = PathsUpdateFragsWorkerDB(fsOp, null, sNodes, rNodes, false, fullAccess, language, includeDeleted, out nodesVFS).Where(f => filterBySpecifiedNodes == false || (sNodes != null && sNodes.Any(n => n == f.sNode)) || (rNodes != null && rNodes.Any(n => n == f.rNode))).ToList();
      return fsOp.PathsRefineNoOpt(null, frags.Select(f => new IKGD_Path(f)).ToList(), false, allowunrooted, true, fullAccess, language, includeDeleted, unconstrained);
    }
    public static List<IKGD_Path> PathsFromNodeAuthorUnconstrained(this FS_Operations fsOp, int sNode) { return fsOp.PathsFromNodesAuthor(new int[] { sNode }, null, true, false, false, true, true, true); }
    public static List<IKGD_Path> PathsFromNodeAuthorEquiv(this FS_Operations fsOp, int sNode, bool allowunrooted) { return fsOp.PathsFromNodesAuthor(new int[] { sNode }, null, true, false, false, false, false, allowunrooted); }
    public static List<IKGD_Path> PathsFromNodesAuthorEquiv(this FS_Operations fsOp, IEnumerable<int> sNodes, IEnumerable<int> rNodes, bool allowunrooted) { return fsOp.PathsFromNodesAuthor(sNodes, rNodes, true, false, false, false, false, allowunrooted); }

    //
    // cerca una risorsa dal path o frammento di path
    //
    public static List<IKGD_Path> PathsFromString(this FS_Operations fsOp, string pathString) { return fsOp.PathsFromString(pathString, false, null, true); }
    public static List<IKGD_Path> PathsFromString(this FS_Operations fsOp, string pathString, bool foldersOnly) { return fsOp.PathsFromString(pathString, foldersOnly, null, true); }
    public static List<IKGD_Path> PathsFromString(this FS_Operations fsOp, string pathString, bool foldersOnly, bool? rootedOnly) { return fsOp.PathsFromFragments((pathString ?? string.Empty).TrimEnd('/').Split('/'), foldersOnly, rootedOnly, null, true, true); }
    public static List<IKGD_Path> PathsFromString(this FS_Operations fsOp, string pathString, bool foldersOnly, bool? rootedOnly, bool filterLanguage) { return fsOp.PathsFromFragments((pathString ?? string.Empty).TrimEnd('/').Split('/'), foldersOnly, rootedOnly, filterLanguage ? IKGD_Language_Provider.Provider.LanguageNoFilterCode : FS_Operations.LanguageNoFilterCode, true, true); }
    public static List<IKGD_Path> PathsFromString(this FS_Operations fsOp, string pathString, bool foldersOnly, bool? rootedOnly, string language) { return fsOp.PathsFromFragments((pathString ?? string.Empty).TrimEnd('/').Split('/'), foldersOnly, rootedOnly, language, true, true); }
    public static List<IKGD_Path> PathsFromString(this FS_Operations fsOp, string pathString, bool foldersOnly, bool? rootedOnly, string language, bool filterAreas) { return fsOp.PathsFromFragments((pathString ?? string.Empty).TrimEnd('/').Split('/'), foldersOnly, rootedOnly, language, true, filterAreas); }
    public static List<IKGD_Path> PathsFromFragments(this FS_Operations fsOp, IEnumerable<string> pathFragments, bool foldersOnly, bool? rootedOnly, string language, bool filterByRootsVFS, bool filterAreas)
    {
      List<string> pathFragmentsNR = pathFragments.Where(f => !string.IsNullOrEmpty(f)).ToList();
      List<string> pathFragmentsNRP = pathFragmentsNR.Take(pathFragmentsNR.Count - 1).ToList();
      string lastFrag = pathFragmentsNR.LastOrDefault();
      string firstFrag = pathFragmentsNR.FirstOrDefault();
      rootedOnly = rootedOnly ?? string.IsNullOrEmpty(pathFragments.FirstOrDefault());
      //
      Expression<Func<IKGD_VNODE, bool>> vNodeFilter = fsOp.Get_vNodeFilterACLv2(language);
      Expression<Func<IKGD_VDATA, bool>> vDataFilter = fsOp.Get_vDataFilterACLv2(true, filterAreas);
      if (pathFragmentsNR.Any())
        vNodeFilter = vNodeFilter.And(n => pathFragmentsNR.Contains(n.name));
      Expression<Func<IKGD_VNODE, bool>> vNodeFilterP = PredicateBuilder.True<IKGD_VNODE>();
      vNodeFilterP = vNodeFilterP.And(n => n.flag_folder && (n.parent == 0 || pathFragmentsNRP.Contains(n.name)));
      //if (language != null && language != FS_Operations.LanguageNoFilterCode)
      //{
      //  vNodeFilter = vNodeFilter.And(n => n.language == null || n.language == language);
      //  vDataFilter = vDataFilter.And(n => n.language == null || n.language == language);
      //}
      if (foldersOnly)
        vNodeFilter = vNodeFilter.And(n => n.flag_folder);
      else
      {
        vNodeFilter = vNodeFilter.And(n => (n.flag_folder && pathFragmentsNRP.Contains(n.name)) || n.name == lastFrag);
        // per eseguire letteralmente un LIKE: 
        // vNodeFilter = vNodeFilter.And(n => SqlMethods.Like(n.name, "%test%pattern%"));
        // per utilizzare le espressioni tipo LIKE si puo' creare una subexpression con OR multipli
      }
      //
      // scan del VFS per selezionare i nodi corrispondenti al path fragment fornito
      // questa query mi consente di ottenere anche l'ultimo parent o l'eventuale root
      //
      //var nodesVFSset =
      //  (from vNode in fsOp.NodesActive<IKGD_VNODE>().Where(vNodeFilter)
      //   from vData in fsOp.NodesActive<IKGD_VDATA>().Where(vDataFilter).Where(n => n.rnode == vNode.rnode)
      //   from vNodeP in fsOp.NodesActive<IKGD_VNODE>().Where(n => n.flag_folder).Where(n => ((vNode.flag_folder && vNode.parent == n.folder) || (!vNode.flag_folder && vNode.folder == n.folder)) && (pathFragmentsNRP.Contains(n.name) || vNode.name == firstFrag))
      //   from vDataP in fsOp.NodesActive<IKGD_VDATA>().Where(vDataFilter).Where(n => n.rnode == vNodeP.rnode)
      //   select new { vNode = vNode, vData = vData, vNodeP = vNodeP, vDataP = vDataP }).ToList();
      //List<IKGD_Path_Fragment> scanFrags = nodesVFSset.Select(n => new IKGD_Path_Fragment(new FS_Operations.FS_NodeInfo { vNode = n.vNode, vData = n.vData })).ToList();
      //
      var nodesVFSset =
        (from vNode in fsOp.NodesActive<IKGD_VNODE>().Where(vNodeFilter)
         from vData in fsOp.NodesActive<IKGD_VDATA>().Where(vDataFilter).Where(n => n.rnode == vNode.rnode)
         from vNodeP in fsOp.NodesActive<IKGD_VNODE>().Where(n => n.flag_folder).Where(n => ((vNode.flag_folder && vNode.parent == n.folder) || (!vNode.flag_folder && vNode.folder == n.folder)) && (pathFragmentsNRP.Contains(n.name) || vNode.name == firstFrag))
         from vDataP in fsOp.NodesActive<IKGD_VDATA>().Where(vDataFilter).Where(n => n.rnode == vNodeP.rnode)
         select new
         {
           fsNode = new IKGD_Path_Fragment(
             vNode.parent ?? vNode.folder,
             vNode.snode,
             vNode.rnode,
             vNode.version,
             vData.version,
             vNode.version_frozen,
             vData.version_frozen,
             (vNode.flag_published || vData.flag_published),
             (vNode.flag_current || vData.flag_current),
             vNode.flag_folder,
             vData.flag_unstructured,
             ((vNode.flag_deleted || vData.flag_inactive || vData.flag_deleted) == false),
             vData.flags_menu,
             vNode.name,
             vNode.position,
             vData.area,
             vNode.language,
             vData.manager_type,
             vData.category,
             vData.date_activation,
             vData.date_expiry),
           fsNodeP = new IKGD_Path_Fragment(
             vNodeP.parent ?? vNodeP.folder,
             vNodeP.snode,
             vNodeP.rnode,
             vNodeP.version,
             vDataP.version,
             vNodeP.version_frozen,
             vDataP.version_frozen,
             (vNodeP.flag_published || vDataP.flag_published),
             (vNodeP.flag_current || vDataP.flag_current),
             vNodeP.flag_folder,
             vDataP.flag_unstructured,
             ((vNodeP.flag_deleted || vDataP.flag_inactive || vDataP.flag_deleted) == false),
             vDataP.flags_menu,
             vNodeP.name,
             vNodeP.position,
             vDataP.area,
             vNodeP.language,
             vDataP.manager_type,
             vDataP.category,
             vDataP.date_activation,
             vDataP.date_expiry)
         }).ToList();
      List<IKGD_Path_Fragment> scanFrags = nodesVFSset.Select(n => n.fsNode).ToList();
      //
      // questo sistema non e' in grado di gestire i root nodes
      //
      if (nodesVFSset.Any())
      {
        nodesVFSset.Where(n => !scanFrags.Select(f => f.sNode).Contains(n.fsNodeP.sNode)).ForEach(n => scanFrags.Add(n.fsNodeP));
      }
      else
      {
        scanFrags =
          (from vNode in fsOp.NodesActive<IKGD_VNODE>().Where(vNodeFilterP)
           from vData in fsOp.NodesActive<IKGD_VDATA>().Where(vDataFilter).Where(n => n.rnode == vNode.rnode)
           select new IKGD_Path_Fragment(
              vNode.parent ?? vNode.folder,
              vNode.snode,
              vNode.rnode,
              vNode.version,
              vData.version,
              vNode.version_frozen,
              vData.version_frozen,
              (vNode.flag_published || vData.flag_published),
              (vNode.flag_current || vData.flag_current),
              vNode.flag_folder,
              vData.flag_unstructured,
              ((vNode.flag_deleted || vData.flag_inactive || vData.flag_deleted) == false),
              vData.flags_menu,
              vNode.name,
              vNode.position,
              vData.area,
              vNode.language,
              vData.manager_type,
              vData.category,
              vData.date_activation,
              vData.date_expiry)
              ).ToList();
      }
      List<IKGD_Path_Fragment> fragsSub = scanFrags.Where(n => n.Name.Equals(lastFrag, StringComparison.OrdinalIgnoreCase)).ToList();
      // attenzione che devo forzare l'inclusione delle root (che sono multiple e non sempre corrispondenti a /: es. /Root) ma solo se path == /
      if (!pathFragmentsNR.Any())
        fragsSub = scanFrags.Where(n => n.flag_folder && n.Parent == 0).ToList();
      List<IKGD_Path> paths = fragsSub.Select(n => new IKGD_Path(n.sNode, new List<IKGD_Path_Fragment> { n })).ToList();
      //
      int innerLoopMax = Utility.TryParse<int>(IKGD_Config.AppSettings["IKGD_Path_MaxRecursionLevels"], 25);
      for (int innerLoop = 0; innerLoop < innerLoopMax; innerLoop++)
      {
        var parentingData =
          (from path in paths
           from frag in scanFrags.Where(f => f.flag_folder && f.rNode == path.FirstFragment.Parent)
           select new { path = path, frag = frag }).ToList();
        int reparented = parentingData.Count;
        if (reparented == 0)
          break;
        //
        foreach (var gPath in parentingData.GroupBy(r => r.path))
        {
          // controllo che non ci siano path ricorsivi
          //int recursiveCount = gPath.Where(r => r.path.Fragments.Any(f => f.rNode == r.frag.rNode)).Count();
          var gPathNR = gPath.Where(r => !r.path.Fragments.Any(f => f.rNode == r.frag.rNode)).ToList();
          if (!gPathNR.Any())
          {
            paths.Remove(gPath.Key);
            continue;
          }
          // se ho trovato piu' parents devo duplicare i path saltando il primo che poi tratto normalmente
          gPathNR.Skip(1).ForEach(r => { IKGD_Path p = r.path.Clone() as IKGD_Path; p.InsertFragment(r.frag); paths.Add(p); });
          gPathNR.FirstOrDefault().path.InsertFragment(gPath.FirstOrDefault().frag);
        }
      }
      //
      fsOp.PathsRefineV2(paths, foldersOnly, true, language, true, null, null);
      paths.RemoveAll(p => !p.IsValid);
      if (filterByRootsVFS)
        paths = paths.FilterPathsByRootsVFS().ToList();
      //
      // la procedura trova anche dei path che soddisfano solo parzialmente i pathfragments
      // (trova pezzi che hanno il lastfragment e frammenti del path)
      // basta fare un check che ci siano tutti i frammenti richiesti senza badare all'ordine che viene gestito correttamente dalla procedura
      //
      paths.RemoveAll(p => !pathFragmentsNR.All(n => p.Fragments.Any(f => string.Equals(f.Name, n, StringComparison.OrdinalIgnoreCase))));
      //
      paths.Sort();
      paths = paths.FilterPathsByLanguageSingleOrNull().ToList();
      return paths;
    }


    public static IKGD_Path PathEnsure(this FS_Operations fsOp, string pathString) { bool newPathCreated = false; return PathEnsure(fsOp, pathString, null, out newPathCreated); }
    public static IKGD_Path PathEnsure(this FS_Operations fsOp, string pathString, bool? createAsPublished, out bool newPathCreated)
    {
      newPathCreated = false;
      if (string.IsNullOrEmpty(pathString) || !pathString.StartsWith("/"))
        throw new ArgumentException("Path Invalid or null");
      //
      List<IKGD_Path_Fragment> frags = new List<IKGD_Path_Fragment>();
      foreach (string pathFrag in Utility.Explode(pathString, "/", " ", true))
      {
        int parentCurrent = (frags.Any()) ? frags.LastOrDefault().rNode : 0;
        FS_Operations.FS_NodeInfo fsNode =
          (from vNode in fsOp.NodesActive<IKGD_VNODE>().Where(n => n.flag_folder && n.parent == parentCurrent && n.name == pathFrag)
           from vData in fsOp.NodesActive<IKGD_VDATA>().Where(n => n.rnode == vNode.rnode)
           select new FS_Operations.FS_NodeInfo { vNode = vNode, vData = vData }).FirstOrDefault();
        //
        // se manca il folder provvedo a crearlo
        //
        if (fsNode == null)
        {
          var vNode = fsOp.COW_NewFolder(parentCurrent, pathFrag, null, false, createAsPublished.GetValueOrDefault(false), FS_Operations.CreateResourcePosition.Last);
          fsNode = fsOp.Get_NodeInfo(vNode.snode, false);
          newPathCreated = true;
        }
        if (fsNode == null)
          throw new ArgumentException("Path Invalid Operation");
        frags.Add(new IKGD_Path_Fragment(fsNode));
      }
      //
      IKGD_Path newPath = new IKGD_Path(frags.LastOrDefault().sNode, frags);
      if (!newPath.IsRooted)
      {
        newPath = fsOp.PathsRefineV2(new List<IKGD_Path> { newPath }, false, true, null, true, null, null).FirstOrDefault();
      }
      //
      return newPath;
    }



    //
    // Paths filtering extensions
    //

    public static IEnumerable<IKGD_Path> FilterPathsByRootsAuthor(this IEnumerable<IKGD_Path> paths)
    {
      try { return IKGD_ConfigVFS.Config.RootsAuthor_sNodes.Any() ? paths.Where(p => p.Fragments.Any(f => IKGD_ConfigVFS.Config.RootsAuthor_sNodes.Contains(f.sNode))) : paths; }
      catch (Exception ex)
      {
        Elmah.ErrorSignal.FromCurrentContext().Raise(ex);
      }
      return paths;
    }


    public static IEnumerable<IKGD_Path> FilterPathsByRootsVFS(this IEnumerable<IKGD_Path> paths)
    {
      try { return IKGD_ConfigVFS.Config.RootsVFS_sNodes.Any() ? paths.Where(p => p.Fragments.Any(f => IKGD_ConfigVFS.Config.RootsVFS_sNodes.Contains(f.sNode))) : paths; }
      catch (Exception ex)
      {
        Elmah.ErrorSignal.FromCurrentContext().Raise(ex);
      }
      return paths;
    }


    public static IEnumerable<IKGD_Path> FilterPathsByRootsCMS(this IEnumerable<IKGD_Path> paths)
    {
      try { return IKGD_ConfigVFS.Config.RootsCMS_sNodes.Any() ? paths.Where(p => p.Fragments.Any(f => IKGD_ConfigVFS.Config.RootsCMS_sNodes.Contains(f.sNode))) : paths; }
      catch (Exception ex)
      {
        Elmah.ErrorSignal.FromCurrentContext().Raise(ex);
      }
      return paths;
    }


    public static IEnumerable<IKGD_Path> FilterPathsByRootsCMS(this IEnumerable<IKGD_Path> paths, bool filterActive, bool filterLanguage)
    {
      if (filterLanguage)
      {
        string lang = IKGD_Language_Provider.Provider.LanguageNN;
        paths = paths.Where(p => p.Fragments.All(f => f.Language == null || f.Language == lang));
      }
      if (filterActive)
      {
        paths = paths.Where(p => p.Fragments.All(f => f.flag_active));
      }
      try { return IKGD_ConfigVFS.Config.RootsCMS_sNodes.Any() ? paths.Where(p => p.Fragments.Any(f => IKGD_ConfigVFS.Config.RootsCMS_sNodes.Contains(f.sNode))) : paths; }
      catch (Exception ex)
      {
        Elmah.ErrorSignal.FromCurrentContext().Raise(ex);
      }
      return paths;
    }


    public static IEnumerable<IKGD_Path> FilterPathsByRootsCMSorderedByACL(this IEnumerable<IKGD_Path> paths, bool filterActive, bool filterLanguage)
    {
      var filteredPaths = FilterPathsByRootsCMS(paths, filterActive, filterLanguage);
      if (!FS_OperationsHelpers.IsRoot)
      {
        //var areas = FS_OperationsHelpers.CachedAreas;
        //filteredPaths = filteredPaths.OrderBy(p => !p.Fragments.All(f => string.IsNullOrEmpty(f.Area) || areas.Contains(f.Area)));
        var areas = FS_OperationsHelpers.CachedAreasExtended;
        if (areas.AreaMatchMode == FS_Areas_Extended.AreaMatchModeEnum.FilterByAllowed)
          filteredPaths = filteredPaths.OrderBy(p => !p.Fragments.All(f => areas.AreasAllowed.Contains(f.Area)));
        else if (areas.AreaMatchMode == FS_Areas_Extended.AreaMatchModeEnum.FilterByDenied)
          filteredPaths = filteredPaths.OrderBy(p => p.Fragments.Any(f => areas.AreasDenied.Contains(f.Area)));
      }
      return filteredPaths;
    }


    public static IEnumerable<IKGD_Path> FilterPathsByLanguageSingleOrNull(this IEnumerable<IKGD_Path> paths) { return paths.Where(FilterByLanguageSingleOrNull); }


    public static IEnumerable<IKGD_Path> FilterPathsByExpiry(this IEnumerable<IKGD_Path> paths) { return paths.Where(FilterByExpiry); }


    public static IEnumerable<IKGD_Path> FilterPathsByLanguage(this IEnumerable<IKGD_Path> paths) { return FilterPathsByLanguage(paths, IKGD_Language_Provider.Provider.LanguageNN); }
    public static IEnumerable<IKGD_Path> FilterPathsByLanguage(this IEnumerable<IKGD_Path> paths, string language)
    {
      if (language != null && language != FS_Operations.LanguageNoFilterCode)
        return paths.Where(p => p.Fragments.All(f => f.Language == null || f.Language == language));
      else
        return paths;
    }


    public static IEnumerable<IKGD_Path> FilterPathsByACL(this IEnumerable<IKGD_Path> paths)
    {
      //var areas = FS_OperationsHelpers.CachedAreas;
      //return paths.Where(p => p.Fragments.All(f => string.IsNullOrEmpty(f.Area) || areas.Contains(f.Area)));
      var areas = FS_OperationsHelpers.CachedAreasExtended;
      if (areas.AreaMatchMode == FS_Areas_Extended.AreaMatchModeEnum.FilterByAllowed)
        return paths.Where(p => p.Fragments.All(f => areas.AreasAllowed.Contains(f.Area)));
      else if (areas.AreaMatchMode == FS_Areas_Extended.AreaMatchModeEnum.FilterByDenied)
        return paths.Where(p => p.Fragments.All(f => !areas.AreasDenied.Contains(f.Area)));
      else
        return paths;
    }


    public static IEnumerable<IKGD_Path> FilterPathsByACL(this IEnumerable<IKGD_Path> paths, string language, bool filterAreas)
    {
      if (filterAreas)
      {
        //var areas = FS_OperationsHelpers.CachedAreas;
        //paths = paths.Where(p => p.Fragments.All(f => string.IsNullOrEmpty(f.Area) || areas.Contains(f.Area)));
        var areas = FS_OperationsHelpers.CachedAreasExtended;
        if (areas.AreaMatchMode == FS_Areas_Extended.AreaMatchModeEnum.FilterByAllowed)
          paths = paths.Where(p => p.Fragments.All(f => areas.AreasAllowed.Contains(f.Area)));
        else if (areas.AreaMatchMode == FS_Areas_Extended.AreaMatchModeEnum.FilterByDenied)
          paths = paths.Where(p => p.Fragments.All(f => !areas.AreasDenied.Contains(f.Area)));
      }
      //
      if (language != null)
        paths = paths.Where(p => !p.Fragments.Any(f => !string.IsNullOrEmpty(f.Language) && f.Language != language));
      //
      return paths;
    }







    public static IEnumerable<IKGD_Path> FilterCustom(this IEnumerable<IKGD_Path> paths, params Func<IKGD_Path, bool>[] filters)
    {
      try { filters.Where(f => f != null).ForEach(f => paths = paths.Where(f)); }
      catch (Exception ex)
      {
        Elmah.ErrorSignal.FromCurrentContext().Raise(ex);
      }
      return paths;
    }


    // applica i filtri in successione, nel caso che la lista si svuoti nel mezzo del loop viene ritornato l'ultimo set con almeno un path valido
    public static IEnumerable<IKGD_Path> FilterFallback(this IEnumerable<IKGD_Path> paths, params Func<IKGD_Path, bool>[] filters)
    {
      IEnumerable<IKGD_Path> pathsFilled = paths;
      foreach (Func<IKGD_Path, bool> filter in filters.Where(f => f != null))
      {
        paths = pathsFilled.Where(filter);
        if (paths.Any())
          pathsFilled = paths;
        else
          break;
      }
      return pathsFilled;
    }


    public static IEnumerable<IKGD_Path> OrderByACL(this IEnumerable<IKGD_Path> paths)
    {
      try
      {
        if (!FS_OperationsHelpers.IsRoot)
        {
          //var areas = FS_OperationsHelpers.CachedAreas;
          //return paths.OrderBy(p => !p.Fragments.All(f => string.IsNullOrEmpty(f.Area) || areas.Contains(f.Area)));
          var areas = FS_OperationsHelpers.CachedAreasExtended;
          if (areas.AreaMatchMode == FS_Areas_Extended.AreaMatchModeEnum.FilterByAllowed)
            return paths.OrderBy(p => !p.Fragments.All(f => areas.AreasAllowed.Contains(f.Area)));
          else if (areas.AreaMatchMode == FS_Areas_Extended.AreaMatchModeEnum.FilterByDenied)
            return paths.OrderBy(p => p.Fragments.Any(f => areas.AreasDenied.Contains(f.Area)));
          else
            return paths.DefaultIfEmpty();
        }
      }
      catch (Exception ex)
      {
        Elmah.ErrorSignal.FromCurrentContext().Raise(ex);
      }
      return paths.DefaultIfEmpty();
    }
    public static IOrderedEnumerable<IKGD_Path> OrderByACL_O(this IEnumerable<IKGD_Path> paths)
    {
      try
      {
        if (!FS_OperationsHelpers.IsRoot)
        {
          //var areas = FS_OperationsHelpers.CachedAreas;
          //return paths.OrderBy(p => !p.Fragments.All(f => string.IsNullOrEmpty(f.Area) || areas.Contains(f.Area)));
          var areas = FS_OperationsHelpers.CachedAreasExtended;
          if (areas.AreaMatchMode == FS_Areas_Extended.AreaMatchModeEnum.FilterByAllowed)
            return paths.OrderBy(p => !p.Fragments.All(f => areas.AreasAllowed.Contains(f.Area)));
          else if (areas.AreaMatchMode == FS_Areas_Extended.AreaMatchModeEnum.FilterByDenied)
            return paths.OrderBy(p => p.Fragments.Any(f => areas.AreasDenied.Contains(f.Area)));
          else
            return Enumerable.OrderBy(paths, p => 0);
        }
      }
      catch (Exception ex)
      {
        Elmah.ErrorSignal.FromCurrentContext().Raise(ex);
      }
      return Enumerable.OrderBy(paths.DefaultIfEmpty(), p => 0);
    }


    public static Func<IKGD_Path, bool> FilterNull { get { return p => true; } }
    public static Func<IKGD_Path, bool> FilterByLanguageSingleOrNull { get { return p => p.Fragments.Select(f => f.Language).Where(f => !string.IsNullOrEmpty(f)).Distinct().Count() < 2; } }
    public static Func<IKGD_Path, bool> FilterByLanguage { get { return FilterByLanguageString(IKGD_Language_Provider.Provider.LanguageNN); } }
    public static Func<IKGD_Path, bool> FilterByLanguageString(string language) { if (language != null && language != FS_Operations.LanguageNoFilterCode) { return p => p.Fragments.All(f => f.Language == null || string.Equals(f.Language, language, StringComparison.OrdinalIgnoreCase)); } else { return p => true; } }
    public static Func<IKGD_Path, bool> FilterByActive { get { return p => p.Fragments.All(f => f.flag_active); } }
    public static Func<IKGD_Path, bool> FilterByExpiry
    {
      get
      {
        return p =>
          {
            DateTime dateCMS = Ikon.GD.FS_OperationsHelpers.DateTimeSession;
            return p.Fragments.All(f => (f.DateActivation == null || f.DateActivation <= dateCMS) && (f.DateExpiry == null || dateCMS <= f.DateExpiry));
          };
      }
    }
    public static Func<IKGD_Path, bool> FilterByRootAuthor { get { return p => IKGD_ConfigVFS.Config.RootsAuthor_sNodes.Any() ? p.Fragments.Any(f => IKGD_ConfigVFS.Config.RootsAuthor_sNodes.Contains(f.sNode)) : true; } }
    public static Func<IKGD_Path, bool> FilterByRootVFS { get { return p => IKGD_ConfigVFS.Config.RootsVFS_sNodes.Any() ? p.Fragments.Any(f => IKGD_ConfigVFS.Config.RootsVFS_sNodes.Contains(f.sNode)) : true; } }
    public static Func<IKGD_Path, bool> FilterByRootCMS { get { return p => IKGD_ConfigVFS.Config.RootsCMS_sNodes.Any() ? p.Fragments.Any(f => IKGD_ConfigVFS.Config.RootsCMS_sNodes.Contains(f.sNode)) : true; } }
    public static Func<IKGD_Path, bool> FilterByAreas
    {
      get
      {
        //return p => p.Fragments.All(f => string.IsNullOrEmpty(f.Area) || FS_OperationsHelpers.CachedAreas.Contains(f.Area));
        var areas = FS_OperationsHelpers.CachedAreasExtended;
        if (areas.AreaMatchMode == FS_Areas_Extended.AreaMatchModeEnum.FilterByAllowed)
          return p => p.Fragments.All(f => areas.AreasAllowed.Contains(f.Area));
        else if (areas.AreaMatchMode == FS_Areas_Extended.AreaMatchModeEnum.FilterByDenied)
          return p => p.Fragments.All(f => !areas.AreasDenied.Contains(f.Area));
        return p => true;
      }
    }



    //private static void LazyFsOp(FS_Operations fsOp)
    //{
    //  bool fsOp_dispose = (fsOp == null);
    //  try
    //  {
    //    fsOp = new FS_Operations();
    //    //
    //    // ...
    //    //
    //  }
    //  catch { }
    //  finally
    //  {
    //    if (fsOp_dispose && fsOp != null)
    //    {
    //      fsOp.Dispose();
    //      fsOp = null;
    //    }
    //  }
    //}


    private class ParentFragPair
    {
      public IKGD_Path path { get; set; }
      public IKGD_Path_Fragment frag { get; set; }
    }

  }



  //
  // classe per lo storage dei path con supporto per il sorting
  //
  [Serializable]
  public class IKGD_Path : IComparable<IKGD_Path>, ICloneable
  {
    public int sNode { get; protected set; }
    public int rNode { get { return LastFragment.rNode; } }
    public List<IKGD_Path_Fragment> Fragments { get; protected set; }
    public IKGD_Path_Fragment LastFragment { get { return Fragments.LastOrDefault(); } }
    public IKGD_Path_Fragment PreLastFragment { get { return Fragments[Math.Max(Fragments.Count - 2, 0)]; } }
    public IKGD_Path_Fragment FirstFragment { get { return Fragments.FirstOrDefault(); } }
    public IKGD_Path_Fragment FolderFragment { get { return Fragments.LastOrDefault(f => f.flag_folder); } }
    public bool IsRooted { get { try { return (FirstFragment.Parent == 0); } catch { return false; } } }
    public bool IsValid { get { return (Fragments != null && Fragments.Any()); } }
    public bool IsFolder { get { try { return Fragments.LastOrDefault().flag_folder; } catch { return false; } } }
    public bool IsFile { get { return !IsFolder; } }


    private IKGD_Path() { }  // costruttore di default nascosto
    public IKGD_Path(int sNode) : this(sNode, new List<IKGD_Path_Fragment>()) { }
    public IKGD_Path(int sNode, List<IKGD_Path_Fragment> Fragments)
    {
      this.sNode = sNode;
      this.Fragments = Fragments;
    }
    public IKGD_Path(IKGD_Path_Fragment fragment)
    {
      this.sNode = fragment.sNode;
      this.Fragments = new List<IKGD_Path_Fragment> { fragment };
    }


    public void InsertFragment(IKGD_Path_Fragment fragment)
    {
      Fragments.Insert(0, fragment);
      fragment.RegisterHit();
    }


    public object Clone()
    {
      //IKGD_Path clone = (IKGD_Path)this.MemberwiseClone();
      //clone.Fragments = new List<IKGD_Path_Fragment>(Fragments);
      //return clone;
      return new IKGD_Path(sNode, new List<IKGD_Path_Fragment>(Fragments));
    }


    public int CompareTo(IKGD_Path other)
    {
      //
      // valutare se trattare nel path anche i flags_menu: FlagsMenuEnum.BreakRecurse | FlagsMenuEnum.HiddenNode
      // per eliminare la priorita' dei path con eventuali limitazioni
      //
      if (this.IsRooted && other.IsRooted)
      {
        int n = Math.Min(this.Fragments.Count, other.Fragments.Count);
        for (int i = 0; i < n; i++)
        {
          if (this.Fragments[i].Position != other.Fragments[i].Position)
            return this.Fragments[i].Position.CompareTo(other.Fragments[i].Position);
          if (this.Fragments[i].Name != other.Fragments[i].Name)
            return this.Fragments[i].Name.CompareTo(other.Fragments[i].Name);
          if (this.Fragments[i].sNode != other.Fragments[i].sNode)
            return this.Fragments[i].sNode.CompareTo(other.Fragments[i].sNode);
        }
        return this.Fragments.Count.CompareTo(other.Fragments.Count);
      }
      else if (!this.IsRooted && !other.IsRooted)
      {
        int n = Math.Min(this.Fragments.Count, other.Fragments.Count);
        for (int i = 0; i < n; i++)
          if (this.Fragments[i].sNode != other.Fragments[i].sNode)
            return this.Fragments[i].sNode.CompareTo(other.Fragments[i].sNode);
        return this.Fragments.Count.CompareTo(other.Fragments.Count);
      }
      else if (this.IsRooted)
        return -1;
      else if (other.IsRooted)
        return +1;
      return 0;
    }


    public string Path
    {
      get
      {
        if (Fragments != null)
          return (IsRooted ? "/" : string.Empty) + string.Join("/", Fragments.Select(f => f.Name).ToArray());
        return string.Empty;
      }
    }

    public string DirectoryName
    {
      get
      {
        if (Fragments != null)
          return (IsRooted ? "/" : string.Empty) + string.Join("/", Fragments.Where(f => f.flag_folder).Select(f => f.Name).ToArray());
        return string.Empty;
      }
    }

    public string FileName
    {
      get
      {
        if (Fragments != null && !LastFragment.flag_folder)
          return LastFragment.Name;
        return string.Empty;
      }
    }


    public bool IsNotExpired() { return IKGD_Path_Helper.FilterByExpiry(this); }


    public bool IsLanguageAccessible() { return Fragments.All(f => f.Language == null || string.Equals(f.Language, IKGD_Language_Provider.Provider.LanguageNN, StringComparison.OrdinalIgnoreCase)); }
    public bool IsLanguageAccessible(string language) { return Fragments.All(f => f.Language == null || string.Equals(f.Language, language, StringComparison.OrdinalIgnoreCase)); }

    public string FirstLanguage { get { return Fragments.Select(f => f.Language).FirstOrDefault(f => !string.IsNullOrEmpty(f)); } }
    public string FirstLanguageNN { get { return Fragments.Select(f => f.Language).FirstOrDefault(f => !string.IsNullOrEmpty(f)) ?? IKGD_Language_Provider.Provider.LanguageNN; } }


    private static Regex Frag_RegEx01 = new Regex(@"\\/", RegexOptions.Compiled);
    private static Regex Frag_RegEx02 = new Regex(@"\s{2,}", RegexOptions.Compiled);  // condensazione dei - e / multipli
    public static string NormalizeFrag(string frag)
    {
      if (frag.IsNotNullOrWhiteSpace())
      {
        frag = Frag_RegEx02.Replace(Frag_RegEx01.Replace(frag, " "), " ").Trim(' ');
      }
      return frag.DefaultIfEmptyTrim("NULL");
    }


    public override string ToString() { return Path; }

  }



  //
  // classe per lo storage delle informazioni relative ai nodi non risolvibili (per evitare lookup continui)
  //
  public class IKGD_Path_Fragment_Missing
  {
    public int VersionVFS { get; set; }
    public int sNode { get; set; }
    public int rNode { get; set; }
  }


  //
  // classe per lo storage delle informazioni relative al path fragment (folder)
  //
  [Serializable]
  public class IKGD_Path_Fragment
  {
    public int Parent { get; set; }
    public int sNode { get; set; }
    public int rNode { get; set; }
    public int vNodeVersion { get; set; }
    public int vDataVersion { get; set; }
    public int? vNodeVersionFrozen { get; set; }
    public int? vDataVersionFrozen { get; set; }
    public bool flag_published { get; set; }  // vNode.flag_published || vData.flag_published
    public bool flag_current { get; set; }  // vNode.flag_current || vData.flag_current
    public bool flag_folder { get; set; }
    public bool flag_unstructured { get; set; }
    public bool flag_active { get; set; }
    public int flags_menu { get; set; }
    public string Name { get; set; }
    public double Position { get; set; }
    public string Area { get; set; }
    public string Language { get; set; }  // fsNode.Language
    public string ManagerType { get; set; }
    public string Category { get; set; }
    public FlagsMenuEnum FlagsMenu { get { return (FlagsMenuEnum)flags_menu; } set { flags_menu = (int)value; } }
    public DateTime? DateActivation { get; set; }
    public DateTime? DateExpiry { get; set; }
    //
    // for the garbage collector management
    //
    public int HitsCount { get; protected set; }
    public DateTime Created { get; protected set; }
    public DateTime LastAccess { get; protected set; }


    public IKGD_Path_Fragment()
    {
      LastAccess = Created = DateTime.Now;
      HitsCount = 0;
    }

    public IKGD_Path_Fragment(
      int Parent,
      int sNode,
      int rNode,
      int vNodeVersion,
      int vDataVersion,
      int? vNodeVersionFrozen,
      int? vDataVersionFrozen,
      bool flag_published,  // vNode.flag_published || vData.flag_published
      bool flag_current,  // vNode.flag_current || vData.flag_current
      bool flag_folder,
      bool flag_unstructured,
      bool flag_active,
      int flags_menu,
      string Name,
      double Position,
      string Area,
      string Language,  // fsNode.Language
      string ManagerType,
      string Category,
      DateTime? DateActivation,
      DateTime? DateExpiry
      )
      : this()
    {
      this.Parent = Parent;
      this.sNode = sNode;
      this.rNode = rNode;
      this.vNodeVersion = vNodeVersion;
      this.vDataVersion = vDataVersion;
      this.vNodeVersionFrozen = vNodeVersionFrozen;
      this.vDataVersionFrozen = vDataVersionFrozen;
      this.flag_published = flag_published;
      this.flag_current = flag_current;
      this.flag_folder = flag_folder;
      this.flag_unstructured = flag_unstructured;
      this.flag_active = flag_active;
      this.flags_menu = flags_menu;
      this.Name = Name;
      this.Position = Position;
      this.Area = Area;
      this.Language = Language;
      this.ManagerType = ManagerType;
      this.Category = Category;
      this.DateActivation = DateActivation;
      this.DateExpiry = DateExpiry;
      this.Parent = Parent;
      this.Parent = Parent;
      this.Parent = Parent;
    }

    public IKGD_Path_Fragment(FS_Operations.FS_NodeInfo fsNode)
      : this(fsNode.vNode.parent ?? fsNode.vNode.folder,
        fsNode.vNode.snode,
        fsNode.vNode.rnode,
        fsNode.vNode.version,
        fsNode.vData.version,
        fsNode.vNode.version_frozen,
        fsNode.vData.version_frozen,
        (fsNode.vNode.flag_published || fsNode.vData.flag_published),
        (fsNode.vNode.flag_current || fsNode.vData.flag_current),
        fsNode.vNode.flag_folder,
        fsNode.vData.flag_unstructured,
        fsNode.IsActive,
        fsNode.vData.flags_menu,
        fsNode.vNode.name,
        fsNode.vNode.position,
        fsNode.vData.area,
        fsNode.Language, // per i folder/pagine la lingua viene definita nel vNode (come link simbolico) e non nel vData
        fsNode.vData.manager_type,
        fsNode.vData.category,
        fsNode.vData.date_activation,
        fsNode.vData.date_expiry)
    { }


    public void RegisterHit()
    {
      HitsCount++;
      LastAccess = DateTime.Now;
    }


    //
    // TODO: sviluppare una metrica da utilizzare per il garbage collector
    //
    public double GC_Metric()
    {
      try
      {
        return HitsCount / (LastAccess - Created).TotalSeconds;
      }
      catch { return 0; }
    }


    public override string ToString()
    {
      return string.Format("[{0}/{1}]{{{2}/{3}}}({4}):{5}", sNode, rNode, vNodeVersion, vDataVersion, flag_folder, Name);
    }
  }



}
