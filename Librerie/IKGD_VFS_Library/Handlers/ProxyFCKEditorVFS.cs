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

  public class ProxyFCKEditorVFS : IHttpHandler, IReadOnlySessionState
  {
    public bool IsReusable { get { return false; } }


    public void ProcessRequest(HttpContext context)
    {
      // Get the main request information.
      string sCommand = context.Request.QueryString["Command"];
      string sResourceType = context.Request.QueryString["Type"];
      string sCurrentFolder = context.Request.QueryString["CurrentFolder"];

      //
      // filtro sui tipi di risorsa
      // TODO: sistemare le regex per i mime types supportati
      //
      Regex streamType = null;
      switch (sResourceType)
      {
        case "Image":
          streamType = new Regex(@"^image/", RegexOptions.IgnoreCase);
          break;
        case "Flash":
          streamType = new Regex(@"^application/flash", RegexOptions.IgnoreCase);
          break;
        case "Media":
          streamType = new Regex(@"^video/", RegexOptions.IgnoreCase);
          break;
        case "File":
          //streamType = new Regex(@"^application/", RegexOptions.IgnoreCase);
          streamType = new Regex(@".*", RegexOptions.IgnoreCase);
          break;
        //default:
        //  streamType = new Regex(@".*", RegexOptions.IgnoreCase);
        //  break;
      }
      if (streamType == null)
      {
        XmlResponseHandler.SendError(context.Response, 1, "Invalid resource type specified.");
        return;
      }

      // Check the current folder syntax (must begin and start with a slash).
      //if (!sCurrentFolder.EndsWith("/"))
      //  sCurrentFolder += "/";
      //if (!sCurrentFolder.StartsWith("/"))
      //  sCurrentFolder = "/" + sCurrentFolder;

      bool getFiles = false;
      bool getFolders = false;
      // check for valid commands.
      switch (sCommand)
      {
        case "GetFolders":
          getFolders = true;
          break;
        case "GetFoldersAndFiles":
          getFiles = true;
          getFolders = true;
          break;
        default:
          XmlResponseHandler.SendError(context.Response, 102, "Invalid command specified.");
          break;
      }
      //
      XmlResponseHandler oResponseHandler = new XmlResponseHandler(context.Response);
      XElement oConnectorXml = oResponseHandler.CreateBaseXml(sCommand, sResourceType, sCurrentFolder);
      //
      this.GetInfoVFS(sCurrentFolder, streamType, getFiles, getFolders, oConnectorXml);
      //
      oResponseHandler.SendResponse();
      //if (context.Response.IsClientConnected)
      //  context.Response.End();
    }


    protected XElement GetInfoVFS(string pathStr, Regex streamFilter, bool files, bool folders, XElement xInfo)
    {
      //
      // testare con http://localhost:63048/proxyfckeditorvfs.axd?Type=Image&Command=GetFolders&CurrentFolder=/Root/WbSite
      //
      using (FS_Operations fsOp = new FS_Operations())
      {
        try
        {
          List<IKGD_Path> paths = fsOp.PathsFromString(pathStr, true, true);
          IKGD_Path path = paths.FilterPathsByLanguage().FirstOrDefault() ?? paths.FirstOrDefault();
          var fsNodes = fsOp.Get_FolderContentsInfo(path.sNode, null, files, folders, true, true, false).ToList();
          if (folders)
          {
            XElement xFolders = new XElement("Folders");
            xInfo.Add(xFolders);
            foreach (var fsNode in fsNodes.Where(n=>n.vNode.flag_folder))
              xFolders.Add(new XElement("Folder", new XAttribute("name", fsNode.vNode.name)));
          }
          if (files)
          {
            XElement xFiles = new XElement("Files");
            xInfo.Add(xFiles);
            var inodes = fsNodes.Where(n => !n.vNode.flag_folder && n.iNode != null).Select(n=>n.iNode.version).ToList();
            var streamsData = fsOp.DB.IKGD_STREAMs.Where(s => inodes.Contains(s.inode.Value)).Select(s => new { inode=s.inode, key=s.key, type=s.type, size=s.data.Length }).ToLookup(s=>s.inode);
            foreach (var fsNode in fsNodes.Where(n => !n.vNode.flag_folder && n.iNode != null))
            {
              try
              {
                foreach (var streamVFS in streamsData[fsNode.iNode.version])
                {
                  if (!streamFilter.IsMatch(streamVFS.type))
                    continue;
                  //TODO: provare ad estrarre l'xml registrato durante il processing con le info estese sugli stream
                  string name = string.Format("{0} {{{1}}}", fsNode.vNode.name, streamVFS.key.DefaultIfEmpty("originale"));
                  string size = (streamVFS.size/1024.0).ToString("f1");
                  string url = Utility.ResolveUrl(string.Format("{0}?snode={1}&stream={2}", (IKGD_Config.AppSettings["HandlerVFS"] ?? "~/ProxyVFS.axd"), fsNode.vNode.snode, streamVFS.key));
                  xFiles.Add(new XElement("File", new XAttribute("url", url), new XAttribute("name", name), new XAttribute("size", size)));
                }
              }
              catch { }
            }
          }
        }
        catch { }
      }
      return xInfo;
    }





    class XmlResponseHandler
    {
      public HttpResponse Response { get; private set; }
      public XElement Xml { get; private set; }

      public XmlResponseHandler(HttpResponse response)
      {
        Response = response;
        Xml = new XElement("Connector");
        SetupResponse(response);
      }


      private static void SetupResponse(HttpResponse response)
      {
        response.ClearHeaders();
        response.Clear();
        response.CacheControl = "no-cache";
        response.ContentEncoding = System.Text.UTF8Encoding.UTF8;
        response.ContentType = "text/xml";
      }


      public void SendResponse()
      {
        Response.Write(Xml.ToString());
        //Response.End();
        System.Web.HttpContext.Current.ApplicationInstance.CompleteRequest();  // da usare al posto di .Response.End();
      }


      public static void SendError(HttpResponse response, int errorNumber, string errorText)
      {
        SetupResponse(response);
        XElement xErr = new XElement("Connector", new XElement("Error", new XAttribute("number", errorNumber), new XAttribute("text", errorText)));
        response.Write(xErr.ToString());
        //response.End();
        System.Web.HttpContext.Current.ApplicationInstance.CompleteRequest();  // da usare al posto di .Response.End();
      }


      public XElement CreateBaseXml(string command, string resourceType, string currentFolder)
      {
        //string url = Connector.GetUrlFromPath(resourceType, currentFolder);
        string url = currentFolder;
        //
        Xml.SetAttributeValue("command", command);
        Xml.SetAttributeValue("resourceType", resourceType);
        Xml.Add(new XElement("CurrentFolder", new XAttribute("path", currentFolder), new XAttribute("url", url)));
        //
        return Xml;
      }


    }

  }


}
