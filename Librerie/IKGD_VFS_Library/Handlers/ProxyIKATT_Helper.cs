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


  public static class ProxyIKATT_Helper
  {
    //
    private static object _lockCache = new object();
    //
    public enum AttributeType { IKATT, IKCAT };


    static ProxyIKATT_Helper()
    {
      //
      _lockCache = new object();
      //
    }


    private static CachedNodesContainer GetCachedNodes()
    {
      try
      {
        string cacheKey = "ProxyIKATT_CachedNodes";
        lock (_lockCache)
        {
          CachedNodesContainer CachedNodes = (CachedNodesContainer)HttpRuntime.Cache[cacheKey];
          if (CachedNodes == null)
          {
            CachedNodes = new CachedNodesContainer();
            var dependencies = FS_OperationsHelpers.GetCacheDependencyWrapper(new string[] { "IKATT_AttributeStream" });
            HttpRuntime.Cache.Insert(cacheKey, CachedNodes, dependencies, DateTime.Now.AddSeconds(ProxyVFS2_Helper.CachingFilesNodesDuration), Cache.NoSlidingExpiration, CacheItemPriority.Low, null);
          }
          return CachedNodes;
        }
      }
      catch { }
      return new CachedNodesContainer();
    }


    private static FS_Operations EnsureFsOp(ref FS_Operations fsOp, IKATT_Args args)
    {
      if (fsOp == null)
      {
        fsOp = new FS_Operations();
      }
      return fsOp;
    }


    private class CachedNodesContainer
    {
      public object _lock;
      public SortedDictionary<int, NodeVFS_Resource> IKATT_Nodes { get; private set; }
      public SortedDictionary<int, NodeVFS_Resource> IKCAT_Nodes { get; private set; }

      public CachedNodesContainer()
      {
        _lock = new object();
        IKATT_Nodes = new SortedDictionary<int, NodeVFS_Resource>();
        IKCAT_Nodes = new SortedDictionary<int, NodeVFS_Resource>();
      }
    }


    private class NodeVFS_Resource
    {
      public int IdAttr { get; set; }
      public List<IKATT_Stream> Streams { get; set; }
      public DateTime LastModified { get; set; }
      public AttributeType AttrType { get; set; }
    }


    private class IKATT_Stream
    {
      public int IdAttr { get; set; }
      public string Key { get; set; }
      public string Mime { get; set; }
      public string FileName { get; set; }
      //public string ETag { get { return "ATTR" + ((int)AttrType).ToString() + "_STREAM_" + IdAttr.ToString(); } }
      public string ETag { get { return string.Format("ATTR{0}_STREAM_{1}_{2}", (int)AttrType, IdAttr, Key); } }
      public DateTime LastModified { get; set; }
      public int Length { get; set; }
      public AttributeType AttrType { get; set; }
    }


    public class IKATT_Args
    {
      public int? IdAttr { get; set; }
      public string Key { get; set; }
      public int? cacheDurationServer { get; set; }
      public int? cacheDurationBrowser { get; set; }
      public bool? forceDownload { get; set; }
      public string pathInfo { get; set; }
      public string defaultResource { get; set; }
      public AttributeType? AttrType { get; set; }
    }


    public static string NormalizeStreamKey(string stream_key)
    {
      var frags = stream_key.TrimSafe(' ').Split("|,".ToCharArray(), 2);
      if (frags.Length < 2)
      {
        stream_key = string.Format("{0}|", frags.FirstOrDefault());
      }
      else
      {
        stream_key = string.Format("{0}|{1}", frags[0], frags[1]);
      }
      return stream_key;
    }


    public static bool ProcessStreamRequest(HttpContext context, IKATT_Args args)
    {
      if (args == null || args.IdAttr == null)
      {
        throw new ProxyVFS2_Helper.ResourceNotFoundException();
      }
      //
      NodeVFS_Resource nodeVfsResource = null;
      IKATT_Stream nodeVfsStream = null;
      //
      FS_Operations fsOp = null;
      ImpersonationHelpers.ImpersonationWorker impersonationWorker = null;
      FileStream fstreamOut = null;
      //
      string mimeType = null;
      string cacheKey = null;
      AttributeType attrType = args.AttrType ?? AttributeType.IKATT;
      //
      int IdAttr = args.IdAttr.Value;
      //
      try
      {
        if (context == null)
        {
          context = System.Web.HttpContext.Current;
        }
        CachedNodesContainer CachedNodes = GetCachedNodes();
        if (args.IdAttr != null)
        {
          if (attrType == AttributeType.IKATT && CachedNodes.IKATT_Nodes.ContainsKey(IdAttr))
          {
            nodeVfsResource = CachedNodes.IKATT_Nodes[IdAttr];
          }
          else if (attrType == AttributeType.IKCAT && CachedNodes.IKCAT_Nodes.ContainsKey(IdAttr))
          {
            nodeVfsResource = CachedNodes.IKCAT_Nodes[IdAttr];
          }
        }
        //
        // se non vengono trovate in cache le informazioni necessarie popoliamo il cacheSet
        //
        if (nodeVfsResource == null)
        {
          nodeVfsResource = new NodeVFS_Resource() { IdAttr = IdAttr, AttrType = attrType };
          EnsureFsOp(ref fsOp, args);
          fsOp.DB.ObjectTrackingEnabled = false;
          nodeVfsResource.LastModified = fsOp.DateTimeContext;
          nodeVfsResource.Streams =
            (from stream in fsOp.DB.IKATT_AttributeStreams.Where(n => n.AttributeId == IdAttr)
             select new IKATT_Stream
             {
               IdAttr = stream.AttributeId,
               Key = stream.Key,
               Mime = stream.Mime,
               FileName = stream.Filename,
               LastModified = stream.Modif ?? fsOp.DateTimeContext,
               AttrType = attrType
             }).ToList();
          //
          lock (CachedNodes._lock)
          {
            if (attrType == AttributeType.IKATT)
            {
              CachedNodes.IKATT_Nodes[IdAttr] = nodeVfsResource;
            }
            else if (attrType == AttributeType.IKCAT)
            {
              CachedNodes.IKCAT_Nodes[IdAttr] = nodeVfsResource;
            }
          }
          //
        }
        //
        if (nodeVfsResource == null)
        {
          throw new ProxyVFS2_Helper.ResourceNotFoundException();
        }
        //
        // selezione dello stream corretto
        //
        // source puo' essere NULL
        // key non e' mai NULL
        string stream_key = NormalizeStreamKey(args.Key);
        //
        nodeVfsStream = nodeVfsResource.Streams.FirstOrDefault(r => string.Equals(r.Key, stream_key, StringComparison.OrdinalIgnoreCase));
        if (nodeVfsStream == null)
        {
          nodeVfsStream = nodeVfsResource.Streams.OrderBy(r => r.Key).FirstOrDefault();
        }
        //
        if (nodeVfsStream == null)
        {
          throw new ProxyVFS2_Helper.ResourceNotFoundException();
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
        // configurazione degli headers
        //
        mimeType = nodeVfsStream.Mime;
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
        if (ProxyVFS2_Helper.CachingProxyVFS_DataEnabled)
        {
          cacheDurationServerOverridden = Utility.TryParse<int?>(context.Request["cacheServer"]);
          cacheDurationServer = cacheDurationServerOverridden ?? ProxyVFS2_Helper.CachingFilesExpiry;
        }
        if (ProxyVFS2_Helper.CachingProxyVFS_BrowserEnabled)
        {
          cacheDurationBrowser = Utility.TryParse<int?>(context.Request["cacheBrowser"], ProxyVFS2_Helper.CachingProxyVFS_Browser);
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
        if (ProxyVFS2_Helper.CachingProxyVFS_DataEnabled)
        {
          cacheKey = nodeVfsStream.ETag;
          byte[] cachedStream = (byte[])HttpRuntime.Cache[cacheKey];
          if (cachedStream != null)
          {
            context.Response.AppendHeader("Content-Length", cachedStream.Length.ToString());
            context.Response.BinaryWrite(cachedStream);
            return true;
          }
        }
        //
        //context.Response.AppendHeader("Accept-Ranges", "bytes");  // e' il colpevole dei problemi che incontriamo con i files .pdf
        //
        // streaming da da cached VFS
        //
        if (ProxyVFS2_Helper.ProxyVFS_SharePath_CachingVFS != null)
        {
          string cachedPath = string.Format(@"{0}\{1}", ProxyVFS2_Helper.ProxyVFS_SharePath_CachingVFS, nodeVfsStream.ETag);
          impersonationWorker = ImpersonationHelpers.ImpersonationWorker.Factory(cachedPath, ProxyVFS2_Helper.ShareServerCacheVFS, ProxyVFS2_Helper.ShareUserNameCacheVFS, ProxyVFS2_Helper.SharePasswordCacheVFS);
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
                  byte[] outbuffer = new byte[ProxyVFS2_Helper.ProxyVFS_BufferingSizeDB];
                  long startIndex = 0;
                  long retval;
                  do
                  {
                    retval = fstream.Read(outbuffer, 0, ProxyVFS2_Helper.ProxyVFS_BufferingSizeDB);
                    startIndex += retval;
                    context.Response.OutputStream.Write(outbuffer, 0, (int)retval);
                    if (ProxyVFS2_Helper.ProxyVFS_BufferingAutoFlushDB)
                    {
                      context.Response.Flush();
                    }
                  } while (retval == ProxyVFS2_Helper.ProxyVFS_BufferingSizeDB);
                  //
                  // se la lettura dei dati è avvenuta in un solo blocco allora possiamo passare la richiesta al cache management
                  //
                  if (startIndex <= ProxyVFS2_Helper.CachingFilesDiskCacheMaxBytes && ProxyVFS2_Helper.CachingProxyVFS_DataEnabled)
                  {
                    ProxyVFS2_Helper.CachingBufferWorker(outbuffer, (int)startIndex, cacheKey, mimeType, cacheDurationServer, cacheDurationServerOverridden);
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
          //
          SqlCommand sqlCmd = null;
          if (nodeVfsStream.AttrType == AttributeType.IKATT)
          {
            sqlCmd = new SqlCommand("SELECT TOP 1 [Data] FROM [IKATT_AttributeStream] WITH(NOLOCK) WHERE ([AttributeId]=@AttributeId AND [Key]=@Key)", fsOp.DB.Connection as SqlConnection);
          }
          else
          {
            sqlCmd = new SqlCommand("SELECT TOP 1 [Data] FROM [IKCAT_AttributeStream] WITH(NOLOCK) WHERE ([AttributeId]=@AttributeId AND [Key]=@Key)", fsOp.DB.Connection as SqlConnection);
          }
          sqlCmd.Parameters.Add("@AttributeId", SqlDbType.Int).Value = nodeVfsStream.IdAttr;
          sqlCmd.Parameters.Add("@Key", SqlDbType.VarChar).Value = nodeVfsStream.Key;
          //
          sqlCmd.CommandTimeout = ProxyVFS2_Helper.ProxyVFS_TimeoutDB;
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
              byte[] outbuffer = new byte[ProxyVFS2_Helper.ProxyVFS_BufferingSizeDB];
              long startIndex = 0;
              long retval;
              do
              {
                retval = reader.GetBytes(0, startIndex, outbuffer, 0, ProxyVFS2_Helper.ProxyVFS_BufferingSizeDB);
                startIndex += retval;
                context.Response.OutputStream.Write(outbuffer, 0, (int)retval);
                if (fstreamOut != null)
                {
                  // salvataggio dello stream anche sul CachedVFS
                  try { fstreamOut.Write(outbuffer, 0, (int)retval); }
                  catch { }
                }
                if (ProxyVFS2_Helper.ProxyVFS_BufferingAutoFlushDB)
                {
                  context.Response.Flush();
                }
              } while (retval == ProxyVFS2_Helper.ProxyVFS_BufferingSizeDB);
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
              if (startIndex <= ProxyVFS2_Helper.ProxyVFS_BufferingSizeDB && ProxyVFS2_Helper.CachingProxyVFS_DataEnabled)
              {
                ProxyVFS2_Helper.CachingBufferWorker(outbuffer, (int)startIndex, cacheKey, mimeType, cacheDurationServer, cacheDurationServerOverridden);
              }
              //
            }
            else
            {
              throw new ProxyVFS2_Helper.ResourceNotFoundException();
            }
          }  //reader
          return true;
        }
      }
      catch (ProxyVFS2_Helper.ResourceNotFoundException)
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
          if (context.Response.IsClientConnected && ProxyVFS2_Helper.ProxyVFS_BufferingAutoFlushDB)
          {
            context.Response.Flush();
          }
        }
        catch { }
      }
    }



  }

}
