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
  using Ikon.IKGD.Library.Resources;
  using Ikon.IKCMS.Library.Resources;


  //
  // models relativi alla gestione del catalogo semplice con pagine CMS
  //

  [IKCMS_ModelCMS_ResourceTypes(typeof(Ikon.IKCMS.Library.Resources.IKCMS_ResourceType_PageCatalogCMS))]  // eg. for client model customization
  [IKCMS_ModelCMS_ResourceTypes(typeof(IKCMS_Page_Interface))]
  [IKCMS_ModelCMS_BootStrapperOpenGenerics(typeof(IKCMS_HasGenericBrick_Interface))]
  [IKCMS_ModelCMS_RecursionMode(ModelRecursionModeEnum.RecursionOnFolders)]
  [IKCMS_ModelCMS_fsNodeMode(vfsNodeFetchModeEnum.vNode_vData_iNode_Extra)]
  [IKCMS_ModelCMS_fsNodeModeRecurse(vfsNodeFetchModeEnum.vNode_vData_iNode)]
  [IKCMS_ModelCMS_Priority(-1999900)]
  public class IKCMS_ModelCMS_PageCatalog<T> : IKCMS_ModelCMS_PageCMS<T>, IKCMS_ModelCMS_Page_Interface
    where T : class, IKCMS_HasGenericBrick_Interface  //ricordarsi di utilizzare anche l'attribute IKCMS_ModelCMS_BootStrapperOpenGenericsAttribute
  {
    // filtro per i widget degli items del catalogo
    public override Expression<Func<IKGD_VDATA, bool>> RecursionVFSvDataFilterSubFolders { get { return vd => (vd.manager_type == typeof(Ikon.IKCMS.Library.Resources.IKCMS_ResourceType_ImageCMS).Name && vd.category == "image_catalog_thumb"); } }


    //
    // override del setup degli items per la ricorsione nei cataloghi
    // si potrebbe anche tenere la funzione originale, tanto la ricorsione viene comunque interrotta al punto corretto
    // pero' bisognerebbe fare in modo che che SetupFinalize non perda tempo con path e normalizzazioni inutili per le subpages
    // in tal modo si potrebbe anche fare a meno di definire la classe IKCMS_ModelCMS_PageCatalogItem<T>
    //
    protected override void SetupRecursive(IEnumerable<int> sNodesBlackList, params object[] args)
    {
      IKCMS_ModelCMS_ModelInfo_Interface modelInfo = null;
      var blackList = new List<int>();
      string lang = this.LanguageNN;
      var areas = fsOp.CurrentAreasExtended.AreasAllowed;
      var nodesSet = managerVFS[vfsNode.vNode.snode].NodesOrdered.Where(n => n.Data.vData.manager_type == this.ManagerType);
      var nodesFiltered = nodesSet.Where(n => n.Data != null && n.Data.IsNotExpired).Where(n => n.Data.LanguageCheck(this.LanguageNN)).Where(n => n.Data.vData.area.IsNullOrEmpty() || areas.Contains(n.Data.vData.area));
      // se una risorsa e' definita senza lingua e con lingua la facciamo comparire una volta sola
      nodesFiltered = nodesFiltered.OrderByDescending(n => n.Data.Language).Distinct((n1, n2) => n1.Data.rNode == n2.Data.rNode);
      foreach (var node in nodesFiltered.OrderBy(n => n.Data.Position).ThenBy(n => n.Data.vNode.name).ThenBy(n => n.Data.sNode))
      {
        blackList.Add(node.Data.vNode.snode);
        modelInfo = modelInfo ?? IKCMS_ModelCMS_Provider.Provider.ModelInfos.FirstOrDefault(m => m.TypeModel == typeof(IKCMS_ModelCMS_PageCatalogItem<>));
        IKCMS_ModelCMS_Provider.Provider.ModelBuild(this, node.Data, modelInfo);
      }
      base.SetupRecursive(blackList, args);
    }


    //
    // continuazione del setup del model dopo le features di base processate in IKCMS_ModelCMS
    //
    protected override void SetupFinalize(params object[] args)
    {
      base.SetupFinalize(args);
      //
      //if (ModelParent == null && TemplateInfo != null)
      if (TemplateInfo != null)
      {
        if (Models.OfType<IKCMS_ModelCMS_PageCatalogItem<T>>().Any())
        {
          TemplateViewPath = TemplateInfo.ViewPaths["index"] ?? TemplateInfo.ViewPath;
        }
        else
        {
          TemplateViewPath = TemplateInfo.ViewPaths["detail"] ?? TemplateInfo.ViewPath;
        }
      }
    }

  }


  [IKCMS_ModelCMS_ResourceTypes(typeof(IKCMS_Page_Interface))]
  [IKCMS_ModelCMS_BootStrapperOpenGenerics(typeof(IKCMS_HasGenericBrick_Interface))]
  [IKCMS_ModelCMS_RecursionMode(ModelRecursionModeEnum.RecursionOnResources)]
  [IKCMS_ModelCMS_fsNodeMode(vfsNodeFetchModeEnum.vNode_vData_iNode)]
  [IKCMS_ModelCMS_fsNodeModeRecurse(vfsNodeFetchModeEnum.vNode_vData_iNode)]
  [IKCMS_ModelCMS_Priority(-3999900)]
  public class IKCMS_ModelCMS_PageCatalogItem<T> : IKCMS_ModelCMS<T>, IKCMS_ModelCMS_Folder_Interface, IKCMS_ModelCMS_VFS_LanguageKVT_Interface
    where T : class, IKCMS_HasGenericBrick_Interface  //ricordarsi di utilizzare anche l'attribute IKCMS_ModelCMS_BootStrapperOpenGenericsAttribute
  {
    //
    public KeyValueObjectTree VFS_ResourceKVT { get { return ResourceSettingsKVT_Wrapper; } }
    public KeyValueObjectTree VFS_ResourceLanguageKVT(params string[] keys) { return ResourceSettingsKVT_Wrapper.KeyFilterTry(Language ?? IKGD_Language_Provider.Provider.Language, keys); }
    public KeyValueObjectTree VFS_ResourceNoLanguageKVT(params string[] keys) { return ResourceSettingsKVT_Wrapper.KeyFilterTry(null, keys); }
    public virtual IEnumerable<KeyValueObjectTree> VFS_ResourceLanguageKVTs(params string[] keys) { return ResourceSettingsKVT_Wrapper.KeyFilterTryMulti(Language ?? IKGD_Language_Provider.Provider.Language, keys); }
    public virtual IEnumerable<KeyValueObjectTree> VFS_ResourceNoLanguageKVTs(params string[] keys) { return ResourceSettingsKVT_Wrapper.KeyFilterTryMulti(null, keys); }
    public virtual List<string> VFS_ResourceLanguageKVTss(params string[] keys) { return ResourceSettingsKVT_Wrapper.KeyFilterTryMulti(Language ?? IKGD_Language_Provider.Provider.Language, keys).Select(r => r.ValueString).Where(s => s != null).ToList(); }
    public virtual List<string> VFS_ResourceNoLanguageKVTss(params string[] keys) { return ResourceSettingsKVT_Wrapper.KeyFilterTryMulti(null, keys).Select(r => r.ValueString).Where(s => s != null).ToList(); }
    //


    //
    // continuazione del setup del model dopo le features di base processate in IKCMS_ModelCMS
    //
    protected override void SetupFinalize(params object[] args)
    {
      base.SetupFinalize(args);
    }

  }




  //
  // models relativi alla gestione del catalogo extended con attributes
  //

  [IKCMS_ModelCMS_ResourceTypes(typeof(Ikon.IKCMS.Library.Resources.IKCMS_ResourceType_PageCatalogCategoryCMS))]  // eg. for client model customization
  [IKCMS_ModelCMS_BootStrapperOpenGenerics(typeof(IKCMS_HasGenericBrick_Interface))]
  [IKCMS_ModelCMS_RecursionMode(ModelRecursionModeEnum.RecursionOnResources)]
  [IKCMS_ModelCMS_fsNodeMode(vfsNodeFetchModeEnum.vNode_vData_iNode_Extra)]
  [IKCMS_ModelCMS_fsNodeModeRecurse(vfsNodeFetchModeEnum.vNode_vData_iNode)]
  [IKCMS_ModelCMS_Priority(-1999800)]
  public class IKCMS_ModelCMS_PageCatalogExtendedCMS_Category<T> : IKCMS_ModelCMS_PageCMS<T>, IKCMS_ModelCMS_Page_Interface
    where T : class, IKCMS_HasGenericBrick_Interface  //ricordarsi di utilizzare anche l'attribute IKCMS_ModelCMS_BootStrapperOpenGenericsAttribute
  {
    // filtro per i widget degli items del catalogo
    public override Expression<Func<IKGD_VDATA, bool>> RecursionVFSvDataFilterSubFolders { get { return vd => (vd.manager_type == typeof(Ikon.IKCMS.Library.Resources.IKCMS_ResourceType_ImageCMS).Name && vd.category == "image_catalog_thumb"); } }


    //
    // override del setup degli items per la ricorsione nei cataloghi
    // si potrebbe anche tenere la funzione originale, tanto la ricorsione viene comunque interrotta al punto corretto
    // pero' bisognerebbe fare in modo che che SetupFinalize non perda tempo con path e normalizzazioni inutili per le subpages
    // in tal modo si potrebbe anche fare a meno di definire la classe IKCMS_ModelCMS_PageCatalogItem<T>
    //
    protected override void SetupRecursive(IEnumerable<int> sNodesBlackList, params object[] args)
    {
      IKCMS_ModelCMS_ModelInfo_Interface modelInfo = null;
      var blackList = new List<int>();
      string lang = this.LanguageNN;
      var areas = fsOp.CurrentAreasExtended.AreasAllowed;
      var nodesSet = managerVFS[vfsNode.vNode.snode].NodesOrdered.Where(n => n.Data.vData.manager_type == this.ManagerType);
      var nodesFiltered = nodesSet.Where(n => n.Data != null && n.Data.IsNotExpired).Where(n => n.Data.LanguageCheck(this.LanguageNN)).Where(n => n.Data.vData.area.IsNullOrEmpty() || areas.Contains(n.Data.vData.area));
      // se una risorsa e' definita senza lingua e con lingua la facciamo comparire una volta sola
      nodesFiltered = nodesFiltered.OrderByDescending(n => n.Data.Language).Distinct((n1, n2) => n1.Data.rNode == n2.Data.rNode);
      foreach (var node in nodesFiltered.OrderBy(n => n.Data.Position).ThenBy(n => n.Data.vNode.name).ThenBy(n => n.Data.sNode))
      {
        blackList.Add(node.Data.vNode.snode);
        modelInfo = modelInfo ?? IKCMS_ModelCMS_Provider.Provider.ModelInfos.FirstOrDefault(m => m.TypeModel == typeof(IKCMS_ModelCMS_PageCatalogItem<>));
        IKCMS_ModelCMS_Provider.Provider.ModelBuild(this, node.Data, modelInfo);
      }
      base.SetupRecursive(blackList, args);
    }


    //
    // continuazione del setup del model dopo le features di base processate in IKCMS_ModelCMS
    //
    protected override void SetupFinalize(params object[] args)
    {
      base.SetupFinalize(args);
      // Paths, vari, canonical page, breadcrumbs, headers, titles, robots, ...
      // render templates...
    }

  }



  [IKCMS_ModelCMS_ResourceTypes(typeof(Ikon.IKCMS.Library.Resources.IKCMS_ResourceType_PageCatalogExtendedCMS))]  // eg. for client model customization
  [IKCMS_ModelCMS_BootStrapperOpenGenerics(typeof(IKCMS_HasGenericBrick_Interface))]
  [IKCMS_ModelCMS_RecursionMode(ModelRecursionModeEnum.RecursionOnResources)]
  [IKCMS_ModelCMS_fsNodeMode(vfsNodeFetchModeEnum.vNode_vData_iNode_Extra)]
  [IKCMS_ModelCMS_fsNodeModeRecurse(vfsNodeFetchModeEnum.vNode_vData_iNode)]
  [IKCMS_ModelCMS_Priority(-1999700)]
  public class IKCMS_ModelCMS_PageCatalogExtendedCMS_Detail<T> : IKCMS_ModelCMS_PageCMS<T>, IKCMS_ModelCMS_Template_Interface
    where T : class, IKCMS_HasGenericBrick_Interface  //ricordarsi di utilizzare anche l'attribute IKCMS_ModelCMS_BootStrapperOpenGenericsAttribute
  {

    //
    // continuazione del setup del model dopo le features di base processate in IKCMS_ModelCMS
    //
    protected override void SetupFinalize(params object[] args)
    {
      base.SetupFinalize(args);
      // Paths, vari, canonical page, breadcrumbs, headers, titles, robots, ...
      // render templates...
    }


  }


}
