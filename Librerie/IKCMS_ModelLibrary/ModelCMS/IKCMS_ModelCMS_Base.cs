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
  using Ikon.IKGD.Library.Resources;



  public interface IKCMS_ModelCMS_Interface : IEnumerable<IKCMS_ModelCMS_Interface>, IEquatable<IKCMS_ModelCMS_Interface>, IKGD_CachingHelper_CacheItemHasRemovedCallback_Interface
  {
    FS_Operations.FS_NodeInfo_Interface vfsNode { get; }
    //
    // oggetto VFS deserializzato
    //
    object VFS_ResourceObjectData { get; }
    IKCMS_HasSerializationCMS_Interface VFS_ResourceObject { get; }
    object CustomStuff { get; set; }
    //
    // solo properties tipo get da usare esclusivamente onDemand
    // per model che ne facciano uso intensivo assegnarle subito ad uno storage locale e implementare l'interface IKCMS_ModelCMS_VFS_Interface
    List<IKGD_Path> PathsVFS { get; set; }
    IKGD_Path PathVFS { get; set; }
    //
    List<IKCMS_ModelCMS_Interface> Models { get; }
    IKCMS_ModelCMS_Interface ModelParent { get; set; }
    IKCMS_ModelCMS_Interface ModelRoot { get; }
    IKCMS_ModelCMS_Interface ModelRootOrContext { get; }
    IList<IKCMS_ModelCMS_Interface> ModelsNoACL { get; }
    IList<IKCMS_ModelCMS_Interface> ModelsNoLanguageFilter { get; }
    //
    void ModelParentSetNoDeps(IKCMS_ModelCMS_Interface newParent);
    int ModelDepth { get; }
    //
    IEnumerable<IKCMS_ModelCMS_Interface> RecurseOnModels { get; }
    IEnumerable<IKCMS_ModelCMS_Interface> BackRecurseOnModels { get; }
    //
    int sNodeReference { get; }
    int sNode { get; }
    int rNode { get; }
    string Name { get; }
    string Language { get; }
    string LanguageNN { get; }
    DateTime DateNode { get; }
    DateTime? DateNodeAux { get; }
    DateTime? DateActivation { get; }
    DateTime? DateExpiry { get; }
    DateTime DateLastUpdated { get; }
    bool IsExpired { get; }
    string Area { get; }
    double Position { get; }
    string Category { get; }
    string CategoryNN { get; }
    string Key { get; }
    string ManagerType { get; }
    //string Collector { get; }
    string Placeholder { get; }
    string PlaceholderNN { get; }
    bool IsAclPublic { get; }
    bool IsAclAuthOK { get; }
    //Utility.DictionaryMV<string, string> Attributes { get; }
    //byte[] DataAsBytes { get; }
    //string DataAsString { get; }
    //XElement DataAsXml { get; }
    string CacheKey { get; set; }
    //
    IEnumerable<IKGD_PROPERTY> Properties { get; }
    IEnumerable<IKGD_RELATION> Relations { get; }
    IEnumerable<IKGD_RELATION> RelationsOrdered { get; }
    //
    IEnumerable<int> TagsIds { get; }
    IEnumerable<IKCAT_Attribute> Tags { get; }
    IEnumerable<FS_Operations.FS_TreeNode<IKCAT_Attribute>> TagsNodes { get; }
    //
    IEnumerable<int> VariantsIds { get; }
    IEnumerable<IKATT_Attribute> Variants { get; }
    IEnumerable<FS_Operations.FS_TreeNode<IKATT_Attribute>> VariantsNodes { get; }
    //
    FS_Operations fsOp { get; }
    IKCMS_ModelCMS_ManagerVFS managerVFS { get; }
    //
    bool HasExceptions { get; set; }
    bool HasExceptionsTree { get; }
    //
    string Url { get; }
    string UrlBack { get; }
    string UrlCanonical { get; }
    bool? UrlCanonicalEnabled { get; set; }
    bool UrlBuilderRequiresParent { get; }
    //
    string UrlDownloadDefaultStream();
    string UrlDownloadDefaultStream(string stream);
    //
    IKCMS_ModelCMS_ModelInfo_Interface ModelInfo { get; set; }
    Expression<Func<IKGD_VNODE, bool>> RecursionVFSvNodeFilter { get; }
    Expression<Func<IKGD_VDATA, bool>> RecursionVFSvDataFilter { get; }
    Expression<Func<IKGD_VDATA, bool>> RecursionVFSvDataFilterSubFolders { get; }
    //
    bool Cleared { get; set; }
    void Clear();
    void Clear(bool? backwardRecurse, bool? forwardRecurse, bool? clearCacheEntries);
    //
    void Setup(FS_Operations.FS_NodeInfo_Interface fsNode, IKCMS_ModelCMS_Interface modelParent, IKCMS_ModelCMS_ModelInfo_Interface modelInfo, params object[] args);
    //
    void SetupExceptionHandler(Exception ex);
    //
    void RegisterHit(System.Web.Mvc.ViewDataDictionary ViewData, int? ActionCode, int? ActionSubCode);
    //
    List<FS_Operations.FS_NodeInfo_Interface> LinksFromRelations { get; }
    //
    List<IKCMS_ModelCMS_BreadCrumbsElement> BreadCrumbs { get; }
    //
    string UrlAddEncodedBackUrl(string targetUrl);
    //
    // titoli della pagina/risorsa
    //
    string TitleVFS { get; set; }
    string TitleHead { get; set; }
    string TitleH1 { get; set; }
    string TitleH2 { get; set; }
    //
    string HeaderMetaDescriptionRecursive { get; }
    string HeaderMetaDescription { get; set; }
    string HeaderMetaKeywords { get; set; }
    string HeaderMetaRobots { get; set; }
    //
    void EnsureHeadersAndTitles();
    //
  }


  public interface IKCMS_ModelCMS_InterfaceGen<T> : IKCMS_ModelCMS_Interface
    where T : IKCMS_HasSerializationCMS_Interface
  {
    new T VFS_ResourceObject { get; }
  }


  public interface IKCMS_ModelCMS_Widget_Interface : IKCMS_ModelCMS_Interface
  {
  }

  public interface IKCMS_ModelCMS_Folder_Interface : IKCMS_ModelCMS_Interface
  {
  }

  public interface IKCMS_ModelCMS_Template_Interface : IKCMS_ModelCMS_Interface
  {
  }


  //
  // interface con metodo custom per fare in modo di inserire il parent model nello stream di processing
  //
  public interface IKCMS_ModelCMS_EnsureParentModel_Interface
  {
    IKCMS_ModelCMS_Interface EnsureParentModel(FS_Operations.FS_NodeInfo_Interface fsNode, IKCMS_ModelCMS_ModelInfo_Interface modelInfo, params object[] args);
  }


  public interface IKCMS_ModelCMS_HasPostFinalizeMethod_Interface
  {
    void SetupFinalizePost(IKCMS_ModelCMS_Interface subModel, params object[] args);
  }



  public class ResourceVfsNotFoundException : Exception
  {
    public object ResourceData { get; private set; }
    public ResourceVfsNotFoundException() : base() { }
    public ResourceVfsNotFoundException(string message) : base(message) { }
    public ResourceVfsNotFoundException(object data) : base() { ResourceData = data; }
    public ResourceVfsNotFoundException(string message, object data) : base(message) { ResourceData = data; }
  }



  //
  // attributes used to decorate IKCMS_ModelCMS<T> derived classes
  //

  [Flags]
  public enum ModelRecursionModeEnum { RecursionNone = 1 << 0, RecursionOnResources = 1 << 1, RecursionOnFolders = 1 << 2 };

  public enum vfsNodeFetchModeEnum { vNode_vData, vNode_vData_iNode, vNode_vData_iNode_Extra, vNode_vData_iNode_ExtraVariants };


  //
  // classe base per la definizione degli attributi associati ai models
  //
  public interface IKCMS_ModelCMS_BaseAttribute_Interface : System.Runtime.InteropServices._Attribute
  {
  }


  //
  // Attribute: priorita' di registrazione del model
  // utilizzare priorita' negative per i models di libreria e positive per i custom models nei siti web
  //
  [global::System.AttributeUsage(AttributeTargets.All, Inherited = true, AllowMultiple = false)]
  public sealed class IKCMS_ModelCMS_PriorityAttribute : Attribute, IKCMS_ModelCMS_BaseAttribute_Interface
  {
    public double Priority { get; private set; }

    public IKCMS_ModelCMS_PriorityAttribute(double priority)
    {
      this.Priority = priority;
    }
  }


  //
  // Attribute: se presente blocca la ricorsione nella costruzione dei models (si usa per i widget)
  //
  [global::System.AttributeUsage(AttributeTargets.All, Inherited = true, AllowMultiple = false)]
  public sealed class IKCMS_ModelCMS_RecursionModeAttribute : Attribute, IKCMS_ModelCMS_BaseAttribute_Interface
  {
    public ModelRecursionModeEnum modelRecursionMode { get; private set; }

    public IKCMS_ModelCMS_RecursionModeAttribute(ModelRecursionModeEnum modelRecursionMode)
    {
      this.modelRecursionMode = ((int)modelRecursionMode < (int)ModelRecursionModeEnum.RecursionNone) ? ModelRecursionModeEnum.RecursionNone : modelRecursionMode;
    }
  }

  //
  // Attribute: se presente specifica il tipo di fsNode richiesto per la costruzione del model
  //
  [global::System.AttributeUsage(AttributeTargets.All, Inherited = true, AllowMultiple = false)]
  public sealed class IKCMS_ModelCMS_fsNodeModeAttribute : Attribute, IKCMS_ModelCMS_BaseAttribute_Interface
  {
    public vfsNodeFetchModeEnum vfsNodeFetchMode { get; private set; }

    public IKCMS_ModelCMS_fsNodeModeAttribute(vfsNodeFetchModeEnum fsNodeFetchMode)
    {
      this.vfsNodeFetchMode = fsNodeFetchMode;
    }
  }

  //
  // Attribute: se presente specifica la modalita' di fetch degli fsNodes dei child nella costruzione ricorsiva del Model tree
  //
  [global::System.AttributeUsage(AttributeTargets.All, Inherited = true, AllowMultiple = false)]
  public sealed class IKCMS_ModelCMS_fsNodeModeRecurseAttribute : Attribute, IKCMS_ModelCMS_BaseAttribute_Interface
  {
    public vfsNodeFetchModeEnum vfsNodeFetchMode { get; private set; }

    public IKCMS_ModelCMS_fsNodeModeRecurseAttribute(vfsNodeFetchModeEnum fsNodeFetchMode)
    {
      this.vfsNodeFetchMode = fsNodeFetchMode;
    }
  }


  //
  // Attribute: lista di types e/o interfaces dei ResourceType associati al Model
  // attenzione che questo attributo non viene ereditato
  //
  [global::System.AttributeUsage(AttributeTargets.All, Inherited = false, AllowMultiple = true)]
  public sealed class IKCMS_ModelCMS_ResourceTypesAttribute : Attribute, IKCMS_ModelCMS_BaseAttribute_Interface
  {
    public List<Type> Types { get; private set; }

    public IKCMS_ModelCMS_ResourceTypesAttribute(params Type[] resourceTypes)
    {
      Types = resourceTypes.Distinct().ToList();
    }
  }


  //
  // Attribute: lista di types e/o interfaces dei ResourceType associati al Model
  // attenzione che questo attributo non viene ereditato
  //
  [global::System.AttributeUsage(AttributeTargets.All, Inherited = false, AllowMultiple = true)]
  public sealed class IKCMS_ModelCMS_ResourceTypeCategoryAttribute : Attribute, IKCMS_ModelCMS_BaseAttribute_Interface
  {
    public Type ResourceType { get; private set; }
    public string Category { get; private set; }

    public IKCMS_ModelCMS_ResourceTypeCategoryAttribute(Type resourceType, string category)
    {
      ResourceType = resourceType;
      Category = category;
    }
  }



}
