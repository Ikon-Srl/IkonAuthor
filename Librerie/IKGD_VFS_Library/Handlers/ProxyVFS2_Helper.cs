/*
 * 
 * Intranet Ikon
 * 
 * Copyright (C) 2008 Ikon Srl
 * Tutti i Diritti Riservati. All Rights Reserved
 * 
 */


using System;
using System.Data;
using System.Configuration;
using System.Collections;
using System.Collections.Generic;
using System.Web;
using System.Web.Caching;
using System.Web.Security;
using System.Linq;
using System.Xml.Linq;
using System.Data.Linq;
using System.Data.Linq.Mapping;
using System.Linq.Expressions;
using System.Net;
using System.IO;
using System.Text;
using System.Transactions;
using System.Web.SessionState;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;  // assembly System.ServiceModel.Web
using System.Data.Common;
using System.Data.Sql;
using System.Data.SqlClient;
using System.Data.SqlTypes;
using LinqKit;

using Ikon;
using Ikon.GD;
using Ikon.Log;
using System.Diagnostics;



namespace Ikon.Handlers
{


  public static class ProxyVFS2_Helper
  {
    //
    private static object _lockCache = new object();
    //
    public static bool Enabled { get; private set; }
    public static bool EnableFallbackScanWithoutMSTREAM { get; private set; }
    public static bool EnableNewMSTREAM_Scan { get; private set; }
    //
    public static bool CachingProxyVFS_BrowserEnabled { get; private set; }
    public static int CachingProxyVFS_Browser { get; private set; }
    //
    public static bool CachingProxyVFS_DataEnabled { get; private set; }
    //
    public static int CachingFilesMaxBytes { get; private set; }
    public static int CachingFilesNodesDuration { get; private set; }
    //
    public static int CachingImagesLimit { get; private set; }
    public static int CachingImagesExpiry { get; private set; }
    //
    public static int CachingFilesLimit { get; private set; }
    public static int CachingFilesExpiry { get; private set; }
    //
    public static int ProxyVFS_TimeoutDB { get; private set; }
    public static int ProxyVFS_BufferingSizeDB { get; private set; }
    public static bool ProxyVFS_BufferingAutoFlushDB { get; private set; }
    //
    public static int CachingFilesDiskCacheMaxBytes { get; private set; }
    public static string ProxyVFS_SharePath_CachingVFS { get; private set; }
    public static string ShareServerCacheVFS { get; private set; }
    public static string ShareUserNameCacheVFS { get; private set; }
    public static string SharePasswordCacheVFS { get; private set; }
    //


    static ProxyVFS2_Helper()
    {
      //
      _lockCache = new object();
      //
      Enabled = Utility.TryParse<bool>(IKGD_Config.AppSettings["ProxyVFS2_Helper_Enabled"], true);
      EnableFallbackScanWithoutMSTREAM = Utility.TryParse<bool>(IKGD_Config.AppSettings["ProxyVFS2_EnableFallbackScanWithoutMSTREAM"], true);
      EnableNewMSTREAM_Scan = Utility.TryParse<bool>(IKGD_Config.AppSettings["ProxyVFS2_EnableNewMSTREAM_Scan"], true);
      //
      CachingProxyVFS_BrowserEnabled = Utility.TryParse<bool>(IKGD_Config.AppSettings["CachingProxyVFS_BrowserEnabled"], true);
      CachingProxyVFS_Browser = Utility.TryParse<int>(IKGD_Config.AppSettings["CachingProxyVFS_Browser"], 3600);
      //
      CachingProxyVFS_DataEnabled = Utility.TryParse<bool>(IKGD_Config.AppSettings["CachingProxyVFS_DataEnabled"], true);
      // valori globali che non possono essere superati dalle singole configurazioni per files e immagini
      CachingFilesMaxBytes = Utility.TryParse<int>(IKGD_Config.AppSettings["CachingFilesMaxBytes"], 128 * 1024);
      CachingFilesNodesDuration = Utility.TryParse<int>(IKGD_Config.AppSettings["CachingFilesNodesDuration"], 3600);
      // valori per il caching delle immagini che non possono superare il limite globale imposto
      CachingImagesLimit = Utility.TryParse<int>(IKGD_Config.AppSettings["CachingImagesLimit"], CachingFilesMaxBytes);
      CachingImagesExpiry = Utility.TryParse<int>(IKGD_Config.AppSettings["CachingImagesExpiry"], CachingFilesNodesDuration);
      // valori per il caching dei files che non possono superare il limite globale imposto
      CachingFilesLimit = Utility.TryParse<int>(IKGD_Config.AppSettings["CachingFilesLimit"], CachingFilesMaxBytes);
      CachingFilesExpiry = Utility.TryParse<int>(IKGD_Config.AppSettings["CachingFilesExpiry"], CachingFilesNodesDuration);
      //
      ProxyVFS_TimeoutDB = Utility.TryParse<int>(IKGD_Config.AppSettings["ProxyVFS_TimeoutDB"], 600);
      ProxyVFS_BufferingSizeDB = Utility.MaxAll(Utility.TryParse<int>(IKGD_Config.AppSettings["ProxyVFS_BufferingSizeDB"], 128 * 1024), CachingFilesMaxBytes, CachingFilesLimit, CachingImagesLimit);
      ProxyVFS_BufferingAutoFlushDB = Utility.TryParse<bool>(IKGD_Config.AppSettings["ProxyVFS_BufferingAutoFlushDB"], false);  // eventualmente aumentato fino ad includere i valori limite dei contenuti cachabili
      //
      CachingFilesDiskCacheMaxBytes = Utility.MinAll(Utility.TryParse<int>(IKGD_Config.AppSettings["CachingFilesDiskCacheMaxBytes"], CachingFilesMaxBytes), CachingFilesMaxBytes);
      //
      if (IKGD_Config.AppSettings["ShareServerCacheVFS"] != null)
      {
        ShareServerCacheVFS = IKGD_Config.AppSettings["ShareServerCacheVFS"];
        ShareUserNameCacheVFS = IKGD_Config.AppSettings["ShareUserNameCacheVFS"];
        SharePasswordCacheVFS = IKGD_Config.AppSettings["SharePasswordCacheVFS"];
      }
      else if (IKGD_Config.AppSettings["ShareServer"] != null)
      {
        ShareServerCacheVFS = IKGD_Config.AppSettings["ShareServer"];
        ShareUserNameCacheVFS = IKGD_Config.AppSettings["ShareUserName"];
        SharePasswordCacheVFS = IKGD_Config.AppSettings["SharePassword"];
      }
      //
      //ProxyVFS_SharePath_CachingVFS = IKGD_Config.AppSettings["SharePath_CachingVFS"].TrimSafe().NullIfEmpty();
      if (IKGD_Config.AppSettings["SharePath_CachingVFS"].IsNotEmpty())
      {
        ImpersonationHelpers.FilePathWorker(IKGD_Config.AppSettings["SharePath_CachingVFS"].TrimSafe(' ').TrimEnd('\\', '/').NullIfEmpty(), (path) =>
        {
          if (Directory.Exists(path))
          {
            ProxyVFS_SharePath_CachingVFS = path;
          }
        }, ShareServerCacheVFS, ShareUserNameCacheVFS, SharePasswordCacheVFS);
      }
      //
    }


    private static CachedNodesContainer GetCachedNodes(int? vfs_version)
    {
      try
      {
        vfs_version = vfs_version ?? FS_OperationsHelpers.VersionFrozenSession;
        string cacheKey = (vfs_version == 0) ? "ProxyVFS_CachedNodes0" : "ProxyVFS_CachedNodes1";
        if (vfs_version > 0)
        {
          return new CachedNodesContainer(vfs_version.Value);
        }
        lock (_lockCache)
        {
          CachedNodesContainer CachedNodes = (CachedNodesContainer)HttpRuntime.Cache[cacheKey];
          if (CachedNodes == null)
          {
            CachedNodes = new CachedNodesContainer(vfs_version.Value);
            var dependencies = FS_OperationsHelpers.GetCacheDependencyWrapper((vfs_version == 0) ? new string[] { "IKGD_FREEZED" } : new string[] { "IKGD_VNODE", "IKGD_VDATA", "IKGD_INODE", "IKGD_MSTREAM" });
            HttpRuntime.Cache.Insert(cacheKey, CachedNodes, dependencies, DateTime.Now.AddSeconds(CachingFilesNodesDuration), Cache.NoSlidingExpiration, CacheItemPriority.Low, null);
          }
          return CachedNodes;
        }
      }
      catch { }
      return new CachedNodesContainer(0);
    }


    private class CachedNodesContainer
    {
      public object _lock;
      public int VfsVersion { get; private set; }
      public SortedDictionary<int, NodeVFS_Resource> NodesBy_rNode { get; private set; }
      public SortedDictionary<int, NodeVFS_Resource> NodesBy_sNode { get; private set; }
      public SortedDictionary<string, NodeVFS_Resource> NodesBy_Path { get; private set; }

      public CachedNodesContainer(int VfsVersion)
      {
        _lock = new object();
        this.VfsVersion = VfsVersion;
        NodesBy_rNode = new SortedDictionary<int, NodeVFS_Resource>();
        NodesBy_sNode = new SortedDictionary<int, NodeVFS_Resource>();
        NodesBy_Path = new SortedDictionary<string, NodeVFS_Resource>(StringComparer.OrdinalIgnoreCase);
      }
    }


    private class NodeVFS_Resource
    {
      public int rNode { get; set; }
      public List<NodeVFS_Alias> Nodes { get; set; }
      public List<NodeVFS_Stream> Streams { get; set; }
      public string Area { get; set; }
      public DateTime LastModified { get; set; }
      //
      //public string ManagerType { get; set; }
      public DateTime? DateActivation { get; set; }
      public DateTime? DateExpiry { get; set; }
      //
    }

    private class NodeVFS_Alias
    {
      public int sNode { get; set; }
      public string Name { get; set; }
      public string Language { get; set; }
    }

    private class NodeVFS_Stream
    {
      public int Id { get; set; }
      public string Source { get; set; }
      public string Key { get; set; }
      public string Mime { get; set; }
      public string FileName { get; set; }
      public string ETag { get { return "IKGD_STREAM_" + Id.ToString(); } }
      public int Length { get; set; }
      public string LocalFileName { get; set; }
    }


    public class NodeVFS_Args
    {
      public int? rNode { get; set; }
      public int? sNode { get; set; }
      public string SourceKey { get; set; }
      public string Source { get; set; }
      public string Key { get; set; }
      public int? VersionFrozen { get; set; }
      public int? cacheDurationServer { get; set; }
      public int? cacheDurationBrowser { get; set; }
      public bool? forceDownload { get; set; }
      public string pathInfo { get; set; }
      public string defaultResource { get; set; }
    }


    public static bool ProcessStreamRequest(HttpContext context, NodeVFS_Args args)
    {
      if (args == null || (args.rNode == null && args.sNode == null && args.pathInfo.IsNullOrEmpty()))
      {
        throw new ResourceNotFoundException();
      }
      //
      NodeVFS_Resource nodeVfsResource = null;
      NodeVFS_Alias nodeVfsAlias = null;
      NodeVFS_Stream nodeVfsStream = null;
      //
      FS_Operations fsOp = null;
      ImpersonationHelpers.ImpersonationWorker impersonationWorker = null;
      FileStream fstreamOut = null;
      //
      string mimeType = null;
      string cacheKey = null;
      bool isExternalStorage = false;
      //
      try
      {
        if (context == null)
        {
          context = System.Web.HttpContext.Current;
        }
        CachedNodesContainer CachedNodes = GetCachedNodes(args.VersionFrozen);
        if (args.sNode != null && CachedNodes.NodesBy_sNode.ContainsKey(args.sNode.Value))
        {
          nodeVfsResource = CachedNodes.NodesBy_sNode[args.sNode.Value];
          nodeVfsAlias = nodeVfsResource.Nodes.FirstOrDefault(n => n.sNode == args.sNode.Value);
        }
        else if (args.rNode != null && CachedNodes.NodesBy_rNode.ContainsKey(args.rNode.Value))
        {
          nodeVfsResource = CachedNodes.NodesBy_rNode[args.rNode.Value];
        }
        else if (args.pathInfo.IsNotEmpty() && CachedNodes.NodesBy_Path.ContainsKey(args.pathInfo))
        {
          nodeVfsResource = CachedNodes.NodesBy_Path[args.pathInfo];
        }
        //
        // se non vengono trovate in cache le informazioni necessarie popoliamo il cacheSet
        //
        bool IsTainted = false;
        if (nodeVfsResource == null)
        {
          IsTainted = true;
          EnsureFsOp(ref fsOp, args);
          fsOp.DB.ObjectTrackingEnabled = false;
          int freeze = args.VersionFrozen.GetValueOrDefault(fsOp.VersionFrozen);
          List<NodeVFS_AuxDB> vfs_data = null;
          var invalidSources = new List<string> { "Lucene" };
          if (args.rNode == null && args.sNode == null && args.pathInfo.IsNotEmpty())
          {
            try
            {
              var paths = fsOp.PathsFromString(args.pathInfo);
              var path = paths.FirstOrDefault();
              args.rNode = path.rNode;
              args.sNode = path.sNode;
            }
            catch { }
          }
          //
          if (args.rNode != null)
          {
            if (EnableNewMSTREAM_Scan)
            {
              vfs_data =
                (from vNode in fsOp.NodesActive<IKGD_VNODE>(freeze == -1).Where(n => n.rnode == args.rNode.Value)
                 join vData in fsOp.NodesActive<IKGD_VDATA>(freeze == -1) on vNode.rnode equals vData.rnode
                 join iNode in fsOp.NodesActive<IKGD_INODE>(freeze == -1) on vNode.rnode equals iNode.rnode
                 from mstream in fsOp.DB.IKGD_MSTREAMs.Where(r => r.inode == iNode.version).DefaultIfEmpty()
                 from stream in fsOp.DB.IKGD_STREAMs.Where(r => r.source == null || !invalidSources.Contains(r.source)).Where(r => r.inode == iNode.version || (mstream != null && mstream.stream == r.id))
                 select new NodeVFS_AuxDB
                 {
                   snode = vNode.snode,
                   rnode = vNode.rnode,
                   name = vNode.name,
                   language = vNode.language,
                   area = vData.area,
                   date_activation = vData.date_activation,
                   date_expiry = vData.date_expiry,
                   streamId = stream.id,
                   source = stream.source,
                   key = stream.key,
                   type = stream.type,
                   filename = iNode.filename,
                   //date_vNode = vNode.version_date,
                   date_vData = vData.version_date,
                   date_iNode = iNode.version_date
                 }).Distinct().ToList();
            }
            else
            {
              //
              // query non efficiente
              // bisogna eliminare .Any che crea un bel casino, e gestire dei cover index corretti
              // l'ottimizzatore non segnala index mancanti ma il plan non e' ottimale, convertirlo in un outer join?
              vfs_data =
                (from vNode in fsOp.NodesActive<IKGD_VNODE>(freeze == -1).Where(n => n.rnode == args.rNode.Value)
                 join vData in fsOp.NodesActive<IKGD_VDATA>(freeze == -1) on vNode.rnode equals vData.rnode
                 join iNode in fsOp.NodesActive<IKGD_INODE>(freeze == -1) on vNode.rnode equals iNode.rnode
                 from stream in fsOp.DB.IKGD_STREAMs.Where(r => r.source == null || !invalidSources.Contains(r.source)).Where(r => r.inode == iNode.version || iNode.IKGD_MSTREAMs.Any(m => m.stream == r.id))
                 //join mstream in fsOp.DB.IKGD_MSTREAMs on iNode.version equals mstream.inode
                 //join stream in fsOp.DB.IKGD_STREAMs.Where(r => r.source == null || !invalidSources.Contains(r.source)) on mstream.stream equals stream.id
                 select new NodeVFS_AuxDB
                 {
                   snode = vNode.snode,
                   rnode = vNode.rnode,
                   name = vNode.name,
                   language = vNode.language,
                   area = vData.area,
                   date_activation = vData.date_activation,
                   date_expiry = vData.date_expiry,
                   streamId = stream.id,
                   source = stream.source,
                   key = stream.key,
                   type = stream.type,
                   filename = iNode.filename,
                   //date_vNode = vNode.version_date,
                   date_vData = vData.version_date,
                   date_iNode = iNode.version_date
                 }).ToList();
            }
            //
            // attenzione che le news con gli allegati gestiti alla vecchia maniera non vengono associati a IKGD_MSTREAM, dobbiamo rifare la query senza uno dei join
            //if (EnableFallbackScanWithoutMSTREAM && !vfs_data.Any())
            //{
            //  vfs_data =
            //    (from vNode in fsOp.NodesActive<IKGD_VNODE>(freeze == -1).Where(n => n.rnode == args.rNode.Value)
            //     join vData in fsOp.NodesActive<IKGD_VDATA>(freeze == -1) on vNode.rnode equals vData.rnode
            //     join iNode in fsOp.NodesActive<IKGD_INODE>(freeze == -1) on vNode.rnode equals iNode.rnode
            //     join stream in fsOp.DB.IKGD_STREAMs.Where(r => r.source == null || !invalidSources.Contains(r.source)) on iNode.version equals stream.inode
            //     select new NodeVFS_AuxDB
            //     {
            //       snode = vNode.snode,
            //       rnode = vNode.rnode,
            //       name = vNode.name,
            //       language = vNode.language,
            //       area = vData.area,
            //       date_activation = vData.date_activation,
            //       date_expiry = vData.date_expiry,
            //       streamId = stream.id,
            //       source = stream.source,
            //       key = stream.key,
            //       type = stream.type,
            //       filename = iNode.filename,
            //       //date_vNode = vNode.version_date,
            //       date_vData = vData.version_date,
            //       date_iNode = iNode.version_date
            //     }).GroupBy(r => r.streamId).Select(g => g.OrderByDescending(r => r.streamId).FirstOrDefault()).ToList();
            //}
          }
          else if (args.sNode != null)
          {
            if (EnableNewMSTREAM_Scan)
            {
              vfs_data =
                (from vNodePre in fsOp.NodesActive<IKGD_VNODE>(freeze == -1).Where(n => n.snode == args.sNode.Value)
                 join vNode in fsOp.NodesActive<IKGD_VNODE>(freeze == -1) on vNodePre.rnode equals vNode.rnode
                 join vData in fsOp.NodesActive<IKGD_VDATA>(freeze == -1) on vNode.rnode equals vData.rnode
                 join iNode in fsOp.NodesActive<IKGD_INODE>(freeze == -1) on vNode.rnode equals iNode.rnode
                 from mstream in fsOp.DB.IKGD_MSTREAMs.Where(r => r.inode == iNode.version).DefaultIfEmpty()
                 from stream in fsOp.DB.IKGD_STREAMs.Where(r => r.source == null || !invalidSources.Contains(r.source)).Where(r => r.inode == iNode.version || (mstream != null && mstream.stream == r.id))
                 select new NodeVFS_AuxDB
                 {
                   snode = vNode.snode,
                   rnode = vNode.rnode,
                   name = vNode.name,
                   language = vNode.language,
                   area = vData.area,
                   date_activation = vData.date_activation,
                   date_expiry = vData.date_expiry,
                   streamId = stream.id,
                   source = stream.source,
                   key = stream.key,
                   type = stream.type,
                   filename = iNode.filename,
                   //date_vNode = vNode.version_date,
                   date_vData = vData.version_date,
                   date_iNode = iNode.version_date
                 }).Distinct().ToList();
            }
            else
            {
              vfs_data =
                (from vNodePre in fsOp.NodesActive<IKGD_VNODE>(freeze == -1).Where(n => n.snode == args.sNode.Value)
                 join vNode in fsOp.NodesActive<IKGD_VNODE>(freeze == -1) on vNodePre.rnode equals vNode.rnode
                 join vData in fsOp.NodesActive<IKGD_VDATA>(freeze == -1) on vNode.rnode equals vData.rnode
                 join iNode in fsOp.NodesActive<IKGD_INODE>(freeze == -1) on vNode.rnode equals iNode.rnode
                 from stream in fsOp.DB.IKGD_STREAMs.Where(r => r.source == null || !invalidSources.Contains(r.source)).Where(r => r.inode == iNode.version || iNode.IKGD_MSTREAMs.Any(m => m.stream == r.id))
                 //join mstream in fsOp.DB.IKGD_MSTREAMs on iNode.version equals mstream.inode
                 //join stream in fsOp.DB.IKGD_STREAMs.Where(r => r.source == null || !invalidSources.Contains(r.source)) on mstream.stream equals stream.id
                 select new NodeVFS_AuxDB
                 {
                   snode = vNode.snode,
                   rnode = vNode.rnode,
                   name = vNode.name,
                   language = vNode.language,
                   area = vData.area,
                   date_activation = vData.date_activation,
                   date_expiry = vData.date_expiry,
                   streamId = stream.id,
                   source = stream.source,
                   key = stream.key,
                   type = stream.type,
                   filename = iNode.filename,
                   //date_vNode = vNode.version_date,
                   date_vData = vData.version_date,
                   date_iNode = iNode.version_date
                 }).ToList();
            }
            //
            // attenzione che le news con gli allegati gestiti alla vecchia maniera non vengono associati a IKGD_MSTREAM, dobbiamo rifare la query senza uno dei join
            //if (EnableFallbackScanWithoutMSTREAM && !vfs_data.Any())
            //{
            //  vfs_data =
            //    (from vNodePre in fsOp.NodesActive<IKGD_VNODE>(freeze == -1).Where(n => n.snode == args.sNode.Value)
            //     join vNode in fsOp.NodesActive<IKGD_VNODE>(freeze == -1) on vNodePre.rnode equals vNode.rnode
            //     join vData in fsOp.NodesActive<IKGD_VDATA>(freeze == -1) on vNode.rnode equals vData.rnode
            //     join iNode in fsOp.NodesActive<IKGD_INODE>(freeze == -1) on vNode.rnode equals iNode.rnode
            //     join stream in fsOp.DB.IKGD_STREAMs.Where(r => r.source == null || !invalidSources.Contains(r.source)) on iNode.version equals stream.inode
            //     select new NodeVFS_AuxDB
            //     {
            //       snode = vNode.snode,
            //       rnode = vNode.rnode,
            //       name = vNode.name,
            //       language = vNode.language,
            //       area = vData.area,
            //       date_activation = vData.date_activation,
            //       date_expiry = vData.date_expiry,
            //       streamId = stream.id,
            //       source = stream.source,
            //       key = stream.key,
            //       type = stream.type,
            //       filename = iNode.filename,
            //       //date_vNode = vNode.version_date,
            //       date_vData = vData.version_date,
            //       date_iNode = iNode.version_date
            //     }).GroupBy(r => r.streamId).Select(g => g.OrderByDescending(r => r.streamId).FirstOrDefault()).ToList();
            //}
          }
          for (int fallbacks = 0; fallbacks <= 1; fallbacks++)
          {
            if (vfs_data != null && vfs_data.Any())
            {
              foreach (var grp_snode in vfs_data.GroupBy(r => r.snode))
              {
                var record = grp_snode.FirstOrDefault();
                if (nodeVfsResource == null)
                {
                  nodeVfsResource = new NodeVFS_Resource()
                  {
                    rNode = record.rnode,
                    Area = record.area,
                    Nodes = new List<NodeVFS_Alias>(),
                    Streams = new List<NodeVFS_Stream>(),
                    DateActivation = record.date_activation,
                    DateExpiry = record.date_expiry,
                    LastModified = Utility.MaxAll(record.date_vData, record.date_iNode)
                  };
                }
                NodeVFS_Alias vNode = new NodeVFS_Alias() { sNode = record.snode, Name = record.name, Language = record.language };
                nodeVfsResource.Nodes.Add(vNode);
              }
              if (nodeVfsResource != null)
              {
                vfs_data.Where(r => r.streamId > 0).Distinct((r1, r2) => r1.streamId == r2.streamId).ForEach(r => nodeVfsResource.Streams.Add(new NodeVFS_Stream
                {
                  Id = r.streamId,
                  Source = r.source,
                  Key = r.key,
                  Mime = r.type,
                  FileName = r.filename
                }));
              }
            }
            //
            // abbiamo letto una risorsa valida oppure abbiamo terminato le chance
            if (nodeVfsResource != null || fallbacks > 0)
            {
              break;  // dati completi: usciamo dal loop
            }
            else
            {
              //
              // probabilmente la risorsa e' priva di streams, la rileggiamo con i soli sNode e rNode data per il caching
              if (args.rNode != null)
              {
                vfs_data =
                  (from vNode in fsOp.NodesActive<IKGD_VNODE>(freeze == -1).Where(n => n.rnode == args.rNode.Value)
                   join vData in fsOp.NodesActive<IKGD_VDATA>(freeze == -1) on vNode.rnode equals vData.rnode
                   select new NodeVFS_AuxDB
                   {
                     snode = vNode.snode,
                     rnode = vNode.rnode,
                     name = vNode.name,
                     language = vNode.language,
                     area = vData.area,
                     date_activation = vData.date_activation,
                     date_expiry = vData.date_expiry,
                     date_vData = vData.version_date,
                     date_iNode = DateTime.MinValue
                   }).ToList();
              }
              else if (args.sNode != null)
              {
                vfs_data =
                  (from vNodePre in fsOp.NodesActive<IKGD_VNODE>(freeze == -1).Where(n => n.snode == args.sNode.Value)
                   join vNode in fsOp.NodesActive<IKGD_VNODE>(freeze == -1) on vNodePre.rnode equals vNode.rnode
                   join vData in fsOp.NodesActive<IKGD_VDATA>(freeze == -1) on vNode.rnode equals vData.rnode
                   select new NodeVFS_AuxDB
                   {
                     snode = vNode.snode,
                     rnode = vNode.rnode,
                     name = vNode.name,
                     language = vNode.language,
                     area = vData.area,
                     date_activation = vData.date_activation,
                     date_expiry = vData.date_expiry,
                     date_vData = vData.version_date,
                     date_iNode = DateTime.MinValue
                   }).ToList();
              }
              //
            }
          }
        }
        //
        // se nodeVfsResource == null
        // devo creare un record fittizio per evitare di fare una connessione per ogni richiesta con dati non validi
        if (nodeVfsResource == null)
        {
          nodeVfsResource = new NodeVFS_Resource()
          {
            rNode = args.rNode.GetValueOrDefault(-1),
            Area = string.Empty,
            Nodes = new List<NodeVFS_Alias>() { new NodeVFS_Alias() { sNode = args.sNode.Value, Name = args.sNode.ToString(), Language = null } },
            Streams = new List<NodeVFS_Stream>()
          };
        }
        //
        if (nodeVfsAlias == null && args.sNode != null)
        {
          nodeVfsAlias = nodeVfsResource.Nodes.FirstOrDefault(r => r.sNode == args.sNode.Value);
        }
        //
        if (nodeVfsAlias == null)
        {
          string lang = FS_OperationsHelpers.LanguageSession;
          nodeVfsAlias = nodeVfsResource.Nodes.FirstOrDefault(r => r.Language.IsNullOrEmpty() || string.Equals(r.Language, lang, StringComparison.OrdinalIgnoreCase));
        }
        //
        if (nodeVfsAlias == null)
        {
          nodeVfsAlias = nodeVfsResource.Nodes.FirstOrDefault();
        }
        //
        if (IsTainted)
        {
          // valutare se sia necessario utilizzare il lock a livello globale per tutto il processo di lettura delle info
          lock (CachedNodes._lock)
          {
            //
            // aggiornamento della cache dei nodi
            //
            if (nodeVfsResource.rNode >= 0)
            {
              if (CachedNodes.NodesBy_rNode.ContainsKey(nodeVfsResource.rNode))
              {
                // pulizia dei vecchi contenuti
                var nodeOld = CachedNodes.NodesBy_rNode[nodeVfsResource.rNode];
                if (nodeOld != null)
                {
                  CachedNodes.NodesBy_rNode.Remove(nodeVfsResource.rNode);
                  nodeOld.Nodes.Where(r => r.sNode >= 0 && CachedNodes.NodesBy_sNode.ContainsKey(r.sNode)).ForEach(r => CachedNodes.NodesBy_sNode.Remove(r.sNode));
                }
              }
              CachedNodes.NodesBy_rNode[nodeVfsResource.rNode] = nodeVfsResource;
            }
            //
            nodeVfsResource.Nodes.Where(r => r.sNode >= 0 && CachedNodes.NodesBy_sNode.ContainsKey(r.sNode)).ForEach(r => CachedNodes.NodesBy_sNode.Remove(r.sNode));
            nodeVfsResource.Nodes.Where(r => r.sNode >= 0).ForEach(r => CachedNodes.NodesBy_sNode[r.sNode] = nodeVfsResource);
            //
            if (args.pathInfo.IsNotEmpty())
            {
              CachedNodes.NodesBy_Path[args.pathInfo] = nodeVfsResource;
            }
            //
          }
        }
        //
        if (nodeVfsResource == null || nodeVfsAlias == null)
        {
          throw new ResourceNotFoundException();
        }
        //
        // selezione dello stream corretto
        //
        // source puo' essere NULL
        // key non e' mai NULL
        string stream_key = args.Key ?? string.Empty;
        string stream_source = args.Source.NullIfEmpty();
        if (stream_source.IsNullOrEmpty() && stream_key.IsNullOrEmpty())
        {
          var frags = (args.SourceKey ?? string.Empty).Split("|,".ToCharArray(), 2);
          if (frags.Length < 2)
          {
            stream_source = null;
            stream_key = frags.FirstOrDefault() ?? string.Empty;
          }
          else
          {
            stream_source = frags.FirstOrDefault().NullIfEmpty();
            stream_key = frags.Skip(1).FirstOrDefault() ?? string.Empty;
          }
        }
        nodeVfsStream = nodeVfsResource.Streams.FirstOrDefault(r => string.Equals(r.Source, stream_source, StringComparison.OrdinalIgnoreCase) && string.Equals(r.Key, stream_key, StringComparison.OrdinalIgnoreCase));
        if (nodeVfsStream == null)
        {
          nodeVfsStream = nodeVfsResource.Streams.FirstOrDefault(r => string.Equals(r.Key, stream_key, StringComparison.OrdinalIgnoreCase));
        }
        if (nodeVfsStream == null)
        {
          nodeVfsStream = nodeVfsResource.Streams.FirstOrDefault();
        }
        //
        if (nodeVfsStream == null)
        {
          throw new ResourceNotFoundException();
        }
        //
        // controllo delle date di validita' della risorsa associate allo stream
        //
        if (FS_OperationsHelpers.DateTimeSession < nodeVfsResource.DateActivation.GetValueOrDefault(DateTime.MinValue) || FS_OperationsHelpers.DateTimeSession > nodeVfsResource.DateExpiry.GetValueOrDefault(DateTime.MaxValue))
        {
          if (Utility.TryParse<bool>(IKGD_Config.AppSettings["ProxyVFS_EnableDateActivation"], true) && FS_OperationsHelpers.DateTimeSession < nodeVfsResource.DateActivation.GetValueOrDefault(DateTime.MinValue))
            throw new ResourceNotFoundException();
          if (Utility.TryParse<bool>(IKGD_Config.AppSettings["ProxyVFS_EnableDateExpiry"], true) && FS_OperationsHelpers.DateTimeSession > nodeVfsResource.DateExpiry.GetValueOrDefault(DateTime.MaxValue))
            throw new ResourceNotFoundException();
        }
        //
        context.Response.ClearContent();
        context.Response.ClearHeaders();
        //
        // controllo se lo stream e' gia' in cache nel browser
        //
        try
        {
          // test per ETag e caching sul client
          bool cachedOnClient = !string.IsNullOrEmpty(context.Request.Headers["If-Modified-Since"]) || !string.IsNullOrEmpty(context.Request.Headers["If-None-Match"]);
          if (cachedOnClient && !string.IsNullOrEmpty(context.Request.Headers["If-None-Match"]))
            cachedOnClient &= context.Request.Headers["If-None-Match"] == nodeVfsStream.ETag;
          if (cachedOnClient && !string.IsNullOrEmpty(context.Request.Headers["If-Modified-Since"]))
          {
            DateTime IfModifiedSince = DateTime.Parse(context.Request.Headers["If-Modified-Since"]);
            cachedOnClient &= IfModifiedSince >= nodeVfsResource.LastModified;
          }
          if (cachedOnClient)
          {
            context.Response.Status = "304 Not Modified";
            context.Response.StatusCode = 304;
            //context.Response.End();
            //context.ApplicationInstance.CompleteRequest();  // da usare al posto di .Response.End();
            return true;
          }
        }
        catch { }
        //
        // controllo ACL
        //
        if (!ProxyVFS_Helper.CheckResourceACL(context, nodeVfsResource.Area, true))
        {
          throw new ResourceNoAclException();
        }
        //
        // configurazione degli headers
        //
        mimeType = nodeVfsStream.Mime;
        if (IKGD_ExternalVFS_Support.IsExternalFileFromMime(mimeType))
        {
          // rinormalizzare del mimetype senza il prefix! e gestione del file esterno
          mimeType = IKGD_ExternalVFS_Support.GetMimeType(mimeType);
          isExternalStorage = true;
        }
        //
        // normalizzazione dei mime type non supportati correttamente da explorer
        //
        if (mimeType == "application/x-pdf")
          mimeType = "application/pdf";
        if (!string.IsNullOrEmpty(mimeType))
          context.Response.ContentType = mimeType;
        //
        // per i mime type non riconosciuti genera un header di download
        //
        string fileName = nodeVfsStream.FileName ?? string.Empty;
        try
        {
          //string pInfo = HttpContext.Current.Request.PathInfo;
          string pInfo = args.pathInfo;
          if (!string.IsNullOrEmpty(pInfo))
          {
            string extraPathInfo = pInfo;
            extraPathInfo = Utility.PathGetFileNameSanitized(extraPathInfo);
            if (extraPathInfo.IndexOfAny(Path.GetInvalidFileNameChars()) < 0 && extraPathInfo.IndexOfAny(Path.GetInvalidPathChars()) < 0)
            {
              fileName = extraPathInfo;
            }
          }
          else
          {
            try
            {
              fileName = Utility.PathGetFileNameSanitized(fileName);
              if (!Ikon.Mime.MimeExtensionHelper.FindExtensionsWithPoint(mimeType).Any(fe => fileName.EndsWith(fe, StringComparison.OrdinalIgnoreCase)))
              {
                string fileExt = Ikon.Mime.MimeExtensionHelper.FindExtensionWithPoint(mimeType);
                if (fileExt.NullIfEmpty(".bin").IsNotEmpty())
                  fileName = Utility.PathGetFileNameSanitized(fileName.Trim(' ', '.') + (fileExt ?? string.Empty));
              }
            }
            catch { }
          }
        }
        catch { }
        fileName = fileName ?? string.Empty;
        string inlineMode = "inline";
        if (!string.IsNullOrEmpty(fileName) && (args.forceDownload.GetValueOrDefault(false) || string.IsNullOrEmpty(mimeType) || mimeType == "application/octet-stream"))
          inlineMode = "attachment";
        context.Response.AppendHeader("Content-Disposition", string.Format("{0}; filename=\"{1}\"", inlineMode, fileName));
        //
        if (!context.Response.IsClientConnected)
        {
          return false;
        }
        //
        // configurazione degli headers per il caching
        //
        int? cacheDurationServer = null;
        int? cacheDurationBrowser = null;
        int? cacheDurationServerOverridden = null;
        if (CachingProxyVFS_DataEnabled)
        {
          cacheDurationServerOverridden = Utility.TryParse<int?>(context.Request["cacheServer"]);
          cacheDurationServer = cacheDurationServerOverridden ?? CachingFilesExpiry;
        }
        if (CachingProxyVFS_BrowserEnabled)
        {
          cacheDurationBrowser = Utility.TryParse<int?>(context.Request["cacheBrowser"], CachingProxyVFS_Browser);
        }
        //
        if (cacheDurationBrowser > 0)
        {
          HttpHelper.CacheResponse(context, cacheDurationBrowser.Value, nodeVfsStream.ETag, nodeVfsResource.LastModified);
        }
        else
        {
          HttpHelper.DoNotCacheResponse(context, nodeVfsStream.ETag);
        }
        //
        // verifica se lo stream ricercato e' gia' in cache
        //
        if (CachingProxyVFS_DataEnabled)
        {
          cacheKey = string.Format("IKGD_STREAM_Data_{0}", nodeVfsStream.Id);
          byte[] cachedStream = (byte[])HttpRuntime.Cache[cacheKey];
          if (cachedStream != null)
          {
            context.Response.AppendHeader("Content-Length", cachedStream.Length.ToString());
            context.Response.BinaryWrite(cachedStream);
            return true;
          }
        }
        //
        if (isExternalStorage)
        {
          if (nodeVfsStream.LocalFileName == null)
          {
            // salviamo il LocalFileName in cache per ottimizzare gli accessi
            string LocalFileName = null;
            try
            {
              EnsureFsOp(ref fsOp, args);
              var data = fsOp.DB.IKGD_STREAMs.Where(r => r.id == nodeVfsStream.Id).Select(r => r.data).FirstOrDefault();
              if (data != null)
              {
                LocalFileName = Utility.LinqBinaryGetStringDB(data.ToArray());
              }
            }
            catch { }
            lock (CachedNodes._lock)
            {
              nodeVfsStream.LocalFileName = LocalFileName ?? string.Empty;
            }
          }
          if (nodeVfsStream.LocalFileName.IsNullOrEmpty())
          {
            throw new ResourceNotFoundException();
          }
          using (IKGD_ExternalVFS_Support extFS = new IKGD_ExternalVFS_Support())
          {
            return extFS.DownloadExternalStream(context.Response, nodeVfsStream.LocalFileName);
          }
        }
        //
        //context.Response.AppendHeader("Accept-Ranges", "bytes");  // e' il colpevole dei problemi che incontriamo con i files .pdf
        //
        // streaming da da cached VFS
        //
        if (ProxyVFS_SharePath_CachingVFS != null)
        {
          string cachedPath = string.Format(@"{0}\{1}", ProxyVFS_SharePath_CachingVFS, nodeVfsStream.ETag);
          impersonationWorker = ImpersonationHelpers.ImpersonationWorker.Factory(cachedPath, ShareServerCacheVFS, ShareUserNameCacheVFS, SharePasswordCacheVFS);
          if (impersonationWorker.FilePath != null)
          {
            try
            {
              if (System.IO.File.Exists(impersonationWorker.FilePath))
              {
                //
                // to avoid locking on resource
                using (FileStream fstream = new FileStream(impersonationWorker.FilePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                {
                  context.Response.AppendHeader("Content-Length", fstream.Length.ToString());
                  byte[] outbuffer = new byte[ProxyVFS_BufferingSizeDB];
                  long startIndex = 0;
                  long retval;
                  do
                  {
                    retval = fstream.Read(outbuffer, 0, ProxyVFS_BufferingSizeDB);
                    startIndex += retval;
                    context.Response.OutputStream.Write(outbuffer, 0, (int)retval);
                    if (ProxyVFS_BufferingAutoFlushDB)
                    {
                      context.Response.Flush();
                    }
                  } while (retval == ProxyVFS_BufferingSizeDB);
                  //
                  // se la lettura dei dati è avvenuta in un solo blocco allora possiamo passare la richiesta al cache management
                  //
                  if (startIndex <= CachingFilesDiskCacheMaxBytes && CachingProxyVFS_DataEnabled)
                  {
                    CachingBufferWorker(outbuffer, (int)startIndex, cacheKey, mimeType, cacheDurationServer, cacheDurationServerOverridden);
                  }
                  return true;
                }
              }
            }
            catch { }
          }
          // se arrivo a questo punto non ho trovato niente nella cache su CachedVFS
        }
        //
        // streaming da database
        //
        {
          EnsureFsOp(ref fsOp, args);
          SqlCommand sqlCmd = null;
          sqlCmd = new SqlCommand("SELECT TOP 1 [data] FROM [IKGD_STREAM] WITH(NOLOCK) WHERE ([id]=@id)", fsOp.DB.Connection as SqlConnection);
          sqlCmd.Parameters.Add("@id", SqlDbType.Int).Value = nodeVfsStream.Id;
          sqlCmd.CommandTimeout = ProxyVFS_TimeoutDB;
          if (fsOp.DB.Connection.State == ConnectionState.Closed)
            fsOp.DB.Connection.Open();
          using (SqlDataReader reader = sqlCmd.ExecuteReader(CommandBehavior.SingleRow | CommandBehavior.SequentialAccess | CommandBehavior.CloseConnection))
          {
            if (reader.Read())
            {
              if (impersonationWorker != null && impersonationWorker.FilePath != null)
              {
                //
                // creazione del file su CachedVFS
                // lo creiamo in modo tale che gli altri processi non possano accedervi finche' non viene terminata la scrittura
                try { fstreamOut = new FileStream(impersonationWorker.FilePath, FileMode.CreateNew, FileAccess.Write, FileShare.Delete); }
                catch { }
              }
              byte[] outbuffer = new byte[ProxyVFS_BufferingSizeDB];
              long startIndex = 0;
              long retval;
              do
              {
                retval = reader.GetBytes(0, startIndex, outbuffer, 0, ProxyVFS_BufferingSizeDB);
                startIndex += retval;
                context.Response.OutputStream.Write(outbuffer, 0, (int)retval);
                if (fstreamOut != null)
                {
                  // salvataggio dello stream anche sul CachedVFS
                  try { fstreamOut.Write(outbuffer, 0, (int)retval); }
                  catch { }
                }
                if (ProxyVFS_BufferingAutoFlushDB)
                {
                  context.Response.Flush();
                }
              } while (retval == ProxyVFS_BufferingSizeDB);
              reader.Close();
              // chiusura dello stream sul CachedVFS
              if (fstreamOut != null)
              {
                try
                {
                  fstreamOut.Close();
                  fstreamOut.Dispose();
                  fstreamOut = null;
                }
                catch { }
              }
              //
              // se la lettura dei dati è avvenuta in un solo blocco allora possiamo passare la richiesta al cache management
              //
              if (startIndex <= ProxyVFS_BufferingSizeDB && CachingProxyVFS_DataEnabled)
              {
                CachingBufferWorker(outbuffer, (int)startIndex, cacheKey, mimeType, cacheDurationServer, cacheDurationServerOverridden);
              }
              //
            }
            else
            {
              throw new ResourceNotFoundException();
            }
          }  //reader
          return true;
        }
      }
      catch (ResourceNoAclException)
      {
        context.Response.Clear();
        ProxyVFS_Helper.ForceAuthRequest(context);
        //throw new Exception("Credenziali di accesso insufficienti per accedere alla risorsa richiesta dalla cache. {0}".FormatString(DateTime.Now));
        return false;
      }
      catch (ResourceNotFoundException)
      {
        context.Response.Clear();
        if (args != null && args.defaultResource.IsNotNullOrWhiteSpace())
        {
          try
          {
            string defaultResourceFileName = Utility.vPathMap(args.defaultResource.TrimSafe());
            if (mimeType.IsNullOrEmpty() && nodeVfsStream != null)
              mimeType = nodeVfsStream.Mime;
            if (!string.IsNullOrEmpty(mimeType))
              context.Response.ContentType = mimeType;
            if (context.Response.IsClientConnected)
            {
              context.Response.WriteFile(defaultResourceFileName);
              return true;   // abbiamo comunque ritornato qualcosa...
            }
          }
          catch { }
        }
        return false;
      }
      catch
      {
        return false;
      }
      finally
      {
        if (fsOp != null)
        {
          try { fsOp.Dispose(); }
          catch { }
          fsOp = null;
        }
        if (fstreamOut != null)
        {
          try
          {
            fstreamOut.Close();
            fstreamOut.Dispose();
            fstreamOut = null;
            try { System.IO.File.Delete(impersonationWorker.FilePath); }
            catch { }
          }
          catch { }
        }
        if (impersonationWorker != null)
        {
          impersonationWorker.Undo();
        }
        try
        {
          if (context.Response.IsClientConnected && ProxyVFS_BufferingAutoFlushDB)
          {
            context.Response.Flush();
          }
        }
        catch { }
      }
    }


    private static FS_Operations EnsureFsOp(ref FS_Operations fsOp, NodeVFS_Args args)
    {
      if (fsOp == null)
      {
        fsOp = new FS_Operations(args.VersionFrozen);
      }
      return fsOp;
    }


    internal class ResourceNotFoundException : Exception { }

    internal class ResourceNoAclException : Exception { }


    internal static bool CachingBufferWorker(byte[] buffer, int dataLength, string cacheKey, string mimeType, int? cacheDurationServer, int? cacheDurationServerOverridden)
    {
      try
      {
        if (cacheDurationServer != null && cacheKey.IsNotEmpty())
        {
          int? cacheDurationOnServer = cacheDurationServer.Value;
          //
          // verificahe ulteriori in funzione della tipologia del contenuto
          //
          if ((mimeType ?? string.Empty).StartsWith("image/", StringComparison.OrdinalIgnoreCase))
          {
            cacheDurationOnServer = (dataLength <= CachingImagesLimit) ? (int?)(cacheDurationServerOverridden ?? CachingImagesExpiry) : null;
          }
          else
          {
            cacheDurationOnServer = (dataLength <= CachingFilesLimit) ? (int?)(cacheDurationServerOverridden ?? CachingFilesExpiry) : null;
          }
          if (cacheDurationOnServer != null)
          {
            byte[] data = new byte[dataLength];
            Array.Copy(buffer, data, dataLength);
            AggregateCacheDependency cacheDeps = null;
            //cacheDeps = new AggregateCacheDependency();
            //cacheDeps.Add(new SqlCacheDependency("GDCS", "IKGD_INODE"));
            //HttpRuntime.Cache.Remove(cacheKey);
            HttpRuntime.Cache.Insert(cacheKey, data, cacheDeps, DateTime.Now.AddSeconds(cacheDurationOnServer.Value), Cache.NoSlidingExpiration, CacheItemPriority.Low, null);
            return true;
          }
        }
      }
      catch { }
      return false;
    }


    public static long ClearDiskCache(int? maxAgeSeconds, DateTime? minDate, int? maxArchiveSizeMB)
    {
      int counter = -1;
      int countOrig = 0;
      long deletedBytes = 0;
      ImpersonationHelpers.ImpersonationWorker impersonationWorker = null;
      if (ProxyVFS_SharePath_CachingVFS != null)
      {
        try
        {
          impersonationWorker = ImpersonationHelpers.ImpersonationWorker.Factory(ProxyVFS_SharePath_CachingVFS, ShareServerCacheVFS, ShareUserNameCacheVFS, SharePasswordCacheVFS);
          if (impersonationWorker.FilePath != null && Directory.Exists(impersonationWorker.FilePath))
          {
            var dirInfo = new DirectoryInfo(impersonationWorker.FilePath);
            var fileList = dirInfo.GetFiles().ToList();
            countOrig = fileList.Count;
            //
            maxAgeSeconds = maxAgeSeconds ?? Utility.TryParse<int?>(IKGD_Config.AppSettings["CachingFilesDiskCache_MaxAgeSeconds"]);
            maxArchiveSizeMB = maxArchiveSizeMB ?? Utility.TryParse<int?>(IKGD_Config.AppSettings["CachingFilesDiskCache_MaxGlobalSizeMB"]);
            //
            if (maxAgeSeconds != null)
            {
              minDate = Utility.MaxAll(minDate.GetValueOrDefault(DateTime.MinValue), DateTime.Now.AddSeconds(-maxAgeSeconds.Value));
            }
            if (minDate != null)
            {
              fileList.RemoveAll(f => f.CreationTime >= minDate.Value);
            }
            if (maxArchiveSizeMB != null)
            {
              long maxAcc = 1024 * 1024 * (long)maxArchiveSizeMB.Value;
              long acc = 0;
              fileList = fileList.OrderByDescending(f => f.CreationTimeUtc).SkipWhile(f => (acc += f.Length) <= maxAcc).ToList();
            }
            //
            deletedBytes = fileList.Sum(f => f.Length);
            counter = fileList.Count;
            fileList.ForEach(f => f.Delete());
            //
          }
        }
        catch { }
        finally
        {
          impersonationWorker.Undo();
        }
      }
      return deletedBytes;
    }



    private class NodeVFS_AuxDB
    {
      public int snode { get; set; }
      public int rnode { get; set; }
      public string name { get; set; }
      public string language { get; set; }
      public string area { get; set; }
      public DateTime? date_activation { get; set; }
      public DateTime? date_expiry { get; set; }
      public int streamId { get; set; }
      public string source { get; set; }
      public string key { get; set; }
      public string type { get; set; }
      public string filename { get; set; }
      //public DateTime date_vNode { get; set; }
      public DateTime date_vData { get; set; }
      public DateTime date_iNode { get; set; }
    }

  }

}
