/*
 * 
 * Intranet Ikon
 * 
 * Copyright (C) 2011 Ikon Srl
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


namespace Ikon.IKCMS
{


  public static class IKCMS_ExceptionsManager
  {
    private static object _lock = new object();
    private static readonly string varName = "IKCMS_ExceptionsManager";


    public static IEnumerable<Exception> Exceptions
    {
      get
      {
        lock (_lock)
        {
          return HttpContext.Current.Items[varName] as IEnumerable<Exception>;
        }
      }
    }

    public static List<Exception> ExceptionsList
    {
      get
      {
        lock (_lock)
        {
          List<Exception> _Exceptions = HttpContext.Current.Items[varName] as List<Exception>;
          if (_Exceptions == null)
          {
            HttpContext.Current.Items[varName] = _Exceptions = new List<Exception>();
          }
          return _Exceptions;
        }
      }
    }


    public static Exception Add(Exception ex)
    {
      //Elmah.ErrorSignal.FromCurrentContext().Raise(ex);
      lock (_lock)
      {
        try { ExceptionsList.Add(ex); }
        catch { }
      }
      return ex;
    }


    public static void Clear(Exception ex)
    {
      lock (_lock)
      {
        HttpContext.Current.Items.Remove(varName);
      }
    }


    public static IEnumerable<T> ExceptionsOfType<T>()
      where T : Exception
    {
      IEnumerable<T> results = Enumerable.Empty<T>();
      lock (_lock)
      {
        IEnumerable<Exception> _Exceptions = HttpContext.Current.Items[varName] as IEnumerable<Exception>;
        if (_Exceptions != null)
        {
          // forziamo un ToList per evitare 
          results = _Exceptions.OfType<T>().ToList();
        }
      }
      return results;
    }


    public static bool ExceptionsAny<T>()
      where T : Exception
    {
      lock (_lock)
      {
        IEnumerable<Exception> _Exceptions = HttpContext.Current.Items[varName] as IEnumerable<Exception>;
        if (_Exceptions != null && _Exceptions.OfType<T>().Any())
          return true;
      }
      return false;
    }


    public static bool ExceptionsAnyOf(params Type[] types)
    {
      lock (_lock)
      {
        try
        {
          IEnumerable<Exception> _Exceptions = HttpContext.Current.Items[varName] as IEnumerable<Exception>;
          if (_Exceptions != null && _Exceptions.Any(e => { Type ty = e.GetType(); return types.Any(t => t.IsAssignableFrom(ty)); }))
          {
            return true;
          }
        }
        catch { }
      }
      return false;
    }


    public static IEnumerable<Exception> ExceptionsFilter(Func<Exception, bool> filter)
    {
      IEnumerable<Exception> results = Enumerable.Empty<Exception>();
      lock (_lock)
      {
        try
        {
          IEnumerable<Exception> _Exceptions = HttpContext.Current.Items[varName] as IEnumerable<Exception>;
          if (_Exceptions != null)
          {
            results = ((filter != null) ? _Exceptions.Where(filter) : _Exceptions).ToList();
          }
        }
        catch { }
      }
      return results;
    }


    public static bool EnableOutput
    {
      get { return (bool)(HttpContext.Current.Items["exceptions_output"] ?? false); }
      set { HttpContext.Current.Items["exceptions_output"] = value; }
    }


    public static string DumpExceptions()
    {
      var exs = Exceptions;
      if (exs != null)
      {
        return Utility.Implode(Exceptions.Select(ex => ex.ToString()), "<br/>\n");
      }
      return string.Empty;
    }


  }



  //
  // custom exception di base da usare con IKCMS_ExceptionsManager
  //

  
  public class IKCMS_Exception_PathManager : Exception
  {
    public IKCMS_Exception_PathManager() : base() { }
    public IKCMS_Exception_PathManager(string message) : base(message) { }
    public IKCMS_Exception_PathManager(string message, Exception innerException) : base(message, innerException) { }
    //
    public override string ToString()
    {
      return base.ToString();
    }
  }


  public class IKCMS_Exception_TreeBuilder : Exception
  {
    public IKCMS_Exception_TreeBuilder() : base() { }
    public IKCMS_Exception_TreeBuilder(string message) : base(message) { }
    public IKCMS_Exception_TreeBuilder(string message, Exception innerException) : base(message, innerException) { }
    //
    public override string ToString()
    {
      return base.ToString();
    }
  }


  public class IKCMS_Exception_ManagerVFS : Exception
  {
    public IKCMS_Exception_ManagerVFS() : base() { }
    public IKCMS_Exception_ManagerVFS(string message) : base(message) { }
    public IKCMS_Exception_ManagerVFS(string message, Exception innerException) : base(message, innerException) { }
  }


  public class IKCMS_Exception_ModelBuilder : Exception
  {
    //
    public int? sNode { get; set; }
    //
    public IKCMS_Exception_ModelBuilder() : base() { }
    public IKCMS_Exception_ModelBuilder(string message) : base(message) { }
    public IKCMS_Exception_ModelBuilder(string message, Exception innerException) : base(message, innerException) { }
    public IKCMS_Exception_ModelBuilder(string message, int? sNode) : base(message) { this.sNode = sNode; }
    //
    public override string ToString()
    {
      return base.ToString() + string.Format("  -->  sNode:{0}", sNode);
    }
  }


  public class IKCMS_Exception_ModelBuilderContext : IKCMS_Exception_ModelBuilder
  {
    //
    public object[] BuilderArgs { get; set; }
    //
    public IKCMS_Exception_ModelBuilderContext() : base() { }
    public IKCMS_Exception_ModelBuilderContext(string message) : base(message) { }
    public IKCMS_Exception_ModelBuilderContext(string message, Exception innerException) : base(message, innerException) { }
    public IKCMS_Exception_ModelBuilderContext(string message, int? sNode) : base(message) { this.sNode = sNode; }
    public IKCMS_Exception_ModelBuilderContext(string message, int? sNode, object[] BuilderArgs) : base(message) { this.sNode = sNode; this.BuilderArgs = BuilderArgs.ToArray(); }
    //
    public override string ToString()
    {
      string extra = string.Empty;
      try
      {
        object[] _BuilderArgs = BuilderArgs ?? new object[] { };
        extra = string.Format("  args[{0}]:{1}", _BuilderArgs.Length, Utility.Implode(_BuilderArgs, "|"));
      }
      catch { }
      return base.ToString() + extra;
    }
  }


}

