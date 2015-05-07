using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Text.RegularExpressions;
using System.Web;
using System.Xml.Linq;
using System.Web.Mvc;
using System.Web.Mvc.Html;
using System.Reflection;
using System.Diagnostics;
using LinqKit;
using Autofac;

using Ikon;
using Ikon.GD;
using Ikon.IKCMS.Library.Resources;
using Ikon.IKGD.Library.Collectors;
using Ikon.IKCMS.Pagers;
using Ikon.Indexer;
using Ikon.IKGD.Library;
using Ikon.IKGD.Library.Resources;


namespace Ikon.IKCMS
{

  //
  // usage:
  // var models = IKCMS_ManagerIoC.applicationContainer.Resolve<IKCMS_ModelScannerParent_Bricks>(new NamedParameter("categories", "PlaceholderFooter")).FindModels(Model).OfType<IKCMS_ModelCMS_GenericBrickInterface>();
  //
  public class IKCMS_ModelScannerParent_Bricks : IKCMS_ModelScannerParentBase
  {
    public List<string> ManagerTypes = IKCMS_RegisteredTypes.Types_IKCMS_BrickBase_Interface.Where(t => t.IsClass && !t.IsAbstract && !t.IsGenericType).Select(t => t.Name).ToList();
    public List<string> Categories { get; set; }
    public override Expression<Func<IKGD_VNODE, bool>> FilterVNODE { get { return vn => vn.flag_folder == false; } }
    public override Expression<Func<IKGD_VDATA, bool>> FilterVDATA { get { return vd => ManagerTypes.Contains(vd.manager_type) && (Categories == null || Categories.Contains(vd.category)); } }

    public IKCMS_ModelScannerParent_Bricks(string categories)
    {
      CachingKey = this.GetType().Name + (categories ?? string.Empty);
      Categories = Utility.Explode(categories, ",", " ", true).ToList();
      if (!Categories.Any())
        Categories = null;
    }
  }


  public class IKCMS_ModelScannerParentPlaceholder_Bricks : IKCMS_ModelScannerParentBase
  {
    //public List<string> ManagerTypes = IKCMS_RegisteredTypes.Types_IKCMS_BrickBase_Interface.Where(t => t.IsClass && !t.IsAbstract && !t.IsGenericType).Select(t => t.Name).ToList();
    public List<string> ManagerTypes = IKCMS_RegisteredTypes.Types_IKCMS_BrickWithPlaceholder_Interface.Where(t => t.IsClass && !t.IsAbstract && !t.IsGenericType).Select(t => t.Name).ToList();
    public List<string> Placeholders { get; set; }
    public override Expression<Func<IKGD_VNODE, bool>> FilterVNODE { get { return vn => Placeholders.Any() ? vn.flag_folder == false && Placeholders.Contains(vn.placeholder) : vn.flag_folder == false; } }
    public override Expression<Func<IKGD_VDATA, bool>> FilterVDATA { get { return vd => ManagerTypes.Contains(vd.manager_type); } }

    public IKCMS_ModelScannerParentPlaceholder_Bricks(string placeholders)
    {
      placeholders = placeholders ?? string.Empty;
      this.Placeholders = Utility.Explode(placeholders, ",", " ", true);
      CachingKey = this.GetType().Name + "|" + placeholders;
    }
  }


  public class IKCMS_ModelScannerParentWithLike_Bricks : IKCMS_ModelScannerParentBase
  {
    public List<string> ManagerTypes = IKCMS_RegisteredTypes.Types_IKCMS_BrickBase_Interface.Where(t => t.IsClass && !t.IsAbstract && !t.IsGenericType).Select(t => t.Name).ToList();
    public List<string> Categories { get; set; }
    public string LikeExpr { get; protected set; }
    public Regex RegExExpr { get; protected set; }
    public override Expression<Func<IKGD_VNODE, bool>> FilterVNODE { get { return vn => vn.flag_folder == false; } }
    public override Expression<Func<IKGD_VDATA, bool>> FilterVDATA { get { return vd => ManagerTypes.Contains(vd.manager_type) && System.Data.Linq.SqlClient.SqlMethods.Like(vd.category, LikeExpr); } }
    public override Expression<Func<IKGD_VDATA, bool>> FilterVDATA_NOSQL { get { return vd => ManagerTypes.Contains(vd.manager_type) && RegExExpr.IsMatch(vd.category); } }

    public IKCMS_ModelScannerParentWithLike_Bricks(string LikeExpr)
    {
      LikeExpr = LikeExpr ?? string.Empty;
      this.LikeExpr = LikeExpr;
      CachingKey = this.GetType().Name + "|" + LikeExpr;
      this.RegExExpr = new Regex("^" + LikeExpr.Replace("%", ".*").Replace("_", ".") + "$", RegexOptions.IgnoreCase);
    }
  }


  public class IKCMS_ModelScannerParentWithLikeOrPlaceholder_Bricks : IKCMS_ModelScannerParentBase
  {
    public List<string> ManagerTypes = IKCMS_RegisteredTypes.Types_IKCMS_BrickBase_Interface.Where(t => t.IsClass && !t.IsAbstract && !t.IsGenericType).Select(t => t.Name).ToList();
    public List<string> Categories { get; set; }
    public List<string> Placeholders { get; set; }
    public string LikeExpr { get; protected set; }
    public Regex RegExExpr { get; protected set; }
    public override Expression<Func<IKGD_VNODE, bool>> FilterVNODE { get { return vn => Placeholders.Any() ? vn.flag_folder == false && Placeholders.Contains(vn.placeholder) : vn.flag_folder == false; } }
    public override Expression<Func<IKGD_VDATA, bool>> FilterVDATA { get { return vd => ManagerTypes.Contains(vd.manager_type) && System.Data.Linq.SqlClient.SqlMethods.Like(vd.category, LikeExpr); } }
    public override Expression<Func<IKGD_VDATA, bool>> FilterVDATA_NOSQL { get { return vd => ManagerTypes.Contains(vd.manager_type) && RegExExpr.IsMatch(vd.category); } }

    public IKCMS_ModelScannerParentWithLikeOrPlaceholder_Bricks(string likeExpr, string placeholders)
    {
      likeExpr = likeExpr ?? string.Empty;
      placeholders = placeholders ?? string.Empty;
      this.LikeExpr = likeExpr;
      this.Placeholders = Utility.Explode(placeholders, ",", " ", true);
      CachingKey = this.GetType().Name + "|" + likeExpr + "|" + placeholders;
      this.RegExExpr = new Regex("^" + likeExpr.Replace("%", ".*").Replace("_", ".") + "$", RegexOptions.IgnoreCase);
    }
  }


  //
  // usage:
  // IKCMS_ModelScannerParent_HeaderFlash modelScannerBack = IKCMS_ManagerIoC.applicationContainer.Resolve<IKCMS_ModelScannerParent_HeaderFlash>();
  //
  public class IKCMS_ModelScannerParent_HeaderFlash : IKCMS_ModelScannerParentBase
  {
    public override Expression<Func<IKGD_VNODE, bool>> FilterVNODE { get { return vn => vn.flag_folder == false; } }
    public override Expression<Func<IKGD_VDATA, bool>> FilterVDATA { get { return vd => vd.manager_type == typeof(IKCMS_ResourceType_Flash).Name && vd.category == "flash_header"; } }
  }


  //
  // usage:
  // IKCMS_ModelScannerParent_ShowReelElementV1 modelScannerBack = IKCMS_ManagerIoC.applicationContainer.Resolve<IKCMS_ModelScannerParent_ShowReelElementV1>();
  // var headerModels = modelScannerBack.FindModels(model).ToList();
  // var headerNodes = modelScannerBack.FindNodes(model).ToList();
  public class IKCMS_ModelScannerParent_ShowReelElementV1 : IKCMS_ModelScannerParentBase
  {
    public List<string> Categories = new List<string> { "PlaceholderHeader" };
    public override Expression<Func<IKGD_VNODE, bool>> FilterVNODE { get { return vn => vn.flag_folder == false; } }
    public override Expression<Func<IKGD_VDATA, bool>> FilterVDATA { get { return vd => vd.manager_type == typeof(IKCMS_ResourceType_ShowReelElementV1).Name && (Categories == null || Categories.Contains(vd.category)); } }

    // costruttore opzionale
    public IKCMS_ModelScannerParent_ShowReelElementV1(string categories)
    {
      CachingKey = this.GetType().Name + (categories ?? string.Empty);
      Categories = Utility.Explode(categories, ",", " ", true).ToList();
      if (!Categories.Any())
        Categories = null;
    }
  }


  // var modelsScannerBack = IKCMS_ManagerIoC.applicationContainer.Resolve<IKCMS_ModelScannerParent_ShowReelElementV1orImage_Header>().FindModels(model);
  public class IKCMS_ModelScannerParent_ShowReelElementV1orImage_Header : IKCMS_ModelScannerParentBase
  {
    public List<string> ManagerTypes = new List<string> { typeof(IKCMS_ResourceType_ShowReelElementV1).Name, typeof(IKCMS_ResourceType_ImageCMS).Name };
    public List<string> Categories = new List<string> { "PlaceholderHeader" };
    public override Expression<Func<IKGD_VNODE, bool>> FilterVNODE { get { return vn => vn.flag_folder == false; } }
    public override Expression<Func<IKGD_VDATA, bool>> FilterVDATA { get { return vd => ManagerTypes.Contains(vd.manager_type) && (Categories == null || Categories.Contains(vd.category)); } }

    // costruttore opzionale
    public IKCMS_ModelScannerParent_ShowReelElementV1orImage_Header(string categories)
    {
      CachingKey = this.GetType().Name + (categories ?? string.Empty);
      Categories = Utility.Explode(categories, ",", " ", true).ToList();
      if (!Categories.Any())
        Categories = null;
    }
  }


}
