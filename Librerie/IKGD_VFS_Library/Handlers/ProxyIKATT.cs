/*
 * 
 * Intranet Ikon
 * 
 * Copyright (C) 2014 Ikon Srl
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

  public class ProxyIKATT : IHttpHandler
  {
    public bool IsReusable { get { return true; } }


    public void ProcessRequest(HttpContext context)
    {
      int? idAttr = Utility.TryParse<int?>(context.Request["id"], null);
      string path = context.Request["path"];
      string stream = context.Request["stream"];
      string contentType = context.Request["mime"];
      bool forceDownload = context.Request.Params.AllKeys.Contains("forceDownload");
      string defaultResource = context.Request["default"];
      string indexPath = null;
      ProxyIKATT_Helper.AttributeType? attrType = null;
      //
      if (context.Request.PathInfo.IsNotEmpty())
      {
        string pathInfo = Utility.UrlDecodePath_IIS(context.Request.PathInfo);
        string[] frags = pathInfo.TrimSafe('/', ' ').Split("/".ToCharArray(), 3);
        string streamFromPath = frags.FirstOrDefault().TrimSafe(' ');
        string node = frags.Skip(1).FirstOrDefault().TrimSafe(' ');
        indexPath = frags.Skip(2).FirstOrDefault();
        if (Regex.IsMatch(node, @"^(a|c){0,1}\d+$", RegexOptions.Singleline | RegexOptions.IgnoreCase))
        {
          if (node.StartsWith("a", StringComparison.OrdinalIgnoreCase))
          {
            idAttr = idAttr ?? Utility.TryParse<int?>(node.Substring(1));
            attrType = ProxyIKATT_Helper.AttributeType.IKATT;
          }
          else if (node.StartsWith("c", StringComparison.OrdinalIgnoreCase))
          {
            idAttr = idAttr ?? Utility.TryParse<int?>(node.Substring(1));
            attrType = ProxyIKATT_Helper.AttributeType.IKCAT;
          }
          else
          {
            idAttr = idAttr ?? Utility.TryParse<int?>(node);
            attrType = ProxyIKATT_Helper.AttributeType.IKATT;
          }
          stream = stream ?? (streamFromPath == "null" ? "," : streamFromPath);
        }
      }
      //
      var res01 = Ikon.Handlers.ProxyIKATT_Helper.ProcessStreamRequest(context, new Ikon.Handlers.ProxyIKATT_Helper.IKATT_Args
      {
        IdAttr = idAttr,
        Key = stream,
        pathInfo = indexPath,
        cacheDurationServer = Utility.TryParse<int?>(context.Request["cacheServer"], null),
        cacheDurationBrowser = Utility.TryParse<int?>(context.Request["cacheBrowser"], null),
        defaultResource = defaultResource,
        forceDownload = forceDownload,
        AttrType = attrType
      });
    }

  }



}
