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
using System.Linq;
using System.Web;
using System.Web.Security;
using System.Xml.Linq;
using System.Data.Linq;
using System.IO;
using System.Text;
using System.Linq.Expressions;
using System.Reflection;
using System.Data.Linq.Mapping;
using System.Diagnostics;
using System.Linq.Dynamic;
using System.Transactions;
using System.Web.Caching;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;  // assembly System.ServiceModel.Web
using System.Threading;
using LinqKit;

using Ikon;
using Ikon.GD;


namespace Ikon.IKCMS
{

  public static class IKCMS_ApplicationStatus
  {
    private static object _lock = new object();
    private static Utility.DictionaryMV<string, object> _Status = new Utility.DictionaryMV<string, object>();
    private static Utility.DictionaryMV<string, Stopwatch> _Stopwatches = new Utility.DictionaryMV<string, Stopwatch>();
    //
    public static Utility.DictionaryMV<string, object> Status { get { return _Status; } }
    public static Utility.DictionaryMV<string, Stopwatch> Stopwatches { get { return _Stopwatches; } }
    //
    public static void ClearAll() { lock (_lock) { _Status.Clear(); _Stopwatches.Clear(); } }
    //

    public static T StatusSet<T>(string key, T value) { lock (_lock) { _Status[key] = value; } return value; }
    public static T StatusGet<T>(string key) { lock (_lock) { return (_Status.ContainsKey(key) && _Status[key] is T) ? (T)_Status[key] : default(T); } }
    public static T StatusGet<T>(string key, T defaultValue) { lock (_lock) { return (_Status.ContainsKey(key) && _Status[key] is T) ? (T)_Status[key] : defaultValue; } }

    public static Stopwatch StopwatchGet(string key) { lock (_lock) { return Stopwatches[key] ?? (Stopwatches[key] = new Stopwatch()); } }
    public static Stopwatch StopwatchReset(string key) { lock (_lock) { var sw = StopwatchGet(key); sw.Reset(); return sw; } }
    public static Stopwatch StopwatchResetAndStart(string key) { lock (_lock) { var sw = StopwatchGet(key); sw.Reset(); sw.Start(); return sw; } }
    public static Stopwatch StopwatchStart(string key) { lock (_lock) { var sw = StopwatchGet(key); sw.Start(); return sw; } }
    public static Stopwatch StopwatchStop(string key) { lock (_lock) { var sw = StopwatchGet(key); sw.Stop(); return sw; } }
    public static void StopwatchRemove(string key) { lock (_lock) { if (Stopwatches.ContainsKey(key)) Stopwatches.Remove(key); } }

  }


}




