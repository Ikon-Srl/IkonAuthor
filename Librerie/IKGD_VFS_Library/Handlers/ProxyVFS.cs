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
using System.Text.RegularExpressions;
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
  public class ProxyVFS : IHttpHandler, IReadOnlySessionState
  {
    public bool IsReusable { get { return true; } }


    public void ProcessRequest(HttpContext context)
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
      string defaultResource = context.Request["default"];
      //
      // inizializzazione da PathInfo (comunque c'e' l'override da QS)
      //if (context.Request.PathInfo.IsNotEmpty())
      //{
      //  try
      //  {
      //    // non sembra funzionare sempre correttamente
      //    Newtonsoft.Json.Linq.JContainer data = Newtonsoft.Json.JsonConvert.DeserializeObject(Utility.StringBase64ToString(context.Request.PathInfo.Substring(1))) as Newtonsoft.Json.Linq.JContainer;
      //    rNodeCode = rNodeCode ?? data.Where(r => r.Value<string>("Key") == "rnode").Select(r => r.Value<int?>("Value")).FirstOrDefault();
      //    sNodeCode = sNodeCode ?? data.Where(r => r.Value<string>("Key") == "snode").Select(r => r.Value<int?>("Value")).FirstOrDefault();
      //    iNodeCode = iNodeCode ?? data.Where(r => r.Value<string>("Key") == "inode").Select(r => r.Value<int?>("Value")).FirstOrDefault();
      //    freeze = freeze ?? data.Where(r => r.Value<string>("Key") == "freeze").Select(r => r.Value<int?>("Value")).FirstOrDefault();
      //    relationType = relationType ?? data.Where(r => r.Value<string>("Key") == "relationType").Select(r => r.Value<string>("Value")).FirstOrDefault();
      //    stream = stream ?? data.Where(r => r.Value<string>("Key") == "stream").Select(r => r.Value<string>("Value")).FirstOrDefault();
      //    path = path ?? data.Where(r => r.Value<string>("Key") == "path").Select(r => r.Value<string>("Value")).FirstOrDefault();
      //    contentType = contentType ?? data.Where(r => r.Value<string>("Key") == "mime").Select(r => r.Value<string>("Value")).FirstOrDefault();
      //    forceDownload = data.Where(r => r.Value<string>("Key") == "forceDownload").Select(r => r.Value<bool>("Value")).DefaultIfEmpty(forceDownload).First();
      //  }
      //  catch
      //  {
      //    path = path ?? context.Request.PathInfo;
      //  }
      //}
      //
      bool useNewEngine = ProxyVFS2_Helper.Enabled;
      useNewEngine &= (iNodeCode == null && relationType == null && contentType == null);
      //
      if (context.Request.PathInfo.IsNotEmpty())
      {
        string pathInfo = Utility.UrlDecodePath_IIS(context.Request.PathInfo);
        string[] frags = pathInfo.TrimSafe('/', ' ').Split("/".ToCharArray(), 3);
        string streamFromPath = frags.FirstOrDefault().TrimSafe(' ');
        string node = frags.Skip(1).FirstOrDefault().TrimSafe(' ');
        string indexPath = frags.Skip(2).FirstOrDefault();
        if (Regex.IsMatch(node, @"^(r|s){0,1}\d+$", RegexOptions.Singleline | RegexOptions.IgnoreCase))
        {
          if (node.StartsWith("r", StringComparison.OrdinalIgnoreCase))
          {
            rNodeCode = rNodeCode ?? Utility.TryParse<int?>(node.Substring(1));
          }
          else if (node.StartsWith("s", StringComparison.OrdinalIgnoreCase))
          {
            sNodeCode = sNodeCode ?? Utility.TryParse<int?>(node.Substring(1));
          }
          else
          {
            sNodeCode = sNodeCode ?? Utility.TryParse<int?>(node);
          }
          stream = stream ?? (streamFromPath == "null" ? "," : streamFromPath);
        }
        else
        {
          path = path ?? pathInfo;
        }
      }
      //
      if (useNewEngine)
      {
        var res01 = Ikon.Handlers.ProxyVFS2_Helper.ProcessStreamRequest(context, new Ikon.Handlers.ProxyVFS2_Helper.NodeVFS_Args
        {
          rNode = rNodeCode,
          sNode = sNodeCode,
          VersionFrozen = freeze,
          SourceKey = stream,
          pathInfo = path,
          cacheDurationServer = Utility.TryParse<int?>(context.Request["cacheServer"], null),
          cacheDurationBrowser = Utility.TryParse<int?>(context.Request["cacheBrowser"], null),
          defaultResource = defaultResource,
          forceDownload = forceDownload
        });
      }
      else
      {
        //
        // vecchio engine
        //
        int? cacheDurationServer = Utility.TryParse<int?>(context.Request["cacheServer"], null);
        if (Utility.TryParse<bool>(IKGD_Config.AppSettings["CachingProxyVFS_DataEnabled"], true) && cacheDurationServer == null)
          cacheDurationServer = Utility.TryParse<int?>(IKGD_Config.AppSettings["CachingFilesExpiry"] ?? "600", cacheDurationServer);
        int? cacheDurationBrowser = Utility.TryParse<int?>(context.Request["cacheBrowser"], null);
        if (Utility.TryParse<bool>(IKGD_Config.AppSettings["CachingProxyVFS_BrowserEnabled"], true) && cacheDurationBrowser == null)
          cacheDurationBrowser = Utility.TryParse<int?>(IKGD_Config.AppSettings["CachingProxyVFS_Browser"] ?? "86400", cacheDurationBrowser);
        //
        ProxyVFS_Helper.ProxyVFS_Request(context,
          path, rNodeCode, sNodeCode, iNodeCode, freeze,
          relationType, stream, contentType,
          cacheDurationServer, cacheDurationBrowser, forceDownload,
          defaultResource);
        //
        if (context.Response.IsClientConnected)
        {
          //context.Response.End();
          context.ApplicationInstance.CompleteRequest();  // da usare al posto di .Response.End();
        }
      }
    }

  }



  public static class ProxyVFS_Helper
  {
    public static readonly int CachingObjectSizeMax = Utility.TryParse<int>(IKGD_Config.AppSettings["CachingFilesMaxBytes"], 25 * 1024);


    public static void ProxyVFS_Request(HttpContext context,
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
        return;
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
        {
          HttpHelper.CacheResponse(context, cacheDurationBrowser.Value);
        }
        else
        {
          HttpHelper.DoNotCacheResponse(context);
        }
        //
        byte[] data = ReadDataFromVFS(context, path, rNodeCode, sNodeCode, iNodeCode, relationType, stream, freeze, null, out fileName, out mimeType, out ETag);
        if (data == null && !context.Response.IsClientConnected)
          return;
        // trovato un ETag valido
        if (data == null && context.Response.StatusCode == 304)
        {
          context.Response.Flush();
          context.ApplicationInstance.CompleteRequest();  // da usare al posto di .Response.End();
          return;
        }
        if (data == null)
          throw new Exception(mimeType);  // contiene l'eccezione generata da ProxyVFS.ReadDataFromVFS
        //
        bool isExternalStorage = false;
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
        //
        if (!string.IsNullOrEmpty(contentType))
          mimeType = contentType;
        if (!string.IsNullOrEmpty(mimeType))
          context.Response.ContentType = mimeType;
        //
        // per i mime type non riconosciuti genera un header di download
        //
        try
        {
          if (!string.IsNullOrEmpty(HttpContext.Current.Request.PathInfo))
          {
            string extraPathInfo = HttpContext.Current.Request.PathInfo;
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
              //string fileExt = Ikon.Mime.MimeExtensionHelper.FindExtensionWithPoint(mimeType);
              //if (!fileName.EndsWith(fileExt, StringComparison.OrdinalIgnoreCase))
              //  fileName = Utility.PathGetFileNameSanitized(fileName.Trim(' ', '.') + (fileExt ?? string.Empty));
            }
            catch { }
          }
        }
        catch { }
        fileName = fileName ?? string.Empty;
        string inlineMode = "inline";
        if (!string.IsNullOrEmpty(fileName) && (forceDownload || string.IsNullOrEmpty(mimeType) || mimeType == "application/octet-stream"))
          inlineMode = "attachment";
        context.Response.AppendHeader("Content-Disposition", string.Format("{0}; filename=\"{1}\"", inlineMode, fileName));
        //
        context.Response.Cache.SetETag(ETag);
        //context.Response.AppendHeader("Accept-Ranges", "bytes");  // e' il colpevole dei problemi che incontriamo con i files .pdf
        //
        if (context.Response.IsClientConnected)
        {
          if (isExternalStorage)
          {
            using (IKGD_ExternalVFS_Support extFS = new IKGD_ExternalVFS_Support())
            {
              bool res = extFS.DownloadExternalStream(context.Response, data);
            }
          }
          else
          {
            context.Response.AppendHeader("Content-Length", data.Length.ToString());
            context.Response.BinaryWrite(data);
          }
          context.Response.Flush();
        }
        //context.Response.End();  //genera un errore in debug
        //context.ApplicationInstance.CompleteRequest();  // da usare al posto di .Response.End();
      }
      catch (Exception ex)
      {
        //Elmah.ErrorSignal.FromCurrentContext().Raise(ex);
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
          //context.ApplicationInstance.CompleteRequest();  // da usare al posto di .Response.End();
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
      try
      {
        context.Response.ContentType = "text/html";
        //context.Response.ContentEncoding = 
        context.Response.AppendHeader("Content-Length", data.Length.ToString());
        if (context.Response.IsClientConnected)
        {

          context.Response.OutputStream.Write(data, 0, data.Length);
          context.Response.Flush();
        }
        //context.Response.End();  //genera un errore in debug
        //context.ApplicationInstance.CompleteRequest();  // da usare al posto di .Response.End();
      }
      //catch (Exception ex)
      //{
      //  Elmah.ErrorSignal.FromCurrentContext().Raise(ex);
      //}
      catch { }
    }


    public static byte[] ReadDataFromVFS(HttpContext context, string path, int? rnode, int? snode, int? inode, string relationType, string stream, int? freeze, int? cacheDurationOnServer, out string fileName, out string contentType, out string ETag)
    {
      fileName = string.Empty;
      contentType = string.Empty;
      ETag = string.Empty;
      byte[] data = null;
      //
      stream = Utility.StringTruncate(stream.TrimSafe(), 250);
      if (stream == "|" || stream == ",")
        stream = string.Empty;
      //
      //path identici possono essere associati a stream differenti nelle varie lingue
      string path4cache = string.IsNullOrEmpty(path) ? null : string.Format("[[{1}]]{0}", path, IKGD_Language_Provider.Provider.Language);
      string cacheKey = ProxyVFS_CacheElement.HashFactory(path4cache, rnode, snode, inode, relationType, stream, freeze);
      ProxyVFS_CacheElement cachedInfo = (ProxyVFS_CacheElement)HttpRuntime.Cache[cacheKey];
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
        //
        // controllo ACL
        if (!CheckResourceACL(context, cachedInfo.area, true))
        {
          ForceAuthRequest(context);
          throw new Exception("Credenziali di accesso insufficienti per accedere alla risorsa richiesta dalla cache. {0}".FormatString(DateTime.Now));
        }
        //
        // attenzione assegnamento e check, si tratta di una property letta direttamente dalla cache
        //
        // controllo se lo stream e' gia' in cache nel browser
        //
        try
        {
          // test per ETag e caching sul client
          bool cachedOnClient = !string.IsNullOrEmpty(context.Request.Headers["If-Modified-Since"]) || !string.IsNullOrEmpty(context.Request.Headers["If-None-Match"]);
          if (cachedOnClient && !string.IsNullOrEmpty(context.Request.Headers["If-None-Match"]))
            cachedOnClient &= context.Request.Headers["If-None-Match"] == cachedInfo.ETag;
          if (cachedOnClient && !string.IsNullOrEmpty(context.Request.Headers["If-Modified-Since"]))
          {
            DateTime IfModifiedSince = DateTime.Parse(context.Request.Headers["If-Modified-Since"]);
            cachedOnClient &= IfModifiedSince >= cachedInfo.LastModified;
          }
          if (cachedOnClient)
          {
            context.Response.Status = "304 Not Modified";
            context.Response.StatusCode = 304;
            //context.Response.End();
            //context.ApplicationInstance.CompleteRequest();  // da usare al posto di .Response.End();
            return null;
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
            cachedInfo = new ProxyVFS_CacheElement { IsOnVFS = null, streamKey = stream, contentType = contentType, fileName = fileName, ETag = ETag };
            int secondsDuration = Utility.TryParse<int>(IKGD_Config.AppSettings["CachingProxyVFS_MetaData"], 3600);
            AggregateCacheDependency sqlDeps = ProxyVFS_CacheElement.CacheDependencyFactory(rnode, snode, inode, relationType);
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
            {
              cachedInfo.IsOnVFS = false;
              throw new Exception("Risorsa non trovata sul Gestore Documentale.");
            }
            fileName = fsNodeInfo.Name ?? Utility.PathGetFileNameSanitized(fsNodeInfo.FileName);
            //ETag = string.Format("{0}/{1}", fsNodeInfo.Version, stream);
            ETag = Guid.NewGuid().ToString();  //verra' assegnato in seguito
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
          if (!CheckResourceACL(context, cachedInfo.area, true))
          {
            ForceAuthRequest(context);
            throw new Exception("Credenziali di accesso insufficienti per accedere alla risorsa richiesta dalla cache. {0}".FormatString(DateTime.Now));
          }
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
                //context.ApplicationInstance.CompleteRequest();  // da usare al posto di .Response.End();
                return null;
              }
            }
          }
          catch { }
          //
          bool isOnExternalStorage = false;
          SqlCommand sqlCmd = null;
          if (string.IsNullOrEmpty(cachedInfo.streamKey) || cachedInfo.streamKey.IndexOfAny("|,".ToCharArray()) < 0)
          {
            sqlCmd = new SqlCommand("SELECT TOP 1 [type],[data],[id] FROM [IKGD_STREAM] WHERE (([inode]=@inode) AND ([key]=@key)) ORDER BY [id] DESC", fsOp.DB.Connection as SqlConnection);
            sqlCmd.Parameters.Add("@inode", SqlDbType.Int).Value = cachedInfo.inode;
            sqlCmd.Parameters.Add("@key", SqlDbType.VarChar).Value = cachedInfo.streamKey;
          }
          else
          {
            //
            // viene usato un sort desc per source in modo che se coesistono source=NULL e source=xyz quest'ultimo risulti in cima alla lista
            //sqlCmd = new SqlCommand("SELECT TOP 1 [type],[data],[id] FROM [IKGD_STREAM] INNER JOIN [IKGD_MSTREAM] ON [IKGD_STREAM].[id]=[IKGD_MSTREAM].[stream] WHERE (([IKGD_MSTREAM].[inode]=@inode) AND ([IKGD_STREAM].[key]=@key) AND ([IKGD_STREAM].[source] IS NULL OR [IKGD_STREAM].[source]=@source)) ORDER BY [IKGD_STREAM].[source] DESC, [id] DESC", fsOp.DB.Connection as SqlConnection);
            //
            // viene usato un sort desc per id perche' nel caso coesistano source=NULL e source=xyz l'ultima risorsa inserita risulti in cima alla lista
            sqlCmd = new SqlCommand("SELECT TOP 1 [type],[data],[id] FROM [IKGD_STREAM] INNER JOIN [IKGD_MSTREAM] ON [IKGD_STREAM].[id]=[IKGD_MSTREAM].[stream] WHERE (([IKGD_MSTREAM].[inode]=@inode) AND ([IKGD_STREAM].[key]=@key) AND ([IKGD_STREAM].[source] IS NULL OR [IKGD_STREAM].[source]=@source)) ORDER BY [id] DESC", fsOp.DB.Connection as SqlConnection);
            sqlCmd.Parameters.Add("@inode", SqlDbType.Int).Value = cachedInfo.inode;
            sqlCmd.Parameters.Add("@source", SqlDbType.VarChar).Value = cachedInfo.streamKey.Split("|,".ToCharArray(), 2).FirstOrDefault();
            sqlCmd.Parameters.Add("@key", SqlDbType.VarChar).Value = cachedInfo.streamKey.Split("|,".ToCharArray(), 2).Skip(1).FirstOrDefault();
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
              //
              contentType = reader[0].ToString();
              if (IKGD_ExternalVFS_Support.IsExternalFileFromMime(contentType))
              {
                isOnExternalStorage = true;
              }
              //
              // TODO:
              // usare fetch diretto o buffered secondo che l'elemento sara' messo in cache oppure e' troppo grande
              // spostare parte del processing dell'header in questa sezione e provare a gestire anche il byte range
              // con flush dell'header prima dei dati
              //
              bool unbuffered = isOnExternalStorage || true;
              if (unbuffered)
              {
                data = (byte[])reader[1];
              }
              else
              {
                const int bufferSize = 1024 * 10;
                byte[] outbuffer = new byte[bufferSize];
                long startIndex = 0;
                long retval;
                do
                {
                  retval = reader.GetBytes(1, startIndex, outbuffer, 0, bufferSize);
                  startIndex += retval;
                  context.Response.OutputStream.Write(outbuffer, 0, (int)retval);
                  //context.Response.Flush();
                } while (retval == bufferSize);
              }
              //
              cachedInfo.ETag = string.Format("{0}/{1}", reader[2], stream);
              reader.Close();
              //
              if (string.IsNullOrEmpty(contentType))
                contentType = cachedInfo.contentType ?? string.Empty;
              if (contentType == "application/octetstream")
                contentType = "application/octet-stream";
              contentType = contentType.ToLower();
              if (string.IsNullOrEmpty(contentType) || contentType == "application/octet-stream")
              {
                // non viene eseguita nel caso di external storage (c'e' un mime con il prefix)
                contentType = Utility.GetMimeType(cachedInfo.fileNameUploaded, data);
              }
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


    public static bool CheckResourceACL(HttpContext context, string area, bool checkForAuth)
    {
      if (area == null || context == null)
        return true;
      if ((bool)(context.Items["ProxyVFS_ResourceACLCheched"] ?? false) == true)
      {
        return true;
      }
      else
      {
        context.Items["ProxyVFS_ResourceACLCheched"] = true;
      }
      //
      if (Ikon.Auth.Roles_IKGD.Provider.AreasPublic.Any(a => a.Name == area))
        return true;
      var areaACL = Ikon.Auth.Roles_IKGD.Provider.AreasAll.FirstOrDefault(a => a.Name == area);
      if (areaACL == null)
      {
        return FS_OperationsHelpers.IsRoot || Utility.TryParse<bool>(IKGD_Config.AppSettings["ProxyVFS_UnamappedAreasAllowed"], false);
      }
      if (areaACL.IsPublic || areaACL.IsHardCoded)
      {
        return true;
      }
      if ((bool)(context.Items["ProxyVFS_CacheHeadersOverriden"] ?? false) == false)
      {
        context.Items["ProxyVFS_CacheHeadersOverriden"] = true;
        HttpHelper.DoNotCacheResponse(context);
      }
      if (checkForAuth)
      {
        bool isAuth = HttpContext.Current.User != null && HttpContext.Current.User.Identity.IsAuthenticated == true;
        if (isAuth && FS_OperationsHelpers.GetAreas().Any(a => a == areaACL.Name))
        {
          return true;
        }
        return false;
      }
      else
      {
        return true;
      }
    }


    public static void ForceAuthRequest(HttpContext context)
    {
      if (Utility.TryParse<bool>(IKGD_Config.AppSettings["ProxyVFS_AllowRedirectToLogin"], true))
      {
        context.Response.Buffer = false;
        context.Response.BufferOutput = false;
        context.Response.ClearContent();
        context.Response.ClearHeaders();
        FormsAuthentication.RedirectToLoginPage();
        context.Response.End();
      }
    }


    public class ProxyVFS_CacheElement
    {
      public bool? IsOnVFS { get; set; }
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
        return Utility.Implode(new object[] { "ProxyVFS_CacheElement_", path ?? string.Empty, rnode.GetValueOrDefault(-1), snode.GetValueOrDefault(-1), inode.GetValueOrDefault(-1), relationType ?? "[NULL]", stream ?? "[NULL]", freeze.GetValueOrDefault(int.MaxValue) }, "|");
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
        if (data.Length > ProxyVFS_Helper.CachingObjectSizeMax)
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
          HttpRuntime.Cache.Insert(cacheKeyForData, data, cacheDeps, DateTime.Now.AddSeconds(cacheDurationOnServer.Value), Cache.NoSlidingExpiration, CacheItemPriority.Low, (key, value, reason) => { HttpRuntime.Cache.Remove(cacheKeyMain); });
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
