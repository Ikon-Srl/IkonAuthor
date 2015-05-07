using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Web;
using System.Web.Mvc;
using System.Web.Routing;
using System.Configuration;
using LinqKit;
using Autofac;

using Ikon;
using Ikon.GD;
using Ikon.IKCMS;
using IkonWeb.Auth.Controllers;


namespace Ikon.IKCMS.Custom
{


  public class RegisterRoutesBase : IBootStrapperTask, IBootStrapperPostTask
  {
    private readonly RouteCollection routes;


    public RegisterRoutesBase()
    {
      routes = RouteTable.Routes;
    }


    public void Execute()
    {
      //
      // parte delle routes sono gia' preconfigurate in Ikon.IKCMS.IKCMS_RegisterBaseRoutesAndIoC.ExecutePre()
      //
      routes.IgnoreRoute("Content/{*pathInfo}");
      routes.IgnoreRoute("Data/{*pathInfo}");
      routes.IgnoreRoute("Scripts/{*pathInfo}");
      routes.IgnoreRoute("Services/{*pathInfo}");
      //


      // non conviene usare l'approccio MVC che presenta sensibili overhead di processing nella pipeline
      routes.MapRoute(
        "ProxyVFS.axd",
        new Regex(@"ProxyVFS(\.axd){0,1}$", RegexOptions.IgnoreCase | RegexOptions.Singleline),
        new { controller = "ProxyVFS", action = "Load" }
      );

      // attenzione a non usare frammenti che sembrano un estensione di file altrimenti si va ad incasinare il meccanismo di mapping degli handler
      routes.MapRoute(
        "ProxyVFS",
        new Regex(@"^VFS(\.axd){0,1}/(?<dataOrPath>.+?)$", RegexOptions.IgnoreCase | RegexOptions.Singleline),
        new { controller = "ProxyVFS", action = "Stream" }
      );


      // --> /{Language}/{xNodeItem}[/{xNodeModule}][/{indexPath}]
      // xNode* --> sNode:12345  rNode:r12345
      routes.MapRoute(
        "IKCMS_Page_byCodeWithLanguage",
        new Regex(@"^(?<Language>([a-z]{2}|code|rnode))/(?<xNodeItem>[rR]{0,1}[\d]+)(/(?<xNodeModule>[rR]{0,1}[\d]+)){0,1}(?<indexPath>.*)$", RegexOptions.IgnoreCase | RegexOptions.Singleline),
        new { controller = "IKCMS", action = "PageCMS_byCodeWithLanguage", moduleOp = "auto", Language = "code" }
      );


      // --> /CMS/...{modulePath}.../(index|item)/...{subIndexPath}...[?page=...][&view=]
      routes.MapRoute(
        "IKCMS_BrowserIndexItem",
        new Regex(@"^CMS/(?<modulePath>.+)/(?<moduleOp>(index|item))/(?<indexPath>.*)$", RegexOptions.IgnoreCase | RegexOptions.Singleline),
        new { controller = "IKCMS", action = "BrowserIndexCMS", moduleOp = "auto" }
      );


      // --> /CMS/...{pagePath}...[?page=...][&view=]
      routes.MapRoute(
        "IKCMS_Page_byPath",
        new Regex(@"^CMS/(?<pagePath>.+?)$", RegexOptions.IgnoreCase | RegexOptions.Singleline),
        new { controller = "IKCMS", action = "PageCMS_byPath" }
      );


      // ~/StaticPage/{*pathInfo}  -->  carica la view (solo nome file e non path) cercandola nel folder ~/Views/Custom/Page
      // ~/StaticPartial/{*pathInfo}  -->  carica la partial view (solo nome file e non path) cercandola nel folder ~/Views/Custom/Page
      routes.MapRoute(
        "StaticPage", "StaticPage/{*pathInfo}",
        new { controller = "IKCMS", action = "StaticPage" }
      );
      routes.MapRoute(
        "StaticPartial", "StaticPartial/{*pathInfo}",
        new { controller = "IKCMS", action = "StaticPartial" }
      );


      routes.MapRoute(
        "404", "404/{*pathInfo}",
        new { controller = "IKCMS", action = "NotFound" }
      );


      // search engine
      routes.MapRoute(
        "SearchCMS",
        "SearchCMS",
        new { controller = "IKCMS", action = "SearchCMS" }  // passare la query con ?searchCMS=...
      );


      routes.MapRoute(
        "RSS",
        "RSS/{action}/{moduleCode}/{maxItems}",
        new { controller = "RSS_Generator", action = "Index", moduleCode = UrlParameter.Optional, maxItems = UrlParameter.Optional },
        new { action = @"(Index|Full|Mini|IndexAtom)" }
      );


      routes.MapRoute(
        "ESurvey",
        "ESurvey/{action}/{moduleCode}",
        new { controller = "ESurvey", action = "Index", moduleCode = UrlParameter.Optional },
        new { action = @"(Index|Display|Vote|Summary|ExportXML)" }
      );


      routes.MapRoute(
        "Batch",
        "Batch/{action}",
        new { controller = "Batch", action = "Index" }
      );


      routes.MapRoute(
        "Logger",
        "Logger/{action}",
        new { controller = "Logger", action = "Index" }
      );


      routes.MapRoute(
        "Custom",
        "Custom/{action}/{code}",
        new { controller = "Custom", action = "Index", code = UrlParameter.Optional }
      );


      routes.MapRoute(
        "SitemapCMS",
        "SitemapCMS",
        new { controller = "Search", action = "SiteMapXml" },  //attenzione il controller BatchCMS e' vietato ai bots
        new { action = @"(SiteMapXml)" }
      );


      routes.MapRoute(
        "Home",
        "{action}",
        new { controller = "Home", action = IKGD_Config.AppSettings["ControllerHomeActionDefault"] ?? "Home" },
        new { action = @"(Home|Sitemap|Index|Home_DEBUG)" }
      );


    }



    public void ExecutePost()
    {
      //
      // mappa un path generico direttamente al CMS senza prefisso
      // deve essere specificato alla file delle routes
      //

      routes.MapRoute(
        "Default",
        "{controller}/{action}/{id}",
        new { controller = "Home", action = "Index", id = UrlParameter.Optional }
      );

    }

  }

}
