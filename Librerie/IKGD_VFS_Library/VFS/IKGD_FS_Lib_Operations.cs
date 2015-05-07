/*
 * 
 * Intranet Ikon
 * 
 * Copyright (C) 2009 Ikon Srl
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
using System.Data.Linq.SqlClient;
using System.Diagnostics;
using System.Linq.Dynamic;
using System.Transactions;
using System.Web.Caching;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;  // assembly System.ServiceModel.Web
using LinqKit;

using Ikon;
using Ikon.GD;
using Ikon.Log;
using Ikon.IKGD.Library;
using Ikon.IKCMS;
using Newtonsoft.Json;


/// <summary>
/// Summary description for IkonGD_dataBase
/// </summary>

namespace Ikon.GD
{


  public class FS_Operations : IDisposable
  {
    public static readonly int rNodeCodeRoot = 0;
    public static readonly int sNodeCodeRoot = 0;
    public static readonly int sNodeCodeFirstFolder = 1;
    public static readonly int MaxNameLen = 250;
    public static readonly int MaxKeyLen = 250;
    public static readonly int MaxFileNameLen = 260;
    public static readonly int MaxINodeMimeLen = 100;


    //
    // IKGD_VNODE.version_frozen = null per preview non in attesa di pubblicazione, oppure > 0 per risorse pubblicate o in attesa di pubblicazione
    // VersionFrozen == -1 --> risorse 'current' (usa il flag flag_preview)
    // VersionFrozen == 0  --> risorse 'published' (usa il flag flag_published)
    // VersionFrozen > 0   --> risorse 'snapshot publication preview' (usa il nodo con version max e frozen <= VersionFrozen, e' MOLTO pesante)
    //
    public int VersionFrozen { get; set; }
    public IKGD_DataContext DB;
    //
    // selettori per la lettura di attributi ausiliari per i nodi
    //
    [Flags]
    public enum AttributesSelector { Settings = 1 << 0, iNode = 1 << 1, ACLs = 1 << 2, Properties = 1 << 3, Relations = 1 << 4 };
    public const AttributesSelector AttributesSelectorDefault = AttributesSelector.Settings | AttributesSelector.ACLs;
    //
    public enum CreateResourcePosition { First, Last };
    public const string UploadFolderName = "__Upload__";
    //
    private Random rndGen = new Random();
    //
    public DateTime DateTimeContext { get; set; }
    //
    [Flags]
    public enum FilterVFS
    {
      None = 0,
      Disabled = 1 << 0,
      ACL = 1 << 1,
      Dates = 1 << 2,
      Language = 1 << 3,
      Deleted = 1 << 4,
      // negation flags
      NoACL = 1 << 5,
      NoDates = 1 << 6,
      NoLanguage = 1 << 7,
      // others
      Unstructured = 1 << 8,
      Folders = 1 << 9
    };
    //public const FilterVFS FiltersVFS_Default = FilterVFS.ACL | FilterVFS.Language | FilterVFS.Dates;
    //public const FilterVFS FiltersVFS_DefaultNoACL = FilterVFS.Language | FilterVFS.Dates;
    public const FilterVFS FiltersVFS_Default = FilterVFS.ACL | FilterVFS.Language;
    public const FilterVFS FiltersVFS_DefaultNoACL = FilterVFS.Language;
    public const FilterVFS FiltersVFS_DefaultNoLanguage = FilterVFS.ACL;
    //


    public FS_Operations()
      : this(null, false, false)
    { }

    public FS_Operations(bool disableObjectTracking)
      : this(null, disableObjectTracking, false)
    { }

    public FS_Operations(int? VersionSelector)
      : this(VersionSelector, false, false)
    { }

    public FS_Operations(int? VersionSelector, bool disableObjectTracking, bool forceRoot)
    {
      ConstructorWorker(null, VersionSelector, disableObjectTracking, forceRoot, false);
    }

    public FS_Operations(int? VersionSelector, bool disableObjectTracking, bool forceRoot, bool forceNewConnection)
    {
      ConstructorWorker(null, VersionSelector, disableObjectTracking, forceRoot, forceNewConnection);
    }

    // equivalente di un costruttore di copia, se si utilizza un null allora utilizza il costruttore di default;
    // ma con creazione di una nuova connessione
    public FS_Operations(FS_Operations fsOp)
    {
      if (fsOp == null)
      {
        ConstructorWorker(null, null, false, false, false);
      }
      else
      {
        disposed = false;  // per evitare il Dispose del DB alla distruzione dell'oggetto lasciandola all'inizializzatore esterno
        DB = fsOp.DB;
        VersionFrozen = fsOp.VersionFrozen;
        CurrentUser = fsOp.CurrentUser;
        CurrentAreasExtended = fsOp.CurrentAreasExtended;
        DateTimeContext = fsOp.DateTimeContext;
      }
    }

    // equivalente di un costruttore di copia, se si utilizza un null allora utilizza il costruttore di default;
    public FS_Operations(IKGD_DataContext _DB, int? VersionSelector, bool disableObjectTracking, bool forceRoot, bool forceNewConnection)
    {
      ConstructorWorker(_DB, VersionSelector, disableObjectTracking, forceRoot, forceNewConnection);
    }


    public void ConstructorWorker(IKGD_DataContext _DB, int? VersionSelector, bool disableObjectTracking, bool forceRoot, bool forceNewConnection)
    {
      DB = (_DB != null) ? IKGD_DBH.GetDB(_DB) : IKGD_DBH.GetDB(forceNewConnection, disableObjectTracking);
      if (disableObjectTracking)
        DB.ObjectTrackingEnabled = false;
      //
      // se la connessione non e' ancora stata aperta e siamo all'interno di una Transaction / TransactionScope
      // e' necessario aprire la connessione subito, prima di altre operazioni o delayed read di LINQ che altrimenti
      // invalidano lo stato della transazione in maniera semirandom
      // attenzione anche all'uso delle funzioni path nelle gtransaction che possono fare uso di settings di configurazione
      // con accessi "nascosti" al database, eventualmente precedere il setup della transaction con il setupp di
      // Ikon.GD.IKGD_ConfigVFS.Config
      //
      //if (System.Transactions.Transaction.Current != null)
      //{
      //  EnsureOpenConnection();
      //}
      //
      VersionSelector = VersionSelector ?? FS_OperationsHelpers.VersionFrozenSession;
      VersionFrozen = VersionSelector.Value;
      //
      CurrentUser = Ikon.GD.MembershipHelper.UserName ?? "unlogged";
      CurrentAreasExtended = FS_OperationsHelpers.GetAreasExtended(CurrentUser, forceRoot, DB);
      if (IsRoot || forceRoot)
        CurrentUser = "root";
      //
      // for debugging only
      //
      //VersionFrozen = -1;
      //
      DateTimeContext = DateTime.Now;
    }


    //
    // IDisposable interface implementation: START
    //
    private bool disposed;
    ~FS_Operations()
    {
      this.Dispose(false);
    }

    public void Dispose()
    {
      if (!this.disposed)
      {
        this.Dispose(true);
        this.disposed = true;
        GC.SuppressFinalize(this);
      }
    }

    protected virtual void Dispose(bool disposing)
    {
      if (disposing)
      {
        // clean up managed resources
        DB.Dispose();
      }
      // clean up unmanaged resources
    }
    //
    // IDisposable interface implementation: END
    //


    public FS_Areas_Extended CurrentAreasExtended { get; protected set; }
    public string CurrentUser { get; protected set; }
    public bool IsRoot { get { return ((CurrentUser == "root") || FS_OperationsHelpers.IsRoot); } }


    //
    // se la connessione non e' ancora stata aperta e siamo all'interno di una Transaction / TransactionScope
    // e' necessario aprire la connessione subito, prima di altre operazioni o delayed read di LINQ che altrimenti
    // invalidano lo stato della transazione in maniera semirandom
    //
    public void EnsureOpenConnection()
    {
      EnsureOpenConnection(DB);
    }


    public static void EnsureOpenConnection(DataContext context)
    {
      if (context != null)
      {
        switch (context.Connection.State)
        {
          case ConnectionState.Closed:
          case ConnectionState.Broken:
            context.Connection.Open();
            break;
        }
      }
    }


    //
    public static readonly string LanguageNoFilterCode = "*";
    //
    private string _Language = null;
    public string Language
    {
      get { return _Language ?? IKGD_Language_Provider.Provider.Language; }
      set { _Language = value; }
    }
    public string LanguageNN
    {
      get { return _Language ?? IKGD_Language_Provider.Provider.LanguageNN; }
      set { _Language = value; }
    }


    public IQueryable<IKGD_VNODE> FilterVNodesByVersion(IQueryable<IKGD_VNODE> vQuery) { return FilterVNodesByVersion(vQuery, false); }
    public IQueryable<IKGD_VNODE> FilterVNodesByVersion(IQueryable<IKGD_VNODE> vQuery, bool includeDeleted)
    {
      IQueryable<IKGD_VNODE> vQueryFiltered;
      if (VersionFrozen == 0)
        vQueryFiltered = vQuery.Where(vn => vn.flag_published);
      else if (VersionFrozen == -1)
      {
        if (includeDeleted)
          vQueryFiltered = vQuery.Where(vn => vn.flag_current);
        else
          vQueryFiltered = vQuery.Where(vn => vn.flag_current && !vn.flag_deleted);
      }
      else
      {
        // perche' questa query piu' semplice funzioni quando si crea la preview di pubblicazione e' necessario
        // duplicare e/o freezare tutto il subtree dal nodo di patrenza della pubblicazione
        // inoltre deve essere VersionFrozen > DB.IKGD_SNAPSHOTs.Max(r=>r.id)
        // inoltre deve essere VersionFrozen > DB.IKGD_VERSIONs.Max(r=>r.id)
        vQueryFiltered = vQuery.Where(vn => (vn.flag_published && vn.version_frozen <= VersionFrozen) || (!vn.flag_published && vn.version_frozen == VersionFrozen));
      }
      return vQueryFiltered;
    }
    //
    // filtraggio dei dati a livello di datacontext con DataLoadOptions per la gestione del versioning
    //
    //public void LoadWithFilterSetup(DataLoadOptions ldOpts, bool getINODE, bool getVDATA, bool getPROPERTY, bool getRELATION)
    //{
    //  ldOpts.LoadWith<IKGD_VNODE>(vn => vn.IKGD_SNODE);
    //  ldOpts.LoadWith<IKGD_VNODE>(vn => vn.IKGD_RNODE);
    //  if (getINODE)
    //    ldOpts.LoadWith<IKGD_RNODE>(r => r.IKGD_INODEs);
    //  if (getVDATA)
    //    ldOpts.LoadWith<IKGD_RNODE>(r => r.IKGD_VDATAs);
    //  if (getPROPERTY)
    //    ldOpts.LoadWith<IKGD_RNODE>(r => r.IKGD_PROPERTies);
    //  if (getRELATION)
    //    ldOpts.LoadWith<IKGD_SNODE>(s => s.IKGD_RELATIONs_src);

    //  if (VersionFrozen == 0)
    //  {
    //    //return vQuery.Where(vn => vn.flag_published);
    //    if (getINODE)
    //      ldOpts.AssociateWith<IKGD_RNODE>(n => n.IKGD_INODEs.Where(r => r.flag_published));
    //    if (getVDATA)
    //      ldOpts.AssociateWith<IKGD_RNODE>(n => n.IKGD_VDATAs.Where(r => r.flag_published));
    //    if (getPROPERTY)
    //      ldOpts.AssociateWith<IKGD_RNODE>(n => n.IKGD_PROPERTies.Where(r => r.flag_published));
    //  }
    //  else if (VersionFrozen == -1)
    //  {
    //    //return vQuery.Where(vn => vn.flag_current && !vn.flag_deleted);
    //    if (getINODE)
    //      ldOpts.AssociateWith<IKGD_RNODE>(n => n.IKGD_INODEs.Where(r => r.flag_current));
    //    if (getVDATA)
    //      ldOpts.AssociateWith<IKGD_RNODE>(n => n.IKGD_VDATAs.Where(r => r.flag_current));
    //    if (getPROPERTY)
    //      ldOpts.AssociateWith<IKGD_RNODE>(n => n.IKGD_PROPERTies.Where(r => r.flag_current));
    //  }
    //  else
    //  {
    //    //return vQuery.Where(vn => (vn.flag_published && vn.version_frozen <= VersionFrozen) || (!vn.flag_published && vn.version_frozen == VersionFrozen));
    //    // NB poi devo usare flag_deleted per filtare le proprieta' da eliminare (v. casi con freeze multipli)
    //    if (getINODE)
    //      ldOpts.AssociateWith<IKGD_RNODE>(n => n.IKGD_INODEs.Where(r => (r.flag_published && r.version_frozen < VersionFrozen) || (r.version_frozen == VersionFrozen)));
    //    if (getVDATA)
    //      ldOpts.AssociateWith<IKGD_RNODE>(n => n.IKGD_VDATAs.Where(r => (r.flag_published && r.version_frozen < VersionFrozen) || (r.version_frozen == VersionFrozen)));
    //    if (getPROPERTY)
    //      ldOpts.AssociateWith<IKGD_RNODE>(n => n.IKGD_PROPERTies.Where(r => (r.flag_published && r.version_frozen < VersionFrozen) || (r.version_frozen == VersionFrozen)));
    //  }
    //}

    //
    // ritorna il vNode corrispondente ad un sNode
    //
    //public IKGD_VNODE GetActiveNode(int sNodeCode, bool includeDeleted) { return FilterVNodesByVersion(DB.IKGD_VNODEs, includeDeleted).FirstOrDefault(n => n.snode == sNodeCode); }
    //public IKGD_VNODE GetActiveNode(int sNodeCode) { return FilterVNodesByVersion(DB.IKGD_VNODEs).FirstOrDefault(n => n.snode == sNodeCode); }

    //
    // ritorna un nodo con tutte le dipendenze risolte e caricate
    //
    public FS_FileInfo GetActiveNodeFullInfo(int sNodeCode)
    {
      FS_NodeInfoExt_Interface nodeInfo = this.Get_NodesInfoFilteredExt2(vn => vn.snode == sNodeCode, null, FS_Operations.FilterVFS.Disabled | FS_Operations.FilterVFS.ACL | FS_Operations.FilterVFS.Deleted).FirstOrDefault();
      FS_FileInfo fsNode = new FS_FileInfo(null, nodeInfo.vNode, nodeInfo.iNode, nodeInfo.vData, nodeInfo.Properties, nodeInfo.Relations, true);
      return fsNode;
      //
      //FS_NodeInfo nodeInfo =
      // (from vNode in this.NodesActive<IKGD_VNODE>(true).Where(n => n.snode == sNodeCode)
      //  from vData in this.NodesActive<IKGD_VDATA>(true).Where(n => n.rnode == vNode.rnode)
      //  from iNode in this.NodesActive<IKGD_INODE>(true).Where(n => n.rnode == vNode.rnode).DefaultIfEmpty()
      //  select new FS_NodeInfo { vNode = vNode, vData = vData, iNode = iNode }).FirstOrDefault();
      //FS_FileInfo fsNode = new FS_FileInfo(null, nodeInfo.vNode, nodeInfo.iNode, nodeInfo.vData, this.NodesActive<IKGD_PROPERTY>().Where(n => n.rnode == nodeInfo.vNode.rnode), this.NodesActive<IKGD_RELATION>().Where(n => n.snode_src == nodeInfo.vNode.snode), true);
      //return fsNode;
    }
    //public FS_FileInfo GetActiveNodeFullInfo(int sNodeCode)
    //{
    //  // uso un nuovo context per poter settare liberamente le opzioni di loading senza effetti collaterali sul DB della classe
    //  using (IKGD_DataContext LDB = IKGD_DBH.GetDB(DB))
    //  {
    //    //LDB.Log = new LINQ_Logger();
    //    var ldOpts = new DataLoadOptions();
    //    LoadWithFilterSetup(ldOpts, true, true, true, true);
    //    LDB.LoadOptions = ldOpts;
    //    IKGD_VNODE vNode = FilterVNodesByVersion(LDB.IKGD_VNODEs, true).Where(vn => vn.snode == sNodeCode).FirstOrDefault();
    //    if (vNode == null)
    //      return null;
    //    var rNode = vNode.IKGD_RNODE;
    //    var sNode = vNode.IKGD_SNODE;
    //    FS_FileInfo fi = new FS_FileInfo(null, vNode, rNode.IKGD_INODEs.FirstOrDefault(), rNode.IKGD_VDATAs.FirstOrDefault(), rNode.IKGD_PROPERTies, sNode.IKGD_RELATIONs_src, true);
    //    fi.ChildCount = sNode.IKGD_VNODEs_parent.Count;
    //    return fi;
    //  }
    //}

    //
    // path support methods
    //

    public static string PathNormalize(string path)
    {
      path = path.Trim().Replace(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
      string preStr = path.StartsWith("/") ? "/" : string.Empty;
      string postStr = path.EndsWith("/") ? "/" : string.Empty;
      List<string> frags = path.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar).ToList();
      frags = frags.Select(f => f.Trim()).Where(f => f.Length > 0 && f != ".").ToList();
      List<string> fragsProc = new List<string>();
      foreach (string frag in frags)
      {
        if (frag != "..")
          fragsProc.Add(frag);
        else if (fragsProc.Count > 0)
          fragsProc.RemoveAt(fragsProc.Count - 1);
      }
      string pathNorm = preStr + string.Join("/", fragsProc.ToArray()) + postStr;
      return pathNorm;
    }



    //
    // folder reading methods
    //

    //
    // metodi generali di ricorsione sul VFS
    // nel caso sia necessario posticipare il filtro sui risultati (es. filtro sui nomi ma senza bloccare le directories)
    // usare una ricorsione senza condizioni con un where applicato al set risultante
    // nestingLevels == 0  --> non esegue la ricorsione e ritorna il nodo stesso se yieldSelf
    //
    public IEnumerable<IKGD_VNODE> IKGD_VNODEsRecurse(IEnumerable<IKGD_VNODE> vNodes, Func<IKGD_VNODE, bool> vNodesFilter, bool yieldSelf, bool orderedList, int? nestingLevels, bool includeDeleted)
    {
      foreach (IKGD_VNODE vNode in vNodes)
        foreach (IKGD_VNODE n in IKGD_VNODEsRecurse(vNode, vNodesFilter, yieldSelf, orderedList, nestingLevels, includeDeleted))
          yield return n;
    }
    public IEnumerable<IKGD_VNODE> IKGD_VNODEsRecurse(IKGD_VNODE vNode, Func<IKGD_VNODE, bool> vNodesFilter, bool yieldSelf, bool orderedList, int? nestingLevels, bool includeDeleted)
    {
      if (!nestingLevels.HasValue)
        nestingLevels = int.MaxValue;
      if (vNode == null || nestingLevels < 0)
        yield break;
      if (vNodesFilter == null)
        vNodesFilter = vn => true;
      if (yieldSelf)
        if (vNodesFilter.Invoke(vNode))
          yield return vNode;
      if (vNode.flag_folder && nestingLevels > 0)
      {
        if (orderedList)
        {
          // files e directory ordinati con i criteri di sorting
          foreach (IKGD_VNODE subNode in FilterVNodesByVersion(DB.IKGD_VNODEs, includeDeleted).Where(vn => (!vn.flag_folder && vn.folder == vNode.folder) || (vn.flag_folder && vn.parent == vNode.folder)).Where(vNodesFilter).OrderBy(vn => vn.position).ThenBy(vn => vn.name.ToLower()))
          {
            yield return subNode;
            foreach (IKGD_VNODE n in IKGD_VNODEsRecurse(subNode, vNodesFilter, false, orderedList, (nestingLevels ?? int.MaxValue) - 1, includeDeleted))
              yield return n;
          }
        }
        else
        {
          // prima le directory poi i files
          //foreach (IKGD_VNODE subNode in FilterVNodesByVersion(vNode.IKGD_SNODE.IKGD_VNODEs_parent.Where(vNodesFilter).AsQueryable(), includeDeleted))
          foreach (IKGD_VNODE subNode in FilterVNodesByVersion(vNode.IKGD_RNODE_folder.IKGD_VNODEs_parent.Where(vNodesFilter).AsQueryable(), includeDeleted))
          {
            yield return subNode;
            foreach (IKGD_VNODE n in IKGD_VNODEsRecurse(subNode, vNodesFilter, false, orderedList, (nestingLevels ?? int.MaxValue) - 1, includeDeleted))
              yield return n;
          }
          //foreach (IKGD_VNODE subNode in FilterVNodesByVersion(vNode.IKGD_VNODEs_sibling.Where(n => !n.flag_folder).Where(vNodesFilter).AsQueryable(), includeDeleted))
          foreach (IKGD_VNODE subNode in FilterVNodesByVersion(vNode.IKGD_RNODE_folder.IKGD_VNODEs_folder.Where(n => !n.flag_folder).Where(vNodesFilter).AsQueryable(), includeDeleted))
            yield return subNode;
        }
      }
    }


    public interface FS_NodeInfo_Interface : ICloneable
    {
      IKGD_VNODE vNode { get; set; }
      IKGD_VDATA vData { get; set; }
      IKGD_INODE iNode { get; set; }
      //
      int sNode { get; }
      int rNode { get; }
      int Folder { get; }
      int? ParentFolder { get; }
      string Language { get; }
      string ManagerType { get; }
      string Key { get; }
      string Category { get; }
      double Position { get; }
      bool IsActive { get; }
      bool IsDeleted { get; }
      bool IsFolder { get; }
      bool IsPublished { get; }
      bool IsCurrent { get; }
      bool IsNotExpired { get; }
      string Name { get; }
      string Placeholder { get; }
      int VersionVNODE { get; }
      int VersionVDATA { get; }
      int? VersionINODE { get; }
      //
      bool LanguageCheck(string languageMain);
      DateTime DateLastModified { get; }
      string ToString();
    }
    public interface FS_NodeInfoExt_Interface : FS_NodeInfo_Interface
    {
      List<IKGD_RELATION> Relations { get; set; }
      List<IKGD_PROPERTY> Properties { get; set; }
    }
    public interface FS_NodeInfoExt2_Interface : FS_NodeInfoExt_Interface
    {
      List<IKATT_AttributeMapping> Variants { get; set; }
      //List<int> VariantsIds { get; set; }
    }
    public interface FS_NodeInfoStreams_Interface : FS_NodeInfo_Interface
    {
      List<IKGD_STREAM> Streams { get; set; }
    }
    public class FS_NodeInfo : FS_NodeInfo_Interface
    {
      public IKGD_VNODE vNode { get; set; }
      public IKGD_VDATA vData { get; set; }
      public IKGD_INODE iNode { get; set; }

      public int sNode { get { return vNode.snode; } }
      public int rNode { get { return vNode.rnode; } }
      public int Folder { get { return vNode.folder; } }
      public int? ParentFolder { get { return vNode.parent; } }
      public int? FolderOrParent { get { return vNode.flag_folder ? vNode.parent : (int?)vNode.folder; } }
      public string Language { get { return vNode.language; } }
      public string ManagerType { get { return vData.manager_type; } }
      public string Key { get { return vData.key; } }
      public string Category { get { return vData.category; } }
      public double Position { get { return vNode.position; } }
      public bool IsActive { get { return (vNode.flag_deleted || vData.flag_inactive || vData.flag_deleted) == false; } }
      public bool IsDeleted { get { return (vNode.flag_deleted || vData.flag_deleted) == true; } }
      public bool IsFolder { get { return vNode.flag_folder; } }
      public bool IsPublished { get { return vNode.flag_published && vData.flag_published && (iNode == null || iNode.flag_published); } }
      public bool IsCurrent { get { return vNode.flag_current && vData.flag_current && (iNode == null || iNode.flag_current); } }
      public bool IsNotExpired { get { { DateTime dateCMS = Ikon.GD.FS_OperationsHelpers.DateTimeSession; return (vData.date_activation == null || vData.date_activation <= dateCMS) && (vData.date_expiry == null || dateCMS <= vData.date_expiry); } } }
      public string Name { get { return vNode.name; } }
      public string Placeholder { get { return vNode.placeholder; } }
      public string Template { get { return vNode.template; } }
      public int VersionVNODE { get { return vNode.version; } }
      public int VersionVDATA { get { return vData.version; } }
      public int? VersionINODE { get { return iNode == null ? null : (int?)iNode.version; } }

      public virtual bool LanguageCheck(string languageMain) { return (languageMain == null || Language == null || string.Equals(Language, languageMain)); }

      public DateTime DateLastModified { get { return Utility.MaxAll(vNode.version_date, vData.version_date, (iNode != null) ? iNode.version_date : DateTime.MinValue); } }


      public override string ToString()
      {
        try { return string.Format("[{0}/{1}] {2}", vNode.snode, vNode.rnode, vNode.name); }
        catch { return "NULL"; }
      }

      protected virtual void DupWorker(FS_NodeInfo_Interface target)
      {
        if (target == null)
          return;
        //
        if (this.vNode != null)
        {
          target.vNode = new IKGD_VNODE
          {
            flag_current = this.vNode.flag_current,
            flag_deleted = this.vNode.flag_deleted,
            flag_published = this.vNode.flag_published,
            flag_folder = this.vNode.flag_folder,
            flag_noDelete = this.vNode.flag_noDelete,
            rnode = this.vNode.rnode,
            snode = this.vNode.snode,
            folder = this.vNode.folder,
            language = this.vNode.language,
            name = this.vNode.name,
            parent = this.vNode.parent,
            placeholder = this.vNode.placeholder,
            position = this.vNode.position,
            template = this.vNode.template,
            version = this.vNode.version,
            version_date = this.vNode.version_date,
            version_frozen = this.vNode.version_frozen,
            //username = this.iNode.username,  //IsDelayLoaded
          };
        }
        //
        if (this.iNode != null)
        {
          target.iNode = new IKGD_INODE
          {
            filename = this.iNode.filename,
            flag_current = this.iNode.flag_current,
            flag_deleted = this.iNode.flag_deleted,
            flag_published = this.iNode.flag_published,
            mime = this.iNode.mime,
            rnode = this.iNode.rnode,
            size = this.iNode.size,
            version = this.iNode.version,
            version_date = this.iNode.version_date,
            version_frozen = this.iNode.version_frozen,
            //username = this.iNode.username,  //IsDelayLoaded
          };
        }
        //
        if (this.vData != null)
        {
          target.vData = new IKGD_VDATA
          {
            flag_current = this.vData.flag_current,
            flag_deleted = this.vData.flag_deleted,
            flag_published = this.vData.flag_published,
            flag_inactive = this.vData.flag_inactive,
            flag_unstructured = this.vData.flag_unstructured,
            flags_menu = this.vData.flags_menu,
            //flag_autoDeleteOnRels = this.vData.flag_autoDeleteOnRels,  //IsDelayLoaded
            rnode = this.vData.rnode,
            area = this.vData.area,
            category = this.vData.category,
            date_activation = this.vData.date_activation,
            date_expiry = this.vData.date_expiry,
            date_node = this.vData.date_node,
            date_node_aux = this.vData.date_node_aux,
            geoLatY = this.vData.geoLatY,
            geoLonX = this.vData.geoLonX,
            geoRangeM = this.vData.geoRangeM,
            key = this.vData.key,
            manager_type = this.vData.manager_type,
            settings = this.vData.settings,
            version = this.vData.version,
            version_date = this.vData.version_date,
            version_frozen = this.vData.version_frozen,
            //username = this.iNode.username,  //IsDelayLoaded
          };
        }
        //
        if (this is FS_NodeInfoExt)
        {
          var src = this as FS_NodeInfoExt;
          if (src.Relations != null)
          {
            (target as FS_NodeInfoExt).Relations = src.Relations.Select(r => new IKGD_RELATION
            {
              version = r.version,
              version_date = r.version_date,
              version_frozen = r.version_frozen,
              flag_current = r.flag_current,
              flag_published = r.flag_published,
              flag_deleted = r.flag_deleted,
              rnode = r.rnode,
              rnode_dst = r.rnode_dst,
              snode_dst = r.snode_dst,
              snode_src = r.snode_src,
              //username = r.username,  //IsDelayLoaded
              type = r.type,
              position = r.position
            }).ToList();
          }
          if (src.Properties != null)
          {
            (target as FS_NodeInfoExt).Properties = src.Properties.Select(r => new IKGD_PROPERTY
            {
              version = r.version,
              version_date = r.version_date,
              version_frozen = r.version_frozen,
              flag_current = r.flag_current,
              flag_published = r.flag_published,
              flag_deleted = r.flag_deleted,
              rnode = r.rnode,
              name = r.name,
              value = r.value,
              //data = r.data,  //IsDelayLoaded
              //username = r.username,  //IsDelayLoaded
              attributeId = r.attributeId
            }).ToList();
          }
        }
        //
        if (this is FS_NodeInfoExt2)
        {
          var src = this as FS_NodeInfoExt2;
          if (src.Variants != null)
          {
            (target as FS_NodeInfoExt2).Variants = src.Variants.Select(r => new IKATT_AttributeMapping
            {
              AttributeId = r.AttributeId,
              rNode = r.rNode,
              Data = r.Data
            }).ToList();
          }
          //if (src.VariantsIds != null)
          //{
          //  (target as FS_NodeInfoExt2).VariantsIds = src.VariantsIds.ToList();
          //}
        }
        //
        if (this is FS_NodeInfoStreams)
        {
          var src = this as FS_NodeInfoStreams;
          if (src.Streams != null)
          {
            (target as FS_NodeInfoStreams).Streams = src.Streams.Select(r => new IKGD_STREAM
            {
              id = r.id,
              inode = r.inode,
              source = r.source,
              key = r.key,
              type = r.type,
              //data = new System.Data.Linq.Binary(r.data.ToArray()),  //IsDelayLoaded
            }).ToList();
          }
        }
        //
      }

      public virtual object Clone()
      {
        FS_NodeInfo_Interface target = new FS_NodeInfo();
        DupWorker(target);
        return target;
      }

    }

    public class FS_NodeInfo2 : FS_NodeInfo
    {
      public int ChildCount { get; set; }
      public bool HasTaintedDependancies { get; set; }

      public override object Clone()
      {
        FS_NodeInfo_Interface target = new FS_NodeInfo2();
        DupWorker(target);
        (target as FS_NodeInfo2).ChildCount = this.ChildCount;
        (target as FS_NodeInfo2).HasTaintedDependancies = this.HasTaintedDependancies;
        return target;
      }

    }

    public class FS_NodeFolderInfo : FS_NodeInfo
    {
      public int FilesCount { get; set; }

      public override object Clone()
      {
        FS_NodeInfo_Interface target = new FS_NodeFolderInfo();
        DupWorker(target);
        (target as FS_NodeFolderInfo).FilesCount = this.FilesCount;
        return target;
      }

    }

    public class FS_NodeInfoExt : FS_NodeInfo, FS_NodeInfoExt_Interface
    {
      public List<IKGD_RELATION> Relations { get; set; }
      public List<IKGD_PROPERTY> Properties { get; set; }

      public override object Clone()
      {
        FS_NodeInfo_Interface target = new FS_NodeInfoExt();
        DupWorker(target);
        return target;
      }

    }

    public class FS_NodeInfoExt2 : FS_NodeInfoExt, FS_NodeInfoExt2_Interface
    {
      public List<IKATT_AttributeMapping> Variants { get; set; }
      //public List<int> VariantsIds { get; set; }

      public override object Clone()
      {
        FS_NodeInfo_Interface target = new FS_NodeInfoExt2();
        DupWorker(target);
        return target;
      }

    }

    public class FS_NodeInfoStreams : FS_NodeInfo, FS_NodeInfoStreams_Interface
    {
      public List<IKGD_STREAM> Streams { get; set; }

      public override object Clone()
      {
        FS_NodeInfo_Interface target = new FS_NodeInfoStreams();
        DupWorker(target);
        return target;
      }

    }

    public class FS_TreeNodeData : FS_NodeInfo
    {
      public FS_TreeNodeData Parent { get; set; }
      public List<FS_TreeNodeData> Nodes { get; private set; }
      //
      public FS_TreeNodeData()
      {
        Nodes = new List<FS_TreeNodeData>();
      }
      public FS_TreeNodeData(FS_TreeNodeData parentNode)
        : this()
      {
        Parent = parentNode;
        if (parentNode != null)
          parentNode.Nodes.Add(this);
      }
    }
    public class FS_TreeNodeData2 : FS_NodeInfo
    {
      public FS_TreeNodeData2 Parent { get { return Parents.FirstOrDefault(); } }
      public List<FS_TreeNodeData2> Parents { get; private set; }
      public List<FS_TreeNodeData2> Nodes { get; private set; }
      //
      public FS_TreeNodeData2()
      {
        Parents = new List<FS_TreeNodeData2>();
        Nodes = new List<FS_TreeNodeData2>();
      }
    }


    [Serializable]
    [JsonObject(MemberSerialization.OptIn, IsReference = true)]
    [DataContract(IsReference = true)]
    //public class FS_TreeNode<T> : IEnumerable<FS_TreeNode<T>>, IEquatable<FS_TreeNode<T>>
    public class FS_TreeNode<T> : IEnumerable<FS_TreeNode<T>>
    {
      [DataMember]
      public T Data { get; set; }

      [DataMember]
      protected List<FS_TreeNode<T>> _Nodes;
      public IList<FS_TreeNode<T>> Nodes { get { return _Nodes; } }
      //public List<FS_TreeNode<T>> Nodes { get; protected set; }


      protected FS_TreeNode()
      {
        this._Nodes = new List<FS_TreeNode<T>>();
      }

      public FS_TreeNode(FS_TreeNode<T> parentNode, T data)
        : this()
      {
        this.Data = data;
        this.Parent = parentNode;
      }


      [DataMember]
      protected FS_TreeNode<T> _Parent = null;
      public FS_TreeNode<T> Parent
      {
        get { return _Parent; }
        set
        {
          if (_Parent != null)
            _Parent.Nodes.Remove(this);
          _Parent = value;
          if (_Parent != null)
            _Parent.Nodes.Add(this);
        }
      }

      public virtual void ParentSetNoDeps(FS_TreeNode<T> newParent) { _Parent = newParent; }

      public int Level { get { return (Parent == null) ? 0 : Parent.Level + 1; } }

      /*
      public override bool Equals(object obj)
      {
        if (obj == null) return base.Equals(obj);
        if (obj is FS_TreeNode<T>)
          return this.Equals((FS_TreeNode<T>)obj);
        return false;
      }
      public bool Equals(FS_TreeNode<T> obj)
      {
        if (ReferenceEquals(null, obj)) return false;
        if (ReferenceEquals(this, obj)) return true;
        return Equals(obj.Data, Data);
      }
      public static bool operator ==(FS_TreeNode<T> obj1, FS_TreeNode<T> obj2)
      {
        if (ReferenceEquals(obj1, obj2)) return true;
        if (ReferenceEquals(null, obj1)) return false;
        return obj1.Equals(obj2);
      }
      public static bool operator !=(FS_TreeNode<T> obj1, FS_TreeNode<T> obj2)
      {
        if (ReferenceEquals(obj1, obj2)) return false;
        if (ReferenceEquals(null, obj1)) return true;
        return (!obj1.Equals(obj2));
      }
      */
      public override int GetHashCode()
      {
        if (Data != null)
          return Data.GetHashCode();
        return base.GetHashCode();
      }


      public override string ToString()
      {
        try { return Data.ToString(); }
        catch { return "NULL"; }
      }


      public virtual void Clear()
      {
        try
        {
          if (_Nodes != null && _Nodes.Any())
          {
            for (int idx = _Nodes.Count - 1; idx >= 0; idx--)
            {
              try
              {
                if (_Nodes[idx] != null)
                {
                  _Nodes[idx].ParentSetNoDeps(null);
                  _Nodes[idx].Clear();
                }
                _Nodes[idx] = null;
              }
              catch { }
            }
            _Nodes.Clear();
            Data = default(T);
            _Nodes = null;
          }
        }
        catch { }
      }


      IEnumerator<FS_TreeNode<T>> IEnumerable<FS_TreeNode<T>>.GetEnumerator() { return RecurseOnTree.GetEnumerator(); }
      IEnumerator IEnumerable.GetEnumerator() { return RecurseOnTree.GetEnumerator(); }


      public IEnumerable<FS_TreeNode<T>> RecurseOnTree
      {
        get
        {
          yield return this;
          foreach (FS_TreeNode<T> node in Nodes)
            foreach (FS_TreeNode<T> subNode in node.RecurseOnTree)
              yield return subNode;
        }
      }

      public IEnumerable<T> RecurseOnData
      {
        get
        {
          if (this.Data != null)
            yield return this.Data;
          foreach (FS_TreeNode<T> node in Nodes)
            foreach (T res in node.RecurseOnData)
              yield return res;
        }
      }

      public IEnumerable<FS_TreeNode<T>> BackRecurseOnTree
      {
        get
        {
          for (var node = this; node != null; node = node.Parent)
            yield return node;
        }
      }

      public IEnumerable<T> BackRecurseOnData
      {
        get
        {
          for (var node = this; node != null; node = node.Parent)
            yield return node.Data;
        }
      }

      public IEnumerable<FS_TreeNode<T>> Siblings
      {
        get
        {
          if (Parent != null)
            foreach (FS_TreeNode<T> node in Parent.Nodes.Where(n => n != this))
              yield return node;
        }
      }

      public IEnumerable<FS_TreeNode<T>> SiblingsPrev
      {
        get
        {
          if (Parent != null)
            foreach (FS_TreeNode<T> node in Parent.Nodes.TakeWhile(n => n != this))
              yield return node;
        }
      }

      public IEnumerable<FS_TreeNode<T>> SiblingsNext
      {
        get
        {
          if (Parent != null)
            foreach (FS_TreeNode<T> node in Parent.Nodes.SkipWhile(n => n != this).Skip(1))
              yield return node;
        }
      }


      //
      // clonazione della struttura del tree ma senza la duplicazione dei payload
      //
      public FS_TreeNode<T> CloneSubTree()
      {
        FS_TreeNode<T> node = new FS_TreeNode<T>(null, this.Data);
        foreach (var child in Nodes)
          child.CloneSubTree().Parent = node;
        return node;
      }


    }


    public class VFS_TreeNode<T> : FS_TreeNode<T> where T : FS_Operations.FS_NodeInfo_Interface
    {
      public IKGD_VNODE vNode { get { return this.Data.vNode; } }
      public IKGD_VDATA vData { get { return this.Data.vData; } }
      public IKGD_INODE iNode { get { return this.Data.iNode; } }

      public VFS_TreeNode(VFS_TreeNode<T> parentNode, T data) : base(parentNode, data) { }


      public IEnumerable<FS_TreeNode<T>> NodesOrdered { get { return Nodes.Where(n => n.Data != null).OrderBy(n => n.Data.vNode.position).ThenBy(n => n.Data.vNode.name).ThenBy(n => n.Data.vNode.snode); } }


      public IEnumerable<VFS_TreeNode<T>> RecurseOnTreeOrdered
      {
        get
        {
          if (this.Data != null)
            yield return this;
          //foreach (VFS_TreeNode<T> node in Nodes.Where(n => n.Data != null).OrderBy(n => n.Data.vNode.position).ThenBy(n => n.Data.vNode.name).ThenBy(n => n.Data.vNode.snode))
          foreach (VFS_TreeNode<T> node in NodesOrdered)
            foreach (VFS_TreeNode<T> subNode in node.RecurseOnTreeOrdered)
              yield return subNode;
        }
      }


      public override string ToString()
      {
        try { return "({1}){0}".FormatString(Data.ToString(), Level); }
        catch { return "NULL"; }
      }

    }


    //
    // read a single folder with filter and various settings
    //
    //public IEnumerable<FS_FileInfo> GetFolderContents(int folder_sCode, Func<IKGD_VNODE, bool> vNodesFilter, AttributesSelector? attrsToFetch, bool includeDeleted)
    //{
    //  attrsToFetch = attrsToFetch ?? AttributesSelectorDefault;
    //  bool get_iNode = (attrsToFetch & AttributesSelector.iNode) != 0;
    //  bool get_vData = (attrsToFetch & AttributesSelector.Settings) != 0;
    //  bool get_Properties = (attrsToFetch & AttributesSelector.Properties) != 0;
    //  bool get_Relations = (attrsToFetch & AttributesSelector.Relations) != 0;
    //  // uso un nuovo context per poter settare liberamente le opzioni di loading senza effetti collaterali sul DB della classe
    //  using (IKGD_DataContext LDB = IKGD_DBH.GetDB(DB))
    //  {
    //    //LDB.Log = new LINQ_Logger();
    //    var ldOpts = new DataLoadOptions();
    //    LoadWithFilterSetup(ldOpts, get_iNode, get_vData, get_Properties, get_Relations);
    //    LDB.LoadOptions = ldOpts;

    //    int auxCode_folder = rNodeCodeRoot;
    //    int auxCode_sNode = folder_sCode;
    //    // nella costruzione di FS_FileInfo voglio un parent folder appartenente allo stesso context dei nodi della ricorsione
    //    //IKGD_VNODE vFolderAux = LDB.IKGD_VNODEs.FirstOrDefault(n => n.version == vFolder.version);
    //    IKGD_VNODE vFolderAux = FilterVNodesByVersion(LDB.IKGD_VNODEs, includeDeleted).FirstOrDefault(n => n.snode == folder_sCode);
    //    if (vFolderAux != null)
    //      auxCode_folder = vFolderAux.folder;
    //    else if (vFolderAux == null && auxCode_sNode != sNodeCodeRoot)
    //      yield break;

    //    //
    //    // per default leggo comunque sia files che directories
    //    //
    //    //var vNodes = FilterVNodesByVersion(LDB.IKGD_VNODEs, includeDeleted).Where(vn => (!vn.flag_folder && vn.folder == vFolderAux.folder) || (vn.flag_folder && vn.parent == vFolderAux.snode));
    //    var vNodes = FilterVNodesByVersion(LDB.IKGD_VNODEs, includeDeleted).Where(vn => (!vn.flag_folder && vn.folder == auxCode_folder) || (vn.flag_folder && vn.parent == auxCode_sNode));
    //    //default sorting mode
    //    vNodes = vNodes.OrderBy(vn => vn.position).ThenBy(vn => vn.name.ToLower());
    //    //
    //    // se definito attiva un filtro sui nodi
    //    // per operare a livello di SQL deve essere del tipo Expression<Func<IKGD_VNODE, bool>>, nel caso sia una normale lambda
    //    // aggiungiamo per ultimo il filtro per sfruttare l'SQL il piu' possibile
    //    //
    //    if (vNodesFilter != null)
    //      vNodes = vNodes.Where(vNodesFilter).AsQueryable<IKGD_VNODE>();
    //    //
    //    foreach (FS_NodeData nodeData in vNodes.Select(vn => new FS_NodeData { vNode = vn, rNode = vn.IKGD_RNODE }))
    //    {
    //      FS_FileInfo fi = new FS_FileInfo(vFolderAux, nodeData.vNode,
    //        get_iNode ? nodeData.rNode.IKGD_INODEs.FirstOrDefault() : null,
    //        get_vData ? nodeData.rNode.IKGD_VDATAs.FirstOrDefault() : null,
    //        get_Properties ? nodeData.rNode.IKGD_PROPERTies : null,
    //        get_Relations ? this.NodesActive<IKGD_RELATION>().Where(n => n.snode_src == nodeData.vNode.snode) : null,
    //        (attrsToFetch & AttributesSelector.ACLs) != 0);
    //      if (fi.ACL.Has_Read)
    //        yield return fi;
    //    }
    //  }
    //}


    //
    // legge una lista di nodi a partire dai nodi di un albero gia' aperto
    // nodesIdList contiene una lista di parent (sNode ID) per i quali caricare tutti i child
    //
    //public List<FS_FileInfo> GetFoldersTreeExpandData(int NodeIdToOpen, List<int> expandedNodesIdList)
    //{
    //  bool expandNodeIdToOpen = false;
    //  bool get_vData = true;  // le ACLs sono in vData
    //  //
    //  List<FS_FileInfo> fsNodes = new List<FS_FileInfo>();
    //  // uso un nuovo context per poter settare liberamente le opzioni di loading senza effetti collaterali sul DB della classe
    //  using (IKGD_DataContext LDB = IKGD_DBH.GetDB(DB))
    //  {
    //    //LDB.Log = new LINQ_Logger();
    //    var ldOpts = new DataLoadOptions();
    //    LoadWithFilterSetup(ldOpts, false, get_vData, false, false);
    //    LDB.LoadOptions = ldOpts;

    //    //
    //    // mi assicuro che selezionando un nuovo nodo ci sia tutto il suo path
    //    //
    //    List<int> nodesIdList = new List<int>() { sNodeCodeRoot }; // la root e' gia' inclusa
    //    if (expandNodeIdToOpen)
    //    {
    //      for (var np = GetActiveNode(NodeIdToOpen, true); np != null; np = FilterVNodesByVersion(np.IKGD_SNODE_parent.IKGD_VNODEs.AsQueryable()).FirstOrDefault())
    //        if (!nodesIdList.Contains(np.snode))
    //          nodesIdList.Add(np.snode);
    //    }
    //    else
    //    {
    //      var np = GetActiveNode(NodeIdToOpen, true);
    //      while ((np = FilterVNodesByVersion(np.IKGD_SNODE_parent.IKGD_VNODEs.AsQueryable()).FirstOrDefault()) != null)
    //        if (!nodesIdList.Contains(np.snode))
    //          nodesIdList.Add(np.snode);
    //    }
    //    if (expandedNodesIdList != null)
    //      nodesIdList = nodesIdList.Union(expandedNodesIdList).ToList();

    //    // estrazione dei soli folder e dei child, non inserisco il nodo stesso perche' e' gia' un child del parent che deve essere per forza aperto
    //    var vNodes = FilterVNodesByVersion(LDB.IKGD_VNODEs).Where(vn => vn.flag_folder && nodesIdList.Contains(vn.parent.Value));
    //    var vNodesData = vNodes.Select(vn => new FS_NodeData { vNode = vn, rNode = vn.IKGD_RNODE, ChildCount = vn.IKGD_SNODE.IKGD_VNODEs_parent.Count });
    //    //
    //    foreach (FS_NodeData nodeData in vNodesData)
    //    {
    //      FS_FileInfo fi = new FS_FileInfo(null, nodeData.vNode, null, get_vData ? nodeData.rNode.IKGD_VDATAs.FirstOrDefault() : null, null, null, true);
    //      fi.ChildCount = nodeData.ChildCount;
    //      if (get_vData && !fi.ACL.Has_Read)
    //        continue;
    //      fsNodes.Add(fi);
    //    }
    //  }
    //  //foreach (FS_FileInfo fi in fsNodes)
    //  //{
    //  //  var pf = fsNodes.FirstOrDefault(n => n.Code_SNODE == fi.Code_Parent);
    //  //  if (pf != null)
    //  //    fi.ParentFolder = pf.vNode;
    //  //}
    //  // non e' un ordinamento completo ma garantisce l'ordine corretto per i subfolders di ciascuna directory
    //  return fsNodes.OrderBy(n => n.Code_Parent).ThenBy(n => n.Position).ThenBy(n => n.Name).ToList();
    //}


    public List<FS_FileInfo> GetFoldersTreeExpandDataV2(int sNodeSelected, IEnumerable<int> rootFolders, IEnumerable<int> foldersToExpand)
    {
      List<FS_FileInfo> fsNodes = new List<FS_FileInfo>();
      try
      {
        List<int> rootFoldersWorker = rootFolders == null ? IKGD_ConfigVFS.ConfigExt.RootsVFS_folders : rootFolders.ToList();
        var pathsAll = this.PathsFromNodeAuthor(sNodeSelected, true, true, false);
        var foldersToExpandedFromSelected = pathsAll.Where(p => p.Fragments.Any(f => rootFoldersWorker.Contains(f.rNode))).SelectMany(p => p.Fragments.SkipWhile(f => !rootFoldersWorker.Contains(f.rNode)).Where(f => f.flag_folder).Select(f => f.rNode)).ToList();
        var foldersToExpandAll = foldersToExpandedFromSelected.Concat(foldersToExpand ?? new int[] { }).Distinct().ToList();
        var foldersToFetch = rootFoldersWorker.Union(foldersToExpandAll).ToList();
        //
        Expression<Func<IKGD_VNODE, bool>> vNodeFilterAll = this.Get_vNodeFilterACLv2(IKGD_Language_Provider.Provider.LanguageAuthorNoFilterCode);  // devo vederli in IKGD
        Expression<Func<IKGD_VDATA, bool>> vDataFilterAll = this.Get_vDataFilterACLv2(true, true);   // devo vederli in IKGD
        //
        var nodesTmp =
          from vnp in this.NodesActive<IKGD_VNODE>().Where(vNodeFilterAll).Where(n => n.flag_folder && foldersToExpandAll.Contains(n.folder))
          from vNode in this.NodesActive<IKGD_VNODE>().Where(vNodeFilterAll).Where(n => n.flag_folder && (foldersToFetch.Contains(n.folder) || vnp.folder == n.parent.Value))
          from vData in this.NodesActive<IKGD_VDATA>().Where(vDataFilterAll).Where(n => n.rnode == vNode.rnode)
          select new FS_Operations.FS_NodeInfo2 { vNode = vNode, vData = vData, ChildCount = this.NodesActive<IKGD_VNODE>().Where(vNodeFilterAll).Count(n => n.flag_folder && n.parent.Value == vNode.folder) };
        //
        var nodes = nodesTmp.Distinct().OrderBy(n => n.vNode.parent.Value).ThenBy(n => n.vNode.position).ThenBy(n => n.vNode.name);
        //var names = nodes.Select(n => string.Format("[{0}]/{1}/[{2}]/{3}", n.vNode.snode, n.vNode.name, n.ChildCount, n.vData.area)).ToList();
        fsNodes = nodes.Select(n => new FS_FileInfo(null, n.vNode, null, n.vData, null, null, true) { ChildCount = n.ChildCount }).ToList();
      }
      catch { }
      return fsNodes;
    }


    public List<FS_FileInfo> GetFoldersTreeExpandData(int NodeIdToOpen, List<int> expandedNodesIdList)
    {
      List<FS_FileInfo> fsNodes = new List<FS_FileInfo>();
      try
      {
        //
        // lettura di tutti i path che portano al folder selezionato
        //
        List<int> nodesIdList = new List<int>() { sNodeCodeRoot };
        GetParentsRecurseWorker(this.NodeActive(NodeIdToOpen, true), nodesIdList);
        //
        if (expandedNodesIdList != null)
          nodesIdList = nodesIdList.Union(expandedNodesIdList).ToList();
        //
        Expression<Func<IKGD_VDATA, bool>> vDataFilterAll = this.Get_vDataFilterACLv2(true);  // devo vederli nel IKGD
        //
        var roots =
          from vNode in this.NodesActive<IKGD_VNODE>().Where(n => n.flag_folder && n.parent.Value == rNodeCodeRoot)
          from vData in this.NodesActive<IKGD_VDATA>().Where(vDataFilterAll).Where(n => n.rnode == vNode.rnode)
          select new FS_Operations.FS_NodeInfo2 { vNode = vNode, vData = vData, ChildCount = this.NodesActive<IKGD_VNODE>().Count(n => n.flag_folder && n.parent.Value == vNode.folder) };
        var subFolders =
          from vnp in this.NodesActive<IKGD_VNODE>().Where(n => n.flag_folder && nodesIdList.Contains(n.snode))
          from vNode in this.NodesActive<IKGD_VNODE>().Where(n => n.flag_folder && n.parent.Value == vnp.folder)
          from vData in this.NodesActive<IKGD_VDATA>().Where(vDataFilterAll).Where(n => n.rnode == vNode.rnode)
          select new FS_Operations.FS_NodeInfo2 { vNode = vNode, vData = vData, ChildCount = this.NodesActive<IKGD_VNODE>().Count(n => n.flag_folder && n.parent.Value == vNode.folder) };
        // attenzione che Concat rompe l'ordinamento...
        var nodes = roots.Concat(subFolders).Distinct().OrderBy(n => n.vNode.parent.Value).ThenBy(n => n.vNode.position).ThenBy(n => n.vNode.name);
        //var names = nodes.Select(n => string.Format("[{0}]/{1}/[{2}]/{3}", n.vNode.snode, n.vNode.name, n.ChildCount, n.vData.area)).ToList();
        foreach (FS_Operations.FS_NodeInfo2 fsInfo in nodes)
        {
          FS_FileInfo fi = new FS_FileInfo(null, fsInfo.vNode, null, fsInfo.vData, null, null, true);
          fi.ChildCount = fsInfo.ChildCount;
          fsNodes.Add(fi);
        }
      }
      catch { }
      return fsNodes;
    }


    public void GetParentsRecurseWorker(IKGD_VNODE vNode, List<int> nodesIdList)
    {
      if (vNode == null || !vNode.flag_folder || vNode.parent == null)
        return;
      nodesIdList.Add(vNode.snode);
      foreach (IKGD_VNODE np in this.NodesActive<IKGD_VNODE>().Where(n => n.flag_folder && n.folder == vNode.parent.Value))
        if (!nodesIdList.Contains(np.snode))
          GetParentsRecurseWorker(np, nodesIdList);
    }

    //
    // popola una lista di nodi a partire da una lista di nodi espansi
    // nodesIdList contiene una lista di parent (sNode ID) per i quali caricare tutti i child
    //
    //public List<FS_FileInfo> ExpandFolders(int sNodeFolder)
    //{
    //  List<FS_FileInfo> fsNodes = new List<FS_FileInfo>();

    //  attrsToFetch = attrsToFetch ?? AttributesSelectorDefault;
    //  bool get_vData = true;
    //  bool get_Properties = false;

    //  // uso un nuovo context per poter settare liberamente le opzioni di loading senza effetti collaterali sul DB della classe
    //  using (IKGD_DataContext LDB = IKGD_DBH.GetDB(DB))
    //  {
    //    //LDB.Log = new LINQ_Logger();
    //    var ldOpts = new DataLoadOptions();
    //    LoadWithFilterSetup(ldOpts, false, true, false, false);
    //    LDB.LoadOptions = ldOpts;

    //    // estrazione dei soli folder e dei child, non inserisco il nodo stesso perche' e' gia' un child del parent che deve essere per forza aperto
    //    var vNodes = FilterVNodesByVersion(LDB.IKGD_VNODEs).Where(vn => vn.flag_folder && vn.parent.Value == sNodeFolder);
    //    var vNodesData = vNodes.Select(vn => new FS_NodeData { vNode = vn, rNode = vn.IKGD_RNODE, ChildCount = vn.IKGD_SNODE.IKGD_VNODEs_parent.Count });
    //    //
    //    foreach (FS_NodeData nodeData in vNodesData)
    //    {
    //      FS_FileInfo fi = new FS_FileInfo(null, nodeData.vNode, null, nodeData.rNode.IKGD_VDATAs.FirstOrDefault(), null, null, true);
    //      fi.ChildCount = nodeData.ChildCount;
    //      if (!fi.ACL.Has_Read)
    //        continue;
    //      fsNodes.Add(fi);
    //    }
    //  }
    //  //foreach (FS_FileInfo fi in fsNodes)
    //  //{
    //  //  var pf = fsNodes.FirstOrDefault(n => n.Code_SNODE == fi.Code_Parent);
    //  //  if (pf != null)
    //  //    fi.ParentFolder = pf.vNode;
    //  //}
    //  return fsNodes.OrderBy(n => n.Position).ThenBy(n => n.Name).ToList();
    //}
    public List<FS_FileInfo> ExpandFolders(int sNodeFolder)
    {
      List<FS_FileInfo> fsNodes = new List<FS_FileInfo>();
      //
      try
      {
        IKGD_VNODE vFolder = this.NodeActive(sNodeFolder, true);
        //
        Expression<Func<IKGD_VNODE, bool>> vNodeFilterAll = this.Get_vNodeFilterACLv2(IKGD_Language_Provider.Provider.LanguageAuthorNoFilterCode);
        Expression<Func<IKGD_VDATA, bool>> vDataFilterAll = this.Get_vDataFilterACLv2(true, true);
        //
        var subFolders =
          from vNode in this.NodesActive<IKGD_VNODE>().Where(vNodeFilterAll).Where(n => n.flag_folder && n.parent.Value == vFolder.folder)
          from vData in this.NodesActive<IKGD_VDATA>().Where(vDataFilterAll).Where(n => n.rnode == vNode.rnode)
          orderby vNode.position, vNode.name
          select new FS_Operations.FS_NodeInfo2 { vNode = vNode, vData = vData, ChildCount = this.NodesActive<IKGD_VNODE>().Count(n => n.flag_folder && n.parent.Value == vNode.folder) };
        foreach (FS_Operations.FS_NodeInfo2 fsInfo in subFolders)
        {
          FS_FileInfo fi = new FS_FileInfo(vFolder, fsInfo.vNode, null, fsInfo.vData, null, null, true);
          fi.ChildCount = fsInfo.ChildCount;
          fsNodes.Add(fi);
        }
      }
      catch { }
      return fsNodes;
    }


    public bool CheckACLs(string area, params FS_ACL_Reduced.AclType[] acls)
    {
      try
      {
        if (this.IsRoot)
          return true;
        var ACL_VFS = FS_Operations.GetUserACLs(this.CurrentUser).FirstOrDefault(r => string.Equals(r.area, area));
        bool match = true;
        foreach (var acl in acls)
          match &= ((ACL_VFS.flags_acl & (int)acl) == (int)acl);
        return match;
      }
      catch { }
      return false;
    }


    //
    // popola una lista di nodi a partire da una lista di nodi espansi
    // nodesIdList contiene una lista di parent (sNode ID) per i quali caricare tutti i child
    // attenzione che questa funzione viene utilizzata solo dall'Author e utilizza le relative roots
    //
    public List<FS_NodeInfo> GetRootNodes(bool ignoreFilters)
    {
      // bisogna ricaricare i nodi perche' e' necessario averli in un context attivo
      List<FS_NodeInfo> roots = this.Get_NodesInfo(IKGD_ConfigVFS.ConfigExt.RootsAuthor_sNodes, null, false).ToList().OrderBy(n => IKGD_ConfigVFS.ConfigExt.RootsAuthor_sNodes.IndexOfSortable(n.sNode)).ThenBy(n => n.Position).ThenBy(n => n.sNode).ToList();
      return roots ?? new List<FS_NodeInfo>();
    }


    public static List<IKGD_ADMIN> GetUserACLs(string userName)
    {
      List<IKGD_ADMIN> result = null;
      using (FS_Operations fsOp = new FS_Operations())
      {
        fsOp.EnsureOpenConnection();
        bool useRealTransactions = Utility.TryParse<bool>(IKGD_Config.AppSettings["IKGD_ConfigVFS_TransactionsEnabled"], false);
        using (System.Transactions.TransactionScope ts = useRealTransactions ? IKGD_TransactionFactory.TransactionReadUncommitted(600) : IKGD_TransactionFactory.TransactionNone(600))
        {
          result = fsOp.DB.IKGD_ADMINs.Where(r => r.username == userName).ToList();
          ts.Committ(); // serve solo per non lasciare un transaction incompleta che incasinerebbe le transaction di livello superiore
        }
      }
      return result;
    }


    public bool IsAreaPresentOnVFS(string area, bool activeOnly)
    {
      bool res = false;
      if (activeOnly)
        res |= this.NodesActive<IKGD_VDATA>().Any(n => n.area == area);
      else
        res |= DB.IKGD_VDATAs.Any(n => n.area == area);
      return res;
    }


    public int UpdateAreaOnVFS(string areaOld, string areaNew)
    {
      try
      {
        using (TransactionScope ts = IKGD_TransactionFactory.Transaction(null))
        {
          areaOld = areaOld ?? string.Empty;
          areaNew = areaNew ?? string.Empty;
          int affectedNodes = DB.ExecuteCommand("UPDATE [IKGD_VDATA] SET [role]={1} WHERE ([role]={0})", areaOld, areaNew);
          int affectedAdmin = 0;
          if (string.IsNullOrEmpty(areaNew))
          {
            affectedAdmin += DB.ExecuteCommand("DELETE FROM [IKGD_ADMIN] WHERE ([role]={0})", areaOld);
          }
          else
          {
            // devo cancellare i record che creerebbero conflitti con la primary key
            affectedAdmin += DB.ExecuteCommand("DELETE FROM [IKGD_ADMIN] WHERE ([role]={0})", areaNew);
            affectedAdmin += DB.ExecuteCommand("UPDATE [IKGD_ADMIN] SET [role]={1} WHERE ([role]={0})", areaOld, areaNew);
          }
          //
          ts.Committ();
          //
          return affectedNodes;
        }
      }
      catch { }
      return 0;
    }


    /////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    //
    //       LOCK methods
    //
    /////////////////////////////////////////////////////////////////////////////////////////////////////////////////

    //
    // TODO: per il lock devo avere write access al nodo
    //

    //
    // funzione per eseguire il locking di un set di nodi all'interno di una propria transaction
    //
    public List<IKGD_RNODE> LockNodesR(List<int> rNodeCodes) { return LockNodes(this.DB.IKGD_RNODEs.Where(r => rNodeCodes.Contains(r.code))); }
    public List<IKGD_RNODE> LockNodes(List<int> sNodeCodes, bool recursive) { return LockNodes(FilterVNodesByVersion(DB.IKGD_VNODEs, true).Where(vn => sNodeCodes.Contains(vn.snode)), recursive); }
    public List<IKGD_RNODE> LockNodes(IEnumerable<IKGD_VNODE> vNodes, bool recursive)
    {
      List<IKGD_RNODE> rNodesLocked = new List<IKGD_RNODE>();
      if (vNodes == null)
        return rNodesLocked;
      try
      {
        using (TransactionScope ts = IKGD_TransactionFactory.Transaction(null))
        {
          foreach (IKGD_VNODE vNode in vNodes)
          {
            foreach (IKGD_VNODE n in IKGD_VNODEsRecurse(vNode, null, true, false, recursive ? int.MaxValue : 0, true))
            {
              IKGD_RNODE rNode = n.IKGD_RNODE;
              if (rNode.locked.HasValue && (!IsRoot && rNode.locked_by != CurrentUser))
                return null;
              if (!rNode.locked.HasValue)
              {
                rNode.locked = DateTime.Now;
                rNode.locked_by = CurrentUser;
              }
              rNodesLocked.Add(rNode);
            }
          }
          // provo a salvare i cambiamenti, in caso di variazioni sulle timestamp dovrebbe generare un errore catturato dal catch
          DB.SubmitChanges();
          //
          ts.Committ();
        }
        return rNodesLocked;
      }
      catch { }
      return null;
    }
    public List<IKGD_RNODE> LockNodes(IEnumerable<IKGD_RNODE> rNodes)
    {
      List<IKGD_RNODE> rNodesLocked = new List<IKGD_RNODE>();
      if (rNodes == null)
        return rNodesLocked;
      try
      {
        using (TransactionScope ts = IKGD_TransactionFactory.Transaction(null))
        {
          foreach (IKGD_RNODE rNode in rNodes.Where(n => n != null))
          {
            if (rNode.locked.HasValue && (!IsRoot && rNode.locked_by != CurrentUser))
              return null;
            if (!rNode.locked.HasValue)
            {
              rNode.locked = DateTime.Now;
              rNode.locked_by = CurrentUser;
            }
            rNodesLocked.Add(rNode);
          }
          // provo a salvare i cambiamenti, in caso di variazioni sulle timestamp dovrebbe generare un errore catturato dal catch
          DB.SubmitChanges();
          //
          ts.Committ();
        }
        return rNodesLocked;
      }
      catch { }
      return null;
    }

    //
    // metodo di unLock semplificato da eseguire sul valore di ritorno di LockNodes
    //
    public IEnumerable<IKGD_RNODE> UnLockNodes(IEnumerable<IKGD_RNODE> rNodes)
    {
      try
      {
        using (TransactionScope ts = IKGD_TransactionFactory.Transaction(null))
        {
          foreach (IKGD_RNODE rNode in rNodes.Where(n => n.locked.HasValue))
          {
            rNode.locked = null;
            rNode.locked_by = string.Empty;
          }
          // provo a salvare i cambiamenti, in caso di variazioni sulle timestamp fuori dal context dovrebbe generare un errore catturato dal catch
          DB.SubmitChanges();
          //
          ts.Committ();
        }
      }
      catch
      {
        return null;
      }
      return rNodes;
    }

    //
    // metodo di unLock completo da eseguire dalla UI
    //
    public List<IKGD_RNODE> UnLockNodes(List<int> sNodeCodes, bool recursive) { return UnLockNodes(FilterVNodesByVersion(DB.IKGD_VNODEs, true).Where(vn => sNodeCodes.Contains(vn.snode)), recursive); }
    public List<IKGD_RNODE> UnLockNodes(IEnumerable<IKGD_VNODE> vNodes, bool recursive)
    {
      List<IKGD_RNODE> rNodesUnLocked = new List<IKGD_RNODE>();
      List<IKGD_VNODE> vNodesLocked = new List<IKGD_VNODE>();
      if (vNodes == null)
        return rNodesUnLocked;
      bool IsPseudoRoot = IsRoot || MembershipHelper.HasMembershipACL();
      try
      {
        using (TransactionScope ts = IKGD_TransactionFactory.Transaction(null))
        {
          foreach (IKGD_VNODE vNode in vNodes)
          {
            foreach (IKGD_VNODE n in IKGD_VNODEsRecurse(vNode, null, true, false, recursive ? int.MaxValue : 0, true))
            {
              IKGD_RNODE rNode = n.IKGD_RNODE;
              if (!rNode.locked.HasValue)
                continue;
              if (!IsPseudoRoot && rNode.locked_by != CurrentUser)
              {
                vNodesLocked.Add(n);
                continue;
              }
              rNode.locked = null;
              rNode.locked_by = string.Empty;
              rNodesUnLocked.Add(rNode);
            }
          }
          // provo a salvare i cambiamenti, in caso di variazioni sulle timestamp dovrebbe generare un errore catturato dal catch
          DB.SubmitChanges();
          //
          ts.Committ();
        }
        int numLockedRemaining = vNodesLocked.Count;
        return rNodesUnLocked;
      }
      catch { }
      return null;
    }

    //
    // metodo per la ricerca di nodi lockati
    //
    public List<IKGD_VNODE> FindLockedNodes(IEnumerable<IKGD_VNODE> vNodes, bool recursive)
    {
      List<IKGD_VNODE> vNodesLocked = new List<IKGD_VNODE>();
      try
      {
        foreach (IKGD_VNODE vNode in vNodes)
          foreach (IKGD_VNODE n in IKGD_VNODEsRecurse(vNode, null, true, false, recursive ? int.MaxValue : 0, true))
            if (n.IKGD_RNODE.locked.HasValue)
              vNodesLocked.Add(n);
      }
      catch { }
      return vNodesLocked;
    }




    /////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    //
    //       COW methods
    //
    /////////////////////////////////////////////////////////////////////////////////////////////////////////////////


    //
    // rivedere lo schema di clonazione utilizzando serialization/deserialization
    //
    public static T CloneNodeLINQ<T>(T entity)
    {
      // in alternativa testare anche
      //return entity.DeepClone();
      var dcs = new System.Runtime.Serialization.DataContractSerializer(typeof(T));
      using (var ms = new System.IO.MemoryStream())
      {
        dcs.WriteObject(ms, entity);
        ms.Seek(0, System.IO.SeekOrigin.Begin);
        return (T)dcs.ReadObject(ms);
      }
    }

    //
    // clonazione di un nodo con il settaggio di tutti i campi standard per un nuovo nodo preview e pulizia di current sull'originale
    //
    public T CloneNode<T>(T entity, bool copyForeignKeys, bool clearOrig) where T : class, new() { return CloneNode(entity, copyForeignKeys, clearOrig, CurrentUser); }
    public T CloneNode<T>(T entity, bool copyForeignKeys, bool clearOrig, string username) where T : class, new()
    {
      T clonedEntity = new T();
      Type ty = typeof(T);
      if (entity != null)
      {
        IEnumerable<PropertyInfo> props = ty.GetProperties(BindingFlags.Public | BindingFlags.Instance);
        if (copyForeignKeys == false)
        {
          List<string> forbiddenProps = new List<string>();
          foreach (PropertyInfo prop in props)
            foreach (AssociationAttribute aa in prop.GetCustomAttributes(typeof(AssociationAttribute), false))
              if (aa.IsForeignKey && !string.IsNullOrEmpty(aa.ThisKey))
                forbiddenProps.Add(aa.ThisKey);
          props = props.Where(p => !forbiddenProps.Contains(p.Name));
        }
        //
        props = props.Where(prop => prop.GetCustomAttributes(typeof(ColumnAttribute), false).OfType<ColumnAttribute>().Count(ca => ca.IsDbGenerated) == 0);
        props = props.Where(prop => prop.GetCustomAttributes(typeof(ColumnAttribute), false).Count() > 0);
        foreach (PropertyInfo prop in props)
          prop.SetValue(clonedEntity, prop.GetValue(entity, null), null);
        //
        // per clonare anche le EntityRef (es. vNode.IKGD_SNODE)
        //
        if (copyForeignKeys)
        {
          //FieldInfo[] fields = ty.GetFields(BindingFlags.NonPublic | BindingFlags.Instance);
          //foreach (FieldInfo fi in fields.Where(f => f.FieldType.ToString().Contains("EntityRef")))
          //  fi.SetValue(clonedEntity, fi.GetValue(entity));
        }
        //
        if (clearOrig)
          Utility.SetPropertySafe<bool>(entity, "flag_current", false);
      }
      else
      {
        // filtraggio sulle date che non vengono gestite bene da SQL
        DateTime dMin = IKGD_DBH.DateTimeMinValue;
        DateTime dMax = IKGD_DBH.DateTimeMaxValue;
        IEnumerable<PropertyInfo> props = ty.GetProperties(BindingFlags.Public | BindingFlags.Instance);
        //
        foreach (PropertyInfo pi in props.Where(p => (Nullable.GetUnderlyingType(p.PropertyType) ?? p.PropertyType) == typeof(DateTime)))
        {
          DateTime? dt0 = (DateTime?)pi.GetValue(clonedEntity, null);
          if (dt0 != null)
          {
            DateTime dt1 = dt0.Value >= dMin ? dt0.Value : dMin;
            DateTime dt2 = dt1 <= dMax ? dt1 : dMax;
            if (dt0.Value != dt2)
              pi.SetValue(clonedEntity, dt2, null);
          }
        }
      }
      Utility.SetPropertySafe<bool>(clonedEntity, "flag_published", false);
      Utility.SetPropertySafe<bool>(clonedEntity, "flag_current", true);
      Utility.SetPropertySafe<bool>(clonedEntity, "flag_deleted", false);
      Utility.SetPropertySafe<int?>(clonedEntity, "version_frozen", null);
      Utility.SetPropertySafe<DateTime>(clonedEntity, "version_date", DateTimeContext);
      Utility.SetPropertySafe<string>(clonedEntity, "username", username);
      return clonedEntity;
    }

    //
    // rename di un nodo (non puo' operare su piu' nodi contemporaneamente)
    //
    public IKGD_VNODE COW_Rename(int sNodeCode, string newName)
    {
      try
      {
        IKGD_VNODE vNodeMaster = this.NodeActive(sNodeCode);
        if (vNodeMaster == null)
          return null;
        if (vNodeMaster.IKGD_RNODE.locked.HasValue && (!IsRoot && vNodeMaster.IKGD_RNODE.locked_by != CurrentUser))
          return null;
        //
        // acquisizione del lock
        //
        List<IKGD_RNODE> lockedNodes = LockNodes(new List<IKGD_VNODE> { vNodeMaster }, false);
        if (lockedNodes == null)
          throw new Exception("Impossibile acquisire l'esclusiva sulle operazioni per le risorse selezionate");

        IKGD_VNODE vNode_dup = null;
        using (TransactionScope ts = IKGD_TransactionFactory.Transaction(null))
        {
          vNode_dup = CloneNode(vNodeMaster, true, true);
          vNode_dup.name = Utility.StringTruncate(newName, MaxNameLen);
          DB.IKGD_VNODEs.InsertOnSubmit(vNode_dup);
          //
          OpHandlerCOW(OpHandlerCOW_OperationEnum.Update, OpHandlerCOW_DeserializeVfsInfoEnum.None);
          //DB.SubmitChanges();
          //
          ts.Committ();
        }
        //
        // release del lock
        //
        if (UnLockNodes(lockedNodes) == null)
          throw new Exception("Errore: impossibile sbloccare le risorse modificate");
        //
        return vNode_dup;
      }
      catch { }
      return null;
    }


    //
    // delete (unlink) di nodi / folder con procedura ricorsiva
    //
    public List<IKGD_VNODE> COW_Unlink(List<int> sNodeCodes, bool recursive) { return COW_Unlink(sNodeCodes, recursive, false); }
    public List<IKGD_VNODE> COW_Unlink(List<int> sNodeCodes, bool recursive, bool NoCOW)
    {
      List<IKGD_VNODE> vNodes_OUT = null;
      try
      {
        //
        // devo prevedere dei filtri piu' restrittivi sui ruoli (pubblicazione o pubblicazione immediata)
        //
        int acls = (int)(FS_ACL_Reduced.AclType.Write | FS_ACL_Reduced.AclType.Delete);
        List<string> activeAreas = IsRoot ? CurrentAreasExtended.AreasAllowed : DB.IKGD_ADMINs.Where(r => r.username == CurrentUser && ((r.flags_acl & acls) != 0)).Select(r => r.area).ToList();
        //
        Expression<Func<IKGD_VDATA, bool>> vDataFilterAll = PredicateBuilder.True<IKGD_VDATA>();
        vDataFilterAll = vDataFilterAll.And(n => activeAreas.Contains(n.area));
        Expression<Func<IKGD_VDATA, bool>> vDataFilterAll2 = PredicateBuilder.True<IKGD_VDATA>();
        vDataFilterAll2 = vDataFilterAll2.And(n => !activeAreas.Contains(n.area));
        //
        //Expression<Func<IKGD_VNODE, bool>> vNodeFilterAll = PredicateBuilder.True<IKGD_VNODE>();
        //
        List<FS_NodeInfo> fsNodes =
          (from vNode in this.NodesActive<IKGD_VNODE>().Where(n => sNodeCodes.Contains(n.snode))
           from vData in this.NodesActive<IKGD_VDATA>().Where(vDataFilterAll).Where(n => n.rnode == vNode.rnode)
           select new FS_NodeInfo { vNode = vNode, vData = vData }).ToList();
        if (fsNodes == null || fsNodes.Count == 0)
          return vNodes_OUT;
        //
        // acquisizione del lock
        //
        List<IKGD_RNODE> lockedNodes = LockNodes(fsNodes.Select(n => n.vNode), recursive);
        if (lockedNodes == null)
          throw new Exception("Impossibile acquisire l'esclusiva sulle operazioni per le risorse selezionate");
        //
        // transazioni per eseguire l'operazione
        //
        using (TransactionScope ts = IKGD_TransactionFactory.Transaction(Utility.TryParse<int>(IKGD_Config.AppSettings["VFS_TimeoutDB"], 300)))
        {
          vNodes_OUT = new List<IKGD_VNODE>();
          //
          // lista dei subfolders accessibili del set specificato
          //
          List<int> snodes = fsNodes.Select(n => n.vNode.snode).ToList();
          List<IKGD_VNODE> foldersSingleton =
            (from vNode in this.NodesActive<IKGD_VNODE>().Where(n => n.flag_folder && sNodeCodes.Contains(n.snode))
             from vData in this.NodesActive<IKGD_VDATA>().Where(vDataFilterAll).Where(n => n.rnode == vNode.rnode)
             where this.NodesActive<IKGD_VNODE>().Where(n => n.rnode == vNode.rnode).Count() <= 1
             select vNode).ToList();
          List<int> folders = foldersSingleton.Select(n => n.folder).ToList();
          for (List<IKGD_VNODE> lastF = foldersSingleton; lastF != null && lastF.Count > 0; )
          {
            lastF =
              (from vNode in this.NodesActive<IKGD_VNODE>().Where(n => n.flag_folder)
               from vData in this.NodesActive<IKGD_VDATA>().Where(vDataFilterAll).Where(n => n.rnode == vNode.rnode)
               where lastF.Select(n => n.folder).Contains(vNode.parent.Value) && this.NodesActive<IKGD_VNODE>().Where(n => n.rnode == vNode.rnode).Count() <= 1
               select vNode).ToList();
            if (lastF != null)
            {
              folders.AddRange(lastF.Select(n => n.folder));
              snodes.AddRange(lastF.Select(n => n.snode));
            }
          }
          folders = folders.Distinct().ToList();
          snodes = snodes.Distinct().ToList();
          //
          // controllo che tutti i files nei folders da cancellare (no unlink) siano cancellabili
          //
          int undeletables =
            (from vNode in this.NodesActive<IKGD_VNODE>().Where(n => !n.flag_folder && folders.Contains(n.folder))
             from vData in this.NodesActive<IKGD_VDATA>().Where(vDataFilterAll2).Where(n => n.rnode == vNode.rnode)
             select vNode).Count();
          if (undeletables > 0)
            throw new Exception("Tentativo di cancellazione di risorse per le quali non si dispone dei requisiti necessari.");
          //
          // cancellazione di tutti i files nei folders da cancellare
          //
          var unlinkableFiles =
            from vNode in this.NodesActive<IKGD_VNODE>().Where(n => !n.flag_folder && folders.Contains(n.folder))
            from vData in this.NodesActive<IKGD_VDATA>().Where(vDataFilterAll).Where(n => n.rnode == vNode.rnode)
            select vNode;
          foreach (IKGD_VNODE n in unlinkableFiles)
          {
            if (NoCOW)
            {
              n.flag_published = false;
              if (n.flag_current && !n.flag_deleted)
                n.flag_deleted = true;
            }
            else
            {
              IKGD_VNODE vNode_dup = CloneNode(n, true, true);
              vNode_dup.flag_deleted = true;
              vNodes_OUT.Add(vNode_dup);
              DB.IKGD_VNODEs.InsertOnSubmit(vNode_dup);
            }
          }
          //
          // cancellazione dei folders e Files selezionati
          //
          var unlinkableFoldersOrFiles = this.NodesActive<IKGD_VNODE>(false).Where(n => snodes.Contains(n.snode));
          foreach (IKGD_VNODE n in unlinkableFoldersOrFiles)
          {
            if (NoCOW)
            {
              n.flag_published = false;
              if (n.flag_current && !n.flag_deleted)
                n.flag_deleted = true;
            }
            else
            {
              IKGD_VNODE vNode_dup = CloneNode(n, true, true);
              vNode_dup.flag_deleted = true;
              vNodes_OUT.Add(vNode_dup);
              DB.IKGD_VNODEs.InsertOnSubmit(vNode_dup);
            }
          }
          //
          //var chg01 = DB.GetChangeSet();
          DB.SubmitChanges();
          //
          var rNodes = vNodes_OUT.Select(n => n.rnode).Distinct().ToList();
          var unlinkable_vDatas =
            from vData in this.NodesActive<IKGD_VDATA>().Where(n => rNodes.Contains(n.rnode))
            join vn in this.NodesActive<IKGD_VNODE>() on vData.rnode equals vn.rnode into vNodes
            where !vNodes.Any()
            select vData;
          var unlinkable_iNodes =
            from iNode in this.NodesActive<IKGD_INODE>().Where(n => rNodes.Contains(n.rnode))
            join vn in this.NodesActive<IKGD_VNODE>() on iNode.rnode equals vn.rnode into vNodes
            where !vNodes.Any()
            select iNode;
          foreach (IKGD_VDATA n in unlinkable_vDatas)
          {
            if (NoCOW)
            {
              n.flag_published = false;
              if (n.flag_current && !n.flag_deleted)
                n.flag_deleted = true;
            }
            else
            {
              IKGD_VDATA node_dup = CloneNode(n, true, true);
              node_dup.flag_deleted = true;
              DB.IKGD_VDATAs.InsertOnSubmit(node_dup);
            }
          }
          foreach (IKGD_INODE n in unlinkable_iNodes)
          {
            if (NoCOW)
            {
              n.flag_published = false;
              if (n.flag_current && !n.flag_deleted)
                n.flag_deleted = true;
            }
            else
            {
              IKGD_INODE node_dup = CloneNode(n, true, true);
              node_dup.flag_deleted = true;
              DB.IKGD_INODEs.InsertOnSubmit(node_dup);
            }
          }
          //
          OpHandlerCOW(OpHandlerCOW_OperationEnum.Unlink, OpHandlerCOW_DeserializeVfsInfoEnum.Rebind, vNodes_OUT, unlinkable_vDatas, unlinkable_iNodes);
          //var chg02 = DB.GetChangeSet();
          //DB.SubmitChanges();
          //
          ts.Committ();
        }
        //
        // release del lock
        //
        if (UnLockNodes(lockedNodes) == null)
          throw new Exception("Errore: impossibile sbloccare le risorse modificate");
        //
        return vNodes_OUT;
      }
      catch { }
      return null;
    }


    //
    // undelete di nodi / folder con procedura anche ricorsiva (i nodi non cancellati restano inalterati)
    //
    public List<IKGD_VNODE> COW_Undelete(List<int> sNodeCodes, bool recursive)
    {
      List<IKGD_VNODE> vNodes_OUT = null;
      try
      {
        List<IKGD_VNODE> vNodes = FilterVNodesByVersion(DB.IKGD_VNODEs, true).Where(vn => sNodeCodes.Contains(vn.snode)).ToList();
        if (vNodes == null || vNodes.Count == 0)
          return vNodes_OUT;
        //
        // acquisizione del lock
        //
        List<IKGD_RNODE> lockedNodes = LockNodes(vNodes, recursive);
        if (lockedNodes == null)
          throw new Exception("Impossibile acquisire l'esclusiva sulle operazioni per le risorse selezionate");
        //
        // transazioni per eseguire l'operazione
        //
        using (TransactionScope ts = IKGD_TransactionFactory.Transaction(Utility.TryParse<int>(IKGD_Config.AppSettings["VFS_TimeoutDB"], 600)))
        {
          vNodes_OUT = new List<IKGD_VNODE>();
          foreach (IKGD_VNODE vNode in vNodes)
          {
            foreach (IKGD_VNODE n in IKGD_VNODEsRecurse(vNode, null, true, false, recursive ? int.MaxValue : 0, recursive).Where(vn => vn.flag_deleted))
            {
              IKGD_VNODE vNode_dup = CloneNode(n, true, true);
              vNode_dup.flag_deleted = false;
              vNodes_OUT.Add(vNode_dup);
              DB.IKGD_VNODEs.InsertOnSubmit(vNode_dup);
            }
          }
          //
          var chg1 = DB.GetChangeSet();
          DB.SubmitChanges();
          //
          var rNodes = vNodes_OUT.Select(n => n.rnode).Distinct().ToList();
          var unlinked_vDatas = this.NodesActive<IKGD_VDATA>(true).Where(n => n.flag_deleted == true && n.flag_current == true && rNodes.Contains(n.rnode));
          var unlinked_iNodes = this.NodesActive<IKGD_INODE>(true).Where(n => n.flag_deleted && rNodes.Contains(n.rnode));
          //var unlinked_vDatas =
          //  from vData in this.NodesActive<IKGD_VDATA>(true).Where(n => n.flag_deleted && rNodes.Contains(n.rnode))
          //  join vn in this.NodesActive<IKGD_VNODE>() on vData.rnode equals vn.rnode into vNodesU
          //  where !vNodesU.Any()
          //  select vData;
          //var unlinked_iNodes =
          //  from iNode in this.NodesActive<IKGD_INODE>(true).Where(n => n.flag_deleted && rNodes.Contains(n.rnode))
          //  join vn in this.NodesActive<IKGD_VNODE>() on iNode.rnode equals vn.rnode into vNodesU
          //  where !vNodesU.Any()
          //  select iNode;
          foreach (IKGD_VDATA n in unlinked_vDatas)
          {
            IKGD_VDATA node_dup = CloneNode(n, true, true);
            node_dup.flag_deleted = false;
            DB.IKGD_VDATAs.InsertOnSubmit(node_dup);
          }
          foreach (IKGD_INODE n in unlinked_iNodes)
          {
            IKGD_INODE node_dup = CloneNode(n, true, true);
            node_dup.flag_deleted = false;
            DB.IKGD_INODEs.InsertOnSubmit(node_dup);
          }
          //
          OpHandlerCOW(OpHandlerCOW_OperationEnum.Undelete, OpHandlerCOW_DeserializeVfsInfoEnum.Full, vNodes_OUT, unlinked_vDatas, unlinked_iNodes);
          //DB.SubmitChanges();
          //
          ts.Committ();
        }
        //
        // release del lock
        //
        if (UnLockNodes(lockedNodes) == null)
          throw new Exception("Errore: impossibile sbloccare le risorse modificate");
        //
        return vNodes_OUT;
      }
      catch { }
      return null;
    }


    //
    // COW new folder
    // se area == null allora lo copia dal parent
    // se area == "" allora l'accesso e' anonimo
    //
    public IKGD_VNODE COW_NewFolder(int rNodeParent, string folderName, string area) { return COW_NewFolder(rNodeParent, folderName, area, false, false, CreateResourcePosition.Last); }
    public IKGD_VNODE COW_NewFolder(int rNodeParent, string folderName, string area, bool flag_inactive, CreateResourcePosition creatPosition) { return COW_NewFolder(rNodeParent, folderName, area, flag_inactive, false, creatPosition); }
    public IKGD_VNODE COW_NewFolder(int rNodeParent, string folderName, string area, bool flag_inactive, bool flag_published, CreateResourcePosition creatPosition)
    {
      List<IKGD_RNODE> lockedNodes = null;
      try
      {
        //
        // fetch dei nodi interessati
        //
        List<IKGD_VNODE> vNodeParents = this.NodesActive<IKGD_VNODE>().Where(n => n.flag_folder && n.folder == rNodeParent).ToList();
        if (vNodeParents == null || vNodeParents.Count == 0)
          return null;
        IKGD_VNODE vNodeParent = vNodeParents.FirstOrDefault();
        //IKGD_VNODE vNodeParent = this.NodeActive(rNodeParent);
        //
        // acquisizione del lock
        //
        //if ((lockedNodes = LockNodes(new List<IKGD_VNODE> { vNodeParent }, false)) == null)
        //  throw new Exception("Impossibile acquisire l'esclusiva sulle operazioni per le risorse selezionate");
        if ((lockedNodes = LockNodes(vNodeParents, false)) == null)
          throw new Exception("Impossibile acquisire l'esclusiva sulle operazioni per le risorse selezionate");
        //
        // transazioni per eseguire l'operazione
        //
        using (TransactionScope ts = IKGD_TransactionFactory.Transaction(null))
        {
          //
          // posizione di inserimento
          //
          double positionNext = 1;
          try
          {
            if (creatPosition == CreateResourcePosition.Last)
              positionNext = FilterVNodesByVersion(DB.IKGD_VNODEs).Where(vn => (!vn.flag_folder && vn.folder == vNodeParent.folder) || (vn.flag_folder && vn.parent == vNodeParent.folder)).Max(vn => vn.position) + 1;
            else
              positionNext = FilterVNodesByVersion(DB.IKGD_VNODEs).Where(vn => (!vn.flag_folder && vn.folder == vNodeParent.folder) || (vn.flag_folder && vn.parent == vNodeParent.folder)).Min(vn => vn.position) - 1;
          }
          catch { }
          //
          // creazione del nuovo nodo e risorse correlate
          //
          IKGD_RNODE rNode = this.Factory_IKGD_RNODE();
          IKGD_SNODE sNode = this.Factory_IKGD_SNODE();
          IKGD_VNODE vNode = this.Factory_IKGD_VNODE();
          IKGD_VDATA vData = this.Factory_IKGD_VDATA();
          //
          vNode.flag_folder = sNode.flag_folder = rNode.flag_folder = true;
          vNode.name = Utility.StringTruncate(folderName, MaxNameLen);
          vNode.position = positionNext;
          vData.flag_inactive = flag_inactive;
          if (flag_published)
          {
            vNode.flag_published = true;
            vData.flag_published = true;
          }
          //
          // relazioni
          vData.IKGD_RNODE = rNode;
          sNode.IKGD_RNODE = rNode;
          vNode.IKGD_RNODE = rNode;
          vNode.IKGD_SNODE = sNode;
          vNode.IKGD_RNODE_folder = rNode;  // punta a se stesso
          vNode.IKGD_RNODE_parent = vNodeParent.IKGD_RNODE_folder;
          //
          DB.IKGD_RNODEs.InsertOnSubmit(rNode);
          DB.IKGD_SNODEs.InsertOnSubmit(sNode);
          DB.IKGD_VNODEs.InsertOnSubmit(vNode);
          DB.IKGD_VDATAs.InsertOnSubmit(vData);
          //
          // duplicazione delle ACLs dalla cartella contenitore
          //
          if (area == null)
          {
            IKGD_VDATA vDataParent = this.Get_VDATA(vNodeParent);
            if (vDataParent != null)
              vData.area = vDataParent.area;
          }
          else
            vData.area = area;
          //
          OpHandlerCOW(OpHandlerCOW_OperationEnum.Update, OpHandlerCOW_DeserializeVfsInfoEnum.Full);
          //DB.SubmitChanges();
          //
          ts.Committ();
          //
          return vNode;
        }
      }
      catch (Exception ex)
      {
        // rilancia l'eventuale eccezione
        throw ex;
      }
      finally
      {
        //
        // release del lock
        //
        if (lockedNodes != null)
          if (UnLockNodes(lockedNodes) == null)
            throw new Exception("Impossibile sbloccare le risorse acquisite precedentemente");
      }
    }


    //
    // funzionalita' di unalias di un symlink
    // opera su files e folders
    // nel caso di folders permette di selezionare la modalita' di ricorsione
    //  - OpCopyUnAlias_Enum.FoldersWithNoFiles: crea solo un duplicato del folder senza nessun contenuto
    //  - OpCopyUnAlias_Enum.FoldersWithFiles: crea un duplicato del folder e duplica anche i soli files contenuti nel folder
    //  - OpCopyUnAlias_Enum.FoldersWithFilesAndSubFolderAliases: crea duplicato del folder, dei files contenuti e crea dei SYMLINKS dei folders (ricorsione parziali)
    //
    public enum OpCopyUnAlias_Enum { FoldersWithNoFiles, FoldersWithFiles, FoldersWithFilesAndSubFolderAliases }
    public List<IKGD_VNODE> COW_ResourceUnAlias(List<int> sNodes_SRC, OpCopyUnAlias_Enum opUnAlias, bool throwExceprions)
    {
      List<IKGD_VNODE> vNodes_NEW = null;
      List<IKGD_RNODE> lockedNodesSRC = null;
      try
      {
        if ((lockedNodesSRC = LockNodes(sNodes_SRC, false)) == null)
          throw new Exception("Impossibile acquisire l'esclusiva delle operazioni sugli elementi da manipolare");
        //
        // test delle ACLs
        //
        bool acl_OK = true;
        if (!IsRoot)
        {
          try
          {
            //FS_ACL_Reduced acl_dst = new FS_ACL_Reduced(this.DB, fsNode_DST.vData.area);  // visto che non siamo root...
            //acl_OK &= acl_dst.Has_Write;
            var fsNodes_SRC = this.Get_NodesInfo(sNodes_SRC.Distinct(), false, true).ToList();
            foreach (var fsNode in fsNodes_SRC)
            {
              FS_ACL_Reduced acl_src = new FS_ACL_Reduced(this.DB, fsNode.vData.area);
              acl_OK &= acl_src.Has_Write && acl_src.Has_Read;
            }
          }
          catch { acl_OK = false; }
        }
        if (!acl_OK)
          throw new Exception("Non si dispone dei permessi sufficienti per completare l'operazione richiesta.");
        //
        vNodes_NEW = new List<IKGD_VNODE>();
        //
        //
        // transazioni per eseguire le operazioni
        //
        using (TransactionScope ts = IKGD_TransactionFactory.Transaction(Utility.TryParse<int>(IKGD_Config.AppSettings["VFS_TimeoutDB"], 300)))
        {
          List<IKGD_VNODE> vNodes_SRC = FilterVNodesByVersion(DB.IKGD_VNODEs, true).Where(vn => sNodes_SRC.Contains(vn.snode)).OrderBy(n => n.position).ThenBy(n => n.snode).ToList();
          foreach (IKGD_VNODE vNode_SRC in vNodes_SRC)
          {
            if (vNode_SRC.flag_deleted)
              continue;
            //
            IKGD_VNODE vNode_dup = this.COW_ResourceCopyDnDWorkerDUP(vNode_SRC, null, vNode_SRC.position, lockedNodesSRC, true);
            if (vNode_dup != null)
            {
              vNodes_NEW.Add(vNode_dup);
              // per la copia (non symlink) di folder si procede con una ricorsione singola sui soli files e non sui folders
              if (vNode_SRC.flag_folder && opUnAlias != OpCopyUnAlias_Enum.FoldersWithNoFiles)
              {
                double posDup = 0.0;
                if (opUnAlias == OpCopyUnAlias_Enum.FoldersWithFilesAndSubFolderAliases)
                {
                  //
                  List<IKGD_VNODE> vNodes_recurse = this.NodesActive<IKGD_VNODE>().Where(n => n.flag_folder == true && n.parent.Value == vNode_SRC.folder).Join(this.NodesActive<IKGD_VDATA>(), vn => vn.rnode, vd => vd.rnode, (vn, vd) => vn).Distinct().ToList();
                  foreach (IKGD_VNODE vNode_srcR in vNodes_recurse)
                  {
                    IKGD_VNODE vNode_dupR = COW_ResourceCopyDnDWorkerSLK(vNode_srcR, vNode_dup.IKGD_RNODE_folder, ++posDup, lockedNodesSRC, false);
                    if (vNode_dupR != null)
                      vNodes_NEW.Add(vNode_dupR);
                  }
                }
                if (opUnAlias == OpCopyUnAlias_Enum.FoldersWithFiles || opUnAlias == OpCopyUnAlias_Enum.FoldersWithFilesAndSubFolderAliases)
                {
                  List<IKGD_VNODE> vNodes_recurse = this.NodesActive<IKGD_VNODE>().Where(n => n.folder == vNode_SRC.folder && n.flag_folder == false).Join(this.NodesActive<IKGD_VDATA>(), vn => vn.rnode, vd => vd.rnode, (vn, vd) => vn).Distinct().ToList();
                  foreach (IKGD_VNODE vNode_srcR in vNodes_recurse)
                  {
                    IKGD_VNODE vNode_dupR = COW_ResourceCopyDnDWorkerDUP(vNode_srcR, vNode_dup, ++posDup, lockedNodesSRC, false);
                    if (vNode_dupR != null)
                      vNodes_NEW.Add(vNode_dupR);
                  }
                }
              }
            }
            //
          }
          //
          //var chg = DB.GetChangeSet();
          OpHandlerCOW(OpHandlerCOW_OperationEnum.Update, OpHandlerCOW_DeserializeVfsInfoEnum.Full);
          //DB.SubmitChanges();
          //
          ts.Committ();
        }
      }
      catch (Exception ex)
      {
        // rilancia l'eventuale eccezione
        if (throwExceprions)
          throw ex;
      }
      finally
      {
        //
        // release dei locks
        //
        bool unlockRes = true;
        if (lockedNodesSRC != null)
          unlockRes &= (UnLockNodes(lockedNodesSRC) != null);
        if (!unlockRes && throwExceprions)
          throw new Exception("Impossibile sbloccare le risorse acquisite precedentemente");
      }
      return vNodes_NEW;
    }


    //
    // manager generale per le operazioni di Cut/Copy/Paste/SymLink e DnD
    //
    // se createSymLink == true crea un SymLink altrimenti e' equivalente ad un DnD o cut/paste
    // se sNode_HOOK non e' nullo allora attacca le risorse subito dopo sNode_HOOK invece che in coda al folder
    // sNode_DST deve sempre essere un folder entro cui verranno sistemati gli sNodes_SRC
    // il valore di ritorno comprende il vNode di destinazione e di seguito tutti i nuovi nodi generati
    //
    public enum OpCopyDnD_Enum { Move, SymLink, Copy }
    public List<IKGD_VNODE> COW_ResourceCopyDnDWorker(List<int> sNodes_SRC, int sNode_DST, int? sNode_HOOK, OpCopyDnD_Enum opCopyDnD, bool? renameConflicts, bool throwExceprions, bool? attachAtBottom)
    {
      List<IKGD_VNODE> vNodes_NEW = null;
      if (sNodes_SRC == null || sNodes_SRC.Count == 0)
        return vNodes_NEW;
      List<IKGD_RNODE> lockedNodesDST = null;
      List<IKGD_RNODE> lockedNodesSRC = null;
      try
      {
        attachAtBottom = attachAtBottom ?? true;
        //
        // controllo che la destinazione sia valida
        //
        FS_NodeInfo fsNode_DST = this.Get_NodeInfo(sNode_DST, false, true);
        IKGD_VNODE vNode_DST = fsNode_DST.vNode;
        //IKGD_VNODE vNode_DST = this.NodeActive(sNode_DST, true);
        if (vNode_DST == null || !vNode_DST.flag_folder || vNode_DST.flag_deleted)
          throw new Exception("La cartella di destinazione è eliminata o inesistente.");
        //
        // controllo che non siano presenti riferimenti circolari
        // eseguendo uno scan completo dei path di destinazione per risorse su tutte le lingue anche se unrooted
        //
        //List<int> sPath_DST = this.PathsFromNodeAuthor(sNode_DST).SelectMany(p => p.Fragments.Select(f => f.sNode)).ToList();
        List<int> sPath_DST = this.PathsFromNodeAuthorEquiv(sNode_DST, true).SelectMany(p => p.Fragments.Select(f => f.sNode)).ToList();
        if (sNodes_SRC.Intersect(sPath_DST).Count() > 0)
          throw new Exception("Non è possibile copiare delle risorse su cartelle che discendono dalle stesse o con riferimenti ricorsivi.");
        //
        // locking dei sorgenti e della destinazione
        //
        if ((lockedNodesDST = LockNodes(new List<int> { sNode_DST }, false)) == null)
          throw new Exception("Impossibile acquisire l'esclusiva delle operazioni sulla cartella di destinazione");
        if ((lockedNodesSRC = LockNodes(sNodes_SRC, false)) == null)
          throw new Exception("Impossibile acquisire l'esclusiva delle operazioni sugli elementi da manipolare");
        //
        // test delle ACLs
        //
        bool acl_OK = true;
        if (!IsRoot)
        {
          try
          {
            FS_ACL_Reduced acl_dst = new FS_ACL_Reduced(this.DB, fsNode_DST.vData.area);  // visto che non siamo root...
            acl_OK &= acl_dst.Has_Write;
            var fsNodes_SRC = this.Get_NodesInfo(sNodes_SRC.Distinct(), false, true).ToList();
            foreach (var fsNode in fsNodes_SRC)
            {
              FS_ACL_Reduced acl_src = new FS_ACL_Reduced(this.DB, fsNode.vData.area);
              if (fsNode.Folder == fsNode_DST.Folder)
                acl_OK &= acl_dst.Has_DnD_Inside;
              else
                acl_OK &= acl_dst.Has_DnD_Outside && acl_src.Has_DnD_Outside;
            }
          }
          catch { acl_OK = false; }
        }
        if (!acl_OK)
          throw new Exception("Non si dispone dei permessi sufficienti per completare l'operazione richiesta.");
        //
        vNodes_NEW = new List<IKGD_VNODE>() { vNode_DST };
        List<string> conflictingNames = null;
        //
        // transazioni per eseguire le operazioni
        //
        using (TransactionScope ts = IKGD_TransactionFactory.Transaction(Utility.TryParse<int>(IKGD_Config.AppSettings["VFS_TimeoutDB"], 600)))
        {
          //
          // setup delle posizioni di inserimento
          //
          List<IKGD_VNODE> vNodes_SRC = FilterVNodesByVersion(DB.IKGD_VNODEs, true).Where(vn => sNodes_SRC.Contains(vn.snode)).OrderBy(n => n.position).ThenBy(n => n.snode).ToList();
          IKGD_VNODE vNode_HOOK = sNode_HOOK.HasValue ? this.NodeActive(sNode_HOOK.Value, true) : null;
          double positionRng1 = 0;
          double positionRng2 = 1;
          IQueryable<IKGD_VNODE> nodesSet = FilterVNodesByVersion(DB.IKGD_VNODEs, true).Where(vn => (!vn.flag_folder && vn.folder == vNode_DST.folder) || (vn.flag_folder && vn.parent == vNode_DST.folder));
          //
          // lettura anticipata dei possibili conflitti sui nomi (LINQ non riesce ad ottimizzare correttamante il codice e devo usare questo approccio)
          //
          if (renameConflicts == true)
          {
            conflictingNames = new List<string>();
            foreach (IKGD_VNODE vn in vNodes_SRC)
            {
              var possibleDups = nodesSet.Where(n => n.name.StartsWith(vn.name)).ToList();
              conflictingNames.AddRange(possibleDups.Except(vNodes_SRC).Select(n => n.name));
            }
          }
          try
          {
            if (vNode_HOOK != null)
            {
              positionRng1 = vNode_HOOK.position;
              positionRng2 = positionRng1 + 1;  // il prossimo assegnamento puo' generare un'eccezione se si tratta dell'ultimo nodo
              positionRng2 = nodesSet.Where(vn => vn.position > positionRng1).Min(vn => vn.position);
            }
            else
            {
              if (attachAtBottom.Value)
              {
                positionRng1 = nodesSet.Max(vn => vn.position);
                positionRng2 = positionRng1 + (sNodes_SRC.Count + 1);
              }
              else
              {
                positionRng2 = nodesSet.Min(vn => vn.position);
                positionRng1 = positionRng2 - (sNodes_SRC.Count + 1);
              }
            }
          }
          catch { }
          double positionDelta = (positionRng2 - positionRng1) / (sNodes_SRC.Count + 1);
          double positionCurrent = positionRng1;
          foreach (IKGD_VNODE vNode_SRC in vNodes_SRC)
          {
            positionCurrent += positionDelta;
            if (vNode_SRC.flag_deleted)
              continue;
            //
            // controllo che non abbia eseguito un'operazione di drag senza spostamento effettivo oppure un paste sullo stesso forder senza
            // specificare il punto di inserimento
            //if (vNode_SRC.folder == vNode_DST.folder && (vNode_SRC.position == positionRng2 || (vNode_HOOK == null && attachAtBottom.Value)))
            //  continue;
            //
            // controllo che non abbia eseguito un'operazione di drag senza spostamento effettivo
            if (vNode_SRC.folder == vNode_DST.folder && vNode_SRC.position == positionRng2)
              continue;
            //
            if (opCopyDnD == OpCopyDnD_Enum.Move || opCopyDnD == OpCopyDnD_Enum.SymLink)
            {
              IKGD_VNODE vNode_dup = COW_ResourceCopyDnDWorkerSLK(vNode_SRC, vNode_DST.IKGD_RNODE_folder, positionCurrent, lockedNodesDST, opCopyDnD == OpCopyDnD_Enum.Move);
              vNodes_NEW.Add(vNode_dup);
            }
            else if (opCopyDnD == OpCopyDnD_Enum.Copy)
            {
              //
              //copia completa dei contenuti con ricorsione parziale/singola per i folders
              //
              IKGD_VNODE vNode_dup = COW_ResourceCopyDnDWorkerDUP(vNode_SRC, vNode_DST, positionCurrent, lockedNodesDST, false);
              if (vNode_dup != null)
              {
                vNodes_NEW.Add(vNode_dup);
                // per la copia (non symlink) di folder si procede con una ricorsione singola sui soli files e non sui folders
                if (vNode_SRC.flag_folder)
                {
                  double posDup = 0.0;
                  var vNodes_recurse = this.NodesActive<IKGD_VNODE>().Where(n => n.folder == vNode_SRC.folder && n.flag_folder == false).Join(this.NodesActive<IKGD_VDATA>(), vn => vn.rnode, vd => vd.rnode, (vn, vd) => vn).Distinct().ToList();
                  foreach (IKGD_VNODE vNode_srcR in vNodes_recurse)
                  {
                    IKGD_VNODE vNode_dupR = COW_ResourceCopyDnDWorkerDUP(vNode_srcR, vNode_dup, ++posDup, lockedNodesDST, false);
                    if (vNode_dupR != null)
                      vNodes_NEW.Add(vNode_dupR);
                  }
                }
              }
            }
            //
          }  //foreach (IKGD_VNODE vNode_SRC in vNodes_SRC)
          //
          // gestione dei conflitti sui nomi
          //
          if (renameConflicts == true && conflictingNames != null)
          {
            foreach (IKGD_VNODE vNode in vNodes_NEW)
            {
              for (int i = 0; i < int.MaxValue; i++)
              {
                string name = (i > 0) ? string.Format("{0} - Copia#{1}", vNode.name, i) : vNode.name;
                if (conflictingNames.Contains(name, StringComparer.InvariantCultureIgnoreCase))
                  continue;
                vNode.name = Utility.StringTruncate(name, MaxNameLen);
                conflictingNames.Add(vNode.name);
                break;
              }
            }
          }
          //
          //var chg = DB.GetChangeSet();
          OpHandlerCOW(OpHandlerCOW_OperationEnum.Update, (opCopyDnD == OpCopyDnD_Enum.Copy) ? OpHandlerCOW_DeserializeVfsInfoEnum.Full : OpHandlerCOW_DeserializeVfsInfoEnum.None);
          //DB.SubmitChanges();
          //
          ts.Committ();
        }
      }
      catch (Exception ex)
      {
        // rilancia l'eventuale eccezione
        if (throwExceprions)
          throw ex;
      }
      finally
      {
        //
        // release dei locks
        //
        bool unlockRes = true;
        if (lockedNodesDST != null)
          unlockRes &= (UnLockNodes(lockedNodesDST) != null);
        if (lockedNodesSRC != null)
          unlockRes &= (UnLockNodes(lockedNodesSRC) != null);
        if (!unlockRes && throwExceprions)
          throw new Exception("Impossibile sbloccare le risorse acquisite precedentemente");
      }
      return vNodes_NEW;
    }


    //
    // metodo ausiliario per COW_ResourceCopyDnDWorker per la duplicazione (non symlink di una risorsa)
    //
    public IKGD_VNODE COW_ResourceCopyDnDWorkerDUP(IKGD_VNODE vNodeSrc, IKGD_VNODE vDestFolder, double position, List<IKGD_RNODE> lockedNodes, bool unAlias)
    {
      if (vNodeSrc == null)
        return null;
      try
      {
        if (unAlias == true && vDestFolder == null)
        {
          if (vNodeSrc.flag_folder == false)
          {
            vDestFolder = this.NodesActive<IKGD_VNODE>().FirstOrDefault(n => n.flag_folder && n.rnode == vNodeSrc.folder);
          }
          else
          {
            vDestFolder = this.NodesActive<IKGD_VNODE>().FirstOrDefault(n => n.flag_folder && n.rnode == vNodeSrc.parent.Value);
          }
        }
        if (unAlias == false && (vDestFolder == null || !vDestFolder.flag_folder))
          return null;
        //
        // creazione del nuovo nodo e risorse correlate
        //
        IKGD_RNODE rNode = CloneNode(vNodeSrc.IKGD_RNODE, false, false);
        IKGD_SNODE sNode = CloneNode(vNodeSrc.IKGD_SNODE, false, false);
        IKGD_VNODE vNode = CloneNode(vNodeSrc, false, unAlias);
        IKGD_VDATA vData = CloneNode(this.Get_VDATA(vNodeSrc), false, false);  // NB duplica anche le ACL del nodo SRC
        sNode.date_creat = rNode.date_creat = DateTimeContext;
        vData.flag_inactive = false;
        vNode.position = position;
        // il nodo e' stato creato locked
        if (lockedNodes != null)
          lockedNodes.Add(rNode);
        // relazioni
        vData.IKGD_RNODE = rNode;
        sNode.IKGD_RNODE = rNode;
        vNode.IKGD_RNODE = rNode;
        vNode.IKGD_SNODE = sNode;
        vNode.IKGD_RNODE_folder = vNode.flag_folder ? rNode : vDestFolder.IKGD_RNODE;
        vNode.IKGD_RNODE_parent = vNode.flag_folder ? vDestFolder.IKGD_RNODE : null;
        //
        DB.IKGD_RNODEs.InsertOnSubmit(rNode);
        DB.IKGD_SNODEs.InsertOnSubmit(sNode);
        DB.IKGD_VNODEs.InsertOnSubmit(vNode);
        DB.IKGD_VDATAs.InsertOnSubmit(vData);
        //
        // duplicazione di iNode, streams e mstreams
        //
        IKGD_INODE iNodeOrig = this.Get_INODE(vNodeSrc);
        if (iNodeOrig != null)
        {
          IKGD_INODE iNode = CloneNode(iNodeOrig, false, false);
          iNode.IKGD_RNODE = rNode;
          DB.IKGD_INODEs.InsertOnSubmit(iNode);
          //
          bool has_mstreams = iNodeOrig.IKGD_MSTREAMs.Any();
          bool is_mstreams = iNodeOrig.IKGD_MSTREAMs.Select(m => m.stream).Except(iNodeOrig.IKGD_STREAMs.Select(s => s.id)).Any();
          if (is_mstreams)
          {
            // se si tratta di una risorsa multistream con effettivamente piu' stream sorgenti la copia si preoccupa
            // di trattarla semplicemnte come multistream senza curarsi della duplicazione in modalita' normale
            // infatti l'inode non risultera' avere degli stream cio' e' ininfluente in un contesto COW ottimizzato
            // tuttavia il VFS non e' in grado di distinguere le risorse multstream da quelle normali nel caso queste abbiano
            // un solo stream sorgente utilizzato (is_mstreams == false) in tal caso passa alla gestione normale con
            // la duplicazione degli streams (e anche dei mapping mstreams)
            // questo e' necessario perche' duplicando inode si e' costretti a duplicare anche i relativi streams
            iNode.IKGD_MSTREAMs.AddRange(iNodeOrig.IKGD_MSTREAMs.Select(m => new IKGD_MSTREAM { IKGD_STREAM = m.IKGD_STREAM }));
          }
          else
          {
            foreach (IKGD_STREAM strm in iNodeOrig.IKGD_STREAMs)
            {
              IKGD_STREAM strmNew = CloneNode(strm, false, false);
              //
              try
              {
                string extMode = IKGD_ExternalVFS_Support.GetExternalModeFromMime(strmNew.type);
                if (IKGD_ExternalVFS_Support.MimePrefixExternalRxCheck.IsMatch(extMode ?? string.Empty))
                {
                  // per la gestione delle risorse su external storage abbiamo bisogno del valore di rNode per cui dobbiamo committare tutto per procedere oltre
                  if (iNode.rnode == 0)
                  {
                    var chg = DB.GetChangeSet();
                    DB.SubmitChanges();
                  }
                  // bisogna gestire correttamente la duplicazione della risorsa nel caso di external files con e senza versioning
                  using (IKGD_ExternalVFS_Support extFS = new IKGD_ExternalVFS_Support())
                  {
                    bool res = extFS.DupExternalFile(extMode, iNode, strmNew);
                  }
                }
              }
              catch { }
              //
              iNode.IKGD_STREAMs.Add(strmNew);  // attenzione che sono dentro un loop, non posso usare InsertOnSubmit
              if (has_mstreams)
                iNode.IKGD_MSTREAMs.Add(new IKGD_MSTREAM { IKGD_STREAM = strmNew });
            }
          }
        }
        //
        // duplicazione delle relations (non serve copiare le published non preview)
        //
        foreach (IKGD_RELATION rel in this.Get_RELATIONs(vNodeSrc))
        {
          IKGD_RELATION relNew = CloneNode(rel, false, false);
          relNew.IKGD_RNODE = rNode;
          relNew.IKGD_RNODE_dst = rel.IKGD_RNODE_dst;
          relNew.IKGD_SNODE_dst = rel.IKGD_SNODE_dst;
          //sNode.IKGD_RELATIONs_src.Add(relNew);  // attenzione che sono dentro un loop, non posso usare InsertOnSubmit
          relNew.IKGD_SNODE_src = sNode;
        }
        //
        // duplicazione delle propeerties (non serve copiare le published non preview)
        //
        foreach (IKGD_PROPERTY prp in this.Get_PROPERTies(vNodeSrc))
        {
          IKGD_PROPERTY prpNew = CloneNode(prp, false, false);
          prpNew.attributeId = prp.attributeId;
          rNode.IKGD_PROPERTies.Add(prpNew);  // attenzione che sono dentro un loop, non posso usare InsertOnSubmit
          //prpNew.IKGD_RNODE = rNode;
          //DB.IKGD_PROPERTies.InsertOnSubmit(prpNew);
        }
        //
        return vNode;
      }
      catch (Exception ex)
      {
        // rilancia l'eventuale eccezione
        throw ex;
        //return null;
      }
    }


    //
    // metodo ausiliario per COW_ResourceCopyDnDWorker e COW_ResourceUnAlias per la creazione di symlink o spostamento di una risorsa
    // rNode_ReferenceParenting deve essere un riferimento all'entity e non un int perchè a volte il valore non è ancora stato salvato e vale 0, meglio attaccrlo all'entity
    //
    public IKGD_VNODE COW_ResourceCopyDnDWorkerSLK(IKGD_VNODE vNode_SRC, IKGD_RNODE rNode_ReferenceParenting, double position, List<IKGD_RNODE> lockedNodes, bool moveOp)
    {
      if (vNode_SRC == null)
        return null;
      try
      {
        IKGD_VNODE vNode_dup = CloneNode(vNode_SRC, true, moveOp);
        if (moveOp == false)
        {
          IKGD_SNODE sNode = Utility.CloneEntity(vNode_SRC.IKGD_SNODE, false, false);
          sNode.IKGD_RNODE = vNode_SRC.IKGD_RNODE;
          sNode.date_creat = DateTimeContext;
          sNode.username = CurrentUser;
          DB.IKGD_SNODEs.InsertOnSubmit(sNode);
          // link al duplicato
          vNode_dup.IKGD_SNODE = sNode;
          //
          // duplicazione delle relations (non serve copiare le published non preview)
          //
          // duplicazione delle relations per i symlinks come per la versione iniziale della intranet (nel nuovo framework porterebbe ad una proliferazione poco controllabile) deve essere attivata esplicitamente
          if (Utility.TryParse<bool>(IKGD_Config.AppSettings["IKGD_DuplicateRelationsForSymLink"], false))
          {
            foreach (IKGD_RELATION rel in this.Get_RELATIONs(vNode_SRC))
            {
              IKGD_RELATION relNew = CloneNode(rel, false, false);
              relNew.IKGD_RNODE = vNode_SRC.IKGD_RNODE;
              relNew.IKGD_RNODE_dst = rel.IKGD_RNODE_dst;
              relNew.IKGD_SNODE_dst = rel.IKGD_SNODE_dst;
              //sNode.IKGD_RELATIONs_src.Add(relNew);  // attenzione che sono dentro un loop, non posso usare InsertOnSubmit
              relNew.IKGD_SNODE_src = sNode;
            }
          }
        }
        if (vNode_dup.flag_folder)
        {
          //vNode_dup.parent = folder_or_parent;
          vNode_dup.IKGD_RNODE_parent = rNode_ReferenceParenting;
        }
        else
        {
          //vNode_dup.folder = folder_or_parent;
          vNode_dup.IKGD_RNODE_folder = rNode_ReferenceParenting;
        }
        vNode_dup.position = position;
        //
        DB.IKGD_VNODEs.InsertOnSubmit(vNode_dup);
        //
        return vNode_dup;
      }
      catch (Exception ex)
      {
        // rilancia l'eventuale eccezione
        throw ex;
        //return null;
      }
    }


    //
    // COW attachment upload
    // NB: sNode_MainFolderOrFile e' il nodo della directory che contiene la cartella con gli allegati (funziona anche se uso il file invece del folder)
    // l'allegato verra' salvato in un subFolder .Upload/fileName
    // sNode_Resource se not null indica la risorsa da aggiornare
    // Attenzione che nel caso sNode_MainFolderOrFile non sia un folder e il file appartenga a piu' folder non e' prevedibile
    // dove verra' sistemato l'attachment
    //
    public IKGD_VNODE COW_AttachmentUpload(int sNode_MainFolderOrFile, int? sNode_Resource, string fileName, string mime, System.IO.Stream fStream)
    {
      fStream.Seek(0, SeekOrigin.Begin);
      // TODO:EXTERNALSTORAGE verificare se vogliamo supportare streams esterni anche per gli allegati automatici (non da subito)
      // utilizzare un setting in web.config che attiva la modalita' solo per files oltre una dimensione fissata
      IKGD_STREAM dataStream = new IKGD_STREAM { key = string.Empty, type = Utility.StringTruncate(mime, MaxKeyLen) };
      int size = (int)fStream.Length;
      byte[] fileData = new byte[size];
      int len = fStream.Read(fileData, 0, size);
      dataStream.data = fileData;  // new System.Data.Linq.Binary(fileData);
      IKGD_VNODE vNode = COW_AttachmentUploadMS(sNode_MainFolderOrFile, sNode_Resource, fileName, mime, new List<IKGD_STREAM> { dataStream });
      return vNode;
    }
    //
    public IKGD_VNODE COW_AttachmentUploadMS(int sNode_MainFolderOrFile, int? sNode_Resource, string fileName, string mime, IEnumerable<IKGD_STREAM> streams)
    {
      List<IKGD_RNODE> lockedNodes = null;
      try
      {
        IKGD_VNODE vMainNode = this.NodeActive(sNode_MainFolderOrFile);
        IKGD_VNODE vMainFolderNode = this.Get_FolderCurrentFallBack(vMainNode);
        if (vMainNode == null || vMainFolderNode == null || streams == null || streams.Count() == 0)
          return null;
        //
        // acquisizione del lock (in questo caso non ha molto senso...)
        //
        //if ((lockedNodes = LockNodes(new List<IKGD_VNODE> { vNode_DST }, false)) == null)
        //  throw new Exception("Impossibile acquisire l'esclusiva sulle operazioni per le risorse selezionate");
        //
        //var docType = new Ikon.IKGD.Library.Resources.IKGD_ResourceTypeDocument();
        //string newManagerType = docType.GetType().Name;
        //bool newIsUnstructured = docType.IsUnstructured;
        string newManagerType = "IKGD_ResourceTypeDocument";
        bool newIsUnstructured = true;
        //
        IKGD_RNODE rNode = null;
        IKGD_SNODE sNode = null;
        IKGD_VNODE vNode = null;
        IKGD_INODE iNode = null;
        IKGD_VDATA vData = null;
        //
        if (!sNode_Resource.HasValue)
        {
          //
          // creazione di un nuovo attachment
          //
          //IKGD_VNODE vNodeUploadFolder = DB.IKGD_VNODEs.FirstOrDefault(vn => vn.flag_folder && vn.parent == vMainFolderNode.folder && vn.name == UploadFolderName);
          IKGD_VNODE vNodeUploadFolder = this.NodesActive<IKGD_VNODE>(false).FirstOrDefault(vn => vn.flag_folder && vn.parent == vMainFolderNode.folder && vn.name == UploadFolderName);
          if (vNodeUploadFolder == null)
          {
            // creazione del vfolder con il flag di browsing a false...
            vNodeUploadFolder = COW_NewFolder(vMainFolderNode.folder, UploadFolderName, null, true, CreateResourcePosition.First);
          }
          //
          // creazione delle nuove entities
          //
          rNode = CloneNode<IKGD_RNODE>(null, false, false);
          sNode = CloneNode<IKGD_SNODE>(null, false, false);
          vNode = CloneNode<IKGD_VNODE>(null, false, false);
          iNode = CloneNode<IKGD_INODE>(null, false, false);
          vData = CloneNode<IKGD_VDATA>(null, false, false);
          //
          vNode.flag_folder = sNode.flag_folder = rNode.flag_folder = false;
          sNode.date_creat = rNode.date_creat = DateTimeContext;
          sNode.IKGD_RNODE = rNode;
          iNode.IKGD_RNODE = rNode;
          vData.IKGD_RNODE = rNode;
          vNode.IKGD_RNODE = rNode;
          vNode.IKGD_SNODE = sNode;
          vNode.IKGD_RNODE_folder = vNodeUploadFolder.IKGD_RNODE;
          vNode.parent = null;  // non si tratta di un folder
          //
          //rivedere (anche in IKGD_EditorMain)
          double positionMax = 0;
          try { positionMax = FilterVNodesByVersion(DB.IKGD_VNODEs).Where(vn => (!vn.flag_folder && vn.folder == vNode.folder) || (vn.flag_folder && vn.parent == vNodeUploadFolder.folder)).Max(vn => vn.position); }
          catch { }
          vNode.position = positionMax + 1;
          //
          DB.IKGD_RNODEs.InsertOnSubmit(rNode);
          DB.IKGD_SNODEs.InsertOnSubmit(sNode);
          DB.IKGD_VNODEs.InsertOnSubmit(vNode);
          DB.IKGD_INODEs.InsertOnSubmit(iNode);
          DB.IKGD_VDATAs.InsertOnSubmit(vData);
          //
          vData.manager_type = newManagerType;
          vData.flag_unstructured = newIsUnstructured;
          vData.flag_autoDeleteOnRels = true;  // gli allegati in directory automatiche vengono creati con il flag di autodelete attivo
          //
          vNode.name = Utility.StringTruncate(fileName, MaxNameLen);
        }
        else
        {
          //
          // modifica di un attachment esistente
          //
          vNode = this.NodeActive(sNode_Resource.Value);
          rNode = vNode.IKGD_RNODE;
          iNode = rNode.IKGD_INODEs.FirstOrDefault(n => n.flag_current && !n.flag_deleted);
          vData = rNode.IKGD_VDATAs.FirstOrDefault(n => n.flag_current && !n.flag_deleted);
          if (vData == null || vData.manager_type != newManagerType || vData.flag_unstructured != newIsUnstructured)
          {
            if (vData == null)
            {
              vData = CloneNode<IKGD_VDATA>(null, false, false);
              vData.IKGD_RNODE = vNode.IKGD_RNODE;
            }
            else
              vData = CloneNode(vData, true, true);
            DB.IKGD_VDATAs.InsertOnSubmit(vData);
          }
          if (iNode == null)
          {
            iNode = CloneNode<IKGD_INODE>(iNode, false, false);
            iNode.IKGD_RNODE = vNode.IKGD_RNODE;
          }
          else
            iNode = CloneNode<IKGD_INODE>(iNode, true, true);
          DB.IKGD_INODEs.InsertOnSubmit(iNode);
        }
        //
        iNode.mime = Utility.StringTruncate(mime, MaxINodeMimeLen);
        iNode.filename = Utility.StringTruncate(fileName, MaxFileNameLen);
        iNode.size = streams.FirstOrDefault().data.Length;
        //
        if (vData.area == null)
          vData.area = this.Get_VDATA(vMainNode).area;
        //
        // aggiungo gli STREAM binari
        //
        foreach (IKGD_STREAM stream in streams)
          if (stream != null)
            iNode.IKGD_STREAMs.Add(stream);
        //
        OpHandlerCOW(OpHandlerCOW_OperationEnum.Update, OpHandlerCOW_DeserializeVfsInfoEnum.Full);
        //var changes = DB.GetChangeSet();
        //DB.SubmitChanges();
        //
        return vNode;
      }
      catch (Exception ex)
      {
        // rilancia l'eventuale eccezione
        throw ex;
      }
      finally
      {
        //
        // release del lock
        //
        if (lockedNodes != null)
          if (UnLockNodes(lockedNodes) == null)
            throw new Exception("Impossibile sbloccare le risorse acquisite precedentemente");
      }
    }


    //
    // creazione di una risorsa su VFS, sNode e rNode saranno creati automaticamente (a meno che rNode non sia gia'
    // definito in vData, nel qual caso crea un nuovo link simbolico
    // non viene specificato IKGD_STREAM in quanto si aggiungono direttamente come FK su iNode
    // iNode, Relations e Properties possono essere null
    //
    public FS_Operations.FS_NodeInfoExt_Interface CreateResourceVFS(string vFolderString, IKGD_VNODE vNode, IKGD_VDATA vData, IKGD_INODE iNode, IEnumerable<IKGD_RELATION> Relations, IEnumerable<IKGD_PROPERTY> Properties)
    {
      try
      {
        IKGD_Path path = this.PathsFromString(vFolderString, true).FirstOrDefault();
        if (path != null && path.IsValid && path.IsRooted)
          return CreateResourceVFS(path.LastFragment.rNode, vNode, vData, iNode, Relations, Properties, null);
      }
      catch { }
      return null;
    }
    public FS_Operations.FS_NodeInfoExt_Interface CreateResourceVFS(int vFolderCWD_rNode, IKGD_VNODE vNode, IKGD_VDATA vData, IKGD_INODE iNode, IEnumerable<IKGD_RELATION> Relations, IEnumerable<IKGD_PROPERTY> Properties) { return CreateResourceVFS(vFolderCWD_rNode, vNode, vData, iNode, Relations, Properties, null); }
    public FS_Operations.FS_NodeInfoExt_Interface CreateResourceVFS(int vFolderCWD_rNode, IKGD_VNODE vNode, IKGD_VDATA vData, IKGD_INODE iNode, IEnumerable<IKGD_RELATION> Relations, IEnumerable<IKGD_PROPERTY> Properties, bool? enableBatchPostProcessing)
    {
      if (vFolderCWD_rNode < 0 || vNode == null || vData == null)
        return null;
      int sNodeNew = -1;
      try
      {
        using (TransactionScope ts = IKGD_TransactionFactory.Transaction(Utility.TryParse<int>(IKGD_Config.AppSettings["VFS_TimeoutDB"], 300)))
        {
          IKGD_VDATA vDataCWD = this.NodesActive<IKGD_VDATA>().Where(n => n.rnode == vFolderCWD_rNode).FirstOrDefault();
          if (vDataCWD == null)
            return null;
          //
          // vData e iNode potrebbero provenire da un nodo esistente o gia' salvato
          bool insert_vNode = (vNode != null && vNode.version == 0);
          bool insert_vData = (vData != null && vData.version == 0);
          bool insert_iNode = (iNode != null && iNode.version == 0);
          //
          if (vData.IKGD_RNODE == null)
          {
            IKGD_RNODE rNode = this.Factory_IKGD_RNODE();
            rNode.flag_folder = vNode.flag_folder;
            vData.IKGD_RNODE = rNode;
            vNode.IKGD_RNODE = rNode;
            if (iNode != null)
              iNode.IKGD_RNODE = rNode;
            if (Relations != null && Relations.Count() > 0)
              Relations.ForEach(r => r.IKGD_RNODE = rNode);
            if (Properties != null && Properties.Count() > 0)
              Properties.ForEach(r => r.IKGD_RNODE = rNode);
            this.DB.IKGD_RNODEs.InsertOnSubmit(rNode);
          }
          if (vNode.IKGD_RNODE == null)
            vNode.IKGD_RNODE = vData.IKGD_RNODE;
          if (iNode != null && iNode.IKGD_RNODE == null)
            iNode.IKGD_RNODE = vData.IKGD_RNODE;
          IKGD_SNODE sNode = this.Factory_IKGD_SNODE();
          sNode.flag_folder = vNode.flag_folder;
          sNode.IKGD_RNODE = vData.IKGD_RNODE ?? vNode.IKGD_RNODE;
          vNode.IKGD_SNODE = sNode;
          vData.area = vData.area ?? vDataCWD.area;
          if (vNode.flag_folder)
          {
            vNode.IKGD_RNODE_folder = vNode.IKGD_RNODE;
            vNode.IKGD_RNODE_parent = vDataCWD.IKGD_RNODE;
          }
          else
          {
            vNode.IKGD_RNODE_folder = vDataCWD.IKGD_RNODE;
            vNode.parent = null;
          }
          if (Relations != null && Relations.Count() > 0)
          {
            Relations.Where(r => r.IKGD_SNODE_src == null).ForEach(r => r.IKGD_SNODE_src = sNode);
            Relations.Where(r => r.IKGD_RNODE == null).ForEach(r => r.IKGD_RNODE = vData.IKGD_RNODE);
          }
          if (Properties != null && Properties.Count() > 0)
          {
            Properties.Where(r => r.IKGD_RNODE == null).ForEach(r => r.IKGD_RNODE = vData.IKGD_RNODE);
          }
          //
          // gestione del posizionamento del nodo
          // vNode.position:  double.MinValue --> first, double.MaxValue --> last
          //
          if (vNode.position != double.MinValue && vNode.position != double.MaxValue)
          {
            vNode.position = this.NodesActive<IKGD_VNODE>().Where(n => n.flag_folder == vNode.flag_folder && ((n.flag_folder == true && n.parent == vNode.parent) || (n.flag_folder == false && n.folder == vNode.folder))).Any(n => n.position == vNode.position) ? double.MaxValue : vNode.position;
          }
          if (vNode.position == double.MinValue)
          {
            try { vNode.position = this.NodesActive<IKGD_VNODE>().Where(n => n.flag_folder == vNode.flag_folder && ((n.flag_folder == true && n.parent == vNode.parent) || (n.flag_folder == false && n.folder == vNode.folder))).Min(n => n.position) - 1.0; }
            catch { vNode.position = 1.0; }
          }
          else if (vNode.position == double.MaxValue)
          {
            try { vNode.position = this.NodesActive<IKGD_VNODE>().Where(n => n.flag_folder == vNode.flag_folder && ((n.flag_folder == true && n.parent == vNode.parent) || (n.flag_folder == false && n.folder == vNode.folder))).Max(n => n.position) + 1.0; }
            catch { vNode.position = 1.0; }
          }
          //
          if (sNode != null)
            this.DB.IKGD_SNODEs.InsertOnSubmit(sNode);
          if (vNode != null && insert_vNode)
            this.DB.IKGD_VNODEs.InsertOnSubmit(vNode);
          if (vData != null && insert_vData)
            this.DB.IKGD_VDATAs.InsertOnSubmit(vData);
          if (iNode != null && insert_iNode)
            this.DB.IKGD_INODEs.InsertOnSubmit(iNode);
          if (Relations != null && Relations.Count() > 0)
            this.DB.IKGD_RELATIONs.InsertAllOnSubmit(Relations.Where(r => r.version == 0));  // evitiamo di inserire duplicati
          if (Properties != null && Properties.Count() > 0)
            this.DB.IKGD_PROPERTies.InsertAllOnSubmit(Properties.Where(r => r.version == 0));  // evitiamo di inserire duplicati
          //
          // non viene utilizzata dal backend, quindi non aggiorniamo la queue dei nodi modificati
          //
          if (enableBatchPostProcessing.GetValueOrDefault(true))
          {
            OpHandlerCOW(OpHandlerCOW_OperationEnum.Custom, OpHandlerCOW_DeserializeVfsInfoEnum.Full);
          }
          else
          {
            var chg = DB.GetChangeSet();
            DB.SubmitChanges();
          }
          //
          sNodeNew = vNode.snode;
          //
          ts.Committ();
        }
        //return this.Get_NodeInfo(sNodeNew, null);
        return this.Get_NodesInfoFilteredExt2(vn => vn.snode == sNodeNew, null, FS_Operations.FilterVFS.Disabled | FS_Operations.FilterVFS.ACL | FS_Operations.FilterVFS.Deleted).FirstOrDefault();
      }
      catch { }
      return null;
    }


    public List<FS_Operations.FS_NodeInfoExt_Interface> EnsureResourceVFS(int? rNodeFolderOrParent, Expression<Func<IKGD_VNODE, bool>> vNodeFilter, Expression<Func<IKGD_VDATA, bool>> vDataFilter, Action<IKGD_VNODE, IKGD_VDATA> newResourceInitializer) { return EnsureResourceVFS(rNodeFolderOrParent, vNodeFilter, vDataFilter, newResourceInitializer, null); }
    public List<FS_Operations.FS_NodeInfoExt_Interface> EnsureResourceVFS(int? rNodeFolderOrParent, Expression<Func<IKGD_VNODE, bool>> vNodeFilter, Expression<Func<IKGD_VDATA, bool>> vDataFilter, Action<IKGD_VNODE, IKGD_VDATA> newResourceInitializer, bool? enableBatchPostProcessing)
    {
      //
      List<FS_Operations.FS_NodeInfoExt_Interface> nodes = null;
      //
      Expression<Func<IKGD_VNODE, bool>> vNodeFilterAll = null;
      Expression<Func<IKGD_VDATA, bool>> vDataFilterAll = null;
      //
      if (vNodeFilter != null || vDataFilter != null)
      {
        vNodeFilterAll = vNodeFilter ?? PredicateBuilder.True<IKGD_VNODE>();
        vDataFilterAll = vDataFilter ?? PredicateBuilder.True<IKGD_VDATA>();
        nodes = this.Get_NodesInfoFilteredExt2(vNodeFilterAll, vDataFilterAll, FS_Operations.FilterVFS.Disabled | FS_Operations.FilterVFS.ACL).ToList();
      }
      if ((nodes == null || !nodes.Any()) && rNodeFolderOrParent != null && newResourceInitializer != null)
      {
        IKGD_VNODE vNode = this.Factory_IKGD_VNODE();
        IKGD_VDATA vData = this.Factory_IKGD_VDATA();
        newResourceInitializer(vNode, vData);
        var node = CreateResourceVFS(rNodeFolderOrParent.Value, vNode, vData, null, null, null, enableBatchPostProcessing);
        nodes = new List<FS_NodeInfoExt_Interface> { node as FS_Operations.FS_NodeInfoExt_Interface };
      }
      //
      return nodes;
    }


    //
    // COW per update delle properties: versione con update ottimizzato delle properties
    // attenzione non usa la property .fsNode di FS_PropertyInfo
    //
    public bool COW_UpdateProperties(int sNodeSRC_code, IEnumerable<FS_PropertyInfo> propertiesData, bool releaseLock) { return COW_UpdateProperties(DB.IKGD_SNODEs.FirstOrDefault(n => n.code == sNodeSRC_code), propertiesData, releaseLock); }
    public bool COW_UpdateProperties(IKGD_SNODE sNode, IEnumerable<FS_PropertyInfo> propertiesData, bool releaseLock)
    {
      List<IKGD_RNODE> lockedNodes = null;
      try
      {
        //
        // acquisizione del lock
        //
        if (sNode != null)
          if ((lockedNodes = LockNodes(new List<int> { sNode.code }, false)) == null)
            throw new Exception("Impossibile acquisire l'esclusiva sulle operazioni per le risorse selezionate");
        //
        // transazioni per eseguire l'operazione
        //
        using (TransactionScope ts = IKGD_TransactionFactory.Transaction(Utility.TryParse<int>(IKGD_Config.AppSettings["VFS_TimeoutDB"], 300)))
        {
          List<IKGD_PROPERTY> propNodes = DB.IKGD_PROPERTies.Where(r => r.rnode == sNode.rnode && r.flag_current).ToList();
          //List<IKGD_PROPERTY> propNodes = DB.IKGD_PROPERTies.Where(r => r.rnode == sNode.rnode && r.flag_current && !r.flag_deleted).ToList();
          //
          List<KeyValuePair<FS_PropertyInfo, IKGD_PROPERTY>> propNodesNew = new List<KeyValuePair<FS_PropertyInfo, IKGD_PROPERTY>>();
          //
          bool useFullCOW = true;
          bool useFullCOW_with_deleted = true;  // per includere anche le properties cancellate nel COW (saranno eliminate solo in pubblicazione)
          if (useFullCOW)
          {
            //
            // sostituzione dell'intero set di properties ad ogni COW per consentire un utilizzo efficace del versioning
            //
            propNodes.ForEach(r => r.flag_current = false);
            foreach (var item in propertiesData.Where(r => !(r.IsDeleted && r.version == null)).Where(r => !r.IsDeleted || useFullCOW_with_deleted).OrderBy(r => !r.IsTainted).ThenBy(r => r.version))
            {
              IKGD_PROPERTY propNodeNew = this.Factory_IKGD_PROPERTY();
              var refNode = propNodes.FirstOrDefault(r => r.version == item.version);
              propNodeNew.rnode = item.rNodeCode;
              propNodeNew.name = item.name;
              propNodeNew.value = item.value;
              //propNodeNew.data = item.data;
              propNodeNew.attributeId = item.attributeId;
              propNodeNew.flag_deleted = item.IsDeleted;
              if (refNode != null && !item.IsTainted)
                propNodeNew.username = refNode.username;
              //DB.IKGD_PROPERTies.InsertOnSubmit(propNodeNew);
              propNodesNew.Add(new KeyValuePair<FS_PropertyInfo, IKGD_PROPERTY>(item, propNodeNew));
            }
            //
          }
          else
          {
            //
            // cancellazione delle properties eliminate
            //
            List<FS_PropertyInfo> unProcessedProperties = propertiesData.Where(r => r.IsTainted).ToList();
            var deleted = unProcessedProperties.Where(p => p.version != null && p.IsDeleted).Select(p => p.version.Value).ToList();
            if (deleted.Any())
            {
              propNodes.Where(p => deleted.Contains(p.version)).ForEach(prop =>
              {
                unProcessedProperties.RemoveAll(p => p.version == prop.version);
                IKGD_PROPERTY propNodeDup = CloneNode<IKGD_PROPERTY>(prop, true, true);
                propNodeDup.flag_deleted = true;
                //DB.IKGD_PROPERTies.InsertOnSubmit(propNodeDup);
                propNodesNew.Add(new KeyValuePair<FS_PropertyInfo, IKGD_PROPERTY>(null, propNodeDup));
              });
            }
            //
            // updates
            //
            var updated = unProcessedProperties.Where(p => p.version != null && !p.IsDeleted).Select(p => p.version.Value).ToList();
            if (updated.Any())
            {
              propNodes.Where(p => updated.Contains(p.version)).ForEach(prop =>
              {
                var propInfo = unProcessedProperties.FirstOrDefault(p => p.version == prop.version);
                if (propInfo == null)
                  return;
                unProcessedProperties.Remove(propInfo);
                IKGD_PROPERTY propNodeDup = CloneNode<IKGD_PROPERTY>(prop, true, true);
                propNodeDup.name = propInfo.name;
                propNodeDup.value = propInfo.value;
                //propNodeDup.data = propInfo.data;
                propNodeDup.flag_deleted = false;
                propNodeDup.attributeId = propInfo.attributeId;
                //DB.IKGD_PROPERTies.InsertOnSubmit(propNodeDup);
                propNodesNew.Add(new KeyValuePair<FS_PropertyInfo, IKGD_PROPERTY>(propInfo, propNodeDup));
              });
            }
            //
            // new entries
            //
            var created = unProcessedProperties.Where(p => p.version == null && !p.IsDeleted).ToList();
            created.ForEach(propInfo =>
            {
              unProcessedProperties.Remove(propInfo);
              IKGD_PROPERTY propNodeDup = CloneNode<IKGD_PROPERTY>(null, false, false);
              propNodeDup.rnode = sNode.rnode;
              propNodeDup.name = propInfo.name;
              propNodeDup.value = propInfo.value;
              //propNodeDup.data = propInfo.data;
              propNodeDup.attributeId = propInfo.attributeId;
              //DB.IKGD_PROPERTies.InsertOnSubmit(propNodeDup);
              propNodesNew.Add(new KeyValuePair<FS_PropertyInfo, IKGD_PROPERTY>(propInfo, propNodeDup));
            });
            //
            // verifica di aver processato tutto
            //
            if (unProcessedProperties.Any())
            {
              // non dovrebbe mai verificarsi questa condizione...
            }
            //
          }  // if useFullCOW
          //
          // per salvare le properties nell'ordine in cui sono state fornite
          var propsOrig = propertiesData.ToList();
          var propsNew = propNodesNew.OrderBy(r => propsOrig.IndexOf(r.Key)).Select(r => r.Value).ToList();
          DB.IKGD_PROPERTies.InsertAllOnSubmit(propsNew);
          //
          this.UpdateVersionDateOnChangeSet(false, true);
          var chg = DB.GetChangeSet();
          DB.SubmitChanges();
          //
          ts.Committ();
        }
        //
        return true;
      }
      catch (Exception ex)
      {
        // rilancia l'eventuale eccezione
        throw ex;
      }
      finally
      {
        //
        // release del lock
        //
        if (releaseLock && lockedNodes != null)
          if (UnLockNodes(lockedNodes) == null)
            throw new Exception("Impossibile sbloccare le risorse acquisite precedentemente");
      }
    }


    //
    // COW per update delle relations: versione con update ottimizzato delle relations
    //
    public bool COW_UpdateRelations(int sNodeSRC_code, IEnumerable<FS_RelationInfo> relationsData) { return COW_UpdateRelations(DB.IKGD_SNODEs.FirstOrDefault(n => n.code == sNodeSRC_code), relationsData, false); }
    public bool COW_UpdateRelations(int sNodeSRC_code, IEnumerable<FS_RelationInfo> relationsData, bool releaseLock) { return COW_UpdateRelations(DB.IKGD_SNODEs.FirstOrDefault(n => n.code == sNodeSRC_code), relationsData, releaseLock); }
    public bool COW_UpdateRelations(IKGD_SNODE sNodeSRC, IEnumerable<FS_RelationInfo> relationsData, bool releaseLock)
    {
      List<IKGD_RNODE> lockedNodes = null;
      try
      {
        //
        // acquisizione del lock
        //
        if (sNodeSRC != null)
          if ((lockedNodes = LockNodes(new List<int> { sNodeSRC.code }, false)) == null)
            throw new Exception("Impossibile acquisire l'esclusiva sulle operazioni per le risorse selezionate");
        //
        // transazioni per eseguire l'operazione
        //
        using (TransactionScope ts = IKGD_TransactionFactory.Transaction(Utility.TryParse<int>(IKGD_Config.AppSettings["VFS_TimeoutDB"], 300)))
        {
          //
          bool useFullCOW = true;
          bool useFullCOW_with_deleted = true;  // per includere anche le relations cancellate nel COW (saranno eliminate solo in pubblicazione)
          bool positionRenormalize = true;
          if (useFullCOW)
          {
            //
            // sostituzione dell'intero set di properties ad ogni COW per consentire un utilizzo efficace del versioning
            //
            List<IKGD_RELATION> relNodes = DB.IKGD_RELATIONs.Where(r => r.rnode == sNodeSRC.rnode && r.flag_current).ToList();
            relNodes.ForEach(r => r.flag_current = false);
            double positionIdx = 0;
            //foreach (var item in relationsData.Where(r => !(r.IsDeleted && r.version == null)).Where(r => !r.IsDeleted || useFullCOW_with_deleted).OrderBy(r => !r.IsTainted).ThenBy(r => r.position).ThenBy(r => r.sNodeCode))
            foreach (var item in relationsData.Where(r => !(r.IsDeleted && r.version == null)).Where(r => !r.IsDeleted || useFullCOW_with_deleted).OrderBy(r => r.position).ThenBy(r => !r.IsTainted).ThenBy(r => r.sNodeCode))
            {
              IKGD_RELATION relNodeNew = this.Factory_IKGD_RELATION();
              var refNode = relNodes.FirstOrDefault(r => r.version == item.version);
              relNodeNew.rnode = sNodeSRC.rnode;
              relNodeNew.snode_src = sNodeSRC.code;
              relNodeNew.rnode_dst = item.rNodeCodeAuto;
              relNodeNew.snode_dst = item.sNodeCodeAuto;
              relNodeNew.type = item.type;
              relNodeNew.position = positionRenormalize ? ++positionIdx : item.position;
              //relNodeNew.position = item.position;
              relNodeNew.flag_deleted = item.IsDeleted;
              if (refNode != null && !item.IsTainted)
                relNodeNew.username = refNode.username;
              DB.IKGD_RELATIONs.InsertOnSubmit(relNodeNew);
            }
            //
          }
          else
          {
            //
            List<FS_RelationInfo> unProcessedRelations = relationsData.Where(r => r.IsTainted).ToList();
            //
            //var predicate = PredicateBuilder.True<IKGD_RELATION>().And(r => r.snode_src == sNodeSRC.code && r.flag_current);
            var predicate = PredicateBuilder.True<IKGD_RELATION>().And(r => (r.rnode == sNodeSRC.rnode || r.snode_src == sNodeSRC.code) && r.flag_current);
            //var predicate = PredicateBuilder.True<IKGD_RELATION>().And(r => r.snode_src == sNodeSRC.code && r.flag_current && !r.flag_deleted);
            List<IKGD_RELATION> relNodes = DB.IKGD_RELATIONs.Where(predicate).ToList();
            //
            // cancellazione delle relazioni eliminate
            //
            foreach (IKGD_RELATION relNode in relNodes)
            {
              FS_RelationInfo relInfo = unProcessedRelations.FirstOrDefault(r => r.sNodeCode == relNode.snode_dst && r.IsDeleted);
              relInfo = relInfo ?? unProcessedRelations.FirstOrDefault(r => r.rNodeCodeSRC == relNode.rnode_dst && r.IsDeleted);
              if (relInfo != null)
              {
                unProcessedRelations.Remove(relInfo);
                IKGD_RELATION relNodeDup = CloneNode<IKGD_RELATION>(relNode, true, true);
                relNodeDup.flag_deleted = true;
                DB.IKGD_RELATIONs.InsertOnSubmit(relNodeDup);
              }
            }
            //
            // updates
            //
            foreach (IKGD_RELATION relNode in relNodes)
            {
              FS_RelationInfo relInfo = unProcessedRelations.FirstOrDefault(r => r.sNodeCode == relNode.snode_dst);
              if (relInfo != null)
              {
                unProcessedRelations.Remove(relInfo);
                IKGD_RELATION relNodeDup = CloneNode<IKGD_RELATION>(relNode, true, true);
                relNodeDup.rnode_dst = relInfo.rNodeCodeAuto;
                relNodeDup.snode_dst = relInfo.sNodeCodeAuto;
                relNodeDup.type = Utility.StringTruncate(relInfo.type ?? string.Empty, 250);
                relNodeDup.position = relInfo.position;
                relNodeDup.flag_deleted = false;
                DB.IKGD_RELATIONs.InsertOnSubmit(relNodeDup);
              }
            }
            //
            // new entries
            //
            foreach (FS_RelationInfo relInfo in unProcessedRelations.Where(r => !r.IsDeleted).ToList())
            {
              unProcessedRelations.Remove(relInfo);
              IKGD_RELATION relNodeDup = CloneNode<IKGD_RELATION>(null, false, false);
              relNodeDup.IKGD_RNODE = sNodeSRC.IKGD_RNODE;
              relNodeDup.IKGD_SNODE_src = sNodeSRC;
              relNodeDup.rnode_dst = relInfo.rNodeCodeAuto;
              relNodeDup.snode_dst = relInfo.sNodeCodeAuto;
              relNodeDup.type = Utility.StringTruncate(relInfo.type ?? string.Empty, 250);
              relNodeDup.position = relInfo.position;
              DB.IKGD_RELATIONs.InsertOnSubmit(relNodeDup);
            }
            //
            // verifica di aver processato tutto
            //
            if (unProcessedRelations.Count > 0)
            {
              // non dovrebbe verificarsi questa condizione...
              // forse potrebbe presentarsi nel caso di risorse con un match in rnode_src e non per snode_src
            }
            //
          }  //if (useFullCOW)
          //
          // modifiche ai nomi delle risorse
          //
          foreach (FS_RelationInfo relInfo in relationsData.Where(r => r.HasTaintedName && !r.IsDeleted))
          {
            IKGD_VNODE vNode = DB.IKGD_VNODEs.FirstOrDefault(n => n.snode == relInfo.sNodeCode && n.flag_current && !n.flag_deleted);
            if (vNode != null)
            {
              vNode = CloneNode(vNode, true, true);
              vNode.name = Utility.StringTruncate(relInfo.name, MaxNameLen);
              DB.IKGD_VNODEs.InsertOnSubmit(vNode);
            }
          }
          //
          this.UpdateVersionDateOnChangeSet(false, true);
          //var chg = DB.GetChangeSet();
          DB.SubmitChanges();
          //
          ts.Committ();
        }
        //
        // transazioni separate per eseguire le operazioni di pulizia
        //
        try
        {
          using (TransactionScope ts = IKGD_TransactionFactory.Transaction(null))
          {
            //
            // rinormalizzazione delle eventuali relazioni non piu' valide
            //
            //var relNodesSet = DB.IKGD_RELATIONs.Where(r => r.snode_src == sNodeSRC.code).ToList();
            // relazioni senza una destinazione pubblicata valida
            var brokenNodes1 =
              (from rel in DB.IKGD_RELATIONs.Where(r => r.snode_src == sNodeSRC.code).Where(r => r.flag_published)
               where !DB.IKGD_VNODEs.Any(n => n.snode == rel.snode_dst && n.flag_published)
               select rel).ToList();
            // relazioni senza una destinazione preview valida
            var brokenNodes2 =
              (from rel in DB.IKGD_RELATIONs.Where(r => r.snode_src == sNodeSRC.code).Where(r => r.flag_current)
               where !DB.IKGD_VNODEs.Any(n => n.snode == rel.snode_dst && n.flag_current)
               select rel).ToList();
            brokenNodes1.ForEach(r => r.flag_published = false);
            brokenNodes2.ForEach(r => r.flag_current = false);
            //var chg01 = DB.GetChangeSet();
            DB.SubmitChanges();
            var brokenNodes3 = DB.IKGD_RELATIONs.Where(r => r.snode_src == sNodeSRC.code).Where(r => r.flag_deleted && !(r.flag_published || r.flag_current)).ToList();
            brokenNodes3.ForEach(r => r.flag_deleted = false);
            //var chg02 = DB.GetChangeSet();
            DB.SubmitChanges();
            //
            ts.Committ();
          }
        }
        catch { }
        //
        return true;
      }
      catch (Exception ex)
      {
        // rilancia l'eventuale eccezione
        throw ex;
      }
      finally
      {
        //
        // release del lock
        //
        if (releaseLock && lockedNodes != null)
          if (UnLockNodes(lockedNodes) == null)
            throw new Exception("Impossibile sbloccare le risorse acquisite precedentemente");
      }
    }


    //
    // COW resource update by delegate
    //
    public delegate List<IKGD_XNODE> COW_UpdateResourceWorkerDelegate(FS_Operations fsOp, ref IKGD_SNODE sNode, out string newArea);
    //
    public IKGD_SNODE COW_UpdateResource(IKGD_SNODE sNode, COW_UpdateResourceWorkerDelegate workerCallBack) { return COW_UpdateResource(sNode, workerCallBack, true, false); }
    public IKGD_SNODE COW_UpdateResource(IKGD_SNODE sNode, COW_UpdateResourceWorkerDelegate workerCallBack, bool releaseLock, bool updateSubTreeAreas)
    {
      if (workerCallBack == null)
        return sNode;
      List<IKGD_RNODE> lockedNodes = null;
      try
      {
        //
        // acquisizione del lock
        //
        if (sNode != null)
          if ((lockedNodes = LockNodes(new List<int> { sNode.code }, false)) == null)
            throw new Exception("Impossibile acquisire l'esclusiva sulle operazioni per le risorse selezionate");
        string newArea = null;
        //
        // transazioni per eseguire l'operazione
        // non posso usarle per come e' implementato workerCallBackDelegate, devo
        // convertire i filtri con linqkit
        //
        List<IKGD_XNODE> affectedNodes = null;
        using (TransactionScope ts = IKGD_TransactionFactory.Transaction(Utility.TryParse<int>(IKGD_Config.AppSettings["VFS_TimeoutDB"], 300)))
        {
          // delegate instantiation
          COW_UpdateResourceWorkerDelegate workerCallBackDelegate = new COW_UpdateResourceWorkerDelegate(workerCallBack);
          affectedNodes = workerCallBackDelegate(this, ref sNode, out newArea);
          if (affectedNodes != null && sNode != null)
          {
            //
            ChangeSet chgSet = DB.GetChangeSet();
            affectedNodes.AddRange(chgSet.Inserts.OfType<IKGD_XNODE>());
            affectedNodes.AddRange(chgSet.Updates.OfType<IKGD_XNODE>());
            //
            OpHandlerCOW(OpHandlerCOW_OperationEnum.Update, OpHandlerCOW_DeserializeVfsInfoEnum.Full, affectedNodes.OfType<IKGD_VNODE>(), affectedNodes.OfType<IKGD_VDATA>(), affectedNodes.OfType<IKGD_INODE>());
            // salvataggio dei dati solo se la callback ha operato correttamente
            //ChangeSet chgSetAfter = DB.GetChangeSet();
            //DB.SubmitChanges();
            //
            ts.Committ();
          }
          else
          {
            return null;
          }
        }
        //
        if (affectedNodes != null && affectedNodes.OfType<IKGD_VDATA>().Any())
        {
          this.Update_vDataKeyValues(affectedNodes.OfType<IKGD_VDATA>());
        }
        //
        if (sNode != null && sNode.flag_folder && newArea != null)
        {
          int areasCount = COW_UpdateSubTreeArea(sNode.code, newArea);
        }
        //
        return sNode;
      }
      catch (Exception ex)
      {
        // rilancia l'eventuale eccezione
        throw ex;
      }
      finally
      {
        //
        // release del lock
        //
        if (releaseLock)
        {
          if (lockedNodes != null)
          {
            if (UnLockNodes(lockedNodes) == null)
              throw new Exception("Impossibile sbloccare le risorse acquisite precedentemente");
          }
          else if (sNode != null)
          {
            // serve nel caso sia stato acquisito un lock durante un upload quando si crea una nuova risorsa
            UnLockNodes(new List<int> { sNode.code }, false);
          }
        }
      }
    }


    //
    // COW forza tutti gli elementi di un subtree ad appartenere alla stessa area
    // NB questo metodo non effettua controlli di accesso ed e' pensato per essere utilizzato solo da root
    //
    public int COW_UpdateSubTreeArea(int sNodeSubRoot, string area)
    {
      try
      {
        int counter = 0;
        using (TransactionScope ts = IKGD_TransactionFactory.Transaction(Utility.TryParse<int>(IKGD_Config.AppSettings["VFS_TimeoutDB"], 600)))
        {
          IKGD_VNODE vNodeRoot = this.NodeActive(sNodeSubRoot);
          if (vNodeRoot == null || !vNodeRoot.flag_folder)
            return 0;
          List<int> folders = new List<int>();
          for (List<IKGD_VNODE> lastF = new List<IKGD_VNODE> { vNodeRoot }; lastF != null && lastF.Count > 0; )
          {
            var foldersTmp = lastF.Select(n => n.folder).ToList();
            lastF.Clear();
            foreach (var folders_slice in foldersTmp.Slice(500))
            {
              var _lastF =
                (from vNode in this.NodesActive<IKGD_VNODE>().Where(n => n.flag_folder)
                 where folders_slice.Contains(vNode.parent.Value)
                 select vNode).ToList();
              lastF.AddRange(_lastF);
              folders.AddRange(_lastF.Select(n => n.folder));
            }
          }
          if (!folders.Contains(vNodeRoot.folder))
            folders.Insert(0, vNodeRoot.folder);
          //
          List<IKGD_VDATA> vDatas = new List<IKGD_VDATA>();
          foreach (var folders_slice in folders.Slice(500))
          {
            var _vDatas =
              (from vNode in this.NodesActive<IKGD_VNODE>().Where(n => folders_slice.Contains(n.folder))
               from vData in this.NodesActive<IKGD_VDATA>().Where(n => n.rnode == vNode.rnode).Where(n => n.area != area)
               select vData).Distinct();
            vDatas.AddRange(_vDatas);
          }
          //
          foreach (IKGD_VDATA vd in vDatas)
          {
            counter++;
            IKGD_VDATA vdDup = CloneNode(vd, true, true);
            vdDup.area = area;
            DB.IKGD_VDATAs.InsertOnSubmit(vdDup);
          }
          //
          OpHandlerCOW(OpHandlerCOW_OperationEnum.Update, OpHandlerCOW_DeserializeVfsInfoEnum.Rebind, null, vDatas, null);
          //ChangeSet chgSet = DB.GetChangeSet();
          //DB.SubmitChanges();
          //
          ts.Committ();
        }
        return counter;
      }
      catch { }
      return 0;
    }


    public int COW_UpdateSubTreeLanguageACL(int sNodeSubRoot, string language, string area, bool updateOnlyNullLanguage, bool updateOnlyFoldersLanguage, bool updateLanguage, bool updateArea, bool recursive)
    {
      try
      {
        int counter = 0;
        using (TransactionScope ts = IKGD_TransactionFactory.Transaction(Utility.TryParse<int>(IKGD_Config.AppSettings["VFS_TimeoutDB"], 3600)))
        {
          IKGD_VNODE vNodeRoot = this.NodeActive(sNodeSubRoot);
          if (vNodeRoot == null)
            return 0;
          List<int> folders = new List<int>();
          if (recursive && vNodeRoot.flag_folder)
          {
            for (List<IKGD_VNODE> lastF = new List<IKGD_VNODE> { vNodeRoot }; lastF != null && lastF.Count > 0; )
            {
              var foldersTmp = lastF.Select(n => n.folder).ToList();
              lastF.Clear();
              foreach (var folders_slice in foldersTmp.Slice(500))
              {
                var _lastF =
                  (from vNode in this.NodesActive<IKGD_VNODE>().Where(n => n.flag_folder)
                   where folders_slice.Contains(vNode.parent.Value)
                   select vNode).ToList();
                lastF.AddRange(_lastF);
                folders.AddRange(_lastF.Select(n => n.folder));
              }
            }
            if (!folders.Contains(vNodeRoot.folder))
              folders.Insert(0, vNodeRoot.folder);
          }
          //
          List<FS_Operations.FS_NodeInfo> fsNodes = new List<FS_NodeInfo>();
          //
          Expression<Func<IKGD_VNODE, bool>> filter_vNode = this.Get_vNodeFilterACLv2(false);
          Expression<Func<IKGD_VDATA, bool>> filter_vData = this.Get_vDataFilterACLv2(true, true);
          //
          if (updateArea)
          {
            filter_vData = filter_vData.And(n => !string.Equals(n.area, area));
          }
          //
          {
            var _fsNodes =
              from vNode in this.NodesActive<IKGD_VNODE>(false).Where(filter_vNode).Where(n => n.snode == sNodeSubRoot)
              from vData in this.NodesActive<IKGD_VDATA>(false).Where(filter_vData).Where(n => n.rnode == vNode.rnode)
              select new FS_Operations.FS_NodeInfo { vNode = vNode, vData = vData };
            fsNodes.AddRange(_fsNodes);
          }
          //
          foreach (var folders_slice in folders.Slice(500))
          {
            Expression<Func<IKGD_VNODE, bool>> _filter_vNode = filter_vNode;
            if (updateOnlyFoldersLanguage)
            {
              _filter_vNode = _filter_vNode.And(n => n.flag_folder && folders_slice.Contains(n.folder));
            }
            else
            {
              _filter_vNode = _filter_vNode.And(n => folders_slice.Contains(n.folder));
            }
            //
            var _fsNodes =
              from vNode in this.NodesActive<IKGD_VNODE>(false).Where(_filter_vNode)
              from vData in this.NodesActive<IKGD_VDATA>(false).Where(filter_vData).Where(n => n.rnode == vNode.rnode)
              select new FS_Operations.FS_NodeInfo { vNode = vNode, vData = vData };
            //
            fsNodes.AddRange(_fsNodes);
            //
          }
          //
          foreach (var grp in fsNodes.Distinct((n1, n2) => n1.VersionVNODE == n2.VersionVNODE).GroupBy(n => n.vNode.rnode))
          {
            var fsNode = grp.FirstOrDefault();
            var node_lang = fsNode.vNode.language;
            bool update_lang = (updateLanguage && node_lang != language && (updateOnlyNullLanguage == false || (updateOnlyNullLanguage == true && string.IsNullOrEmpty(node_lang))));
            bool update_area = (updateArea && fsNode.vData.area != area);
            if (update_area == false && update_lang == false)
              continue;
            bool update_vNode = update_lang;
            bool update_vData = update_area;
            counter += grp.Count();
            //
            if (update_vData)
            {
              IKGD_VDATA vdDup = CloneNode(fsNode.vData, true, true);
              if (update_area)
                vdDup.area = area;
              DB.IKGD_VDATAs.InsertOnSubmit(vdDup);
            }
            //
            if (update_vNode)
            {
              foreach (var node in grp)
              {
                IKGD_VNODE vnDup = CloneNode(node.vNode, true, true);
                vnDup.language = language;
                DB.IKGD_VNODEs.InsertOnSubmit(vnDup);
              }
            }
          }
          //
          ChangeSet chgSet = DB.GetChangeSet();
          OpHandlerCOW(OpHandlerCOW_OperationEnum.Update, OpHandlerCOW_DeserializeVfsInfoEnum.Rebind, chgSet.Inserts.OfType<IKGD_VNODE>(), chgSet.Inserts.OfType<IKGD_VDATA>(), null);
          //DB.SubmitChanges();
          //
          ts.Committ();
        }
        return counter;
      }
      catch { }
      return 0;
    }



    /////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    //
    //       PUBLICATION methods
    //
    /////////////////////////////////////////////////////////////////////////////////////////////////////////////////


    //
    // scan di un tree per conteggiare il numero di risorse coinvolte
    //
    public int FreezeTreeScan(int sNodeRoot, int sNodeFolder, bool noRecurse)
    {
      try
      {
        //
        int timeOut = Utility.TryParse<int>(IKGD_Config.AppSettings["VFS_TimeoutDB"], 1800);
        if (this.DB.CommandTimeout < timeOut)
          this.DB.CommandTimeout = timeOut;
        //
        IKGD_VNODE vNodeRoot = this.NodeActive(sNodeRoot, true);
        List<int> rNodes = new List<int>();
        //
        // devo prevedere dei filtri piu' restrittivi sui ruoli (pubblicazione o pubblicazione immediata)
        //
        int acls = (int)(FS_ACL_Reduced.AclType.Publish | FS_ACL_Reduced.AclType.PublishDirect);
        List<string> activeAreas = IsRoot ? CurrentAreasExtended.AreasAllowed : DB.IKGD_ADMINs.Where(r => r.username == CurrentUser && ((r.flags_acl & acls) != 0)).Select(r => r.area).ToList();
        //
        Expression<Func<IKGD_VDATA, bool>> vDataFilterAll = PredicateBuilder.True<IKGD_VDATA>();
        vDataFilterAll = vDataFilterAll.And(n => activeAreas.Contains(n.area));  //TODO:ACL (usare ACL differenti per il folder scan [read] e per il publish [write])
        List<IKGD_VNODE> subFolders = new List<IKGD_VNODE>();
        //
        // scan del tree preview
        //
        // se parto da un folder genero la lista di tutti i suoi subfolders (senza quelli cancellati)
        if (vNodeRoot.flag_folder && !vNodeRoot.flag_deleted)
        {
          subFolders.Add(vNodeRoot);  // mantengo comunque il folder corrente (viene pubblicata la cartella con i contenuti ma senza ricorsione)
          if (!noRecurse)
          {
            for (List<IKGD_VNODE> lastF = new List<IKGD_VNODE> { vNodeRoot }; lastF != null && lastF.Count > 0; )
            {
              var foldersTmp = lastF.Where(n => !n.flag_deleted).Select(n => n.folder).ToList();
              lastF.Clear();
              foreach (var folders_slice in foldersTmp.Slice(500))
              {
                var _lastF =
                  (from vNode in this.NodesActive<IKGD_VNODE>(true).Where(n => n.flag_folder)
                   from vData in this.NodesActive<IKGD_VDATA>(true).Where(vDataFilterAll)
                   where vNode.rnode == vData.rnode && folders_slice.Contains(vNode.parent.Value)
                   select vNode).ToList();
                lastF.AddRange(_lastF);
                subFolders.AddRange(_lastF);
              }
            }
          }
        }
        List<int> subFoldersCodes = subFolders.Select(n => n.folder).Distinct().ToList();
        List<int> subFoldersCodes2 = subFolders.Select(n => n.snode).Concat(new int[] { vNodeRoot.snode }).Distinct().ToList();
        //
        // scan del tree published per i corner case di pubblicazione per i files/folders in preview
        // che sono stati spostati altrove per cui il published corrispondente deve essere eliminato
        //
        List<int> subFoldersPublished = new List<int>();
        IKGD_VNODE vNodeRootPub = this.NodesActive<IKGD_VNODE>(0, false).FirstOrDefault(n => n.snode == sNodeRoot);
        if (vNodeRootPub != null && vNodeRootPub.flag_folder)
        {
          subFoldersPublished.Add(vNodeRoot.folder);  // mantengo comunque il folder corrente (viene pubblicata la cartella con i contenuti ma senza ricorsione
          if (!noRecurse)
          {
            for (List<int> lastF = new List<int> { vNodeRootPub.folder }; lastF != null && lastF.Count > 0; )
            {
              var folders = lastF.ToList();
              lastF.Clear();
              foreach (var folders_slice in folders.Slice(500))
              {
                var _lastF =
                  (from vNode in this.NodesActive<IKGD_VNODE>(0, false).Where(n => n.flag_folder)
                   from vData in this.NodesActive<IKGD_VDATA>(0, false).Where(n => n.rnode == vNode.rnode).Where(vDataFilterAll)
                   where folders_slice.Contains(vNode.parent.Value)
                   select vNode.folder).ToList();
                lastF.AddRange(_lastF);
                subFoldersPublished.AddRange(_lastF);
              }
            }
          }
        }
        subFoldersPublished = subFoldersPublished.Distinct().ToList();
        //
        // aggiungo le relations del root node nel caso di pubblicazione di un nodo singolo (non folder)
        //
        try
        {
          var relNodes =
            (from rel in this.NodesActive<IKGD_RELATION>(false).Where(r => r.rnode == vNodeRoot.rnode)
             from vNode in this.NodesActive<IKGD_VNODE>(true).Where(n => n.rnode == rel.rnode_dst)
             from vData in this.NodesActive<IKGD_VDATA>(true).Where(vDataFilterAll).Where(n => n.rnode == vNode.rnode)
             select vNode.snode).ToList();
          subFoldersCodes2 = subFoldersCodes2.Concat(relNodes).Distinct().ToList();
        }
        catch { }
        //
        int idx_slice = 500;
        for (int idx = 0; idx < Math.Max(subFoldersCodes.Count, subFoldersCodes2.Count); idx += idx_slice)
        {
          List<int> _subFoldersCodes = subFoldersCodes.Skip(idx).Take(idx_slice).ToList();
          List<int> _subFoldersCodes2 = subFoldersCodes2.Skip(idx).Take(idx_slice).ToList();
          //
          // preparo la lista di tutti i nodi (files+folders) che devono essere marchiati come snapshot
          //
          IEnumerable<IKGD_VNODE> List_vNodes =
            (from vNode in this.NodesActive<IKGD_VNODE>(true).Where(n => (!n.flag_folder && _subFoldersCodes.Contains(n.folder)) || _subFoldersCodes2.Contains(n.snode)).Where(n => !n.flag_published)
             from vData in this.NodesActive<IKGD_VDATA>(true).Where(vDataFilterAll).Where(n => n.rnode == vNode.rnode)
             select vNode);
          IEnumerable<IKGD_VDATA> List_vDatas =
            (from vNode in this.NodesActive<IKGD_VNODE>(true).Where(n => (!n.flag_folder && _subFoldersCodes.Contains(n.folder)) || _subFoldersCodes2.Contains(n.snode))
             from vData in this.NodesActive<IKGD_VDATA>(true).Where(vDataFilterAll).Where(n => !n.flag_published).Where(n => n.rnode == vNode.rnode)
             select vData);
          IEnumerable<IKGD_INODE> List_iNodes =
            (from vNode in this.NodesActive<IKGD_VNODE>(true).Where(n => (!n.flag_folder && _subFoldersCodes.Contains(n.folder)) || _subFoldersCodes2.Contains(n.snode))
             from vData in this.NodesActive<IKGD_VDATA>(true).Where(vDataFilterAll).Where(n => n.rnode == vNode.rnode)
             from iNode in this.NodesActive<IKGD_INODE>(true).Where(n => !n.flag_published).Where(n => n.rnode == vNode.rnode)
             select iNode);
          IEnumerable<IKGD_PROPERTY> List_properties =
            (from vNode in this.NodesActive<IKGD_VNODE>(true).Where(n => (!n.flag_folder && _subFoldersCodes.Contains(n.folder)) || _subFoldersCodes2.Contains(n.snode))
             from vData in this.NodesActive<IKGD_VDATA>(true).Where(vDataFilterAll).Where(n => n.rnode == vNode.rnode)
             from prop in this.NodesActive<IKGD_PROPERTY>(true).Where(n => n.rnode == vNode.rnode)
             select prop);
          IEnumerable<IKGD_RELATION> List_relations =
            (from vNode in this.NodesActive<IKGD_VNODE>(true).Where(n => (!n.flag_folder && _subFoldersCodes.Contains(n.folder)) || _subFoldersCodes2.Contains(n.snode))
             from vData in this.NodesActive<IKGD_VDATA>(true).Where(vDataFilterAll).Where(n => n.rnode == vNode.rnode)
             from rel in this.NodesActive<IKGD_RELATION>(true).Where(n => n.rnode == vNode.rnode)
             select rel);
          // considera solo le risorse che hanno almeno un elemento non pubblicato
          List_properties = List_properties.GroupBy(r => r.rnode).Where(g => g.Any(r => r.flag_published == false || r.flag_deleted == true)).SelectMany(g => g.Select(r => r));
          List_relations = List_relations.GroupBy(r => r.rnode).Where(g => g.Any(r => r.flag_published == false || r.flag_deleted == true)).SelectMany(g => g.Select(r => r));
          //
          rNodes.AddRange(List_vNodes.Select(n => n.rnode));
          rNodes.AddRange(List_vDatas.Select(n => n.rnode));
          rNodes.AddRange(List_iNodes.Select(n => n.rnode));
          rNodes.AddRange(List_properties.Select(n => n.rnode));
          rNodes.AddRange(List_relations.Select(n => n.rnode));
        }
        //
        // conteggio dei files/folders in preview che sono stati spostati altrove per cui il published corrispondente
        // deve essere eliminato
        //
        if (subFoldersPublished != null && subFoldersPublished.Count > 0)
        {
          foreach (var _subFoldersPublished in subFoldersPublished.Slice(500))
          {
            var _subFoldersPublished_slice = _subFoldersPublished.ToList();
            List<int> moved_nodes =
              (from vn1 in this.NodesActive<IKGD_VNODE>(0, false).Where(n => _subFoldersPublished_slice.Contains(n.folder))
               from vn2 in this.NodesActive<IKGD_VNODE>(-1, true).Where(n => n.snode == vn1.snode)
               where vn1.folder != vn2.folder || vn1.parent != vn2.parent
               select vn1.rnode).Distinct().ToList();
            rNodes.AddRange(moved_nodes);
          }
        }
        //
        rNodes = rNodes.Distinct().ToList();
        //
        return rNodes.Count();
      }
      catch (Exception ex)
      {
        throw ex;
      }
    }


    //
    // freeze di un subtree per la creazione di uno snapshot di pubblicazione
    //
    public IKGD_SNAPSHOT FreezeTree(int sNodeRoot, int sNodeFolder, bool noRecurse, string message)
    {
      try
      {
        int timeOut = Utility.TryParse<int>(IKGD_Config.AppSettings["VFS_TimeoutDB"], 1800);
        //TODO: sarebbe forse il caso di usare IKGD_TransactionFactory.TransactionSerializable ?
        using (TransactionScope ts = IKGD_TransactionFactory.Transaction(timeOut))
        {
          if (this.DB.CommandTimeout < timeOut)
            this.DB.CommandTimeout = timeOut;
          //
          IKGD_VNODE vNodeRoot = this.NodeActive(sNodeRoot, true);
          List<IKGD_Path> paths = this.PathsFromNodeAuthor(sNodeRoot, true, false, true);
          IKGD_Path pathInfo = paths.FirstOrDefault(p => p.Fragments.Any(f => f.sNode == sNodeFolder)) ?? paths.FirstOrDefault();
          //
          // ottiene un nuovo ID per il freeze
          //
          IKGD_SNAPSHOT freeze = new IKGD_SNAPSHOT { date_frozen = DateTimeContext, flag_published = false, flag_rejected = false, username = this.CurrentUser, snode_root = sNodeRoot, snode_folder = sNodeFolder, affected = 0, name = vNodeRoot.name, path = pathInfo.Path, message = message };
          DB.IKGD_SNAPSHOTs.InsertOnSubmit(freeze);
          //var chg01 = DB.GetChangeSet();
          DB.SubmitChanges();
          //
          // devo prevedere dei filtri piu' restrittivi sui ruoli (pubblicazione o pubblicazione immediata)
          //
          int acls = (int)(FS_ACL_Reduced.AclType.Publish | FS_ACL_Reduced.AclType.PublishDirect);
          List<string> activeAreas = IsRoot ? CurrentAreasExtended.AreasAllowed : DB.IKGD_ADMINs.Where(r => r.username == CurrentUser && ((r.flags_acl & acls) != 0)).Select(r => r.area).ToList();
          //
          Expression<Func<IKGD_VDATA, bool>> vDataFilterAll = PredicateBuilder.True<IKGD_VDATA>();
          vDataFilterAll = vDataFilterAll.And(n => activeAreas.Contains(n.area));  //TODO:ACL (usare ACL differenti per il folder scan [read] e per il publish [write])
          List<IKGD_VNODE> subFolders = new List<IKGD_VNODE>();
          //
          // scan del tree preview
          //
          // se parto da un folder genero la lista di tutti i suoi subfolders (senza quelli cancellati)
          if (vNodeRoot.flag_folder && !vNodeRoot.flag_deleted)
          {
            subFolders.Add(vNodeRoot);  // mantengo comunque il folder corrente (viene pubblicata la cartella con i contenuti ma senza ricorsione
            if (!noRecurse)
            {
              for (List<IKGD_VNODE> lastF = new List<IKGD_VNODE> { vNodeRoot }; lastF != null && lastF.Count > 0; )
              {
                var foldersTmp = lastF.Where(n => !n.flag_deleted).Select(n => n.folder).ToList();
                lastF.Clear();
                foreach (var folders_slice in foldersTmp.Slice(500))
                {
                  var _lastF =
                    (from vNode in this.NodesActive<IKGD_VNODE>(true).Where(n => n.flag_folder)
                     from vData in this.NodesActive<IKGD_VDATA>(true).Where(vDataFilterAll)
                     where vNode.rnode == vData.rnode && folders_slice.Contains(vNode.parent.Value)
                     select vNode).ToList();
                  lastF.AddRange(_lastF);
                  subFolders.AddRange(_lastF);
                }
              }
            }
          }
          List<int> subFoldersCodes = subFolders.Select(n => n.folder).Distinct().ToList();
          List<int> subFoldersCodes2 = subFolders.Select(n => n.snode).Concat(new int[] { vNodeRoot.snode }).Distinct().ToList();
          //
          // scan del tree published per i corner case di pubblicazione per i files/folders in preview
          // che sono stati spostati altrove per cui il published corrispondente deve essere eliminato
          //
          List<int> subFoldersPublished = new List<int>();
          IKGD_VNODE vNodeRootPub = this.NodesActive<IKGD_VNODE>(0, false).FirstOrDefault(n => n.snode == sNodeRoot);
          if (vNodeRootPub != null && vNodeRootPub.flag_folder)
          {
            subFoldersPublished.Add(vNodeRoot.folder);  // mantengo comunque il folder corrente (viene pubblicata la cartella con i contenuti ma senza ricorsione
            if (!noRecurse)
            {
              for (List<int> lastF = new List<int> { vNodeRootPub.folder }; lastF != null && lastF.Count > 0; )
              {
                var folders = lastF.ToList();
                lastF.Clear();
                foreach (var folders_slice in folders.Slice(500))
                {
                  var _lastF =
                    (from vNode in this.NodesActive<IKGD_VNODE>(0, false).Where(n => n.flag_folder)
                     from vData in this.NodesActive<IKGD_VDATA>(0, false).Where(n => n.rnode == vNode.rnode).Where(vDataFilterAll)
                     where folders_slice.Contains(vNode.parent.Value)
                     select vNode.folder).ToList();
                  lastF.AddRange(_lastF);
                  subFoldersPublished.AddRange(_lastF);
                }
              }
            }
          }
          subFoldersPublished = subFoldersPublished.Distinct().ToList();
          //
          // aggiungo le relations del root node nel caso di pubblicazione di un nodo singolo (non folder)
          //
          try
          {
            var relNodes =
              (from rel in this.NodesActive<IKGD_RELATION>(false).Where(r => r.rnode == vNodeRoot.rnode)
               from vNode in this.NodesActive<IKGD_VNODE>(true).Where(n => n.rnode == rel.rnode_dst)
               from vData in this.NodesActive<IKGD_VDATA>(true).Where(vDataFilterAll).Where(n => n.rnode == vNode.rnode)
               select vNode.snode).ToList();
            subFoldersCodes2 = subFoldersCodes2.Concat(relNodes).Distinct().ToList();
          }
          catch { }
          //
          List<IKGD_FREEZED> freezeList = new List<IKGD_FREEZED>();
          //
          int idx_slice = 500;
          for (int idx = 0; idx < Math.Max(subFoldersCodes.Count, subFoldersCodes2.Count); idx += idx_slice)
          {
            List<int> _subFoldersCodes = subFoldersCodes.Skip(idx).Take(idx_slice).ToList();
            List<int> _subFoldersCodes2 = subFoldersCodes2.Skip(idx).Take(idx_slice).ToList();
            //
            // preparo la lista di tutti i nodi (files+folders) che devono essere marchiati come snapshot
            //
            IEnumerable<IKGD_VNODE> List_vNodes =
              (from vNode in this.NodesActive<IKGD_VNODE>(true).Where(n => (!n.flag_folder && _subFoldersCodes.Contains(n.folder)) || _subFoldersCodes2.Contains(n.snode)).Where(n => !n.flag_published)
               from vData in this.NodesActive<IKGD_VDATA>(true).Where(vDataFilterAll).Where(n => n.rnode == vNode.rnode)
               select vNode);
            IEnumerable<IKGD_VDATA> List_vDatas =
              (from vNode in this.NodesActive<IKGD_VNODE>(true).Where(n => (!n.flag_folder && _subFoldersCodes.Contains(n.folder)) || _subFoldersCodes2.Contains(n.snode))
               from vData in this.NodesActive<IKGD_VDATA>(true).Where(vDataFilterAll).Where(n => !n.flag_published).Where(n => n.rnode == vNode.rnode)
               select vData);
            IEnumerable<IKGD_INODE> List_iNodes =
              (from vNode in this.NodesActive<IKGD_VNODE>(true).Where(n => (!n.flag_folder && _subFoldersCodes.Contains(n.folder)) || _subFoldersCodes2.Contains(n.snode))
               from vData in this.NodesActive<IKGD_VDATA>(true).Where(vDataFilterAll).Where(n => n.rnode == vNode.rnode)
               from iNode in this.NodesActive<IKGD_INODE>(true).Where(n => !n.flag_published).Where(n => n.rnode == vNode.rnode)
               select iNode);
            IEnumerable<IKGD_PROPERTY> List_properties =
              (from vNode in this.NodesActive<IKGD_VNODE>(true).Where(n => (!n.flag_folder && _subFoldersCodes.Contains(n.folder)) || _subFoldersCodes2.Contains(n.snode))
               from vData in this.NodesActive<IKGD_VDATA>(true).Where(vDataFilterAll).Where(n => n.rnode == vNode.rnode)
               from prop in this.NodesActive<IKGD_PROPERTY>(true).Where(n => n.rnode == vNode.rnode)
               select prop);
            IEnumerable<IKGD_RELATION> List_relations =
              (from vNode in this.NodesActive<IKGD_VNODE>(true).Where(n => (!n.flag_folder && _subFoldersCodes.Contains(n.folder)) || _subFoldersCodes2.Contains(n.snode))
               from vData in this.NodesActive<IKGD_VDATA>(true).Where(vDataFilterAll).Where(n => n.rnode == vNode.rnode)
               from rel in this.NodesActive<IKGD_RELATION>(true).Where(n => n.rnode == vNode.rnode)
               select rel);
            // considera solo le risorse che hanno almeno un elemento non pubblicato
            List_properties = List_properties.GroupBy(r => r.rnode).Where(g => g.Any(r => r.flag_published == false || r.flag_deleted == true)).SelectMany(g => g.Select(r => r));
            List_relations = List_relations.GroupBy(r => r.rnode).Where(g => g.Any(r => r.flag_published == false || r.flag_deleted == true)).SelectMany(g => g.Select(r => r));
            //
            // setto il version_frozen dove mancante e preparo la lista dei nodi freezati nello snapshot
            //
            var List_vNodesDup = COW_FreezedEntities(freeze.version_frozen, List_vNodes).ToList();
            var List_vDatasDup = COW_FreezedEntities(freeze.version_frozen, List_vDatas).ToList();
            var List_iNodesDup = COW_FreezedEntities(freeze.version_frozen, List_iNodes).ToList();
            var List_propertiesDup = COW_FreezedEntities(freeze.version_frozen, List_properties).ToList();
            var List_relationsDup = COW_FreezedEntities(freeze.version_frozen, List_relations).ToList();
            //
            // i nodi pubblicati che sono stati spostati non devono essere duplicati con COW_FreezedEntities
            // ma solo inseriti nella lista di pubblicazione con un codice speciale
            //
            //var chg02 = DB.GetChangeSet();
            DB.SubmitChanges();
            //
            // rileggo le liste di tutti i nodi (files+folders) che devono essere marchiati come snapshot
            //
            List_vNodes =
              (from vNode in this.NodesActive<IKGD_VNODE>(true).Where(n => (!n.flag_folder && _subFoldersCodes.Contains(n.folder)) || _subFoldersCodes2.Contains(n.snode)).Where(n => !n.flag_published)
               from vData in this.NodesActive<IKGD_VDATA>(true).Where(vDataFilterAll).Where(n => n.rnode == vNode.rnode)
               select vNode);
            List_vDatas =
              (from vNode in this.NodesActive<IKGD_VNODE>(true).Where(n => (!n.flag_folder && _subFoldersCodes.Contains(n.folder)) || _subFoldersCodes2.Contains(n.snode))
               from vData in this.NodesActive<IKGD_VDATA>(true).Where(vDataFilterAll).Where(n => !n.flag_published).Where(n => n.rnode == vNode.rnode)
               select vData);
            List_iNodes =
              (from vNode in this.NodesActive<IKGD_VNODE>(true).Where(n => (!n.flag_folder && _subFoldersCodes.Contains(n.folder)) || _subFoldersCodes2.Contains(n.snode))
               from vData in this.NodesActive<IKGD_VDATA>(true).Where(vDataFilterAll).Where(n => n.rnode == vNode.rnode)
               from iNode in this.NodesActive<IKGD_INODE>(true).Where(n => !n.flag_published).Where(n => n.rnode == vNode.rnode)
               select iNode);
            //
            // trattamento diverso per i mapping 1->m
            //
            List_properties =
              (from vNode in this.NodesActive<IKGD_VNODE>(true).Where(n => (!n.flag_folder && _subFoldersCodes.Contains(n.folder)) || _subFoldersCodes2.Contains(n.snode))
               from vData in this.NodesActive<IKGD_VDATA>(true).Where(vDataFilterAll).Where(n => n.rnode == vNode.rnode)
               from prop in this.NodesActive<IKGD_PROPERTY>(true).Where(n => n.version_frozen == freeze.version_frozen).Where(n => n.rnode == vNode.rnode)
               select prop);
            List_relations =
              (from vNode in this.NodesActive<IKGD_VNODE>(true).Where(n => (!n.flag_folder && _subFoldersCodes.Contains(n.folder)) || _subFoldersCodes2.Contains(n.snode))
               from vData in this.NodesActive<IKGD_VDATA>(true).Where(vDataFilterAll).Where(n => n.rnode == vNode.rnode)
               from rel in this.NodesActive<IKGD_RELATION>(true).Where(n => n.version_frozen == freeze.version_frozen).Where(n => n.rnode == vNode.rnode)
               select rel);
            //
            foreach (IKGD_XNODE node in List_vNodes)
              freezeList.Add(new IKGD_FREEZED { version_frozen = freeze.version_frozen, node_version = node.version, node_type = 1 });
            foreach (IKGD_XNODE node in List_vDatas)
              freezeList.Add(new IKGD_FREEZED { version_frozen = freeze.version_frozen, node_version = node.version, node_type = 2 });
            foreach (IKGD_XNODE node in List_iNodes)
              freezeList.Add(new IKGD_FREEZED { version_frozen = freeze.version_frozen, node_version = node.version, node_type = 3 });
            foreach (IKGD_XNODE node in List_properties)
              freezeList.Add(new IKGD_FREEZED { version_frozen = freeze.version_frozen, node_version = node.version, node_type = 4 });
            foreach (IKGD_XNODE node in List_relations)
              freezeList.Add(new IKGD_FREEZED { version_frozen = freeze.version_frozen, node_version = node.version, node_type = 5 });
            //
          }
          //
          // i nodi pubblicati che sono stati spostati non devono essere duplicati con COW_FreezedEntities
          // ma solo inseriti nella lista di pubblicazione con un codice speciale
          //
          foreach (var _subFoldersPublished in subFoldersPublished.Slice(500))
          {
            var _subFoldersPublished_slice = _subFoldersPublished.ToList();
            var Moved_vNodes =
              (from vn1 in this.NodesActive<IKGD_VNODE>(0, false).Where(n => _subFoldersPublished_slice.Contains(n.folder))
               from vn2 in this.NodesActive<IKGD_VNODE>(-1, true).Where(n => n.snode == vn1.snode)
               where vn1.folder != vn2.folder || vn1.parent != vn2.parent
               select vn1).Distinct();
            foreach (IKGD_XNODE node in Moved_vNodes)
              freezeList.Add(new IKGD_FREEZED { version_frozen = freeze.version_frozen, node_version = node.version, node_type = 6 });
          }
          //
          DB.IKGD_FREEZEDs.InsertAllOnSubmit(freezeList);
          //var chg03 = DB.GetChangeSet();
          DB.SubmitChanges();
          freeze.affected = freezeList.Count;
          //var chg04 = DB.GetChangeSet();
          DB.SubmitChanges();
          //
          ts.Committ();
          //
          return freeze;
        }
      }
      catch { }
      return null;
    }


    public IKGD_SNAPSHOT FreezeTreeFromList(List<int> sNodes, List<int> rNodes, string message)
    {
      if ((sNodes == null || !sNodes.Any()) && (rNodes == null || !rNodes.Any()))
        return null;
      try
      {
        int timeOut = Utility.TryParse<int>(IKGD_Config.AppSettings["VFS_TimeoutDB"], 1800);
        //TODO: sarebbe forse il caso di usare IKGD_TransactionFactory.TransactionSerializable ?
        using (TransactionScope ts = IKGD_TransactionFactory.Transaction(timeOut))
        {
          if (this.DB.CommandTimeout < timeOut)
            this.DB.CommandTimeout = timeOut;
          //
          List<IKGD_Path> paths = this.PathsFromNodesAuthor(sNodes, rNodes, false);
          IKGD_Path pathInfo = paths.FirstOrDefault();
          int sNodeRoot = pathInfo.sNode;
          int sNodeFolder = pathInfo.FolderFragment.sNode;
          IKGD_VNODE vNodeRoot = this.NodeActive(sNodeRoot, true);
          //
          var sNodesAux = paths.Select(p => p.sNode).Distinct().ToList();
          //
          // ottiene un nuovo ID per il freeze
          //
          IKGD_SNAPSHOT freeze = new IKGD_SNAPSHOT { date_frozen = DateTimeContext, flag_published = false, flag_rejected = false, username = this.CurrentUser, snode_root = sNodeRoot, snode_folder = sNodeFolder, affected = 0, name = vNodeRoot.name, path = pathInfo.Path, message = message };
          DB.IKGD_SNAPSHOTs.InsertOnSubmit(freeze);
          //var chg01 = DB.GetChangeSet();
          DB.SubmitChanges();
          //
          // devo prevedere dei filtri piu' restrittivi sui ruoli (pubblicazione o pubblicazione immediata)
          //
          int acls = (int)(FS_ACL_Reduced.AclType.Publish | FS_ACL_Reduced.AclType.PublishDirect);
          List<string> activeAreas = IsRoot ? CurrentAreasExtended.AreasAllowed : DB.IKGD_ADMINs.Where(r => r.username == CurrentUser && ((r.flags_acl & acls) != 0)).Select(r => r.area).ToList();
          //
          Expression<Func<IKGD_VDATA, bool>> vDataFilterAll = PredicateBuilder.True<IKGD_VDATA>();
          vDataFilterAll = vDataFilterAll.And(n => activeAreas.Contains(n.area));  //TODO:ACL (usare ACL differenti per il folder scan [read] e per il publish [write])
          List<IKGD_VNODE> subFolders = new List<IKGD_VNODE>();
          //
          List<IKGD_FREEZED> freezeList = new List<IKGD_FREEZED>();
          //
          int idx_slice = 500;
          for (int idx = 0; idx < sNodesAux.Count; idx += idx_slice)
          {
            List<int> _sNodes = sNodesAux.Skip(idx).Take(idx_slice).ToList();
            //
            // preparo la lista di tutti i nodi (files+folders) che devono essere marchiati come snapshot
            //
            IEnumerable<IKGD_VNODE> List_vNodes =
              (from vNode in this.NodesActive<IKGD_VNODE>(true).Where(n => _sNodes.Contains(n.snode)).Where(n => !n.flag_published)
               from vData in this.NodesActive<IKGD_VDATA>(true).Where(vDataFilterAll).Where(n => n.rnode == vNode.rnode)
               select vNode);
            IEnumerable<IKGD_VDATA> List_vDatas =
              (from vNode in this.NodesActive<IKGD_VNODE>(true).Where(n => _sNodes.Contains(n.snode))
               from vData in this.NodesActive<IKGD_VDATA>(true).Where(vDataFilterAll).Where(n => !n.flag_published).Where(n => n.rnode == vNode.rnode)
               select vData);
            IEnumerable<IKGD_INODE> List_iNodes =
              (from vNode in this.NodesActive<IKGD_VNODE>(true).Where(n => _sNodes.Contains(n.snode))
               from vData in this.NodesActive<IKGD_VDATA>(true).Where(vDataFilterAll).Where(n => n.rnode == vNode.rnode)
               from iNode in this.NodesActive<IKGD_INODE>(true).Where(n => !n.flag_published).Where(n => n.rnode == vNode.rnode)
               select iNode);
            IEnumerable<IKGD_PROPERTY> List_properties =
              (from vNode in this.NodesActive<IKGD_VNODE>(true).Where(n => _sNodes.Contains(n.snode))
               from vData in this.NodesActive<IKGD_VDATA>(true).Where(vDataFilterAll).Where(n => n.rnode == vNode.rnode)
               from prop in this.NodesActive<IKGD_PROPERTY>(true).Where(n => n.rnode == vNode.rnode)
               select prop);
            IEnumerable<IKGD_RELATION> List_relations =
              (from vNode in this.NodesActive<IKGD_VNODE>(true).Where(n => _sNodes.Contains(n.snode))
               from vData in this.NodesActive<IKGD_VDATA>(true).Where(vDataFilterAll).Where(n => n.rnode == vNode.rnode)
               from rel in this.NodesActive<IKGD_RELATION>(true).Where(n => n.rnode == vNode.rnode)
               select rel);
            // considera solo le risorse che hanno almeno un elemento non pubblicato
            List_properties = List_properties.GroupBy(r => r.rnode).Where(g => g.Any(r => r.flag_published == false || r.flag_deleted == true)).SelectMany(g => g.Select(r => r));
            List_relations = List_relations.GroupBy(r => r.rnode).Where(g => g.Any(r => r.flag_published == false || r.flag_deleted == true)).SelectMany(g => g.Select(r => r));
            //
            // setto il version_frozen dove mancante e preparo la lista dei nodi freezati nello snapshot
            //
            var List_vNodesDup = COW_FreezedEntities(freeze.version_frozen, List_vNodes).ToList();
            var List_vDatasDup = COW_FreezedEntities(freeze.version_frozen, List_vDatas).ToList();
            var List_iNodesDup = COW_FreezedEntities(freeze.version_frozen, List_iNodes).ToList();
            var List_propertiesDup = COW_FreezedEntities(freeze.version_frozen, List_properties).ToList();
            var List_relationsDup = COW_FreezedEntities(freeze.version_frozen, List_relations).ToList();
            //
            // i nodi pubblicati che sono stati spostati non devono essere duplicati con COW_FreezedEntities
            // ma solo inseriti nella lista di pubblicazione con un codice speciale
            //
            //var chg02 = DB.GetChangeSet();
            DB.SubmitChanges();
            //
            // rileggo le liste di tutti i nodi (files+folders) che devono essere marchiati come snapshot
            //
            List_vNodes =
              (from vNode in this.NodesActive<IKGD_VNODE>(true).Where(n => _sNodes.Contains(n.snode)).Where(n => !n.flag_published)
               from vData in this.NodesActive<IKGD_VDATA>(true).Where(vDataFilterAll).Where(n => n.rnode == vNode.rnode)
               select vNode);
            List_vDatas =
              (from vNode in this.NodesActive<IKGD_VNODE>(true).Where(n => _sNodes.Contains(n.snode))
               from vData in this.NodesActive<IKGD_VDATA>(true).Where(vDataFilterAll).Where(n => !n.flag_published).Where(n => n.rnode == vNode.rnode)
               select vData);
            List_iNodes =
              (from vNode in this.NodesActive<IKGD_VNODE>(true).Where(n => _sNodes.Contains(n.snode))
               from vData in this.NodesActive<IKGD_VDATA>(true).Where(vDataFilterAll).Where(n => n.rnode == vNode.rnode)
               from iNode in this.NodesActive<IKGD_INODE>(true).Where(n => !n.flag_published).Where(n => n.rnode == vNode.rnode)
               select iNode);
            //
            // trattamento diverso per i mapping 1->m
            //
            List_properties =
              (from vNode in this.NodesActive<IKGD_VNODE>(true).Where(n => _sNodes.Contains(n.snode))
               from vData in this.NodesActive<IKGD_VDATA>(true).Where(vDataFilterAll).Where(n => n.rnode == vNode.rnode)
               from prop in this.NodesActive<IKGD_PROPERTY>(true).Where(n => n.version_frozen == freeze.version_frozen).Where(n => n.rnode == vNode.rnode)
               select prop);
            List_relations =
              (from vNode in this.NodesActive<IKGD_VNODE>(true).Where(n => _sNodes.Contains(n.snode))
               from vData in this.NodesActive<IKGD_VDATA>(true).Where(vDataFilterAll).Where(n => n.rnode == vNode.rnode)
               from rel in this.NodesActive<IKGD_RELATION>(true).Where(n => n.version_frozen == freeze.version_frozen).Where(n => n.rnode == vNode.rnode)
               select rel);
            //
            foreach (IKGD_XNODE node in List_vNodes)
              freezeList.Add(new IKGD_FREEZED { version_frozen = freeze.version_frozen, node_version = node.version, node_type = 1 });
            foreach (IKGD_XNODE node in List_vDatas)
              freezeList.Add(new IKGD_FREEZED { version_frozen = freeze.version_frozen, node_version = node.version, node_type = 2 });
            foreach (IKGD_XNODE node in List_iNodes)
              freezeList.Add(new IKGD_FREEZED { version_frozen = freeze.version_frozen, node_version = node.version, node_type = 3 });
            foreach (IKGD_XNODE node in List_properties)
              freezeList.Add(new IKGD_FREEZED { version_frozen = freeze.version_frozen, node_version = node.version, node_type = 4 });
            foreach (IKGD_XNODE node in List_relations)
              freezeList.Add(new IKGD_FREEZED { version_frozen = freeze.version_frozen, node_version = node.version, node_type = 5 });
            //
          }
          //
          // i nodi pubblicati che sono stati spostati non devono essere duplicati con COW_FreezedEntities
          // ma solo inseriti nella lista di pubblicazione con un codice speciale
          //
          foreach (var _subFoldersPublished in sNodesAux.Slice(500))
          {
            var _subFoldersPublished_slice = _subFoldersPublished.ToList();
            var Moved_vNodes =
              (from vn1 in this.NodesActive<IKGD_VNODE>(0, false).Where(n => _subFoldersPublished_slice.Contains(n.folder))
               from vn2 in this.NodesActive<IKGD_VNODE>(-1, true).Where(n => n.snode == vn1.snode)
               where vn1.folder != vn2.folder || vn1.parent != vn2.parent
               select vn1).Distinct();
            foreach (IKGD_XNODE node in Moved_vNodes)
              freezeList.Add(new IKGD_FREEZED { version_frozen = freeze.version_frozen, node_version = node.version, node_type = 6 });
          }
          //
          DB.IKGD_FREEZEDs.InsertAllOnSubmit(freezeList);
          //var chg03 = DB.GetChangeSet();
          DB.SubmitChanges();
          freeze.affected = freezeList.Count;
          //var chg04 = DB.GetChangeSet();
          DB.SubmitChanges();
          //
          ts.Committ();
          //
          return freeze;
        }
      }
      catch { }
      return null;
    }


    //
    // spazzola una lista di nodi selezionati per essere freezati, nel caso siano gia' stati freezati da un altro snapshot
    // viene creato al volo un duplicato (con trattamento corretto degli streams per gli iNodes)
    // viene settato correttamente anche il version_frozen per il supporto delle preview degli snapshots
    // la freezeList non verra' piu' usata per la pubblicazione ma solo per la visualizzazione del tree delle risorse freezate
    //
    protected IEnumerable<TEntity> COW_FreezedEntities<TEntity>(int version_frozen, IEnumerable<TEntity> nodes) where TEntity : class, IKGD_XNODE, new()
    {
      var table = DB.GetTable<TEntity>();
      foreach (TEntity node in nodes)
      {
        if (node.version_frozen == null || node.version_frozen == version_frozen)
        {
          if (node.version_frozen != version_frozen)
            node.version_frozen = version_frozen;
          yield return node;
          continue;
        }
        //
        // creo un clone del nodo per settare il version_frozen
        //
        //TEntity clone = CloneNode(node, true, true);
        TEntity clone = Utility.CloneEntity(node, false, true);
        clone.version_frozen = version_frozen;
        if (node.flag_current)
        {
          node.flag_current = false;
          clone.flag_current = true;
        }
        //
        // visto che il set viene duplicato e pubblicato in blocco devo pulire anche il published
        //
        if (typeof(TEntity) == typeof(IKGD_PROPERTY) || typeof(TEntity) == typeof(IKGD_RELATION))
        {
          clone.flag_published = false;
        }
        table.InsertOnSubmit(clone);
        //
        // se ho degli stream associati devo duplicarli
        //
        if (node.GetType() == typeof(IKGD_INODE))
        {
          foreach (IKGD_STREAM strm in (node as IKGD_INODE).IKGD_STREAMs)
          {
            IKGD_STREAM strmNew = CloneNode(strm, false, false);
            //
            try
            {
              string extMode = IKGD_ExternalVFS_Support.GetExternalModeFromMime(strmNew.type);
              if (string.Equals(extMode, IKGD_ExternalVFS_Support.MimePrefixExternalWithVersioning, StringComparison.OrdinalIgnoreCase))
              {
                // bisogna gestire correttamente la duplicazione della risorsa nel caso di external files with versioning
                // nel caso di versioning disabilitato per files esterni (default) non serve fare nulla!
                using (IKGD_ExternalVFS_Support extFS = new IKGD_ExternalVFS_Support())
                {
                  bool res = extFS.DupExternalFile(extMode, node, strmNew);
                }
              }
            }
            catch { }
            //
            (clone as IKGD_INODE).IKGD_STREAMs.Add(strmNew);
          }
          foreach (IKGD_MSTREAM mstrm in (node as IKGD_INODE).IKGD_MSTREAMs)
          {
            IKGD_MSTREAM mstrmNew = new IKGD_MSTREAM { stream = mstrm.stream };
            (clone as IKGD_INODE).IKGD_MSTREAMs.Add(mstrmNew);
          }
        }
        //
        yield return clone;
      }
    }


    public IEnumerable<TEntity> COW_VersioningFreezeDup<TEntity>(IEnumerable<TEntity> nodes) where TEntity : class, IKGD_XNODE, new()
    {
      var table = DB.GetTable<TEntity>();
      foreach (TEntity node in nodes)
      {
        //
        // creo un clone del nodo per settare il version_frozen
        //
        //TEntity clone = CloneNode(node, true, true);
        TEntity clone = Utility.CloneEntity(node, false, true);
        node.flag_current = false;
        clone.flag_published = false;
        clone.flag_current = true;
        clone.version_frozen = null;
        clone.version_date = this.DateTimeContext;
        clone.username = this.CurrentUser;
        //
        table.InsertOnSubmit(clone);
        //
        // se ho degli stream associati devo duplicarli
        //
        if (node.GetType() == typeof(IKGD_INODE))
        {
          foreach (IKGD_STREAM strm in (node as IKGD_INODE).IKGD_STREAMs)
          {
            IKGD_STREAM strmNew = CloneNode(strm, false, false);
            //
            try
            {
              string extMode = IKGD_ExternalVFS_Support.GetExternalModeFromMime(strmNew.type);
              if (string.Equals(extMode, IKGD_ExternalVFS_Support.MimePrefixExternalWithVersioning, StringComparison.OrdinalIgnoreCase))
              {
                // bisogna gestire correttamente la duplicazione della risorsa nel caso di external files with versioning
                // nel caso di versioning disabilitato per files esterni (default) non serve fare nulla!
                using (IKGD_ExternalVFS_Support extFS = new IKGD_ExternalVFS_Support())
                {
                  bool res = extFS.DupExternalFile(extMode, node, strmNew);
                }
              }
            }
            catch { }
            //
            (clone as IKGD_INODE).IKGD_STREAMs.Add(strmNew);
          }
          foreach (IKGD_MSTREAM mstrm in (node as IKGD_INODE).IKGD_MSTREAMs)
          {
            IKGD_MSTREAM mstrmNew = new IKGD_MSTREAM { stream = mstrm.stream };
            (clone as IKGD_INODE).IKGD_MSTREAMs.Add(mstrmNew);
          }
        }
        //
        yield return clone;
      }
    }


    //
    // revoca della pubblicazione di un freezed subtree
    //
    public IKGD_SNAPSHOT FreezedTreePublicationDeny(int version_frozen, string message)
    {
      try
      {
        int timeOut = Utility.TryParse<int>(IKGD_Config.AppSettings["VFS_TimeoutDB"], 1800);
        //TODO: sarebbe forse il caso di usare: IKGD_TransactionFactory.TransactionSerializable ?
        using (TransactionScope ts = IKGD_TransactionFactory.Transaction(timeOut))
        {
          if (this.DB.CommandTimeout < timeOut)
            this.DB.CommandTimeout = timeOut;
          //
          // ottiene il reference del freeze
          //
          IKGD_SNAPSHOT freeze = DB.IKGD_SNAPSHOTs.FirstOrDefault(r => r.version_frozen == version_frozen);
          if (freeze == null)
            return freeze;
          //
          // in caso di successo cancello la lista dei nodi pendenti
          //
          int res01 = DB.ExecuteCommand("DELETE FROM [IKGD_FREEZED] WHERE ([version_frozen]={0})", version_frozen);
          //
          freeze.validator = CurrentUser;
          freeze.date_published = DateTimeContext;
          freeze.flag_published = false;
          freeze.flag_rejected = true;
          freeze.message = message ?? string.Empty;
          //
          var chg = DB.GetChangeSet();
          DB.SubmitChanges();
          //
          ts.Committ();
          //
          return freeze;
        }
      }
      catch (Exception ex)
      {
        throw ex;
      }
    }


    //
    // pubblicazione di un freezed subtree
    //
    public IKGD_SNAPSHOT FreezedTreePublish(int version_frozen, string message)
    {
      IKGD_SNAPSHOT freeze = null;
      try
      {
        int timeOut = Utility.TryParse<int>(IKGD_Config.AppSettings["VFS_TimeoutDB"], 1800);
        //TODO: sarebbe forse il caso di usare: IKGD_TransactionFactory.TransactionSerializable ?
        using (TransactionScope ts = IKGD_TransactionFactory.Transaction(timeOut))
        {
          if (this.DB.CommandTimeout < timeOut)
            this.DB.CommandTimeout = timeOut;
          //
          // ottiene il reference del freeze
          //
          freeze = DB.IKGD_SNAPSHOTs.FirstOrDefault(r => r.version_frozen == version_frozen);
          if (freeze == null)
            return freeze;
          //
          // devo prevedere dei filtri piu' restrittivi sui ruoli (pubblicazione o pubblicazione immediata)
          //
          int acls = (int)(FS_ACL_Reduced.AclType.Publish | FS_ACL_Reduced.AclType.PublishDirect);
          List<string> activeAreas = IsRoot ? CurrentAreasExtended.AreasAllowed : DB.IKGD_ADMINs.Where(r => r.username == CurrentUser && ((r.flags_acl & acls) != 0)).Select(r => r.area).ToList();
          //
          // scan sulla lista di tutti i nodi per vedere se c'e' quanlche nodo che il validatore
          // non puo' validare, in caso lancia un'eccezione
          //
          int deny1 =
            (from fr in DB.IKGD_FREEZEDs.Where(r => r.version_frozen == version_frozen && r.node_type == 1)
             from node in DB.IKGD_VNODEs.Where(n => n.version == fr.node_version)
             from vd in DB.IKGD_VDATAs.Where(n => n.rnode == node.rnode).Where(n => (n.flag_published && n.version_frozen < version_frozen) || (n.version_frozen == version_frozen))
             where !activeAreas.Contains(vd.area)
             select node).Count();
          int deny2 =
            (from fr in DB.IKGD_FREEZEDs.Where(r => r.version_frozen == version_frozen && r.node_type == 2)
             from vd in DB.IKGD_VDATAs.Where(n => n.version == fr.node_version)
             where !activeAreas.Contains(vd.area)
             select vd).Count();
          int deny3 =
            (from fr in DB.IKGD_FREEZEDs.Where(r => r.version_frozen == version_frozen && r.node_type == 3)
             from node in DB.IKGD_INODEs.Where(n => n.version == fr.node_version)
             from vd in DB.IKGD_VDATAs.Where(n => n.rnode == node.rnode).Where(n => (n.flag_published && n.version_frozen < version_frozen) || (n.version_frozen == version_frozen))
             where !activeAreas.Contains(vd.area)
             select node).Count();
          int deny4 =
            (from fr in DB.IKGD_FREEZEDs.Where(r => r.version_frozen == version_frozen && r.node_type == 4)
             from node in DB.IKGD_PROPERTies.Where(n => n.version == fr.node_version)
             from vd in DB.IKGD_VDATAs.Where(n => n.rnode == node.rnode).Where(n => (n.flag_published && n.version_frozen < version_frozen) || (n.version_frozen == version_frozen))
             where !activeAreas.Contains(vd.area)
             select node).Count();
          int deny5 =
            (from fr in DB.IKGD_FREEZEDs.Where(r => r.version_frozen == version_frozen && r.node_type == 5)
             from node in DB.IKGD_RELATIONs.Where(n => n.version == fr.node_version)
             from vd in DB.IKGD_VDATAs.Where(n => n.rnode == node.rnode).Where(n => (n.flag_published && n.version_frozen < version_frozen) || (n.version_frozen == version_frozen))
             where !activeAreas.Contains(vd.area)
             select node).Count();
          int deny6 =
            (from fr in DB.IKGD_FREEZEDs.Where(r => r.version_frozen == version_frozen && r.node_type == 6)
             from node in DB.IKGD_VNODEs.Where(n => n.version == fr.node_version)
             from vd in DB.IKGD_VDATAs.Where(n => n.rnode == node.rnode).Where(n => (n.flag_published && n.version_frozen < version_frozen) || (n.version_frozen == version_frozen))
             where !activeAreas.Contains(vd.area)
             select node).Count();
          //
          int deniedCount = deny1 + deny2 + deny3 + deny4 + deny5 + deny6;
          if (deniedCount > 0)
            throw new Exception(string.Format("L'insieme di risorse da pubblicare contiene {0} elementi non autorizzabili dallo user corrente, contattare l'amministratore di sistema.", deniedCount));
          //
          // pubblicazione dei nodi spostati per i quali il published deve essere cancellato
          //
          var Moved_vNodes =
            (from fr in DB.IKGD_FREEZEDs.Where(r => r.version_frozen == version_frozen && r.node_type == 6)
             from node in DB.IKGD_VNODEs.Where(n => n.version == fr.node_version)
             select node).ToList();
          if (Moved_vNodes != null && Moved_vNodes.Count > 0)
          {
            //var tmp01 = Moved_vNodes.Where(n => n.flag_published).ToList();
            Moved_vNodes.Where(n => n.flag_published).ForEach(n => n.flag_published = false);
            //var chg01 = DB.GetChangeSet();
            DB.SubmitChanges();
          }
          //
          // posso procedere con la pubblicazione
          //
          FreezedTreePublishWorker<IKGD_VNODE>(version_frozen);
          FreezedTreePublishWorker<IKGD_VDATA>(version_frozen);
          FreezedTreePublishWorker<IKGD_INODE>(version_frozen);
          FreezedTreePublishWorker<IKGD_PROPERTY>(version_frozen);
          FreezedTreePublishWorker<IKGD_RELATION>(version_frozen);
          //
          // in caso di successo cancello la lista dei nodi pendenti
          //
          int res01 = DB.ExecuteCommand("DELETE FROM [IKGD_FREEZED] WHERE ([version_frozen]={0})", version_frozen);
          //
          freeze.validator = CurrentUser;
          freeze.date_published = DateTimeContext;
          freeze.flag_published = true;
          freeze.message = message ?? string.Empty;
          //
          var chg = DB.GetChangeSet();
          DB.SubmitChanges();
          //
          ts.Committ();
        }
      }
      catch (Exception ex)
      {
        throw ex;
      }
      return freeze;
    }


    private void FreezedTreePublishWorker<TEntity>(int version_frozen) where TEntity : class, IKGD_XNODE, new()
    {
      try
      {
        var table = DB.GetTable<TEntity>();
        IEnumerable<IGrouping<int, TEntity>> nodesData = null;
        if (typeof(TEntity) == typeof(IKGD_VNODE))
        {
          nodesData =
            (from node in table.Where(n => n.version_frozen == version_frozen)
             from ver in table.Where(n => (n as IKGD_VNODE).snode == (node as IKGD_VNODE).snode).Where(n => n.version_frozen == version_frozen || n.flag_published)
             select ver).Distinct().GroupBy(n => (n as IKGD_VNODE).snode);
        }
        else
        {
          nodesData =
            (from node in table.Where(n => n.version_frozen == version_frozen)
             from ver in table.Where(n => n.rnode == node.rnode).Where(n => n.version_frozen == version_frozen || n.flag_published)
             select ver).Distinct().GroupBy(n => n.rnode);
        }
        //
        foreach (IGrouping<int, TEntity> nodeData in nodesData)
        {
          int vMin = nodeData.Min(n => n.version);
          int vMax = nodeData.Max(n => n.version);
          Expression<Func<TEntity, bool>> filter = PredicateBuilder.True<TEntity>();
          filter = filter.And(n => vMin <= n.version && n.version <= vMax);
          if (typeof(TEntity) == typeof(IKGD_VNODE))
            filter = filter.And(n => nodeData.Key == (n as IKGD_VNODE).snode);
          else
            filter = filter.And(n => nodeData.Key == n.rnode);
          List<TEntity> versions = table.AsExpandable().Where(filter).ToList();
          //
          // accodo per l'eliminazione tutte le risorse che non sono pubblicate o in version_frozen
          // e quelle che sono state cancellate e non erano pubblicate oppure quelle che non sono state ancora freezate
          // e antecedenti il freeze corrente
          // lasciamo le risorse con .flag_deleted per poter ricostruire correttamente le risorse versioned
          if (Utility.TryParse<bool>(IKGD_Config.AppSettings["VFS_PurgeNotFrozenOnPublish"], true))
          {
            Expression<Func<TEntity, bool>> filter2del = PredicateBuilder.False<TEntity>();
            filter2del = filter2del.Or(n => !n.flag_published && n.version_frozen != null && n.version_frozen.Value < version_frozen);
            filter2del = filter2del.Or(n => n.version_frozen == null && n.version <= vMax);
            var records2delete = versions.Where(filter2del.Compile()).ToList();
            if (records2delete.Any())
            {
              table.DeleteAllOnSubmit(records2delete);
            }
          }
          //
          // tolgo il flag_published dalle vecchie risorse
          //
          versions.Where(n => n.flag_published == true).ForEach(n => n.flag_published = false);
          //
          // applico flag_published a tutte le risorse con version_frozen = version_frozen e non cancellate
          //
          versions.Where(n => !n.flag_deleted && n.version_frozen == version_frozen).ForEach(n => n.flag_published = true);
          versions.Where(n => n.flag_deleted && n.flag_current == true && n.version_frozen == version_frozen).ForEach(n => n.flag_current = false);
          //
        }
        //
        var chg1 = DB.GetChangeSet();
        //
        if (typeof(TEntity) == typeof(IKGD_VNODE) || typeof(TEntity) == typeof(IKGD_INODE) || typeof(TEntity) == typeof(IKGD_VDATA))
        {
          OpHandlerCOW(OpHandlerCOW_OperationEnum.Publish, OpHandlerCOW_DeserializeVfsInfoEnum.Rebind);
        }
        var chg2 = DB.GetChangeSet();
        DB.SubmitChanges();
      }
      catch
      {
        throw;
      }
    }


    // in questa libreria non abbiamo il supporto per IKCMS_RegisteredTypes quindi si deve procedere con il solo reflection support
    private static List<string> _DeserializableTypesVFS;
    protected static List<string> DeserializableTypesVFS
    {
      get
      {
        if (_DeserializableTypesVFS == null)
        {
          Type interfaceType = Utility.FindInterface("IKCMS_HasDeserializeOnVFS_Interface");
          _DeserializableTypesVFS = Utility.FindTypesWithInterfaces(interfaceType).Where(t => t.IsClass && !t.IsAbstract && !t.IsGenericType).Select(t => t.Name).ToList();
        }
        return _DeserializableTypesVFS;
      }
    }


    public virtual int DeserializeOnVFS(IKGD_VDATA vData)
    {
      int itemsCount = 0;
      try
      {
        if (Utility.TryParse<bool>(IKGD_Config.AppSettings["IKGD_DeserializeOnVFS_Enabled"], false))
        {
          if (vData != null && DeserializableTypesVFS.Contains(vData.manager_type))
          {
            Type resourceType = Utility.FindTypeCached(vData.manager_type);
            if (resourceType != null)
            {
              PropertyInfo settingsProperty = resourceType.GetProperty("ResourceSettings");
              if (settingsProperty != null)
              {
                Type settingsType = settingsProperty.PropertyType;
                object resourceSettingsObject = IKGD_Serialization.DeSerializeJSON(settingsType, vData.settings, null);
                if (resourceSettingsObject != null)
                {
                  foreach (PropertyInfo pi in settingsType.GetProperties())
                  {
                    itemsCount += DeserializeOnVFS_Worker(vData, pi.GetValue(resourceSettingsObject, null), pi, 0, false);
                  }
                }
              }
            }
          }
        }
      }
      catch { }
      return itemsCount;
    }


    protected int DeserializeOnVFS_Worker(IKGD_VDATA vData, object property, PropertyInfo propertyInfo, int level, bool forceScan)
    {
      int itemsCount = 0;
      if (property == null || propertyInfo == null)
        return itemsCount;
      try
      {
        string pname = propertyInfo.Name;
        var attrs = propertyInfo.GetCustomAttributes(true);
        IKGD_DeserializeOnVFS_Attribute_Interface attr = propertyInfo.GetCustomAttributes(typeof(IKGD_DeserializeOnVFS_Attribute_Interface), true).OfType<IKGD_DeserializeOnVFS_Attribute_Interface>().FirstOrDefault();
        if (attr != null && attr.DeserializerType != null)
        {
          IKGD_DeserializeOnVFS_Interface deserializer = Activator.CreateInstance(attr.DeserializerType) as IKGD_DeserializeOnVFS_Interface;
          if (deserializer != null)
          {
            itemsCount += deserializer.DeserializeOnVFS(this, vData, property, propertyInfo, attr, level);
          }
          return itemsCount;
        }
        if (forceScan || (attr != null && attr.AllowRecursion))
        {
          property.GetType().GetProperties().ForEach(p => itemsCount += DeserializeOnVFS_Worker(vData, p.GetValue(property, null), p, level + 1, false));
        }
      }
      catch { }
      return itemsCount;
    }


    public virtual int DeserializeOnVFS_Manager(IEnumerable<IKGD_VDATA> vDatas, OpHandlerCOW_OperationEnum opType, OpHandlerCOW_DeserializeVfsInfoEnum deserInfo)
    {
      int counterItems = 0;
      if (!Utility.TryParse<bool>(IKGD_Config.AppSettings["IKGD_DeserializeOnVFS_Enabled"], false))
        return counterItems;
      try
      {
        //
        if (deserInfo == OpHandlerCOW_DeserializeVfsInfoEnum.None)
        {
          return counterItems;
        }
        //
        List<IKGD_VDATA_KEYVALUE> keyValuesAll = new List<IKGD_VDATA_KEYVALUE>();
        List<IKGD_VDATA> vDatasAll = DB.GetChangeSet().Inserts.OfType<IKGD_VDATA>().Union(DB.GetChangeSet().Updates.OfType<IKGD_VDATA>()).Union(vDatas).ToList();
        foreach (var rNodeSet in vDatasAll.Select(n => n.rnode).Distinct().Slice(100))
        {
          var vDatasExt = DB.IKGD_VDATAs.Where(r => (r.flag_published || r.flag_current) && !r.flag_deleted && !r.flag_inactive).Where(r => rNodeSet.Contains(r.rnode) && DeserializableTypesVFS.Contains(r.manager_type));
          vDatasAll = vDatasAll.Union(vDatasExt).ToList();
        }
        foreach (var rNodeSet in vDatasAll.Select(n => n.rnode).Distinct().Slice(100))
        {
          keyValuesAll.AddRange(DB.IKGD_VDATA_KEYVALUEs.Where(r => rNodeSet.Contains(r.rNode)));
        }
        //
        if (deserInfo == OpHandlerCOW_DeserializeVfsInfoEnum.Rebind)
        {
          if (vDatasAll.All(r => r.version != 0))
          {
            //
            // TODO:
            // special pattern senza rebuild dei dati
            //
          }
        }
        //
        foreach (var vDatasGrp in vDatasAll.GroupBy(n => n.rnode))
        {
          int rNode = vDatasGrp.Key;
          var vDatasCurrent = vDatasGrp.Where(r => (r.flag_published || r.flag_current) && !r.flag_deleted && !r.flag_inactive).Where(r => DeserializableTypesVFS.Contains(r.manager_type)).OrderByDescending(r => r.version == 0 ? int.MaxValue : r.version).ToList();
          var vDatasActive = new List<IKGD_VDATA>() { vDatasCurrent.FirstOrDefault(r => r.flag_current), vDatasCurrent.FirstOrDefault(r => r.flag_published) }.Where(r => r != null).Distinct().OrderByDescending(r => r.version == 0 ? int.MaxValue : r.version).ToList();
          //
          var keyvalues = keyValuesAll.Where(r => r.rNode == rNode).ToList();
          var versions = vDatasActive.Where(n => n.version != 0).Select(n => n.version).Distinct().ToList();
          // cancellazione di tutte i keyvalues con mapping non piu' validi
          if (keyvalues.Any(r => !versions.Contains(r.vDataVersion)))
          {
            DB.IKGD_VDATA_KEYVALUEs.DeleteAllOnSubmit(keyvalues.Where(r => !versions.Contains(r.vDataVersion)));
          }
          foreach (var vData in vDatasActive)
          {
            try
            {
              bool flag_published = vData.flag_published && !vData.flag_inactive;
              bool flag_current = vData.flag_current && !vData.flag_deleted && !vData.flag_inactive;
              bool rebuild = false;
              if (keyvalues.Any(r => r.vDataVersion == vData.version && (vData.version_date > r.modif || r.flag_published != flag_published || r.flag_current != flag_current)))
              {
                rebuild = true;
                DB.IKGD_VDATA_KEYVALUEs.DeleteAllOnSubmit(keyvalues.Where(r => r.vDataVersion == vData.version));
              }
              else if (!keyvalues.Any(r => r.vDataVersion == vData.version))
              {
                rebuild = true;
              }
              if (rebuild)
              {
                counterItems += DeserializeOnVFS(vData);
              }
            }
            catch { }
          }
          //
        }
      }
      catch { }
      return counterItems;
    }


    //
    // rebuild/update dei dati deserializzati da IKGD_VDATA.settings
    // monitoraggio operazioni:
    // SELECT * FROM IKGD_VDATA_KEYVALUE WITH (NOLOCK);
    //
    public virtual List<string> DeserializeOnVFS_UpdateAll(bool fullClean)
    {
      List<string> messages = new List<string>();
      EnsureOpenConnection();
      // lasciando attiva la transaction combina un gran casino con risorse raddoppiate
      //using (TransactionScope ts = IKGD_TransactionFactory.Transaction(7200))
      {
        try
        {
          DB.CommandTimeout = 3600;
          IKCMS_ApplicationStatus.StatusSet("batch_progress", "DeserializeOnVFS_UpdateAll START".FormatString());
          //
          if (fullClean)
          {
            IKCMS_ApplicationStatus.StatusSet("batch_progress", "DeserializeOnVFS_UpdateAll deleting all deserialized records".FormatString());
            int res01 = DB.ExecuteCommand("TRUNCATE TABLE [IKGD_VDATA_KEYVALUE]");
            IKCMS_ApplicationStatus.StatusSet("batch_progress", "DeserializeOnVFS_UpdateAll deleting all deserialized records: DONE".FormatString());
          }
          //
          if (Utility.TryParse<bool>(IKGD_Config.AppSettings["IKGD_DeserializeOnVFS_Enabled"], false))
          {
            var types = DeserializableTypesVFS.ToList();
            var vDatas = DB.IKGD_VDATAs.Where(r => (r.flag_published || r.flag_current) && !r.flag_deleted && !r.flag_inactive).Where(r => types.Contains(r.manager_type));
            int counterAll = vDatas.Count();
            int counter = 0;
            //
            //var rNodesDebug = new int[] { 13686, 15027, 15524, 17648 };
            //vDatas = vDatas.Where(n => rNodesDebug.Contains(n.rnode));
            //
            //foreach (var vDataGrp in vDatas.GroupBy(n => n.rnode).Where(r => rNodesDebug.Contains(r.Key)))
            foreach (var vDataGrp in vDatas.GroupBy(n => n.rnode))
            {
              var keyvalues = DB.IKGD_VDATA_KEYVALUEs.Where(r => r.rNode == vDataGrp.Key).ToList();
              var versions = vDataGrp.Select(n => n.version).Distinct().ToList();
              // cancellazione di tutte i keyvalues con mapping non validi
              if (keyvalues.Any(r => !versions.Contains(r.vDataVersion)))
              {
                DB.IKGD_VDATA_KEYVALUEs.DeleteAllOnSubmit(keyvalues.Where(r => !versions.Contains(r.vDataVersion)));
              }
              bool sync = false;
              foreach (var vData in vDataGrp)
              {
                if (++counter % 100 == 0)
                  sync = true;
                try
                {
                  //
                  IKCMS_ApplicationStatus.StatusSet("batch_progress", "DeserializeOnVFS_UpdateAll processing rNode={0}  {1}/{2}".FormatString(vDataGrp.Key, counter, counterAll));
                  //
                  bool flag_published = vData.flag_published && !vData.flag_inactive;
                  bool flag_current = vData.flag_current && !vData.flag_deleted && !vData.flag_inactive;
                  bool rebuild = false;
                  if (keyvalues.Any(r => r.vDataVersion == vData.version && (vData.version_date > r.modif || r.flag_published != flag_published || r.flag_current != flag_current)))
                  {
                    rebuild = true;
                    DB.IKGD_VDATA_KEYVALUEs.DeleteAllOnSubmit(keyvalues.Where(r => r.vDataVersion == vData.version));
                  }
                  else if (!keyvalues.Any(r => r.vDataVersion == vData.version))
                  {
                    rebuild = true;
                  }
                  if (rebuild)
                  {
                    int items = DeserializeOnVFS(vData);
                  }
                }
                catch (Exception ex) { messages.Add(ex.Message); }
              }
              if (sync)
              {
                var chg = DB.GetChangeSet();
                IKCMS_ApplicationStatus.StatusSet("batch_progress", "DeserializeOnVFS_UpdateAll insert/updates/deletes: {0}/{1}/{2}  progress: {3}/{4}".FormatString(chg.Inserts.Count, chg.Updates.Count, chg.Deletes.Count, counter, counterAll));
                DB.SubmitChanges();
              }
            }
            {
              var chg = DB.GetChangeSet();
              IKCMS_ApplicationStatus.StatusSet("batch_progress", "DeserializeOnVFS_UpdateAll insert/updates/deletes: {0}/{1}/{2}  progress: {3}/{4}".FormatString(chg.Inserts.Count, chg.Updates.Count, chg.Deletes.Count, counter, counterAll));
              DB.SubmitChanges();
            }
          }
          //
          //ts.Committ();
          //
        }
        catch (Exception ex) { messages.Add(ex.Message); }
      }
      return messages;
    }



    public virtual void OpHandlerCOW(OpHandlerCOW_OperationEnum opType, OpHandlerCOW_DeserializeVfsInfoEnum deserInfo)
    {
      ChangeSet chgSet = DB.GetChangeSet();
      var nodes = chgSet.Inserts.Concat(chgSet.Updates).Concat(chgSet.Deletes);  // chgSet.Deletes non dovrebbe servire ma se abbiamo avuto dei processing non standard (opType.Custom)...
      OpHandlerCOW(opType, deserInfo, nodes.OfType<IKGD_VNODE>(), nodes.OfType<IKGD_VDATA>(), nodes.OfType<IKGD_INODE>(), true, true);
    }
    public virtual void OpHandlerCOW(OpHandlerCOW_OperationEnum opType, OpHandlerCOW_DeserializeVfsInfoEnum deserInfo, IEnumerable<IKGD_VNODE> vNodes, IEnumerable<IKGD_VDATA> vDatas, IEnumerable<IKGD_INODE> iNodes) { OpHandlerCOW(opType, deserInfo, vNodes, vDatas, iNodes, true, true); }
    public virtual void OpHandlerCOW(OpHandlerCOW_OperationEnum opType, OpHandlerCOW_DeserializeVfsInfoEnum deserInfo, IEnumerable<IKGD_VNODE> vNodes, IEnumerable<IKGD_VDATA> vDatas, IEnumerable<IKGD_INODE> iNodes, bool submitOnEnter, bool submitOnExit)
    {
      try
      {
        if (submitOnEnter)
        {
          //var chg = DB.GetChangeSet();
          DB.SubmitChanges();
        }
        //
        if (vDatas != null && vDatas.Any())
        {
          int keyvalues_updated = DeserializeOnVFS_Manager(vDatas, opType, deserInfo);
        }
        //
        if (opType == OpHandlerCOW_OperationEnum.None || opType == OpHandlerCOW_OperationEnum.Custom)
        {
          return;
        }
        //
        IKGD_QueuedOpDataVFS qData = new IKGD_QueuedOpDataVFS();
        qData.opType = opType;
        try
        {
          List<int> affected = new List<int>();
          if (vNodes != null)
          {
            qData.sNodes.AddRange(vNodes.Select(n => n.snode).Distinct());
            affected.AddRange(vNodes.Select(n => n.rnode));
          }
          if (iNodes != null)
          {
            qData.iNodes.AddRange(iNodes.Select(n => n.version).Distinct());
            affected.AddRange(iNodes.Select(n => n.rnode));
          }
          if (vDatas != null)
          {
            qData.rNodes.AddRange(vDatas.Select(n => n.rnode).Distinct());
            affected.AddRange(vDatas.Select(n => n.rnode));
          }
          qData.rNodesAffected.AddRange(affected.Distinct());
          //
          if (qData.rNodesAffected.Any())
          {
            IKGD_QueueData qd = new IKGD_QueueData { Data = qData.Serialize(), Log = null };
            IKGD_QueueMeta qm = new IKGD_QueueMeta
            {
              Application = IKGD_Config.ApplicationName,
              ApplicationInstanceHash = System.Web.HttpContext.Current.Application.GetHashCode(),
              ProcessingDateTime = null,
              QueueDateTime = DateTime.Now,
              StatusEnum = IKGD_QueueMetaStatusEnum.Queued,
              Title = opType.ToString(),
              TypeEnum = IKGD_QueueMetaTypeEnum.VFS_COW
            };
            qm.IKGD_QueueData = qd;
            this.DB.IKGD_QueueMetas.InsertOnSubmit(qm);
            //
            // notifica al queue manager dei nuovi dati inseriti
            IKGD_QueueManager.NotifyNewEntry();
            //
          }
          //
          if (submitOnExit)
          {
            //var chg = DB.GetChangeSet();
            DB.SubmitChanges();
          }
          //
        }
        catch { }
      }
      catch { }
    }




  }


}

