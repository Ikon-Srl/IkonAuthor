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


/*
namespace Ikon.Handlers
{

  //
  // aggiungere un parametro: download
  // se presente o se il mime tipe non viene riconosciuto solamente in quel caso si
  // genera l'header di download, altrimenti lo lasciamo come stream normale
  // attenzione a modificare anche l'helper per la generazione delle proxyVFS URLs
  // aggiungendo un parametro download quando lo si usa per gli attachemnts (news, qualita, ...)
  // prevedere una ghost copy dei parametri querystring non riconosciuti su post
  // o mirrorare direttamente la querystring su post header
  // per specificare il path si puo' usare anche context.Request.PathInfo
  // es. /ProxyVFS.axd/path1/path2/path3?stream=
  // il path utilizza l'encoding dei caratteri non supportati da IIS
  // stream puo' anche essere utilizzato per le risorse multistream: multi-stream = source|stream
  // utilizzare l'appSetting "ProxyVFS_DontUseQueryString" per forzare la generazione delle url senza querystring
  //
  public class AsyncProxyVFS : IHttpAsyncHandler, IReadOnlySessionState
  {
    public static readonly int CachingObjectSizeMax = 1024 * 1024;
    public bool IsReusable { get { return true; } }


    public void ProcessRequest(HttpContext context)
    {
      // should never get called
      throw new Exception("The method or operation is not implemented.");
    }


    public IAsyncResult BeginProcessRequest(HttpContext context, AsyncCallback cb, object extraData)
    {
      int? rNodeCode = Utility.TryParse<int?>(context.Request["rnode"], null);
      int? sNodeCode = Utility.TryParse<int?>(context.Request["snode"], null);
      int? iNodeCode = Utility.TryParse<int?>(context.Request["inode"], null);
      int? freeze = Utility.TryParse<int?>(context.Request["freeze"], null);
      string relationType = context.Request["relationType"];
      string path = context.Request["path"];
      string stream = context.Request["stream"];
      string contentType = context.Request["mime"];
      bool forceDownload = context.Request.Params.AllKeys.Contains("forceDownload");
      //
      // iniziaizzazione da PathInfo (comunque c'e' l'override da QS)
      if (!string.IsNullOrEmpty(context.Request.PathInfo))
      {
        try
        {
          Newtonsoft.Json.Linq.JContainer data = Newtonsoft.Json.JsonConvert.DeserializeObject(Utility.StringBase64ToString(context.Request.PathInfo.Substring(1))) as Newtonsoft.Json.Linq.JContainer;
          rNodeCode = rNodeCode ?? data.Where(r => r.Value<string>("Key") == "rnode").Select(r => r.Value<int?>("Value")).FirstOrDefault();
          sNodeCode = sNodeCode ?? data.Where(r => r.Value<string>("Key") == "snode").Select(r => r.Value<int?>("Value")).FirstOrDefault();
          iNodeCode = iNodeCode ?? data.Where(r => r.Value<string>("Key") == "inode").Select(r => r.Value<int?>("Value")).FirstOrDefault();
          freeze = freeze ?? data.Where(r => r.Value<string>("Key") == "freeze").Select(r => r.Value<int?>("Value")).FirstOrDefault();
          relationType = relationType ?? data.Where(r => r.Value<string>("Key") == "relationType").Select(r => r.Value<string>("Value")).FirstOrDefault();
          stream = stream ?? data.Where(r => r.Value<string>("Key") == "stream").Select(r => r.Value<string>("Value")).FirstOrDefault();
          path = path ?? data.Where(r => r.Value<string>("Key") == "path").Select(r => r.Value<string>("Value")).FirstOrDefault();
          contentType = contentType ?? data.Where(r => r.Value<string>("Key") == "mime").Select(r => r.Value<string>("Value")).FirstOrDefault();
          forceDownload = data.Where(r => r.Value<string>("Key") == "forceDownload").Select(r => r.Value<bool>("Value")).DefaultIfEmpty(forceDownload).First();
        }
        catch
        {
          path = path ?? context.Request.PathInfo;
        }
      }
      //
      int? cacheDurationServer = Utility.TryParse<int?>(context.Request["cacheServer"], null);
      if (Utility.TryParse<bool>(IKGD_Config.AppSettings["CachingProxyVFS_DataEnabled"], true) && cacheDurationServer == null)
        cacheDurationServer = Utility.TryParse<int?>(IKGD_Config.AppSettings["CachingFilesExpiry"] ?? "600", cacheDurationServer);
      int? cacheDurationBrowser = Utility.TryParse<int?>(context.Request["cacheBrowser"], null);
      if (Utility.TryParse<bool>(IKGD_Config.AppSettings["CachingProxyVFS_BrowserEnabled"], true) && cacheDurationBrowser == null)
        cacheDurationBrowser = Utility.TryParse<int?>(IKGD_Config.AppSettings["CachingProxyVFS_Browser"] ?? "86400", cacheDurationBrowser);
      //
      string defaultResource = context.Request["default"];
      if (!string.IsNullOrEmpty(path))
        path = Utility.UrlDecodePath_IIS(path);
      //
      return AsyncProxyVFS_Helper.ProxyVFS_Request(context,
        path, rNodeCode, sNodeCode, iNodeCode, freeze,
        relationType, stream, contentType,
        cacheDurationServer, cacheDurationBrowser, forceDownload,
        defaultResource);
      //
    }


    public void EndProcessRequest(IAsyncResult result)
    {
      HttpContext context = (HttpContext)result.AsyncState;

      try
      {
        // restore used Command
        SqlCommand cmd = (SqlCommand)context.Items["cmd"];
        // retrieve result
        using (SqlDataReader reader = cmd.EndExecuteReader(result))
        {
          this.renderImage(context, reader);
        }
      }
      catch (Exception exc)
      {
        // will return an image with the error text
        this.renderError(context, "ERROR: " + exc.Message);
        context.Response.StatusCode = 500;
      }
    }


  }



  public static class AsyncProxyVFS_Helper
  {
    public static readonly int CachingObjectSizeMax = 1024 * 1024;


    public static IAsyncResult ProxyVFS_Request(HttpContext context,
      string path, int? rNodeCode, int? sNodeCode, int? iNodeCode, int? freeze,
      string relationType, string stream, string contentType,
      int? cacheDurationServer, int? cacheDurationBrowser, bool forceDownload,
      string defaultResource)
    {
      stream = Utility.StringTruncate(stream ?? string.Empty, 250);
      string mimeType = string.Empty;
      string fileName = string.Empty;
      string ETag = string.Empty;
      //
      if (string.IsNullOrEmpty(path) && rNodeCode == null && sNodeCode == null && iNodeCode == null)
      {
        ResourceNotFound(context, defaultResource, mimeType, "Nessuna risorsa specificata.");
        return null;
      }
      // We don't want to buffer because we want to save memory
      context.Response.Buffer = false;
      context.Response.BufferOutput = false;
      //
      try
      {
        //
        context.Response.ClearContent();
        context.Response.ClearHeaders();
        //
        if (cacheDurationBrowser > 0)
          HttpHelper.CacheResponse(context, cacheDurationBrowser.Value);
        else
          HttpHelper.DoNotCacheResponse(context);
        //
        byte[] data = ReadDataFromVFS(context, path, rNodeCode, sNodeCode, iNodeCode, relationType, stream, freeze, null, out fileName, out mimeType, out ETag);
        if (data == null && !context.Response.IsClientConnected)
          return;
        if (data == null)
          throw new Exception(mimeType);  // contiene l'eccezione generata da ProxyVFS.ReadDataFromVFS
        //
        // normalizzazione dei mime type non supportati correttamente da explorer
        //
        if (mimeType == "application/x-pdf")
          mimeType = "application/pdf";
        //
        if (!string.IsNullOrEmpty(contentType))
          mimeType = contentType;
        if (!string.IsNullOrEmpty(mimeType))
          context.Response.ContentType = mimeType;
        //
        // per i mime type non riconosciuti genera un header di download
        //
        fileName = fileName ?? string.Empty;
        string inlineMode = "inline";
        if (!string.IsNullOrEmpty(fileName) && (forceDownload || string.IsNullOrEmpty(mimeType) || mimeType == "application/octet-stream"))
          inlineMode = "attachment";
        context.Response.AppendHeader("Content-Disposition", string.Format("{0}; filename=\"{1}\"", inlineMode, fileName));
        //
        context.Response.AppendHeader("Content-Length", data.Length.ToString());
        context.Response.Cache.SetETag(ETag);
        //context.Response.AppendHeader("Accept-Ranges", "bytes");  // e' il colpevole dei problemi che incontriamo con i files .pdf
        //
        if (context.Response.IsClientConnected)
        {
          context.Response.BinaryWrite(data);
          context.Response.Flush();
        }
        //context.Response.End();  //genera un errore in debug
      }
      catch (Exception ex)
      {
        ResourceNotFound(context, defaultResource, mimeType, ex.Message);
      }
    }


    public static void ResourceNotFound(HttpContext context, string defaultResource, string mimeType, string message)
    {
      try
      {
        context.Response.Clear();
        if (!string.IsNullOrEmpty(defaultResource))
        {
          string defaultResourceFileName = Utility.vPathMap(defaultResource);
          mimeType = Utility.GetMimeType(defaultResourceFileName);
          if (!string.IsNullOrEmpty(mimeType))
            context.Response.ContentType = mimeType;
          if (context.Response.IsClientConnected)
          {
            context.Response.WriteFile(defaultResourceFileName);
            context.Response.Flush();
          }
          //context.Response.End();  //genera un errore in debug
          return;
        }
      }
      catch { }
      byte[] data = Encoding.UTF8.GetBytes(message);
      try
      {
        string fname = VirtualPathUtility.GetDirectory(context.Request.Url.AbsolutePath) + "HandlerError.html";
        string page = Utility.FileReadVirtual(fname);
        page = page.Replace("@@@MESSAGE@@@", message);
        data = Encoding.UTF8.GetBytes(page);
      }
      catch { }
      context.Response.ContentType = "text/html";
      //context.Response.ContentEncoding = 
      context.Response.AppendHeader("Content-Length", data.Length.ToString());
      if (context.Response.IsClientConnected)
      {

        context.Response.OutputStream.Write(data, 0, data.Length);
        context.Response.Flush();
      }
      //context.Response.End();  //genera un errore in debug
    }


    public static IAsyncResult ReadDataFromVFS(HttpContext context, string path, int? rnode, int? snode, int? inode, string relationType, string stream, int? freeze, int? cacheDurationOnServer, out string fileName, out string contentType, out string ETag)
    {
      fileName = string.Empty;
      contentType = string.Empty;
      ETag = string.Empty;
      byte[] data = null;
      //
      string cacheKey = AsyncProxyVFS_CacheElement.HashFactory(path, rnode, snode, inode, relationType, stream, freeze);
      AsyncProxyVFS_CacheElement cachedInfo = (AsyncProxyVFS_CacheElement)HttpRuntime.Cache[cacheKey];
      if (cachedInfo != null)
      {
        if (cachedInfo.IsOnVFS == false)
        {
          // lo stream non e' presente nel database
          return data;
        }
        //
        fileName = cachedInfo.fileName;
        contentType = cachedInfo.contentType;
        ETag = cachedInfo.ETag;
        // controllo ACL
        if (!FS_OperationsHelpers.GetAreas().Contains(cachedInfo.area))
          throw new Exception("Credenziali di accesso insufficienti per accedere alla risorsa richiesta dalla cache.");
        // attenzione assegnamento e check, si tratta di una property letta direttamente dalla cache
        //
        // controllo se lo stream e' gia' in cache nel browser
        //
        try
        {
          if (!string.IsNullOrEmpty(context.Request.Headers["If-Modified-Since"]))
          {
            DateTime IfModifiedSince = DateTime.Parse(context.Request.Headers["If-Modified-Since"]);
            if (IfModifiedSince >= cachedInfo.LastModified || cachedInfo.ETag == context.Request.Headers["If-None-Match"])
            {
              context.Response.Status = "304 Not Modified";
              context.Response.StatusCode = 304;
              //context.Response.End();
              return null;
            }
          }
        }
        catch { }
        //
        data = cachedInfo.data;
        if (data != null)
          return data;
      }
      //
      int rNodeCode = rnode ?? -1;
      int sNodeCode = snode ?? -1;
      int iNodeCode = inode ?? -1;
      stream = Utility.StringTruncate(stream ?? string.Empty, 250);
      // freeze puo' anche restare null per mantenere il versioning attuale
      //
      try
      {
        using (FS_Operations fsOp = new FS_Operations(freeze))
        {
          fsOp.DB.ObjectTrackingEnabled = false;
          fsOp.DB.DeferredLoadingEnabled = false;
          if (cachedInfo == null)
          {
            cachedInfo = new AsyncProxyVFS_CacheElement { IsOnVFS = false, streamKey = stream, contentType = contentType, fileName = fileName, ETag = ETag };
            int secondsDuration = Utility.TryParse<int>(IKGD_Config.AppSettings["CachingProxyVFS_MetaData"], 3600);
            AggregateCacheDependency sqlDeps = AsyncProxyVFS_CacheElement.CacheDependencyFactory(rnode, snode, inode, relationType);
            HttpRuntime.Cache.Insert(cacheKey, cachedInfo, sqlDeps, DateTime.Now.AddSeconds(secondsDuration), Cache.NoSlidingExpiration, CacheItemPriority.Low, null);
            //
            ProxyVFS_fsInfo fsNodeInfo = null;
            if (!string.IsNullOrEmpty(path) && iNodeCode < 0 && rNodeCode < 0 && sNodeCode < 0)
            {
              // seleziona il primo path valido che soddisfa la richiesta
              try
              {
                var paths = fsOp.PathsFromString(path);
                sNodeCode = (paths.FilterPathsByLanguage().FirstOrDefault() ?? paths.FirstOrDefault()).sNode;
              }
              catch { }
            }
            //
            if (iNodeCode > 0)
            {
              fsNodeInfo =
                (from iNode in fsOp.NodesActive<IKGD_INODE>(freeze == -1).Where(n => n.version == iNodeCode)
                 from vData in fsOp.NodesActive<IKGD_VDATA>(freeze == -1).Where(n => n.rnode == iNode.rnode)
                 select new ProxyVFS_fsInfo { Area = vData.area, FileName = iNode.filename, Mime = iNode.mime, Version = iNode.version, VersionDate = iNode.version_date }).FirstOrDefault();
            }
            else if (rNodeCode > 0)
            {
              fsNodeInfo =
                (from iNode in fsOp.NodesActive<IKGD_INODE>(freeze == -1).Where(n => n.rnode == rNodeCode)
                 from vData in fsOp.NodesActive<IKGD_VDATA>(freeze == -1).Where(n => n.rnode == iNode.rnode)
                 select new ProxyVFS_fsInfo { Area = vData.area, FileName = iNode.filename, Mime = iNode.mime, Version = iNode.version, VersionDate = iNode.version_date }).FirstOrDefault();
            }
            else if (sNodeCode > 0 && relationType != null)
            {
              fsNodeInfo =
                (from rel in fsOp.NodesActive<IKGD_RELATION>(freeze == -1).Where(n => n.snode_src == sNodeCode && n.type == relationType)
                 from vNode in fsOp.NodesActive<IKGD_VNODE>(freeze == -1).Where(n => n.snode == rel.snode_dst)
                 from vData in fsOp.NodesActive<IKGD_VDATA>(freeze == -1).Where(n => n.rnode == vNode.rnode)
                 from iNode in fsOp.NodesActive<IKGD_INODE>(freeze == -1).Where(n => n.rnode == vNode.rnode)
                 orderby rel.position
                 select new ProxyVFS_fsInfo { Name = vNode.name, Area = vData.area, FileName = iNode.filename, Mime = iNode.mime, Version = iNode.version, VersionDate = iNode.version_date }).FirstOrDefault();
            }
            else if (sNodeCode > 0)
            {
              fsNodeInfo =
                (from vNode in fsOp.NodesActive<IKGD_VNODE>(freeze == -1).Where(n => n.snode == sNodeCode)
                 from vData in fsOp.NodesActive<IKGD_VDATA>(freeze == -1).Where(n => n.rnode == vNode.rnode)
                 from iNode in fsOp.NodesActive<IKGD_INODE>(freeze == -1).Where(n => n.rnode == vNode.rnode)
                 select new ProxyVFS_fsInfo { Name = vNode.name, Area = vData.area, FileName = iNode.filename, Mime = iNode.mime, Version = iNode.version, VersionDate = iNode.version_date }).FirstOrDefault();
            }
            if (fsNodeInfo == null || fsNodeInfo.Version == null)
              throw new Exception("Risorsa non trovata sul Gestore Documentale.");
            fileName = fsNodeInfo.Name ?? fsNodeInfo.FileName;
            ETag = string.Format("{0}/{1}", fsNodeInfo.Version, stream);
            //
            cachedInfo.inode = fsNodeInfo.Version.Value;
            cachedInfo.area = fsNodeInfo.Area;
            cachedInfo.fileNameUploaded = fsNodeInfo.FileName;
            cachedInfo.fileName = fileName;
            cachedInfo.ETag = ETag;
            cachedInfo.contentType = fsNodeInfo.Mime;  // intanto un valore temporaneo prima della lettura dello stream
            cachedInfo.LastModified = fsNodeInfo.VersionDate;
          }
          //
          // se attivo questo check non permetto la visualizzazione dei contenuti multimediali
          //if (fsNode.vData.flag_unstructured == false)
          //  throw new Exception("Tipo di risorsa non fruibile attraverso l'HttpHandler.");
          //
          // verifica dei permessi di accesso
          //
          if (!fsOp.CurrentAreas.Contains(cachedInfo.area))
            throw new Exception("Credenziali di accesso insufficienti per accedere alla risorsa richiesta.");
          //
          // controllo se lo stream e' gia' in cache nel browser
          //
          try
          {
            if (!string.IsNullOrEmpty(context.Request.Headers["If-Modified-Since"]))
            {
              DateTime IfModifiedSince = DateTime.Parse(context.Request.Headers["If-Modified-Since"]);
              if (IfModifiedSince >= cachedInfo.LastModified || cachedInfo.ETag == context.Request.Headers["If-None-Match"])
              {
                context.Response.Status = "304 Not Modified";
                context.Response.StatusCode = 304;
                //context.Response.End();
                return null;
              }
            }
          }
          catch { }
          //
          SqlCommand sqlCmd = null;
          if (string.IsNullOrEmpty(cachedInfo.streamKey) || cachedInfo.streamKey.IndexOf('|') < 0)
          {
            //select top 1 * from ikgd_stream where inode=5169 and [key]='' order by id;
            sqlCmd = new SqlCommand("SELECT TOP 1 [type],[data] FROM [IKGD_STREAM] WHERE (([inode]=@inode) AND ([key]=@key)) ORDER BY [id]", fsOp.DB.Connection as SqlConnection);
            sqlCmd.Parameters.Add("@inode", SqlDbType.Int).Value = cachedInfo.inode;
            sqlCmd.Parameters.Add("@key", SqlDbType.VarChar).Value = cachedInfo.streamKey;
          }
          else
          {
            sqlCmd = new SqlCommand("SELECT TOP 1 [type],[data] FROM [IKGD_STREAM] INNER JOIN [IKGD_MSTREAM] ON [IKGD_STREAM].[id]=[IKGD_MSTREAM].[stream] WHERE (([IKGD_MSTREAM].[inode]=@inode) AND ([IKGD_STREAM].[key]=@key) AND ([IKGD_STREAM].[source] IS NULL OR [IKGD_STREAM].[source]=@source)) ORDER BY [id]", fsOp.DB.Connection as SqlConnection);
            sqlCmd.Parameters.Add("@inode", SqlDbType.Int).Value = cachedInfo.inode;
            sqlCmd.Parameters.Add("@source", SqlDbType.VarChar).Value = cachedInfo.streamKey.Split("|".ToCharArray(), 2).FirstOrDefault();
            sqlCmd.Parameters.Add("@key", SqlDbType.VarChar).Value = cachedInfo.streamKey.Split("|".ToCharArray(), 2).Skip(1).FirstOrDefault();
          }
          if (fsOp.DB.Connection.State == ConnectionState.Closed)
            fsOp.DB.Connection.Open();
          sqlCmd.CommandTimeout = Math.Max(sqlCmd.CommandTimeout, Utility.TryParse<int>(IKGD_Config.AppSettings["ProxyVFS_TimeoutDB"], 300));
          //
          //IAsyncResult res = sqlCmd.BeginExecuteReader((CommandBehavior.SingleRow | CommandBehavior.SequentialAccess | CommandBehavior.CloseConnection);
          //using (SqlDataReader reader = sqlCmd.ExecuteReader(CommandBehavior.CloseConnection))
          using (SqlDataReader reader = sqlCmd.ExecuteReader(CommandBehavior.SingleRow | CommandBehavior.SequentialAccess | CommandBehavior.CloseConnection))
          {
            if (reader.Read())
            {
              contentType = reader[0].ToString();
              //
              data = (byte[])reader[1];
              reader.Close();
              //
              if (string.IsNullOrEmpty(contentType))
                contentType = cachedInfo.contentType ?? string.Empty;
              if (contentType == "application/octetstream")
                contentType = "application/octet-stream";
              contentType = contentType.ToLower();
              if (contentType == string.Empty || contentType == "application/octet-stream")
                contentType = Utility.GetMimeType(cachedInfo.fileNameUploaded, data);
              cachedInfo.contentType = contentType;
              cachedInfo.IsOnVFS = true;
              //
              // manager per il caching sullo stream nel caso rientri nei parametri di configurazione
              //
              string cacheKeyForData = cachedInfo.AutoCacheData(data, cacheKey, cacheDurationOnServer);
            }
            else
            {
              throw new Exception("Stream non disponibile per la risorsa richiesta.");
            }
          }
          fsOp.DB.Connection.Close();
        }
      }
      catch (Exception ex)
      {
        data = null;  // per attivare il display del message
        contentType = ex.Message;
      }
      return data;
    }


    public class AsyncProxyVFS_CacheElement
    {
      public bool IsOnVFS { get; set; }
      public int inode { get; set; }
      public string streamKey { get; set; }
      public string area { get; set; }
      public string fileNameUploaded { get; set; }
      public string cacheKeyForData { get; set; }
      //
      public string fileName { get; set; }
      public string contentType { get; set; }
      public string ETag { get; set; }
      public DateTime LastModified { get; set; }
      //
      public byte[] data { get { return (string.IsNullOrEmpty(cacheKeyForData)) ? null : (byte[])HttpRuntime.Cache[cacheKeyForData]; } }
      //


      public static string HashFactory(string path, int? rnode, int? snode, int? inode, string relationType, string stream, int? freeze)
      {
        return Utility.Implode(new object[] { "AsyncProxyVFS_CacheElement_", path ?? string.Empty, rnode.GetValueOrDefault(-1), snode.GetValueOrDefault(-1), inode.GetValueOrDefault(-1), relationType ?? "[NULL]", stream ?? "[NULL]", freeze.GetValueOrDefault(int.MaxValue) }, "|");
      }


      public static AggregateCacheDependency CacheDependencyFactory(int? rnode, int? snode, int? inode, string relationType)
      {
        AggregateCacheDependency sqlDeps = new AggregateCacheDependency();
        // per default dipendenza da inode e vdata (area)
        sqlDeps.Add(new SqlCacheDependency("GDCS", "IKGD_INODE"), new SqlCacheDependency("GDCS", "IKGD_VDATA"));
        if (snode > 0 || relationType != null)
          sqlDeps.Add(new SqlCacheDependency("GDCS", "IKGD_VNODE"));
        if (relationType != null)
          sqlDeps.Add(new SqlCacheDependency("GDCS", "IKGD_RELATION"));
        return sqlDeps;
      }


      public string AutoCacheData(byte[] data, string cacheKeyMain, int? cacheDurationOnServer)
      {
        if (data == null || !Utility.TryParse<bool>(IKGD_Config.AppSettings["CachingProxyVFS_DataEnabled"], false) || cacheDurationOnServer <= 0)
          return null;
        if (data.Length > ProxyVFS.CachingObjectSizeMax)
          return null;
        cacheKeyForData = cacheKeyMain + "_data";
        try
        {
          if (contentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase))
          {
            if (data.Length > Utility.TryParse<int>(IKGD_Config.AppSettings["CachingImagesLimit"], -1))
              return null;
            cacheDurationOnServer = cacheDurationOnServer ?? Utility.TryParse<int?>(IKGD_Config.AppSettings["CachingImagesExpiry"], 600);
          }
          else
          {
            if (data.Length > Utility.TryParse<int>(IKGD_Config.AppSettings["CachingFilesLimit"], -1))
              return null;
            cacheDurationOnServer = cacheDurationOnServer ?? Utility.TryParse<int?>(IKGD_Config.AppSettings["CachingFilesExpiry"], 600);
          }
          if (cacheDurationOnServer == null || cacheDurationOnServer <= 0)
            return null;
          //
          AggregateCacheDependency cacheDeps = new AggregateCacheDependency();
          cacheDeps.Add(new SqlCacheDependency("GDCS", "IKGD_INODE"));
          cacheDeps.Add(new CacheDependency(null, new string[] { cacheKeyMain }));  // dipendenza dal cached element principale
          HttpRuntime.Cache.Insert(cacheKeyForData, data, cacheDeps, DateTime.Now.AddSeconds(cacheDurationOnServer.Value), Cache.NoSlidingExpiration, CacheItemPriority.Low, null);
          //
          return cacheKeyForData;
        }
        catch { }
        return null;
      }

    }


    private class ProxyVFS_fsInfo
    {
      public string Name { get; set; }
      public string Area { get; set; }
      public string FileName { get; set; }
      public int? Version { get; set; }
      public DateTime VersionDate { get; set; }
      public string Mime { get; set; }
    }


  }

}
*/