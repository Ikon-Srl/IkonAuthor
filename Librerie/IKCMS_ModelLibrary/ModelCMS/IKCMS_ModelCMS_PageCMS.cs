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


namespace Ikon.IKCMS
{
  using Ikon.Config;
  using Ikon.GD;
  using Ikon.IKGD.Library;
  using Ikon.IKGD.Library.Resources;
  using Ikon.IKCMS.Library.Resources;



  //
  // Pagine CMS normali : abstract base class
  // attenzione che non esegue nessun setup dalle properties in KVT
  //
  [IKCMS_ModelCMS_ResourceTypes(typeof(IKCMS_Page_Interface))]
  //[IKCMS_ModelCMS_BootStrapperOpenGenerics(typeof(IKCMS_HasGenericBrick_Interface))]   // prima usavamo: [IKCMS_ModelCMS_BootStrapperOpenGenerics(typeof(IKCMS_HasPropertiesLanguageKVT_Interface))]
  [IKCMS_ModelCMS_RecursionMode(ModelRecursionModeEnum.RecursionOnResources)]
  [IKCMS_ModelCMS_fsNodeMode(vfsNodeFetchModeEnum.vNode_vData_iNode_Extra)]
  [IKCMS_ModelCMS_fsNodeModeRecurse(vfsNodeFetchModeEnum.vNode_vData_iNode)]
  public abstract class IKCMS_ModelCMS_PageCMS_Abstract<T> : IKCMS_ModelCMS<T>, IKCMS_ModelCMS_Page_Interface, IKCMS_ModelCMS_GenericBrickInterface, IKCMS_ModelCMS_GenericBrickInterfaceT<T>, IKCMS_ModelCMS_VFS_LanguageKVT_Interface, IKCMS_ModelCMS_VFS_KVT_Interface
    where T : class, IKCMS_HasGenericBrick_Interface
  {
    //
    // la lingua per le risorse KVT viene selezionata dalla lingua del fsNode e se null dalla lingua della sessione (comunque vale il fallback ai global fields)
    public IKCMS_HasGenericBrickSettings_Interface ResourceSettingsBase { get { return this.VFS_Resource.ResourceSettingsBase; } }
    public virtual KeyValueObjectTree VFS_ResourceKVT { get { return ResourceSettingsKVT_Wrapper; } }
    public virtual KeyValueObjectTree VFS_ResourceLanguageKVT(params string[] keys) { return ResourceSettingsKVT_Wrapper.KeyFilterTry(Language ?? IKGD_Language_Provider.Provider.Language, keys); }
    public virtual KeyValueObjectTree VFS_ResourceNoLanguageKVT(params string[] keys) { return ResourceSettingsKVT_Wrapper.KeyFilterTry(null, keys); }
    public virtual IEnumerable<KeyValueObjectTree> VFS_ResourceLanguageKVTs(params string[] keys) { return ResourceSettingsKVT_Wrapper.KeyFilterTryMulti(Language ?? IKGD_Language_Provider.Provider.Language, keys); }
    public virtual IEnumerable<KeyValueObjectTree> VFS_ResourceNoLanguageKVTs(params string[] keys) { return ResourceSettingsKVT_Wrapper.KeyFilterTryMulti(null, keys); }
    public virtual List<string> VFS_ResourceLanguageKVTss(params string[] keys) { return ResourceSettingsKVT_Wrapper.KeyFilterTryMulti(Language ?? IKGD_Language_Provider.Provider.Language, keys).Select(r => r.ValueString).Where(s => s != null).ToList(); }
    public virtual List<string> VFS_ResourceNoLanguageKVTss(params string[] keys) { return ResourceSettingsKVT_Wrapper.KeyFilterTryMulti(null, keys).Select(r => r.ValueString).Where(s => s != null).ToList(); }
    //
    public virtual string UrlModuleHome { get; protected set; }
    //
    public virtual string indexPath { get; set; }  // viene settato direttamente dal provider (es. per i moduli news) e si usa solo per la generazione delle Url
    public virtual string moduleOp { get; set; }    // viene settato direttamente dal provider (es. per i moduli news) e si usa solo per la generazione delle Url
    //
    // gestione della view e del template di visualizzazione
    // da utilizzare direttamente nella action e nella view
    //
    public IKCMS_PageCMS_Template_Interface TemplateInfo { get; set; }
    public string TemplateViewPath { get; set; }
    public IKCMS_PageCMS_Template_Interface TemplateInfoParent { get { return (this.ModelParent != null && this.ModelParent is IKCMS_ModelCMS_HasTemplateInfo_Interface) ? (this.ModelParent as IKCMS_ModelCMS_HasTemplateInfo_Interface).TemplateInfo : null; } }
    //
    protected virtual string TemplateTypePropertyName { get { return "TemplateType"; } }
    //
    public bool HasLink { get; set; }
    public string LinkUrl { get; set; }
    public string LinkTarget { get; set; }
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
    // setup del model dopo l'inizializzazione standard in IKCMS_ModelCMS
    //
    protected override void SetupInstance(FS_Operations.FS_NodeInfo_Interface fsNode, IKCMS_ModelCMS_Interface modelParent, IKCMS_ModelCMS_ModelInfo_Interface modelInfo, params object[] args)
    {
      base.SetupInstance(fsNode, modelParent, modelInfo, args);
      //
      moduleOp = (args.Length > 0) ? args[0] as string : null;
      indexPath = (args.Length > 1) ? args[1] as string : null;
      //
      //try { PreFetchPaths(); }
      //catch { }
      //
      // il tutto e' gia' gestito on demand nella classe base del model
      /*
      try
      {
        // generazione di url e url canonical (con override del metodo di default)
        int? sNodeModule = null;
        if (UrlBuilderRequiresParent)
          sNodeModule = ModelRoot.sNode;
        Url = IKCMS_RouteUrlManager.GetMvcUrlGeneral(sNodeModule, sNode, indexPath, true, false);
        // per la UrlCanonical indexPath viene incluso solo se sNodeModule e' definito analogamente alla policy usata per il caching dei models
        UrlCanonical = IKGD_SEO_Manager.MapOutcomingUrl(sNode, rNode, Language) ?? ((sNodeModule != null) ? Url : IKCMS_RouteUrlManager.GetMvcUrlGeneralV2(LanguageNN, sNode, null, "/" + Utility.UrlEncodeIndexPathForSEO(Name), false));
        //UrlCanonical = IKGD_SEO_Manager.MapOutcomingUrl(sNode) ?? ((sNodeModule != null) ? Url : IKCMS_RouteUrlManager.GetMvcUrlGeneral(null, sNode, null, true, false));
      }
      catch { }
      */
      //
      EnsureHeadersAndTitles();
      //
    }


    //
    // continuazione del setup del model dopo le features di base processate in IKCMS_ModelCMS
    //
    protected override void SetupFinalize(params object[] args)
    {
      base.SetupFinalize(args);
      //
      //try
      //{
      //}
      //catch { }
      //
      // commentato perche' altrimenti non inizializza i templates per le pagine appartenenti a models ricorsivi
      //if (ModelParent != null)
      //  return;
      //
      try
      {
        if (TemplateInfo == null || TemplateViewPath.IsNullOrEmpty())
        {
          // assegnazione dei templates solo per la pagine principali (non per widget e subPages del catalogo)
          string resourceTemplateType = this.TemplateVnode.NullIfEmpty() ?? Utility.FindPropertySafe<string>(VFS_ResourceObjectData, TemplateTypePropertyName);
          TemplateInfo = IKCMS_TemplatesTypeHelper.GetTemplateForType(this.VFS_Resource.GetType(), resourceTemplateType, this.Category, this.Placeholder);
          TemplateViewPath = TemplateInfo.ViewPath;
        }
      }
      catch { }
      //
      try
      {
        if (VFS_ResourceObjectData is WidgetSettingsType_FullUrl_Interface)
        {
          LinkUrl = this.ResourceSettingsBase.GetUrlFromResourceSettings(this.LanguageNN, true, true);
          LinkTarget = this.ResourceSettingsBase.GetUrlTargetFromResourceSettings();
          HasLink = !string.IsNullOrEmpty(LinkUrl) && LinkUrl != "javascript:;";
        }
      }
      catch { }
      //
    }


    public virtual void SetupFinalizePost(IKCMS_ModelCMS_Interface subModel, params object[] args)
    {
      try
      {
        //
        // questo codice viene eseguito solo per i parent models
        // quando esiste un ControllerContext negli args e quindi il metodo e' stato chiamato da un controller IKCMS
        if (subModel != null && args != null && args.OfType<System.Web.Mvc.ControllerContext>().Any())
        {
          try
          {
            if (TemplateInfo != null)
            {
              if (TemplateInfo.ViewPaths.ContainsKey("detail"))
              {
                TemplateViewPath = TemplateInfo.ViewPaths["detail"];
              }
            }
          }
          catch { }
        }
      }
      catch { }
    }


  }  //class IKCMS_ModelCMS_PageCMS_Abstract<T>



  [IKCMS_ModelCMS_ResourceTypes(typeof(Ikon.IKCMS.Library.Resources.IKCMS_ResourceType_PageCMS), typeof(Ikon.IKCMS.Library.Resources.IKCMS_ResourceType_MultiPageCMS))]
  [IKCMS_ModelCMS_BootStrapperOpenGenerics(typeof(IKCMS_HasGenericBrick_Interface))]
  [IKCMS_ModelCMS_Priority(-1999999)]
  public class IKCMS_ModelCMS_PageCMS<T> : IKCMS_ModelCMS_PageCMS_Abstract<T>
    where T : class, IKCMS_HasGenericBrick_Interface  //ricordarsi di utilizzare anche l'attribute IKCMS_ModelCMS_BootStrapperOpenGenericsAttribute
  {
  }  //class IKCMS_ModelCMS_PageCMS<T>


  //
  // Pagine Statiche CMS
  // vengono costruite come pagine normali del CMS per avere a disposizione tutto l'armamentario che serve per la costruzione del template
  // teasers, template, breadcrumbs, metatags, immagini, ...
  //
  [IKCMS_ModelCMS_ResourceTypes(typeof(Ikon.IKCMS.Library.Resources.IKCMS_ResourceType_PageStatic))]
  //[IKCMS_ModelCMS_BootStrapperOpenGenerics(typeof(IKCMS_HasGenericBrick_Interface))]
  [IKCMS_ModelCMS_Priority(-2999000)]
  public class IKCMS_ModelCMS_PageStatic<T> : IKCMS_ModelCMS_PageCMS<T>, IKCMS_ModelCMS_PageStatic_Interface
    where T : class, IKCMS_HasGenericBrick_Interface  //ricordarsi di utilizzare anche l'attribute IKCMS_ModelCMS_BootStrapperOpenGenericsAttribute
  {
    //
    // setup del model dopo l'inizializzazione standard in IKCMS_ModelCMS
    //
    protected override void SetupInstance(FS_Operations.FS_NodeInfo_Interface fsNode, IKCMS_ModelCMS_Interface modelParent, IKCMS_ModelCMS_ModelInfo_Interface modelInfo, params object[] args)
    {
      base.SetupInstance(fsNode, modelParent, modelInfo, args);
      //
      try
      {
        Url = Utility.ResolveUrl((this.VFS_Resource as Ikon.IKCMS.Library.Resources.IKCMS_ResourceType_PageStatic).ResourceSettings.UrlExternal);
      }
      catch { }
      try
      {
        //UrlCanonical = IKGD_SEO_Manager.MapOutcomingUrl(sNode) ?? Url;
        //UrlCanonical = IKGD_SEO_Manager.MapOutcomingUrl(sNode) ?? IKCMS_RouteUrlManager.GetMvcUrlGeneral(sNode) ?? Url;
        UrlCanonical = IKGD_SEO_Manager.MapOutcomingUrl(sNode, rNode, Language) ?? IKGD_SEO_Manager.MapOutcomingUrl(Url) ?? Url;
      }
      catch { }
      //
    }

  }  //class IKCMS_ModelCMS_PageStatic<T>



  [IKCMS_ModelCMS_ResourceTypes(typeof(Ikon.IKCMS.Library.Resources.IKCMS_ResourceType_ProductCMS))]
  [IKCMS_ModelCMS_BootStrapperOpenGenerics(typeof(IKCMS_HasGenericBrick_Interface))]
  [IKCMS_ModelCMS_fsNodeMode(vfsNodeFetchModeEnum.vNode_vData_iNode_ExtraVariants)]
  [IKCMS_ModelCMS_Priority(-2599999)]
  public class IKCMS_ModelCMS_ProductCMS<T> : IKCMS_ModelCMS_PageCMS_Abstract<T>
    where T : class, IKCMS_HasGenericBrick_Interface  //ricordarsi di utilizzare anche l'attribute IKCMS_ModelCMS_BootStrapperOpenGenericsAttribute
  {
  }  //class IKCMS_ModelCMS_ProductCMS<T>



}  //namespace
