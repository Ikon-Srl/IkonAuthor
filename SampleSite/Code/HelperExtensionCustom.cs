using System;
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
using Ikon.IKCMS;
using Ikon.IKCMS.Library.Resources;
using Ikon.IKGD.Library.Resources;
using Ikon.IKGD.Library;


namespace SampleSiteWeb
{

  public static class HelperExtensionCustom
  {
    // standardized DB accessor for custom tables
    public static Custom.DB.DataContext_Custom DC { get { return Ikon.IKCMS.IKCMS_ManagerIoC.requestContainer.Resolve<Custom.DB.DataContext_Custom>(); } }
    public static FS_Operations fsOp { get { return IKCMS_ManagerIoC.requestContainer.Resolve<FS_Operations>(); } }
    //


    //
    // menu builder custom
    //
    public static string BuildMenuQuick(this HtmlHelper helper, string rNodesRoot, string mainMenuClass)
    {
      XElement xMenu = new XElement("ul");
      try
      {
        if (!string.IsNullOrEmpty(mainMenuClass))
          xMenu.SetAttributeValue("class", mainMenuClass);
        List<int> sNodesRoot = fsOp.PathsFromNodesExt(null, Utility.ExplodeT<int>(rNodesRoot ?? IKGD_Config.AppSettings["RootsQuickMenu"], ",", " ", true), true, false, true).Select(p => p.sNode).Distinct().ToList();
        FS_Operations.FS_TreeNode<TreeNodeInfoVFS> treeRoot = IKCMS_TreeStructureVFS.TreeDataBuildCached(sNodesRoot, null);
        HelperExtensionCommon.GetMenuTreeWorkerExtendedV1(treeRoot, false, 0, null, xMenu, null);
        xMenu = HelperExtensionCommon.PostProcessMenuXml(xMenu);
      }
      catch { }
      if (xMenu == null || !xMenu.Elements().Any())
        return string.Empty;
      return xMenu.ToString();
    }


  }  //class HelperExtensionCustom
}
