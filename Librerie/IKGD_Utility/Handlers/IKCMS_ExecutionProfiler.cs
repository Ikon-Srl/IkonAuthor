/*
 * 
 * Intranet Ikon
 * 
 * Copyright (C) 2010 Ikon Srl
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
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;  // assembly System.ServiceModel.Web
using LinqKit;

using Ikon;


namespace Ikon
{


  public static class IKCMS_ExecutionProfiler
  {
    private static object _lock = new object();
    public static Process CurrentProcess = Process.GetCurrentProcess();


    public static void AddMessage(string message)
    {
      lock (_lock)
      {
        try
        {
          //Stopwatch sw = (HttpContext.Current.Items["sw_profiler"] ?? (HttpContext.Current.Items["sw_profiler"] = Stopwatch.StartNew())) as Stopwatch;
          List<TupleW<TimeSpan, TimeSpan, string>> history = (HttpContext.Current.Items["history_profiler"] ?? (HttpContext.Current.Items["history_profiler"] = new List<TupleW<TimeSpan, TimeSpan, string>>())) as List<TupleW<TimeSpan, TimeSpan, string>>;
          //System.Threading.Thread.CurrentThread
          history.Add(new TupleW<TimeSpan, TimeSpan, string>(DateTime.Now - CurrentProcess.StartTime, CurrentProcess.TotalProcessorTime, message));
          //history.Add(new TupleW<TimeSpan, TimeSpan, string>(DateTime.Now - CurrentProcess.StartTime, System.Diagnostics.ProcessThread.TotalProcessorTime, message));
        }
        catch { }
      }
    }


    public static bool EnableOutput
    {
      get { return (bool)(HttpContext.Current.Items["history_output"] ?? false); }
      set { HttpContext.Current.Items["history_output"] = value; }
    }


    public static string DumpMessages()
    {
      List<TupleW<TimeSpan, TimeSpan, string>> history = null;
      lock (_lock)
      {
        try { history = (HttpContext.Current.Items["history_profiler"] ?? (HttpContext.Current.Items["history_profiler"] = new List<TupleW<TimeSpan, TimeSpan, string>>())) as List<TupleW<TimeSpan, TimeSpan, string>>; }
        catch { }
      }
      List<string> items = new List<string>();
      if (history != null && history.Any())
      {
        try
        {
          TupleW<TimeSpan, TimeSpan, string> firstItem = history.FirstOrDefault();
          TupleW<TimeSpan, TimeSpan, string> lastItem = null;
          foreach (TupleW<TimeSpan, TimeSpan, string> item in history)
          {
            lastItem = lastItem ?? item;
            items.Add(string.Format("[{0:000000.000}]({1:000000.000}) - [{2:000000.000}]({3:000000.000}) - {4}", (item.Item1 - firstItem.Item1).TotalMilliseconds, (item.Item1 - lastItem.Item1).TotalMilliseconds, (item.Item2 - firstItem.Item2).TotalMilliseconds, (item.Item2 - lastItem.Item2).TotalMilliseconds, item.Item3));
            lastItem = item;
          }
        }
        catch { }
      }
      return Utility.Implode(items, "<br/>\n");
    }


  }
}

