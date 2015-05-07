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
using System.Collections.Specialized;
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
using System.Configuration.Provider;
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
  using Ikon.IKCMS.Library.Resources;



  public static class IKCMS_ModelCMS_Provider
  {
    public static IKCMS_ModelCMS_Provider_Interface Provider { get; private set; }

    static IKCMS_ModelCMS_Provider()
    {
      Provider = IKCMS_ManagerIoC.applicationContainer.Resolve<IKCMS_ModelCMS_Provider_Interface>() as IKCMS_ModelCMS_Provider_Interface;
      Provider.Initialize();
    }


    public static T ModelBaseForContext<T>(this System.Web.Mvc.ViewDataDictionary viewData) where T : class, IKCMS_ModelCMS_Interface
    {
      try { return (Provider.ModelBaseForContext as T) ?? (viewData.Model as T); }
      catch { return null; }
    }

  }


  public interface IKCMS_ModelCMS_ModelInfo_Interface
  {
    Type TypeModel { get; }
    List<Type> ResourceTypes { get; }
    ILookup<Type, string> ResourceTypesCategory { get; }
    List<IKCMS_ModelCMS_BaseAttribute_Interface> Attributes { get; }
    double Priority { get; }
  }


  public interface IKCMS_ModelCMS_Provider_Interface
  {
    List<IKCMS_ModelCMS_ModelInfo_Interface> ModelInfos { get; }
    FS_Operations fsOp { get; }
    IKCMS_ModelCMS_ManagerVFS managerVFS { get; }
    Func<IKGD_Path, bool>[] pathFilters { get; }
    Func<IKGD_Path, bool>[] pathFiltersSingleOrNull { get; }
    bool IsMultiPageEnabled { get; }
    bool IsMultiPageBackScanEnabled { get; set; }

    void Initialize();

    IKCMS_ModelCMS_ModelInfo_Interface FindBestModelMatch(Type resourceType);
    List<Type> GetFullTypeInheritance(Type type);

    //
    // builder da VFS
    IKCMS_ModelCMS_Interface ModelBuildGeneric(params object[] args);
    IKCMS_ModelCMS_Interface ModelBuildGenericByRNODE(int rNode, params object[] args);
    IKCMS_ModelCMS_Interface ModelBuild(IKCMS_ModelCMS_Interface modelParent, int sNode, IKCMS_ModelCMS_ModelInfo_Interface modelInfo, params object[] args);
    IKCMS_ModelCMS_Interface ModelBuild(IKCMS_ModelCMS_Interface modelParent, FS_Operations.FS_NodeInfo_Interface fsNode, IKCMS_ModelCMS_ModelInfo_Interface modelInfo, params object[] args);
    //
    // builder alternativi
    IKCMS_ModelCMS_Interface ModelBuildFromResource(IKCMS_ModelCMS_Interface modelParent, IKCMS_ModelCMS_ModelInfo_Interface modelInfo, Type resourceType, params object[] args);
    IKCMS_ModelCMS_Interface ModelBuildFromType(IKCMS_ModelCMS_Interface modelParent, Type modelType, params object[] args);
    IKCMS_ModelCMS_Interface ModelBuildFromFullTree(IKCMS_ModelCMS_Interface modelParent, int? sNode, int? rNode, int? maxRecursionLevel, bool? fetchRelations);
    IKCMS_ModelCMS_Interface ModelBuildFromUrl(params string[] pathAndQueryStrings);
    IKCMS_ModelCMS_Interface ModelBuildFromContext();
    IKCMS_ModelCMS_Interface ModelBuildFromContext(bool returnDumbOnNull);
    IKCMS_ModelCMS_Interface ModelForContext { get; set; }
    IKCMS_ModelCMS_Interface ModelBaseForContext { get; set; }
    //
    void RemoveModelsFromCache(params IKCMS_ModelCMS_Interface[] models);
    void RemoveModelsFromCache(params string[] cacheKeys);
    //
    object[] GetNormalizedArgs(params object[] args);
    string GetCacheKey(params object[] argsNormalized);
    //
  }



  [IKCMS_ModelCMS_BootStrapperOrder(-1000000)]
  public abstract class IKCMS_ModelCMS_ProviderBase : IKCMS_ModelCMS_Provider_Interface, IBootStrapperAutofacTask
  {
    //
    protected object _lock = new object();
    //
    public List<IKCMS_ModelCMS_ModelInfo_Interface> ModelInfos { get; protected set; }
    //
    public FS_Operations fsOp { get { return IKCMS_ManagerIoC.requestContainer.ResolveNamed<FS_Operations>("readonly"); } }
    //public FS_Operations fsOp { get { return IKCMS_ManagerIoC.requestContainer.Resolve<FS_Operations>(); } }
    //
    public virtual bool ModelLanguageAutoChangeOnSession { get; protected set; }
    //
    public bool IsMultiPageEnabled { get; protected set; }
    public int maxRecursionLevelDefault { get; protected set; }
    //


    public IKCMS_ModelCMS_ManagerVFS managerVFS { get { return IKCMS_ModelCMS_ManagerVFS.Factory(); } }


    public IKCMS_ModelCMS_ProviderBase()
    {
      ModelLanguageAutoChangeOnSession = Utility.TryParse<bool>(IKGD_Config.AppSettings["ModelLanguageAutoChangeOnSession"], true);
      IsMultiPageEnabled = Utility.TryParse<bool>(IKGD_Config.AppSettings["ModelsMultiPageEnabled"], false);
      maxRecursionLevelDefault = Utility.TryParse<int>(IKGD_Config.AppSettings["ModelsMultiPageMaxRecursionLevel"], 5);
    }


    public void Initialize()
    {
      ModelInfos = new List<IKCMS_ModelCMS_ModelInfo_Interface>();
      //
      var models = Utility.FindTypesWithInterfaces(false, typeof(IKCMS_ModelCMS_Interface)).Where(t => !t.IsAbstract && t.IsClass).ToList();
      ModelInfos.AddRange(models.Select(t => new ModelInfo(t) as IKCMS_ModelCMS_ModelInfo_Interface).OrderBy(m => m.Priority).ThenBy(m => m.ResourceTypes.Count).ThenBy(m => m.TypeModel.Name));
    }


    void IBootStrapperAutofacTask.ExecuteAutofac(ContainerBuilder builder)
    {
      Type type = this.GetType();
      builder.RegisterType(type).As(typeof(IKCMS_ModelCMS_Provider_Interface)).SingleInstance();
    }


    //
    // non e' possibile definirli in una costante perche' altrimenti il language non viene gestito dinamicamente ma compilato con il suo valore iniziale...
    public virtual Func<IKGD_Path, bool>[] pathFiltersPre { get { return new Func<IKGD_Path, bool>[] { IKGD_Path_Helper.FilterByRootVFS, IKGD_Path_Helper.FilterByExpiry }; } }
    public virtual Func<IKGD_Path, bool>[] pathFilters { get { return new Func<IKGD_Path, bool>[] { IKGD_Path_Helper.FilterByRootCMS, IKGD_Path_Helper.FilterByActive, IKGD_Path_Helper.FilterByAreas, IKGD_Path_Helper.FilterByLanguage }; } }
    //public virtual Func<IKGD_Path, bool>[] pathFilters { get { return new Func<IKGD_Path, bool>[] { IKGD_Path_Helper.FilterByRootCMS, IKGD_Path_Helper.FilterByActive, IKGD_Path_Helper.FilterByAreas, IKGD_Path_Helper.FilterByExpiry, IKGD_Path_Helper.FilterByLanguage }; } }
    public virtual Func<IKGD_Path, bool>[] pathFiltersSingleOrNull { get { return new Func<IKGD_Path, bool>[] { IKGD_Path_Helper.FilterByRootCMS, IKGD_Path_Helper.FilterByActive, IKGD_Path_Helper.FilterByAreas, IKGD_Path_Helper.FilterByExpiry }; } }


    //
    // per assegnare un model di default al context e utilizzarlo in caso di override del ViewData nei controls
    //
    private static readonly string _ModelForContextVarName = "IKCMS_ModelForContext";
    public IKCMS_ModelCMS_Interface ModelForContext
    {
      get { return (HttpContext.Current == null ? null : HttpContext.Current.Items[_ModelForContextVarName]) as IKCMS_ModelCMS_Interface; }
      set { if (HttpContext.Current != null) HttpContext.Current.Items[_ModelForContextVarName] = value; }
    }


    private static readonly string _ModelBaseForContextVarName = "IKCMS_ModelBaseForContext";
    public IKCMS_ModelCMS_Interface ModelBaseForContext
    {
      get { return (HttpContext.Current == null ? null : HttpContext.Current.Items[_ModelBaseForContextVarName]) as IKCMS_ModelCMS_Interface; }
      set { if (HttpContext.Current != null) HttpContext.Current.Items[_ModelBaseForContextVarName] = value; }
    }


    public bool IsMultiPageBackScanEnabled
    {
      get
      {
        if (HttpContext.Current != null)
          return (bool)(HttpContext.Current.Items["IKCMS_ModelCMS_ProviderBase_MPBS"] ?? IsMultiPageEnabled);
        else
          return IsMultiPageEnabled;
      }
      set { HttpContext.Current.Items["IKCMS_ModelCMS_ProviderBase_MPBS"] = value; }
    }


    public List<Type> GetFullTypeInheritance(Type type)
    {
      List<Type> types = new List<Type>();
      if (type == null)
        return types;
      try
      {
        types.Add(type);
        for (Type t = type.BaseType; t != null && t.IsClass; t = t.BaseType)
          types.Add(t);
        types = types.Union(type.GetInterfaces()).ToList();
      }
      catch (Exception ex)
      {
        Elmah.ErrorSignal.FromCurrentContext().Raise(ex);
      }
      return types;
    }


    //per supportare anche le category, bisogna cambiare la API
    public IKCMS_ModelCMS_ModelInfo_Interface FindBestModelMatch(Type resourceType)
    {
      IKCMS_ModelCMS_ModelInfo_Interface modelInfo = null;
      try
      {
        modelInfo = ModelInfos.FirstOrDefault(m => m.TypeModel == resourceType);
        if (modelInfo != null)
          return modelInfo;
        //
        List<Type> resourceTypes = GetFullTypeInheritance(resourceType);
        //
        var compatibleModels = ModelInfos.Where(m => m.ResourceTypes.Any(t => t == resourceType));
        if (!compatibleModels.Any())
          compatibleModels = ModelInfos.Where(m => m.ResourceTypes.All(t => t.IsAssignableFrom(resourceType)));  // mapping basato sulla compatibilita' di tutti i types/interfaces coinvolte (e' piu' generico)
        //if (!compatibleModels.Any())
        //  compatibleModels = ModelInfos.Where(m => m.ResourceTypes.Except(resourceTypes).Any() == false);  // mapping basato esclusivamente sulle interfaces
        //
        // tra i modelli compatibili si tratta di assegnare quello con gli oggetti meno derivati, poi applico il sorting come in precedenza
        // TODO: (si tratta di un'ipotesi ancora da verificare dettagliatamente)
        modelInfo = compatibleModels.OrderBy(m => m.ResourceTypes.Count(r => resourceType.IsAssignableFrom(r))).ThenByDescending(m => m.Priority).ThenByDescending(m => ModelInfos.IndexOf(m)).FirstOrDefault();
        //modelInfo = compatibleModels.OrderByDescending(m => m.Priority).ThenByDescending(m => m.ResourceTypes.Count).ThenByDescending(m => ModelInfos.IndexOf(m)).FirstOrDefault();
        //
        if (modelInfo != null)
          return modelInfo;
        //
      }
      catch (Exception ex)
      {
        Elmah.ErrorSignal.FromCurrentContext().Raise(ex);
      }
      return modelInfo;
    }


    public object[] GetNormalizedArgs(params object[] args) { return (args != null && args.Length >= 4) ? args : ((args == null) ? Enumerable.Repeat<object>(null, 4) : args.Concat(Enumerable.Repeat<object>(null, 4 - args.Length))).ToArray(); }


    public string GetCacheKey(params object[] argsNormalized)
    {
      string cacheKey = "ModelBuilder_";
      if (IsMultiPageEnabled)
      {
        cacheKey += IsMultiPageBackScanEnabled ? "R1_" : "R0_";
      }
      if (argsNormalized[0] == null || argsNormalized[1] == null)
      {
        if (argsNormalized[0] != null || argsNormalized[1] != null)
        {
          cacheKey += FS_OperationsHelpers.ContextHashNN(argsNormalized[0], argsNormalized[1]);
        }
        else if (argsNormalized.OfType<System.Web.Mvc.ControllerContext>().Any())
        {
          // costruzione della cache hash key a partire dai parametri della route/controller
          System.Web.Mvc.ControllerContext ctx = argsNormalized.OfType<System.Web.Mvc.ControllerContext>().FirstOrDefault();
          cacheKey += FS_OperationsHelpers.ContextHashNN(ctx.RouteData.Values.Select(r => r.Value).ToArray());
        }
        else if (argsNormalized.Skip(2).Any(a => a != null))
        {
          cacheKey += FS_OperationsHelpers.ContextHashNN(argsNormalized);
        }
        else
        {
          cacheKey = null;
        }
      }
      else
        cacheKey += FS_OperationsHelpers.ContextHashNN(argsNormalized[0], argsNormalized[1], argsNormalized[2], argsNormalized[3]);
      return cacheKey;
    }


    //
    // args:
    // [0] -> int/string: contenente sNode o path della risorsa con la quale costruire il model
    // [1] -> int/string: contenente sNode o path del modulo di visualizzazione (per moduli tipo news) o del master template 
    //        viene utilizzato per le risorse che richiedono un parent model
    // [2] -> string: contenente moduleOp (index/item/auto)
    // [3] -> string: contenente l'indexPath per la generazione dell'index dei moduli tipo browse/news
    //
    public IKCMS_ModelCMS_Interface ModelBuildGeneric(params object[] args)
    {
      IKCMS_ExecutionProfiler.AddMessage("ModelBuildGeneric: START");
      IKCMS_ModelCMS_Interface model = null;
      //
      // generazione dei models con supporto della cache
      //
      object[] argsNormalized = GetNormalizedArgs(args);
      //
      IKCMS_ExecutionProfiler.AddMessage("ModelBuildGeneric: argsNormalized={0}".FormatString(Utility.Implode(argsNormalized, ",")));
      if (Utility.TryParse<bool>(IKGD_Config.AppSettingsWeb["CachingIKCMS_ModelsEnabled"], true) && Utility.TryParse<bool>(System.Web.HttpContext.Current.Request.QueryString["cacheOff"]) == false)
      {
        string cacheKey = GetCacheKey(argsNormalized);
        model = FS_OperationsHelpers.CachedEntityWrapper(cacheKey, () =>
        {
          IKCMS_ExecutionProfiler.AddMessage(string.Format("ModelBuildGeneric: NOT FOUND IN CACHE arg0[{0}] arg1[{1}]", argsNormalized[0], argsNormalized[1]));
          IKCMS_ModelCMS_Interface mdl = ModelBuildGenericWorker(argsNormalized);
          if (mdl != null)
          {
            mdl.CacheKey = cacheKey;
          }
          return mdl;
        }
        , m => m != null && !m.HasExceptionsTree && !managerVFS.HasExceptions && !managerVFS.HasErrors && !IKCMS_ExceptionsManager.ExceptionsAnyOf(typeof(IKCMS_Exception_ModelBuilder), typeof(IKCMS_Exception_ManagerVFS))
        , Utility.TryParse<int>(IKGD_Config.AppSettings["CachingIKCMS_Models"], 3600), null, FS_OperationsHelpers.Const_CacheDependencyIKGD_vNode_vData_iNode_Relation_Property);
      }
      else
      {
        IKCMS_ExecutionProfiler.AddMessage("ModelBuildGeneric: CACHE DISABLED");
        model = ModelBuildGenericWorker(argsNormalized);
      }
      //
      if (model != null)
      {
        string language = null;
        try { language = model.BackRecurseOnModels.Where(m => m.Language != null).Select(m => m.Language).FirstOrDefault() ?? model.Language; }
        catch { }
        if (!string.IsNullOrEmpty(language))
        {
          if (IKGD_Language_Provider.Provider.LanguageContext == null)
          {
            if (ModelLanguageAutoChangeOnSession)
            {
              if (!string.Equals(language, IKGD_Language_Provider.Provider.LanguageSession, StringComparison.OrdinalIgnoreCase))
                IKGD_Language_Provider.Provider.Language = language;
            }
            else if (!string.Equals(language, IKGD_Language_Provider.Provider.LanguageNN, StringComparison.OrdinalIgnoreCase))
            {
              IKGD_Language_Provider.Provider.LanguageContext = language;
            }
          }
        }
      }
      //
      if (model != null)
      {
        if (ModelForContext == null)
        {
          ModelForContext = model;
        }
      }
      //
      IKCMS_ExecutionProfiler.AddMessage(string.Format("ModelBuildGeneric: IKCMS_ModelCMS_ManagerVFS reads={1} ms={0}", managerVFS.CumulatedWaits * 1000, managerVFS.CumulatedReads));
      IKCMS_ExecutionProfiler.AddMessage("ModelBuildGeneric: END");
      return model;
    }


    public IKCMS_ModelCMS_Interface ModelBuildGenericByRNODE(int rNode, params object[] args)
    {
      object[] argsNormalized = (args.Length >= 3) ? args : args.Concat(Enumerable.Repeat<object>(null, 3 - args.Length)).ToArray();
      if (argsNormalized[2] == null)
        argsNormalized[2] = HttpContext.Current.Request.Params["sNodeFragFilter"];
      //
      // generazione dei models con supporto della cache
      //
      IKCMS_ModelCMS_Interface model = null;
      if (Utility.TryParse<bool>(IKGD_Config.AppSettingsWeb["CachingIKCMS_ModelsEnabled"], true) && Utility.TryParse<bool>(System.Web.HttpContext.Current.Request.QueryString["cacheOff"]) == false)
      {
        string cacheKey = "ModelBuilder_RNODE_";
        if (IsMultiPageEnabled)
        {
          cacheKey += IsMultiPageBackScanEnabled ? "R1_" : "R0_";
        }
        cacheKey += FS_OperationsHelpers.ContextHashNN(rNode, argsNormalized[0], argsNormalized[1]);
        model = FS_OperationsHelpers.CachedEntityWrapper(cacheKey, () =>
        {
          IKCMS_ModelCMS_Interface mdl = ModelBuildGenericWorkerRNODE(rNode, argsNormalized);
          mdl.CacheKey = cacheKey;
          return mdl;
        }
        , m => m != null && !m.HasExceptionsTree && !managerVFS.HasExceptions && !managerVFS.HasErrors && !IKCMS_ExceptionsManager.ExceptionsAnyOf(typeof(IKCMS_Exception_ModelBuilder), typeof(IKCMS_Exception_ManagerVFS))
        , Utility.TryParse<int>(IKGD_Config.AppSettings["CachingIKCMS_Models"], 3600), null, FS_OperationsHelpers.Const_CacheDependencyIKGD_vNode_vData_iNode_Relation_Property);
      }
      else
      {
        model = ModelBuildGenericWorkerRNODE(rNode, argsNormalized);
      }
      //
      if (model != null)
      {
        string language = null;
        try { language = model.BackRecurseOnModels.Where(m => m.Language != null).Select(m => m.Language).FirstOrDefault() ?? model.Language; }
        catch { }
        if (!string.IsNullOrEmpty(language))
        {
          if (IKGD_Language_Provider.Provider.LanguageContext == null)
          {
            if (ModelLanguageAutoChangeOnSession)
            {
              if (!string.Equals(language, IKGD_Language_Provider.Provider.LanguageSession, StringComparison.OrdinalIgnoreCase))
                IKGD_Language_Provider.Provider.Language = language;
            }
            else if (!string.Equals(language, IKGD_Language_Provider.Provider.LanguageNN, StringComparison.OrdinalIgnoreCase))
            {
              IKGD_Language_Provider.Provider.LanguageContext = language;
            }
          }
        }
      }
      //
      if (ModelForContext == null && model != null)
        ModelForContext = model;
      //
      return model;
    }


    // qesto metodo dovrebbe essere chiamato con almeno 4 argomenti (anche nulli)
    protected IKCMS_ModelCMS_Interface ModelBuildGenericWorker(params object[] args)
    {
      object arg00 = (args.Length > 0) ? args[0] : null;  // (int?)sNode|(string)path  item
      object arg01 = (args.Length > 1) ? args[1] : null;  // (int?)sNode|(string)path  module
      //object arg02 = (args.Length > 2) ? args[2] : null;  // (string)moduleOp
      //object arg03 = (args.Length > 3) ? args[3] : null;  // (string)indexPath
      //object arg04 = (args.Length > 4) ? args[4] : null;  // ControllerContext (settato se viene chiamato dal controller del CMS)
      //
      object[] argsNext = args.Skip(2).ToArray();
      //
      IKCMS_ModelCMS_Interface model = null;
      IKCMS_ModelCMS_Interface modelParent = null;
      IKCMS_ModelCMS_Interface modelToReturn = null;
      try
      {
        //
        // processing del template / modulo news container richiesto
        // da eseguire prima del setup del model che potrebbe anche essere == null (accade per esempio nel caso di index delle news senza item selezionato)
        //
        if (arg01 != null)
        {
          List<IKGD_Path> fsPathsModuleAll = null;
          if (arg01 is string)
          {
            fsPathsModuleAll = fsOp.PathsFromFragments(Utility.UrlDecodePathTofrags_IIS(arg01 as string), false, true, null, true, false);
          }
          else
          {
            //fsPathsModuleAll = fsOp.PathsFromNodeExt((int)arg01, false, false, true);
            fsPathsModuleAll = fsOp.PathsFromNodeExt((int)arg01, false, true, false);
            if (!fsPathsModuleAll.Any() && !Utility.TryParse<bool>(IKGD_Config.AppSettings["IKGD_Path_forceNoOpt"], false) == false)
              fsPathsModuleAll = fsOp.PathsFromNodeExt((int)arg01, false, true, true, true);  //force NoOpt
          }
          if (pathFiltersPre != null && pathFiltersPre.Any() && fsPathsModuleAll.Any())
          {
            fsPathsModuleAll = fsPathsModuleAll.FilterCustom(pathFiltersPre).ToList();
          }
          IKGD_Path fsPathModule = fsPathsModuleAll.FilterCustom(pathFilters).OrderByACL().FirstOrDefault();
          fsPathModule = fsPathModule ?? fsPathsModuleAll.OrderByDescending(p => p.Fragments.Count(f => !string.IsNullOrEmpty(f.Language))).FilterFallback(pathFiltersSingleOrNull).OrderByACL().FirstOrDefault();
          if (fsPathModule != null)
          {
            if (!fsPathModule.IsLanguageAccessible())
            {
              IKGD_Language_Provider.Provider.LanguageContext = fsPathModule.FirstLanguageNN;
            }
            try { modelParent = ModelBuild(null, fsPathModule.sNode, null, argsNext); }
            catch (Exception ex)
            {
              Elmah.ErrorSignal.FromCurrentContext().Raise(ex);
            }
            if (modelParent == null)
            {
              managerVFS.RegisterNode(fsOp.Get_NodeInfoExtACL(fsPathModule.sNode));
              try { modelParent = ModelBuild(null, fsPathModule.sNode, null, argsNext); }
              catch (Exception ex)
              {
                Elmah.ErrorSignal.FromCurrentContext().Raise(ex);
              }
            }
          }
        }
        if (arg00 != null)
        {
          //
          // processing del nodo principale richiesto
          //
          int? sNode_tmp = null;
          List<IKGD_Path> fsPathsAll = null;
          if (arg00 is string)
          {
            fsPathsAll = fsOp.PathsFromFragments(Utility.UrlDecodePathTofrags_IIS(arg00 as string), false, true, null, true, false);
          }
          else
          {
            sNode_tmp = (int)arg00;  //funziona anche con int?
            //fsPathsAll = fsOp.PathsFromNodeExt(sNode_tmp.Value, false, true, true);
            fsPathsAll = fsOp.PathsFromNodeExt(sNode_tmp.Value, false, true, false);
            if (!fsPathsAll.Any() && !Utility.TryParse<bool>(IKGD_Config.AppSettings["IKGD_Path_forceNoOpt"], false) == false)
              fsPathsAll = fsOp.PathsFromNodeExt(sNode_tmp.Value, false, true, true, true);  //force NoOpt
          }
          if (pathFiltersPre != null && pathFiltersPre.Any() && fsPathsAll.Any())
          {
            fsPathsAll = fsPathsAll.FilterCustom(pathFiltersPre).ToList();
          }
          IKGD_Path fsPath = fsPathsAll.FilterCustom(pathFilters).OrderByACL().FirstOrDefault();
          fsPath = fsPath ?? fsPathsAll.OrderByDescending(p => p.Fragments.Count(f => !string.IsNullOrEmpty(f.Language))).FilterFallback(pathFiltersSingleOrNull).OrderByACL().FirstOrDefault();
          //
          IKCMS_ExecutionProfiler.AddMessage("ModelBuildGenericWorker: fsPath=[{0}]{1}".FormatString(fsPath.Return(p => p.sNode), fsPath.Return(p => p.Path)));
          //
          // verifica che non si tratti di un tentativo di costruzione di un model da una risorsa non strutturata (es. link interno ad un allegato)
          if (fsPath != null && fsPath.LastFragment.flag_unstructured)
          {
            //TODO: controllare che la risorsa abbia inode e mime riconosciuti. passare al proxyVFS anche l'eventuale query string
            // cosi' evitiamo la costruzione del model e i processing del controller
            //HttpContext.Current.Response.Redirect(IKCMS_RouteUrlManager.GetUrlProxyVFS(null, fsPath.sNode, null, null, null, false, null), true);
            //HttpContext.Current.Response.End();
            HttpContext.Current.Response.Redirect(IKCMS_RouteUrlManager.GetUrlProxyVFS(null, fsPath.sNode, null, null, null, false, null), false);
            HttpContext.Current.ApplicationInstance.CompleteRequest();  // da usare al posto di .Response.End();
            return null;
          }
          //if (fsPath != null && fsPath.IsNotExpired())  //gestito nel controller IKCMS
          if (fsPath != null)
          {
            if (!fsPath.IsLanguageAccessible())
            {
              IKGD_Language_Provider.Provider.LanguageContext = fsPath.FirstLanguageNN;
            }
            //
            model = ModelBuildGenericWorkerWithBackScan(fsPath, modelParent, argsNext);
            //
          }
        }
        //
        if (model != null && modelParent == null)
          modelParent = model.ModelParent;
        //
        if (modelParent != null && modelParent is IKCMS_ModelCMS_HasPostFinalizeMethod_Interface)
        {
          (modelParent as IKCMS_ModelCMS_HasPostFinalizeMethod_Interface).SetupFinalizePost(model, argsNext);
        }
        else if (model != null && model is IKCMS_ModelCMS_HasPostFinalizeMethod_Interface)
        {
          (model as IKCMS_ModelCMS_HasPostFinalizeMethod_Interface).SetupFinalizePost(null, argsNext);
        }
        //
        if (model != null && model.ModelParent == null && typeof(IKCMS_ModelCMS_Widget_Interface).IsAssignableFrom(model.GetType()))
        {
          // TODO:
          // verificare il tipo corretto di check per l'if (forse serve != Page?)
          // generazione della pagina contenitore nel caso sia stato richiesto solo il widget di una pagina, e' utile per il search engine
          modelToReturn = model.ModelRoot;
        }
        //
      }
      catch (Exception ex)
      {
        Elmah.ErrorSignal.FromCurrentContext().Raise(ex);
      }
      //
      modelToReturn = modelToReturn ?? model ?? modelParent;
      //
      return modelToReturn;
    }


    //
    // NB non vengono usati i primi due args e quindi si comporta in maniera differente da ModelBuildGenericWorker
    //
    protected IKCMS_ModelCMS_Interface ModelBuildGenericWorkerRNODE(int rNode, params object[] args)
    {
      IKCMS_ModelCMS_Interface model = null;
      IKCMS_ModelCMS_Interface modelParent = null;
      IKCMS_ModelCMS_Interface modelToReturn = null;
      bool savedStatus = IKCMS_ModelCMS_Provider.Provider.managerVFS.Enabled;
      try
      {
        //
        IKCMS_ModelCMS_Provider.Provider.managerVFS.Enabled = true;
        var fsPathsAll = fsOp.PathsFromNodesExt(null, new int[] { rNode }, false, true, false);
        if (!fsPathsAll.Any() && Utility.TryParse<bool>(IKGD_Config.AppSettings["IKGD_Path_forceNoOpt"], false) == false)
          fsPathsAll = fsOp.PathsFromNodesExt(null, new int[] { rNode }, false, true, true, false);  //force NoOpt
        if (pathFiltersPre != null && pathFiltersPre.Any() && fsPathsAll.Any())
        {
          fsPathsAll = fsPathsAll.FilterCustom(pathFiltersPre).ToList();
        }
        fsPathsAll = fsPathsAll.FilterPathsByLanguage().ToList();  // solo i path che non hanno languages mescolati e sono compatibili con la linua corrente (usando rNode non e' previsto un cambio lingua)
        IKGD_Path fsPath = fsPathsAll.FilterFallback(pathFilters).OrderByACL().FirstOrDefault();
        if (args[2] != null && args[2] is string && !string.IsNullOrEmpty((string)args[2]))
        {
          // TODO:
          // ordinare i path per numero di matches, quindi per index del last match e poi per path sorter
          // in caso di non match prende il primo path valido per le root cms
          //
          var sNodes = Utility.ExplodeT<int>((string)args[2], ",", " ", true);
          fsPath = fsPathsAll.Where(p => p.Fragments.Any(f => sNodes.Contains(f.sNode))).FilterFallback(pathFilters).OrderByACL().FirstOrDefault() ?? fsPath;
        }
        //
        if (fsPath != null && !fsPath.IsLanguageAccessible())
          IKGD_Language_Provider.Provider.LanguageContext = fsPath.FirstLanguageNN;
        //
        // processing del nodo
        //
        if (fsPath != null)
        {
          managerVFS.EnsureNodes<FS_Operations.FS_NodeInfo>(new int[] { fsPath.sNode });
        }
        else
        {
          managerVFS.EnsureNodesRNODE<FS_Operations.FS_NodeInfo>(new int[] { rNode }, false);
        }
        //
        //FS_Operations.FS_NodeInfo_Interface fsNode = managerVFS.GetVfsNode(fsPath.sNode, rNode);
        FS_Operations.FS_NodeInfo_Interface fsNode = managerVFS.GetVfsNode((fsPath != null ? (int?)fsPath.sNode : null), rNode);
        //
        //if (fsNode != null && fsNode.IsNotExpired)  //gestito nel controller IKCMS
        if (fsNode != null)
        {
          if (fsPath == null)
          {
            // se non abbiamo gia' ottenuto un path rinunciamo ad effettuare lo scan per modelbuild ricorsivi
            //fsPath = fsOp.PathsFromNodeExt(fsNode.sNode, false, true, false).FirstOrDefault();
            model = ModelBuild(modelParent, fsNode, null, args);
          }
          else
          {
            model = ModelBuildGenericWorkerWithBackScan(fsPath, modelParent, args);
          }
        }
        //
        if (model != null && modelParent == null)
          modelParent = model.ModelParent;
        //
        if (modelParent != null && modelParent is IKCMS_ModelCMS_HasPostFinalizeMethod_Interface)
        {
          (modelParent as IKCMS_ModelCMS_HasPostFinalizeMethod_Interface).SetupFinalizePost(model, args);
        }
        //
        if (model != null && model.ModelParent == null && typeof(IKCMS_ModelCMS_Widget_Interface).IsAssignableFrom(model.GetType()))
        {
          modelToReturn = model.ModelRoot;
        }
        //
      }
      catch (Exception ex)
      {
        Elmah.ErrorSignal.FromCurrentContext().Raise(ex);
      }
      finally
      {
        IKCMS_ModelCMS_Provider.Provider.managerVFS.Enabled = savedStatus;
      }
      //
      modelToReturn = modelToReturn ?? model ?? modelParent;
      //
      return modelToReturn;
    }


    //
    // TODO
    // aggiungere un check del model richiesto in fase di costruzione
    // per i model multipage associarli anche ad un hash della vera root in modo
    // da poterlo riciclare in build successivi (uno stesso model potrebbe essere associato a 2 o piu' caching keys)
    //
    protected IKCMS_ModelCMS_Interface ModelBuildGenericWorkerWithBackScan(IKGD_Path fsPath, IKCMS_ModelCMS_Interface modelParent, object[] argsNext)
    {
      IKCMS_ModelCMS_Interface model = null;
      if (fsPath == null)
        return model;
      //
      IKGD_Path_Fragment fragRecursive = null;
      if (IsMultiPageEnabled)
      {
        if (IsMultiPageBackScanEnabled)
        {
          fragRecursive = fsPath.Fragments.LastOrDefault(f => f.ManagerType == typeof(IKCMS_ResourceType_MultiPageCMS).Name);
        }
        else if (fsPath.LastFragment.ManagerType == typeof(IKCMS_ResourceType_MultiPageCMS).Name)
        {
          fragRecursive = fsPath.LastFragment;
        }
        if (fragRecursive != null)
        {
          int maxRecursionLevel = maxRecursionLevelDefault;
          //
          var nodeStart = managerVFS.GetVfsNode(fragRecursive.sNode, null);
          if (nodeStart == null)
          {
            nodeStart = fsOp.Get_NodeInfoExtACL(fragRecursive.sNode);
          }
          //
          try
          {
            IKCMS_ResourceType_MultiPageCMS resource = new IKCMS_ResourceType_MultiPageCMS();
            resource.ResourceSettings_Constructor(nodeStart.vData);
            maxRecursionLevel = resource.ResourceSettings.RecursionLevelMax ?? maxRecursionLevel;
          }
          catch { }
          //
          // controllo che la pagina richiesta non sia oltre al limite della ricorsione, in tal caso viene effettuato un build normale
          // TODO: verificare che combaci con i limiti effettivi di ricorsione del tree scan (NB recursion=1 --> model build normale)
          int idxRec = fsPath.Fragments.IndexOf(fragRecursive);
          //if (fsPath.Fragments.Count - idxRec > maxRecursionLevel + 1)
          if (fsPath.Fragments.Count - idxRec > maxRecursionLevel)
          {
            fragRecursive = null;
          }
          //
          if (fragRecursive != null)
          {
            bool savedStatus = IKCMS_ModelCMS_Provider.Provider.managerVFS.Enabled;
            try
            {
              bool? fetchRelations = null;
              //
              // fetch ricorsivo del subtree, con pulizia preliminare per evitare eventuali eredita' di altri modelbuilders
              IKCMS_ModelCMS_Provider.Provider.managerVFS.Clear();
              IKCMS_ModelCMS_Provider.Provider.managerVFS.EnsureTrees<FS_Operations.FS_NodeInfoExt>(maxRecursionLevel, fetchRelations, fragRecursive.rNode);
              var treeNodeRoot = IKCMS_ModelCMS_Provider.Provider.managerVFS.NodesTree.FirstOrDefault(n => n.Data.sNode == fragRecursive.sNode);
              //
              // usiamo solo i nodi appena caricati senza fare ulteriori accessi al DB durante la costruzione ricorsiva
              IKCMS_ModelCMS_Provider.Provider.managerVFS.Enabled = false;
              model = this.ModelBuildFromTreeWorker(modelParent, treeNodeRoot);
              //
              if (model is IKCMS_ModelCMS && model.sNodeReference != fsPath.sNode)
              {
                (model as IKCMS_ModelCMS).sNodeReference = fsPath.sNode;
              }
              //
            }
            catch (Exception ex)
            {
              Elmah.ErrorSignal.FromCurrentContext().Raise(ex);
            }
            finally
            {
              IKCMS_ModelCMS_Provider.Provider.managerVFS.Enabled = savedStatus;
            }
          }
          //
        }
      }
      //
      if (model == null)
      {
        try { model = ModelBuild(modelParent, fsPath.sNode, null, argsNext); }
        catch (Exception ex)
        {
          Elmah.ErrorSignal.FromCurrentContext().Raise(ex);
        }
        if (model == null)
        {
          managerVFS.RegisterNode(fsOp.Get_NodeInfoExtACL(fsPath.sNode));
          try { model = ModelBuild(modelParent, fsPath.sNode, null, argsNext); }
          catch (Exception ex)
          {
            Elmah.ErrorSignal.FromCurrentContext().Raise(ex);
          }
        }
      }
      //
      return model;
    }


    public IKCMS_ModelCMS_Interface ModelBuild(IKCMS_ModelCMS_Interface modelParent, int sNode, IKCMS_ModelCMS_ModelInfo_Interface modelInfo, params object[] args)
    {
      IKCMS_ModelCMS_Interface model = null;
      try
      {
        if (modelInfo == null)
        {
          string manager_type = null;
          try { manager_type = manager_type ?? managerVFS[sNode].vData.manager_type; }
          catch { }
          try { manager_type = manager_type ?? fsOp.PathsFragsCollectionContext().FirstOrDefault(f => f.sNode == sNode).ManagerType; }  //TODO: aggiungere il supporto per la lingua nei filtri di PathsFragsCollectionContext
          catch { }
          try
          {
            //managerVFS.EnsureNodes<FS_Operations.FS_NodeInfo>(sNode);
            //manager_type = manager_type ?? managerVFS[sNode].vData.manager_type;
            manager_type = manager_type ?? fsOp.PathsFromNodeExt(sNode).FirstOrDefault().LastFragment.ManagerType;  // per non fare un eventuale fetch a vuoto con FS_NodeInfo al posto di FS_NodeInfoExt
          }
          catch { }
          if (manager_type != null)
            modelInfo = FindBestModelMatch(Utility.FindTypeCached(manager_type));
        }
        vfsNodeFetchModeEnum fsNodeFetchMode = vfsNodeFetchModeEnum.vNode_vData_iNode;
        try { fsNodeFetchMode = modelInfo.Attributes.OfType<IKCMS_ModelCMS_fsNodeModeAttribute>().Select(a => a.vfsNodeFetchMode).DefaultIfEmpty(vfsNodeFetchModeEnum.vNode_vData).FirstOrDefault(); }
        catch { }
        switch (fsNodeFetchMode)
        {
          case vfsNodeFetchModeEnum.vNode_vData_iNode_ExtraVariants:
            managerVFS.EnsureNodes<FS_Operations.FS_NodeInfoExt2>(sNode);
            break;
          case vfsNodeFetchModeEnum.vNode_vData_iNode_Extra:
            managerVFS.EnsureNodes<FS_Operations.FS_NodeInfoExt>(sNode);
            break;
          default:
            managerVFS.EnsureNodes<FS_Operations.FS_NodeInfo>(sNode);
            break;
        }
      }
      catch
      {
        managerVFS.EnsureNodes<FS_Operations.FS_NodeInfo>(sNode);
      }
      try { model = ModelBuild(modelParent, managerVFS[sNode].Data, modelInfo, args); }
      catch { }
      return model;
    }


    public IKCMS_ModelCMS_Interface ModelBuild(IKCMS_ModelCMS_Interface modelParent, FS_Operations.FS_NodeInfo_Interface fsNode, IKCMS_ModelCMS_ModelInfo_Interface modelInfo, params object[] args)
    {
      IKCMS_ModelCMS_Interface model = null;
      if (fsNode == null)
        return model;
      try
      {
        Type resourceType = Utility.FindTypeCached(fsNode.vData.manager_type);
        modelInfo = modelInfo ?? FindBestModelMatch(resourceType);
        var modeDefault = vfsNodeFetchModeEnum.vNode_vData_iNode;
        //var modeDefault = vfsNodeFetchModeEnum.vNode_vData_iNode_Extra;
        vfsNodeFetchModeEnum fsNodeFetchMode = modeDefault;
        try { fsNodeFetchMode = modelInfo.Attributes.OfType<IKCMS_ModelCMS_fsNodeModeAttribute>().Select(a => a.vfsNodeFetchMode).DefaultIfEmpty(modeDefault).First(); }
        catch { }
        switch (fsNodeFetchMode)
        {
          case vfsNodeFetchModeEnum.vNode_vData_iNode_ExtraVariants:
            fsNode = managerVFS.EnsureNodeOrRegister<FS_Operations.FS_NodeInfoExt2>(fsNode) ?? fsNode;
            break;
          case vfsNodeFetchModeEnum.vNode_vData_iNode_Extra:
            fsNode = managerVFS.EnsureNodeOrRegister<FS_Operations.FS_NodeInfoExt>(fsNode) ?? fsNode;
            break;
          default:
            fsNode = managerVFS.EnsureNodeOrRegister<FS_Operations.FS_NodeInfo>(fsNode) ?? fsNode;
            break;
        }
        //
        if (modelInfo == null)
          return model;
        try
        {
          Type modelType = modelInfo.TypeModel;
          if (modelType != null && modelType.IsGenericTypeDefinition)
            modelType = modelType.MakeGenericType(resourceType);
          //
          //model = (IKCMS_ModelCMS_Interface)IKCMS_ManagerIoC.requestContainer.Resolve(modelInfo.TypeModel.MakeGenericType(resourceType), new TypedParameter(typeof(IKCMS_ModelCMS_ModelInfo_Interface), modelInfo));  // oppure con NamedParameter
          model = (IKCMS_ModelCMS_Interface)IKCMS_ManagerIoC.requestContainer.Resolve(modelType);
          model.Setup(fsNode, modelParent, modelInfo, args);
        }
        catch (Exception ex)
        {
          Elmah.ErrorSignal.FromCurrentContext().Raise(ex);
        }
        //
      }
      catch (Exception ex)
      {
        Elmah.ErrorSignal.FromCurrentContext().Raise(ex);
      }
      return model;
    }


    //
    // model builder per risorse senza fsNode (costruito dagli args)
    //
    public IKCMS_ModelCMS_Interface ModelBuildFromResource(IKCMS_ModelCMS_Interface modelParent, IKCMS_ModelCMS_ModelInfo_Interface modelInfo, Type resourceType, params object[] args)
    {
      IKCMS_ModelCMS_Interface model = null;
      try
      {
        modelInfo = modelInfo ?? FindBestModelMatch(resourceType);
        //model = (IKCMS_ModelCMS_Interface)IKCMS_ManagerIoC.requestContainer.Resolve(modelInfo.TypeModel.MakeGenericType(resourceType), new TypedParameter(typeof(IKCMS_ModelCMS_ModelInfo_Interface), modelInfo));  // oppure con NamedParameter
        model = (IKCMS_ModelCMS_Interface)IKCMS_ManagerIoC.requestContainer.Resolve(modelInfo.TypeModel.MakeGenericType(resourceType));
        model.Setup(null, modelParent, modelInfo, args);
      }
      catch (Exception ex)
      {
        Elmah.ErrorSignal.FromCurrentContext().Raise(ex);
      }
      return model;
    }


    //
    // model builder per risorse senza fsNode (costruito dagli args)
    //
    public IKCMS_ModelCMS_Interface ModelBuildFromType(IKCMS_ModelCMS_Interface modelParent, Type modelType, params object[] args)
    {
      IKCMS_ModelCMS_Interface model = null;
      try
      {
        //model = (IKCMS_ModelCMS_Interface)IKCMS_ManagerIoC.requestContainer.Resolve(modelInfo.TypeModel.MakeGenericType(resourceType), new TypedParameter(typeof(IKCMS_ModelCMS_ModelInfo_Interface), modelInfo));  // oppure con NamedParameter
        model = (IKCMS_ModelCMS_Interface)IKCMS_ManagerIoC.requestContainer.Resolve(modelType);
        model.Setup(null, modelParent, null, args);
      }
      catch (Exception ex)
      {
        Elmah.ErrorSignal.FromCurrentContext().Raise(ex);
      }
      return model;
    }


    //
    // costruzione di un megamodel da un tree di risorse
    //
    protected IKCMS_ModelCMS_Interface ModelBuildFromTreeWorker<T>(IKCMS_ModelCMS_Interface modelParent, FS_Operations.FS_TreeNode<T> nodesTree) where T : class, FS_Operations.FS_NodeInfo_Interface
    {
      try
      {
        if (modelParent == null)
        {
          modelParent = (nodesTree.Data != null) ? ModelBuild(null, nodesTree.Data, null) : new IKCMS_ModelCMS_Dumb();
        }
        //
        foreach (var nodeTree in nodesTree.Nodes.Where(n => n.Data != null && n.Data.vNode != null && n.Data.vNode.flag_folder))
        {
          var subModel = modelParent.Models.FirstOrDefault(m => m.vfsNode != null && m.sNode == nodeTree.Data.sNode);
          if (subModel != null)
            continue;
          subModel = ModelBuild(modelParent, nodeTree.Data, null);
          ModelBuildFromTreeWorker(subModel, nodeTree);
        }
      }
      catch (Exception ex)
      {
        Elmah.ErrorSignal.FromCurrentContext().Raise(ex);
      }
      return modelParent;
    }


    public IKCMS_ModelCMS_Interface ModelBuildFromFullTree(IKCMS_ModelCMS_Interface modelParent, int? sNode, int? rNode, int? maxRecursionLevel, bool? fetchRelations)
    {
      bool savedStatus = IKCMS_ModelCMS_Provider.Provider.managerVFS.Enabled;
      try
      {
        //
        var sNodesLst = Enumerable.Repeat(sNode, 1).Where(n => n != null).Select(n => n.Value).ToList();
        var rNodesLst = Enumerable.Repeat(rNode, 1).Where(n => n != null).Select(n => n.Value).ToList();
        //
        var fsPathsAll = fsOp.PathsFromNodesExt(sNodesLst, rNodesLst, false, true, false);
        if (!fsPathsAll.Any() && Utility.TryParse<bool>(IKGD_Config.AppSettings["IKGD_Path_forceNoOpt"], false) == false)
          fsPathsAll = fsOp.PathsFromNodesExt(sNodesLst, rNodesLst, false, true, true, false);  //force NoOpt
        if (pathFiltersPre != null && pathFiltersPre.Any() && fsPathsAll.Any())
        {
          fsPathsAll = fsPathsAll.FilterCustom(pathFiltersPre).ToList();
        }
        fsPathsAll = fsPathsAll.FilterPathsByLanguage().ToList();  // solo i path che non hanno languages mescolati e sono compatibili con la linua corrente (usando rNode non e' previsto un cambio lingua)
        fsPathsAll.RemoveAll(p => !p.Fragments.Any(f => f.sNode == sNode || sNode == null));
        IKGD_Path fsPath = fsPathsAll.FilterFallback(pathFilters).OrderByACL().FirstOrDefault();
        //
        if (fsPath != null && !fsPath.IsLanguageAccessible())
          IKGD_Language_Provider.Provider.LanguageContext = fsPath.FirstLanguageNN;
        //
        // processing del nodo
        //
        if (fsPath == null)
        {
          return null;
        }
        //
        // fetch ricorsivo del subtree, con pulizia preliminare per evitare eventuali eredita' di altri modelbuilders
        IKCMS_ModelCMS_Provider.Provider.managerVFS.Clear();
        IKCMS_ModelCMS_Provider.Provider.managerVFS.EnsureTrees<FS_Operations.FS_NodeInfoExt>(maxRecursionLevel, fetchRelations, fsPath.rNode);
        var treeNodeRoot = IKCMS_ModelCMS_Provider.Provider.managerVFS.NodesTree.FirstOrDefault(n => n.Data.sNode == fsPath.sNode);
        //
        // usiamo solo i nodi appena caricati senza fare ulteriori accessi al DB durante la costruzione ricorsiva
        IKCMS_ModelCMS_Provider.Provider.managerVFS.Enabled = false;
        IKCMS_ModelCMS_Interface model = this.ModelBuildFromTreeWorker(modelParent, treeNodeRoot);
        return model;
        //
      }
      catch (Exception ex)
      {
        Elmah.ErrorSignal.FromCurrentContext().Raise(ex);
      }
      finally
      {
        IKCMS_ModelCMS_Provider.Provider.managerVFS.Enabled = savedStatus;
      }
      return modelParent;
    }


    //
    // model builder da HttpContext
    //
    public IKCMS_ModelCMS_Interface ModelBuildFromContext() { return ModelBuildFromContext(true); }
    public IKCMS_ModelCMS_Interface ModelBuildFromContext(bool returnDumbOnNull)
    {
      var request = System.Web.HttpContext.Current.Request;
      IKCMS_ModelCMS_Interface model = ModelBuildFromUrl(request.Url.PathAndQuery, request.RawUrl, request.Url.AbsolutePath, request.Path);
      if (model == null && returnDumbOnNull)
        model = new IKCMS_ModelCMS_Dumb();
      return model;
    }


    //
    // model builder da url (da usare in action specifiche dei controller con supporto parziale per il CMS)
    //
    public IKCMS_ModelCMS_Interface ModelBuildFromUrl(params string[] pathAndQueryStrings)
    {
      IKCMS_ModelCMS_Interface model = null;
      try
      {
        IKCMS_ExecutionProfiler.AddMessage("ModelBuildFromUrl: START pathAndQueryStrings={0}".FormatString(Utility.Implode(pathAndQueryStrings, ",")));
        var allowed_manager_types = IKCMS_RegisteredTypes.Types_IKCMS_PageBase_Interface.Select(t => t.Name).ToList();
        //TODO: aggiornare il fetch del tree e utilizzarne uno piu' adatto ad uno scan globale (eg. senza filtri attivi per acl, menu', ecc.)
        FS_Operations.FS_TreeNode<TreeNodeInfoVFS> treeRoot = IKCMS_TreeStructureVFS.TreeDataBuildCached(IKGD_ConfigVFS.Config.RootsCMS_sNodes, null);
        foreach (string url in pathAndQueryStrings.Distinct())
        {
          var matches = treeRoot.RecurseOnTree.Where(n => n.Data != null).Where(n => string.Equals(n.Data.Url, url, StringComparison.OrdinalIgnoreCase) && allowed_manager_types.Contains(n.Data.ManagerType));
          foreach (var match in matches)
          {
            model = ModelBuildGeneric(match.Data.sNode);
            if (model != null)
              return model;
          }
          try
          {
            int? sNodeStatic = ScanStaticPagesForUrlExt(url);
            if (sNodeStatic != null)
              model = ModelBuildGeneric(sNodeStatic.Value);
            if (model != null)
              return model;
          }
          catch { }
          try
          {
            var matchesSeo = IKGD_SEO_Manager.MapUnknownUrlToElements(url, false);
            if (matchesSeo.Any(n => n.Target_sNode != null))
            {
              var paths = fsOp.PathsFromNodesExt(matchesSeo.Where(n => n.Target_sNode != null).Select(n => n.Target_sNode.Value), null, true, true, false).FilterCustom(IKGD_Path_Helper.FilterByRootVFS, IKGD_Path_Helper.FilterByActive, IKGD_Path_Helper.FilterByLanguage).ToList();
              if (paths.Any())
              {
                model = ModelBuildGeneric(paths.FirstOrDefault().sNode);
              }
            }
            //var matchSeo = IKGD_SEO_Manager.MapUnknownUrlToElement(url, false);
            //if (matchSeo != null)
            //  model = ModelBuildGeneric(matchSeo.Target_sNode.Value);
            if (model != null)
              return model;
          }
          catch { }
        }
      }
      //catch (Exception ex)
      //{
      //  Elmah.ErrorSignal.FromCurrentContext().Raise(ex);
      //}
      catch { }
      finally
      {
        IKCMS_ExecutionProfiler.AddMessage("ModelBuildFromUrl: END");
      }
      return model;
    }


    //
    // scan delle pagine statiche non incluse nel menu' di navigazione
    // usa una cache separata per version
    //
    public int? ScanStaticPagesForUrlExt(string url)
    {
      try
      {
        string cacheKey = "ScanStaticPagesForUrl_" + FS_OperationsHelpers.ContextHashGeneral(false, false, false, false);
        var data = FS_OperationsHelpers.CachedEntityWrapper(cacheKey, () =>
        {
          var staticPages = fsOp.Get_NodesInfoFilteredExt1(vn => vn.flag_folder == true, vd => vd.manager_type == typeof(IKCMS_ResourceType_PageStatic).Name, FS_Operations.FilterVFS.Disabled).ToList();
          var paths = fsOp.PathsFromNodesExt(staticPages.Select(n => n.sNode), null, true, true, false).FilterCustom(IKGD_Path_Helper.FilterByRootVFS, IKGD_Path_Helper.FilterByActive).ToList();
          //
          var nodes_data = staticPages.Select(n => new { node = n, data = (IKCMS_RegisteredTypes.Deserialize_IKCMS_ResourceVFS(n) as IKCMS_ResourceType_PageStatic).ResourceSettings }).Join(paths, n => n.node.sNode, p => p.sNode, (n, p) => new { sNode = n.node.sNode, path = p, regEx = n.data.UrlMatchRegEx, url = Utility.ResolveUrl(n.data.UrlExternal) }).ToList();
          var lookupUrl = nodes_data.Where(r => r.url.IsNotEmpty()).ToLookup(r => r.url, r => r.path, StringComparer.OrdinalIgnoreCase);
          var lookupRegEx = nodes_data.Where(r => r.regEx.IsNotEmpty()).ToLookup(r => new Regex(r.regEx, RegexOptions.IgnoreCase | RegexOptions.Singleline), r => r.path);
          return new { lookupUrl, lookupRegEx };
        }
        , null, Utility.TryParse<int>(IKGD_Config.AppSettings["CachingIKCMS_Models"], 3600), null, FS_OperationsHelpers.Const_CacheDependencyIKGD_vNode_vData);
        //
        var pathsFound = data.lookupUrl[url];
        if (pathsFound != null && pathsFound.Any())
        {
          pathsFound = pathsFound.FilterCustom(IKGD_Path_Helper.FilterByAreas, IKGD_Path_Helper.FilterByLanguage).FilterFallback(IKGD_Path_Helper.FilterByRootCMS);
        }
        if (pathsFound == null || !pathsFound.Any() && url.IndexOf('?') >= 0)
        {
          string url_noqs = url.Split(new char[] { '?' }, 2).FirstOrDefault();
          pathsFound = data.lookupUrl[url_noqs];
          if (pathsFound != null && pathsFound.Any())
          {
            pathsFound = pathsFound.FilterCustom(IKGD_Path_Helper.FilterByAreas, IKGD_Path_Helper.FilterByLanguage).FilterFallback(IKGD_Path_Helper.FilterByRootCMS);
          }
        }
        if (pathsFound == null || !pathsFound.Any())
        {
          pathsFound = data.lookupRegEx.FirstOrDefault(r => r.Key.IsMatch(url));
          if (pathsFound != null && pathsFound.Any())
          {
            pathsFound = pathsFound.FilterCustom(IKGD_Path_Helper.FilterByAreas, IKGD_Path_Helper.FilterByLanguage).FilterFallback(IKGD_Path_Helper.FilterByRootCMS);
          }
        }
        if (pathsFound != null && pathsFound.Any())
        {
          var pathsFiltered = pathsFound.ToList();
          pathsFiltered.Sort();
          return pathsFiltered.FirstOrDefault().sNode;
        }
      }
      //catch (Exception ex)
      //{
      //  Elmah.ErrorSignal.FromCurrentContext().Raise(ex);
      //}
      catch { }
      return null;
    }

    /*
    public int? ScanStaticPagesForUrl(string url)
    {
      try
      {
        string cacheKey = "ScanStaticPagesForUrl_" + FS_OperationsHelpers.ContextHashGeneral(false, false, false, false);
        var data = FS_OperationsHelpers.CachedEntityWrapper(cacheKey, () =>
        {
          var staticPages = fsOp.Get_NodesInfoFilteredExt1(vn => vn.flag_folder == true, vd => vd.manager_type == typeof(IKCMS_ResourceType_PageStatic).Name, true, false, false, false).ToList();
          var paths = fsOp.PathsFromNodesExt(staticPages.Select(n => n.sNode), null, true, true, false).FilterCustom(IKGD_Path_Helper.FilterByRootVFS, IKGD_Path_Helper.FilterByActive).ToList();
          //mapping staticUrl->paths
          return
            (from node in staticPages.Select(n => new { node = n, url = Utility.ResolveUrl((IKCMS_RegisteredTypes.Deserialize_IKCMS_ResourceVFS(n) as IKCMS_ResourceType_PageStatic).ResourceSettings.UrlExternal) })
             join path in paths on node.node.sNode equals path.sNode
             select new { node, path }).ToLookup(r => r.node.url, r => r.path, StringComparer.OrdinalIgnoreCase);
        }
        , null, Utility.TryParse<int>(IKGD_Config.AppSettings["CachingIKCMS_Models"], 3600), null, FS_OperationsHelpers.Const_CacheDependencyIKGD_vNode_vData);
        //
        var pathsFound = data[url].FilterCustom(IKGD_Path_Helper.FilterByAreas, IKGD_Path_Helper.FilterByLanguage).FilterFallback(IKGD_Path_Helper.FilterByRootCMS).ToList();
        if (pathsFound.Any())
        {
          pathsFound.Sort();
          return pathsFound.FirstOrDefault().sNode;
        }
        //
      }
      //catch (Exception ex)
      //{
      //  Elmah.ErrorSignal.FromCurrentContext().Raise(ex);
      //}
      catch { }
      return null;
    }
    */

    //
    // classe per lo storage di informazioni ausiliarie sui modelli
    //
    public class ModelInfo : IKCMS_ModelCMS_ModelInfo_Interface
    {
      public Type TypeModel { get; protected set; }
      public List<Type> ResourceTypes { get; protected set; }
      public ILookup<Type, string> ResourceTypesCategory { get; protected set; }
      public List<IKCMS_ModelCMS_BaseAttribute_Interface> Attributes { get; protected set; }
      public double Priority { get; protected set; }


      public ModelInfo(Type typeModel)
      {
        TypeModel = typeModel;
        Attributes = Utility.GetAttributesFromType<IKCMS_ModelCMS_BaseAttribute_Interface>(TypeModel).ToList();
        Priority = (Attributes.OfType<IKCMS_ModelCMS_PriorityAttribute>().FirstOrDefault() ?? new IKCMS_ModelCMS_PriorityAttribute(0.0)).Priority;
        //
        //TODO: vedere se mappare i types di ResourceTypesCategory anche in ResourceTypes (magari anche all'inizio della lista)
        ResourceTypesCategory = Attributes.OfType<IKCMS_ModelCMS_ResourceTypeCategoryAttribute>().ToLookup(a => a.ResourceType, a => a.Category);
        ResourceTypes = Attributes.OfType<IKCMS_ModelCMS_ResourceTypesAttribute>().SelectMany(a => a.Types.SelectMany(t => IKCMS_ModelCMS_Provider.Provider.GetFullTypeInheritance(t))).Distinct().ToList();
        //
        // setup dell'attributo per il fetch mode del nodo in modo che sia consistente con le interfaces nel caso non sia stato specificato esplicitamente
        if (ResourceTypes.Contains(typeof(IKCMS_HasRelations_Interface)) || ResourceTypes.Contains(typeof(IKCMS_HasProperties_Interface)))
          if (!Attributes.OfType<IKCMS_ModelCMS_fsNodeModeAttribute>().Any())
            Attributes.Add(new IKCMS_ModelCMS_fsNodeModeAttribute(vfsNodeFetchModeEnum.vNode_vData_iNode_Extra));
        // setup dell'attributo per la ricorsione del fetch sui widget in modo che sia consistente con le interfaces nel caso non sia stato specificato esplicitamente
        if (ResourceTypes.Contains(typeof(IKCMS_Page_Interface)))
          if (!Attributes.OfType<IKCMS_ModelCMS_RecursionModeAttribute>().Any())
            Attributes.Add(new IKCMS_ModelCMS_RecursionModeAttribute(ModelRecursionModeEnum.RecursionOnResources));
      }

    }  //class ModelInfo


    public void RemoveModelsFromCache(params IKCMS_ModelCMS_Interface[] models)
    {
      try { RemoveModelsFromCache(models.Select(m => m.CacheKey).ToArray()); }
      catch { }
    }
    public void RemoveModelsFromCache(params string[] cacheKeys)
    {
      try
      {
        foreach (string cacheKey in cacheKeys.Where(s => s.IsNotNullOrWhiteSpace()).Distinct())
        {
          try { HttpRuntime.Cache.Remove(cacheKey); }
          catch { }
        }
      }
      catch { }
    }


  }  //class IKCMS_ModelCMS_ProviderBase



  public class IKCMS_ModelCMS_ProviderCMS : IKCMS_ModelCMS_ProviderBase
  {
    public IKCMS_ModelCMS_ProviderCMS() : base() { }
  }




  //
  // customizzazione del language provider per implementare il supporto IoC/fsOp che manca nell'implementazione di base
  //
  [IKCMS_ModelCMS_BootStrapperOrder(-1000000)]
  public class IKGD_Language_Provider_CMS : IKGD_Language_Provider_Base, IBootStrapperTask
  {
    public FS_Operations fsOpRO { get { return IKCMS_ManagerIoC.requestContainer.ResolveNamed<FS_Operations>("readonly"); } }
    public FS_Operations fsOp { get { return IKCMS_ManagerIoC.requestContainer.Resolve<FS_Operations>(); } }


    void IBootStrapperTask.Execute()
    {
      // non posso usare il this potrebbe trattarsi di un oggetto farlocco creato come Proxy
      // per evitare di attivare provider meno evoluti
      if (IKGD_Language_Provider.Provider == null || !(IKGD_Language_Provider.Provider is IKGD_Language_Provider_CMS))
        IKGD_Language_Provider.Provider = new IKGD_Language_Provider_CMS();
    }


    //
    // attenzione gestisce il language solo per le fsOp veicolate del manager IoC
    // per settare la lingua in un fsOp generico utilizzare fsOp.Language che esegue gia' l'override della lingua
    // di default settata per l'environment (session/context)
    //
    public override string LanguageVFS
    {
      get
      {
        lock (_lock)
        {
          return fsOp.Language;
        }
      }
      set
      {
        lock (_lock)
        {
          if (ValidateLanguage(value))
          {
            fsOp.Language = value;
            fsOpRO.Language = value;
          }
        }
      }
    }


  }


}
