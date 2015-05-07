using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Text.RegularExpressions;
using System.Web;
using System.Xml.Linq;
using System.Web.Mvc;
using System.Web.Mvc.Html;
using System.Reflection;
using System.Runtime.Serialization;
using LinqKit;
using Autofac;

using Ikon;
using Ikon.GD;
using Ikon.IKGD.Library;


namespace Ikon.IKCMS
{

  /*
  using System.Workflow.Runtime;
  
  public static class WorkflowHelper
  {
    //
    private static object _lock;
    //
    private static System.Workflow.Runtime.WorkflowRuntime _workflowRuntime;
    //
    public static bool Enabled { get; private set; }
    public static bool Initialized { get; private set; }
    //
    public static string ConnectionString { get { return IKGD_DBH.ConnectionString; } }
    //


    static WorkflowHelper()
    {
      _lock = new object();
      _workflowRuntime = null;
      Initialized = false;
      Enabled = Utility.TryParse<bool>(IKGD_Config.AppSettings["WorkflowRuntimeEnabled"], false);
    }


    public static System.Workflow.Runtime.WorkflowRuntime workflowRuntime
    {
      get
      {
        lock (_lock)
        {
          if (Enabled && !Initialized)
          {
            StartEngine();
          }
          return _workflowRuntime;
        }
      }
    }


    public static void StartEngine()
    {
      lock (_lock)
      {
        if (Initialized)
        {
          return;
        }
        //
        try
        {
          //
          // evitiamo delle inizializzazioni continue in caso di failure
          Initialized = true;
          //
          // Create the workflow runtime.
          _workflowRuntime = new System.Workflow.Runtime.WorkflowRuntime();
          //
          // Add the tracking service.
          _workflowRuntime.AddService(new System.Workflow.Runtime.Tracking.SqlTrackingService(ConnectionString));
          //
          // Add the scheduling service.
          _workflowRuntime.AddService(new System.Workflow.Runtime.Hosting.ManualWorkflowSchedulerService());
          //
          // Add the persistence service.
          NameValueCollection parameters = new NameValueCollection();
          parameters.Add("ConnectionString", ConnectionString);
          parameters.Add("UnloadOnIdle", "true");
          _workflowRuntime.AddService(new System.Workflow.Runtime.Hosting.SqlWorkflowPersistenceService(parameters));
          //_workflowRuntime.AddService(new System.Workflow.Runtime.Hosting.SqlWorkflowPersistenceService(connectionString, true, new TimeSpan(1, 0, 0), new TimeSpan(0, 0, 5)));
          //
          // Add the shared connection service.
          _workflowRuntime.AddService(new System.Workflow.Runtime.Hosting.SharedConnectionWorkflowCommitWorkBatchService(ConnectionString));
          //
          // Add the communication service.
          System.Workflow.Activities.ExternalDataExchangeService dataService = new System.Workflow.Activities.ExternalDataExchangeService();
          _workflowRuntime.AddService(dataService);
          // es.
          //dataService.AddService(new WorkflowLibrary.LoanEventService());
          //
          // Set up the WorkflowRuntime event handlers
          _workflowRuntime.WorkflowCompleted += OnWorkflowCompleted;
          _workflowRuntime.WorkflowIdled += OnWorkflowIdled;
          _workflowRuntime.WorkflowPersisted += OnWorkflowPersisted;
          _workflowRuntime.WorkflowUnloaded += OnWorkflowUnloaded;
          _workflowRuntime.WorkflowLoaded += OnWorkflowLoaded;
          _workflowRuntime.WorkflowTerminated += OnWorkflowTerminated;
          _workflowRuntime.WorkflowAborted += OnWorkflowAborted;
          //
          // Start the workflow runtime.
          // (The workflow runtime starts automatically when workflows are started, but 
          // will not start automatically when tracking data are requested.)
          _workflowRuntime.StartRuntime();
          //
        }
        catch { }
      }
    }


    public static void StopEngine()
    {
      lock (_lock)
      {
        if (!Initialized || _workflowRuntime == null)
        {
          return;
        }
        //
        try
        {
          _workflowRuntime.StopRuntime();
          _workflowRuntime = null;
          Initialized = false;
        }
        catch { }
        //
      }
    }


    //
    // var res = WorkflowHelper.RunInstance<System.Workflow.Activities.SequentialWorkflowActivity>();
    //
    public static bool RunInstance<T>() where T : System.Workflow.ComponentModel.Activity
    {
      bool result = false;
      if (!Enabled)
      {
        return result;
      }
      try
      {
        System.Workflow.Runtime.Hosting.ManualWorkflowSchedulerService manualScheduler = workflowRuntime.GetService(typeof(System.Workflow.Runtime.Hosting.ManualWorkflowSchedulerService)) as System.Workflow.Runtime.Hosting.ManualWorkflowSchedulerService;
        System.Workflow.Runtime.WorkflowInstance instance = workflowRuntime.CreateWorkflow(typeof(T));
        instance.Start();
        result = manualScheduler.RunWorkflow(instance.InstanceId);
      }
      catch { }
      return result;
    }




    //
    //It is good practice to provide a handler for the WorkflowTerminated event
    // so the host application can manage unexpected problems during workflow execution
    // such as database connectivity issues, networking issues, and so on.
    public static void OnWorkflowTerminated(object sender, WorkflowTerminatedEventArgs e)
    {
      Console.WriteLine(e.Exception.Message);
    }

    //
    //Called when the workflow is loaded back into memory - in this sample this occurs when the timer expires
    public static void OnWorkflowLoaded(object sender, WorkflowEventArgs e)
    {
      Console.WriteLine("Workflow was loaded.");
    }

    //
    //Called when the workflow is unloaded from memory - in this sample the workflow instance is unloaded by the application
    // in the UnloadInstance method below.
    public static void OnWorkflowUnloaded(object sender, WorkflowEventArgs e)
    {
      Console.WriteLine("Workflow was unloaded.");
    }

    //
    //Called when the workflow is persisted - in this sample when it is unloaded and completed
    public static void OnWorkflowPersisted(object sender, WorkflowEventArgs e)
    {
      Console.WriteLine("Workflow was persisted.");
    }

    //
    //Called when the workflow is idle - in this sample this occurs when the workflow is waiting on the
    // delay1 activity to expire.
    public static void OnWorkflowIdled(object sender, WorkflowEventArgs e)
    {
      Console.WriteLine("Workflow is idle.");
      e.WorkflowInstance.TryUnload();
    }

    //
    // This method is called when a workflow instance is completed; because only a single instance is 
    // started, the event arguments are ignored and the waitHandle is signaled so the main thread can continue.
    public static void OnWorkflowCompleted(object sender, WorkflowCompletedEventArgs instance)
    {
    }

    //
    public static void OnWorkflowAborted(object sender, WorkflowEventArgs e)
    {
      Console.WriteLine("Workflow aborted: Please check database connectivity");
    }


  }
  */

}
