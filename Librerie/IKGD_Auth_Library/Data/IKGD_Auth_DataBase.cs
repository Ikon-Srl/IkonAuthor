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
using System.Data.Linq.Mapping;
using System.ComponentModel;
using System.Reflection;
using System.Text;



namespace Ikon.Auth.ExtraDB
{


  public partial class DataContext
  {
    public static readonly string ConnectionStringName = "GDCS";
    public const string ContextStringDB = "Ikon.Auth.ExtraDB.DB";
    private static MappingSource GD_MappingSource;

    public static string ConnectionString { get { return ConfigurationManager.ConnectionStrings[ConnectionStringName].ConnectionString; } }

    public static DataContext Factory() { return Factory(false); }
    public static DataContext Factory(bool forceNewconnection)
    {
      if (GD_MappingSource != null && !forceNewconnection)
        return new DataContext(ConnectionString, GD_MappingSource);
      else
      {
        DataContext newContext = new DataContext(ConnectionString);
        if (GD_MappingSource == null)
        {
          GD_MappingSource = newContext.Mapping.MappingSource;
        }
        return newContext;
      }
    }
    public static DataContext Factory(DataContext baseDB) { return new DataContext(baseDB.Connection, baseDB.Mapping.MappingSource); }
    public static DataContext Factory(IDbConnection connection) { return new DataContext(connection); }

    partial void OnCreated()
    {
      IKCMS_ExecutionProfiler.AddMessage("DB: {0}.OnCreated()".FormatString(this.GetType().FullName));
    }


    public static DataContext DB
    {
      get
      {
        if (HttpContext.Current == null || HttpContext.Current.Items == null)
          return Factory();
        if (HttpContext.Current.Items[ContextStringDB] == null)
          HttpContext.Current.Items[ContextStringDB] = Factory();
        return HttpContext.Current.Items[ContextStringDB] as DataContext;
      }
      set { HttpContext.Current.Items[ContextStringDB] = value; }
    }

    public static void DB_AutoDispose()
    {
      if (HttpContext.Current.Items[ContextStringDB] != null)
      {
        try
        {
          (HttpContext.Current.Items[ContextStringDB] as DataContext).Dispose();
        }
        catch { }
        finally
        {
          HttpContext.Current.Items[ContextStringDB] = null;
        }
      }
    }

  }



}

