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

using Newtonsoft.Json;
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
  using Ikon.IKGD.Library;
  using Ikon.IKGD.Library.Resources;
  using Ikon.IKCMS.Library.Resources;



  public interface IKCMS_ModelCMS_VFS_Interface : IKCMS_ModelCMS_Interface
  {
    // sono gia' definiti in IKCMS_ModelCMS_Interface, solamente che vengono implementati con storage locale per ottimizzare l'accesso
    //List<IKGD_Path> PathsVFS { get; }
    //IKGD_Path PathVFS { get; }
    void PreFetchPaths();
  }


  public interface IKCMS_ModelCMS_HasTemplateInfo_Interface
  {
    IKCMS_PageCMS_Template_Interface TemplateInfo { get; }
    string TemplateViewPath { get; }
  }


  public interface IKCMS_ModelCMS_VFS_KVT_Interface
  {
    KeyValueObjectTree VFS_ResourceKVT { get; }
  }


  public interface IKCMS_ModelCMS_VFS_LanguageKVT_Interface : IKCMS_ModelCMS_VFS_KVT_Interface
  {
    KeyValueObjectTree VFS_ResourceLanguageKVT(params string[] keys);
    KeyValueObjectTree VFS_ResourceNoLanguageKVT(params string[] keys);
    IEnumerable<KeyValueObjectTree> VFS_ResourceLanguageKVTs(params string[] keys);
    IEnumerable<KeyValueObjectTree> VFS_ResourceNoLanguageKVTs(params string[] keys);
    List<string> VFS_ResourceLanguageKVTss(params string[] keys);
    List<string> VFS_ResourceNoLanguageKVTss(params string[] keys);
  }


  public interface IKCMS_ModelCMS_PageStatic_Interface : IKCMS_ModelCMS_Page_Interface
  {
  }


  public interface IKCMS_ModelCMS_Page_Interface : IKCMS_ModelCMS_GenericBrickInterface, IKCMS_ModelCMS_VFS_Interface, IKCMS_ModelCMS_HasTemplateInfo_Interface, IKCMS_ModelCMS_VFS_LanguageKVT_Interface, IKCMS_ModelCMS_HasPostFinalizeMethod_Interface
  {
    //
    string indexPath { get; }
    string moduleOp { get; }
    new string TemplateViewPath { get; set; }
    //
    string UrlModuleHome { get; }
    //
  }


  public interface IKCMS_ModelCMS_InterfaceT<T> : IKCMS_ModelCMS_Interface
    where T : class, IKCMS_HasSerializationCMS_Interface
  {
    T VFS_Resource { get; }
  }


  [IKCMS_ModelCMS_BootStrapperOpenGenerics(typeof(IKCMS_HasSerializationCMS_Interface))]
  public abstract class IKCMS_ModelCMS<T> : IKCMS_ModelCMS, IKCMS_ModelCMS_InterfaceT<T>, IKCMS_ModelCMS_Interface, IBootStrapperAutofacTask, IEnumerable<IKCMS_ModelCMS_Interface>
    where T : class, IKCMS_HasSerializationCMS_Interface
  {
    public virtual T VFS_Resource { get { return VFS_ResourceObject as T; } }
    //
    // accessor da utilizzare nelle classi derivate per gestire senza eccezioni eventuali problemi di deserializzazione
    // c'e' da valutare se dopo la rimozione dalla cache vogliamo anche lanciare un exception
    public virtual KeyValueObjectTree ResourceSettingsKVT_Wrapper
    {
      get
      {
        if (this.VFS_Resource != null && this.VFS_Resource is IKCMS_HasPropertiesKVT_Interface)
        {
          return (this.VFS_Resource as IKCMS_HasPropertiesKVT_Interface).ResourceSettingsKVT;
        }
        else
        {
          if (this.CacheKey.IsNotEmpty())
          {
            HttpRuntime.Cache.Remove(this.CacheKey);
          }
          //throw new NullReferenceException(string.Format("NullReferenceException: Model with vfsNode:{0}", vfsNode));
          return null;
        }
      }
    }
    //

    public IKCMS_ModelCMS()
      : base()
    { }
  }


  // l'implementazione di Serializable e' solo parziale e inserita al solo scopo di consentire la serializzazione del model da passare come oggetto json per le operazioni ajax
  [Serializable]
  [JsonObject(MemberSerialization.OptIn, IsReference = true)]
  [DataContract(IsReference = true)]
  public abstract class IKCMS_ModelCMS : IKCMS_ModelCMS_Interface, IBootStrapperAutofacTask, IEnumerable<IKCMS_ModelCMS_Interface>
  {
    //
    protected object _lock = new object();
    //
    [DataMember]
    public virtual FS_Operations.FS_NodeInfo_Interface vfsNode { get; protected set; }
    //
    // oggetto VFS deserializzato
    //
    [DataMember]
    public virtual object VFS_ResourceObjectData { get; protected set; }
    [DataMember]
    public virtual IKCMS_HasSerializationCMS_Interface VFS_ResourceObject { get; protected set; }
    [DataMember]
    public object CustomStuff { get; set; }
    //
    public int sNode { get { return vfsNode == null ? 0 : vfsNode.vNode.snode; } }
    public int rNode { get { return vfsNode == null ? 0 : vfsNode.vNode.rnode; } }
    public string Name { get { return vfsNode == null ? string.Empty : vfsNode.vNode.name; } }
    public string Language { get { return vfsNode == null ? null : vfsNode.Language; } }
    public string LanguageNN { get { return Language ?? IKGD_Language_Provider.Provider.LanguageNN; } }
    public double Position { get { return vfsNode == null ? 0 : vfsNode.vNode.position; } }
    public DateTime DateNode { get { return vfsNode == null ? DateTime.Now : vfsNode.vData.date_node; } }
    public DateTime? DateNodeAux { get { return vfsNode == null ? null : vfsNode.vData.date_node_aux; } }
    public DateTime? DateActivation { get { return vfsNode == null ? null : vfsNode.vData.date_activation; } }
    public DateTime? DateExpiry { get { return vfsNode == null ? null : vfsNode.vData.date_expiry; } }
    public DateTime DateLastUpdated { get { return this.RecurseOnModels.Max(m => (DateTime?)m.vfsNode.DateLastModified) ?? DateTime.Now; } }
    public string Area { get { return vfsNode == null ? null : vfsNode.vData.area; } }
    public string Category { get { return vfsNode == null ? null : vfsNode.vData.category; } }
    public string CategoryNN { get { return Category ?? string.Empty; } }
    public string Key { get { return vfsNode == null ? null : vfsNode.vData.key; } }
    public string ManagerType { get { return vfsNode == null ? null : vfsNode.vData.manager_type; } }
    //public string Collector { get { return vfsNode.vData.collector; } }
    public string Placeholder { get { return vfsNode == null ? null : vfsNode.vNode.placeholder; } }
    public string PlaceholderNN { get { return Placeholder ?? string.Empty; } }
    public string TemplateVnode { get { return vfsNode == null ? null : vfsNode.vNode.template; } }
    public string TemplateVnodeNN { get { return TemplateVnode ?? string.Empty; } }
    //public Utility.DictionaryMV<string, string> Attributes { get { return vfsNode.vData.AttributesDictionary; } }
    //public byte[] DataAsBytes { get { return vfsNode.vData.data.ToArray(); } }
    //public string DataAsString { get { return vfsNode.vData.dataAsString; } }
    //public XElement DataAsXml { get { return vfsNode.vData.dataAsXml; } }
    //
    public string CacheKey { get; set; }
    //
    public IEnumerable<IKGD_PROPERTY> Properties { get { return (vfsNode is FS_Operations.FS_NodeInfoExt_Interface && (vfsNode as FS_Operations.FS_NodeInfoExt_Interface).Relations != null) ? (vfsNode as FS_Operations.FS_NodeInfoExt_Interface).Properties : Enumerable.Empty<IKGD_PROPERTY>(); } }
    public IEnumerable<IKGD_RELATION> Relations { get { return (vfsNode is FS_Operations.FS_NodeInfoExt_Interface && (vfsNode as FS_Operations.FS_NodeInfoExt_Interface).Relations != null) ? (vfsNode as FS_Operations.FS_NodeInfoExt_Interface).Relations : Enumerable.Empty<IKGD_RELATION>(); } }
    public IEnumerable<IKGD_RELATION> RelationsOrdered { get { return (vfsNode is FS_Operations.FS_NodeInfoExt_Interface && (vfsNode as FS_Operations.FS_NodeInfoExt_Interface).Relations != null) ? (vfsNode as FS_Operations.FS_NodeInfoExt_Interface).Relations.OrderBy(r => r.position).ThenBy(r => r.rnode_dst).ThenBy(r => r.snode_dst).ThenBy(r => r.version).AsEnumerable() : Enumerable.Empty<IKGD_RELATION>(); } }
    //
    //public IEnumerable<int> TagsIds { get { return (Properties != null) ? Properties.Where(p => p.name == IKGD_Constants.IKCAT_TagPropertyName && p.attributeId != null).Select(p => p.attributeId.Value) : Enumerable.Empty<int>(); } }
    public IEnumerable<int> TagsIds { get { return (Properties != null) ? Properties.Where(p => p.attributeId != null).Select(p => p.attributeId.Value) : Enumerable.Empty<int>(); } }
    public IEnumerable<IKCAT_Attribute> Tags { get { return IKCAT_AttributeStorage.GetTags(TagsIds); } }
    public IEnumerable<FS_Operations.FS_TreeNode<IKCAT_Attribute>> TagsNodes { get { return IKCAT_AttributeStorage.GetTagsNodes(TagsIds); } }
    //
    public IEnumerable<IKATT_AttributeMapping> VariantsMapping { get { return (vfsNode is FS_Operations.FS_NodeInfoExt2_Interface && (vfsNode as FS_Operations.FS_NodeInfoExt2_Interface).Variants != null) ? (vfsNode as FS_Operations.FS_NodeInfoExt2_Interface).Variants : Enumerable.Empty<IKATT_AttributeMapping>(); } }
    public IEnumerable<int> VariantsIds { get { return (VariantsMapping != null) ? VariantsMapping.Select(p => p.AttributeId) : Enumerable.Empty<int>(); } }
    public IEnumerable<IKATT_Attribute> Variants { get { return IKATT_AttributeStorage.GetVarianti(VariantsIds); } }
    public IEnumerable<FS_Operations.FS_TreeNode<IKATT_Attribute>> VariantsNodes { get { return IKATT_AttributeStorage.GetVariantiNodes(VariantsIds); } }
    //
    [DataMember]
    protected bool? _IsExpired;
    public virtual bool IsExpired { get { return _IsExpired.GetValueOrDefault(vfsNode != null ? !vfsNode.IsNotExpired : false); } set { _IsExpired = value; } }
    //


    public IKCMS_ModelCMS()
    {
      Models = new List<IKCMS_ModelCMS_Interface>();
      IsValidatedVFS = false;
    }


    //
    // attenzione: l'implementazione di IDisposable non e' compatibile con il salvataggio in cache di IKCMS_ModelCMS_Interface, in quanto annullando tutti i ModelParent
    // azzera la gerarchia e la cache probabilmente non e' uno standard reference...
    //
    // IDisposable interface implementation: START
    //
    /*
    private bool _disposed;
    ~IKCMS_ModelCMS()
    {
      this.Dispose(false);
    }

    public void Dispose()
    {
      if (!this._disposed)
      {
        this.Dispose(true);
        this._disposed = true;
        GC.SuppressFinalize(this);
      }
    }

    protected virtual void Dispose(bool disposing)
    {
      if (disposing)
      {
        // clean up managed resources if any ...
        Clear();
        //
      }
      // clean up unmanaged resources
    }
    */
    //
    // IDisposable interface implementation: END
    //


    //
    // implementazione di IKGD_CachingHelper_CacheItemHasRemovedCallback_Interface
    //
    public CacheItemRemovedCallback CachingHelper_onRemoveCallback
    {
      get
      {
        return (key, value, reason) => { try { (value as IKCMS_ModelCMS).Clear(true, true, false); } catch { } };
      }
    }
    //
    public bool Cleared { get; set; }
    public virtual void Clear() { Clear(true, true, true); }
    public virtual void Clear(bool? backwardRecurse, bool? forwardRecurse, bool? clearCacheEntry)
    {
      // clean up unmanaged resources
      lock (_lock)
      {
        if (Cleared)
          return;
        Cleared = true;
        try
        {
          if (Models != null)
          {
            if (forwardRecurse.GetValueOrDefault(false))
            {
              for (int idx = Models.Count - 1; idx >= 0; idx--)
              {
                try
                {
                  if (Models[idx] != null && !Models[idx].Cleared)
                  {
                    Models[idx].ModelParentSetNoDeps(null);
                    Models[idx].Clear(false, true, clearCacheEntry);
                  }
                  Models[idx] = null;
                }
                catch { }
              }
            }
            Models.Clear();
            Models.TrimExcess();
          }
          if (backwardRecurse.GetValueOrDefault(false))
          {
            try
            {
              if (_ModelParent != null)
              {
                if (!_ModelParent.Cleared)
                {
                  _ModelParent = null;
                  _ModelParent.Clear(true, true, clearCacheEntry);
                }
              }
            }
            catch { }
          }
          if (_PathVFS != null)
          {
            _PathVFS.Fragments.Clear();
            _PathVFS = null;
          }
          if (_PathsVFS != null)
          {
            _PathsVFS.ForEach(p => p.Fragments.Clear());
            Utility.ClearCachingFriendly(_PathsVFS);
          }
          if (_BreadCrumbs != null)
          {
            Utility.ClearCachingFriendly(_BreadCrumbs);
            _BreadCrumbs = null;
          }
          if (_LinksFromRelations != null)
          {
            Utility.ClearCachingFriendly(_LinksFromRelations);
            _LinksFromRelations = null;
          }
          vfsNode = null;
          VFS_ResourceObject = null;
          VFS_ResourceObjectData = null;
          CustomStuff = null;
          //Models = null;
          if (clearCacheEntry.GetValueOrDefault(true) && CacheKey.IsNotEmpty())
          {
            string key = CacheKey;
            CacheKey = null;
            HttpRuntime.Cache.Remove(key);
          }
        }
        catch { }
      }
    }



    //
    // riferimenti al Model di livello piu' elevato (es. Page)
    //
    [DataMember]
    public virtual List<IKCMS_ModelCMS_Interface> Models { get; protected set; }
    //
    [DataMember]
    private IKCMS_ModelCMS_Interface _ModelParent = null;
    // reverted perche' crea qualche problema con le news di varese (da indagare)
    //private IKCMS_ModelCMS_Interface _ModelParent
    //{
    //  get { return _ModelParentWeakRef.WeakReferenceGet<IKCMS_ModelCMS_Interface>(); }
    //  set { _ModelParentWeakRef = new WeakReference(value); }
    //}
    //private WeakReference _ModelParentWeakRef;

    [JsonIgnore()]
    public virtual IKCMS_ModelCMS_Interface ModelParent
    {
      get { return _ModelParent; }
      set
      {
        lock (_lock)
        {
          if (_ModelParent != null)
            _ModelParent.Models.Remove(this);
          _ModelParent = value;
          if (_ModelParent != null)
            _ModelParent.Models.Add(this);
        }
      }
    }
    public virtual void ModelParentSetNoDeps(IKCMS_ModelCMS_Interface newParent) { _ModelParent = newParent; }
    public virtual IKCMS_ModelCMS_Interface ModelRoot { get { return (_ModelParent != null) ? _ModelParent.ModelRoot : this; } }
    public virtual IKCMS_ModelCMS_Interface ModelRootOrContext { get { return ((_ModelParent != null) ? ModelRoot : IKCMS_ModelCMS_Provider.Provider.ModelForContext) ?? ModelRoot; } }
    public virtual int ModelDepth { get { return (_ModelParent == null) ? 0 : _ModelParent.ModelDepth + 1; } }


    public IEnumerator<IKCMS_ModelCMS_Interface> GetEnumerator() { return RecurseOnModels.GetEnumerator(); }
    IEnumerator IEnumerable.GetEnumerator() { return RecurseOnModels.GetEnumerator(); }


    //
    // scan ricorsivo dei Models
    // per eseguire uno scan ricorsivo su tutti i models partendo da qualsiasi widget usare:
    // ModelRoot.RecurseOnModels
    //
    public virtual IEnumerable<IKCMS_ModelCMS_Interface> RecurseOnModels
    {
      get
      {
        yield return this;
        foreach (IKCMS_ModelCMS_Interface model in Models)
          foreach (IKCMS_ModelCMS_Interface subModel in model.RecurseOnModels)
            yield return subModel;
      }
    }

    //
    // back scan dei Models sui parents
    //
    public virtual IEnumerable<IKCMS_ModelCMS_Interface> BackRecurseOnModels
    {
      get
      {
        for (IKCMS_ModelCMS_Interface m = this; m != null; m = m.ModelParent)
          yield return m;
      }
    }


    //
    // child models costruiti omettendo il check sul Language (ma solo per il primo livello di ricorsione!)
    // la proprieta' viene popolata solamente on demand
    //
    //[DataMember]
    protected List<IKCMS_ModelCMS_Interface> _ModelsNoLanguageFilter;
    public virtual IList<IKCMS_ModelCMS_Interface> ModelsNoLanguageFilter
    {
      get
      {
        lock (_lock)
        {
          if (_ModelsNoLanguageFilter == null)
          {
            _ModelsNoLanguageFilter = new List<IKCMS_ModelCMS_Interface>();
            try
            {
              bool statusSaved = managerVFS.Enabled;
              if (managerVFS.Enabled != true)
                managerVFS.Enabled = true;
              SetupVfsDataForRecursion(FS_Operations.FiltersVFS_DefaultNoLanguage);
              if (managerVFS.Enabled != statusSaved)
                managerVFS.Enabled = statusSaved;
              var nodeRef = managerVFS[vfsNode.vNode.snode];
              List<int> sNodsParents = new List<int>();
              sNodsParents.AddRange(BackRecurseOnModels.Where(m => m.vfsNode != null).Select(m => m.vfsNode.sNode).Distinct());
              if (nodeRef != null)
              {
                foreach (var node in managerVFS.NodesVFS.Where(n => nodeRef.Data.rNode != n.rNode).Where(n => nodeRef.Data.rNode == n.ParentFolder || nodeRef.Data.rNode == n.Folder).Where(n => n.IsNotExpired).OrderBy(n => n.Position).ThenBy(n => n.vNode.name).ThenBy(n => n.sNode))
                {
                  if (sNodsParents.Contains(node.vNode.snode))
                    continue;
                  try
                  {
                    IKCMS_ModelCMS_Interface mdl = IKCMS_ModelCMS_Provider.Provider.ModelBuild(null, node, null);
                    if (mdl != null)
                      _ModelsNoLanguageFilter.Add(mdl);
                  }
                  catch { }
                }
              }
            }
            catch { }
          }
        }
        return _ModelsNoLanguageFilter;
      }
    }


    //
    // child models costruiti omettendo il check sulle ACL (ma solo per il primo livello di ricorsione!)
    // la proprieta' viene popolata solamente on demand
    //
    //[DataMember]
    protected List<IKCMS_ModelCMS_Interface> _ModelsNoACL;
    public virtual IList<IKCMS_ModelCMS_Interface> ModelsNoACL
    {
      get
      {
        lock (_lock)
        {
          if (_ModelsNoACL == null)
          {
            _ModelsNoACL = new List<IKCMS_ModelCMS_Interface>();
            try
            {
              bool statusSaved = managerVFS.Enabled;
              if (managerVFS.Enabled != true)
                managerVFS.Enabled = true;
              SetupVfsDataForRecursion(FS_Operations.FiltersVFS_DefaultNoACL);
              if (managerVFS.Enabled != statusSaved)
                managerVFS.Enabled = statusSaved;
              var nodeRef = managerVFS[vfsNode.vNode.snode];
              List<int> sNodsParents = new List<int>();
              sNodsParents.AddRange(BackRecurseOnModels.Where(m => m.vfsNode != null).Select(m => m.vfsNode.sNode).Distinct());
              if (nodeRef != null)
              {
                foreach (var node in managerVFS.NodesVFS.Where(n => nodeRef.Data.rNode != n.rNode).Where(n => nodeRef.Data.rNode == n.ParentFolder || nodeRef.Data.rNode == n.Folder).Where(n => n.IsNotExpired).OrderBy(n => n.Position).ThenBy(n => n.vNode.name).ThenBy(n => n.sNode))
                {
                  if (sNodsParents.Contains(node.vNode.snode))
                    continue;
                  try
                  {
                    IKCMS_ModelCMS_Interface mdl = IKCMS_ModelCMS_Provider.Provider.ModelBuild(null, node, null);
                    if (mdl != null)
                      _ModelsNoACL.Add(mdl);
                  }
                  catch { }
                }
              }
            }
            catch { }
          }
        }
        return _ModelsNoACL;
      }
    }


    public bool IsAclPublic { get { return (vfsNode == null || vfsNode.vData.area.IsNullOrEmpty()) ? true : Ikon.Auth.Roles_IKGD.Provider.AreasPublic.Any(a => a.Name == vfsNode.vData.area); } }
    public bool IsAclAuthOK
    {
      get
      {
        if (IsAclPublic)
          return true;
        var area = Ikon.Auth.Roles_IKGD.Provider.AreasAll.FirstOrDefault(a => a.Name == vfsNode.vData.area);
        if (area == null)
        {
          return false;
        }
        if (area.IsPublic || area.IsHardCoded)
        {
          return true;
        }
        bool isAuth = HttpContext.Current.User != null && HttpContext.Current.User.Identity.IsAuthenticated == true;
        if (isAuth && FS_OperationsHelpers.GetAreas().Any(a => a == area.Name))
        {
          return true;
        }
        return false;
      }
    }


    //
    // IEquatable<IKCMS_ModelCMS_Interface> interface:
    //
    public override int GetHashCode()
    {
      return (this != null && this.vfsNode != null) ? this.sNode : -1;
      //return base.GetHashCode();
    }
    public override bool Equals(object obj)
    {
      if (obj is IKCMS_ModelCMS_Interface)
        return Equals(obj as IKCMS_ModelCMS_Interface);
      return false;
      //return base.Equals(obj);
    }
    public bool Equals(IKCMS_ModelCMS_Interface obj)
    {
      if (obj == null)
        return false;
      return this.sNode == obj.sNode;
    }
    public new bool Equals(object obj1, object obj2)
    {
      if (obj1 == null && obj2 == null)
        return true;
      else if (obj1 == null || obj2 == null)
        return false;
      return obj1.GetHashCode() == obj2.GetHashCode();
    }



    public void RegisterHit(System.Web.Mvc.ViewDataDictionary ViewData, int? ActionCode, int? ActionSubCode)
    {
      if (Utility.TryParse<bool>(IKGD_Config.AppSettings["ModelAutoRegisterHits"], false))
      {
        IKCMS_HitLogger.ProcessHitLL(this.rNode, ActionCode, ActionSubCode);
      }
      else if (ViewData != null)
      {
        ViewData["HitLogActionCode"] = ActionCode.ToString();
        ViewData["HitLogActionSubCode"] = ActionSubCode.ToString();
      }
    }


    public virtual void ExecuteAutofac(ContainerBuilder builder)
    {
      Type ty = this.GetType();
      // trattamento separato per gli Open Generics
      if (ty.IsGenericType || ty.IsGenericTypeDefinition)
      {
        if (Utility.GetAttributesFromType<IKCMS_ModelCMS_BootStrapperOpenGenericsAttribute>(ty).Any())
          ty = ty.GetGenericTypeDefinition();
      }
      //
      // trattamento separato per gli Open Generics
      if (ty.IsGenericTypeDefinition)
      {
        builder.RegisterGeneric(ty.GetGenericTypeDefinition());
      }
      else
      {
        builder.RegisterType(ty);
      }
    }


    [JsonIgnore()]
    public FS_Operations fsOp { get { return IKCMS_ManagerIoC.requestContainer.ResolveNamed<FS_Operations>("readonly"); } }
    //public FS_Operations fsOp { get { return IKCMS_ManagerIoC.requestContainer.Resolve<FS_Operations>(); } }


    [JsonIgnore()]
    public IKCMS_ModelCMS_ManagerVFS managerVFS { get { lock (_lock) { return IKCMS_ModelCMS_ManagerVFS.Factory(); } } }


    [DataMember]
    public bool HasExceptions { get; set; }
    [DataMember]
    public bool HasExceptionsTree { get { return RecurseOnModels.Any(m => m.HasExceptions); } }
    [DataMember]
    public bool IsValidatedVFS { get; set; }
    public virtual IKCMS_ModelCMS_ModelInfo_Interface ModelInfo { get; set; }
    public virtual Expression<Func<IKGD_VNODE, bool>> RecursionVFSvNodeFilter { get; set; }
    public virtual Expression<Func<IKGD_VDATA, bool>> RecursionVFSvDataFilter { get; set; }
    public virtual Expression<Func<IKGD_VDATA, bool>> RecursionVFSvDataFilterSubFolders { get; set; }


    [DataMember]
    protected int? _sNodeReference;
    public virtual int sNodeReference { get { return _sNodeReference ?? sNode; } set { _sNodeReference = (value <= 0) ? null : (int?)value; } }


    [DataMember]
    protected List<IKGD_Path> _PathsVFS;
    public virtual List<IKGD_Path> PathsVFS { get { lock (_lock) { return _PathsVFS ?? (_PathsVFS = fsOp.PathsFromNodeExt(sNode, false, true, true)); } } set { _PathsVFS = value; } }

    [DataMember]
    protected IKGD_Path _PathVFS;
    public virtual IKGD_Path PathVFS { get { lock (_lock) { return _PathVFS ?? (_PathVFS = PathsVFS.FilterFallback(IKCMS_ModelCMS_Provider.Provider.pathFilters).OrderByACL().FirstOrDefault()); } } set { _PathVFS = value; } }

    public virtual void PreFetchPaths()
    {
      lock (_lock)
      {
        if (_PathsVFS == null)
          _PathsVFS = fsOp.PathsFromNodeExt(sNode, false, true, true);
        if (_PathVFS == null && _PathsVFS != null)
          _PathVFS = _PathsVFS.FilterFallback(IKCMS_ModelCMS_Provider.Provider.pathFilters).OrderByACL().FirstOrDefault();
      }
    }


    public virtual void Setup(FS_Operations.FS_NodeInfo_Interface fsNode, IKCMS_ModelCMS_Interface modelParent, IKCMS_ModelCMS_ModelInfo_Interface modelInfo, params object[] args)
    {
      bool statusSaved = managerVFS.Enabled;
      Exception exSetup = null;
      //
      // setup del ModelParent nel caso questo non sia stato fornito nei parametri del builder e il model lo richiedesse
      //
      if (modelParent == null && this is IKCMS_ModelCMS_EnsureParentModel_Interface)
      {
        modelParent = (this as IKCMS_ModelCMS_EnsureParentModel_Interface).EnsureParentModel(fsNode, modelInfo, args);
      }
      //
      try { SetupInstance(fsNode, modelParent, modelInfo, args); }
      catch (Exception ex) { exSetup = ex; }
      //
      try
      {
        if (fsNode == null || fsNode.IsNotExpired)  //disabilitiamo il build ricorsivo nel caso di risorse scadute
        {
          SetupVfsDataForRecursion(null, args);
          SetupRecursive(null, args);
        }
      }
      catch (Exception ex) { exSetup = ex; }
      //
      try
      {
        if (fsNode == null || fsNode.IsNotExpired)  //disabilitiamo il build ricorsivo nel caso di risorse scadute
        {
          SetupFinalize(args);
        }
      }
      catch (Exception ex) { exSetup = ex; }
      //
      if (managerVFS.Enabled != statusSaved)
        managerVFS.Enabled = statusSaved;
      //
      if (exSetup != null)
        SetupExceptionHandler(exSetup);
      //
    }


    public virtual void SetupExceptionHandler(Exception ex)
    {
      Elmah.ErrorSignal.FromCurrentContext().Raise(ex);
      IKCMS_ExceptionsManager.Add(new IKCMS_Exception_ModelBuilder(this.GetType().FullName + ".SetupExceptionHandler", ex));
      HasExceptions = true;
      throw ex;
    }


    protected virtual void SetupInstance(FS_Operations.FS_NodeInfo_Interface fsNode, IKCMS_ModelCMS_Interface modelParent, IKCMS_ModelCMS_ModelInfo_Interface modelInfo, params object[] args)
    {
      if (fsNode == null)
      {
        // vogliamo consentire anche il setup di models non VFS
        //throw new ResourceVfsNotFoundException();
      }
      //
      ModelParent = modelParent;
      vfsNode = fsNode;
      ModelInfo = modelInfo;
      //
      // setup dei settings di default per il fetch dei dati
      //
      if (!IsValidatedVFS && vfsNode != null)
      {
        var modeDefault = vfsNodeFetchModeEnum.vNode_vData_iNode;
        //var modeDefault = vfsNodeFetchModeEnum.vNode_vData_iNode_Extra;
        vfsNodeFetchModeEnum fsNodeFetchMode = modeDefault;
        try { fsNodeFetchMode = ModelInfo.Attributes.OfType<IKCMS_ModelCMS_fsNodeModeAttribute>().Select(a => a.vfsNodeFetchMode).DefaultIfEmpty(modeDefault).First(); }
        catch { }
        switch (fsNodeFetchMode)
        {
          case vfsNodeFetchModeEnum.vNode_vData_iNode_ExtraVariants:
            fsNode = managerVFS.EnsureNodeOrRegister<FS_Operations.FS_NodeInfoExt2>(fsNode);
            break;
          case vfsNodeFetchModeEnum.vNode_vData_iNode_Extra:
            fsNode = managerVFS.EnsureNodeOrRegister<FS_Operations.FS_NodeInfoExt>(fsNode);
            break;
          default:
            fsNode = managerVFS.EnsureNodeOrRegister<FS_Operations.FS_NodeInfo>(fsNode);
            break;
        }
        IsValidatedVFS = true;
      }
      //
      try
      {
        if (vfsNode != null)
        {
          VFS_ResourceObject = IKCMS_RegisteredTypes.Deserialize_IKCMS_ResourceVFS(vfsNode.vData);
          if (VFS_ResourceObject != null)
          {
            VFS_ResourceObjectData = VFS_ResourceObject.ResourceSettingsObject;
          }
        }
      }
      catch (Exception ex)
      {
        if (vfsNode != null && vfsNode.vData.flag_unstructured == false)
        {
          Elmah.ErrorSignal.FromCurrentContext().Raise(ex);
        }
      }
      //catch { }
      //
    }


    protected virtual void SetupVfsDataForRecursion(FS_Operations.FilterVFS? filters, params object[] args)
    {
      //
      // preparazione dei nodi per la ricorsione
      //
      if (managerVFS.Enabled)
      {
        //
        var modeDefault = vfsNodeFetchModeEnum.vNode_vData_iNode;
        //var modeDefault = vfsNodeFetchModeEnum.vNode_vData_iNode_Extra;
        vfsNodeFetchModeEnum fsNodeRecurseFetchMode = ModelInfo.Attributes.OfType<IKCMS_ModelCMS_fsNodeModeRecurseAttribute>().Select(a => a.vfsNodeFetchMode).DefaultIfEmpty(modeDefault).First();
        ModelRecursionModeEnum modelRecursionMode = ModelInfo.Attributes.OfType<IKCMS_ModelCMS_RecursionModeAttribute>().Select(a => a.modelRecursionMode).DefaultIfEmpty(ModelRecursionModeEnum.RecursionNone).First();
        //
        if (modelRecursionMode == ModelRecursionModeEnum.RecursionOnResources)
        {
          Expression<Func<IKGD_VNODE, bool>> vNodeFilter = PredicateBuilder.True<IKGD_VNODE>().And(n => n.flag_folder == false && n.folder == vfsNode.vNode.folder);
          if (RecursionVFSvNodeFilter != null)
            vNodeFilter = vNodeFilter.And(RecursionVFSvNodeFilter);
          managerVFS.FetchNodes(vNodeFilter, RecursionVFSvDataFilter, fsNodeRecurseFetchMode, filters, false);
        }
        else if (modelRecursionMode == ModelRecursionModeEnum.RecursionOnFolders)
        {
          //
          // non ci sono problemi con le ricorsioni, perche' dopo il primo run disabilita' l'aggiornamento del managerVFS
          //
          managerVFS.FetchNodes(vn => (vn.flag_folder == false && vn.folder == vfsNode.vNode.folder) || (vn.flag_folder == true && vn.parent == vfsNode.vNode.folder), RecursionVFSvDataFilter, fsNodeRecurseFetchMode, filters, false);
          //
          List<int> folders = managerVFS[vfsNode.vNode.snode].Nodes.Where(n => n.Data.vNode.flag_folder).Select(n => n.Data.vNode.rnode).ToList();
          Expression<Func<IKGD_VNODE, bool>> vNodeFilter = PredicateBuilder.True<IKGD_VNODE>().And(n => n.flag_folder == false && folders.Contains(n.folder));
          if (RecursionVFSvNodeFilter != null)
            vNodeFilter = vNodeFilter.And(RecursionVFSvNodeFilter);
          managerVFS.FetchNodes(vNodeFilter, RecursionVFSvDataFilterSubFolders, fsNodeRecurseFetchMode, filters, false);
          //
          managerVFS.Enabled = false;
        }
      }
      //
    }


    protected virtual void SetupRecursive(IEnumerable<int> sNodesBlackList, params object[] args)
    {
      List<int> sNodesParents = new List<int>();
      sNodesParents.AddRange(BackRecurseOnModels.Where(m => m.vfsNode != null).Select(m => m.vfsNode.sNode).Distinct());
      FS_Operations.VFS_TreeNode<FS_Operations.FS_NodeInfo_Interface> nodeRef = managerVFS[vfsNode.vNode.snode];
      FS_Operations.FS_NodeInfo_Interface fsNodeRef = (nodeRef != null) ? nodeRef.Data : this.vfsNode;
      if (fsNodeRef != null)
      {
        string lang = this.LanguageNN;
        var areas = fsOp.CurrentAreasExtended.AreasAllowed;
        var nodesSet = managerVFS.NodesVFS.Where(n => fsNodeRef.rNode != n.rNode).Where(n => fsNodeRef.rNode == n.ParentFolder || fsNodeRef.rNode == n.Folder);
        var nodesFiltered = nodesSet.Where(n => n.IsNotExpired).Where(n => n.LanguageCheck(this.LanguageNN)).Where(n => n.vData.area.IsNullOrEmpty() || areas.Contains(n.vData.area));
        // se una risorsa e' definita senza lingua e con lingua la facciamo comparire una volta sola
        nodesFiltered = nodesFiltered.OrderByDescending(n => n.Language).Distinct((n1, n2) => n1.rNode == n2.rNode);
        foreach (var node in nodesFiltered.OrderBy(n => n.Position).ThenBy(n => n.vNode.name).ThenBy(n => n.sNode))
        {
          if (sNodesBlackList != null && sNodesBlackList.Contains(node.vNode.snode))
            continue;
          if (sNodesParents.Contains(node.vNode.snode))
            continue;
          IKCMS_ModelCMS_Provider.Provider.ModelBuild(this, node, null);
        }
      }
      //
    }


    protected virtual void SetupFinalize(params object[] args)
    {
      // per completare il setup del model dopo la creazione della struttura ricorsiva
    }


    public virtual bool UrlBuilderRequiresParent
    {
      get
      {
        bool result = this is IKCMS_ModelCMS_ArchiveBrowserItem_Interface;
        result &= this != ModelRoot;
        return result;
      }
    }


    [DataMember]
    protected string _Url;
    public virtual string Url
    {
      get
      {
        lock (_lock)
        {
          if (_Url == null)
          {
            _Url = GetUrlCanonical(rNode, sNode, UrlBuilderRequiresParent ? (int?)ModelRoot.sNode : null, Name);
          }
        }
        return _Url;
      }
      protected set { _Url = value; }
    }



    // non usare public altrimenti non funziona piu' niente...
    private static Regex _UrlBackRx = new Regex(@"(\?|&|#)UrlBack=.+(&|#|$)", RegexOptions.Compiled | RegexOptions.Singleline);
    [DataMember]
    protected string _UrlBack;
    public virtual string UrlBack
    {
      get
      {
        lock (_lock)
        {
          if (_UrlBack == null)
          {
            try
            {
              if (!string.IsNullOrEmpty(HttpContext.Current.Request.QueryString["UrlBack"]))
                return Utility.StringBase64ToString(HttpContext.Current.Request.QueryString["UrlBack"]);
              else if (_UrlBackRx.IsMatch(HttpContext.Current.Request.Url.ToString()))
              {
                var kv = Utility.ParseQueryString(HttpContext.Current.Request.Url.ToString(), true).LastOrDefault(p => p.Key == "UrlBack");
                if (kv.Key != null && !string.IsNullOrEmpty(kv.Value))
                  return Utility.StringBase64ToString(kv.Value);
              }
              if (BreadCrumbs != null && BreadCrumbs.Any())
                return BreadCrumbs.Reverse<IKCMS_ModelCMS_BreadCrumbsElement>().Skip(1).FirstOrDefault(b => !string.IsNullOrEmpty(b.Url)).Url;
            }
            catch { }
          }
        }
        return _UrlBack;
      }
      protected set { _UrlBack = value; }
    }


    [DataMember]
    protected string _UrlCanonical;
    public virtual string UrlCanonical
    {
      get
      {
        if (UrlCanonicalEnabled.GetValueOrDefault(true) == false)
        {
          return HttpContext.Current.Request.Url.PathAndQuery;
        }
        lock (_lock)
        {
          if (_UrlCanonical == null)
          {
            _UrlCanonical = GetUrlCanonical(rNode, sNode, UrlBuilderRequiresParent ? (int?)ModelRoot.sNode : null, Name);
          }
        }
        return _UrlCanonical;
      }
      protected set { _UrlCanonical = value; }
    }


    [DataMember]
    public bool? UrlCanonicalEnabled { get; set; }


    public static string GetUrlCanonical(int? rNode, int? sNode, int? sNodeRoot, string Name) { return GetUrlCanonical(rNode, sNode, sNodeRoot, Name, null, true, null); }
    public static string GetUrlCanonical(int? rNode, int? sNode, int? sNodeRoot, string Name, bool enableUrlRewrite) { return GetUrlCanonical(rNode, sNode, sNodeRoot, Name, null, enableUrlRewrite, null); }
    public static string GetUrlCanonical(int? rNode, int? sNode, int? sNodeRoot, string Name, string language, bool enableUrlRewrite, UrlGeneratorFormatEnum? urlFormat)
    {
      string canonicalUrl = null;
      if (enableUrlRewrite && sNode != null)
      {
        canonicalUrl = IKGD_SEO_Manager.MapOutcomingUrl(sNode, rNode, language);
      }
      if (string.IsNullOrEmpty(canonicalUrl))
      {
        if (sNodeRoot != null)
          canonicalUrl = IKCMS_RouteUrlManager.GetMvcUrlGeneral(sNodeRoot, sNode, language, true, false);
        else
        {
          urlFormat = urlFormat ?? IKCMS_TreeStructureVFS.UrlGeneratorFormat;
          canonicalUrl = IKCMS_TreeStructureVFS.UrlFormatterWorkerBase(sNode, rNode, language, Name, FlagsMenuEnum.None, urlFormat, null, null);  // modificato in seguito a bug rilevato per jezik lingua su cambio lingua per url associate ad un flag find first valid url
          //canonicalUrl = IKCMS_TreeStructureVFS.UrlFormatterWorkerBase(sNode, rNode, null, Name, FlagsMenuEnum.None, urlFormat, null, null);
          if (canonicalUrl == null && sNode != null)
          {
            canonicalUrl = IKCMS_RouteUrlManager.GetMvcUrlGeneralV2(language, sNode, sNode != sNodeRoot ? sNodeRoot : null, "/" + Utility.UrlEncodeIndexPathForSEO(Name), false);
          }
        }
      }
      if (!string.IsNullOrEmpty(canonicalUrl) && canonicalUrl.StartsWith("~/"))
        canonicalUrl = Utility.ResolveUrl(canonicalUrl);
      return canonicalUrl;
    }


    //
    // set the encoded back url on a given Url
    //
    public virtual string UrlAddEncodedBackUrl(string targetUrl)
    {
      return GetEncodedBackUrl(targetUrl);
    }


    public static string GetEncodedBackUrl(string targetUrl)
    {
      if (!string.IsNullOrEmpty(targetUrl) && targetUrl != "javascript:;")
      {
        return Utility.UriSetQuery(targetUrl, "UrlBack", Utility.StringToBase64(HttpContext.Current.Request.Url.PathAndQuery));
      }
      return targetUrl;
    }


    //
    // Url di default per il download di documenti non strutturati
    //
    public string UrlDownloadDefaultStream() { return IKCMS_TreeStructureVFS.UrlDownloadDefaultStream(vfsNode); }
    public string UrlDownloadDefaultStream(string stream) { return IKCMS_TreeStructureVFS.UrlDownloadDefaultStream(vfsNode, stream); }


    //
    // breadcrumbs support, e' definito virtuale per customizzarlo nelle classi derivate
    // per default ritorna il path fino ad una delle root del sito che dovrebbe sempre andare bene eccetto che per i moduli browse/news
    // servira' anche per controllare le voci evidenziate sul menu' di navigazione
    //
    [DataMember]
    protected List<IKCMS_ModelCMS_BreadCrumbsElement> _BreadCrumbs;
    public virtual List<IKCMS_ModelCMS_BreadCrumbsElement> BreadCrumbs
    {
      get
      {
        lock (_lock)
        {
          if (_BreadCrumbs == null)
          {
            try
            {
              _BreadCrumbs = new List<IKCMS_ModelCMS_BreadCrumbsElement>();
              //
              // valutare se costruire le breadcrumbs a partire dal PathVFS oppure dal menu' (quando sara' disponibile)
              //
              List<int> rootNodes = IKGD_ConfigVFS.Config.RootsCMS_sNodes;
              //
              int fragRootIdx = PathVFS.Fragments.FindLastIndex(f => rootNodes.Contains(f.sNode));
              if (fragRootIdx >= 0)
              {
                string lang = PathVFS.FirstLanguage ?? LanguageNN;
                var frags = PathVFS.Fragments.Skip(fragRootIdx + 1).Where(f => ((f.FlagsMenu & FlagsMenuEnum.SkipBreadCrumbs) != FlagsMenuEnum.SkipBreadCrumbs));
                foreach (IKGD_Path_Fragment frag in frags)
                {
                  IKCMS_ModelCMS_BreadCrumbsElement item = new IKCMS_ModelCMS_BreadCrumbsElement(frag.sNode, null, frag.Name);
                  //
                  // TODO:
                  // la Url dovrebbe essere sempre reperita dal menu', se non esiste nel menu' non deve essere cliccabile nelle breadcrumbs
                  // per adesso le genero in maniera standard
                  //
                  if (frag.flag_folder == false || IKCMS_RegisteredTypes.Types_IKCMS_PageBase_Interface.Any(t => t.Name == frag.ManagerType))
                  {
                    //item.Url = IKCMS_RouteUrlManager.GetMvcUrlGeneral(null, item.sNode, null, true, false);
                    item.Url = IKCMS_RouteUrlManager.GetMvcUrlGeneralV2(lang, frag.sNode, null, "/" + Utility.UrlEncodeIndexPathForSEO(frag.Name), false);
                  }
                  //
                  _BreadCrumbs.Add(item);
                }
              }

              // costruzione delle breadcrumbs:
              // (lastindexof roots)..(end)
              // se non cliccabile (no url o no menu flags) -> url = null che poi mappiamo a javascript:; durante il rendering
              // nei moduli news si possono completare le breadcrumbs con l'index dell'item o lista
              // inizializzare anche url back
              //
            }
            catch { }
          }
        }
        return _BreadCrumbs;
      }
      set { _BreadCrumbs = value; }
    }


    //
    // nodi delle relations da caricare on demand
    [DataMember]
    protected List<FS_Operations.FS_NodeInfo_Interface> _LinksFromRelations;
    public virtual List<FS_Operations.FS_NodeInfo_Interface> LinksFromRelations
    {
      get
      {
        lock (_lock)
        {
          if (_LinksFromRelations == null)
          {
            _LinksFromRelations = new List<FS_Operations.FS_NodeInfo_Interface>();
            try
            {
              string lang = this.Language ?? IKGD_Language_Provider.Provider.Language;
              IKCMS_ModelCMS_Provider.Provider.managerVFS.EnsureNodesRNODE<FS_Operations.FS_NodeInfo>(Relations.Where(r => r.type == Ikon.IKGD.Library.IKGD_Constants.IKGD_LinkRelationName).Select(r => r.rnode_dst).Distinct(), false);
              _LinksFromRelations = IKCMS_ModelCMS_Provider.Provider.managerVFS.NodesVFS.Where(n => Relations.Where(r => r.type == Ikon.IKGD.Library.IKGD_Constants.IKGD_LinkRelationName).Any(r => r.rnode_dst == n.rNode)).Where(n => n.IsActive).Where(n => n.LanguageCheck(lang)).OrderBy(n => Relations.ToList().FindIndex(r => r.rnode_dst == n.vNode.rnode)).Distinct((n1, n2) => n1.rNode == n2.rNode).ToList();
            }
            catch { }
          }
        }
        return _LinksFromRelations;
      }
    }


    //
    // titoli della pagina
    //
    [DataMember]
    protected string _TitleVFS;
    [DataMember]
    protected string _TitleHead;
    [DataMember]
    protected string _TitleH1;
    [DataMember]
    protected string _TitleH2;
    [DataMember]
    protected string _HeaderMetaDescription;
    [DataMember]
    protected string _HeaderMetaKeywords;
    [DataMember]
    protected string _HeaderMetaRobots;
    //
    public string TitleVFS { get { EnsureHeadersAndTitles(); return _TitleVFS; } set { _TitleVFS = value; } }
    public string TitleHead { get { EnsureHeadersAndTitles(); return _TitleHead; } set { _TitleHead = value; } }
    public string TitleH1 { get { EnsureHeadersAndTitles(); return _TitleH1; } set { _TitleH1 = value; } }
    public string TitleH2 { get { EnsureHeadersAndTitles(); return _TitleH2; } set { _TitleH2 = value; } }
    //
    public string HeaderMetaDescriptionRecursive { get { return BackRecurseOnModels.Select(m => m.HeaderMetaDescription).FirstOrDefault(r => r.IsNotEmpty()); } }
    public string HeaderMetaDescription { get { EnsureHeadersAndTitles(); return _HeaderMetaDescription; } set { _HeaderMetaDescription = value; } }
    public string HeaderMetaKeywords { get { EnsureHeadersAndTitles(); return _HeaderMetaKeywords; } set { _HeaderMetaKeywords = value; } }
    public string HeaderMetaRobots { get { EnsureHeadersAndTitles(); return _HeaderMetaRobots; } set { _HeaderMetaRobots = value; } }
    //
    [DataMember]
    protected bool _HeadersAndTitlesProcessed;
    public virtual void EnsureHeadersAndTitles()
    {
      lock (_lock)
      {
        if (_HeadersAndTitlesProcessed)
          return;
        _HeadersAndTitlesProcessed = true;
        //
        // estrazione dei title dall'oggetto KVT
        //
        string titleBase = this.Name;
        try
        {
          _TitleVFS = _TitleVFS ?? this.Name;
          if (this is IKCMS_ModelCMS_VFS_LanguageKVT_Interface)
          {
            IKCMS_ModelCMS_VFS_LanguageKVT_Interface mdl = this as IKCMS_ModelCMS_VFS_LanguageKVT_Interface;
            //
            titleBase = mdl.VFS_ResourceLanguageKVT("Title").ValueString ?? _TitleVFS;
            _TitleHead = _TitleHead ?? mdl.VFS_ResourceLanguageKVT("TitleHead").ValueString;
            _TitleH1 = _TitleH1 ?? mdl.VFS_ResourceLanguageKVT("TitleH1").ValueString;
            _TitleH2 = _TitleH2 ?? mdl.VFS_ResourceLanguageKVT("TitleH2").ValueString;
            //
            _HeaderMetaDescription = _HeaderMetaDescription ?? mdl.VFS_ResourceLanguageKVT("MetaDescription").ValueString;
            _HeaderMetaKeywords = _HeaderMetaKeywords ?? mdl.VFS_ResourceLanguageKVT("MetaKeywords").ValueString;
            _HeaderMetaRobots = _HeaderMetaRobots ?? mdl.VFS_ResourceLanguageKVT("MetaRobots").ValueString;
            //
          }
        }
        catch { }
        //
        _TitleHead = _TitleHead.DefaultIfEmpty(titleBase);
        _TitleH1 = _TitleH1.DefaultIfEmpty(_TitleHead);
        //
      }
    }

  }  //class IKCMS_ModelCMS




  //
  // Model instanziabile senza niente...
  // permette il salvataggio di una risorsa VFS ma non richiede che abbia dei dati deserializzabili e si puo' usare con qualunque cosa
  //
  [IKCMS_ModelCMS_ResourceTypes(typeof(IKCMS_Base_Interface))]
  [IKCMS_ModelCMS_RecursionMode(ModelRecursionModeEnum.RecursionOnResources)]
  [IKCMS_ModelCMS_fsNodeMode(vfsNodeFetchModeEnum.vNode_vData_iNode)]
  [IKCMS_ModelCMS_Priority(-9999999)]
  public class IKCMS_ModelCMS_Dumb : IKCMS_ModelCMS, IKCMS_ModelCMS_VFS_Interface
  {
    public IKCMS_ModelCMS_Dumb()
      : base()
    { }


    public IKCMS_ModelCMS_Dumb(IKCMS_ModelCMS_Interface modelParent, FS_Operations.FS_NodeInfo_Interface fsNode)
      : this()
    {
      this.Setup(fsNode, modelParent, null);
    }


    protected override void SetupInstance(FS_Operations.FS_NodeInfo_Interface fsNode, IKCMS_ModelCMS_Interface modelParent, IKCMS_ModelCMS_ModelInfo_Interface modelInfo, params object[] args)
    {
      base.SetupInstance(fsNode, modelParent, modelInfo, args);
      //
      //try { PreFetchPaths(); }
      //catch { }
    }


    // per evitare le ricorsioni automatiche con questi models semplici
    protected override void SetupVfsDataForRecursion(FS_Operations.FilterVFS? filters, params object[] args) { }
    protected override void SetupRecursive(IEnumerable<int> sNodesBlackList, params object[] args) { }

  }


  //
  // Model instanziabile senza niente eccetto una risorsa VFS...
  //
  [IKCMS_ModelCMS_ResourceTypes(typeof(IKCMS_HasSerializationCMS_Interface))]
  [IKCMS_ModelCMS_RecursionMode(ModelRecursionModeEnum.RecursionOnResources)]
  [IKCMS_ModelCMS_fsNodeMode(vfsNodeFetchModeEnum.vNode_vData_iNode)]
  //[IKCMS_ModelCMS_fsNodeModeRecurse(vfsNodeFetchModeEnum.vNode_vData_iNode)]
  [IKCMS_ModelCMS_Priority(-9999990)]
  public class IKCMS_ModelCMS_DumbVFS<T> : IKCMS_ModelCMS<T>, IKCMS_ModelCMS_VFS_Interface
    where T : class, IKCMS_HasSerializationCMS_Interface
  {
    public IKCMS_ModelCMS_DumbVFS()
      : base()
    { }


    public IKCMS_ModelCMS_DumbVFS(IKCMS_ModelCMS_Interface modelParent, FS_Operations.FS_NodeInfo_Interface fsNode)
      : this()
    {
      this.Setup(fsNode, modelParent, null);
    }


    protected override void SetupInstance(FS_Operations.FS_NodeInfo_Interface fsNode, IKCMS_ModelCMS_Interface modelParent, IKCMS_ModelCMS_ModelInfo_Interface modelInfo, params object[] args)
    {
      base.SetupInstance(fsNode, modelParent, modelInfo, args);
      //
      //try { PreFetchPaths(); }
      //catch { }
    }


    // per evitare le ricorsioni automatiche con questi models semplici
    protected override void SetupVfsDataForRecursion(FS_Operations.FilterVFS? filters, params object[] args) { }
    protected override void SetupRecursive(IEnumerable<int> sNodesBlackList, params object[] args) { }

  }


  //
  // classe ausiliaria per la gestione delle breadcrumbs
  //
  public class IKCMS_ModelCMS_BreadCrumbsElement : TupleW<int?, string, string>
  {
    public int? sNode { get { return Item1; } set { Item1 = value; } }
    public string Url { get { return Item2; } set { Item2 = value; } }
    public string Text { get { return Item3; } set { Item3 = value; } }


    public IKCMS_ModelCMS_BreadCrumbsElement(int? sNode, string Url, string Text)
      : base(sNode, Url, Text)
    {
    }


    public override string ToString()
    {
      return string.Format("({0},{1},{2})", sNode, Url, Text);
    }

  }


}  //namespace
