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




  [IKCMS_ModelCMS_ResourceTypes(typeof(IKCMS_ResourceType_BrickCMS))]
  [IKCMS_ModelCMS_fsNodeMode(vfsNodeFetchModeEnum.vNode_vData_iNode_Extra)]
  [IKCMS_ModelCMS_Priority(-1999999)]
  public class IKCMS_ModelCMS_BrickCMS : IKCMS_ModelCMS_GenericBrickBase<IKCMS_ResourceType_BrickCMS>
  {
  }


  [IKCMS_ModelCMS_ResourceTypes(typeof(IKCMS_ResourceType_ParagraphKVT))]
  [IKCMS_ModelCMS_Priority(-1999999)]
  public class IKCMS_ModelCMS_ParagraphKVT : IKCMS_ModelCMS_GenericBrickBase<IKCMS_ResourceType_ParagraphKVT>
  {
  }



  [IKCMS_ModelCMS_ResourceTypes(typeof(IKCMS_ResourceType_LinkKVT))]
  [IKCMS_ModelCMS_Priority(-1999999)]
  public class IKCMS_ModelCMS_LinkKVT : IKCMS_ModelCMS_GenericBrickBase<IKCMS_ResourceType_LinkKVT>, IKCMS_ModelCMS_GenericBrickSlotTeaserOrWidgetInterface
  {
  }



  [IKCMS_ModelCMS_ResourceTypes(typeof(IKCMS_ResourceType_ContactKVT))]
  [IKCMS_ModelCMS_Priority(-1999999)]
  public class IKCMS_ModelCMS_ContactKVT : IKCMS_ModelCMS_GenericBrickBase<IKCMS_ResourceType_ContactKVT>, IKCMS_ModelCMS_GenericBrickSlotTeaserOrWidgetInterface
  {
  }





  //
  // model per la gestione dei bricks di base
  //
  [IKCMS_ModelCMS_ResourceTypes(typeof(IKCMS_BrickBase_Interface))]
  [IKCMS_ModelCMS_RecursionMode(ModelRecursionModeEnum.RecursionNone)]
  [IKCMS_ModelCMS_fsNodeMode(vfsNodeFetchModeEnum.vNode_vData_iNode_Extra)]
  [IKCMS_ModelCMS_Priority(-2999000)]
  public class IKCMS_ModelCMS_GenericBrick<T> : IKCMS_ModelCMS_GenericBrickBase<T>, IKCMS_ModelCMS_GenericBrickInterface
    where T : class, IKCMS_HasGenericBrick_Interface
  {
  }


  [IKCMS_ModelCMS_ResourceTypes(typeof(IKCMS_BrickCollector_Interface))]
  [IKCMS_ModelCMS_RecursionMode(ModelRecursionModeEnum.RecursionNone)]
  [IKCMS_ModelCMS_fsNodeMode(vfsNodeFetchModeEnum.vNode_vData_iNode_Extra)]
  [IKCMS_ModelCMS_Priority(-2999000)]
  public class IKCMS_ModelCMS_GenericBrickCollector<T> : IKCMS_ModelCMS_GenericBrickCollectorBase<T>, IKCMS_ModelCMS_GenericBrickCollectorInterface
    where T : class, IKCMS_HasGenericBrick_Interface
  {
  }


  [IKCMS_ModelCMS_ResourceTypes(typeof(IKCMS_BrickWidget_Interface))]
  [IKCMS_ModelCMS_RecursionMode(ModelRecursionModeEnum.RecursionNone)]
  [IKCMS_ModelCMS_fsNodeMode(vfsNodeFetchModeEnum.vNode_vData_iNode_Extra)]
  [IKCMS_ModelCMS_Priority(-2999000)]
  public class IKCMS_ModelCMS_GenericBrickWidget<T> : IKCMS_ModelCMS_GenericBrickWidgetBase<T>, IKCMS_ModelCMS_GenericBrickWidgetInterface, IKCMS_ModelCMS_GenericBrickSlotTeaserOrWidgetInterface
    where T : class, IKCMS_HasGenericBrick_Interface
  {
  }


  [IKCMS_ModelCMS_ResourceTypes(typeof(IKCMS_ResourceType_ImageCMS))]
  [IKCMS_ModelCMS_RecursionMode(ModelRecursionModeEnum.RecursionNone)]
  [IKCMS_ModelCMS_fsNodeMode(vfsNodeFetchModeEnum.vNode_vData_iNode_Extra)]  //[IKCMS_ModelCMS_fsNodeMode(vfsNodeFetchModeEnum.vNode_vData_iNode)]
  [IKCMS_ModelCMS_Priority(-2899999)]
  //public class IKCMS_ModelCMS_WidgetCMS_ImageCMS : IKCMS_ModelCMS_WidgetCMS_LanguageKVT<IKCMS_ResourceType_ImageCMS>
  public class IKCMS_ModelCMS_WidgetCMS_ImageCMS : IKCMS_ModelCMS_GenericBrickBase<IKCMS_ResourceType_ImageCMS>, IKCMS_ModelCMS_GenericBrickInterface
  {
  }



  // TODO: componente ancora da convertire in brick
  [IKCMS_ModelCMS_ResourceTypes(typeof(IKCMS_ResourceType_Flash))]
  [IKCMS_ModelCMS_RecursionMode(ModelRecursionModeEnum.RecursionNone)]
  [IKCMS_ModelCMS_fsNodeMode(vfsNodeFetchModeEnum.vNode_vData_iNode_Extra)]  //[IKCMS_ModelCMS_fsNodeMode(vfsNodeFetchModeEnum.vNode_vData_iNode)]
  [IKCMS_ModelCMS_Priority(-2899998)]
  //public class IKCMS_ModelCMS_WidgetCMS_Flash : IKCMS_ModelCMS_GenericBrickBase<IKCMS_ResourceType_Flash>, IKCMS_ModelCMS_GenericBrickInterface
  public class IKCMS_ModelCMS_WidgetCMS_Flash : IKCMS_ModelCMS_WidgetCMS<IKCMS_ResourceType_Flash>
  {
  }



  //
  // Bricks model relativi a elementi tipo collection
  //

  public interface IKCMS_ModelCMS_TeaserCollection_Interface : IKCMS_ModelCMS_GenericBrickInterface, IKCMS_ModelCMS_GenericBrickSlotTeaserOrWidgetInterface
  {
  }


  public interface IKCMS_ModelCMS_DocumentCollection_Interface : IKCMS_ModelCMS_GenericBrickInterface, IKCMS_ModelCMS_GenericBrickSlotTeaserOrWidgetInterface
  {
    FS_Operations.FS_TreeNode<TreeNodeInfoVFS> TreeDocuments { get; }
  }



  //
  // model per la gestione di un teaser collection
  //
  [IKCMS_ModelCMS_ResourceTypes(typeof(IKCMS_ResourceType_TeaserCollection))]
  [IKCMS_ModelCMS_RecursionMode(ModelRecursionModeEnum.RecursionNone)]
  [IKCMS_ModelCMS_fsNodeMode(vfsNodeFetchModeEnum.vNode_vData_iNode_Extra)]
  [IKCMS_ModelCMS_Priority(-2499999)]
  public class IKCMS_ModelCMS_TeaserCollection<T> : IKCMS_ModelCMS_GenericBrickCollectorBase<T>, IKCMS_ModelCMS_TeaserCollection_Interface
    where T : class, IKCMS_HasGenericBrick_Interface
  {
  }


  //
  // model per la gestione di una documents collection
  //
  [IKCMS_ModelCMS_ResourceTypes(typeof(IKCMS_ResourceType_DocumentCollection))]
  [IKCMS_ModelCMS_RecursionMode(ModelRecursionModeEnum.RecursionNone)]
  [IKCMS_ModelCMS_fsNodeMode(vfsNodeFetchModeEnum.vNode_vData_iNode_Extra)]
  [IKCMS_ModelCMS_Priority(-2499999)]
  public class IKCMS_ModelCMS_DocumentCollection<T> : IKCMS_ModelCMS_GenericBrickBase<T>, IKCMS_ModelCMS_DocumentCollection_Interface
    where T : class, IKCMS_HasGenericBrick_Interface
  {
    public FS_Operations.FS_TreeNode<TreeNodeInfoVFS> TreeDocuments { get; protected set; }


    //
    // continuazione del setup del model dopo le features di base processate in IKCMS_ModelCMS
    //
    protected override void SetupFinalize(params object[] args)
    {
      base.SetupFinalize(args);
      //
      try
      {
        bool mixFilesAndFolders = true;
        bool hideRoots = Utility.TryParse<bool>(VFS_ResourceLanguageKVT("HideRoots").ValueString, false);
        TreeDocuments = IKCMS_TreeStructureVFS.TreeDataBuildFolderedDocuments(this.vfsNode.Folder, RelationsOrdered.Where(r => r.type == IKGD_Constants.IKGD_ArchiveRelationName).Select(r => r.rnode_dst).Distinct(), hideRoots, mixFilesAndFolders);
      }
      catch { }
      //
    }

  }

}  //namespace
