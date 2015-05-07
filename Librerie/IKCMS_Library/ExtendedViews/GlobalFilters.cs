/*
 * 
 * Copyright (C) 2012 Ikon Srl
 * Tutti i Diritti Riservati. All Rights Reserved
 * 
 */


using System;
using System.Data;
using System.Configuration;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Web;
using System.Web.UI;
using System.Web.Security;
using System.Xml.Linq;
using System.Data.Linq;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Reflection;
using System.Data.Linq.Mapping;
using System.Diagnostics;
using System.Linq.Dynamic;
using System.Transactions;
using System.Web.Caching;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;  // assembly System.ServiceModel.Web
using System.Web.Mvc;

using LinqKit;
using Autofac;

using Ikon;
using Ikon.GD;
using Ikon.Log;


namespace Ikon.IKCMS
{
  using FluentFilters;
  using FluentFilters.Criteria;


  public class RegisterGlobalActionFilters : IBootStrapperPostTask
  {

    public void ExecutePost()
    {
      // per disabilitare FluentFiltersControllerFactory e MV_ResponseFilterPostProcessorStream
      // usati per la gestione di use/content senza lo spark view engine
      // attenzione che la funzionalita' non e' compatibile con le parenzate del sito Li.m.oni
      if (Utility.TryParse<bool>(IKGD_Config.AppSettings["FluentFiltersControllerFactoryEnabled"], false))  // TODO alla fine dello sviluppo mettere a true per default
      {
        RegisterFluentFilters();
        ControllerBuilder.Current.SetControllerFactory(new Microsoft.Web.Mvc.MvcDynamicSessionControllerFactory(new FluentFiltersControllerFactory()));
      }
    }


    private static void RegisterFluentFilters()
    {
      FluentFiltersBuilder.Current.Add<MV_ViewPostProcessorFilterAttribute>();
    }

  }


  public class MV_ViewPostProcessorFilterAttribute : IResultFilter
  {

    public void OnResultExecuting(ResultExecutingContext filterContext)
    {
      if (string.Equals(filterContext.HttpContext.Response.ContentType, "text/html", StringComparison.OrdinalIgnoreCase))
      {
        try { filterContext.HttpContext.Response.Filter = new MV_ResponseFilterPostProcessorStream(filterContext.HttpContext.Response); }
        catch { }
      }
    }


    public void OnResultExecuted(ResultExecutedContext filterContext)
    {
    }

  }


  //
  // http://stackoverflow.com/questions/10591651/asp-net-mvc-3-4-equivalent-to-a-response-filter
  //
  /*
  public class ReplaceTagsAttribute : ActionFilterAttribute
  {
    public override void OnActionExecuting(ActionExecutingContext filterContext)
    {
      var response = filterContext.HttpContext.Response;
      response.Filter = new MV_ResponseFilterPostProcessorStream_02(response.Filter);
    }
  }
  */

}

