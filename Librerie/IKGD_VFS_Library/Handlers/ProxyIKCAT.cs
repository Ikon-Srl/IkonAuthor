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



namespace Ikon.Handlers
{

  //
  //
  //
  public class ProxyIKCAT : IHttpHandler
  {
    public bool IsReusable { get { return true; } }


    public void ProcessRequest(HttpContext context)
    {
      //
      //int? rNodeCode = Utility.TryParse<int?>(context.Request["rnode"], null);
      //int? sNodeCode = Utility.TryParse<int?>(context.Request["snode"], null);
      //int? iNodeCode = Utility.TryParse<int?>(context.Request["inode"], null);
      //int? freeze = Utility.TryParse<int?>(context.Request["freeze"], null);
      //string relationType = context.Request["relationType"];
      //string path = context.Request["path"] ?? context.Request.PathInfo;
      //string stream = context.Request["stream"];
      //string contentType = context.Request["mime"];
      ////
      //int? cacheDurationServer = Utility.TryParse<int?>(context.Request["cacheServer"], null);
      //if (Utility.TryParse<bool>(IKGD_Config.AppSettings["CachingProxyVFS_DataEnabled"], true) && cacheDurationServer == null)
      //  cacheDurationServer = Utility.TryParse<int?>(IKGD_Config.AppSettings["CachingFilesExpiry"] ?? "600", cacheDurationServer);
      //int? cacheDurationBrowser = Utility.TryParse<int?>(context.Request["cacheBrowser"], null);
      //if (Utility.TryParse<bool>(IKGD_Config.AppSettings["CachingProxyVFS_BrowserEnabled"], true) && cacheDurationBrowser == null)
      //  cacheDurationBrowser = Utility.TryParse<int?>(IKGD_Config.AppSettings["CachingProxyVFS_Browser"] ?? "86400", cacheDurationBrowser);
      ////
      //string defaultResource = context.Request["default"];
      //if (!string.IsNullOrEmpty(path))
      //  path = Utility.UrlDecodePath_IIS(path);
      ////
      //ProxyVFS_Helper.ProxyVFS_Request(context,
      //  path, rNodeCode, sNodeCode, iNodeCode, freeze,
      //  relationType, stream, contentType,
      //  cacheDurationServer, cacheDurationBrowser,
      //  defaultResource);
      //
      //context.Response.End();
      context.ApplicationInstance.CompleteRequest();  // da usare al posto di .Response.End();
    }

  }



  public static class ProxyIKCAT_Helper
  {

    public static void ProxyIKCAT_Request(HttpContext context, int attributeId, string stream, int? cacheDurationBrowser, string defaultResource)
    {
      string mimeType = string.Empty;
      string fileName = string.Empty;
      string ETag = string.Empty;
      //
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
        byte[] data = ReadDataFromDB(context, attributeId, stream, out fileName, out mimeType, out ETag);
        if (data == null && !context.Response.IsClientConnected)
          return;
        if (data == null)
          throw new Exception(mimeType);  // contiene l'eccezione generata da ProxyVFS.ReadDataFromVFS
        //
        if (!string.IsNullOrEmpty(mimeType))
          context.Response.ContentType = mimeType;
        //
        // per i mime type non riconosciuti genera un header di download
        //
        fileName = fileName ?? string.Empty;
        string inlineMode = "inline";
        if (!string.IsNullOrEmpty(fileName) && (string.IsNullOrEmpty(mimeType) || mimeType == "application/octet-stream"))
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
        //context.ApplicationInstance.ApplicationInstance.CompleteRequest();  // da usare al posto di .Response.End();
      }
      catch (Exception ex)
      {
        //
        //ResourceNotFound(context, defaultResource, mimeType, ex.Message);
        if (!string.IsNullOrEmpty(defaultResource))
        {
          context.Response.StatusCode = 302;
          context.Response.Redirect(defaultResource, false);
          //context.Response.End();
          context.ApplicationInstance.CompleteRequest();  // da usare al posto di .Response.End();
        }
        else
        {
          context.Response.Write(ex.Message);
        }
      }
    }


    public static byte[] ReadDataFromDB(HttpContext context, int attributeId, string stream, out string fileName, out string contentType, out string ETag)
    {
      fileName = string.Empty;
      contentType = string.Empty;
      ETag = string.Empty;
      byte[] data = null;
      //
      stream = Utility.StringTruncate(stream ?? string.Empty, 250);
      //
      try
      {
        using (IKGD_DataContext DB = IKGD_DBH.GetDB())
        {
          //
          DB.ObjectTrackingEnabled = false;
          //
          // controllo se lo stream e' gia' in cache nel browser
          //
          //try
          //{
          //  if (!string.IsNullOrEmpty(context.Request.Headers["If-Modified-Since"]))
          //  {
          //    DateTime IfModifiedSince = DateTime.Parse(context.Request.Headers["If-Modified-Since"]);
          //    if (IfModifiedSince >= cachedInfo.LastModified || cachedInfo.ETag == context.Request.Headers["If-None-Match"])
          //    {
          //      context.Response.Status = "304 Not Modified";
          //      context.Response.StatusCode = 304;
          //      context.Response.End();
          //      return null;
          //    }
          //  }
          //}
          //catch { }
          //
          //
          SqlCommand sqlCmd = null;
          if (!string.IsNullOrEmpty(stream))
          {
            //SELECT TOP 1 * FROM [IKCAT_AttributeStream] WHERE [AttributeId]=5169 AND [Key]='' ORDER BY id;
            sqlCmd = new SqlCommand("SELECT TOP 1 [Mime],[Data] FROM [IKCAT_AttributeStream] WHERE (([AttributeId]=@AttributeId) AND ([Key]=@Key))", DB.Connection as SqlConnection);
            sqlCmd.Parameters.Add("@AttributeId", SqlDbType.Int).Value = attributeId;
            sqlCmd.Parameters.Add("@Key", SqlDbType.VarChar).Value = stream;
          }
          else
          {
            sqlCmd = new SqlCommand("SELECT TOP 1 [Mime],[Data] FROM [IKCAT_AttributeStream] WHERE (([AttributeId]=@AttributeId)) ORDER BY [Key]", DB.Connection as SqlConnection);
            sqlCmd.Parameters.Add("@AttributeId", SqlDbType.Int).Value = attributeId;
          }
          if (DB.Connection.State == ConnectionState.Closed)
            DB.Connection.Open();
          sqlCmd.CommandTimeout = Math.Max(sqlCmd.CommandTimeout, Utility.TryParse<int>(IKGD_Config.AppSettings["ProxyVFS_TimeoutDB"], 300));
          //
          //using (SqlDataReader reader = sqlCmd.ExecuteReader(CommandBehavior.CloseConnection | CommandBehavior.SequentialAccess))
          using (SqlDataReader reader = sqlCmd.ExecuteReader(CommandBehavior.CloseConnection))
          {
            if (reader.Read())
            {
              contentType = reader[0].ToString();
              ETag = string.Format("IKCAT_AttributeStream/{0}/{1}", attributeId, stream);
              //
              data = (byte[])reader[1];
              reader.Close();
              //
              if (contentType == "application/octetstream")
                contentType = "application/octet-stream";
              contentType = contentType.DefaultIfEmpty("application/octet-stream").ToLower();
            }
            else
            {
              throw new Exception("Stream non disponibile per la risorsa richiesta.");
            }
          }
          DB.Connection.Close();
        }
      }
      catch (Exception ex)
      {
        contentType = ex.Message;
      }
      return data;
    }


  }

}
