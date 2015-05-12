using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Web;
using System.Web.Mvc;
using System.Web.Routing;
using Spark;
using Spark.Web.Mvc;
using Spark.Web.Mvc.Descriptors;
using System.Reflection;
using System.Configuration;
using Autofac;
using Autofac.Integration.Web;
using LinqKit;

using Ikon;
using Ikon.GD;
using Ikon.IKCMS;


namespace IkonWeb.Main
{

  public class MvcApplication : System.Web.HttpApplication, IContainerProviderAccessor
  {
    public IContainerProvider ContainerProvider { get { return Ikon.IKCMS.IKCMS_ManagerIoC.containerProvider; } }


    //protected void Application_Start(object sender, EventArgs e) { }
    protected void Application_Start()
    {
      //
      Ikon.IKCMS.IKCMS_ManagerIoC.Setup(builder =>
      {
        //builder.Register(fsOp => new Ikon.GD.FS_Operations()).InstancePerHttpRequest();
        //
        // e' necessario registrare manualmente i controllers presenti negli altri assembly non marchiati con IKGD_Assembly_WithControllersAttribute
        //builder.RegisterControllers(typeof(IKCMSControllerBase).Assembly);
        //
      });
      // usage:
      // var fsOp = Ikon.IKCMS.IKCMS_ManagerIoC.requestContainer.Resolve<FS_Operations>();
      //
      AreaRegistration.RegisterAllAreas();
      //
      ControllerBuilder.Current.SetControllerFactory(new Microsoft.Web.Mvc.MvcDynamicSessionControllerFactory());
      //
      Ikon.IKCMS.SparkIKCMS.IKCMS_SparkInitializer.RegisterSparkViewEngine();
      //
      Ikon.IKCMS.Bootstrapper.Run();
      //
    }

    
    //
    // in questo handler e' possibile cambiare
    // CurrentCulture e CurrentUICulture per CurrentThread
	// http://adamyan.blogspot.com/2010/02/aspnet-mvc-2-localization-complete.html
    // per quanto riguarda il riconoscimento di culture e region fare riferimento a
    // http://madskristensen.net/post/Get-language-and-country-from-a-browser-in-ASPNET.aspx
    //
    protected void Application_AcquireRequestState(object sender, EventArgs e)
    {
      IKCMS_ExecutionProfiler.AddMessage("Application_AcquireRequestState: BEGIN");
      if (IKCMS_RouteUrlManager.IsCultureSetupAllowed(HttpContext.Current.Request.Path))
      {
        IKGD_Language_Provider.Provider.AutoConfig(IKGD_Language_Provider.LanguageAutoconfigEnum.BrowserFallBack, "it");
      }
      IKCMS_ExecutionProfiler.AddMessage("Application_AcquireRequestState: END");
    }



    protected void Application_BeginRequest(object sender, EventArgs e)
    {
      IKCMS_ExecutionProfiler.AddMessage("Application_BeginRequest");
    }

    protected void Application_PreRequestHandlerExecute(object sender, EventArgs e)
    {
      IKCMS_ExecutionProfiler.AddMessage("Application_PreRequestHandlerExecute");
    }

    protected void Application_PostRequestHandlerExecute(object sender, EventArgs e)
    {
      IKCMS_ExecutionProfiler.AddMessage("Application_PostRequestHandlerExecute");
    }

    protected void Application_ReleaseRequestState(object sender, EventArgs e)
    {
      IKCMS_ExecutionProfiler.AddMessage("Application_ReleaseRequestState");
      if (IKCMS_ExecutionProfiler.EnableOutput && HttpContext.Current.Response.Buffer == true)
      {
        HttpContext.Current.Response.Output.Write("<div class='clearfloat'></div><hr/>\n");
        HttpContext.Current.Response.Output.Write(IKCMS_ExecutionProfiler.DumpMessages());
      }
    }

    //viene chiamato prima di EndRequest solo se il buffering e' disattivato
    protected void Application_PreSendRequestContent(object sender, EventArgs e)
    {
      IKCMS_ExecutionProfiler.AddMessage("Application_PreSendRequestContent");
    }

    protected void Application_EndRequest(object sender, EventArgs e)
    {
      IKCMS_ExecutionProfiler.AddMessage("Application_EndRequest");
      if (IKCMS_ExecutionProfiler.EnableOutput && HttpContext.Current.Response.Buffer == false)
      {
        HttpContext.Current.Response.Output.Write("<div class='clearfloat'></div><hr/>\n");
        HttpContext.Current.Response.Output.Write(IKCMS_ExecutionProfiler.DumpMessages());
      }
    }


    protected void Application_Error(object sender, EventArgs e)
    {
    }


    protected void Application_End(object sender, EventArgs e)
    {
      if (IKGD_QueueManager.IsAsyncProcessingEnabled)
      {
        IKGD_QueueManager.Stop();
      }
      IKCMS_HitLogger.Flush(true);
    }


    public override string GetVaryByCustomString(HttpContext context, string custom)
    {
      if (custom == "CacheIKCMS")
      {
        string hash = FS_OperationsHelpers.ContextHashNN(context.User.Identity.IsAuthenticated);
        return hash;
      }
      return base.GetVaryByCustomString(context, custom);
    }


  }
}