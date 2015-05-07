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
using System.Xml.Linq;
using System.Data.Linq;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Security;
using System.Linq.Expressions;
using System.Reflection;
using System.Data.Linq.Mapping;
using System.Diagnostics;
using System.Transactions;
using System.Web.Caching;
using System.Web.Mvc;
using System.Web.Mvc.Html;
using System.Web.Routing;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;  // assembly System.ServiceModel.Web
using LinqKit;
using Autofac;
using Autofac.Integration.Web;

using Ikon;
using Ikon.GD;


namespace Ikon.IKCMS
{

  public class IKCMS_RegisterBaseRoutesAndIoC : IBootStrapperAutofacTask, IBootStrapperPreTask
  {

    public void ExecuteAutofac(ContainerBuilder builder)
    {
      //var fsOp = IkonWeb.Main.MvcApplication.ContainerProviderStatic.RequestLifetime.Resolve<FS_Operations>();
      builder.Register(fsOp => new Ikon.GD.FS_Operations()).InstancePerHttpRequest();
    }


    public void ExecutePre()
    {
      RouteCollection routes  = RouteTable.Routes;
      //
      routes.RouteExistingFiles = false;
      //routes.Clear();
      //
      routes.IgnoreRoute("{file}.wgx");
      routes.IgnoreRoute("Author/{*pathInfo}");
      routes.IgnoreRoute("{resource}.axd/{*pathInfo}");
      routes.IgnoreRoute("{*favicon}", new { favicon = @"(.*/)?favicon.(ico|gif)(/.*)?" });
      //routes.IgnoreRoute("{*allaspx}", new { allaspx = @".*\.aspx(/.*)?" });
      //
      if (Utility.TryParse<bool>(IKGD_Config.AppSettings["EnableRoutingForHtmlTxt"], false) == false)
      {
        routes.IgnoreRoute("{*allfiles}", new { allfiles = @".*\.(txt|htm|html)(/.*)?" });
        //
        //routes.IgnoreRoute("{file}.txt");
        //routes.IgnoreRoute("{file}.htm");
        //routes.IgnoreRoute("{file}.html");
      }
      //
      Utility.Explode(IKGD_Config.AppSettings["IgnoreRouteCustom"], "|", " ", true).ForEach(r => routes.IgnoreRoute(r));
      //
      //var tmp01 = routes.OfType<System.Web.Routing.Route>().Select(r => r.Url).ToList();
      //
    }

  }



  public class RegexRoute : Route
  {
    private readonly Regex _urlRegex;


    public RegexRoute(Regex urlPattern, IRouteHandler routeHandler)
      : this(urlPattern, null, routeHandler)
    {
    }


    public RegexRoute(Regex urlPattern, RouteValueDictionary defaults, IRouteHandler routeHandler)
      : base(null, defaults, routeHandler)
    {
      _urlRegex = urlPattern;
    }


    // HttpContextBase e' definito in System.Web.Abstractions
    public override RouteData GetRouteData(HttpContextBase httpContext)
    {
      var requestUrl = httpContext.Request.AppRelativeCurrentExecutionFilePath.Substring(2) + httpContext.Request.PathInfo;
      var match = _urlRegex.Match(requestUrl);
      RouteData data = null;
      if (match.Success)
      {
        data = new RouteData(this, RouteHandler);
        // add defaults first
        if (null != Defaults)
        {
          foreach (var def in Defaults)
          {
            data.Values[def.Key] = def.Value;
          }
        }
        // iterate matching groups
        for (var i = 1; i < match.Groups.Count; i++)
        {
          var group = match.Groups[i];
          if (!group.Success) continue;
          var key = _urlRegex.GroupNameFromNumber(i);
          if (!String.IsNullOrEmpty(key) && !Char.IsNumber(key, 0))
          {
            data.Values[key] = group.Value;
          }
        }
      }
      return data;
    }


    public override VirtualPathData GetVirtualPath(RequestContext requestContext, RouteValueDictionary values)
    {
      //TODO: generare correttamente la url associata a questo tipo di route
      return base.GetVirtualPath(requestContext, values);
    }

  }


  public static class RegexRouteCollectionExtensions
  {
    public static Route MapRoute(this RouteCollection routes, string name, Regex urlPattern)
    {
      return routes.MapRoute(name, urlPattern, null, null);
    }

    public static Route MapRoute(this RouteCollection routes, string name, Regex urlPattern, object defaults)
    {
      return routes.MapRoute(name, urlPattern, defaults, null);
    }

    public static Route MapRoute(this RouteCollection routes, string name, Regex urlPattern, string[] namespaces)
    {
      return routes.MapRoute(name, urlPattern, null, null, namespaces);
    }

    public static Route MapRoute(this RouteCollection routes, string name, Regex urlPattern, object defaults, object constraints)
    {
      return routes.MapRoute(name, urlPattern, defaults, constraints, null);
    }

    public static Route MapRoute(this RouteCollection routes, string name, Regex urlPattern, object defaults, string[] namespaces)
    {
      return routes.MapRoute(name, urlPattern, defaults, null, namespaces);
    }

    public static Route MapRoute(this RouteCollection routes, string name, Regex urlPattern, object defaults, object constraints, string[] namespaces)
    {
      if (routes == null)
      {
        throw new ArgumentNullException("routes");
      }
      if (urlPattern == null)
      {
        throw new ArgumentNullException("urlPattern");
      }
      var route2 = new RegexRoute(urlPattern, new MvcRouteHandler())
      {
        Defaults = new RouteValueDictionary(defaults),
        Constraints = new RouteValueDictionary(constraints)
      };
      var item = route2;
      if ((namespaces != null) && (namespaces.Length > 0))
      {
        item.DataTokens = new RouteValueDictionary();
        item.DataTokens["Namespaces"] = namespaces;
      }
      routes.Add(name, item);
      return item;
    }

  }

}