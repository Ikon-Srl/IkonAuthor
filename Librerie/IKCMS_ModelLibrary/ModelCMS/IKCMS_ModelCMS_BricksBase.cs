/*
 * 
 * Intranet Ikon
 * 
 * Copyright (C) 2011 Ikon Srl
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
  using Ikon.IKGD.Library;
  using Ikon.IKGD.Library.Resources;
  using Ikon.IKGD.Library.Collectors;
  using Ikon.IKCMS.Library.Resources;



  public interface IKCMS_ModelCMS_WithLinkInterface
  {
    bool HasLink { get; }
    string LinkUrl { get; }
    string LinkTarget { get; }
    string LinkTargetFullString { get; }
    bool HasLinkTarget { get; }
  }


  public interface IKCMS_ModelCMS_WithMultiStreamInfo4SettingsInterface
  {
    bool HasStream(string stream);
    bool HasStream(string source, string key);
    IEnumerable<MultiStreamInfo4Settings> StreamInfos();
    MultiStreamInfo4Settings StreamInfos(string stream);
    MultiStreamInfo4Settings StreamInfos(string source, string key);
    IEnumerable<string> StreamSources();
    IEnumerable<string> StreamSourceKeys(string source);
  }


  public interface IKCMS_ModelCMS_GenericBrickInterface : IKCMS_ModelCMS_Interface, IKCMS_ModelCMS_Widget_Interface, IKCMS_ModelCMS_VFS_LanguageKVT_Interface, IKCMS_ModelCMS_WithMultiStreamInfo4SettingsInterface, IKCMS_ModelCMS_HasTemplateInfo_Interface, IKCMS_ModelCMS_WithLinkInterface
  {
    //
    IKCMS_HasGenericBrickSettings_Interface ResourceSettingsBase { get; }
    IKCMS_PageCMS_Template_Interface TemplateInfoParent { get; }
    //
    string Title { get; }
    string Text { get; }
    string CssClass { get; }
    bool HasTitle { get; }
    bool HasText { get; }
    bool HasCssClass { get; }
  }


  public interface IKCMS_ModelCMS_GenericBrickInterfaceT<T> : IKCMS_ModelCMS_InterfaceT<T>, IKCMS_ModelCMS_GenericBrickInterface
    where T : class, IKCMS_HasSerializationCMS_Interface
  {
  }


  public interface IKCMS_ModelCMS_GenericBrickSlotTeaserOrWidgetInterface : IKCMS_ModelCMS_GenericBrickInterface
  {
  }


  public interface ManagerTagFilterBase_Interface
  {
    // aggiungeremo via via solo i metodi che ci serviranno per generalizzare il supporto nel backend
    IKCMS_ModelCMS_Interface Model { get; }
    List<IKCMS_ModelCMS_GenericBrickInterface> GetModelsForTeasers(int maxNodes, string forcedSortingMode);
  }


  //
  // model base per la gestione di risorse tipo IKCMS_HasGenericResource_Interface
  //
  [IKCMS_ModelCMS_ResourceTypes(typeof(IKCMS_BrickBase_Interface))]
  [IKCMS_ModelCMS_BootStrapperOpenGenerics(typeof(IKCMS_HasGenericBrick_Interface))]
  [IKCMS_ModelCMS_RecursionMode(ModelRecursionModeEnum.RecursionNone)]
  //[IKCMS_ModelCMS_fsNodeMode(vfsNodeFetchModeEnum.vNode_vData)]
  [IKCMS_ModelCMS_fsNodeMode(vfsNodeFetchModeEnum.vNode_vData_iNode)]
  [IKCMS_ModelCMS_Priority(-2499900)]
  public abstract class IKCMS_ModelCMS_GenericBrickBase<T> : IKCMS_ModelCMS_WidgetCMS<T>, IKCMS_ModelCMS_GenericBrickInterface, IKCMS_ModelCMS_GenericBrickInterfaceT<T>, IKCMS_ModelCMS_Widget_Interface, IKCMS_ModelCMS_VFS_KVT_Interface, IKCMS_ModelCMS_VFS_LanguageKVT_Interface
    where T : class, IKCMS_HasGenericBrick_Interface
  {
    protected virtual string TemplateTypePropertyName { get { return "TemplateType"; } }
    public IKCMS_PageCMS_Template_Interface TemplateInfo { get; set; }
    public string TemplateViewPath { get; set; }
    public IKCMS_PageCMS_Template_Interface TemplateInfoParent { get { return (this.ModelParent != null && this.ModelParent is IKCMS_ModelCMS_HasTemplateInfo_Interface) ? (this.ModelParent as IKCMS_ModelCMS_HasTemplateInfo_Interface).TemplateInfo : null; } }
    //
    public IKCMS_HasGenericBrickSettings_Interface ResourceSettingsBase { get { return this.VFS_Resource.ResourceSettingsBase; } }
    public virtual KeyValueObjectTree VFS_ResourceKVT { get { return ResourceSettingsKVT_Wrapper; } }
    public virtual KeyValueObjectTree VFS_ResourceLanguageKVT(params string[] keys) { return ResourceSettingsKVT_Wrapper.KeyFilterTry(Language ?? IKGD_Language_Provider.Provider.Language, keys); }
    public virtual KeyValueObjectTree VFS_ResourceNoLanguageKVT(params string[] keys) { return ResourceSettingsKVT_Wrapper.KeyFilterTry(null, keys); }
    public virtual IEnumerable<KeyValueObjectTree> VFS_ResourceLanguageKVTs(params string[] keys) { return ResourceSettingsKVT_Wrapper.KeyFilterTryMulti(Language ?? IKGD_Language_Provider.Provider.Language, keys); }
    public virtual IEnumerable<KeyValueObjectTree> VFS_ResourceNoLanguageKVTs(params string[] keys) { return ResourceSettingsKVT_Wrapper.KeyFilterTryMulti(null, keys); }
    public virtual List<string> VFS_ResourceLanguageKVTss(params string[] keys) { return ResourceSettingsKVT_Wrapper.KeyFilterTryMulti(Language ?? IKGD_Language_Provider.Provider.Language, keys).Select(r => r.ValueString).Where(s => s != null).ToList(); }
    public virtual List<string> VFS_ResourceNoLanguageKVTss(params string[] keys) { return ResourceSettingsKVT_Wrapper.KeyFilterTryMulti(null, keys).Select(r => r.ValueString).Where(s => s != null).ToList(); }
    //
    public virtual bool HasLink { get; set; }
    public virtual string LinkUrl { get; set; }
    public virtual string LinkTarget { get; set; }
    //
    public bool HasLinkTarget { get { return !string.IsNullOrEmpty(LinkTarget) && IKGD_SiteMode.IsTargetSupported; } }
    public string LinkTargetFullString { get { return (string.IsNullOrEmpty(LinkTarget) || !IKGD_SiteMode.IsTargetSupported) ? string.Empty : string.Format("target='{0}'", LinkTarget); } }
    //
    public bool HasStream(string stream) { return StreamInfos(stream) != null; }
    public bool HasStream(string source, string key) { return VFS_ResourceKVT.Get(null, IKGD_Constants.IKGD_StreamsProcessorFieldName, source, key) != null; }
    public IEnumerable<MultiStreamInfo4Settings> StreamInfos() { return VFS_ResourceKVT.KeyFilterTry(null, IKGD_Constants.IKGD_StreamsProcessorFieldName).RecurseOnTree.Select(n => n.ValueComplex<MultiStreamInfo4Settings>()).Where(r => r != null); }
    public MultiStreamInfo4Settings StreamInfos(string stream) { var nodes = VFS_ResourceKVT.KeyFilterTry(null, IKGD_Constants.IKGD_StreamsProcessorFieldName).RecurseOnTree.Select(n => n.ValueComplex<MultiStreamInfo4Settings>()).Where(r => r != null); return nodes.FirstOrDefault(n => string.Equals(string.Format("{0}|{1}", n.Source, n.Key), stream, StringComparison.OrdinalIgnoreCase)) ?? nodes.FirstOrDefault(n => string.Equals(n.Key, stream, StringComparison.OrdinalIgnoreCase)); }
    public MultiStreamInfo4Settings StreamInfos(string source, string key) { return VFS_ResourceKVT.KeyFilterTry(null, IKGD_Constants.IKGD_StreamsProcessorFieldName, source, key).ValueComplex<MultiStreamInfo4Settings>(); }
    public IEnumerable<string> StreamSources() { return VFS_ResourceKVT.KeyFilterTry(null, IKGD_Constants.IKGD_StreamsProcessorFieldName).Nodes.Select(n => n.Key); }
    public IEnumerable<string> StreamSourceKeys(string source) { return VFS_ResourceKVT.KeyFilterTry(null, IKGD_Constants.IKGD_StreamsProcessorFieldName, source).Nodes.Select(n => n.Key); }
    //
    // Model.StreamInfos("") --> risorsa originale
    // Model.StreamInfos("|") --> risorsa originale
    // Model.StreamInfos(null, "") --> risorsa originale
    // Model.StreamInfos(null, "stream_name") --> ritaglio
    //
    public string Title { get { return VFS_ResourceLanguageKVT("Title").ValueString; } }
    public string Text { get { return VFS_ResourceLanguageKVT("Text").ValueString; } }
    public string CssClass { get { return VFS_ResourceNoLanguageKVT("CssClass").ValueString; } }
    //
    public bool HasTitle { get { return !string.IsNullOrEmpty(Title); } }
    public bool HasText { get { return !string.IsNullOrEmpty(Text); } }
    public bool HasCssClass { get { return !string.IsNullOrEmpty(CssClass); } }
    //


    //
    // continuazione del setup del model dopo le features di base processate in IKCMS_ModelCMS
    //
    protected override void SetupFinalize(params object[] args)
    {
      base.SetupFinalize(args);
      //
      try
      {
        if (VFS_ResourceObjectData is WidgetSettingsType_HasTemplateSelector_Interface)
        {
          //string resourceTemplateType = Utility.FindPropertySafe<string>(VFS_ResourceObjectData, TemplateTypePropertyName);
          //string resourceTemplateType = (VFS_ResourceObjectData as WidgetSettingsType_HasTemplateSelector_Interface).TemplateType;
          //TemplateInfo = IKCMS_TemplatesTypeHelper.GetTemplateForType(this.VFS_Resource.GetType(), resourceTemplateType, this.Category, this.Placeholder);
          //TemplateViewPath = TemplateInfo.ViewPaths["container"] ?? TemplateInfo.ViewPath;
          TemplateInfo = IKCMS_TemplatesTypeHelper.GetTemplateForType(this.VFS_Resource.GetType(), this.TemplateVnode.NullIfEmpty() ?? this.ResourceSettingsBase.TemplateType, this.Category, this.Placeholder);
          TemplateViewPath = TemplateInfo.ViewPath;
        }
      }
      catch { }
      //
      try
      {
        if (VFS_ResourceObjectData is WidgetSettingsType_FullUrl_Interface)
        {
          //WidgetSettingsType_FullUrl_Interface data = (VFS_ResourceObjectData as WidgetSettingsType_FullUrl_Interface);
          //LinkUrl = data.GetUrlFromResourceSettings(true);
          //LinkTarget = data.GetUrlTargetFromResourceSettings();
          //HasLink = !string.IsNullOrEmpty(LinkUrl) && LinkUrl != "javascript:;";
          //
          LinkUrl = this.ResourceSettingsBase.GetUrlFromResourceSettings(this.LanguageNN, true, true);
          LinkTarget = this.ResourceSettingsBase.GetUrlTargetFromResourceSettings();
          HasLink = !string.IsNullOrEmpty(LinkUrl) && LinkUrl != "javascript:;";
        }
      }
      catch { }
      //
    }

  }




  public interface IKCMS_ModelCMS_GenericBrickCollectorInterface : IKCMS_ModelCMS_GenericBrickInterface
  {
  }



  //
  // model base per la gestione di risorse tipo Brick con collector: IKCMS_BrickCollector_Interface
  //
  [IKCMS_ModelCMS_ResourceTypes(typeof(IKCMS_BrickCollector_Interface))]
  [IKCMS_ModelCMS_BootStrapperOpenGenerics(typeof(IKCMS_HasGenericBrick_Interface))]
  [IKCMS_ModelCMS_RecursionMode(ModelRecursionModeEnum.RecursionNone)]
  [IKCMS_ModelCMS_fsNodeMode(vfsNodeFetchModeEnum.vNode_vData_iNode_Extra)]
  [IKCMS_ModelCMS_fsNodeModeRecurse(vfsNodeFetchModeEnum.vNode_vData_iNode)]
  [IKCMS_ModelCMS_Priority(-2499900)]
  public abstract class IKCMS_ModelCMS_GenericBrickCollectorBase<T> : IKCMS_ModelCMS_GenericBrickBase<T>, IKCMS_ModelCMS_GenericBrickCollectorInterface, IKCMS_ModelCMS_GenericBrickInterface
    where T : class, IKCMS_HasGenericBrick_Interface
  {

    //
    // continuazione del setup del model dopo le features di base processate in IKCMS_ModelCMS
    //
    protected override void SetupFinalize(params object[] args)
    {
      base.SetupFinalize(args);
      //
      bool savedStatus = IKCMS_ModelCMS_Provider.Provider.managerVFS.Enabled;
      try
      {
        var relations = RelationsOrdered.Where(r => r.type == Ikon.IKGD.Library.IKGD_Constants.IKGD_LinkRelationName);
        // c'erano dei problemi al secondo run con rnode bisognava debuggare EnsureNodesRNODE quando c'erano nodi gia' presenti (risolto 17/09/2010)
        //IKCMS_ModelCMS_Provider.Provider.managerVFS.EnsureNodes<FS_Operations.FS_NodeInfo>(relations.Select(r => r.snode_dst), false);
        IKCMS_ModelCMS_Provider.Provider.managerVFS.EnsureNodesRNODE<FS_Operations.FS_NodeInfo>(relations.Select(r => r.rnode_dst), false);
        IKCMS_ModelCMS_Provider.Provider.managerVFS.Enabled = false;
        foreach (IKGD_RELATION relation in relations.OrderBy(r => r.position).ThenBy(r => r.version))
        {
          try
          {
            var fsNode = IKCMS_ModelCMS_Provider.Provider.managerVFS.GetVfsNode(relation.snode_dst, relation.rnode_dst);  // comanda sNode, in caso usa rNode se non trova sNode
            if (fsNode != null && fsNode.IsNotExpired)
            {
              IKCMS_ModelCMS_Interface model = IKCMS_ModelCMS_Provider.Provider.ModelBuild(this, fsNode, null);
            }
          }
          catch { }
        }
      }
      catch { }
      finally
      {
        IKCMS_ModelCMS_Provider.Provider.managerVFS.Enabled = savedStatus;
      }
      //
    }

  }


  public interface IKCMS_ModelCMS_GenericBrickWidgetInterface : IKCMS_ModelCMS_GenericBrickInterface
  {
  }


  //
  // model base per la gestione di risorse tipo Brick Widget: IKCMS_BrickWidget_Interface
  //
  [IKCMS_ModelCMS_ResourceTypes(typeof(IKCMS_BrickWidget_Interface))]
  [IKCMS_ModelCMS_BootStrapperOpenGenerics(typeof(IKCMS_HasGenericBrick_Interface))]
  [IKCMS_ModelCMS_RecursionMode(ModelRecursionModeEnum.RecursionNone)]
  [IKCMS_ModelCMS_fsNodeMode(vfsNodeFetchModeEnum.vNode_vData_iNode_Extra)]
  [IKCMS_ModelCMS_Priority(-2499900)]
  public abstract class IKCMS_ModelCMS_GenericBrickWidgetBase<T> : IKCMS_ModelCMS_GenericBrickBase<T>, IKCMS_ModelCMS_GenericBrickWidgetInterface, IKCMS_ModelCMS_GenericBrickInterface
    where T : class, IKCMS_HasGenericBrick_Interface
  {

    //
    // continuazione del setup del model dopo le features di base processate in IKCMS_ModelCMS
    //
    protected override void SetupFinalize(params object[] args)
    {
      base.SetupFinalize(args);
    }

  }


}  //namespace
