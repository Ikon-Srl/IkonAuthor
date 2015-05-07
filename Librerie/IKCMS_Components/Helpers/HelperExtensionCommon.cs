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
using Ikon.IKCMS.Library.Resources;
using Ikon.IKGD.Library;
using Ikon.IKGD.Library.Resources;


namespace Ikon.IKCMS
{


  public static class HelperExtensionCommon
  {
    private static FS_Operations fsOp { get { return IKCMS_ManagerIoC.requestContainer.Resolve<FS_Operations>(); } }

    private static readonly string fakeAutoFindUrl = "::autourl::";
    private static readonly string[] fakeUrlList = new string[] { "", fakeAutoFindUrl, "javascript:;", "void();", "#" };


    public static string DDL_HelperSimple(string name, IEnumerable<SelectListItem> items, string cssClass) { return DDL_HelperSimple(name, null, items, cssClass, null, null, null); }
    public static string DDL_HelperSimple(string name, IEnumerable<SelectListItem> items, string cssClass, string extraAttrs, string blankItem) { return DDL_HelperSimple(name, null, items, cssClass, extraAttrs, null, blankItem); }
    public static string DDL_HelperSimple(string name, IEnumerable<SelectListItem> items, string cssClass, string extraAttrs, string blankItemValue, string blankItemText) { return DDL_HelperSimple(name, null, items, cssClass, extraAttrs, blankItemValue, blankItemText); }
    public static string DDL_HelperSimple(string name, IEnumerable<SelectListItem> itemsAll, IEnumerable<SelectListItem> items, string cssClass, string extraAttrs, string blankItemValue, string blankItemText)
    {
      StringBuilder sb = new StringBuilder();
      if (blankItemText != null || blankItemValue != null)
      {
        string firstSelected = string.Empty;
        if (!string.IsNullOrEmpty(blankItemValue))
        {
          for (int i = HttpContext.Current.Request.Params.Count - 1; i >= 0; i--)
          {
            if (HttpContext.Current.Request.Params.Keys[i] == name && HttpContext.Current.Request.Params[i] == blankItemValue)
            {
              firstSelected = "selected='selected'";
              break;
            }
          }
        }
        sb.AppendFormat("<option value=\"{2}\" title=\"{1}\" {3}>{0}</option>\n", HttpUtility.HtmlEncode(blankItemText), HttpUtility.HtmlAttributeEncode(blankItemText), HttpUtility.HtmlAttributeEncode(blankItemValue), firstSelected);
      }
      try
      {
        if (itemsAll != null)
        {
          foreach (var item in itemsAll)
          {
            sb.AppendFormat("<option value=\"{0}\" title=\"{3}\" {2} {4}>{1}</option>\n", HttpUtility.HtmlAttributeEncode(item.Value), HttpUtility.HtmlEncode(item.Text), (item.Selected ? "selected='selected'" : string.Empty), HttpUtility.HtmlAttributeEncode(item.Text), (items == null || items.Any(r => r.Value == item.Value)) ? null : "disabled='disabled' class='disabled'");
          }
        }
        else if (items != null)
        {
          foreach (var item in items)
          {
            sb.AppendFormat("<option value=\"{0}\" title=\"{3}\" {2}>{1}</option>\n", HttpUtility.HtmlAttributeEncode(item.Value), HttpUtility.HtmlEncode(item.Text), (item.Selected ? "selected='selected'" : string.Empty), HttpUtility.HtmlAttributeEncode(item.Text));
          }
        }
        sb.Insert(0, string.Format("<select name=\"{0}\" class=\"{1}\" {2}>\n", name, cssClass, extraAttrs));
      }
      catch { }
      sb.AppendLine("</select>\n");
      return sb.ToString();
    }


    public static string StarRatingHelperReadOnly(int maxValue, int? value, int? splits, string className) { return StarRatingHelper(null, null, maxValue, value, splits, className, true); }
    public static string StarRatingHelper(string url, string name, int maxValue, int? value, int? splits, string className, bool disabled)
    {
      StringBuilder sb = new StringBuilder();
      name = name.NullIfEmpty() ?? "star_" + Guid.NewGuid().GetHashCode().ToString("x");
      string disabledAttr = disabled ? "disabled='disabled'" : string.Empty;
      string classAttr = (className.NullIfEmpty() ?? "star") + ((splits != null) ? " {{split:{0}}}".FormatString(splits) : string.Empty);
      for (int i = 1; i <= maxValue; i++)
      {
        string checkedAttr = (i == value) ? "checked='checked'" : string.Empty;
        sb.AppendFormat("<input name='{0}' type='radio' value='{1}' class='{2}' {3} {4}/>\n", name, i, classAttr, checkedAttr, disabledAttr);
      }
      if (!string.IsNullOrEmpty(url))
      {
        sb.Insert(0, string.Format("<form action=\"{0}\" method=\"POST\">\n", url));
        sb.AppendLine("</form>\n");
      }
      return sb.ToString();
    }


    public static string GetSortingUrl(string qsVarName, string qsVarValue, string qsVarValueDefault, int? defaultDirection)
    {
      //string currentValue = HttpContext.Current.Request.Params[qsVarName].NullIfEmpty() ?? qsVarValueDefault ?? string.Empty;
      string currentValue = null;
      for (int i = 0; i < HttpContext.Current.Request.Params.Count; i++)
      {
        if (HttpContext.Current.Request.Params.Keys[i] == qsVarName && (HttpContext.Current.Request.Params[i] ?? string.Empty).Trim(' ', '+', '-') == qsVarValue)
        {
          currentValue = HttpContext.Current.Request.Params[i];
          break;
        }
      }
      currentValue = currentValue.NullIfEmpty() ?? qsVarValueDefault ?? string.Empty;
      int? nextDirection = currentValue.StartsWith("-") ? +1 : -1;  // attenzione che il + nelle query string viene convertito in spazio
      if (defaultDirection != null)
        currentValue = currentValue.TrimStart('+', '-', ' ');
      if (currentValue != qsVarValue)
        nextDirection = defaultDirection;
      string newValue = (defaultDirection != null ? (nextDirection < 0 ? "-" : "+") : string.Empty) + qsVarValue;
      if (Utility.TryParse<bool>(IKGD_Config.AppSettings["SortingLinksEnabled"], true) == false)
        return "javascript:;";
      return Utility.UriSetQuery(HttpContext.Current.Request.Url, qsVarName, newValue).ToString().EncodeAsAttribute();
    }


    public static string GetSortingCSS(string qsVarName, string qsVarValue, string qsVarValueDefault, int? defaultDirection, string cssClassActive, string cssClassPlus, string cssClassMinus)
    {
      //string currentValue = HttpContext.Current.Request.Params[qsVarName];
      string currentValue = HttpContext.Current.Request.Params.AllKeys.Contains(qsVarName) ? null : qsVarValueDefault;
      for (int i = 0; i < HttpContext.Current.Request.Params.Count; i++)
      {
        if (HttpContext.Current.Request.Params.Keys[i] == qsVarName && (HttpContext.Current.Request.Params[i] ?? string.Empty).Trim(' ', '+', '-') == qsVarValue)
        {
          currentValue = HttpContext.Current.Request.Params[i];
          break;
        }
      }
      currentValue = currentValue.NullIfEmpty() ?? string.Empty;
      int? currentDirection = currentValue.StartsWith("-") ? -1 : +1;  // attenzione che il + nelle query string viene convertito in spazio
      currentValue = currentValue.TrimStart('+', '-', ' ');
      if (currentValue != qsVarValue)
        currentDirection = defaultDirection;
      string css = (currentValue == qsVarValue) ? cssClassActive : string.Empty;
      if (currentDirection != null)
        css += (currentDirection < 0) ? cssClassMinus : cssClassPlus;
      return css.EncodeAsAttribute();
    }


    public static List<System.Web.UI.WebControls.HyperLink> BuildLinksFromRelations(IKCMS_ModelCMS_Interface model)
    {
      List<System.Web.UI.WebControls.HyperLink> links = new List<System.Web.UI.WebControls.HyperLink>();
      try
      {
        links = (model as IKCMS_ModelCMS_Page_Interface).LinksFromRelations.Select(n => new System.Web.UI.WebControls.HyperLink() { NavigateUrl = IKCMS_RouteUrlManager.GetMvcUrlGeneral(null, n.vNode.snode, null, true, false), Text = n.vNode.name }).ToList();
      }
      catch { }
      return links;
    }


    //
    // menu builder custom
    //
    public static string BuildMenuMain(this HtmlHelper helper, string mainMenuClass, object modelObj, params int[] sNodeRootActiveSubSet)
    {
      return BuildMenuGeneral(helper, mainMenuClass, modelObj, null, sNodeRootActiveSubSet);
    }


    public static string BuildMenuGeneral(this HtmlHelper helper, string mainMenuClass, object modelObj, int? maxLevels, params int[] sNodeRootActiveSubSet)
    {
      XElement xMenu = new XElement("ul");
      try
      {
        if (!string.IsNullOrEmpty(mainMenuClass))
          xMenu.SetAttributeValue("class", mainMenuClass);
        //var roots = IKGD_ConfigVFS.Config.RootsCMS_sNodes;
        var roots = IKGD_ConfigVFS.Config.RootsCMS_fsNodes.Where(n => n.LanguageCheck(IKGD_Language_Provider.Provider.LanguageNN)).Select(n => n.sNode).Distinct().ToList();
        if (sNodeRootActiveSubSet != null && sNodeRootActiveSubSet.Any())
          roots = roots.Intersect(sNodeRootActiveSubSet).ToList();
        List<int> sNodesSelected = null;
        if (modelObj != null && modelObj is IKCMS_ModelCMS_Interface)
        {
          try
          {
            IKCMS_ModelCMS_Interface model = (modelObj as IKCMS_ModelCMS_Interface).BackRecurseOnModels.OfType<IKCMS_ModelCMS_Page_Interface>().FirstOrDefault() ?? (modelObj as IKCMS_ModelCMS_Interface);
            if (model is IKCMS_ModelCMS_VFS_Interface && model.PathVFS != null)
            {
              sNodesSelected = model.PathVFS.Fragments.Select(f => f.sNode).ToList();
            }
            else
              sNodesSelected = new List<int> { model.sNode };
          }
          catch { }
        }
        //FS_Operations.FS_TreeNode<TreeNodeInfoVFS> treeRoot = IKCMS_TreeStructureVFS.TreeDataBuild(roots, null);
        FS_Operations.FS_TreeNode<TreeNodeInfoVFS> treeRoot = IKCMS_TreeStructureVFS.TreeDataBuildCached(roots, null);
        GetMenuTreeWorkerExtendedV1(treeRoot, true, 0, maxLevels, xMenu, sNodesSelected);
        xMenu = PostProcessMenuXml(xMenu);
      }
      catch { }
      if (xMenu == null || !xMenu.Elements().Any())
        return string.Empty;
      return xMenu.ToString();
    }


    public static string BuildMenuGeneralExt(this HtmlHelper helper, string mainMenuClass, object modelObj, int? maxLevels, string rNodesRoots, string sNodesRoots)
    {
      XElement xMenu = new XElement("ul");
      try
      {
        if (!string.IsNullOrEmpty(mainMenuClass))
          xMenu.SetAttributeValue("class", mainMenuClass);
        //List<int> roots = IKGD_ConfigVFS.Config.RootsCMS_sNodes;
        List<int> roots = IKGD_ConfigVFS.Config.RootsCMS_fsNodes.Where(n => n.LanguageCheck(IKGD_Language_Provider.Provider.LanguageNN)).Select(n => n.sNode).Distinct().ToList();
        if (!string.IsNullOrEmpty(rNodesRoots) || !string.IsNullOrEmpty(sNodesRoots))
        {
          string cacheKey = FS_OperationsHelpers.ContextHashNN("Ikon.IKCMS.HelperExtensionCommon.BuildMenuGeneralExtRoots", rNodesRoots, sNodesRoots);
          roots = FS_OperationsHelpers.CachedEntityWrapper(cacheKey, () =>
          {
            List<IKGD_Path> paths = null;
            paths = fsOp.PathsFromNodesExt(Utility.ExplodeT<int>(sNodesRoots, ",", " ", true), Utility.ExplodeT<int>(rNodesRoots, ",", " ", true), false, true).FilterCustom(new Func<IKGD_Path, bool>[] { IKGD_Path_Helper.FilterByRootCMS, IKGD_Path_Helper.FilterByActive, IKGD_Path_Helper.FilterByAreas, IKGD_Path_Helper.FilterByExpiry, IKGD_Path_Helper.FilterByLanguage }).ToList();
            return paths.Select(p => p.sNode).Distinct().ToList();
          }
          , m => m != null
          , Utility.TryParse<int?>(IKGD_Config.AppSettings["CachingMenu"], 3600), null, FS_OperationsHelpers.Const_CacheDependencyIKGD_vNode_vData);
        }
        if (roots == null || !roots.Any())
          roots = IKGD_ConfigVFS.Config.RootsCMS_fsNodes.Where(n => n.LanguageCheck(IKGD_Language_Provider.Provider.LanguageNN)).Select(n => n.sNode).Distinct().ToList();
        //
        List<int> sNodesSelected = null;
        if (modelObj != null && modelObj is IKCMS_ModelCMS_Interface)
        {
          try
          {
            IKCMS_ModelCMS_Interface model = (modelObj as IKCMS_ModelCMS_Interface).BackRecurseOnModels.OfType<IKCMS_ModelCMS_Page_Interface>().FirstOrDefault() ?? (modelObj as IKCMS_ModelCMS_Interface);
            if (model is IKCMS_ModelCMS_VFS_Interface && model.PathVFS != null)
            {
              sNodesSelected = model.PathVFS.Fragments.Select(f => f.sNode).ToList();
            }
            else
              sNodesSelected = new List<int> { model.sNode };
          }
          catch { }
        }
        //FS_Operations.FS_TreeNode<TreeNodeInfoVFS> treeRoot = IKCMS_TreeStructureVFS.TreeDataBuild(roots, null);
        FS_Operations.FS_TreeNode<TreeNodeInfoVFS> treeRoot = IKCMS_TreeStructureVFS.TreeDataBuildCached(roots, null);
        GetMenuTreeWorkerExtendedV1(treeRoot, true, 0, maxLevels, xMenu, sNodesSelected);
        xMenu = PostProcessMenuXml(xMenu);
      }
      catch { }
      if (xMenu == null || !xMenu.Elements().Any())
        return string.Empty;
      return xMenu.ToString();
    }


    public static string BuildMenuSub(this HtmlHelper helper, string mainMenuClass, object modelObj, bool renderRootNode, int? maxLevels) { return BuildMenuSub(helper, mainMenuClass, modelObj, renderRootNode, maxLevels, null); }
    public static string BuildMenuSub(this HtmlHelper helper, string mainMenuClass, object modelObj, bool renderRootNode, int? maxLevels, bool? startFromModelNode)
    {
      XElement xMenu = new XElement("ul");
      try
      {
        if (!string.IsNullOrEmpty(mainMenuClass))
          xMenu.SetAttributeValue("class", mainMenuClass);
        //
        if (modelObj != null && modelObj is IKCMS_ModelCMS_Interface)
        {
          IKCMS_ModelCMS_Page_Interface model = (modelObj as IKCMS_ModelCMS_Interface).BackRecurseOnModels.OfType<IKCMS_ModelCMS_Page_Interface>().FirstOrDefault();
          if (model != null)
          {
            IKGD_Path_Fragment frag = null;
            if (startFromModelNode == true)
              frag = model.PathVFS.LastFragment;
            else
              frag = model.PathVFS.Fragments.LastOrDefault(f => ((f.FlagsMenu & FlagsMenuEnum.SubTreeRoot) == FlagsMenuEnum.SubTreeRoot) && !IKGD_ConfigVFS.Config.RootsCMS_sNodes.Contains(f.sNode));
            if (frag != null)
            {
              List<int> sNodesSelected = null;
              if (model.PathVFS != null)
                sNodesSelected = model.PathVFS.Fragments.Select(f => f.sNode).ToList();
              //TODO: la versione con cache non funziona correttamente per i subMenu
              FS_Operations.FS_TreeNode<TreeNodeInfoVFS> treeRoot = IKCMS_TreeStructureVFS.TreeDataBuildCached(null, frag.sNode, renderRootNode);
              //FS_Operations.FS_TreeNode<TreeNodeInfoVFS> treeRoot = IKCMS_TreeStructureVFS.TreeDataBuild(null, frag.sNode, renderRootNode);
              bool processSelf = true;
              if (startFromModelNode == true)
              {
                treeRoot = treeRoot.FirstOrDefault(n => n.Data != null && n.Data.sNode == frag.sNode) ?? treeRoot;
                processSelf = treeRoot.Data == null;
              }
              GetMenuTreeWorkerExtendedV1(treeRoot, processSelf, 0, maxLevels, xMenu, sNodesSelected);
              xMenu = PostProcessMenuXml(xMenu);
            }
          }
        }
      }
      catch { }
      if (xMenu == null || !xMenu.Elements().Any())
        return string.Empty;
      return xMenu.ToString();
    }


    public static string BuildMenuSubFlattened(this HtmlHelper helper, string mainMenuClass, IKCMS_ModelCMS_Interface modelObj, int? maxLevels)
    {
      XElement xMenu = new XElement("ul");
      try
      {
        if (!string.IsNullOrEmpty(mainMenuClass))
          xMenu.SetAttributeValue("class", mainMenuClass);
        //
        if (modelObj != null && modelObj is IKCMS_ModelCMS_Page_Interface)
        {
          IKCMS_ModelCMS_Page_Interface model = (modelObj as IKCMS_ModelCMS_Interface).BackRecurseOnModels.OfType<IKCMS_ModelCMS_Page_Interface>().FirstOrDefault();
          if (model != null)
          {
            var frag = model.PathVFS.Fragments.LastOrDefault(f => ((f.FlagsMenu & FlagsMenuEnum.SubTreeRoot) == FlagsMenuEnum.SubTreeRoot) && !IKGD_ConfigVFS.Config.RootsCMS_sNodes.Contains(f.sNode));
            if (frag != null)
            {
              List<int> sNodesSelected = null;
              if (model.PathVFS != null)
                sNodesSelected = model.PathVFS.Fragments.Select(f => f.sNode).ToList();
              XElement xMenuAux = new XElement("ul");
              FS_Operations.FS_TreeNode<TreeNodeInfoVFS> treeRoot = IKCMS_TreeStructureVFS.TreeDataBuildCached(null, frag.sNode, false);
              GetMenuTreeWorkerExtendedV1(treeRoot, true, 0, maxLevels, xMenuAux, sNodesSelected);
              xMenuAux.Descendants("a").Where(x => x.AttributeValue("href", "javascript:;") != "javascript:;").ForEach(x => xMenu.Add(new XElement("li", x)));
            }
          }
        }
        xMenu = PostProcessMenuXml(xMenu);
      }
      catch { }
      if (xMenu == null || !xMenu.Elements().Any())
        return string.Empty;
      return xMenu.ToString();
    }


    public static string BuildMenuSiblings(this HtmlHelper helper, IKCMS_ModelCMS_Interface modelObj, string rootClass, string rootName, Func<FS_Operations.FS_NodeInfo_Interface, XElement, XElement> processor)
    {
      XElement xMenu = null;
      try
      {
        // cache key dipendente dal folder contenitore delle pagine
        string cacheKey = FS_OperationsHelpers.ContextHashNN("BuildMenuSiblings", modelObj.vfsNode.vNode.parent, rootClass, rootName);
        XElement xMenuReference = FS_OperationsHelpers.CachedEntityWrapper(cacheKey, () =>
        {
          try
          {
            var nodes = fsOp.Get_NodesInfoFiltered(vn => vn.parent == modelObj.vfsNode.vNode.parent, vd => vd.manager_type == typeof(IKCMS_ResourceType_PageCMS).Name, false).Where(n=>!n.vData.date_expiry.HasValue || n.vData.date_expiry.Value>DateTime.Now).OrderBy(n => n.vNode.position).ToList();
            XElement xMenuOut = new XElement("ul");
            XElement xPivot = xMenuOut;
            xMenuOut.SetAttributeValue("class", rootClass);
            if (!string.IsNullOrEmpty(rootName))
            {
              xMenuOut.Add(new XElement("li", rootName));
              xPivot = new XElement("ul");
              xMenuOut.Add(xPivot);
            }
            //
            foreach (var node in nodes)
            {
              string url = IKCMS_TreeStructureVFS.MenuFormatterWorker(node);
              XElement xLk = new XElement("a", new XAttribute("sNode", node.sNode), new XAttribute("href", url), new XText(node.vNode.name));
              XElement xLi = new XElement("li", xLk);
              if (processor != null)
                xLk = processor(node, xLk);
              if (xLk != null)
                xPivot.Add(xLi);
              //var resKVT = IKCMS_RegisteredTypes.Deserialize_IKCMS_ResourceVFS(node) as IKCMS_HasPropertiesLanguageKVT_Interface;
            }
            //
            return xMenuOut;
          }
          catch { return null; }
        }
        , 3600, FS_OperationsHelpers.Const_CacheDependencyIKGD_vNode_vData);
        xMenu = new XElement(xMenuReference);
        string sNodeSelected = modelObj.sNode.ToString();
        xMenu.Descendants("a").ForEach(x =>
        {
          if (x.AttributeValue("sNode") == sNodeSelected)
            x.SetAttributeValue("class", "selected");
          x.SetAttributeValue("sNode", null);
        });
        xMenu = PostProcessMenuXml(xMenu);
        if (xMenu == null || !xMenu.Elements().Any())
          return string.Empty;
        return xMenu.ToString();
      }
      catch { }
      return string.Empty;
    }


    //public static string BuildMenuSiblingsExample(this HtmlHelper helper, IKCMS_ModelCMS_Interface modelObj, string rootClass, string rootName)
    //{
    //  return BuildMenuSiblings(helper, modelObj, rootClass, rootName, (node, xEl) =>
    //  {
    //    try
    //    {
    //      var resKVT = IKCMS_RegisteredTypes.Deserialize_IKCMS_ResourceVFS(node) as IKCMS_HasPropertiesLanguageKVT_Interface;
    //      string title = node.vNode.name;
    //      string subTitle = resKVT.ResourceSettingsKVT.KeyFilterTry(IKGD_Language_Provider.Provider.Language, "TitleMenu").ValueString;
    //      xEl.RemoveNodes();
    //      xEl.Add(new XElement("strong", title), new XElement("br"), new XText(subTitle));
    //      return xEl;
    //    }
    //    catch { }
    //    return null;
    //  });
    //}



    //
    // generatore per la mappa del sito
    // maxLevelToSkipBC e' livello massimo fino al quale comprimere la ricorsione per i nodi flaggati con FlagsMenuEnum.SkipBreadCrumbs
    // quelli ai livelli superiori vengono mantenuti nel tree
    //
    public static string BuildSiteMapTree(this HtmlHelper helper, string mainMenuClass, int? maxLevels, int? maxLevelToSkipBC) { return BuildSiteMapTree(helper, null, mainMenuClass, maxLevels, maxLevelToSkipBC); }
    public static string BuildSiteMapTree(this HtmlHelper helper, string rNodesRoot, string mainMenuClass, int? maxLevels, int? maxLevelToSkipBC)
    {
      XElement xMenu = new XElement("ul");
      try
      {
        if (!string.IsNullOrEmpty(mainMenuClass))
          xMenu.SetAttributeValue("class", mainMenuClass);
        List<int> rNodes = IKGD_SiteMode.GetConfig4SiteMode<int>(rNodesRoot ?? IKGD_Config.AppSettings["RootsSiteMap"], ",");
        List<int> sNodesRoot = null;
        if (rNodes.Any())
        {
          sNodesRoot = fsOp.PathsFromNodesExt(null, rNodes, true, false, true).Select(p => p.sNode).Distinct().ToList();
        }
        else
        {
          sNodesRoot = IKGD_ConfigVFS.Config.RootsCMS_fsNodes.Where(n => n.LanguageCheck(IKGD_Language_Provider.Provider.LanguageNN)).Select(n => n.sNode).Distinct().ToList();
        }
        //FS_Operations.FS_TreeNode<TreeNodeInfoVFS> treeRoot = IKCMS_TreeStructureVFS.TreeDataBuild(sNodesRoot, null, false, true, true);
        FS_Operations.FS_TreeNode<TreeNodeInfoVFS> treeRoot = IKCMS_TreeStructureVFS.TreeDataBuildCached(sNodesRoot, null, false, true, true);
        GetSiteMapTreeWorker(treeRoot, xMenu, false, 0, maxLevels, maxLevelToSkipBC);
        xMenu = PostProcessMenuXml(xMenu);
      }
      catch { }
      if (xMenu == null || !xMenu.Elements().Any())
        return string.Empty;
      return xMenu.ToString();
    }


    public static string BuildDocumentsTree(this HtmlHelper helper, object modelObj, string mainClass, string sectionClass, string itemClass, string target)
    {
      XElement xMenu = new XElement("ul");
      sectionClass = sectionClass ?? "section";
      itemClass = itemClass ?? "leaf";
      if (!IKGD_SiteMode.IsTargetSupported)
        target = null;
      try
      {
        if (!string.IsNullOrEmpty(mainClass))
          xMenu.SetAttributeValue("class", mainClass);
        //
        if (modelObj is IKCMS_ModelCMS_DocumentCollection_Interface)
        {
          IKCMS_ModelCMS_DocumentCollection_Interface model = modelObj as IKCMS_ModelCMS_DocumentCollection_Interface;
          if (model != null)
          {
            //
            // worker function per la costruzione ricorsiva del tree
            //
            Action<FS_Operations.FS_TreeNode<TreeNodeInfoVFS>, XElement, bool> worker = null;
            worker = (tn, xNode, processSelf) =>
            {
              try
              {
                if (processSelf)
                {
                  XElement xLI = new XElement("li");
                  XElement xItem = null;
                  xNode.Add(xLI);
                  if (string.IsNullOrEmpty(tn.Data.Url) || tn.Data.Url == "javascript:;")
                  {
                    if (!string.IsNullOrEmpty(sectionClass))
                      xLI.SetAttributeValue("class", sectionClass);
                    xItem = new XElement("span", tn.Data.vNode.name);
                  }
                  else
                  {
                    if (!string.IsNullOrEmpty(itemClass))
                      xLI.SetAttributeValue("class", itemClass);
                    xItem = new XElement("a", new XAttribute("href", tn.Data.Url), tn.Data.vNode.name);
                    if (!string.IsNullOrEmpty(target))
                      xItem.SetAttributeValue("target", target);
                  }
                  if (xItem != null)
                    xLI.Add(xItem);
                  if (tn.Nodes.Any())
                  {
                    XElement subNodes = new XElement("ul");
                    xLI.Add(subNodes);
                    xNode = subNodes;
                  }
                }
                if (tn.Nodes.Any())
                {
                  foreach (var sn in tn.Nodes)
                    worker(sn, xNode, true);
                }
              }
              catch { }
            };
            //
            worker(model.TreeDocuments, xMenu, false);
            //
          }
        }
      }
      catch { }
      if (xMenu == null || !xMenu.Elements().Any())
        return string.Empty;
      return xMenu.ToString();
    }



    public static bool GetMenuTreeWorkerExtendedV1(FS_Operations.FS_TreeNode<TreeNodeInfoVFS> treeNode, bool processSelf, int level, int? maxLevels, XElement xNode, List<int> sNodesSelected) { return GetMenuTreeWorkerExtendedV1(treeNode, processSelf, level, maxLevels, xNode, sNodesSelected, null); }
    public static bool GetMenuTreeWorkerExtendedV1(FS_Operations.FS_TreeNode<TreeNodeInfoVFS> treeNode, bool processSelf, int level, int? maxLevels, XElement xNode, List<int> sNodesSelected, Func<FS_Operations.FS_TreeNode<TreeNodeInfoVFS>, string> urlProcessor)
    {
      if (treeNode == null)
        return false;
      //
      bool selected = false;
      bool selected_parent = false;
      //
      XElement itemContainer = new XElement("li");
      XElement itemControl = new XElement("a");
      itemContainer.Add(itemControl);
      //
      // non sempre devo processare l'output del nodo (eg. root)
      //
      if (processSelf)
      {
        try
        {
          selected = (sNodesSelected != null) && (sNodesSelected.LastOrDefault() == treeNode.Data.vNode.snode);
          selected_parent |= (sNodesSelected != null) && sNodesSelected.Contains(treeNode.Data.vNode.snode) && (selected == false);
          xNode.Add(itemContainer);
          //
          string nodeText = treeNode.Data.vNode.name;
          //nodeText = string.Format("{0} [{1}] -> {2}", treeNode.Data.vNode.name, treeNode.Data.vNode.snode, treeNode.Data.Url);
          //nodeText = string.Format("{0} [{1}]", treeNode.Data.vNode.name, treeNode.Data.vNode.snode);
          //nodeText = string.Format("{0} [{1}] [{2}]", treeNode.Data.vNode.name, treeNode.Data.vNode.snode, treeNode.Data.vData.manager_type);
          //
          string url = null;
          if (urlProcessor != null)
          {
            try { url = urlProcessor(treeNode); }
            catch { }
          }
          else
          {
            url = treeNode.Data.Url ?? string.Empty;
          }
          if (url.StartsWith("~/"))
            url = Utility.ResolveUrl(url);
          if (string.IsNullOrEmpty(url) || ((treeNode.Data.vData.FlagsMenu & FlagsMenuEnum.UnSelectableNode) == FlagsMenuEnum.UnSelectableNode && (treeNode.Data.vData.FlagsMenu & FlagsMenuEnum.FindFirstValidNode) != FlagsMenuEnum.FindFirstValidNode))
            url = "javascript:;";
          if ((treeNode.Data.vData.FlagsMenu & FlagsMenuEnum.FindFirstValidNode) == FlagsMenuEnum.FindFirstValidNode && fakeUrlList.Contains(url))
            url = fakeAutoFindUrl;
          itemControl.SetAttributeValue("href", url);
          if (!string.IsNullOrEmpty(url) && url != "javascript;")
            if (!string.IsNullOrEmpty(treeNode.Data.Target))
              itemControl.SetAttributeValue("target", treeNode.Data.Target);
          itemControl.Add(new XText(nodeText));
          //
          if (selected)
          {
            itemControl.AddAttributeFragment("class", "selected");
            //itemControl.Value += " [SELECTED]";
          }
          //
          if ((treeNode.Data.vData.FlagsMenu & FlagsMenuEnum.SkipBreadCrumbs) == FlagsMenuEnum.SkipBreadCrumbs)
          {
            //itemControl.Value += " [skipBC]";
            itemControl.AddAttributeFragment("class", "skipBC");
            itemContainer.AddAttributeFragment("class", "skipBC");
          }
          //
          itemContainer.AddAttributeFragment("class", "idx_{0}_{1}".FormatString(level, itemContainer.Parent.Elements("li").Count()));
          //
        }
        catch { }
      }
      //
      // creo il contenitore <ul/> per il livello successivo e relativa ricorsione
      //
      if (treeNode.Nodes.Count > 0 && level < maxLevels.GetValueOrDefault(int.MaxValue))
      {
        XElement nextLevelContainer = null;
        if (itemContainer.Parent != null)
        {
          nextLevelContainer = new XElement("ul");
          nextLevelContainer.AddAttributeFragment("class", "level_{0}".FormatString(level + 1));
          nextLevelContainer.AddAttributeFragment("class", "idx_{0}_{1}".FormatString(level, itemContainer.Parent.Elements("li").Count()));  // idx fa riferimento al livello precedente
          itemContainer.Add(nextLevelContainer);
        }
        else
          nextLevelContainer = xNode;
        //
        foreach (var node in treeNode.Nodes)
          selected_parent |= GetMenuTreeWorkerExtendedV1(node, true, level + 1, maxLevels, nextLevelContainer, sNodesSelected, urlProcessor);
      }
      //
      // se uno dei child e' selezionato allora apro anche il nodo corrente
      //
      if (selected_parent)
      {
        itemControl.AddAttributeFragment("class", "selected_parent");
        //itemControl.Value += " [SELECTED_PARENT]";
      }
      if (itemContainer.Elements("ul").Any())
      {
        itemContainer.AddAttributeFragment("class", "hasChild");
      }
      else
      {
        itemContainer.AddAttributeFragment("class", "isLeaf");
      }
      //
      return selected;
    }


    public static void GetSiteMapTreeWorker(FS_Operations.FS_TreeNode<TreeNodeInfoVFS> treeNode, XElement xNode, bool processSelf, int level, int? maxLevels, int? maxLevelToSkipBC)
    {
      if (treeNode == null)
        return;
      //
      string sectionClass = "section";
      string leafClass = "leaf";
      XElement itemContainer = new XElement("li");
      XElement itemControl = new XElement("a");
      itemContainer.Add(itemControl);
      //
      // non sempre devo processare l'output del nodo (eg. root o skipBC)
      //
      if (processSelf)
      {
        try
        {
          //
          string nodeText = treeNode.Data.vNode.name;
          string url = treeNode.Data.Url ?? string.Empty;
          if (url.StartsWith("~/"))
            url = Utility.ResolveUrl(url);
          if (string.IsNullOrEmpty(url) || (treeNode.Data.vData.FlagsMenu & FlagsMenuEnum.UnSelectableNode) == FlagsMenuEnum.UnSelectableNode)
            url = "javascript:;";
          itemControl.SetAttributeValue("href", url);
          if (!string.IsNullOrEmpty(url) && url != "javascript;")
            if (!string.IsNullOrEmpty(treeNode.Data.Target))
              itemControl.SetAttributeValue("target", treeNode.Data.Target);
          itemControl.Add(new XText(nodeText));
          //
          if ((treeNode.Data.vData.FlagsMenu & FlagsMenuEnum.SkipBreadCrumbs) == FlagsMenuEnum.SkipBreadCrumbs)
          {
            //itemControl.Value = "[skipBC] " + itemControl.Value;
            itemControl.AddAttributeFragment("class", "skipBC");
            itemContainer.AddAttributeFragment("class", "skipBC");
          }
          itemContainer.AddFirst(new XElement("div", new XAttribute("class", "hitarea"), string.Empty));  // seno aggiungiamo un testo vuoto genera l'elemento senza contenuti e JS non funziona piu'!
          //
          if ((treeNode.Data.vData.FlagsMenu & FlagsMenuEnum.SkipBreadCrumbs) != FlagsMenuEnum.SkipBreadCrumbs || level > maxLevelToSkipBC.GetValueOrDefault(int.MinValue))
            xNode.Add(itemContainer);
          //
          try { itemContainer.AddAttributeFragment("class", "idx_{0}_{1}".FormatString(level, itemContainer.Parent.Elements("li").Count())); }
          catch { }
        }
        catch { }
      }
      //
      // creo il contenitore <ul/> per il livello successivo e relativa ricorsione
      //
      if (treeNode.Nodes.Count > 0 && level < maxLevels.GetValueOrDefault(int.MaxValue))
      {
        XElement nextLevelContainer = null;
        if (itemContainer.Parent != null)
        {
          nextLevelContainer = new XElement("ul");
          nextLevelContainer.AddAttributeFragment("class", "level_{0}".FormatString(level + 1));
          //nextLevelContainer.AddAttributeFragment("class", "idx_{0}_{1}".FormatString(level, itemContainer.Parent.Elements("li").Count()));  // idx fa riferimento al livello precedente
          itemContainer.Add(nextLevelContainer);
        }
        else
          nextLevelContainer = xNode;
        //
        foreach (var node in treeNode.Nodes)
          GetSiteMapTreeWorker(node, nextLevelContainer, true, level + 1, maxLevels, maxLevelToSkipBC);
        //
      }
      //
      if (itemContainer.Elements("ul").Any())
        itemContainer.ToggleCssClass("class", sectionClass, true);
      else
        itemContainer.ToggleCssClass("class", leafClass, true);
      //else if (itemContainer.Elements().Where(x => x.Name.LocalName != "ul").Any(x => x.Descendants("a").Any()))
      //  itemContainer.ToggleCssClass("class", itemClass, true);
      //
    }


    //
    // postprocessor del menu xml per sistemare le url a ricerca automatica
    //
    public static XElement PostProcessMenuXml(XElement xMenu)
    {
      try
      {
        if (!IKGD_SiteMode.IsTargetSupported)
        {
          // il sito in modalita' accessibile non supporta il target per i link
          xMenu.Descendants("a").Where(x => x.AttributeValue("target") != null).ForEach(x => x.SetAttributeValue("target", null));
        }
        var fakeAutoFindUrlNodes = xMenu.DescendantsAndSelf("a").Where(x => x.AttributeValue("href") == fakeAutoFindUrl).Reverse();
        foreach (var xLk in fakeAutoFindUrlNodes)
        {
          XElement xLkValid = null;
          try { xLkValid = xLk.Parent.DescendantsAndSelf("a").FirstOrDefault(a => !fakeUrlList.Contains(a.AttributeValueNN("href"))); }
          catch { }
          xLk.SetAttributeValue("href", xLkValid != null ? xLkValid.AttributeValue("href", "javascript:;") : "javascript:;");
        }
      }
      catch { }
      return xMenu;
    }



    public static List<IKCMS_ModelCMS_Interface> GetModelsFromRelations(IKCMS_ModelCMS_Interface model)
    {
      List<IKCMS_ModelCMS_Interface> models = null;
      string cacheKey = FS_OperationsHelpers.ContextHashNN("HelperExtensionCustom.GetModelsFromRelations", model.rNode);
      models = FS_OperationsHelpers.CachedEntityWrapper(cacheKey, () =>
      {
        List<IKCMS_ModelCMS_Interface> mdls = null;
        try { mdls = model.RelationsOrdered.Where(r => r.type == IKGD_Constants.IKGD_LinkRelationName).Select(r => IKCMS_ModelCMS_Provider.Provider.ModelBuildGenericByRNODE(r.rnode_dst, r.snode_dst)).Where(m => m != null).ToList(); }
        catch { mdls = new List<IKCMS_ModelCMS_Interface>(); }
        return mdls;
      }, 3600, FS_OperationsHelpers.Const_CacheDependencyIKGD_vNode_vData_iNode_Relation_Property);
      //
      return models ?? new List<IKCMS_ModelCMS_Interface>();
    }


    public static List<IKCMS_ModelCMS_Interface> GetModelsFromArchiveSubFolder(IKCMS_ModelCMS_Interface model)
    {
      List<IKCMS_ModelCMS_Interface> models = null;
      string cacheKey = FS_OperationsHelpers.ContextHashNN("HelperExtensionCustom.GetModelsFromArchiveSubFolder", model.rNode);
      models = FS_OperationsHelpers.CachedEntityWrapper(cacheKey, () =>
      {
        List<IKCMS_ModelCMS_Interface> mdls = null;
        try
        {
          var fsNodeArchive = fsOp.Get_NodesInfoFiltered(vn => vn.flag_folder && vn.parent == model.rNode, vd => vd.manager_type == typeof(IKCMS_FolderType_ArchiveRoot).Name, null).FirstOrDefault();
          if (fsNodeArchive != null)
          {
            var manager_types = IKCMS_RegisteredTypes.Types_IKCMS_Page_Interface.Select(t => t.Name).ToList();
            var fsNodes = fsOp.Get_NodesInfoFiltered(vn => vn.flag_folder && vn.parent == fsNodeArchive.vData.rnode, vd => manager_types.Contains(vd.manager_type), null, FS_Operations.FilterVFS.ACL | FS_Operations.FilterVFS.Language).ToList();
            mdls = fsNodes.OrderBy(n => n.Position).ThenBy(n => n.Name).ThenBy(n => n.sNode).Select(n => IKCMS_ModelCMS_Provider.Provider.ModelBuildGeneric(n.sNode)).Where(m => m != null).ToList();
          }
        }
        catch { }
        return mdls ?? new List<IKCMS_ModelCMS_Interface>();
      }, 3600, FS_OperationsHelpers.Const_CacheDependencyIKGD_vNode_vData_iNode_Relation_Property);
      //
      return models ?? new List<IKCMS_ModelCMS_Interface>();
    }



    //public static string ShowReelFlashGetUrl<T>(IKCMS_ModelCMS_Interface model, string categories, string showReelType, string fallbackImageStream)
    //  where T : Controller, ShowReelController_Interface
    //{
    //  try
    //  {
    //    IKCMS_ModelScannerParent_ShowReelElementV1 manager = IKCMS_ManagerIoC.applicationContainer.Resolve<IKCMS_ModelScannerParent_ShowReelElementV1>(new NamedParameter("categories", categories));
    //    if (fallbackImageStream != null)
    //    {
    //      var modelImage = manager.FindModels(model).OfType<IKCMS_ModelCMS_GenericBrickInterface>().FirstOrDefault(m => m.HasStream(fallbackImageStream));
    //      if (modelImage != null)
    //      {
    //        string url = IKCMS_RouteUrlManager.GetUrlProxyVFS(modelImage.rNode, null, fallbackImageStream, null, null, false, null, false);
    //        return url;
    //      }
    //      return "javascript:;";
    //    }
    //    var nodes = manager.FindNodes(model).Select(n => n.rNode).Distinct().ToList();
    //    //
    //    string hash = string.Format("{0}|{1}", FS_OperationsHelpers.VersionFrozenSession, IKGD_Language_Provider.Provider.LanguageNN);
    //    string xmlGen = IKCMS_RouteUrlManager.GetMvcActionUrl<T>(c => c.ShowReelXmlGenerator(showReelType, Utility.Implode(nodes, ","), hash));
    //    string qs = "lingua={1}&cache_xml=&nome_xml={0}".FormatString(HttpUtility.UrlEncode(xmlGen), IKGD_Language_Provider.Provider.LanguageNN);  // solo il frammento per le FlashVars
    //    return qs;
    //    //
    //  }
    //  catch { }
    //  return "javascript:;";
    //}


    public static string ShowReelFlashGetFlashVars<T>(IKCMS_ModelCMS_Interface model, string categories, string placheholder, string xmlVirtualPath, string fallbackImageStream)
      where T : Controller, ShowReelController_Interface
    {
      string flashVars = "javascript:;";
      try
      {
        if (string.IsNullOrEmpty(xmlVirtualPath))
          return flashVars;
        IKCMS_ModelScannerParentBase manager = null;
        if (!string.IsNullOrEmpty(placheholder))
        {
          manager = IKCMS_ManagerIoC.applicationContainer.Resolve<IKCMS_ModelScannerParentPlaceholder_Bricks>(new NamedParameter("placeholders", placheholder));
        }
        else
        {
          manager = IKCMS_ManagerIoC.applicationContainer.Resolve<IKCMS_ModelScannerParent_ShowReelElementV1>(new NamedParameter("categories", categories));
        }
        if (manager == null)
          return flashVars;
        if (fallbackImageStream != null)
        {
          var modelImage = manager.FindModels(model).OfType<IKCMS_ModelCMS_GenericBrickInterface>().FirstOrDefault(m => m.HasStream(fallbackImageStream));
          if (modelImage != null)
          {
            string url = IKCMS_RouteUrlManager.GetUrlProxyVFS(modelImage.rNode, null, fallbackImageStream, null, null, false, null, false);
            return url;
          }
          return flashVars;
        }
        var nodes = manager.FindNodes(model).Select(n => n.rNode).Distinct().ToList();
        //
        string hash = string.Format("{0}|{1}", FS_OperationsHelpers.VersionFrozenSession, IKGD_Language_Provider.Provider.LanguageNN);
        string xmlGen = IKCMS_RouteUrlManager.GetMvcActionUrl<T>(c => c.ShowReelXmlGeneratorAutoV1(xmlVirtualPath, Utility.Implode(nodes, ","), IKGD_Language_Provider.Provider.LanguageNN, hash));
        string qs = "lingua={1}&cache_xml=&nome_xml={0}".FormatString(HttpUtility.UrlEncode(xmlGen), IKGD_Language_Provider.Provider.LanguageNN);  // solo il frammento per le FlashVars
        return qs;
        //
      }
      catch { }
      return flashVars;
    }




  }  //class HelperExtensionCommon
}
