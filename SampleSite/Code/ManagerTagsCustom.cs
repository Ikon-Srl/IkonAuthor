using System;
using System.Collections;
using System.Collections.Specialized;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Web;
using System.Xml.Linq;
using System.Web.Mvc;
using System.Web.Mvc.Html;
using System.Reflection;
using LinqKit;
using Autofac;

using Ikon;
using Ikon.GD;
using Ikon.IKGD.Library.Collectors;
using Ikon.IKCMS.Library.Resources;
using Ikon.IKCMS.Pagers;
using Ikon.IKCMS;
using Ikon.IKGD.Library.Resources;


namespace SampleSiteWeb
{


  public abstract class ManagerTagFilter_BricksBase : ManagerTagFilterBase
  {
    public override ManagerTagFilterBase.FetchModeEnum FetchMode { get { return FetchModeEnum.rNodeFetch; } }
    public override bool? FilteredResourcesAreFolders { get { return false; } }

    public ManagerTagFilter_BricksBase(IKCMS_ModelCMS_Interface model)
      : base(model)
    {
      AllowedTypeNames = IKCMS_RegisteredTypes.Types_IKCMS_BrickBase_Interface.Select(t => t.Name).ToList();
      AllowedCategories = new List<string> { };
      AllowEmptyFilterAndArchiveSet = true;
      UseModelFolderAsArchive = false;
      UseGenericModelBuild = true;
    }
  }


  public abstract class ManagerTagFilter_PhotoGalleryBase : ManagerTagFilterBase
  {
      public override ManagerTagFilterBase.FetchModeEnum FetchMode { get { return FetchModeEnum.rNodeFetch; } }
      public override bool? FilteredResourcesAreFolders { get { return false; } }

      public ManagerTagFilter_PhotoGalleryBase(IKCMS_ModelCMS_Interface model)
          : base(model)
      {
          AllowedTypeNames = new List<string> { typeof(IKCMS_ResourceType_ImageCMS).Name };
          AllowedCategories = new List<string> { "image_photogallery" };
          AllowEmptyFilterAndArchiveSet = false;
          UseModelFolderAsArchive = false;
          UseGenericModelBuild = false;
      }
  }


  public class ManagerTagFilter_PhotoGallery : ManagerTagFilter_PhotoGalleryBase
  {
      public ManagerTagFilter_PhotoGallery(IKCMS_ModelCMS_Interface model)
          : base(model)
      { }
  }


  public class ManagerTagFilter_VideoGallery : ManagerTagFilter_PhotoGalleryBase
  {
      public ManagerTagFilter_VideoGallery(IKCMS_ModelCMS_Interface model)
          : base(model)
      {
          AllowedCategories = new List<string> { "image_videogallery" };
      }
  }


  //public class ManagerTagFilter_ArchivioFiles : ManagerTagFilterBase
  //{
  //  public override ManagerTagFilterBase.FetchModeEnum FetchMode { get { return FetchModeEnum.rNodeFetch; } }
  //  public override bool? FilteredResourcesAreFolders { get { return false; } }

  //  public ManagerTagFilter_ArchivioFiles(IKCMS_ModelCMS_Interface model)
  //    : base(model)
  //  {
  //    AllowedTypeNames = new List<string> { typeof(IKCMS_ResourceType_FileCMS).Name };
  //    AllowedCategories = new List<string> { "file" };
  //    SelectTagsWithVarNameOnly = true;
  //    LikeIsUnordered = true;
  //    AllowEmptyFilterAndArchiveSet = true;
  //    UseModelFolderAsArchive = false;
  //    UseGenericModelBuild = true;
  //  }
  //}


  public class ManagerTagFilter_Bandi : ManagerTagFilterBase
  {
    public override ManagerTagFilterBase.FetchModeEnum FetchMode { get { return FetchModeEnum.rNodeFetch; } }
    public override bool? FilteredResourcesAreFolders { get { return true; } }

    public ManagerTagFilter_Bandi(IKCMS_ModelCMS_Interface model)
      : base(model)
    {
      AllowedCategories = new List<string> { "PageCMS_Bando" };
      AllowEmptyFilterAndArchiveSet = false;
      //UseModelFolderAsArchive = true;
    }
  }


}
