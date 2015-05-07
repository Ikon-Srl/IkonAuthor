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
using LinqKit;

using System.Reflection;
using System.Data.Linq.Mapping;
using System.Diagnostics;
using System.Linq.Dynamic;
using System.Transactions;
using System.Web.Caching;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;  // assembly System.ServiceModel.Web

using Ikon;
using Ikon.GD;
using Ikon.Log;


/// <summary>
/// Summary description for IkonGD_dataBase
/// </summary>

namespace Ikon.GD
{

  public enum OpHandlerCOW_OperationEnum { None, Unlink, Undelete, Update, Publish, Custom };

  public enum OpHandlerCOW_DeserializeVfsInfoEnum { None, Rebind, Full };


  public class FS_FileInfo
  {
    //
    // parametri dal VNODE
    //
    private readonly IKGD_VNODE DB_vNode;
    private readonly IKGD_RNODE DB_rNode;
    private readonly IKGD_SNODE DB_sNode;
    private readonly IKGD_VDATA DB_vData;
    private readonly IKGD_INODE DB_iNode;
    private readonly List<IKGD_PROPERTY> DB_Properties;
    private readonly List<IKGD_RELATION> DB_Relations;
    private FS_ACL_Reduced DB_ACL;
    private IKGD_VNODE DB_parentFolder;

    public IKGD_VNODE vNode { get { return DB_vNode; } }
    public IKGD_RNODE rNode { get { return DB_rNode; } }
    public IKGD_SNODE sNode { get { return DB_sNode; } }
    public IKGD_VDATA vData { get { return DB_vData; } }
    public IKGD_INODE iNode { get { return DB_iNode; } }
    public List<IKGD_PROPERTY> Properties { get { return DB_Properties; } }
    public List<IKGD_RELATION> Relations { get { return DB_Relations; } }
    public FS_ACL_Reduced ACL { get { return DB_ACL; } set { DB_ACL = value; } }
    public IKGD_VNODE ParentFolder { get { return DB_parentFolder; } set { DB_parentFolder = value; } }
    public int ChildCount { get; set; }
    public bool HasTaintedDependancies { get; set; }
    public object Data { get; set; }


    public string Name { get { return DB_vNode.name; } }
    public string FullName { get; set; }   // path completo da aggiornare manualmente
    public double Position { get { return DB_vNode.position; } }

    public DateTime TimeReference { get { return (DB_vData != null) ? DB_vData.date_node : DateTime.MinValue; } }
    public DateTime TimeCreation { get { return DB_rNode.date_creat; } }
    public DateTime? TimeActivation { get { return (DB_vData != null) ? DB_vData.date_activation : null; } }
    public DateTime? TimeExpiry { get { return (DB_vData != null) ? DB_vData.date_expiry : null; } }

    public DateTime TimeLastWrite
    {
      get
      {
        DateTime date = Utility.Max(DB_vData.version_date, DB_vNode.version_date);
        if (DB_iNode != null)
          date = Utility.Max(date, DB_iNode.version_date);
        if (Properties != null)
          Properties.ForEach(r => date = Utility.Max(date, r.version_date));
        if (Relations != null)
          Relations.ForEach(r => date = Utility.Max(date, r.version_date));
        return date;
      }
    }


    public string UserLastWrite
    {
      get
      {
        DateTime date = DB_vNode.version_date;
        string username = DB_vNode.username;
        if (DB_vData != null && DB_vData.version_date > date)
        {
          username = DB_vData.username;
          date = DB_vData.version_date;
        }
        if (DB_iNode != null && DB_iNode.version_date > date)
        {
          username = DB_iNode.username;
          date = DB_iNode.version_date;
        }
        if (Properties != null)
        {
          foreach (var r in Properties)
          {
            if (r.version_date > date)
            {
              username = r.username;
              date = r.version_date;
            }
          }
        }
        if (Relations != null)
        {
          foreach (var r in Relations)
          {
            if (r.version_date > date)
            {
              username = r.username;
              date = r.version_date;
            }
          }
        }
        return username;
      }
    }


    public DateTime? TimeLocked { get { return DB_rNode.locked; } }
    public string LockedBy { get { return DB_rNode.locked_by; } }
    public bool IsLocked { get { return DB_rNode.locked.HasValue; } }

    public bool IsFolder { get { return DB_vNode.flag_folder; } }
    public bool IsPublished { get { return DB_vNode.flag_published; } }
    public bool IsCurrent { get { return DB_vNode.flag_current; } }
    public bool IsFrozen { get { return DB_vNode.version_frozen.HasValue; } }
    public bool IsDeleted { get { return DB_vNode.flag_deleted; } }
    public bool IsInactive { get { return (DB_vData != null) ? DB_vData.flag_inactive : false; } }

    public string ManagerType { get { return (DB_vData != null) ? DB_vData.manager_type : null; } }
    //public string CollectorType { get { return (DB_vData != null) ? DB_vData.collector : null; } }
    public string Category { get { return (DB_vData != null) ? DB_vData.category : null; } }
    public string Key { get { return (DB_vData != null) ? DB_vData.key : null; } }
    public string Area { get { return (DB_vData != null) ? DB_vData.area : null; } }
    public string Placeholder { get { return (DB_vNode != null) ? DB_vNode.placeholder : null; } }
    public string Template { get { return (DB_vNode != null) ? DB_vNode.template : null; } }

    public string UserCreator { get { return DB_rNode.username; } }
    public string User_vData { get { return (DB_vData != null) ? DB_vData.username : null; } }
    public string User_vNode { get { return DB_vNode.username; } }
    public string User_rNode { get { return DB_rNode.username; } }
    public string User_sNode { get { return DB_sNode.username; } }

    public int Version { get { return DB_vNode.version; } }
    public int? VersionFrozen { get { return DB_vNode.version_frozen; } }
    public int? Code_Parent { get { return DB_vNode.parent; } }
    public int Code_RNODE { get { return DB_vNode.rnode; } }
    public int Code_SNODE { get { return DB_vNode.snode; } }
    public int Code_VNODE { get { return DB_vNode.version; } }
    public int Code_FOLDER { get { return DB_vNode.folder; } }

    public string Language { get { return DB_vNode.language; } }
    public virtual bool LanguageCheck(string languageMain) { return (languageMain == null || Language == null || string.Equals(Language, languageMain)); }

    public string UploadedName { get { return (DB_iNode != null) ? DB_iNode.filename : null; } }
    public string UploadedMime { get { return (DB_iNode != null) ? DB_iNode.mime : null; } }

    public bool IsPublishedAll
    {
      get
      {
        bool result = DB_vNode.flag_published && (HasTaintedDependancies == false);
        if (result && DB_vData != null)
          result &= DB_vData.flag_published;
        if (result && DB_iNode != null)
          result &= DB_iNode.flag_published;
        if (result && DB_Properties != null)
          foreach (var n in DB_Properties)
            result &= n.flag_published;
        if (result && DB_Relations != null)
          foreach (var n in DB_Relations)
            result &= n.flag_published;
        return result;
      }
    }

    private FS_FileInfo() { }

    public FS_FileInfo(IKGD_VNODE vFolder, IKGD_VNODE vNode, IKGD_INODE iNode, IKGD_VDATA vData, IEnumerable<IKGD_PROPERTY> vProperties, IEnumerable<IKGD_RELATION> vRelations, bool getACLs)
    {
      DB_vNode = vNode;
      DB_sNode = vNode.IKGD_SNODE;
      DB_rNode = vNode.IKGD_RNODE;
      DB_vData = vData;
      DB_iNode = iNode;
      DB_Properties = (vProperties != null) ? vProperties.ToList() : null;
      DB_Relations = (vRelations != null) ? vRelations.ToList() : null;
      DB_parentFolder = vFolder;
      //
      // wrap for root access
      //
      if (FS_OperationsHelpers.IsRoot)
      {
        DB_ACL = FS_ACL_Reduced.GetRootACLs(vNode);
        return;
      }
      //
      if (getACLs && vData != null)
        DB_ACL = new FS_ACL_Reduced(null, vData.area);
      else
        DB_ACL = new FS_ACL_Reduced(FS_ACL_Reduced.NoneACLs);
      //
    }

    public FS_FileInfo(IKGD_VNODE vFolder, FS_Operations.FS_NodeInfo_Interface fsNode, bool getACLs)
      : this(vFolder, fsNode.vNode, fsNode.iNode, fsNode.vData, null, null, getACLs)
    { }

    public FS_FileInfo(IKGD_VNODE vFolder, FS_Operations.FS_NodeInfoExt_Interface fsNode, bool getACLs)
      : this(vFolder, fsNode.vNode, fsNode.iNode, fsNode.vData, fsNode.Properties, fsNode.Relations, getACLs)
    { }


    //
    // verifica che un nome sia valido per il VFS
    // (non testa un po' di tutto ma solo che non abbia caratteri di controllo)
    //
    public static bool ValidateName(string fileName)
    {
      //return fileName.IndexOfAny(System.IO.Path.GetInvalidFileNameChars().Union(System.IO.Path.GetInvalidPathChars()).ToArray()) == -1;
      foreach (char c in fileName)
        if (char.IsControl(c))
          return false;
      return true;
    }

  }


  //
  // classe ausiliaria per la gestione delle relazioni
  //
  public class FS_RelationInfo
  {
    public int rNodeCodeSRC { get; set; }
    public int rNodeCode { get; set; }
    public int rNodeCodeAuto { get { return fsNode != null ? fsNode.Code_RNODE : rNodeCode; } }
    public int sNodeCodeSRC { get; set; }
    public int sNodeCode { get; set; }
    public int sNodeCodeAuto { get { return fsNode != null ? fsNode.Code_SNODE : sNodeCode; } }
    public string type { get; set; }
    public double position { get; set; }
    public int? version { get; set; }
    //
    public string name { get; set; }
    public string path { get; set; }
    public FS_FileInfo fsNode { get; set; }
    //
    public bool IsDeleted { get; set; }
    public bool IsTainted { get; set; }
    public bool HasTaintedName { get { return (fsNode != null && fsNode.vNode != null) && (fsNode.vNode.name != this.name); } }

    public FS_RelationInfo()
    {
      IsDeleted = false;
      IsTainted = false;
    }

    public FS_RelationInfo(IKGD_RELATION relNode, FS_FileInfo fsNode)
      : this()
    {
      this.rNodeCodeSRC = relNode.rnode;
      this.rNodeCode = relNode.rnode_dst;
      this.sNodeCodeSRC = relNode.snode_src;
      this.sNodeCode = relNode.snode_dst;
      this.type = relNode.type;
      this.position = relNode.position;
      this.version = relNode.version;
      this.IsDeleted = relNode.flag_deleted;
      this.fsNode = fsNode;
      this.name = fsNode != null ? fsNode.vNode.name : null;
    }

    //public string Serialize() { return Ikon_Serialization.SerializeDC<FS_RelationInfo>(this); }
    //public static FS_RelationInfo UnSerialize(string json) { return Ikon_Serialization.UnSerializeDC<FS_RelationInfo>(json); }

    public override string ToString()
    {
      return string.Format("{0} - [T{1}-D{2}] R[{3}|{4}] S[{5}|{6}] - {7} - {8}", version, IsTainted ? 1 : 0, IsDeleted ? 1 : 0, rNodeCodeSRC, rNodeCode, sNodeCodeSRC, sNodeCode, type, position);
    }

  }


  //
  // classe ausiliaria per la gestione delle properties
  //
  public class FS_PropertyInfo
  {
    //
    public int? version { get; set; }
    public int rNodeCode { get; set; }
    public string name { get; set; }
    public string value { get; set; }
    //public string data { get; set; }
    public int? attributeId { get; set; }
    //public FS_FileInfo fsNode { get; set; }
    //
    public bool IsDeleted { get; set; }
    public bool IsTainted { get; set; }

    public FS_PropertyInfo()
    {
      IsDeleted = false;
      IsTainted = false;
    }

    public FS_PropertyInfo(IKGD_PROPERTY propNode)
      : this()
    {
      this.rNodeCode = propNode.rnode;
      this.version = propNode.version;
      this.IsDeleted = propNode.flag_deleted;
      this.name = propNode.name;
      this.value = propNode.value;
      //this.data = propNode.data;
      this.attributeId = propNode.attributeId;
      this.IsDeleted = propNode.flag_deleted;
    }


    public override string ToString()
    {
      return string.Format("{0} - [T{1}-D{2}]/{3} - {4} - attr:{5} --> {6}", version, IsTainted ? 1 : 0, IsDeleted ? 1 : 0, rNodeCode, name, attributeId, value);
    }

  }


  public class FS_ACL_Reduced
  {
    [Flags]  // non sembra funzionare automaticamente....
    public enum AclType
    {
      Read = 1 << 0,
      Write = 1 << 1,
      Delete = 1 << 2,
      Properties = 1 << 3,
      ACL = 1 << 4,
      Publish = 1 << 5,
      PublishDirect = 1 << 6,
      Validator = 1 << 7,
      DnD_Inside = 1 << 8,
      DnD_Outside = 1 << 9,
      CreateFiles = 1 << 10,
      CreateFolders = 1 << 11
    };
    //
    public const AclType NoneACLs = (AclType)0;
    public const AclType DefaulACLs = AclType.Read;
    public const AclType RootACLs = (AclType)0x7fffffff;
    //
    public AclType Flags { get; private set; }


    // non voglio costruttori senza argomenti
    private FS_ACL_Reduced() { }

    public FS_ACL_Reduced(IKGD_DataContext DB, string area)
    {
      //caching migliorato
      Flags = NoneACLs;
      string cacheKey = FS_OperationsHelpers.cacheBaseName + Ikon.GD.MembershipHelper.UserName + "_UserDataACLs";
      DictionaryMV_Cachable<string, AclType> data = FS_OperationsHelpers.CachedEntityWrapper(cacheKey, () => { return GetUserACLData(DB); }, Utility.TryParse<int?>(IKGD_Config.AppSettings["CachingACLs"], 3600), new string[] { "IKGD_ADMIN" });
      if (data != null && data.ContainsKey(area))
        Flags = data[area];
    }

    public FS_ACL_Reduced(AclType baseFlags) { Flags = baseFlags; }


    public static FS_ACL_Reduced GetRootACLs(IKGD_hasFolderInfo vNode)
    {
      AclType mask = ((AclType)0x7fffffff ^ AclType.CreateFiles) ^ AclType.CreateFolders;
      if (vNode.flag_folder)
        mask |= AclType.CreateFiles | AclType.CreateFolders;
      return new FS_ACL_Reduced(mask);
    }


    //
    public static bool HasOperatorACLs() { return HasOperatorACLs(~(int)(AclType.Read)); }
    public static bool HasOperatorACLsCached() { return HasOperatorACLsCached(~(int)(AclType.Read)); }
    //
    public static bool HasOperatorACLs(int mask) { return GetUserACLData().Any(r => ((int)r.Value & mask) != 0); }
    public static bool HasOperatorACLsCached(int mask) { return GetUserACLDataCached().Any(r => ((int)r.Value & mask) != 0); }
    //
    // lista delle ACL mappate per l'utente (attenzione che c'e' anche AclType.Read!!!)
    public static DictionaryMV_Cachable<string, AclType> GetUserACLDataCached() { return FS_OperationsHelpers.CachedEntityWrapper("UserACLData_" + (Ikon.GD.MembershipHelper.UserName ?? "anonymous"), () => { return GetUserACLData(null); }, 3600, new string[] { "IKGD_ADMIN" }); }
    public static DictionaryMV_Cachable<string, AclType> GetUserACLData() { return GetUserACLData(null); }
    public static DictionaryMV_Cachable<string, AclType> GetUserACLData(IKGD_DataContext DB)
    {
      //
      DictionaryMV_Cachable<string, AclType> areasACL = new DictionaryMV_Cachable<string, AclType>();
      //
      string user = Ikon.GD.MembershipHelper.UserName ?? "anonymous";
      DB = DB ?? IKGD_DBH.GetDB();  // verra' usato raramente non utilizzo lo using
      var areasAll = FS_OperationsHelpers.CachedAreasExtended.AreasAllowed;
      var areasDB = DB.IKGD_ADMINs.Where(r => r.username == user).Select(r => new { area = r.area, acl = r.flags_acl }).ToList();
      foreach (string area in areasAll)
        areasACL[area] = FS_OperationsHelpers.IsRoot ? FS_ACL_Reduced.RootACLs : FS_ACL_Reduced.DefaulACLs;
      foreach (var acl in areasDB)
      {
        if (areasACL.ContainsKey(acl.area))
        {
          areasACL[acl.area] |= (AclType)acl.acl;
        }
        else
        {
          //non assegno i privilegi per una acl di amministrazione rimasta sul VFS ma non piu' assegnata all'utente
        }
      }
      return areasACL;
    }


    public bool Has_Read { get { return (Flags & AclType.Read) != 0; } }
    public bool Has_Write { get { return (Flags & AclType.Write) != 0; } }
    public bool Has_Delete { get { return (Flags & AclType.Delete) != 0; } }
    public bool Has_Properties { get { return (Flags & AclType.Properties) != 0; } }
    public bool Has_ACL { get { return (Flags & AclType.ACL) != 0; } }
    public bool Has_Publish { get { return (Flags & AclType.Publish) != 0; } }
    public bool Has_PublishDirect { get { return (Flags & AclType.PublishDirect) != 0; } }
    public bool Has_Validator { get { return (Flags & AclType.Validator) != 0; } }
    public bool Has_DnD_Inside { get { return (Flags & AclType.DnD_Inside) != 0; } }
    public bool Has_DnD_Outside { get { return (Flags & AclType.DnD_Outside) != 0; } }
    public bool Has_CreateFiles { get { return (Flags & AclType.CreateFiles) != 0; } }
    public bool Has_CreateFolders { get { return (Flags & AclType.CreateFolders) != 0; } }

  }



  public class FS_Areas_Extended
  {
    public enum AreaMatchModeEnum { FilterByAllowed, FilterByDenied, FilterNone }
    public AreaMatchModeEnum AreaMatchMode { get; set; }
    public List<string> AreasAllowed { get; set; }
    public List<string> AreasAllowedNotPublic { get; set; }
    public List<string> AreasDenied { get; set; }


    public FS_Areas_Extended()
    {
      AreasAllowed = new List<string>();
      AreasAllowedNotPublic = new List<string>();
      AreasDenied = new List<string>();
      AreaMatchMode = AreaMatchModeEnum.FilterByAllowed;
    }


    public FS_Areas_Extended(IEnumerable<string> allowdAreas)
    {
      AreasAllowed = new List<string>(allowdAreas);
      AreasAllowedNotPublic = new List<string>();
      AreasDenied = new List<string>();
      AreaMatchMode = AreaMatchModeEnum.FilterByAllowed;
    }


    public FS_Areas_Extended(string userName, bool forceRoot, IKGD_DataContext DB)
    {
      try
      {
        List<string> res = null;
        List<string> res2 = null;
        try
        {
          if (forceRoot)
          {
            res = Ikon.Auth.Roles_IKGD.Provider.GetAllAreas().ToList();
            res2 = Ikon.Auth.Roles_IKGD.Provider.GetAllAreasNotPublic().ToList();
            List<string> areasDB = null;
            try
            {
              if (DB != null)
              {
                areasDB = DB.IKGD_VDATAs.Select(n => n.area).Distinct().ToList();
              }
              else
              {
                using (IKGD_DataContext tmpDB = IKGD_DBH.GetDB())
                {
                  FS_Operations.EnsureOpenConnection(tmpDB);
                  bool useRealTransactions = Utility.TryParse<bool>(IKGD_Config.AppSettings["IKGD_ConfigVFS_TransactionsEnabled"], false);
                  using (System.Transactions.TransactionScope ts = useRealTransactions ? IKGD_TransactionFactory.TransactionReadUncommitted(600) : IKGD_TransactionFactory.TransactionNone(600))
                  {
                    areasDB = tmpDB.IKGD_VDATAs.Select(n => n.area).Distinct().ToList();
                    ts.Committ(); // serve solo per non lasciare una transaction incompleta che incasinerebbe le transaction di livello superiore
                  }
                }
              }
              res = res.Union(areasDB).ToList();
            }
            catch { }
          }
          else
          {
            res = Ikon.Auth.Roles_IKGD.Provider.GetAreasForUser(userName).ToList();
            res2 = res.Except(Ikon.Auth.Roles_IKGD.Provider.AreasPublic.Select(a => a.Name).Distinct(StringComparer.OrdinalIgnoreCase)).ToList();
          }
        }
        catch { }
        AreasAllowed = res ?? new List<string>();
        AreasAllowedNotPublic = res2 ?? new List<string>();
        AreasDenied = Ikon.Auth.Roles_IKGD.Provider.AreasProtected.Select(a => a.Name).Where(a => !string.IsNullOrEmpty(a)).Except(AreasAllowed).ToList();
        if (forceRoot || (AreasAllowed.Count > 0 && AreasDenied.Count == 0))
        {
          AreaMatchMode = AreaMatchModeEnum.FilterNone;
          if (forceRoot && AreasDenied.Count > 0)
            AreasDenied.Clear();
        }
        else
          AreaMatchMode = (AreasDenied.Count < AreasAllowed.Count) ? AreaMatchModeEnum.FilterByDenied : AreaMatchModeEnum.FilterByAllowed;
      }
      catch
      {
        AreasAllowed = new List<string>();
        AreasAllowedNotPublic = new List<string>();
        AreasDenied = new List<string>();
        AreaMatchMode = AreaMatchModeEnum.FilterByAllowed;
      }
    }


    public override int GetHashCode()
    {
      if (AreaMatchMode == AreaMatchModeEnum.FilterByAllowed)
        return string.Join(",", AreasAllowedNotPublic.OrderBy(a => a).ToArray()).GetHashCode() + (int)AreaMatchMode;
      else if (AreaMatchMode == AreaMatchModeEnum.FilterByDenied)
        return string.Join(",", AreasDenied.OrderBy(a => a).ToArray()).GetHashCode() + (int)AreaMatchMode;
      return (int)AreaMatchMode;
    }

  }


  //
  // classe ausiliaria per la gestione del COW sui nodi principali
  //
  public class IKGD_XNODE_COW<TEntity> where TEntity : class, IKGD_XNODE, new()
  {
    public DataContext cntxt { get; protected set; }
    public TEntity NodeOrig { get; protected set; }
    public TEntity Node { get; protected set; }
    public bool IsTainted { get; set; }
    public bool IsInserted { get; set; }
    public bool IgnoreChanges { get; set; }


    private IKGD_XNODE_COW() { }  // per bloccare il costruttore di default
    public IKGD_XNODE_COW(FS_Operations fsOp, TEntity NodeOrig)
    {
      IsTainted = false;
      IsInserted = false;
      cntxt = fsOp.DB;
      this.NodeOrig = NodeOrig;
      Node = fsOp.CloneNode(NodeOrig, true, false);
      Node.PropertyChanged += new System.ComponentModel.PropertyChangedEventHandler(Node_PropertyChanged);
      IgnoreChanges = false;
    }

    public void StopListen()
    {
      IgnoreChanges = true;
      Node.PropertyChanged -= Node_PropertyChanged;
    }

    //
    // forza la duplicazione anche se non ho modificato nessuna property (serve per duplicare iNode quando aggiungo solo streams)
    //
    public void EnsureDup()
    {
      IsTainted = true;
      if (!IsInserted)
      {
        cntxt.GetTable<TEntity>().InsertOnSubmit(Node);
        if (NodeOrig != null)
          Utility.SetPropertySafe<bool>(NodeOrig, "flag_current", false);
        IsInserted = true;
      }
    }

    void Node_PropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
      if (IgnoreChanges)
        return;
      EnsureDup();
    }

  }


  public class IKGD_QueuedOpDataVFS
  {
    public OpHandlerCOW_OperationEnum opType { get; set; }
    public List<int> sNodes { get; set; }
    public List<int> iNodes { get; set; }
    public List<int> rNodes { get; set; }
    public List<int> rNodesAffected { get; set; }


    public IKGD_QueuedOpDataVFS()
    {
      opType = OpHandlerCOW_OperationEnum.None;
      sNodes = new List<int>();
      iNodes = new List<int>();
      rNodes = new List<int>();
      rNodesAffected = new List<int>();
    }


    public string Serialize() { return IKGD_Serialization.SerializeToJSON(this); }
    public static IKGD_QueuedOpDataVFS DeSerialize(string json) { return IKGD_Serialization.DeSerializeJSON<IKGD_QueuedOpDataVFS>(json); }

  }




  public interface IKGD_CachingHelper_HasCacheDependencies_Interface
  {
    CacheDependency CachingHelper_Dependencies { get; }
  }


  //
  // usage:
  // public CacheItemRemovedCallback CachingHelper_onRemoveCallback { get { return (key, value, reason) => { try { (value as XYZ).Clear(); } catch { } }; } }
  //
  public interface IKGD_CachingHelper_CacheItemHasRemovedCallback_Interface
  {
    CacheItemRemovedCallback CachingHelper_onRemoveCallback { get; }
  }




  //
  // Dictionary<TKey, TValue> che non ritorna un'eccezione nel caso manchi la Key cercata
  // ritorna un null per gli object oppure il valore di default per i value type
  //
  public class DictionaryMV_Cachable<TKey, TValue> : Dictionary<TKey, TValue>, IKGD_CachingHelper_CacheItemHasRemovedCallback_Interface
  {
    public DictionaryMV_Cachable() : base() { }
    public DictionaryMV_Cachable(IDictionary<TKey, TValue> dictionary) : base(dictionary) { }
    public DictionaryMV_Cachable(IEqualityComparer<TKey> comparer) : base(comparer) { }
    public DictionaryMV_Cachable(int capacity) : base(capacity) { }
    public DictionaryMV_Cachable(IDictionary<TKey, TValue> dictionary, IEqualityComparer<TKey> comparer) : base(dictionary, comparer) { }
    public DictionaryMV_Cachable(int capacity, IEqualityComparer<TKey> comparer) : base(capacity, comparer) { }

    public new TValue this[TKey key]
    {
      get
      {
        TValue val = default(TValue);
        TryGetValue(key, out val);
        return val;
      }
      set { base[key] = value; }
    }

    public CacheItemRemovedCallback CachingHelper_onRemoveCallback { get { return (key, value, reason) => { try { (value as DictionaryMV_Cachable<TKey, TValue>).ClearCachingFriendly(); } catch { } }; } }

  }


}

