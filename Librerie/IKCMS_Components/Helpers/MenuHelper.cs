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
using Ikon.IKGD.Library.Resources;
using Ikon.IKGD.Library;


namespace Ikon.IKCMS
{

  public static class HelperMenuCommon
  {
    public static FS_Operations fsOp { get { return IKCMS_ManagerIoC.requestContainer.ResolveNamed<FS_Operations>("readonly"); } }
    //public static FS_Operations fsOp { get { return IKCMS_ManagerIoC.requestContainer.Resolve<FS_Operations>(); } }

    //
    // get current model/url menu' index mapping at each menu' level
    //
    public static List<int> GetMenuItemIndexes(IKCMS_ModelCMS_Interface modelObj) { return GetMenuItemIndexes(modelObj, null); }
    public static List<int> GetMenuItemIndexes(IKCMS_ModelCMS_Interface modelObj, int? minDepth)
    {
      List<int> mappings = new List<int>();
      try
      {
        //
        List<FS_Operations.FS_TreeNode<TreeNodeInfoVFS>> matches = GetMenuItemMatches(modelObj);
        //
        foreach (var node in matches.FirstOrDefault().BackRecurseOnTree)
        {
          if (node != null && node.Parent != null)
            mappings.Insert(0, node.Parent.Nodes.IndexOf(node));
        }
      }
      catch { }
      if (minDepth != null && mappings.Count < minDepth.Value)
        mappings.AddRange(Enumerable.Repeat<int>(0, minDepth.Value - mappings.Count));
      return mappings;
    }


    public static List<FS_Operations.FS_TreeNode<TreeNodeInfoVFS>> GetMenuItemMatches(IKCMS_ModelCMS_Interface modelObj)
    {
      List<FS_Operations.FS_TreeNode<TreeNodeInfoVFS>> matches = new List<FS_Operations.FS_TreeNode<TreeNodeInfoVFS>>();
      try
      {
        int? sNodeSub = null;
        //if (modelObj != null)
        //  sNodeSub = modelObj.sNode;
        FS_Operations.FS_TreeNode<TreeNodeInfoVFS> treeRoot = IKCMS_TreeStructureVFS.TreeDataBuildCached(IKGD_ConfigVFS.Config.RootsCMS_sNodes, sNodeSub);
        int? sNode = null;
        try { sNode = modelObj.ModelRoot.sNode; }
        catch { }
        //
        if (sNode != null)
        {
          matches.AddRange(treeRoot.RecurseOnTree.Where(n => n.Data != null).Where(n => n.Data.sNode == sNode));
        }
        if (!matches.Any())
        {
          var request = System.Web.HttpContext.Current.Request;
          foreach (string url in new string[] { request.Url.PathAndQuery, request.RawUrl, request.Url.AbsolutePath, request.Path }.Distinct())
          {
            matches.AddRange(treeRoot.RecurseOnTree.Where(n => n.Data != null).Where(n => string.Equals(n.Data.Url, url, StringComparison.OrdinalIgnoreCase)));
            if (matches.Any())
              break;
          }
        }
      }
      catch { }
      return matches;
    }


    public static List<FS_Operations.FS_TreeNode<TreeNodeInfoVFS>> GetNodesFromUrl(string pageUrl) { return GetNodesFromUrl(string.IsNullOrEmpty(pageUrl) ? null : new Uri(pageUrl)); }
    public static List<FS_Operations.FS_TreeNode<TreeNodeInfoVFS>> GetNodesFromUrl(Uri pageUrl)
    {
      pageUrl = pageUrl ?? HttpContext.Current.Request.Url;
      List<FS_Operations.FS_TreeNode<TreeNodeInfoVFS>> matches = new List<FS_Operations.FS_TreeNode<TreeNodeInfoVFS>>();
      try
      {
        FS_Operations.FS_TreeNode<TreeNodeInfoVFS> treeRoot = IKCMS_TreeStructureVFS.TreeDataBuildCached(IKGD_ConfigVFS.Config.RootsCMS_sNodes, null);
        foreach (string url in new string[] { pageUrl.PathAndQuery, pageUrl.AbsolutePath }.Distinct())
        {
          // se nel menu' ci sono voci con link automatico alla prima url valida dobbiamo sovrascriverle con la url valida trovata eventualmente nel seguito (altrimenti fornisce il match su una url non valida)
          //matches.AddRange(treeRoot.RecurseOnTree.Where(n => n.Data != null).Where(n => string.Equals(n.Data.Url, url, StringComparison.OrdinalIgnoreCase)));
          foreach (var match in treeRoot.RecurseOnTree.Where(n => n.Data != null).Where(n => string.Equals(n.Data.Url, url, StringComparison.OrdinalIgnoreCase)))
          {
            matches.RemoveAll(m => string.Equals(m.Data.Url, match.Data.Url, StringComparison.OrdinalIgnoreCase));
            matches.Add(match);
          }
          if (matches.Any())
            break;
        }
      }
      catch { }
      return matches;
    }

  }
}
