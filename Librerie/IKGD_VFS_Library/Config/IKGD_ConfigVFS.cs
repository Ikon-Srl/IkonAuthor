/*
 * 
 * Intranet Ikon
 * 
 * Copyright (C) 2009 Ikon Srl
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

using Ikon;
using Ikon.GD;
using Ikon.Log;


namespace Ikon.GD
{


  public static class IKGD_ConfigVFS
  {
    private static object _lock = new object();


    static IKGD_ConfigVFS()
    {
      //Clear();
    }


    private static string CacheKey { get { return "CacheKey_IKGD_ConfigVFS_" + IKGD_SiteMode.GetSiteHash; } }
    private static string CacheKeyRoot { get { return "CacheKey_IKGD_ConfigVFS_Root_" + IKGD_SiteMode.GetSiteHash; } }


    public static void Clear()
    {
      lock (_lock)
      {
        try { HttpRuntime.Cache.Remove(CacheKey); }
        catch { }
        try { HttpRuntime.Cache.Remove(CacheKeyRoot); }
        catch { }
      }
    }


    public static ConfigVFS Config
    {
      get
      {
        lock (_lock)
        {
          ConfigVFS config = HttpRuntime.Cache[CacheKey] as ConfigVFS;
          if (config == null)
          {
            config = BuildConfigVFS(false);
            if (config != null)
            {
              AggregateCacheDependency sqlDeps = new AggregateCacheDependency();
              sqlDeps.Add(new SqlCacheDependency("GDCS", "IKGD_CONFIG"));
              HttpRuntime.Cache.Insert(CacheKey, config, sqlDeps, Cache.NoAbsoluteExpiration, Cache.NoSlidingExpiration, CacheItemPriority.High, null);
            }
          }
          return config;
        }
      }
    }


    public static ConfigVFS ConfigExt
    {
      get
      {
        lock (_lock)
        {
          string cacheKeyAux = FS_OperationsHelpers.IsRoot ? CacheKeyRoot : CacheKey;
          ConfigVFS config = HttpRuntime.Cache[cacheKeyAux] as ConfigVFS;
          if (config == null)
          {
            config = BuildConfigVFS(true);
            if (config != null)
            {
              AggregateCacheDependency sqlDeps = new AggregateCacheDependency();
              sqlDeps.Add(new SqlCacheDependency("GDCS", "IKGD_CONFIG"));
              HttpRuntime.Cache.Insert(cacheKeyAux, config, sqlDeps, Cache.NoAbsoluteExpiration, Cache.NoSlidingExpiration, CacheItemPriority.High, null);
            }
          }
          return config;
        }
      }
    }


    public static ConfigVFS BuildConfigVFS(bool enableRootAccess)
    {
      ConfigVFS config = new ConfigVFS();
      try
      {
        //
        // lasciando la transaction abbiamo riscontrato in rari casi, durante lo startup del sito e abbastanza difficili da triggerare, dei
        // problemi con operazioni non valide nel corso della transaction relative ad altre operazioni effettuate dall' SQL RoleProvider
        // sembra piu' sicuro disabilitare la transaction che mantenerla per isolare l'accesso
        //
        using (FS_Operations fsOp = new FS_Operations(null, true, false, true))
        {
          fsOp.EnsureOpenConnection();
          bool useRealTransactions = Utility.TryParse<bool>(IKGD_Config.AppSettings["IKGD_ConfigVFS_TransactionsEnabled"], false);
          using (System.Transactions.TransactionScope ts = useRealTransactions ? IKGD_TransactionFactory.TransactionReadUncommitted(600) : IKGD_TransactionFactory.TransactionNone(600))
          {
            config.RootsVFS_fsNodes = null;
            config.RootsCMS_fsNodes = null;
            //
            //enableRootAccess &= fsOp.IsRoot;
            enableRootAccess &= FS_OperationsHelpers.IsRoot;
            List<int> foldersList = null;
            //
            // folders per le roots visibili nell'Author
            //
            foldersList = IKGD_SiteMode.GetConfig4SiteMode<int>(IKGD_Config.AppSettings["Author_Roots"], ",");
            if (foldersList.Any() && enableRootAccess == false)
            {
              try { config.RootsAuthor_fsNodes = fsOp.Get_NodesInfoFiltered(vn => vn.flag_folder && foldersList.Contains(vn.folder), null, null, FS_Operations.FilterVFS.Disabled | FS_Operations.FilterVFS.ACL | FS_Operations.FilterVFS.Deleted).ToList().OrderBy(n => foldersList.IndexOf(n.vNode.folder)).ThenBy(n => n.vNode.position).ThenBy(n => n.vNode.snode).OfType<FS_Operations.FS_NodeInfo_Interface>().ToList(); }
              catch { }
            }
            if (config.RootsAuthor_fsNodes == null || !config.RootsAuthor_fsNodes.Any())
            {
              try { config.RootsAuthor_fsNodes = fsOp.Get_NodesInfoFiltered(vn => vn.flag_folder && vn.parent == 0, null, null, FS_Operations.FilterVFS.Disabled | FS_Operations.FilterVFS.ACL | FS_Operations.FilterVFS.Deleted).OrderBy(n => n.vNode.position).ThenBy(n => n.vNode.snode).OfType<FS_Operations.FS_NodeInfo_Interface>().ToList(); }
              catch { }
            }
            //
            // folders per le roots accessivili al VFS
            //
            foldersList = IKGD_SiteMode.GetConfig4SiteMode<int>(IKGD_Config.AppSettings["VFS_Roots"], ",");
            if (foldersList.Any())
            {
              try { config.RootsVFS_fsNodes = fsOp.Get_NodesInfoFiltered(vn => vn.flag_folder && foldersList.Contains(vn.folder), null, null, FS_Operations.FilterVFS.Disabled | FS_Operations.FilterVFS.ACL | FS_Operations.FilterVFS.Deleted).ToList().OrderBy(n => foldersList.IndexOf(n.vNode.folder)).ThenBy(n => n.vNode.position).ThenBy(n => n.vNode.snode).OfType<FS_Operations.FS_NodeInfo_Interface>().ToList(); }
              catch { }
            }
            if (config.RootsVFS_fsNodes == null || !config.RootsVFS_fsNodes.Any())
            {
              config.RootsVFS_fsNodes = config.RootsAuthor_fsNodes;
            }
            //
            // folders per le roots visibili nel frontend
            //
            foldersList = IKGD_SiteMode.GetConfig4SiteMode<int>(IKGD_Config.AppSettings["RootsMenuFolders"], ",");  // rnodes/folders delle root CMS
            if (foldersList.Any())
            {
              try { config.RootsCMS_fsNodes = fsOp.Get_NodesInfoFiltered(vn => vn.flag_folder && foldersList.Contains(vn.folder), null, null, FS_Operations.FilterVFS.Disabled | FS_Operations.FilterVFS.ACL | FS_Operations.FilterVFS.Deleted).ToList().OrderBy(n => foldersList.IndexOf(n.vNode.folder)).ThenBy(n => n.vNode.position).ThenBy(n => n.vNode.snode).OfType<FS_Operations.FS_NodeInfo_Interface>().ToList(); }
              catch { }
            }
            if (config.RootsCMS_fsNodes == null || !config.RootsCMS_fsNodes.Any())
            {
              //foldersList = Utility.ExplodeT<int>(IKGD_Config.AppSettings["RootsMenu"], ",", " ", true);  // snodes delle root CMS
              foldersList = IKGD_SiteMode.GetConfig4SiteMode<int>(IKGD_Config.AppSettings["RootsMenu"], ",");  // snodes delle root CMS
              try { config.RootsCMS_fsNodes = fsOp.Get_NodesInfoFiltered(vn => vn.flag_folder && foldersList.Contains(vn.snode), null, null, FS_Operations.FilterVFS.Disabled | FS_Operations.FilterVFS.ACL | FS_Operations.FilterVFS.Deleted).ToList().OrderBy(n => foldersList.IndexOf(n.vNode.snode)).ThenBy(n => n.vNode.position).ThenBy(n => n.vNode.snode).OfType<FS_Operations.FS_NodeInfo_Interface>().ToList(); }
              catch { }
            }
            if (config.RootsCMS_fsNodes == null || !config.RootsCMS_fsNodes.Any())
            {
              try { config.RootsCMS_fsNodes = fsOp.Get_NodesInfoFiltered(vn => vn.flag_folder && vn.parent == 0, null, null, FS_Operations.FilterVFS.Disabled | FS_Operations.FilterVFS.ACL | FS_Operations.FilterVFS.Deleted).OrderBy(n => n.vNode.position).ThenBy(n => n.vNode.snode).OfType<FS_Operations.FS_NodeInfo_Interface>().ToList(); }
              catch { }
            }
            //
            config.RootsCMS_Paths = fsOp.PathsFromNodesAuthor(config.RootsCMS_fsNodes.Select(n => n.sNode).Distinct(), null).Where(p => config.RootsCMS_fsNodes.Any(n => n.sNode == p.sNode)).ToList();
            //
            ts.Committ(); // serve solo per non lasciare un transaction incompleta che incasinerebbe le transaction di livello superiore
          }
        }
        config.RootsAuthor_folders = config.RootsAuthor_fsNodes.Select(n => n.Folder).Distinct().ToList();
        config.RootsAuthor_sNodes = config.RootsAuthor_fsNodes.Select(n => n.sNode).Distinct().ToList();
        config.RootsVFS_folders = config.RootsVFS_fsNodes.Select(n => n.Folder).Distinct().ToList();
        config.RootsVFS_sNodes = config.RootsVFS_fsNodes.Select(n => n.sNode).Distinct().ToList();
        config.RootsCMS_folders = config.RootsCMS_fsNodes.Select(n => n.Folder).Distinct().ToList();
        config.RootsCMS_sNodes = config.RootsCMS_fsNodes.Select(n => n.sNode).Distinct().ToList();
        //
        return config;
        //
      }
      catch { return null; }
    }



    public class ConfigVFS
    {
      //
      // nodi con accessibilita' all'author, devono essere un superset di RootsVFS che e' un superset di RootsCMS
      public List<FS_Operations.FS_NodeInfo_Interface> RootsAuthor_fsNodes { get; set; }
      public List<int> RootsAuthor_folders { get; set; }
      public List<int> RootsAuthor_sNodes { get; set; }
      //
      // per verifica accessibilita' paths nei model, lucene ecc
      public List<FS_Operations.FS_NodeInfo_Interface> RootsVFS_fsNodes { get; set; }
      public List<int> RootsVFS_folders { get; set; }
      public List<int> RootsVFS_sNodes { get; set; }
      //
      // per l'albero di navigazione principale, modificare il generatore del menu' tree principale per gestire meglio la configurabilita'
      public List<FS_Operations.FS_NodeInfo_Interface> RootsCMS_fsNodes { get; set; }
      public List<int> RootsCMS_folders { get; set; }
      public List<int> RootsCMS_sNodes { get; set; }
      public List<IKGD_Path> RootsCMS_Paths { get; set; }
      //
      public IEnumerable<IKGD_Path> RootsCMS_PathsActive { get { return RootsCMS_Paths.FilterCustom(IKGD_Path_Helper.FilterByActive, IKGD_Path_Helper.FilterByLanguage, IKGD_Path_Helper.FilterByAreas); } }
      //
    }


  }


}
