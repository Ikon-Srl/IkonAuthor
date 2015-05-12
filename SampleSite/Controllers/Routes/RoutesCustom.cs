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


  public class RegisterRoutesCustom : IBootStrapperTask, IBootStrapperPostTask
  {
    private readonly RouteCollection routes;


    public RegisterRoutesCustom()
    {
      routes = RouteTable.Routes;
    }


    public void Execute()
    {
      //
      // parte delle routes sono gia' preconfigurate in Ikon.IKCMS.IKCMS_RegisterBaseRoutesAndIoC.ExecutePre()
      // oppure trattate in RegisterRoutesBase
      //
      
      //routes.MapRoute(
      //  "Custom",
      //  "Custom/{action}/{code}",
      //  new { controller = "Custom", action = "Index", code = UrlParameter.Optional }
      //  );


      //routes.MapRoute(
      //  "Home",
      //  "{action}",
      //  new { controller = "Home", action = "Home" },
      //  new { action = @"(Home|Search|Sitemap)" }
      //);

    }



    public void ExecutePost()
    {

      //routes.MapRoute(
      //  "Default",
      //  "{controller}/{action}/{id}",
      //  new { controller = "Home", action = "Index", id = "" }
      //  //new { controller = "Home", action = "Index", id = "" }
      //);

    }

  }

}
