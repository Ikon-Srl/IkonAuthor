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
using System.Web.UI;
using System.Web.Security;
using System.Xml.Linq;
using System.Data.Linq;
using System.IO;
using System.Text;
using System.Linq.Expressions;
using LinqKit;

using System.Reflection;
using System.Data.Linq.Mapping;
using System.Diagnostics;
using System.Linq.Dynamic;
using System.Transactions;
using System.Web.Caching;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;  // assembly System.ServiceModel.Web

using Ikon;
using Ikon.GD;
using Ikon.Log;


namespace Ikon.GD
{


  //
  // metodi per l'utilizzo senza using e registrazione dell'handler di unload per il dispose
  //
  public static class IKGD_VFS_Helpers
  {
    public static object _lock = new object();

    //
    // registrazione delle cache dependencies su tutte le tabelle utilizzate
    // NB:attenzione alla connection string che deve contenere il frammento:
    // Persist Security Info=False;
    // altrimenti fsOp.DB.Connection.ConnectionString viene passata senza la password e non si connette al DB!
    //
    public static List<string> RegisterSqlCacheDependencyInvalidation()
    {
      List<string> messages = new List<string>();
      using (FS_Operations fsOp = new FS_Operations())
      {
        string[] tables = new string[] {
          "IKGD_VNODE", "IKGD_VDATA", "IKGD_INODE", "IKGD_RELATION", "IKGD_PROPERTY",
          "IKGD_SNAPSHOT", "IKGD_FREEZED",
          "IKGD_CONFIG", "IKGD_KEYSTORAGE", "IKGD_KEYSTORAGE_MAP",
          "IKCAT_ElementMain", "IKCAT_ElementVariant", "IKCAT_ElementAttribute", "IKCAT_ElementFolder", "IKCAT_Attribute", "IKATT_Attribute", "IKATT_AttributeMapping",
          "IKCMS_SEO",
          "IKGD_ADMIN", "aspnet_UsersInRoles", "aspnet_Roles"
        };
        //
        SqlCacheDependencyAdmin.EnableNotifications(fsOp.DB.Connection.ConnectionString);
        //SqlCacheDependencyAdmin.EnableTableForNotifications(fsOp.DB.Connection.ConnectionString, tables);
        foreach (string table in tables)
        {
          try { System.Web.Caching.SqlCacheDependencyAdmin.EnableTableForNotifications(fsOp.DB.Connection.ConnectionString, new string[] { table }); }
          catch (Exception ex) { messages.Add(ex.Message); }
        }
        //
        messages.AddRange(SqlCacheDependencyAdmin.GetTablesEnabledForNotifications(fsOp.DB.Connection.ConnectionString));
        //
        //foreach (string table in tables)
        //  fsOp.DB.ExecuteCommand(string.Format("exec dbo.AspNet_SqlCacheRegisterTableStoredProcedure @tableName='{0}';", table));
      }
      return messages;
    }


    //
    // rebuild degli indici sulle tabelle principali del VFS
    // rigenerazione delle statistiche per l'ottimizzazione delle query
    // cancellazione della cache dell'ottimizzatore delle query
    //
    public static void OptimizeStatisticsOnDB(bool? shrink, bool? freecache) { OptimizeStatisticsOnDB(shrink, freecache, null); }
    public static void OptimizeStatisticsOnDB(bool? shrink, bool? freecache, bool? updateallstats, params string[] tables_extra)
    {
      string[] tables = new string[] {
        "IKGD_RNODE", "IKGD_SNODE", "IKGD_VNODE", "IKGD_VDATA", "IKGD_VDATA_KEYVALUE", "IKGD_INODE", "IKGD_RELATION", "IKGD_PROPERTY", "IKGD_STREAM", "IKGD_MSTREAM",
        "IKCAT_ElementAttribute", "IKCAT_ElementFolder", "IKCAT_ElementMain", "IKCAT_ElementVariant", "IKCAT_Attribute", "IKATT_Attribute", "IKATT_AttributeMapping", "IKCAT_AttributeResource", "IKCAT_AttributeStream", 
        "IKGD_SNAPSHOT", "IKGD_FREEZED",
        "LazyLoginMapper", "LazyLogin_Vote", "LazyLogin_Log",
        "IKGD_CONFIG", "IKGD_KEYSTORAGE", "IKGD_KEYSTORAGE_MAP",
        "IKCMS_SEO",
        "IKG_HITLOG", "IKG_HITACC",
        "IKGD_ADMIN",
        "aspnet_UsersInRoles", "aspnet_Membership", "aspnet_Users", "aspnet_Roles"
      };
      Dictionary<string, int> fillRatio = new Dictionary<string, int> {
      { "IKG_HITLOG", 70 }, { "IKG_HITACC", 80 },
      { "IKGD_RNODE", 80 }, { "IKGD_SNODE", 80 }, { "IKGD_VNODE", 80 }, { "IKGD_VDATA", 80 }, { "IKGD_VDATA_KEYVALUE", 80 },
      { "IKGD_INODE", 80 }, { "IKGD_RELATION", 80 }, { "IKGD_PROPERTY", 80 }, { "IKGD_MSTREAM", 80 }, { "IKGD_STREAM", 80 },
      { "LazyLoginMapper", 80 }, { "LazyLogin_Vote", 80 }, { "LazyLogin_Log", 80 }, { "aspnet_Membership", 80 }, { "aspnet_Users", 80 } };
      using (FS_Operations fsOp = new FS_Operations())
      {
        fsOp.DB.CommandTimeout = 3600;
        foreach (string tablename in tables.Concat(tables_extra).Distinct())
        {
          int fillFactor = 0;
          if (fillRatio.ContainsKey(tablename))
            fillFactor = fillRatio[tablename];
          try { int res = fsOp.DB.ExecuteCommand("DBCC DBREINDEX({0}, '', {1});", tablename, fillFactor.ToString()); }
          catch { }
        }
        foreach (string tablename in tables.Concat(tables_extra).Distinct())
        {
          try { int res = fsOp.DB.ExecuteCommand("UPDATE STATISTICS [{0}];", tablename); }
          catch { }
        }
        //
        if (updateallstats.GetValueOrDefault(true))
        {
          int res01 = fsOp.DB.ExecuteCommand("EXEC sp_updatestats;");
        }
        if (freecache.GetValueOrDefault(false))
        {
          fsOp.DB.ExecuteCommand("DBCC FREEPROCCACHE;");
        }
        //
        if (shrink.GetValueOrDefault(false))
        {
          fsOp.DB.ExecuteCommand("DBCC SHRINKDATABASE({0})", fsOp.DB.Connection.Database);
        }
      }
      //
      // verificare che la modalita' di locking di default del database sia quella corretta: (altrimenti qualsiasi transaction blocca anche le read al di fuori delle transaction)
      // SELECT name,is_read_committed_snapshot_on FROM sys.databases
      // in caso modificarla con:
      // ALTER DATABASE [***] SET READ_COMMITTED_SNAPSHOT ON WITH ROLLBACK IMMEDIATE;
      //
    }




  }



  //
  //System.Transactions.TransactionScope wrapper class
  //
  public static class IKGD_TransactionFactory
  {
    private static bool globallyDisabled = false;


    static IKGD_TransactionFactory()
    {
      globallyDisabled = IKGD_Config.AppSettings["IKGD_TransactionIsolationLevel"] == "NONE";
    }


    //
    // modalita' di default per le transaction configurabile da web.config: per default ReadUncommitted
    //
    public static TransactionScope Transaction(int? timeoutSeconds)
    {
      System.Transactions.TransactionOptions options = new System.Transactions.TransactionOptions();
      if (timeoutSeconds != null && timeoutSeconds > 0)
        options.Timeout = TimeSpan.FromSeconds(timeoutSeconds.Value);
      switch (IKGD_Config.AppSettings["IKGD_TransactionIsolationLevel"])
      {
        case "NONE":
          return null;
        case "Serializable":
          options.IsolationLevel = System.Transactions.IsolationLevel.Serializable;
          break;
        case "ReadCommitted":
          options.IsolationLevel = System.Transactions.IsolationLevel.ReadCommitted;
          break;
        case "Snapshot":
          options.IsolationLevel = System.Transactions.IsolationLevel.Snapshot;
          break;
        case "RepeatableRead":
          options.IsolationLevel = System.Transactions.IsolationLevel.RepeatableRead;
          break;
        case "Chaos":
          options.IsolationLevel = System.Transactions.IsolationLevel.Chaos;
          break;
        case "ReadUncommitted":
        default:
          options.IsolationLevel = System.Transactions.IsolationLevel.ReadUncommitted;
          break;
      }
      if (globallyDisabled)
        return null;
      return new TransactionScope(System.Transactions.TransactionScopeOption.Required, options);
    }


    public static void Committ(this TransactionScope ts)
    {
      if (ts != null)
      {
        ts.Complete();
      }
    }


    public static TransactionScope TransactionNone() { return TransactionNone(null, null); }
    public static TransactionScope TransactionNone(int? timeoutSeconds) { return TransactionNone(timeoutSeconds, null); }
    public static TransactionScope TransactionNone(int? timeoutSeconds, bool? enabled)
    {
      return null;
    }


    //
    // modalita' di default per le transaction: totalmente bloccante
    //
    public static TransactionScope TransactionSerializable(int? timeoutSeconds) { return TransactionSerializable(timeoutSeconds, null); }
    public static TransactionScope TransactionSerializable(int? timeoutSeconds, bool? enabled)
    {
      if (globallyDisabled || enabled.GetValueOrDefault(true))
        return null;
      System.Transactions.TransactionOptions options = new System.Transactions.TransactionOptions();
      options.IsolationLevel = System.Transactions.IsolationLevel.Serializable;
      if (timeoutSeconds != null && timeoutSeconds > 0)
        options.Timeout = TimeSpan.FromSeconds(timeoutSeconds.Value);
      return new TransactionScope(System.Transactions.TransactionScopeOption.Required, options);
    }


    //
    // modalita' ottimale di gestione che non impatta particolarmente sulle performances
    //
    public static TransactionScope TransactionReadUncommitted(int? timeoutSeconds) { return TransactionReadUncommitted(timeoutSeconds, null); }
    public static TransactionScope TransactionReadUncommitted(int? timeoutSeconds, bool? enabled)
    {
      if (globallyDisabled || enabled.GetValueOrDefault(true))
        return null;
      System.Transactions.TransactionOptions options = new System.Transactions.TransactionOptions();
      options.IsolationLevel = System.Transactions.IsolationLevel.ReadUncommitted;
      if (timeoutSeconds != null && timeoutSeconds > 0)
        options.Timeout = TimeSpan.FromSeconds(timeoutSeconds.Value);
      return new TransactionScope(System.Transactions.TransactionScopeOption.Required, options);
    }


    public static TransactionScope TransactionReadCommitted(int? timeoutSeconds) { return TransactionReadCommitted(timeoutSeconds, null); }
    public static TransactionScope TransactionReadCommitted(int? timeoutSeconds, bool? enabled)
    {
      if (globallyDisabled || enabled.GetValueOrDefault(true))
        return null;
      System.Transactions.TransactionOptions options = new System.Transactions.TransactionOptions();
      options.IsolationLevel = System.Transactions.IsolationLevel.ReadCommitted;
      if (timeoutSeconds != null && timeoutSeconds > 0)
        options.Timeout = TimeSpan.FromSeconds(timeoutSeconds.Value);
      return new TransactionScope(System.Transactions.TransactionScopeOption.Required, options);
    }


    //
    // SELECT name,is_read_committed_snapshot_on,snapshot_isolation_state FROM sys.databases ORDER BY sys.databases.name;
    // ALTER DATABASE [dbName] SET READ_COMMITTED_SNAPSHOT ON WITH ROLLBACK IMMEDIATE;
    // ALTER DATABASE [dbName] SET ALLOW_SNAPSHOT_ISOLATION ON;
    //
    public static TransactionScope TransactionSnapshot(int? timeoutSeconds) { return TransactionSnapshot(timeoutSeconds, null); }
    public static TransactionScope TransactionSnapshot(int? timeoutSeconds, bool? enabled)
    {
      if (globallyDisabled || enabled.GetValueOrDefault(true))
        return null;
      System.Transactions.TransactionOptions options = new System.Transactions.TransactionOptions();
      options.IsolationLevel = System.Transactions.IsolationLevel.Snapshot;
      if (timeoutSeconds != null && timeoutSeconds > 0)
        options.Timeout = TimeSpan.FromSeconds(timeoutSeconds.Value);
      return new TransactionScope(System.Transactions.TransactionScopeOption.Required, options);
    }


    public static TransactionScope TransactionRepeatableRead(int? timeoutSeconds) { return TransactionRepeatableRead(timeoutSeconds, null); }
    public static TransactionScope TransactionRepeatableRead(int? timeoutSeconds, bool? enabled)
    {
      if (globallyDisabled || enabled.GetValueOrDefault(true))
        return null;
      System.Transactions.TransactionOptions options = new System.Transactions.TransactionOptions();
      options.IsolationLevel = System.Transactions.IsolationLevel.RepeatableRead;
      if (timeoutSeconds != null && timeoutSeconds > 0)
        options.Timeout = TimeSpan.FromSeconds(timeoutSeconds.Value);
      return new TransactionScope(System.Transactions.TransactionScopeOption.Required, options);
    }

  }


}
