/*
 * 
 * Intranet Ikon
 * 
 * Copyright (C) 2009 Ikon Srl
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
using System.Text.RegularExpressions;
using System.Linq.Expressions;
using System.Reflection;
using System.Data.Linq.Mapping;
using System.Data.Linq.SqlClient;
using System.Diagnostics;
using System.Linq.Dynamic;
using System.Transactions;
using System.Web.Caching;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;  // assembly System.ServiceModel.Web
using System.Data.SqlClient;
using LinqKit;

using Ikon;
using Ikon.GD;
using Ikon.Log;


/// <summary>
/// Summary description for IkonGD_dataBase
/// </summary>

namespace Ikon.GD
{

  //
  // definizione di extension methods per operare il filtraggio del versioning sui nodi e le correlazioni
  // piu' altri metodi utili per la vavigazione sul VFS
  //
  public static class FS_OperationsHelpers
  {
    public readonly static string cacheBaseName = "IKGD_FSOH_";
    public static readonly string cookie_VFS_Status = "IKGD_VFS_Status_";
    public static bool CacheItemRemovedCallbackEnabled { get; private set; }


    static FS_OperationsHelpers()
    {
      try
      {
        CacheItemRemovedCallbackEnabled = Utility.TryParse<bool>(IKGD_Config.AppSettings["CacheItemRemovedCallbackEnabled"], true);
      }
      catch { }
    }


    //
    // funzionalita' ausiliarie per la gestione e il caching delle aree associate a root o altri utenti
    //
    public static void ClearCachedData() { ClearCachedData(false); }
    public static void ClearCachedData(bool allUsers)
    {
      if (allUsers)
        HttpRuntime.Cache.OfType<DictionaryEntry>().Where(c => c.Key is string && (c.Key as string).StartsWith(cacheBaseName)).Select(c => c.Key as string).ForEach(k => HttpRuntime.Cache.Remove(k));
      else
        HttpRuntime.Cache.OfType<DictionaryEntry>().Where(c => c.Key is string && (c.Key as string).StartsWith(cacheBaseName + Ikon.GD.MembershipHelper.UserName)).Select(c => c.Key as string).ForEach(k => HttpRuntime.Cache.Remove(k));
    }
    //
    public static T RegisterCachedAclObject<T>(string cacheKey, Func<T> entityBuilder)
    {
      object entity = HttpRuntime.Cache[cacheKey];
      if (entity == null)
      {
        try
        {
          entity = entityBuilder();
          if (entity != null)
            HttpRuntime.Cache.Insert(cacheKey, entity, Ikon.Auth.Roles_IKGD.GetCacheDependency, DateTime.Now.AddSeconds(Utility.TryParse<int>(IKGD_Config.AppSettings["CachingACLs"], 3600)), Cache.NoSlidingExpiration, CacheItemPriority.Low, null);
        }
        catch { }
      }
      return (T)entity;
    }


    //
    public static void StatusSessionRemove() { Utility.CookieRemove(cookie_VFS_Status + IKGD_Config.ApplicationFullName); }
    //

    public static int? VersionFrozenSessionNullable
    {
      get
      {
        try { return Utility.TryParse<int?>(HttpContext.Current.Request.QueryString["VFS_Snapshot"] ?? HttpContext.Current.Request.Cookies.Value(cookie_VFS_Status + IKGD_Config.ApplicationFullName, "VFS_Snapshot"), null); }
        catch { return null; }
        //try { return Utility.TryParse<int?>(HttpContext.Current.Request.Cookies.Value(cookie_VFS_Status + IKGD_Config.ApplicationFullName, "VFS_Snapshot"), null); }
        //catch { return null; }
      }
    }
    public static int VersionFrozenSession
    {
      // in cookie temporanea con path / valida per il dominio di II livello
      // utilizza la cookie o il valore registrato nel config "IKGD_VersionFrozenSession"
      // "IKGD_VersionFrozenSession" viene letto da web.config e non dal config su database
      // viene testato anche il dominio cosi' possiamo usare preview.domain.it sullo stesso sito pubblicato
      // <add key="IKGD_VersionPreviewDomainRegex" value="^preview\."/>
      get
      {
        int? version = VersionFrozenSessionNullable;
        if (version == null && !string.IsNullOrEmpty(IKGD_Config.AppSettingsWeb["IKGD_VersionPreviewDomainRegex"]))
          version = version ?? (Regex.IsMatch(HttpContext.Current.Request.Url.Host, IKGD_Config.AppSettingsWeb["IKGD_VersionPreviewDomainRegex"]) ? (int?)-1 : null);
        return version ?? Utility.TryParse<int>(IKGD_Config.AppSettingsWeb["IKGD_VersionFrozenSession"], 0);
      }
      set
      {
        Utility.CookieUpdateKeyValue(cookie_VFS_Status + IKGD_Config.ApplicationFullName, "VFS_Snapshot", value.ToString(), true);
      }
    }


    // viene utilizzata direttamente dal language provider (cosi' utiliziamo direttamente il cookie management di fsOp)
    // NB per l'utilizzo normale nel codice che non sia di basso livello (es. accesso diretto al VFS con filtri custom) utilizzare:
    // IKGD_Language_Provider.Provider.LanguageNN / fsOp.LanguageNN
    // IKGD_Language_Provider.Provider.Language / fsOp.Language
    public static string LanguageSession
    {
      get { return CustomSessionGet("Language"); }
      set { CustomSessionSet("Language", value); }
    }


    public static string CountrySession
    {
      get { return CustomSessionGet("Country"); }
      set { CustomSessionSet("Country", value); }
    }


    public static string CustomSessionGetTrusted(string key)
    {
      try { return HttpContext.Current.Request.Cookies.Value(cookie_VFS_Status + IKGD_Config.ApplicationFullName, key); }
      catch { return null; }
    }


    private static Regex _CustomSessionGetRegexNotAllowd = new Regex(@"[^\p{L}\p{Nd}_\-\. ]", RegexOptions.Compiled | RegexOptions.Singleline);
    public static string CustomSessionGet(string key)
    {
      string result = null;
      try
      {
        result = (string)HttpContext.Current.Items[cookie_VFS_Status + IKGD_Config.ApplicationFullName + (key ?? string.Empty)];
        if (result == null)
        {
          result = HttpContext.Current.Request.Cookies.Value(cookie_VFS_Status + IKGD_Config.ApplicationFullName, key);
          if (result != null)
          {
            result = _CustomSessionGetRegexNotAllowd.Replace(result, string.Empty);  // lettere numeri . - _
            HttpContext.Current.Items[cookie_VFS_Status + IKGD_Config.ApplicationFullName + (key ?? string.Empty)] = result;
          }
        }
      }
      catch { }
      return result;
    }


    public static string CustomSessionSet(string key, string value)
    {
      Utility.CookieUpdateKeyValue(cookie_VFS_Status + IKGD_Config.ApplicationFullName, key, value, true);
      //
      HttpContext.Current.Items.Remove(cookie_VFS_Status + IKGD_Config.ApplicationFullName + (key ?? string.Empty));
      //
      // lasciamo poi eseguire la pulizia e normalizzazione al getter
      //if (value != null)
      //{
      //  HttpContext.Current.Items[cookie_VFS_Status + IKGD_Config.ApplicationFullName + (key ?? string.Empty)] = value;
      //}
      return value;
    }


    //
    // per poter impostare date passate/future nel CMS e' necessario
    // utilizzare un wrapper per la data settata da utilizzare al posto di DateTime.Now
    // DateTimeSessionStorage rientra anche nei parametri automatici delle cacheKey
    //
    public static DateTime DateTimeSession
    {
      get { return DateTimeSessionStorage ?? DateTime.Now; }
      set { DateTimeSessionStorage = value; ; }
    }
    public static DateTime? DateTimeSessionStorage
    {
      get { try { return HttpContext.Current.Session != null ? HttpContext.Current.Session[cacheBaseName + "DateTime"] as DateTime? : null; } catch { return null; } }
      set { try { HttpContext.Current.Session[cacheBaseName + "DateTime"] = value; } catch { } }
    }
    private static int DateTimeSessionStorageHash { get { try { return ((HttpContext.Current.Session != null ? HttpContext.Current.Session[cacheBaseName + "DateTime"] as DateTime? : null) ?? DateTime.MinValue).GetHashCode(); } catch { return 0; } } }


    public static bool IsRootUser(string userName)
    {
      try
      {
        string cacheKey = cacheBaseName + userName + "::IsRoot";
        bool result = RegisterCachedAclObject<bool>(cacheKey, () =>
        {
          bool res = false;
          if (IKGD_Config.AppSettings["RootUser"].IsNotEmpty())
            res |= (userName == IKGD_Config.AppSettings["RootUser"]);
          if (res == false && IKGD_Config.AppSettings["RootGroup"].IsNotEmpty())
            res |= Ikon.Auth.Roles_IKGD.Provider.IsUserInRole(userName, IKGD_Config.AppSettings["RootGroup"]);
          return res;
        });
        return result;
      }
      catch { }
      return false;
    }


    public static FS_Areas_Extended GetAreasExtended(string userName) { return GetAreasExtended(userName, false, null); }
    public static FS_Areas_Extended GetAreasExtended(string userName, bool forceRoot) { return GetAreasExtended(userName, forceRoot, null); }
    public static FS_Areas_Extended GetAreasExtended(string userName, bool forceRoot, IKGD_DataContext DB)
    {
      FS_Areas_Extended result = null;
      try
      {
        //
        bool isRootCombined = forceRoot || IsRootUser(userName);
        string cacheKey = cacheBaseName + userName + string.Format(":{0}:", isRootCombined ? "1" : "0") + "AreasX";
        //
        result = RegisterCachedAclObject<FS_Areas_Extended>(cacheKey, () =>
        {
          return new FS_Areas_Extended(userName, isRootCombined, DB);
        });
      }
      catch { }
      return result;
    }


    public static List<string> GetAreas() { return GetAreas(Ikon.GD.MembershipHelper.UserName, false, null); }
    public static List<string> GetAreas(string userName, bool forceRoot, IKGD_DataContext DB)
    {
      try
      {
        //
        bool isRootCombined = forceRoot || IsRootUser(userName);
        string cacheKey = cacheBaseName + userName + string.Format(":{0}:", isRootCombined ? "1" : "0") + "Areas";
        return RegisterCachedAclObject<List<string>>(cacheKey, () => { return new FS_Areas_Extended(userName, isRootCombined, DB).AreasAllowed; }) ?? new List<string>();
      }
      catch { }
      return new List<string>();
    }


    //
    // cached wrapper around slow provider functions
    //
    public static bool IsRoot { get { return IsRootUser(Ikon.GD.MembershipHelper.UserName); } }
    public static FS_Areas_Extended CachedAreasExtended { get { return GetAreasExtended(Ikon.GD.MembershipHelper.UserName, false); } }
    public static string CachedAreasHash { get { return CachedAreasExtended.GetHashCode().ToString("x"); } }
    public static string ContextHash(params object[] frags) { return Utility.Implode(new object[] { VersionFrozenSession, IKGD_SiteMode.GetSiteHash, IKGD_Language_Provider.Provider.LanguageNN, FS_OperationsHelpers.DateTimeSessionStorageHash, CachedAreasHash }.Concat(frags).Where(f => f != null), "_"); }
    public static string ContextHashNN(params object[] frags) { return Utility.Implode(new object[] { VersionFrozenSession, IKGD_SiteMode.GetSiteHash, IKGD_Language_Provider.Provider.LanguageNN, FS_OperationsHelpers.DateTimeSessionStorageHash, CachedAreasHash }.Concat(frags).Select(f => f ?? (object)string.Empty), "_"); }
    public static string ContextHashGeneral(bool? useLanguage, bool? useDateTimeSession, bool? useAreas, bool? useSiteMode, params object[] frags)
    {
      List<object> data = new List<object> { VersionFrozenSession };
      if (useSiteMode.GetValueOrDefault(false))
        data.Add(IKGD_SiteMode.GetSiteHash);
      if (useLanguage.GetValueOrDefault(false))
        data.Add(IKGD_Language_Provider.Provider.LanguageNN);
      if (useDateTimeSession.GetValueOrDefault(false))
        data.Add(FS_OperationsHelpers.DateTimeSessionStorageHash);
      if (useAreas.GetValueOrDefault(false))
        data.Add(CachedAreasHash);
      return Utility.Implode(data.Concat(frags).Select(f => f ?? (object)string.Empty), "_");
    }

    //
    public static readonly string[] Const_CacheDependencyIKGD_NONE = new string[] { };
    public static readonly string[] Const_CacheDependencyIKGD_vNode_vData = new string[] { "IKGD_VNODE", "IKGD_VDATA", "IKGD_FREEZED" };
    public static readonly string[] Const_CacheDependencyIKGD_vNode_vData_iNode = new string[] { "IKGD_VNODE", "IKGD_VDATA", "IKGD_INODE", "IKGD_FREEZED" };
    public static readonly string[] Const_CacheDependencyIKGD_vNode_vData_Property = new string[] { "IKGD_VNODE", "IKGD_VDATA", "IKGD_PROPERTY", "IKGD_FREEZED" };
    public static readonly string[] Const_CacheDependencyIKGD_vNode_vData_Relation = new string[] { "IKGD_VNODE", "IKGD_VDATA", "IKGD_RELATION", "IKGD_FREEZED" };
    public static readonly string[] Const_CacheDependencyIKGD_vNode_vData_Relation_Property = new string[] { "IKGD_VNODE", "IKGD_VDATA", "IKGD_RELATION", "IKGD_PROPERTY", "IKGD_FREEZED" };
    public static readonly string[] Const_CacheDependencyIKGD_vNode_vData_Relation_Property_Tags = new string[] { "IKGD_VNODE", "IKGD_VDATA", "IKGD_RELATION", "IKGD_PROPERTY", "IKGD_FREEZED", "IKCAT_Attribute", "IKATT_Attribute", "IKATT_AttributeMapping" };
    public static readonly string[] Const_CacheDependencyIKGD_vNode_vData_iNode_Relation = new string[] { "IKGD_VNODE", "IKGD_VDATA", "IKGD_INODE", "IKGD_RELATION", "IKGD_FREEZED" };
    public static readonly string[] Const_CacheDependencyIKGD_vNode_vData_iNode_Relation_Property = new string[] { "IKGD_VNODE", "IKGD_VDATA", "IKGD_INODE", "IKGD_RELATION", "IKGD_PROPERTY", "IKGD_FREEZED" };
    //
    public static void CachedEntityClear(object lockObject, string cacheKey)
    {
      if (lockObject != null)
      {
        lock (lockObject)
        {
          object obj = HttpRuntime.Cache.Remove(cacheKey);
          //object tmp01 = HttpRuntime.Cache.Get(cacheKey);
          //object tmp02 = HttpRuntime.Cache[cacheKey];
        }
      }
      else
        HttpRuntime.Cache.Remove(cacheKey);
    }
    //
    public static T CachedEntityWrapperLock<T>(object lockObject, string cacheKey, Func<T> entityBuilder, int? secondsDuration, IEnumerable<string> tablesDependencies) { return CachedEntityWrapperLock(lockObject, cacheKey, entityBuilder, null, secondsDuration, tablesDependencies); }
    public static T CachedEntityWrapperLock<T>(object lockObject, string cacheKey, Func<T> entityBuilder, Func<T, bool> entityValidator, int? secondsDuration, IEnumerable<string> tablesDependencies)
    {
      if (lockObject != null)
      {
        lock (lockObject)
        {
          return CachedEntityWrapperNoLock(cacheKey, entityBuilder, entityValidator, secondsDuration, tablesDependencies);
        }
      }
      else
      {
        return CachedEntityWrapper(cacheKey, entityBuilder, entityValidator, secondsDuration, null, tablesDependencies);
      }
    }
    //
    public static T CachedEntityWrapperNoLock<T>(string cacheKey, Func<T> entityBuilder, int? secondsDuration, IEnumerable<string> tablesDependencies) { return CachedEntityWrapperNoLock(cacheKey, entityBuilder, null, secondsDuration, tablesDependencies); }
    public static T CachedEntityWrapperNoLock<T>(string cacheKey, Func<T> entityBuilder, Func<T, bool> entityValidator, int? secondsDuration, IEnumerable<string> tablesDependencies)
    {
      T entity = (T)HttpRuntime.Cache[cacheKey];
      if (entity == null)
      {
        try
        {
          entity = entityBuilder();
          bool enableCaching = false;
          try { enableCaching = (entityValidator != null) ? entityValidator((T)entity) : entity != null; }
          catch { }
          if (enableCaching)
          {
            secondsDuration = secondsDuration ?? Utility.TryParse<int>(IKGD_Config.AppSettings["CachingDEFAULT"], 3600);
            CacheDependency dependencies = null;
            CacheItemRemovedCallback onRemoveCallback = null;
            if (entity is IKGD_CachingHelper_HasCacheDependencies_Interface)
            {
              dependencies = (entity as IKGD_CachingHelper_HasCacheDependencies_Interface).CachingHelper_Dependencies;
            }
            else
            {
              var depsList = tablesDependencies ?? Const_CacheDependencyIKGD_vNode_vData;
              dependencies = GetCacheDependencyWrapper((VersionFrozenSession != -1 && depsList.Contains("IKGD_FREEZED")) ? depsList.SkipWhile(d => d != "IKGD_FREEZED") : depsList);
            }
            if (CacheItemRemovedCallbackEnabled && entity is IKGD_CachingHelper_CacheItemHasRemovedCallback_Interface)
            {
              //onRemoveCallback = (entity as IKGD_CachingHelper_CacheItemHasRemovedCallback_Interface).CachingHelper_onRemoveCallback;
              onRemoveCallback = CacheItemRemovedCallbackStaticHelper;
            }
            HttpRuntime.Cache.Insert(cacheKey, entity, dependencies, DateTime.Now.AddSeconds(secondsDuration.Value), Cache.NoSlidingExpiration, CacheItemPriority.Low, onRemoveCallback);
          }
          else
          {
          }
        }
        catch { }
      }
      return entity;
    }


    //
    // caching helper con lock automatico (dipendente dalla caching key)
    //
    private static List<string> _CacheWrapperLocksList = new List<string>();
    //
    public static T CachedEntityWrapper<T>(string cacheKey, Func<T> entityBuilder, int? secondsDuration, IEnumerable<string> tablesDependencies) { return CachedEntityWrapper(cacheKey, entityBuilder, null, secondsDuration, null, tablesDependencies); }
    public static T CachedEntityWrapper<T>(string cacheKey, Func<T> entityBuilder, Func<T, bool> entityValidator, int? secondsDuration, IEnumerable<string> tablesDependencies) { return CachedEntityWrapper(cacheKey, entityBuilder, entityValidator, secondsDuration, null, tablesDependencies); }
    public static T CachedEntityWrapper<T>(string cacheKey, Func<T> entityBuilder, Func<T, bool> entityValidator, int? secondsDuration, int? secondsDurationOnNotValid, IEnumerable<string> tablesDependencies)
    {
      if (string.IsNullOrEmpty(cacheKey))
        return default(T);
      object lockObject = null;
      try
      {
        lock (_CacheWrapperLocksList)
        {
          if (!_CacheWrapperLocksList.Contains(cacheKey))
            _CacheWrapperLocksList.Add(cacheKey);
          lockObject = _CacheWrapperLocksList.FirstOrDefault(c => c == cacheKey);
        }
        if (lockObject != null)
          System.Threading.Monitor.Enter(lockObject);
        //
        object entity = HttpRuntime.Cache[cacheKey];
        if (entity == null)
        {
          try
          {
            entity = entityBuilder();
            bool enableCaching = false;
            try { enableCaching = (entityValidator != null) ? entityValidator((T)entity) : entity != null; }
            catch { }
            if (enableCaching || secondsDurationOnNotValid.HasValue)
            {
              secondsDuration = secondsDuration ?? Utility.TryParse<int>(IKGD_Config.AppSettings["CachingDEFAULT"], 3600);
              if (!enableCaching && secondsDurationOnNotValid.HasValue)
              {
                secondsDuration = secondsDurationOnNotValid.Value;
                if (entity == null)
                  entity = new CachedNull();
              }
              CacheDependency dependencies = null;
              CacheItemRemovedCallback onRemoveCallback = null;
              if (entity is IKGD_CachingHelper_HasCacheDependencies_Interface)
              {
                dependencies = (entity as IKGD_CachingHelper_HasCacheDependencies_Interface).CachingHelper_Dependencies;
              }
              else
              {
                var depsList = tablesDependencies ?? Const_CacheDependencyIKGD_vNode_vData;
                if (depsList != null && depsList.Any())
                {
                  dependencies = GetCacheDependencyWrapper((VersionFrozenSession != -1 && depsList.Contains("IKGD_FREEZED")) ? depsList.SkipWhile(d => d != "IKGD_FREEZED") : depsList);
                }
              }
              if (CacheItemRemovedCallbackEnabled && entity is IKGD_CachingHelper_CacheItemHasRemovedCallback_Interface)
              {
                //onRemoveCallback = (entity as IKGD_CachingHelper_CacheItemHasRemovedCallback_Interface).CachingHelper_onRemoveCallback;
                onRemoveCallback = CacheItemRemovedCallbackStaticHelper;
              }
              HttpRuntime.Cache.Insert(cacheKey, entity, dependencies, DateTime.Now.AddSeconds(secondsDuration.Value), Cache.NoSlidingExpiration, CacheItemPriority.Low, onRemoveCallback);
            }
            else
            {
            }
          }
          catch { }
        }
        if (entity is CachedNull)
        {
          return default(T);
        }
        return (T)entity;
      }
      catch { return default(T); }
      finally
      {
        if (lockObject != null)
        {
          lock (_CacheWrapperLocksList)
          {
            //TODO: bisognerebbe controllare un reference count del lock prima di pulire la lista
            _CacheWrapperLocksList.RemoveAll(c => object.Equals(lockObject, c));
          }
          System.Threading.Monitor.Exit(lockObject);
          lockObject = null;
        }
      }
    }


    public static T CachedEntityWrapperCustomDeps<T>(string cacheKey, Func<T> entityBuilder, Func<T, bool> entityValidator, int? secondsDuration, int? secondsDurationOnNotValid, Func<CacheDependency> cacheDepsGenerator)
    {
      if (string.IsNullOrEmpty(cacheKey))
        return default(T);
      object lockObject = null;
      try
      {
        lock (_CacheWrapperLocksList)
        {
          if (!_CacheWrapperLocksList.Contains(cacheKey))
            _CacheWrapperLocksList.Add(cacheKey);
          lockObject = _CacheWrapperLocksList.FirstOrDefault(c => c == cacheKey);
        }
        if (lockObject != null)
          System.Threading.Monitor.Enter(lockObject);
        //
        object entity = HttpRuntime.Cache[cacheKey];
        if (entity == null)
        {
          try
          {
            entity = entityBuilder();
            bool enableCaching = false;
            try { enableCaching = (entityValidator != null) ? entityValidator((T)entity) : entity != null; }
            catch { }
            if (enableCaching || secondsDurationOnNotValid.HasValue)
            {
              secondsDuration = secondsDuration ?? Utility.TryParse<int>(IKGD_Config.AppSettings["CachingDEFAULT"], 3600);
              if (!enableCaching && secondsDurationOnNotValid.HasValue)
              {
                secondsDuration = secondsDurationOnNotValid.Value;
                if (entity == null)
                  entity = new CachedNull();
              }
              CacheDependency dependencies = null;
              CacheItemRemovedCallback onRemoveCallback = null;
              if (entity is IKGD_CachingHelper_HasCacheDependencies_Interface)
              {
                dependencies = (entity as IKGD_CachingHelper_HasCacheDependencies_Interface).CachingHelper_Dependencies;
              }
              else
              {
                if (cacheDepsGenerator != null)
                {
                  try { dependencies = cacheDepsGenerator(); }
                  catch { }
                }
              }
              if (CacheItemRemovedCallbackEnabled && entity is IKGD_CachingHelper_CacheItemHasRemovedCallback_Interface)
              {
                //onRemoveCallback = (entity as IKGD_CachingHelper_CacheItemHasRemovedCallback_Interface).CachingHelper_onRemoveCallback;
                onRemoveCallback = CacheItemRemovedCallbackStaticHelper;
              }
              HttpRuntime.Cache.Insert(cacheKey, entity, dependencies, DateTime.Now.AddSeconds(secondsDuration.Value), Cache.NoSlidingExpiration, CacheItemPriority.Low, onRemoveCallback);
            }
            else
            {
            }
          }
          catch { }
        }
        if (entity is CachedNull)
        {
          return default(T);
        }
        return (T)entity;
      }
      catch { return default(T); }
      finally
      {
        if (lockObject != null)
        {
          lock (_CacheWrapperLocksList)
          {
            //TODO: bisognerebbe controllare un reference count del lock prima di pulire la lista
            _CacheWrapperLocksList.RemoveAll(c => object.Equals(lockObject, c));
          }
          System.Threading.Monitor.Exit(lockObject);
          lockObject = null;
        }
      }
    }


    //
    // locking helper con lock automatico (dipendente dalla locking key)
    //
    private static List<string> _LockedWorkerWrapperLocksList = new List<string>();
    //
    public static T LockedWorkerWrapper<T>(string lockingKey, Func<T> workerFunc)
    {
      if (string.IsNullOrEmpty(lockingKey))
        return default(T);
      object lockObject = null;
      try
      {
        lock (_LockedWorkerWrapperLocksList)
        {
          if (!_LockedWorkerWrapperLocksList.Contains(lockingKey))
            _LockedWorkerWrapperLocksList.Add(lockingKey);
          lockObject = _LockedWorkerWrapperLocksList.FirstOrDefault(c => c == lockingKey);
        }
        if (lockObject != null)
          System.Threading.Monitor.Enter(lockObject);
        //
        return workerFunc();
        //
      }
      catch { return default(T); }
      finally
      {
        if (lockObject != null)
        {
          lock (_LockedWorkerWrapperLocksList)
          {
            //TODO: bisognerebbe controllare un reference count del lock prima di pulire la lista
            _LockedWorkerWrapperLocksList.RemoveAll(c => object.Equals(lockObject, c));
          }
          System.Threading.Monitor.Exit(lockObject);
          lockObject = null;
        }
      }
    }


    private static Dictionary<string, AggregateCacheDependency> _GetCacheDependencyWrapperStorage = new Dictionary<string, AggregateCacheDependency>();
    public static AggregateCacheDependency GetCacheDependencyWrapper(IEnumerable<string> tablesDependencies)
    {
      AggregateCacheDependency sqlDeps = null;
      try
      {
        if (tablesDependencies != null && tablesDependencies.Any())
        {
          sqlDeps = new AggregateCacheDependency();
          sqlDeps.Add(tablesDependencies.Distinct((t1, t2) => string.Equals(t1, t2, StringComparison.OrdinalIgnoreCase)).Select(t => new SqlCacheDependency("GDCS", t)).ToArray());
          //tablesDependencies.ForEach(t => sqlDeps.Add(new SqlCacheDependency("GDCS", t)));
        }
      }
      catch { }
      return sqlDeps;
    }


    //
    // implementazione di CacheItemRemovedCallback
    // da usare per gli oggetti che implementano IKGD_CachingHelper_CacheItemHasRemovedCallback_Interface
    // in modo da non lasciare per questi oggetti un reference che incasina il GC
    //
    public static void CacheItemRemovedCallbackStaticHelper(string cacheKey, object value, CacheItemRemovedReason reason)
    {
      if (value != null && value is IKGD_CachingHelper_CacheItemHasRemovedCallback_Interface)
      {
        try { (value as IKGD_CachingHelper_CacheItemHasRemovedCallback_Interface).CachingHelper_onRemoveCallback(cacheKey, value, reason); }
        catch { }
      }
    }


    //
    // per la pulizia della cache
    //
    public static List<string> CacheClear(string baseKey)
    {
      List<string> messages = new List<string>();
      messages.Add(string.Format(FileSizeFormatProvider.Factory(), "Total managed memory in GC before cleaning: {0:fs}", System.GC.GetTotalMemory(false)));
      try
      {
        IKGD_Path_Helper.GarbageCollectorWorker(true);
      }
      catch (Exception ex) { messages.Add(ex.Message); }
      messages.Add(string.Format("Items in cache before cleaning: {0}", HttpRuntime.Cache.Count));
      try
      {
        HttpRuntime.Cache.OfType<DictionaryEntry>().Select(c => c.Key as string).Where(k => k != null && (baseKey == null || k.StartsWith(baseKey))).ToArray().ForEach(k => HttpRuntime.Cache.Remove(k));
      }
      catch (Exception ex) { messages.Add(ex.Message); }
      messages.Add(string.Format("Items in cache after cleaning: {0}", HttpRuntime.Cache.Count));
      try
      {
        for (int i = 0; i <= System.GC.MaxGeneration; i++)
        {
          // chiamate multiple per consentire la promozione fino alla generazione massima e quindi effettivamente deallocare gli oggetti dal GC
          System.GC.Collect();
        }
        System.GC.Collect(System.GC.MaxGeneration);
        System.GC.WaitForPendingFinalizers();
        System.GC.Collect(System.GC.MaxGeneration);
      }
      catch (Exception ex) { messages.Add(ex.Message); }
      messages.Add(string.Format(FileSizeFormatProvider.Factory(), "Total managed memory in GC after cleaning & collect: {0:fs}", System.GC.GetTotalMemory(false)));
      Elmah.ErrorSignal.FromCurrentContext().Raise(new Exception("CacheClear [CALLED]\n" + Utility.Implode(messages, "\n")));
      return messages;
    }


    //
    // classe ausiliaria per lo storage di null values nel caching subsystem
    //
    public class CachedNull { }


    //
    // query expression sulla interface class per il filtraggio dei nodi attivi
    // siccome viene usata per le correlazioni flag_deleted non e' opzionale 
    //
    public static Expression<Func<TEntity, bool>> FilterActive<TEntity>(this FS_Operations fsOp) where TEntity : class, IKGD_XNODE { return fsOp.FilterActive<TEntity>(fsOp.VersionFrozen, false); }
    public static Expression<Func<TEntity, bool>> FilterActive<TEntity>(this FS_Operations fsOp, bool include_deleted) where TEntity : class, IKGD_XNODE { return fsOp.FilterActive<TEntity>(fsOp.VersionFrozen, include_deleted); }
    public static Expression<Func<TEntity, bool>> FilterActive<TEntity>(this FS_Operations fsOp, int version_frozen, bool include_deleted) where TEntity : class, IKGD_XNODE
    {
      Expression<Func<TEntity, bool>> filter = PredicateBuilder.True<TEntity>();
      if (version_frozen == 0)
        filter = filter.And(n => n.flag_published);
      else if (version_frozen == -1)
      {
        filter = filter.And(n => n.flag_current);
        if (!include_deleted)
          filter = filter.And(n => !n.flag_deleted);
      }
      else
      {
        //per come gestiamo le relation forse e' il caso di filtrare anche i deleted
        filter = filter.And(n => (n.flag_published && n.version_frozen < version_frozen) || (n.version_frozen == version_frozen && !n.flag_deleted));
        //filter = filter.And(n => (n.flag_published && n.version_frozen < version_frozen) || (n.version_frozen == version_frozen));
      }

      return filter;
    }
    public static Expression<Func<TEntity, bool>> FilterFrozen<TEntity>(this FS_Operations fsOp, int version_frozen) where TEntity : class, IKGD_XNODE
    {
      Expression<Func<TEntity, bool>> filter = PredicateBuilder.True<TEntity>();
      filter = filter.And(n => (n.flag_published && n.version_frozen < version_frozen) || (n.version_frozen == version_frozen));
      return filter;
    }

    //
    // creazione automatica del filtro per il controllo degli accessi sul vNode
    //
    public static Expression<Func<IKGD_VNODE, bool>> Get_vNodeFilterACLv2(this FS_Operations fsOp) { return fsOp.Get_vNodeFilterACLv2(FS_Operations.FiltersVFS_Default); }
    public static Expression<Func<IKGD_VNODE, bool>> Get_vNodeFilterACLv2(this FS_Operations fsOp, bool filterLanguage) { return Get_vNodeFilterACLv2(fsOp, FS_Operations.FiltersVFS_Default | (filterLanguage ? FS_Operations.FilterVFS.Language : FS_Operations.FilterVFS.NoLanguage)); }
    public static Expression<Func<IKGD_VNODE, bool>> Get_vNodeFilterACLv2(this FS_Operations fsOp, FS_Operations.FilterVFS filters)
    {
      Expression<Func<IKGD_VNODE, bool>> vNodeFilter = PredicateBuilder.True<IKGD_VNODE>();
      // NB:
      // folders: vNode.language != null; vData.language == null;  (per folder con contenuti condivisi)
      // resources: vNode.language != null; vData.language == null;  (per resources con dati condivisi)
      // resources: vNode.language == null; vData.language != null;  (per resources con dati differenti)
      // "it" --> lingua forzata manualmente
      // null --> IKGD_Language_Provider.Provider.Language / fsOp.Language
      // "*" --> filtro per le lingue disabilitato
      //
      if (((filters & FS_Operations.FilterVFS.Language) == FS_Operations.FilterVFS.Language) && ((filters & FS_Operations.FilterVFS.NoLanguage) != FS_Operations.FilterVFS.NoLanguage))
      {
        string lang = IKGD_Language_Provider.Provider.Language ?? FS_Operations.LanguageNoFilterCode;
        if (lang != FS_Operations.LanguageNoFilterCode)
        {
          vNodeFilter = vNodeFilter.And(n => n.language == null || string.Equals(n.language, lang));  // attenzione language == "" e' una lingua valida!
        }
      }
      if ((filters & FS_Operations.FilterVFS.Folders) == FS_Operations.FilterVFS.Folders)
      {
        vNodeFilter = vNodeFilter.And(n => n.flag_folder);
      }
      //
      return vNodeFilter;
    }
    //
    public static Expression<Func<IKGD_VNODE, bool>> Get_vNodeFilterACLv2(this FS_Operations fsOp, string filterLanguage)
    {
      Expression<Func<IKGD_VNODE, bool>> vNodeFilter = PredicateBuilder.True<IKGD_VNODE>();
      // NB:
      // folders: vNode.language != null; vData.language == null;  (per folder con contenuti condivisi)
      // resources: vNode.language != null; vData.language == null;  (per resources con dati condivisi)
      // resources: vNode.language == null; vData.language != null;  (per resources con dati differenti)
      // "it" --> lingua forzata manualmente
      // null --> IKGD_Language_Provider.Provider.Language / fsOp.Language
      // "*" --> filtro per le lingue disabilitato
      string lang = filterLanguage ?? fsOp.LanguageNN; // faccio una copia per sicurezza v. problemi con il late binding
      if (lang != FS_Operations.LanguageNoFilterCode)
        vNodeFilter = vNodeFilter.And(n => n.language == null || string.Equals(n.language, lang));  // attenzione language == "" e' una lingua valida!
      return vNodeFilter;
    }
    //
    // creazione automatica del filtro per il controllo degli accessi sul vData
    //
    public static Expression<Func<IKGD_VDATA, bool>> Get_vDataFilterACLv2(this FS_Operations fsOp) { return fsOp.Get_vDataFilterACLv2(FS_Operations.FiltersVFS_Default); }
    public static Expression<Func<IKGD_VDATA, bool>> Get_vDataFilterACLv2(this FS_Operations fsOp, bool fullAccess) { return fsOp.Get_vDataFilterACLv2(FS_Operations.FiltersVFS_Default | (fullAccess ? FS_Operations.FilterVFS.Disabled : FS_Operations.FilterVFS.None)); }
    public static Expression<Func<IKGD_VDATA, bool>> Get_vDataFilterACLv2(this FS_Operations fsOp, bool fullAccess, bool filterAreas) { return fsOp.Get_vDataFilterACLv2((FS_Operations.FiltersVFS_Default | (fullAccess ? FS_Operations.FilterVFS.Disabled : FS_Operations.FilterVFS.None)) | (filterAreas ? FS_Operations.FilterVFS.ACL : FS_Operations.FilterVFS.NoACL)); }
    public static Expression<Func<IKGD_VDATA, bool>> Get_vDataFilterACLv2(this FS_Operations fsOp, FS_Operations.FilterVFS filters)
    {
      Expression<Func<IKGD_VDATA, bool>> vDataFilter = PredicateBuilder.True<IKGD_VDATA>();
      if (!fsOp.IsRoot && ((filters & FS_Operations.FilterVFS.ACL) == FS_Operations.FilterVFS.ACL) && ((filters & FS_Operations.FilterVFS.NoACL) != FS_Operations.FilterVFS.NoACL))
      {
        //vDataFilter = vDataFilter.And(n => fsOp.CurrentAreas.Contains(n.area));
        if (fsOp.CurrentAreasExtended.AreaMatchMode == FS_Areas_Extended.AreaMatchModeEnum.FilterByAllowed)
          vDataFilter = vDataFilter.And(n => fsOp.CurrentAreasExtended.AreasAllowed.Contains(n.area));
        else if (fsOp.CurrentAreasExtended.AreaMatchMode == FS_Areas_Extended.AreaMatchModeEnum.FilterByDenied)
          vDataFilter = vDataFilter.And(n => !fsOp.CurrentAreasExtended.AreasDenied.Contains(n.area));
      }
      if ((filters & FS_Operations.FilterVFS.Disabled) != FS_Operations.FilterVFS.Disabled)
      {
        vDataFilter = vDataFilter.And(n => !n.flag_inactive);
      }
      if (((filters & FS_Operations.FilterVFS.Dates) == FS_Operations.FilterVFS.Dates) && ((filters & FS_Operations.FilterVFS.NoDates) != FS_Operations.FilterVFS.NoDates))
      {
        vDataFilter = vDataFilter.And(n => (n.date_activation == null || n.date_activation.Value <= fsOp.DateTimeContext) && (n.date_expiry == null || n.date_expiry.Value >= fsOp.DateTimeContext));
      }
      if ((filters & FS_Operations.FilterVFS.Unstructured) == FS_Operations.FilterVFS.Unstructured)
      {
        vDataFilter = vDataFilter.And(n => n.flag_unstructured);
      }
      // viene gia' gestito nei filtri per il versioning
      //if ((filters & FS_Operations.FilterVFS.Deleted) == FS_Operations.FilterVFS.Deleted)
      //{
      //  vDataFilter = vDataFilter.And(n => !n.flag_inactive);
      //}
      return vDataFilter;
    }


    //
    // filtro per ottenere il dataset attivo
    //
    public static IQueryable<TEntity> NodesActive<TEntity>(this FS_Operations fsOp) where TEntity : class, IKGD_XNODE { return fsOp.NodesActive<TEntity>(fsOp.VersionFrozen, false); }
    public static IQueryable<TEntity> NodesActive<TEntity>(this FS_Operations fsOp, bool include_deleted) where TEntity : class, IKGD_XNODE { return fsOp.NodesActive<TEntity>(fsOp.VersionFrozen, include_deleted); }
    public static IQueryable<TEntity> NodesActive<TEntity>(this FS_Operations fsOp, int version_frozen, bool include_deleted) where TEntity : class, IKGD_XNODE
    {
      Expression<Func<TEntity, bool>> filter = PredicateBuilder.True<TEntity>();
      if (version_frozen == 0)
        filter = filter.And(n => n.flag_published);
      else if (version_frozen == -1)
      {
        filter = filter.And(n => n.flag_current);
        if (!include_deleted)
          filter = filter.And(n => !n.flag_deleted);
      }
      else
      {
        //per come gestiamo le relation forse e' il caso di filtrare anche i deleted
        filter = filter.And(n => (n.flag_published && n.version_frozen < version_frozen) || (n.version_frozen == version_frozen && !n.flag_deleted));
        //filter = filter.And(n => (n.flag_published && n.version_frozen < version_frozen) || (n.version_frozen == version_frozen));
      }
      return fsOp.DB.GetTable<TEntity>().AsExpandable().Where(filter);
    }
    //
    public static IKGD_VNODE NodeActive(this FS_Operations fsOp, IKGD_SNODE sNode) { return sNode.IKGD_VNODEs.AsQueryable().FirstOrDefault(fsOp.FilterActive<IKGD_VNODE>()); }
    public static IKGD_VNODE NodeActive(this FS_Operations fsOp, IKGD_SNODE sNode, bool include_deleted) { return sNode.IKGD_VNODEs.AsQueryable().FirstOrDefault(fsOp.FilterActive<IKGD_VNODE>(include_deleted)); }
    public static IKGD_VNODE NodeActive(this FS_Operations fsOp, int sNodeCode) { return fsOp.NodesActive<IKGD_VNODE>().FirstOrDefault(n => n.snode == sNodeCode); }
    public static IKGD_VNODE NodeActive(this FS_Operations fsOp, int sNodeCode, bool include_deleted) { return fsOp.NodesActive<IKGD_VNODE>(include_deleted).FirstOrDefault(n => n.snode == sNodeCode); }
    //
    public static TEntity Get_Active<TEntity>(this FS_Operations fsOp, int rNodeCode) where TEntity : class, IKGD_XNODE { return fsOp.NodesActive<TEntity>().FirstOrDefault(n => n.rnode == rNodeCode); }
    public static TEntity Get_Active<TEntity>(this FS_Operations fsOp, IKGD_hasRNODE entity) where TEntity : class, IKGD_XNODE { return fsOp.NodesActive<TEntity>().FirstOrDefault(n => n.rnode == entity.rnode); }
    //
    public static IKGD_VDATA Get_VDATA<TEntity>(this FS_Operations fsOp, TEntity entity) where TEntity : class, IKGD_hasRNODE { return fsOp.Get_Active<IKGD_VDATA>(entity.rnode); }
    public static IKGD_INODE Get_INODE<TEntity>(this FS_Operations fsOp, TEntity entity) where TEntity : class, IKGD_hasRNODE { return fsOp.Get_Active<IKGD_INODE>(entity.rnode); }
    //
    public static FS_Operations.FilterVFS BuildFilters(bool fullAccess, bool filterLanguage, bool filterAreas, bool includeDeleted)
    {
      FS_Operations.FilterVFS filters = FS_Operations.FiltersVFS_Default;
      if (fullAccess)
        filters = filters | FS_Operations.FilterVFS.Disabled;
      if (includeDeleted)
        filters = filters | FS_Operations.FilterVFS.Deleted;
      if (!filterLanguage)
        filters = filters & (FS_Operations.FilterVFS)(~(int)FS_Operations.FilterVFS.Language);
      if (!filterAreas)
        filters = filters & (FS_Operations.FilterVFS)(~(int)FS_Operations.FilterVFS.ACL);
      return filters;
    }
    //
    public static FS_Operations.FS_NodeInfoExt Get_NodeInfoExtACL(this FS_Operations fsOp, int sNodeCode) { return Get_NodeInfoExtACL(fsOp, sNodeCode, false, false); }
    public static FS_Operations.FS_NodeInfoExt Get_NodeInfoExtACL(this FS_Operations fsOp, int sNodeCode, bool fullAccess) { return Get_NodeInfoExtACL(fsOp, sNodeCode, fullAccess, false); }
    public static FS_Operations.FS_NodeInfoExt Get_NodeInfoExtACL(this FS_Operations fsOp, int sNodeCode, bool fullAccess, bool includeDeleted)
    {
      Expression<Func<IKGD_VNODE, bool>> vNodeFilterAll = fsOp.Get_vNodeFilterACLv2();
      Expression<Func<IKGD_VDATA, bool>> vDataFilterAll = fsOp.Get_vDataFilterACLv2(fullAccess);
      return
        (from vNode in fsOp.NodesActive<IKGD_VNODE>(includeDeleted).Where(vNodeFilterAll).Where(n => n.snode == sNodeCode)
         from vData in fsOp.NodesActive<IKGD_VDATA>(includeDeleted).Where(vDataFilterAll).Where(n => n.rnode == vNode.rnode)
         from iNode in fsOp.NodesActive<IKGD_INODE>(includeDeleted).Where(n => n.rnode == vNode.rnode).DefaultIfEmpty()
         join rel in fsOp.NodesActive<IKGD_RELATION>(includeDeleted) on vNode.rnode equals rel.rnode into rels
         join prp in fsOp.NodesActive<IKGD_PROPERTY>(includeDeleted) on vNode.rnode equals prp.rnode into prps
         select new FS_Operations.FS_NodeInfoExt { vNode = vNode, vData = vData, iNode = iNode, Relations = rels.ToList(), Properties = prps.ToList() }).FirstOrDefault();
    }
    //
    public static FS_Operations.FS_NodeInfo Get_NodeInfoACL(this FS_Operations fsOp, int sNodeCode, bool? get_iNode) { return Get_NodeInfoACL(fsOp, sNodeCode, get_iNode, false, false); }
    public static FS_Operations.FS_NodeInfo Get_NodeInfoACL(this FS_Operations fsOp, int sNodeCode, bool? get_iNode, bool fullAccess) { return Get_NodeInfoACL(fsOp, sNodeCode, get_iNode, fullAccess, false); }
    public static FS_Operations.FS_NodeInfo Get_NodeInfoACL(this FS_Operations fsOp, int sNodeCode, bool? get_iNode, bool fullAccess, bool includeDeleted)
    {
      Expression<Func<IKGD_VNODE, bool>> vNodeFilterAll = fsOp.Get_vNodeFilterACLv2();
      Expression<Func<IKGD_VDATA, bool>> vDataFilterAll = fsOp.Get_vDataFilterACLv2(fullAccess);
      if (get_iNode == null)
      {
        return (from vNode in fsOp.NodesActive<IKGD_VNODE>(includeDeleted).Where(vNodeFilterAll).Where(n => n.snode == sNodeCode)
                from vData in fsOp.NodesActive<IKGD_VDATA>(includeDeleted).Where(vDataFilterAll).Where(n => n.rnode == vNode.rnode)
                from iNode in fsOp.NodesActive<IKGD_INODE>(includeDeleted).Where(n => n.rnode == vNode.rnode).DefaultIfEmpty()
                select new FS_Operations.FS_NodeInfo { vNode = vNode, vData = vData, iNode = iNode }).FirstOrDefault();
      }
      else if (get_iNode.Value)
      {
        return (from vNode in fsOp.NodesActive<IKGD_VNODE>(includeDeleted).Where(vNodeFilterAll).Where(n => n.snode == sNodeCode)
                from vData in fsOp.NodesActive<IKGD_VDATA>(includeDeleted).Where(vDataFilterAll).Where(n => n.rnode == vNode.rnode)
                from iNode in fsOp.NodesActive<IKGD_INODE>(includeDeleted).Where(n => n.rnode == vNode.rnode)
                select new FS_Operations.FS_NodeInfo { vNode = vNode, vData = vData, iNode = iNode }).FirstOrDefault();
      }
      else
      {
        return (from vNode in fsOp.NodesActive<IKGD_VNODE>(true).Where(vNodeFilterAll).Where(n => n.snode == sNodeCode)
                from vData in fsOp.NodesActive<IKGD_VDATA>(true).Where(vDataFilterAll).Where(n => n.rnode == vNode.rnode)
                select new FS_Operations.FS_NodeInfo { vNode = vNode, vData = vData }).FirstOrDefault();
      }
    }
    //
    public static IQueryable<FS_Operations.FS_NodeInfo> Get_NodesInfoFiltered(this FS_Operations fsOp, Expression<Func<IKGD_VNODE, bool>> vNodeFilter, Expression<Func<IKGD_VDATA, bool>> vDataFilter, bool? get_iNode) { return Get_NodesInfoFiltered(fsOp, vNodeFilter, vDataFilter, get_iNode, FS_Operations.FilterVFS.ACL | FS_Operations.FilterVFS.Language); }
    public static IQueryable<FS_Operations.FS_NodeInfo> Get_NodesInfoFiltered(this FS_Operations fsOp, Expression<Func<IKGD_VNODE, bool>> vNodeFilter, Expression<Func<IKGD_VDATA, bool>> vDataFilter, bool? get_iNode, bool fullAccess) { return Get_NodesInfoFiltered(fsOp, vNodeFilter, vDataFilter, get_iNode, BuildFilters(fullAccess, true, true, false)); }
    public static IQueryable<FS_Operations.FS_NodeInfo> Get_NodesInfoFiltered(this FS_Operations fsOp, Expression<Func<IKGD_VNODE, bool>> vNodeFilter, Expression<Func<IKGD_VDATA, bool>> vDataFilter, bool? get_iNode, bool fullAccess, bool filterLanguage, bool includeDeleted) { return Get_NodesInfoFiltered(fsOp, vNodeFilter, vDataFilter, get_iNode, BuildFilters(fullAccess, filterLanguage, true, includeDeleted)); }
    public static IQueryable<FS_Operations.FS_NodeInfo> Get_NodesInfoFiltered(this FS_Operations fsOp, Expression<Func<IKGD_VNODE, bool>> vNodeFilter, Expression<Func<IKGD_VDATA, bool>> vDataFilter, bool? get_iNode, FS_Operations.FilterVFS filters)
    {
      bool includeDeleted = (filters & FS_Operations.FilterVFS.Deleted) == FS_Operations.FilterVFS.Deleted;
      Expression<Func<IKGD_VNODE, bool>> vNodeFilterAll = fsOp.Get_vNodeFilterACLv2(filters);
      Expression<Func<IKGD_VDATA, bool>> vDataFilterAll = fsOp.Get_vDataFilterACLv2(filters);
      if (vNodeFilter != null)
        vNodeFilterAll = vNodeFilterAll.And(vNodeFilter.Expand());
      if (vDataFilter != null)
        vDataFilterAll = vDataFilterAll.And(vDataFilter.Expand());
      if (get_iNode == null)
      {
        return from vNode in fsOp.NodesActive<IKGD_VNODE>(includeDeleted).Where(vNodeFilterAll)
               from vData in fsOp.NodesActive<IKGD_VDATA>(includeDeleted).Where(vDataFilterAll).Where(n => n.rnode == vNode.rnode)
               from iNode in fsOp.NodesActive<IKGD_INODE>(includeDeleted).Where(n => n.rnode == vNode.rnode).DefaultIfEmpty()
               select new FS_Operations.FS_NodeInfo { vNode = vNode, vData = vData, iNode = iNode };
      }
      else if (get_iNode.Value)
      {
        return from vNode in fsOp.NodesActive<IKGD_VNODE>(includeDeleted).Where(vNodeFilterAll)
               from vData in fsOp.NodesActive<IKGD_VDATA>(includeDeleted).Where(vDataFilterAll).Where(n => n.rnode == vNode.rnode)
               from iNode in fsOp.NodesActive<IKGD_INODE>(includeDeleted).Where(n => n.rnode == vNode.rnode)
               select new FS_Operations.FS_NodeInfo { vNode = vNode, vData = vData, iNode = iNode };
      }
      else
      {
        return from vNode in fsOp.NodesActive<IKGD_VNODE>(includeDeleted).Where(vNodeFilterAll)
               from vData in fsOp.NodesActive<IKGD_VDATA>(includeDeleted).Where(vDataFilterAll).Where(n => n.rnode == vNode.rnode)
               select new FS_Operations.FS_NodeInfo { vNode = vNode, vData = vData };
      }
    }


    //
    // attenzione che sul return esegue un cast sull'interface, ma non so se questo interferisce con la late execution
    public static IQueryable<FS_Operations.FS_NodeInfo_Interface> Get_NodesInfoFilteredExt1(this FS_Operations fsOp, Expression<Func<IKGD_VNODE, bool>> vNodeFilter, Expression<Func<IKGD_VDATA, bool>> vDataFilter) { return Get_NodesInfoFilteredExt1(fsOp, vNodeFilter, vDataFilter, FS_Operations.FiltersVFS_Default); }
    //public static IQueryable<FS_Operations.FS_NodeInfo_Interface> Get_NodesInfoFilteredExt1(this FS_Operations fsOp, Expression<Func<IKGD_VNODE, bool>> vNodeFilter, Expression<Func<IKGD_VDATA, bool>> vDataFilter, bool fullAccess, bool filterLanguage, bool filterAreas, bool includeDeleted) { return Get_NodesInfoFilteredExt1(fsOp, vNodeFilter, vDataFilter, BuildFilters(fullAccess, filterLanguage, filterAreas, includeDeleted)); }
    public static IQueryable<FS_Operations.FS_NodeInfo_Interface> Get_NodesInfoFilteredExt1(this FS_Operations fsOp, Expression<Func<IKGD_VNODE, bool>> vNodeFilter, Expression<Func<IKGD_VDATA, bool>> vDataFilter, FS_Operations.FilterVFS filters)
    {
      bool includeDeleted = (filters & FS_Operations.FilterVFS.Deleted) == FS_Operations.FilterVFS.Deleted;
      Expression<Func<IKGD_VNODE, bool>> vNodeFilterAll = fsOp.Get_vNodeFilterACLv2(filters);
      Expression<Func<IKGD_VDATA, bool>> vDataFilterAll = fsOp.Get_vDataFilterACLv2(filters);
      if (vNodeFilter != null)
        vNodeFilterAll = vNodeFilterAll.And(vNodeFilter.Expand());
      if (vDataFilter != null)
        vDataFilterAll = vDataFilterAll.And(vDataFilter.Expand());
      //
      IQueryable<FS_Operations.FS_NodeInfo_Interface> vfsNodes = null;
      //
      vfsNodes =
        from vNode in fsOp.NodesActive<IKGD_VNODE>(includeDeleted).Where(vNodeFilterAll)
        from vData in fsOp.NodesActive<IKGD_VDATA>(includeDeleted).Where(vDataFilterAll).Where(n => n.rnode == vNode.rnode)
        from iNode in fsOp.NodesActive<IKGD_INODE>(includeDeleted).Where(n => n.rnode == vNode.rnode).DefaultIfEmpty()
        select new FS_Operations.FS_NodeInfo { vNode = vNode, vData = vData, iNode = iNode } as FS_Operations.FS_NodeInfo_Interface;
      //
      return vfsNodes.Cast<FS_Operations.FS_NodeInfo_Interface>();
    }
    //
    public static IQueryable<FS_Operations.FS_NodeInfoExt_Interface> Get_NodesInfoFilteredExt2(this FS_Operations fsOp, Expression<Func<IKGD_VNODE, bool>> vNodeFilter, Expression<Func<IKGD_VDATA, bool>> vDataFilter) { return Get_NodesInfoFilteredExt2(fsOp, vNodeFilter, vDataFilter, FS_Operations.FiltersVFS_Default); }
    public static IQueryable<FS_Operations.FS_NodeInfoExt_Interface> Get_NodesInfoFilteredExt2(this FS_Operations fsOp, Expression<Func<IKGD_VNODE, bool>> vNodeFilter, Expression<Func<IKGD_VDATA, bool>> vDataFilter, FS_Operations.FilterVFS filters)
    {
      bool includeDeleted = (filters & FS_Operations.FilterVFS.Deleted) == FS_Operations.FilterVFS.Deleted;
      Expression<Func<IKGD_VNODE, bool>> vNodeFilterAll = fsOp.Get_vNodeFilterACLv2(filters);
      Expression<Func<IKGD_VDATA, bool>> vDataFilterAll = fsOp.Get_vDataFilterACLv2(filters);
      if (vNodeFilter != null)
        vNodeFilterAll = vNodeFilterAll.And(vNodeFilter.Expand());
      if (vDataFilter != null)
        vDataFilterAll = vDataFilterAll.And(vDataFilter.Expand());
      //
      IQueryable<FS_Operations.FS_NodeInfoExt_Interface> vfsNodes = null;
      //
      vfsNodes =
        from vNode in fsOp.NodesActive<IKGD_VNODE>(includeDeleted).Where(vNodeFilterAll)
        from vData in fsOp.NodesActive<IKGD_VDATA>(includeDeleted).Where(vDataFilterAll).Where(n => n.rnode == vNode.rnode)
        from iNode in fsOp.NodesActive<IKGD_INODE>(includeDeleted).Where(n => n.rnode == vNode.rnode).DefaultIfEmpty()
        join rel in fsOp.NodesActive<IKGD_RELATION>(includeDeleted) on vNode.rnode equals rel.rnode into rels
        join prp in fsOp.NodesActive<IKGD_PROPERTY>(includeDeleted) on vNode.rnode equals prp.rnode into prps
        select new FS_Operations.FS_NodeInfoExt { vNode = vNode, vData = vData, iNode = iNode, Relations = rels.ToList(), Properties = prps.ToList() } as FS_Operations.FS_NodeInfoExt_Interface;
      //
      return vfsNodes.Cast<FS_Operations.FS_NodeInfoExt_Interface>();
    }
    //
    public static IQueryable<FS_Operations.FS_NodeInfoExt_Interface> Get_NodesInfoFilteredExt2(this FS_Operations fsOp, Expression<Func<IKGD_VNODE, bool>> vNodeFilter, Expression<Func<IKGD_VDATA, bool>> vDataFilter, Expression<Func<IKGD_PROPERTY, bool>> propertiesFilter, Expression<Func<IKGD_RELATION, bool>> relationsFilter) { return Get_NodesInfoFilteredExt2(fsOp, vNodeFilter, vDataFilter, propertiesFilter, relationsFilter, FS_Operations.FiltersVFS_Default); }
    public static IQueryable<FS_Operations.FS_NodeInfoExt_Interface> Get_NodesInfoFilteredExt2(this FS_Operations fsOp, Expression<Func<IKGD_VNODE, bool>> vNodeFilter, Expression<Func<IKGD_VDATA, bool>> vDataFilter, Expression<Func<IKGD_PROPERTY, bool>> propertiesFilter, Expression<Func<IKGD_RELATION, bool>> relationsFilter, bool fullAccess, bool filterLanguage, bool filterAreas, bool includeDeleted) { return Get_NodesInfoFilteredExt2(fsOp, vNodeFilter, vDataFilter, propertiesFilter, relationsFilter, BuildFilters(fullAccess, filterLanguage, filterAreas, includeDeleted)); }
    public static IQueryable<FS_Operations.FS_NodeInfoExt_Interface> Get_NodesInfoFilteredExt2(this FS_Operations fsOp, Expression<Func<IKGD_VNODE, bool>> vNodeFilter, Expression<Func<IKGD_VDATA, bool>> vDataFilter, Expression<Func<IKGD_PROPERTY, bool>> propertiesFilter, Expression<Func<IKGD_RELATION, bool>> relationsFilter, FS_Operations.FilterVFS filters)
    {
      if (propertiesFilter == null && relationsFilter == null)
      {
        return Get_NodesInfoFilteredExt2(fsOp, vNodeFilter, vDataFilter, filters);
      }
      bool includeDeleted = (filters & FS_Operations.FilterVFS.Deleted) == FS_Operations.FilterVFS.Deleted;
      Expression<Func<IKGD_VNODE, bool>> vNodeFilterAll = fsOp.Get_vNodeFilterACLv2(filters);
      Expression<Func<IKGD_VDATA, bool>> vDataFilterAll = fsOp.Get_vDataFilterACLv2(filters);
      Expression<Func<IKGD_PROPERTY, bool>> propertiesFilterAll = PredicateBuilder.True<IKGD_PROPERTY>();
      Expression<Func<IKGD_RELATION, bool>> relationsFilterAll = PredicateBuilder.True<IKGD_RELATION>();
      if (vNodeFilter != null)
        vNodeFilterAll = vNodeFilterAll.And(vNodeFilter.Expand());
      if (vDataFilter != null)
        vDataFilterAll = vDataFilterAll.And(vDataFilter.Expand());
      if (propertiesFilter != null)
        propertiesFilterAll = propertiesFilterAll.And(propertiesFilter.Expand());
      if (relationsFilter != null)
        relationsFilterAll = relationsFilterAll.And(relationsFilter.Expand());
      //
      IQueryable<FS_Operations.FS_NodeInfoExt_Interface> vfsNodes = null;
      //
      vfsNodes =
        from vNode in fsOp.NodesActive<IKGD_VNODE>(includeDeleted).Where(vNodeFilterAll)
        from vData in fsOp.NodesActive<IKGD_VDATA>(includeDeleted).Where(vDataFilterAll).Where(n => n.rnode == vNode.rnode)
        from iNode in fsOp.NodesActive<IKGD_INODE>(includeDeleted).Where(n => n.rnode == vNode.rnode).DefaultIfEmpty()
        join rel in fsOp.NodesActive<IKGD_RELATION>(includeDeleted) on vNode.rnode equals rel.rnode into rels
        join prp in fsOp.NodesActive<IKGD_PROPERTY>(includeDeleted) on vNode.rnode equals prp.rnode into prps
        where !rels.Any() || rels.Where(relationsFilterAll.Compile()).Any()
        where !prps.Any() || prps.Where(propertiesFilterAll.Compile()).Any()
        select new FS_Operations.FS_NodeInfoExt { vNode = vNode, vData = vData, iNode = iNode, Relations = rels.ToList(), Properties = prps.ToList() } as FS_Operations.FS_NodeInfoExt_Interface;
      //
      return vfsNodes.Cast<FS_Operations.FS_NodeInfoExt_Interface>();
    }
    //
    public static IQueryable<FS_Operations.FS_NodeInfoExt2_Interface> Get_NodesInfoFilteredExt3(this FS_Operations fsOp, Expression<Func<IKGD_VNODE, bool>> vNodeFilter, Expression<Func<IKGD_VDATA, bool>> vDataFilter) { return Get_NodesInfoFilteredExt3(fsOp, vNodeFilter, vDataFilter, FS_Operations.FiltersVFS_Default); }
    public static IQueryable<FS_Operations.FS_NodeInfoExt2_Interface> Get_NodesInfoFilteredExt3(this FS_Operations fsOp, Expression<Func<IKGD_VNODE, bool>> vNodeFilter, Expression<Func<IKGD_VDATA, bool>> vDataFilter, FS_Operations.FilterVFS filters)
    {
      bool includeDeleted = (filters & FS_Operations.FilterVFS.Deleted) == FS_Operations.FilterVFS.Deleted;
      Expression<Func<IKGD_VNODE, bool>> vNodeFilterAll = fsOp.Get_vNodeFilterACLv2(filters);
      Expression<Func<IKGD_VDATA, bool>> vDataFilterAll = fsOp.Get_vDataFilterACLv2(filters);
      if (vNodeFilter != null)
        vNodeFilterAll = vNodeFilterAll.And(vNodeFilter.Expand());
      if (vDataFilter != null)
        vDataFilterAll = vDataFilterAll.And(vDataFilter.Expand());
      //
      IQueryable<FS_Operations.FS_NodeInfoExt2_Interface> vfsNodes = null;
      //
      vfsNodes =
        from vNode in fsOp.NodesActive<IKGD_VNODE>(includeDeleted).Where(vNodeFilterAll)
        from vData in fsOp.NodesActive<IKGD_VDATA>(includeDeleted).Where(vDataFilterAll).Where(n => n.rnode == vNode.rnode)
        from iNode in fsOp.NodesActive<IKGD_INODE>(includeDeleted).Where(n => n.rnode == vNode.rnode).DefaultIfEmpty()
        join rel in fsOp.NodesActive<IKGD_RELATION>(includeDeleted) on vNode.rnode equals rel.rnode into rels
        join prp in fsOp.NodesActive<IKGD_PROPERTY>(includeDeleted) on vNode.rnode equals prp.rnode into prps
        join vrs in fsOp.DB.IKATT_AttributeMappings on vNode.rnode equals vrs.rNode into variants
        select new FS_Operations.FS_NodeInfoExt2 { vNode = vNode, vData = vData, iNode = iNode, Relations = rels.ToList(), Properties = prps.ToList(), Variants = variants.ToList() } as FS_Operations.FS_NodeInfoExt2_Interface;
      //
      return vfsNodes.Cast<FS_Operations.FS_NodeInfoExt2_Interface>();
    }


    //
    public static IQueryable<FS_Operations.FS_NodeInfo> Get_NodesInfoACL(this FS_Operations fsOp, IEnumerable<int> sNodeCodes, bool? get_iNode) { return Get_NodesInfoACL(fsOp, sNodeCodes, get_iNode, FS_Operations.FiltersVFS_Default); }
    public static IQueryable<FS_Operations.FS_NodeInfo> Get_NodesInfoACL(this FS_Operations fsOp, IEnumerable<int> sNodeCodes, bool? get_iNode, bool fullAccess) { return Get_NodesInfoACL(fsOp, sNodeCodes, get_iNode, BuildFilters(fullAccess, false, true, false)); }
    public static IQueryable<FS_Operations.FS_NodeInfo> Get_NodesInfoACL(this FS_Operations fsOp, IEnumerable<int> sNodeCodes, bool? get_iNode, bool fullAccess, bool includeDeleted) { return Get_NodesInfoACL(fsOp, sNodeCodes, get_iNode, BuildFilters(fullAccess, false, true, includeDeleted)); }
    public static IQueryable<FS_Operations.FS_NodeInfo> Get_NodesInfoACL(this FS_Operations fsOp, IEnumerable<int> sNodeCodes, bool? get_iNode, FS_Operations.FilterVFS filters)
    {
      bool includeDeleted = (filters & FS_Operations.FilterVFS.Deleted) == FS_Operations.FilterVFS.Deleted;
      //Expression<Func<IKGD_VNODE, bool>> vNodeFilterAll = fsOp.Get_vNodeFilterACLv2(filters);
      Expression<Func<IKGD_VDATA, bool>> vDataFilterAll = fsOp.Get_vDataFilterACLv2(filters);
      if (get_iNode == null)
      {
        return from vNode in fsOp.NodesActive<IKGD_VNODE>(includeDeleted).Where(n => sNodeCodes.Contains(n.snode))
               from vData in fsOp.NodesActive<IKGD_VDATA>(includeDeleted).Where(vDataFilterAll).Where(n => n.rnode == vNode.rnode)
               from iNode in fsOp.NodesActive<IKGD_INODE>(includeDeleted).Where(n => n.rnode == vNode.rnode).DefaultIfEmpty()
               select new FS_Operations.FS_NodeInfo { vNode = vNode, vData = vData, iNode = iNode };
      }
      else if (get_iNode.Value)
      {
        return from vNode in fsOp.NodesActive<IKGD_VNODE>(includeDeleted).Where(n => sNodeCodes.Contains(n.snode))
               from vData in fsOp.NodesActive<IKGD_VDATA>(includeDeleted).Where(vDataFilterAll).Where(n => n.rnode == vNode.rnode)
               from iNode in fsOp.NodesActive<IKGD_INODE>(includeDeleted).Where(n => n.rnode == vNode.rnode)
               select new FS_Operations.FS_NodeInfo { vNode = vNode, vData = vData, iNode = iNode };
      }
      else
      {
        return from vNode in fsOp.NodesActive<IKGD_VNODE>(includeDeleted).Where(n => sNodeCodes.Contains(n.snode))
               from vData in fsOp.NodesActive<IKGD_VDATA>(includeDeleted).Where(vDataFilterAll).Where(n => n.rnode == vNode.rnode)
               select new FS_Operations.FS_NodeInfo { vNode = vNode, vData = vData };
      }
    }
    //
    public static FS_Operations.FS_NodeInfo Get_NodeInfo(this FS_Operations fsOp, int sNodeCode, bool? get_iNode) { return Get_NodeInfo(fsOp, sNodeCode, get_iNode, false); }
    public static FS_Operations.FS_NodeInfo Get_NodeInfo(this FS_Operations fsOp, int sNodeCode, bool? get_iNode, bool includeDeleted)
    {
      if (get_iNode == null)
      {
        return (from vNode in fsOp.NodesActive<IKGD_VNODE>(includeDeleted).Where(n => n.snode == sNodeCode)
                from vData in fsOp.NodesActive<IKGD_VDATA>(includeDeleted).Where(n => n.rnode == vNode.rnode)
                from iNode in fsOp.NodesActive<IKGD_INODE>(includeDeleted).Where(n => n.rnode == vNode.rnode).DefaultIfEmpty()
                select new FS_Operations.FS_NodeInfo { vNode = vNode, vData = vData, iNode = iNode }).FirstOrDefault();
      }
      else if (get_iNode.Value)
      {
        return (from vNode in fsOp.NodesActive<IKGD_VNODE>(includeDeleted).Where(n => n.snode == sNodeCode)
                from vData in fsOp.NodesActive<IKGD_VDATA>(includeDeleted).Where(n => n.rnode == vNode.rnode)
                from iNode in fsOp.NodesActive<IKGD_INODE>(includeDeleted).Where(n => n.rnode == vNode.rnode)
                select new FS_Operations.FS_NodeInfo { vNode = vNode, vData = vData, iNode = iNode }).FirstOrDefault();
      }
      else
      {
        return (from vNode in fsOp.NodesActive<IKGD_VNODE>(includeDeleted).Where(n => n.snode == sNodeCode)
                from vData in fsOp.NodesActive<IKGD_VDATA>(includeDeleted).Where(n => n.rnode == vNode.rnode)
                select new FS_Operations.FS_NodeInfo { vNode = vNode, vData = vData }).FirstOrDefault();
      }
    }
    //
    public static IQueryable<FS_Operations.FS_NodeInfo> Get_NodesInfo(this FS_Operations fsOp, IEnumerable<int> sNodeCodes, bool? get_iNode) { return Get_NodesInfo(fsOp, sNodeCodes, get_iNode, false); }
    public static IQueryable<FS_Operations.FS_NodeInfo> Get_NodesInfo(this FS_Operations fsOp, IEnumerable<int> sNodeCodes, bool? get_iNode, bool includeDeleted)
    {
      if (get_iNode == null)
      {
        return from vNode in fsOp.NodesActive<IKGD_VNODE>(includeDeleted).Where(n => sNodeCodes.Contains(n.snode))
               from vData in fsOp.NodesActive<IKGD_VDATA>(includeDeleted).Where(n => n.rnode == vNode.rnode)
               from iNode in fsOp.NodesActive<IKGD_INODE>(includeDeleted).Where(n => n.rnode == vNode.rnode).DefaultIfEmpty()
               select new FS_Operations.FS_NodeInfo { vNode = vNode, vData = vData, iNode = iNode };
      }
      else if (get_iNode.Value)
      {
        return from vNode in fsOp.NodesActive<IKGD_VNODE>(includeDeleted).Where(n => sNodeCodes.Contains(n.snode))
               from vData in fsOp.NodesActive<IKGD_VDATA>(includeDeleted).Where(n => n.rnode == vNode.rnode)
               from iNode in fsOp.NodesActive<IKGD_INODE>(includeDeleted).Where(n => n.rnode == vNode.rnode)
               select new FS_Operations.FS_NodeInfo { vNode = vNode, vData = vData, iNode = iNode };
      }
      else
      {
        return from vNode in fsOp.NodesActive<IKGD_VNODE>(includeDeleted).Where(n => sNodeCodes.Contains(n.snode))
               from vData in fsOp.NodesActive<IKGD_VDATA>(includeDeleted).Where(n => n.rnode == vNode.rnode)
               select new FS_Operations.FS_NodeInfo { vNode = vNode, vData = vData };
      }
    }


    //
    public static IQueryable<IKGD_PROPERTY> Get_PROPERTies<TEntity>(this FS_Operations fsOp, int rNodeCode) where TEntity : class, IKGD_hasRNODE { return fsOp.NodesActive<IKGD_PROPERTY>().Where(r => r.rnode == rNodeCode); }
    public static IQueryable<IKGD_PROPERTY> Get_PROPERTies<TEntity>(this FS_Operations fsOp, TEntity entity) where TEntity : class, IKGD_hasRNODE { return fsOp.NodesActive<IKGD_PROPERTY>().Where(r => r.rnode == entity.rnode); }
    //public static IQueryable<IKGD_RELATION> Get_RELATIONs(this FS_Operations fsOp, int sNodeCode) { return fsOp.NodesActive<IKGD_RELATION>().Where(r => r.snode_src == sNodeCode); }
    //public static IQueryable<IKGD_RELATION> Get_RELATIONs(this FS_Operations fsOp, IKGD_SNODE entity) { return fsOp.NodesActive<IKGD_RELATION>().Where(r => r.snode_src == entity.code); }
    //public static IQueryable<IKGD_RELATION> Get_RELATIONs(this FS_Operations fsOp, IKGD_VNODE entity) { return fsOp.NodesActive<IKGD_RELATION>().Where(r => r.snode_src == entity.snode); }
    public static IQueryable<IKGD_RELATION> Get_RELATIONs(this FS_Operations fsOp, IKGD_hasRNODE entity) { return fsOp.NodesActive<IKGD_RELATION>().Where(r => r.rnode == entity.rnode); }
    public static IQueryable<IKGD_RELATION> Get_RELATIONs(this FS_Operations fsOp, int? rNodeCode, int? sNodeCode, int? sNodeCodeStrict)
    {
      if (rNodeCode != null)
        return fsOp.NodesActive<IKGD_RELATION>().Where(r => r.rnode == rNodeCode.Value);
      else if (sNodeCodeStrict != null)  // accesso con mapping diretto su snode_src
        return fsOp.NodesActive<IKGD_RELATION>().Where(r => r.snode_src == sNodeCodeStrict.Value);
      else if (sNodeCode != null)  // accesso con mapping indiretto su snode_src: equivalente ad usare rnode
        return fsOp.NodesActive<IKGD_RELATION>().Where(r => r.IKGD_RNODE.IKGD_SNODEs.Any(n => n.code == sNodeCode.Value));
      return Enumerable.Empty<IKGD_RELATION>().AsQueryable();
    }
    //
    //public static FS_ACL_Reduced ReduceACLs<TEntity>(this FS_Operations fsOp, IEnumerable<IKGD_ACL> ACLs, TEntity entity) where TEntity : class, IKGD_hasRNODE, IKGD_hasFolderInfo { return fsOp.IsRoot ? FS_ACL_Reduced.GetRootACLs(entity) : new FS_ACL_Reduced(ACLs, null); }

    //
    // estrazione e parsing dei multi-streams
    //
    public static byte[] Get_MSTREAM_Data<TEntity>(this FS_Operations fsOp, TEntity entity, string source, string key) where TEntity : class, IKGD_hasRNODE { return fsOp.Get_MSTREAM_Data(fsOp.Get_Active<IKGD_INODE>(entity.rnode), source, key); }
    public static byte[] Get_MSTREAM_Data(this FS_Operations fsOp, int rNodeCode, string source, string key) { return fsOp.Get_MSTREAM_Data(fsOp.Get_Active<IKGD_INODE>(rNodeCode), source, key); }
    public static byte[] Get_MSTREAM_Data(this FS_Operations fsOp, IKGD_INODE entity, string source, string key)
    {
      try { return fsOp.DB.IKGD_STREAMs.FirstOrDefault(s => s.IKGD_MSTREAMs.Any(m => m.IKGD_INODE.version == entity.version) && (source == null || s.source == source) && s.key == key).data.ToArray(); }
      catch { return null; }
    }
    public static byte[] Get_MSTREAM_Data(this FS_Operations fsOp, IKGD_INODE entity, string source, string key, out string mimeType)
    {
      mimeType = "application/octet-stream";
      try
      {
        //IKGD_STREAM stream = entity.IKGD_MSTREAMs.FirstOrDefault(m => (source == null || m.IKGD_STREAM.source == source) && m.IKGD_STREAM.key == key).IKGD_STREAM;
        IKGD_STREAM stream = fsOp.DB.IKGD_STREAMs.FirstOrDefault(s => s.IKGD_MSTREAMs.Any(m => m.IKGD_INODE.version == entity.version) && (source == null || s.source == source) && s.key == key);
        mimeType = stream.type;
        return stream.data.ToArray();
      }
      catch { return null; }
    }


    //
    // estrazione e parsing degli streams
    //
    public static byte[] Get_STREAM_Data<TEntity>(this FS_Operations fsOp, TEntity entity, string key) where TEntity : class, IKGD_hasRNODE { return fsOp.Get_STREAM_Data(fsOp.Get_Active<IKGD_INODE>(entity.rnode), key); }
    public static byte[] Get_STREAM_Data(this FS_Operations fsOp, int rNodeCode, string key) { return fsOp.Get_STREAM_Data(fsOp.Get_Active<IKGD_INODE>(rNodeCode), key); }
    public static byte[] Get_STREAM_Data(this FS_Operations fsOp, IKGD_INODE entity, string key)
    {
      //try { return entity.IKGD_STREAMs.FirstOrDefault(s => s.key == key).data.ToArray(); }
      //catch { return null; }
      try { return fsOp.DB.IKGD_STREAMs.FirstOrDefault(s => s.inode == entity.version && s.key == key).data.ToArray(); }
      catch { return null; }
    }
    public static byte[] Get_STREAM_Data(this FS_Operations fsOp, IKGD_INODE entity, string key, out string mimeType)
    {
      mimeType = "application/octet-stream";
      try
      {
        IKGD_STREAM stream = fsOp.DB.IKGD_STREAMs.FirstOrDefault(s => s.inode == entity.version && s.key == key);
        if (stream != null)
        {
          mimeType = stream.type;
          return stream.data.ToArray();
        }
      }
      catch { }
      return null;
    }
    public static string Get_STREAM_String<TEntity>(this FS_Operations fsOp, TEntity entity, string key) where TEntity : class, IKGD_hasRNODE { return fsOp.Get_STREAM_String(fsOp.Get_Active<IKGD_INODE>(entity.rnode), key); }
    public static string Get_STREAM_String(this FS_Operations fsOp, int rNodeCode, string key) { return fsOp.Get_STREAM_String(fsOp.Get_Active<IKGD_INODE>(rNodeCode), key); }
    public static string Get_STREAM_String(this FS_Operations fsOp, IKGD_INODE entity, string key)
    {
      //
      // Encoding.Unicode (UTF-16) e' l'encoding per compatibilita' binaria su DB tra varbinary(MAX) e nvarchar(MAX)
      // viene utilizzato per IKGD_VDATA.data per mappare direttamente come stringa con cast DB, nel caso degli
      // streams utiliziamo UTF-8 che e' piu' compatto
      //
      //try { return Encoding.UTF8.GetString(fsOp.DB.IKGD_STREAMs.FirstOrDefault(s => s.inode == entity.version && s.key == key).data.ToArray()); }
      //catch { return null; }
      //return fsOp.DB.IKGD_STREAMs.FirstOrDefault(s => s.inode == entity.version && s.key == key).dataAsString;
      return fsOp.DB.IKGD_STREAMs.Where(s => s.inode == entity.version && s.key == key).Select(r => r.dataAsString).FirstOrDefault();
    }
    public static XElement Get_STREAM_Xml<TEntity>(this FS_Operations fsOp, TEntity entity, string key) where TEntity : class, IKGD_hasRNODE { return fsOp.Get_STREAM_Xml(fsOp.Get_Active<IKGD_INODE>(entity.rnode), key); }
    public static XElement Get_STREAM_Xml(this FS_Operations fsOp, int rNodeCode, string key) { return fsOp.Get_STREAM_Xml(fsOp.Get_Active<IKGD_INODE>(rNodeCode), key); }
    public static XElement Get_STREAM_Xml(this FS_Operations fsOp, IKGD_INODE entity, string key)
    {
      //
      // Encoding.Unicode (UTF-16) e' l'encoding per compatibilita' binaria su DB tra varbinary(MAX) e nvarchar(MAX)
      // viene utilizzato per IKGD_VDATA.data per mappare direttamente come stringa con cast DB, nel caso degli
      // streams utiliziamo UTF-8 che e' piu' compatto
      //
      //try { return XElement.Parse(Encoding.UTF8.GetString(fsOp.DB.IKGD_STREAMs.FirstOrDefault(s => s.inode == entity.version && s.key == key).data.ToArray())); }
      //catch { return null; }
      try { return XElement.Parse(fsOp.DB.IKGD_STREAMs.FirstOrDefault(s => s.inode == entity.version && s.key == key).dataAsString); }
      catch { return null; }
    }


    public static byte[] Get_STREAM_NoLinq(this FS_Operations fsOp, int id)
    {
      byte[] data = null;
      try
      {
        SqlCommand sqlCmd = null;
        //select top 1 * from ikgd_stream where inode=5169 and [key]='' order by id;
        sqlCmd = new SqlCommand("SELECT TOP 1 [data] FROM [IKGD_STREAM] WHERE ([id]=@id)", fsOp.DB.Connection as SqlConnection);
        sqlCmd.Parameters.Add("@id", SqlDbType.Int).Value = id;
        if (fsOp.DB.Connection.State == ConnectionState.Closed)
          fsOp.DB.Connection.Open();
        sqlCmd.CommandTimeout = Math.Max(sqlCmd.CommandTimeout, Utility.TryParse<int>(IKGD_Config.AppSettings["ProxyVFS_TimeoutDB"], 300));
        using (SqlDataReader reader = sqlCmd.ExecuteReader(CommandBehavior.SingleRow | CommandBehavior.SequentialAccess))
        {
          if (reader.Read())
          {
            data = (byte[])reader[0];
            reader.Close();
          }
        }
      }
      catch { }
      return data;
    }


    public static IQueryable<IKGD_VDATA_KEYVALUE> DeserializedVDATA(this FS_Operations fsOp)
    {
      Expression<Func<IKGD_VDATA_KEYVALUE, bool>> filter = PredicateBuilder.True<IKGD_VDATA_KEYVALUE>();
      filter = (fsOp.VersionFrozen == 0) ? filter.And(r => r.flag_published) : filter.And(r => r.flag_current);
      return fsOp.DB.IKGD_VDATA_KEYVALUEs.AsExpandable().Where(filter);
    }


    public static IQueryable<IKGD_VDATA_KEYVALUE> DeserializedVDATA(this FS_Operations fsOp, int rNode)
    {
      Expression<Func<IKGD_VDATA_KEYVALUE, bool>> filter = PredicateBuilder.True<IKGD_VDATA_KEYVALUE>();
      filter = filter.And(r => r.rNode == rNode);
      filter = (fsOp.VersionFrozen == 0) ? filter.And(r => r.flag_published) : filter.And(r => r.flag_current);
      return fsOp.DB.IKGD_VDATA_KEYVALUEs.AsExpandable().Where(filter);
    }


    public static IQueryable<IKGD_VDATA_KEYVALUE> DeserializedVDATA(this FS_Operations fsOp, int rNode, int? Level, string Key)
    {
      Expression<Func<IKGD_VDATA_KEYVALUE, bool>> filter = PredicateBuilder.True<IKGD_VDATA_KEYVALUE>();
      filter = filter.And(r => r.rNode == rNode);
      filter = (fsOp.VersionFrozen == 0) ? filter.And(r => r.flag_published) : filter.And(r => r.flag_current);
      if (Level != null)
        filter = filter.And(r => r.Level == Level.Value);
      filter = filter.And(r => string.Equals(r.Key, Key));
      return fsOp.DB.IKGD_VDATA_KEYVALUEs.AsExpandable().Where(filter);
    }


    public static IQueryable<IKGD_VDATA_KEYVALUE> DeserializedVDATA(this FS_Operations fsOp, int rNode, int? Level, string Key, string KeyParent)
    {
      Expression<Func<IKGD_VDATA_KEYVALUE, bool>> filter = PredicateBuilder.True<IKGD_VDATA_KEYVALUE>();
      filter = filter.And(r => r.rNode == rNode);
      filter = (fsOp.VersionFrozen == 0) ? filter.And(r => r.flag_published) : filter.And(r => r.flag_current);
      if (Level != null)
        filter = filter.And(r => r.Level == Level.Value);
      filter = filter.And(r => string.Equals(r.Key, Key) && string.Equals(r.KeyParent, KeyParent));
      return fsOp.DB.IKGD_VDATA_KEYVALUEs.AsExpandable().Where(filter);
    }


    public static IQueryable<IKGD_VDATA_KEYVALUE> DeserializedVDATA_WithLanguage(this FS_Operations fsOp)
    {
      Expression<Func<IKGD_VDATA_KEYVALUE, bool>> filter = PredicateBuilder.True<IKGD_VDATA_KEYVALUE>();
      filter = (fsOp.VersionFrozen == 0) ? filter.And(r => r.flag_published) : filter.And(r => r.flag_current);
      string KeyParent = IKGD_Language_Provider.Provider.LanguageNN;
      filter = filter.And(r => r.Level == 1 && string.Equals(r.KeyParent, KeyParent));
      return fsOp.DB.IKGD_VDATA_KEYVALUEs.AsExpandable().Where(filter);
    }


    public static IQueryable<IKGD_VDATA_KEYVALUE> DeserializedVDATA_WithLanguage(this FS_Operations fsOp, int rNode)
    {
      Expression<Func<IKGD_VDATA_KEYVALUE, bool>> filter = PredicateBuilder.True<IKGD_VDATA_KEYVALUE>();
      filter = filter.And(r => r.rNode == rNode);
      filter = (fsOp.VersionFrozen == 0) ? filter.And(r => r.flag_published) : filter.And(r => r.flag_current);
      string KeyParent = IKGD_Language_Provider.Provider.LanguageNN;
      filter = filter.And(r => r.Level == 1 && string.Equals(r.KeyParent, KeyParent));
      return fsOp.DB.IKGD_VDATA_KEYVALUEs.AsExpandable().Where(filter);
    }


    public static IQueryable<IKGD_VDATA_KEYVALUE> DeserializedVDATA_WithLanguage(this FS_Operations fsOp, int rNode, string Key)
    {
      Expression<Func<IKGD_VDATA_KEYVALUE, bool>> filter = PredicateBuilder.True<IKGD_VDATA_KEYVALUE>();
      filter = filter.And(r => r.rNode == rNode);
      filter = (fsOp.VersionFrozen == 0) ? filter.And(r => r.flag_published) : filter.And(r => r.flag_current);
      string KeyParent = IKGD_Language_Provider.Provider.LanguageNN;
      filter = filter.And(r => r.Level == 1 && string.Equals(r.Key, Key) && string.Equals(r.KeyParent, KeyParent));
      return fsOp.DB.IKGD_VDATA_KEYVALUEs.AsExpandable().Where(filter);
    }


    public static void UpdateVersionDate<TEntity>(this FS_Operations fsOp, TEntity entity) where TEntity : class, IKGD_XNODE
    {
      if (entity.version_date != fsOp.DateTimeContext)
        entity.version_date = fsOp.DateTimeContext;
    }


    public static void UpdateVersionDateOnChangeSet(this FS_Operations fsOp) { UpdateVersionDateOnChangeSet(fsOp, true, true); }
    public static void UpdateVersionDateOnChangeSet(this FS_Operations fsOp, bool processUpdates, bool processInserts)
    {
      var chgSet = fsOp.DB.GetChangeSet();
      if (chgSet != null)
      {
        if (processUpdates)
        {
          chgSet.Updates.OfType<IKGD_XNODE>().ForEach(n => UpdateVersionDate(fsOp, n));
        }
        if (processInserts)
        {
          chgSet.Inserts.OfType<IKGD_XNODE>().ForEach(n => UpdateVersionDate(fsOp, n));
        }
      }
    }


    //
    // COW wrappers
    //
    public static IKGD_XNODE_COW<TEntity> Factory_COW<TEntity>(this FS_Operations fsOp, TEntity entity) where TEntity : class, IKGD_XNODE, new() { return new IKGD_XNODE_COW<TEntity>(fsOp, entity); }
    //
    // controlla che non vengano generati duplicati attivi al salvataggio in caso di incasinamenti in operazioni precedenti
    //
    public static int EnsureNoDuplicateActive<TEntity>(this FS_Operations fsOp, IKGD_XNODE_COW<TEntity> cow_wrapper) where TEntity : class, IKGD_XNODE, new()
    {
      int renormalized = 0;
      if (cow_wrapper == null)
        return renormalized;
      try
      {
        if (!cow_wrapper.IgnoreChanges)
          cow_wrapper.StopListen();
        IQueryable<TEntity> dbNodes;
        if (typeof(TEntity) == typeof(IKGD_VNODE))
          dbNodes = fsOp.NodesActive<TEntity>().Where(n => (n as IKGD_VNODE).snode == (cow_wrapper.NodeOrig as IKGD_VNODE).snode);
        else
          dbNodes = fsOp.NodesActive<TEntity>().Where(n => n.rnode == cow_wrapper.NodeOrig.rnode);
        if (dbNodes == null)
          return renormalized;
        foreach (TEntity node in dbNodes)
        {
          if (node != cow_wrapper.NodeOrig && node != cow_wrapper.Node)
          {
            node.flag_current = false;
            renormalized++;
          }
        }
      }
      catch { }
      return renormalized;
    }
    //
    // restituisce la lista dei possibili folders in cui e' presente il file, se e' gia' un folder torna se stesso
    //
    public static IEnumerable<IKGD_VNODE> Get_FoldersCurrent(this FS_Operations fsOp, IKGD_VNODE vNode)
    {
      if (vNode == null)
        yield break;
      if (vNode.flag_folder)
        yield return vNode;
      else
        foreach (IKGD_VNODE n in fsOp.NodesActive<IKGD_VNODE>().Where(n => n.flag_folder && n.folder == vNode.folder))
          yield return n;
    }
    //
    // restituisce il folder del vNode, nel caso si tratti gia' di un folder ritorna il nodo stesso
    //
    public static IKGD_VNODE Get_FolderCurrentFallBack(this FS_Operations fsOp, IKGD_VNODE vNode)
    {
      if (vNode != null)
        return vNode.flag_folder ? vNode : fsOp.NodesActive<IKGD_VNODE>().FirstOrDefault(n => n.flag_folder && n.folder == vNode.folder);
      return null;
    }
    //
    // lettura dei contenuti di un folder
    // vNode puo' essere sia un folder che un file, nel secondo caso il folder viene estratto automaticamente
    // nel caso vNode sia un file e' come se stessi leggendo i siblings, pero' non e' detto che ottenga
    // la lista corretta dei subfolders, in quanto dipendono dal primo dei dei folder trovati associati al file
    // la lista dei subfolders e' corretta solo se vNode e' gia' un folder in partenza
    //
    public static IQueryable<IKGD_VNODE> Get_FolderContents(this FS_Operations fsOp, IKGD_VNODE vNode) { return fsOp.Get_FolderContents(vNode, true, true); }
    public static IQueryable<IKGD_VNODE> Get_FolderContents(this FS_Operations fsOp, IKGD_VNODE vNode, bool files, bool folders)
    {
      IKGD_VNODE vFolder = fsOp.Get_FolderCurrentFallBack(vNode);
      if (vFolder != null)
      {
        Expression<Func<IKGD_VNODE, bool>> filter = PredicateBuilder.False<IKGD_VNODE>();
        if (files)
          filter = filter.Or(n => !n.flag_folder && n.folder == vFolder.folder);
        if (folders)
          filter = filter.Or(n => n.flag_folder && n.parent == vFolder.folder);
        return fsOp.NodesActive<IKGD_VNODE>().Where(filter);
      }
      return null;
    }
    public static IQueryable<IKGD_VNODE> Get_FolderContentsOrdered(this FS_Operations fsOp, IKGD_VNODE vNode) { return fsOp.Get_FolderContentsOrdered(vNode, true, true); }
    public static IQueryable<IKGD_VNODE> Get_FolderContentsOrdered(this FS_Operations fsOp, IKGD_VNODE vNode, bool files, bool folders)
    {
      return fsOp.Get_FolderContents(vNode, files, folders).OrderBy(n => n.position).ThenBy(n => n.name.ToLower());
    }

    public static IEnumerable<FS_FileInfo> Get_FolderContentsInfoExt(this FS_Operations fsOp, int sNodeCode, Expression<Func<FS_Operations.FS_NodeInfo, bool>> fsNodeFilter, bool selectFiles, bool selectFolders, bool ordered, bool fetchINODEs, bool fullAccess)
    {
      IKGD_VNODE vNodeStart = fsOp.NodeActive(sNodeCode, fullAccess);
      IKGD_VNODE vFolder = fsOp.Get_FolderCurrentFallBack(vNodeStart);
      IQueryable<FS_Operations.FS_NodeInfo> resources = fsOp.Get_FolderContentsInfo(vFolder, fsNodeFilter, selectFiles, selectFolders, ordered, fetchINODEs, fullAccess);
      if (resources != null)
        foreach (FS_Operations.FS_NodeInfo fsInfo in resources)
          yield return new FS_FileInfo(vFolder, fsInfo.vNode, fsInfo.iNode, fsInfo.vData, null, null, true);
    }

    public static IEnumerable<FS_FileInfo> Get_FolderContentsInfoExt(this FS_Operations fsOp, int sNodeCode, Expression<Func<IKGD_VNODE, bool>> vNodeFilter, Expression<Func<IKGD_VDATA, bool>> vDataFilter, bool selectFiles, bool selectFolders, bool ordered, bool fetchINODEs, bool fullAccess)
    {
      IKGD_VNODE vNodeStart = fsOp.NodeActive(sNodeCode, fullAccess);
      IKGD_VNODE vFolder = fsOp.Get_FolderCurrentFallBack(vNodeStart);
      IQueryable<FS_Operations.FS_NodeInfo> resources = fsOp.Get_FolderContentsInfo(vFolder, vNodeFilter, vDataFilter, selectFiles, selectFolders, ordered, fetchINODEs, fullAccess);
      if (resources != null)
        foreach (FS_Operations.FS_NodeInfo fsInfo in resources)
          yield return new FS_FileInfo(vFolder, fsInfo.vNode, fsInfo.iNode, fsInfo.vData, null, null, true);
    }

    //
    // questa versione ha anche il conteggio dei symlinks e viene usata solo dal visualizzatore delle lista files del backend
    //
    public static IEnumerable<FS_FileInfo> Get_FolderContentsInfoExtListView(this FS_Operations fsOp, int sNodeCode, Expression<Func<IKGD_VNODE, bool>> vNodeFilter, Expression<Func<IKGD_VDATA, bool>> vDataFilter, bool selectFiles, bool selectFolders, bool ordered, bool fetchINODEs, bool fullAccess, string language)
    {
      IKGD_VNODE vNodeStart = fsOp.NodeActive(sNodeCode, fullAccess);
      IKGD_VNODE vFolder = fsOp.Get_FolderCurrentFallBack(vNodeStart);
      IQueryable<FS_Operations.FS_NodeInfo2> resources = fsOp.Get_FolderContentsInfoT<FS_Operations.FS_NodeInfo2>(vFolder, vNodeFilter, vDataFilter, selectFiles, selectFolders, ordered, fetchINODEs, fullAccess, language);
      if (resources != null)
        foreach (FS_Operations.FS_NodeInfo2 fsInfo in resources)
          yield return new FS_FileInfo(vFolder, fsInfo.vNode, fsInfo.iNode, fsInfo.vData, null, null, true) { ChildCount = fsInfo.ChildCount, HasTaintedDependancies = fsInfo.HasTaintedDependancies };
    }

    public static IQueryable<FS_Operations.FS_NodeInfo> Get_FolderContentsInfo(this FS_Operations fsOp, int sNodeCode, Expression<Func<FS_Operations.FS_NodeInfo, bool>> fsNodeFilter, bool selectFiles, bool selectFolders, bool ordered, bool fetchINODEs, bool fullAccess)
    {
      IKGD_VNODE vNodeStart = fsOp.NodeActive(sNodeCode, fullAccess);
      IKGD_VNODE vFolder = fsOp.Get_FolderCurrentFallBack(vNodeStart);
      return fsOp.Get_FolderContentsInfo(vFolder, fsNodeFilter, selectFiles, selectFolders, ordered, fetchINODEs, fullAccess);
    }
    public static IQueryable<FS_Operations.FS_NodeInfo> Get_FolderContentsInfo(this FS_Operations fsOp, IKGD_VNODE vFolder, Expression<Func<FS_Operations.FS_NodeInfo, bool>> fsNodeFilter, bool selectFiles, bool selectFolders, bool ordered, bool fetchINODEs, bool fullAccess)
    {
      IQueryable<FS_Operations.FS_NodeInfo> resources = fsOp.Get_FolderContentsInfo(vFolder, null, null, selectFiles, selectFolders, ordered, fetchINODEs, fullAccess);
      if (fsNodeFilter != null)
        resources = resources.Where(fsNodeFilter);
      return resources;
    }

    public static IQueryable<FS_Operations.FS_NodeInfo> Get_FolderContentsInfo(this FS_Operations fsOp, IKGD_VNODE vFolder, Expression<Func<IKGD_VNODE, bool>> vNodeFilter, Expression<Func<IKGD_VDATA, bool>> vDataFilter, bool selectFiles, bool selectFolders, bool ordered, bool fetchINODEs, bool fullAccess)
    {
      IQueryable<FS_Operations.FS_NodeInfo> resources = null;
      try
      {
        int folder_rNode = (vFolder != null) ? vFolder.rnode : 0;
        int folder_folder = (vFolder != null) ? vFolder.folder : 0;
        //
        Expression<Func<IKGD_VNODE, bool>> vNodeFilterAll = PredicateBuilder.False<IKGD_VNODE>();
        if (selectFiles)
          vNodeFilterAll = vNodeFilterAll.Or(n => !n.flag_folder && n.folder == folder_rNode);
        if (selectFolders)
          vNodeFilterAll = vNodeFilterAll.Or(n => n.flag_folder && n.parent == folder_folder);
        //
        Expression<Func<IKGD_VDATA, bool>> vDataFilterAll = fsOp.Get_vDataFilterACLv2(fullAccess);
        //
        if (vNodeFilter != null)
          vNodeFilterAll = vNodeFilterAll.And(vNodeFilter.Expand());
        if (vDataFilter != null)
          vDataFilterAll = vDataFilterAll.And(vDataFilter.Expand());
        //
        if (fetchINODEs)
        {
          //
          // NB uso DefaultIfEmpty() per ottenre un left outer join su iNode in modo da ottenere anche
          // i nodi senza un inode definito
          //
          resources =
            from vNode in fsOp.NodesActive<IKGD_VNODE>(fullAccess).Where(vNodeFilterAll)
            from vData in fsOp.NodesActive<IKGD_VDATA>(fullAccess).Where(vDataFilterAll).Where(n => n.rnode == vNode.rnode)
            from iNode in fsOp.NodesActive<IKGD_INODE>(fullAccess).Where(n => n.rnode == vNode.rnode).DefaultIfEmpty()
            select new FS_Operations.FS_NodeInfo { vNode = vNode, vData = vData, iNode = iNode };
        }
        else
        {
          resources =
            from vNode in fsOp.NodesActive<IKGD_VNODE>(fullAccess).Where(vNodeFilterAll)
            from vData in fsOp.NodesActive<IKGD_VDATA>(fullAccess).Where(vDataFilterAll).Where(n => n.rnode == vNode.rnode)
            select new FS_Operations.FS_NodeInfo { vNode = vNode, vData = vData };
        }
        if (ordered)
          resources = resources.OrderBy(n => n.vNode.position).ThenBy(n => n.vNode.name);
      }
      catch { }
      return resources;
    }

    //
    // questa versione ha anche il conteggio dei symlinks e viene usata solo dal visualizzatore delle liste files del backend
    //
    public static IQueryable<T> Get_FolderContentsInfoT<T>(this FS_Operations fsOp, IKGD_VNODE vFolder, Expression<Func<T, bool>> fsNodeFilter, bool selectFiles, bool selectFolders, bool ordered, bool fetchINODEs, bool fullAccess, string language) where T : class, FS_Operations.FS_NodeInfo_Interface, new()
    {
      IQueryable<T> resources = fsOp.Get_FolderContentsInfoT<T>(vFolder, null, null, selectFiles, selectFolders, ordered, fetchINODEs, fullAccess, language);
      if (fsNodeFilter != null)
        resources = resources.Where(fsNodeFilter);
      return resources;
    }
    public static IQueryable<T> Get_FolderContentsInfoT<T>(this FS_Operations fsOp, IKGD_VNODE vFolder, Expression<Func<IKGD_VNODE, bool>> vNodeFilter, Expression<Func<IKGD_VDATA, bool>> vDataFilter, bool selectFiles, bool selectFolders, bool ordered, bool fetchINODEs, bool fullAccess, string language) where T : class, FS_Operations.FS_NodeInfo_Interface, new()
    {
      IQueryable<T> resources = null;
      try
      {
        int folder_rNode = (vFolder != null) ? vFolder.rnode : 0;
        int folder_folder = (vFolder != null) ? vFolder.folder : 0;
        //
        Expression<Func<IKGD_VNODE, bool>> vNodeFilterAll = fsOp.Get_vNodeFilterACLv2(language);
        Expression<Func<IKGD_VDATA, bool>> vDataFilterAll = fsOp.Get_vDataFilterACLv2(fullAccess, true);
        //
        if (selectFiles && !selectFolders)
          vNodeFilterAll = vNodeFilterAll.And(n => !n.flag_folder && n.folder == folder_rNode);
        else if (selectFolders && !selectFiles)
          vNodeFilterAll = vNodeFilterAll.And(n => n.flag_folder && n.parent == folder_folder);
        else if (selectFiles && selectFolders)
          vNodeFilterAll = vNodeFilterAll.And(n => (!n.flag_folder && n.folder == folder_rNode) || (n.flag_folder && n.parent == folder_folder));
        //
        if (vNodeFilter != null)
          vNodeFilterAll = vNodeFilterAll.And(vNodeFilter.Expand());
        if (vDataFilter != null)
          vDataFilterAll = vDataFilterAll.And(vDataFilter.Expand());
        //
        if (typeof(T).IsAssignableFrom(typeof(FS_Operations.FS_NodeInfo2)))
        {
          //resources =
          //  from vNode in fsOp.NodesActive<IKGD_VNODE>(fullAccess).Where(vNodeFilterAll)
          //  from vData in fsOp.NodesActive<IKGD_VDATA>(fullAccess).Where(vDataFilterAll).Where(n => n.rnode == vNode.rnode)
          //  from iNode in fsOp.NodesActive<IKGD_INODE>(fullAccess).Where(n => n.rnode == vNode.rnode).DefaultIfEmpty()
          //  select new FS_Operations.FS_NodeInfo2 { vNode = vNode, vData = vData, iNode = iNode, ChildCount = fsOp.NodesActive<IKGD_VNODE>(fullAccess).Count(n => n.rnode == vNode.rnode) } as T;
          resources =
            from vNode in fsOp.NodesActive<IKGD_VNODE>(fullAccess).Where(vNodeFilterAll)
            from vData in fsOp.NodesActive<IKGD_VDATA>(fullAccess).Where(vDataFilterAll).Where(n => n.rnode == vNode.rnode)
            from iNode in fsOp.NodesActive<IKGD_INODE>(fullAccess).Where(n => n.rnode == vNode.rnode).DefaultIfEmpty()
            join lk in fsOp.NodesActive<IKGD_VNODE>(fullAccess) on vNode.rnode equals lk.rnode into links
            join prop in fsOp.NodesActive<IKGD_PROPERTY>(fullAccess) on vNode.rnode equals prop.rnode into props
            join rel in fsOp.NodesActive<IKGD_RELATION>(fullAccess) on vNode.rnode equals rel.rnode into rels
            select new FS_Operations.FS_NodeInfo2 { vNode = vNode, vData = vData, iNode = iNode, ChildCount = links.Count(), HasTaintedDependancies = (props.Any(p => !p.flag_published) || rels.Any(r => !r.flag_published)) } as T;
        }
        else
        {
          if (fetchINODEs)
          {
            //
            // NB uso DefaultIfEmpty() per ottenre un left outer join su iNode in modo da ottenere anche
            // i nodi senza un inode definito
            //
            resources =
              from vNode in fsOp.NodesActive<IKGD_VNODE>(fullAccess).Where(vNodeFilterAll)
              from vData in fsOp.NodesActive<IKGD_VDATA>(fullAccess).Where(vDataFilterAll).Where(n => n.rnode == vNode.rnode)
              from iNode in fsOp.NodesActive<IKGD_INODE>(fullAccess).Where(n => n.rnode == vNode.rnode).DefaultIfEmpty()
              select new T { vNode = vNode, vData = vData, iNode = iNode };
          }
          else
          {
            resources =
              from vNode in fsOp.NodesActive<IKGD_VNODE>(fullAccess).Where(vNodeFilterAll)
              from vData in fsOp.NodesActive<IKGD_VDATA>(fullAccess).Where(vDataFilterAll).Where(n => n.rnode == vNode.rnode)
              select new T { vNode = vNode, vData = vData };
          }
        }
        if (ordered)
          resources = resources.OrderBy(n => n.vNode.position).ThenBy(n => n.vNode.name);
      }
      catch { }
      return resources;
    }


    public static IQueryable<FS_Operations.FS_NodeInfo> Get_TreeContents(this FS_Operations fsOp, int sNodeRoot, List<int> subRoots, Expression<Func<IKGD_VNODE, bool>> vNodeFilter, Expression<Func<IKGD_VDATA, bool>> vDataFilter, bool? fetchINODEs, bool selectFolders, bool fullAccess)
    {
      IQueryable<FS_Operations.FS_NodeInfo> resources = null;
      try
      {
        Expression<Func<IKGD_VDATA, bool>> vDataFilterAll = fsOp.Get_vDataFilterACLv2(fullAccess);
        //
        Expression<Func<IKGD_VNODE, bool>> vNodeFilterAll = fsOp.Get_vNodeFilterACLv2();
        //
        IKGD_VNODE vNodeRoot = fsOp.NodeActive(sNodeRoot);
        //
        List<int> folders = new List<int>();
        if (subRoots != null && subRoots.Count > 0)
          folders.AddRange(subRoots);
        //
        // lettura degli snode di tutti i folder accessibili
        //
        if (folders.Count == 0)
        {
          for (List<IKGD_VNODE> lastF = new List<IKGD_VNODE> { vNodeRoot }; lastF != null && lastF.Count > 0; )
          {
            var foldersTmp = lastF.Select(n => n.folder).ToList();
            lastF.Clear();
            foreach (var folders_slice in foldersTmp.Slice(500))
            {
              var _lastF =
                (from vNode in fsOp.NodesActive<IKGD_VNODE>().Where(n => n.flag_folder)
                 from vData in fsOp.NodesActive<IKGD_VDATA>().Where(vDataFilterAll)
                 where vNode.rnode == vData.rnode && folders_slice.Contains(vNode.parent.Value)
                 select vNode).ToList();
              lastF.AddRange(_lastF);
              folders.AddRange(_lastF.Select(n => n.folder));
            }
          }
        }
        if (!folders.Contains(vNodeRoot.rnode))
          folders.Insert(0, vNodeRoot.rnode);
        //
        // scan di tutti i folder selezionati
        //
        if (selectFolders == false)
          vNodeFilterAll = vNodeFilterAll.And(n => !n.flag_folder);
        vNodeFilterAll = vNodeFilterAll.And(n => folders.Contains(n.folder));
        if (vNodeFilter != null)
          vNodeFilterAll = vNodeFilterAll.And(vNodeFilter.Expand());
        if (vDataFilter != null)
          vDataFilterAll = vDataFilterAll.And(vDataFilter.Expand());
        //
        if (fetchINODEs == null)
        {
          resources =
            from vNode in fsOp.NodesActive<IKGD_VNODE>().Where(vNodeFilterAll)
            from vData in fsOp.NodesActive<IKGD_VDATA>().Where(vDataFilterAll).Where(n => n.rnode == vNode.rnode)
            from iNode in fsOp.NodesActive<IKGD_INODE>().Where(n => n.rnode == vNode.rnode).DefaultIfEmpty()
            select new FS_Operations.FS_NodeInfo { vNode = vNode, vData = vData, iNode = iNode };
        }
        if (fetchINODEs.Value)
        {
          resources =
            from vNode in fsOp.NodesActive<IKGD_VNODE>().Where(vNodeFilterAll)
            from vData in fsOp.NodesActive<IKGD_VDATA>().Where(vDataFilterAll).Where(n => n.rnode == vNode.rnode)
            from iNode in fsOp.NodesActive<IKGD_INODE>().Where(n => n.rnode == vNode.rnode)
            select new FS_Operations.FS_NodeInfo { vNode = vNode, vData = vData, iNode = iNode };
        }
        else
        {
          resources =
            from vNode in fsOp.NodesActive<IKGD_VNODE>().Where(vNodeFilterAll)
            from vData in fsOp.NodesActive<IKGD_VDATA>().Where(vDataFilterAll).Where(n => n.rnode == vNode.rnode)
            select new FS_Operations.FS_NodeInfo { vNode = vNode, vData = vData };
        }
      }
      catch { }
      //
      return resources;
    }


    //
    // metodo per ottenere un tree generico con lo scan di tutti i subfolders di un rootset
    // con la possibilita' di ottenere anche l'indicazione del numero di risorse per ogni folder
    // e di ottimizzare il tree risultante per eliminare le foglie morte e le root fittizie senza risorse
    // il root node che viene ritornato e' fittizio (nel caso siano state specificate piu' root)
    // inoltre dispone di due comodi iteratori per lo scan ordinato del tree (attenzione al .Data null per la root!)
    //
    // versione con supporto completo di interfaces e automapper
    // e con la possibilita' di specificare tutti i tipi dettagliati di filtraggio
    //
    // T => FS_Operations.FS_NodeFolderInfo o FS_Operations.FS_NodeInfo
    //
    public static FS_Operations.FS_TreeNode<T> Get_TreeDataShortGeneric<T>(this FS_Operations fsOp, IEnumerable<int> rNodeRoots, IEnumerable<int> sNodeRoots, Expression<Func<IKGD_VNODE, bool>> vNodeFilterFilesCustom, Expression<Func<IKGD_VDATA, bool>> vDataFilterFilesCustom, int? maxRecursionLevel, bool removeVoidLeafs, bool compactFakeRoots) where T : FS_Operations.FS_NodeInfo_Interface
    {
      return Get_TreeDataShortGenericExt<T>(fsOp, fsOp.VersionFrozen, false, false, true, rNodeRoots, sNodeRoots, null, vNodeFilterFilesCustom, vDataFilterFilesCustom, maxRecursionLevel, removeVoidLeafs, compactFakeRoots, null);
    }
    public static FS_Operations.FS_TreeNode<T> Get_TreeDataShortGeneric<T>(this FS_Operations fsOp, IEnumerable<int> rNodeRoots, IEnumerable<int> sNodeRoots, Expression<Func<IKGD_VDATA, bool>> vDataFilterFoldersCustom, Expression<Func<IKGD_VNODE, bool>> vNodeFilterFilesCustom, Expression<Func<IKGD_VDATA, bool>> vDataFilterFilesCustom, int? maxRecursionLevel, bool removeVoidLeafs, bool compactFakeRoots) where T : FS_Operations.FS_NodeInfo_Interface
    {
      return Get_TreeDataShortGenericExt<T>(fsOp, fsOp.VersionFrozen, false, false, true, rNodeRoots, sNodeRoots, vDataFilterFoldersCustom, vNodeFilterFilesCustom, vDataFilterFilesCustom, maxRecursionLevel, removeVoidLeafs, compactFakeRoots, null);
    }
    public static FS_Operations.FS_TreeNode<T> Get_TreeDataShortGeneric<T>(this FS_Operations fsOp, IEnumerable<int> rNodeRoots, IEnumerable<int> sNodeRoots, Expression<Func<IKGD_VDATA, bool>> vDataFilterFoldersCustom, Expression<Func<IKGD_VNODE, bool>> vNodeFilterFilesCustom, Expression<Func<IKGD_VDATA, bool>> vDataFilterFilesCustom, int? maxRecursionLevel, bool removeVoidLeafs, bool compactFakeRoots, bool? noRecurseToLeafFolders) where T : FS_Operations.FS_NodeInfo_Interface
    {
      return Get_TreeDataShortGenericExt<T>(fsOp, fsOp.VersionFrozen, false, false, true, rNodeRoots, sNodeRoots, vDataFilterFoldersCustom, vNodeFilterFilesCustom, vDataFilterFilesCustom, maxRecursionLevel, removeVoidLeafs, compactFakeRoots, noRecurseToLeafFolders);
    }
    public static FS_Operations.FS_TreeNode<T> Get_TreeDataShortGenericExt<T>(this FS_Operations fsOp, int version_frozen, bool include_deleted, bool fullAccess, bool filterLanguage, IEnumerable<int> rNodeRoots, IEnumerable<int> sNodeRoots, Expression<Func<IKGD_VDATA, bool>> vDataFilterFoldersCustom, Expression<Func<IKGD_VNODE, bool>> vNodeFilterFilesCustom, Expression<Func<IKGD_VDATA, bool>> vDataFilterFilesCustom, int? maxRecursionLevel, bool removeVoidLeafs, bool compactFakeRoots, bool? noRecurseToLeafFolders) where T : FS_Operations.FS_NodeInfo_Interface
    {
      FS_Operations.FS_TreeNode<T> rootTree = new FS_Operations.FS_TreeNode<T>(null, default(T));
      if ((sNodeRoots == null || !sNodeRoots.Any()) && (rNodeRoots == null || !rNodeRoots.Any()))
        return rootTree;
      maxRecursionLevel = maxRecursionLevel ?? 10;  // per limitare la ricorsione
      try
      {
        Expression<Func<IKGD_VNODE, bool>> vNodeFilterFolders = fsOp.Get_vNodeFilterACLv2(filterLanguage);
        vNodeFilterFolders = vNodeFilterFolders.And(n => n.flag_folder);
        Expression<Func<IKGD_VDATA, bool>> vDataFilterFolders = fsOp.Get_vDataFilterACLv2(fullAccess, fullAccess);
        if (vDataFilterFoldersCustom != null)
        {
          vDataFilterFolders = vDataFilterFolders.And(vDataFilterFoldersCustom.Expand());
        }
        //
        Expression<Func<IKGD_VNODE, bool>> vNodeFilterFiles = fsOp.Get_vNodeFilterACLv2(filterLanguage);
        vNodeFilterFiles = vNodeFilterFiles.And(n => !n.flag_folder);
        if (vNodeFilterFilesCustom != null)
        {
          vNodeFilterFiles = vNodeFilterFiles.And(vNodeFilterFilesCustom.Expand());
        }
        Expression<Func<IKGD_VDATA, bool>> vDataFilterFiles = fsOp.Get_vDataFilterACLv2(fullAccess, fullAccess);
        if (vDataFilterFilesCustom != null)
        {
          vDataFilterFiles = vDataFilterFiles.And(vDataFilterFilesCustom.Expand());
        }
        //
        if (typeof(FS_Operations.FS_NodeFolderInfo).IsAssignableFrom(typeof(T)))
        {
          AutoMapperWrapper.AutoRegister<FS_Operations.FS_NodeFolderInfo, T>();
        }
        else if (typeof(FS_Operations.FS_NodeInfoExt).IsAssignableFrom(typeof(T)))
        {
          AutoMapperWrapper.AutoRegister<FS_Operations.FS_NodeInfoExt, T>();
        }
        else
        {
          AutoMapperWrapper.AutoRegister<FS_Operations.FS_NodeInfo, T>();
        }
        //
        List<FS_Operations.FS_TreeNode<T>> treeNodesSet = new List<FS_Operations.FS_TreeNode<T>>();
        //
        List<int> foldersToScan = fsOp.PathsFromNodesExt(sNodeRoots, rNodeRoots, true, fullAccess, filterLanguage, null, true).Select(p => p.rNode).Distinct().ToList();
        //
        for (int i = 0; i < maxRecursionLevel.Value && foldersToScan.Count > 0; i++)
        {
          // per il primo run uso le root specificate, poi passo alle subdirs
          Expression<Func<IKGD_VNODE, bool>> vNodeFilterLink = fsOp.Get_vNodeFilterACLv2(filterLanguage);
          //
          vNodeFilterLink = (treeNodesSet.Count == 0) ? vNodeFilterLink.And(n => foldersToScan.Contains(n.folder)) : vNodeFilterLink.And(n => foldersToScan.Contains(n.parent.Value));
          //
          List<T> newFolderSet = null;
          if (typeof(FS_Operations.FS_NodeFolderInfo).IsAssignableFrom(typeof(T)))
          {
            newFolderSet =
              (from vNode in fsOp.NodesActive<IKGD_VNODE>(version_frozen, include_deleted).Where(vNodeFilterFolders).Where(vNodeFilterLink)
               from vData in fsOp.NodesActive<IKGD_VDATA>(version_frozen, include_deleted).Where(vDataFilterFolders).Where(n => n.rnode == vNode.rnode)
               orderby vNode.position, vNode.name
               let contentsCount =
               (from vNodeX in fsOp.NodesActive<IKGD_VNODE>(version_frozen, include_deleted).Where(vNodeFilterFiles).Where(n => n.folder == vNode.folder)
                from vDataX in fsOp.NodesActive<IKGD_VDATA>(version_frozen, include_deleted).Where(vDataFilterFiles).Where(n => n.rnode == vNodeX.rnode)
                select vNodeX.snode).Count()
               select AutoMapper.Mapper.Map<FS_Operations.FS_NodeFolderInfo, T>(new FS_Operations.FS_NodeFolderInfo { vNode = vNode, vData = vData, FilesCount = contentsCount })).ToList();
          }
          else if (typeof(FS_Operations.FS_NodeInfoExt).IsAssignableFrom(typeof(T)))
          {
            if (noRecurseToLeafFolders.GetValueOrDefault(false) == false)
            {
              newFolderSet =
                (from vNode in fsOp.NodesActive<IKGD_VNODE>(version_frozen, include_deleted).Where(vNodeFilterFolders).Where(vNodeFilterLink)
                 from vData in fsOp.NodesActive<IKGD_VDATA>(version_frozen, include_deleted).Where(vDataFilterFolders).Where(n => n.rnode == vNode.rnode)
                 from iNode in fsOp.NodesActive<IKGD_INODE>(version_frozen, include_deleted).Where(n => n.rnode == vNode.rnode).DefaultIfEmpty()
                 join rel in fsOp.NodesActive<IKGD_RELATION>(version_frozen, include_deleted) on vNode.rnode equals rel.rnode into rels
                 join prp in fsOp.NodesActive<IKGD_PROPERTY>(version_frozen, include_deleted) on vNode.rnode equals prp.rnode into prps
                 orderby vNode.position, vNode.name
                 select AutoMapper.Mapper.Map<FS_Operations.FS_NodeInfoExt, T>(new FS_Operations.FS_NodeInfoExt { vNode = vNode, vData = vData, iNode = iNode, Relations = rels.ToList(), Properties = prps.ToList() })).ToList();
            }
            else
            {
              newFolderSet =
                (from vNode in fsOp.NodesActive<IKGD_VNODE>(version_frozen, include_deleted).Where(vNodeFilterFolders).Where(vNodeFilterLink)
                 from vData in fsOp.NodesActive<IKGD_VDATA>(version_frozen, include_deleted).Where(vDataFilterFolders).Where(n => n.rnode == vNode.rnode)
                 from iNode in fsOp.NodesActive<IKGD_INODE>(version_frozen, include_deleted).Where(n => n.rnode == vNode.rnode).DefaultIfEmpty()
                 join rel in fsOp.NodesActive<IKGD_RELATION>(version_frozen, include_deleted) on vNode.rnode equals rel.rnode into rels
                 join prp in fsOp.NodesActive<IKGD_PROPERTY>(version_frozen, include_deleted) on vNode.rnode equals prp.rnode into prps
                 where fsOp.NodesActive<IKGD_VNODE>(version_frozen, include_deleted).Any(n => n.flag_folder == true && n.parent.Value == vNode.rnode)
                 orderby vNode.position, vNode.name
                 select AutoMapper.Mapper.Map<FS_Operations.FS_NodeInfoExt, T>(new FS_Operations.FS_NodeInfoExt { vNode = vNode, vData = vData, iNode = iNode, Relations = rels.ToList(), Properties = prps.ToList() })).ToList();
            }
          }
          else
          {
            if (noRecurseToLeafFolders.GetValueOrDefault(false) == false)
            {
              newFolderSet =
                (from vNode in fsOp.NodesActive<IKGD_VNODE>(version_frozen, include_deleted).Where(vNodeFilterFolders).Where(vNodeFilterLink)
                 from vData in fsOp.NodesActive<IKGD_VDATA>(version_frozen, include_deleted).Where(vDataFilterFolders).Where(n => n.rnode == vNode.rnode)
                 orderby vNode.position, vNode.name
                 select AutoMapper.Mapper.Map<FS_Operations.FS_NodeInfo, T>(new FS_Operations.FS_NodeInfo { vNode = vNode, vData = vData })).ToList();
            }
            else
            {
              newFolderSet =
                (from vNode in fsOp.NodesActive<IKGD_VNODE>(version_frozen, include_deleted).Where(vNodeFilterFolders).Where(vNodeFilterLink)
                 from vData in fsOp.NodesActive<IKGD_VDATA>(version_frozen, include_deleted).Where(vDataFilterFolders).Where(n => n.rnode == vNode.rnode)
                 where fsOp.NodesActive<IKGD_VNODE>(version_frozen, include_deleted).Any(n => n.flag_folder == true && n.parent.Value == vNode.rnode)
                 orderby vNode.position, vNode.name
                 select AutoMapper.Mapper.Map<FS_Operations.FS_NodeInfo, T>(new FS_Operations.FS_NodeInfo { vNode = vNode, vData = vData })).ToList();
            }
          }
          if (newFolderSet.Count == 0)
            break;
          foldersToScan = newFolderSet.Select(n => n.vNode.folder).Except(treeNodesSet.Select(n => n.Data.vNode.folder)).ToList();
          newFolderSet.Where(n => foldersToScan.Contains(n.vNode.folder)).ForEach(n => treeNodesSet.Add(new FS_Operations.FS_TreeNode<T>(null, n)));
        }
        // join sul tree per creare correttamente la struttura ricorsiva
        treeNodesSet.Join(treeNodesSet, t1 => t1.Data.vNode.folder, t2 => t2.Data.vNode.parent.Value, (t1, t2) => new { node1 = t1, node2 = t2 }).ForEach(r => r.node2.Parent = r.node1);
        // attacco i nodi orfani alla root e rilascio le dipendenze
        treeNodesSet.Where(n => n.Parent == null).ForEach(r => r.Parent = rootTree);
        treeNodesSet.Clear();
        //
        // processi di ottimizzazione sul tree nel caso ci siano informazioni sui contenuti
        //
        if (typeof(FS_Operations.FS_NodeFolderInfo).IsAssignableFrom(typeof(T)))
        {
          // eliminazione dei rami secchi senza risorse
          if (removeVoidLeafs)
          {
            for (bool tainted = false; tainted == true; tainted = false)
            {
              foreach (var node in rootTree.RecurseOnTree.Where(r => r.Data != null && (r.Data as FS_Operations.FS_NodeFolderInfo).FilesCount == 0 && r.Nodes.Count == 0).ToList())
              {
                tainted = true;
                node.Parent = null;  // rimozione dal tree
              }
            }
          }
          // selezione ottimizzata della root quando non ci sono elementi nei nodi iniziali
          // in modo che non ci sia mai una root senza elementi selezionabili
          if (compactFakeRoots)
          {
            while (rootTree.Nodes.Count <= 1 && (rootTree.Nodes.FirstOrDefault().Data as FS_Operations.FS_NodeFolderInfo).FilesCount == 0)
            {
              var node_old = rootTree.Nodes.FirstOrDefault();
              node_old.Parent = null;
              node_old.Nodes.ForEach(n => n.Parent = rootTree);
            }
          }
        }
      }
      catch { }
      return rootTree;
    }


    public static List<string> RemovePendingVoidPublicationRequests(this FS_Operations fsOp, bool completeTransaction)
    {
      List<string> messages = new List<string>();
      try
      {
        int chunkSize = 500;
        int db_timeout = 3600;
        DateTime start = DateTime.Now;
        fsOp.EnsureOpenConnection();
        fsOp.DB.CommandTimeout = 3600;
        //
        using (System.Transactions.TransactionScope ts = IKGD_TransactionFactory.Transaction(db_timeout))
        {
          //
          // out of date IKGD_FREEZED and IKGD_SNAPSHOT removal
          //
          var version_frozen_all = fsOp.DB.IKGD_VNODEs.Select(n => n.version_frozen)
            .Union(fsOp.DB.IKGD_INODEs.Select(n => n.version_frozen))
            .Union(fsOp.DB.IKGD_VDATAs.Select(n => n.version_frozen))
            .Union(fsOp.DB.IKGD_PROPERTies.Select(n => n.version_frozen))
            .Union(fsOp.DB.IKGD_RELATIONs.Select(n => n.version_frozen))
            .Distinct().Where(v => v != null).Select(v => v.Value);
          var inactive_freezes = fsOp.DB.IKGD_FREEZEDs.Select(r => r.version_frozen).Except(version_frozen_all).ToList();
          foreach (var _inactive_versions in inactive_freezes.Slice(chunkSize))
          {
            var _inactive_versions_slice = _inactive_versions.ToList();
            fsOp.DB.IKGD_FREEZEDs.DeleteAllOnSubmit(fsOp.DB.IKGD_FREEZEDs.Where(r => _inactive_versions_slice.Contains(r.version_frozen)));
          }
          var chg01b = fsOp.DB.GetChangeSet();
          messages.Add("Publication snapshost IKGD_FREEZED items removed: {0} [{1} ms]".FormatString(chg01b.Deletes.Count, (DateTime.Now - start).TotalMilliseconds));
          fsOp.DB.SubmitChanges();
          //
          // rimozione dei freezes associati a nodi che sono gia' stati pubblicati
          fsOp.DB.IKGD_FREEZEDs.DeleteAllOnSubmit(fsOp.DB.IKGD_VNODEs.Where(r => r.flag_published && r.version_frozen != null).Join(fsOp.DB.IKGD_VNODEs, n1 => n1.snode, n2 => n2.snode, (n1, n2) => new { n1, n2 }).Where(r => r.n1.version_frozen >= r.n2.version_frozen).Select(r => r.n2.version).Distinct().Join(fsOp.DB.IKGD_FREEZEDs.Where(r => r.node_type == 1), v => v, f => f.node_version, (v, f) => f).Distinct());
          fsOp.DB.IKGD_FREEZEDs.DeleteAllOnSubmit(fsOp.DB.IKGD_VDATAs.Where(r => r.flag_published && r.version_frozen != null).Join(fsOp.DB.IKGD_VDATAs, n1 => n1.rnode, n2 => n2.rnode, (n1, n2) => new { n1, n2 }).Where(r => r.n1.version_frozen >= r.n2.version_frozen).Select(r => r.n2.version).Distinct().Join(fsOp.DB.IKGD_FREEZEDs.Where(r => r.node_type == 2), v => v, f => f.node_version, (v, f) => f).Distinct());
          fsOp.DB.IKGD_FREEZEDs.DeleteAllOnSubmit(fsOp.DB.IKGD_INODEs.Where(r => r.flag_published && r.version_frozen != null).Join(fsOp.DB.IKGD_INODEs, n1 => n1.rnode, n2 => n2.rnode, (n1, n2) => new { n1, n2 }).Where(r => r.n1.version_frozen >= r.n2.version_frozen).Select(r => r.n2.version).Distinct().Join(fsOp.DB.IKGD_FREEZEDs.Where(r => r.node_type == 3), v => v, f => f.node_version, (v, f) => f).Distinct());
          fsOp.DB.IKGD_FREEZEDs.DeleteAllOnSubmit(fsOp.DB.IKGD_PROPERTies.Where(r => r.flag_published && r.version_frozen != null).Join(fsOp.DB.IKGD_PROPERTies, n1 => n1.rnode, n2 => n2.rnode, (n1, n2) => new { n1, n2 }).Where(r => r.n1.version_frozen >= r.n2.version_frozen).Select(r => r.n2.version).Distinct().Join(fsOp.DB.IKGD_FREEZEDs.Where(r => r.node_type == 4), v => v, f => f.node_version, (v, f) => f).Distinct());
          fsOp.DB.IKGD_FREEZEDs.DeleteAllOnSubmit(fsOp.DB.IKGD_RELATIONs.Where(r => r.flag_published && r.version_frozen != null).Join(fsOp.DB.IKGD_RELATIONs, n1 => n1.rnode, n2 => n2.rnode, (n1, n2) => new { n1, n2 }).Where(r => r.n1.version_frozen >= r.n2.version_frozen).Select(r => r.n2.version).Distinct().Join(fsOp.DB.IKGD_FREEZEDs.Where(r => r.node_type == 5), v => v, f => f.node_version, (v, f) => f).Distinct());
          fsOp.DB.IKGD_FREEZEDs.DeleteAllOnSubmit(fsOp.DB.IKGD_VNODEs.Where(r => r.flag_published && r.version_frozen != null).Join(fsOp.DB.IKGD_VNODEs, n1 => n1.snode, n2 => n2.snode, (n1, n2) => new { n1, n2 }).Where(r => r.n1.version_frozen >= r.n2.version_frozen).Select(r => r.n2.version).Distinct().Join(fsOp.DB.IKGD_FREEZEDs.Where(r => r.node_type == 6), v => v, f => f.node_version, (v, f) => f).Distinct());
          var chg01c = fsOp.DB.GetChangeSet();
          messages.Add("Publication snapshost IKGD_FREEZED items removed as already published: {0} [{1} ms]".FormatString(chg01c.Deletes.Count, (DateTime.Now - start).TotalMilliseconds));
          fsOp.DB.SubmitChanges();
          //
          // rimozione dei freezes senza mapping su risorse da pubblicare
          var freeze_set_active1 = fsOp.DB.IKGD_FREEZEDs.Where(r => r.node_type == 1).Join(fsOp.DB.IKGD_VNODEs, r => r.version_frozen, r => r.version_frozen, (f, n) => f);
          var freeze_set_active2 = fsOp.DB.IKGD_FREEZEDs.Where(r => r.node_type == 2).Join(fsOp.DB.IKGD_VDATAs, r => r.version_frozen, r => r.version_frozen, (f, n) => f);
          var freeze_set_active3 = fsOp.DB.IKGD_FREEZEDs.Where(r => r.node_type == 3).Join(fsOp.DB.IKGD_INODEs, r => r.version_frozen, r => r.version_frozen, (f, n) => f);
          var freeze_set_active4 = fsOp.DB.IKGD_FREEZEDs.Where(r => r.node_type == 4).Join(fsOp.DB.IKGD_PROPERTies, r => r.version_frozen, r => r.version_frozen, (f, n) => f);
          var freeze_set_active5 = fsOp.DB.IKGD_FREEZEDs.Where(r => r.node_type == 5).Join(fsOp.DB.IKGD_RELATIONs, r => r.version_frozen, r => r.version_frozen, (f, n) => f);
          var freeze_set_active6 = fsOp.DB.IKGD_FREEZEDs.Where(r => r.node_type == 6).Join(fsOp.DB.IKGD_VNODEs, r => r.version_frozen, r => r.version_frozen, (f, n) => f);
          var freeze_set_active = freeze_set_active1.Union(freeze_set_active2).Union(freeze_set_active3).Union(freeze_set_active4).Union(freeze_set_active5).Union(freeze_set_active6);
          var freeze_set_inactive = fsOp.DB.IKGD_FREEZEDs.Except(freeze_set_active);
          //
          fsOp.DB.IKGD_FREEZEDs.DeleteAllOnSubmit(freeze_set_inactive);
          var chg01d = fsOp.DB.GetChangeSet();
          messages.Add("Publication snapshost IKGD_FREEZED items unmapped removed: {0} [{1} ms]".FormatString(chg01d.Deletes.Count, (DateTime.Now - start).TotalMilliseconds));
          fsOp.DB.SubmitChanges();
          //
          var version_frozen_active = fsOp.DB.IKGD_FREEZEDs.Select(r => r.version_frozen);
          var snapshots_to_remove = fsOp.DB.IKGD_SNAPSHOTs.Where(r => r.flag_published == false || r.date_published == null).Select(r => r.version_frozen).Except(version_frozen_active).ToList();
          foreach (var _inactive_versions in snapshots_to_remove.Slice(chunkSize))
          {
            var _inactive_versions_slice = _inactive_versions.ToList();
            fsOp.DB.IKGD_SNAPSHOTs.DeleteAllOnSubmit(fsOp.DB.IKGD_SNAPSHOTs.Where(r => _inactive_versions_slice.Contains(r.version_frozen)));
          }
          var chg01e = fsOp.DB.GetChangeSet();
          messages.Add("Publication snapshost IKGD_SNAPSHOT items removed: {0} [{1} ms]".FormatString(chg01e.Deletes.Count, (DateTime.Now - start).TotalMilliseconds));
          fsOp.DB.SubmitChanges();
          //
          var snapshots_to_update = fsOp.DB.IKGD_SNAPSHOTs.Where(r => r.flag_published == false || r.date_published == null).Where(r => r.affected != fsOp.DB.IKGD_FREEZEDs.Count(f => f.version_frozen == r.version_frozen)).Select(r => new { snapshot = r, num = fsOp.DB.IKGD_FREEZEDs.Count(f => f.version_frozen == r.version_frozen) }).ToList();
          snapshots_to_update.ForEach(r => r.snapshot.affected = r.num);
          var chg01f = fsOp.DB.GetChangeSet();
          messages.Add("Publication snapshost IKGD_SNAPSHOT items count updated: {0} [{1} ms]".FormatString(chg01f.Updates.Count, (DateTime.Now - start).TotalMilliseconds));
          fsOp.DB.SubmitChanges();
          //
          if (completeTransaction)
            ts.Committ();
        }
      }
      catch (Exception ex)
      {
        messages.Add("Exception: {0}".FormatString(ex.Message));
      }
      return messages;
    }


    //
    // data una lista di nodi IKGD_VDATA procede con l'aggiornamento dei dati presenti in IKGD_VDATA.setting
    // TODO:
    // aggiungere il supporto per le varie operazioni supportate da FS_Lib
    // COW_UpdateResource (ha gia' il codice incluso ma ancora da testare)
    // bisogna inserirlo per tutte le operazioni COW
    // fare anche una verifica dei punti dove viene creato/duplicato un VDATA (factory,Utility.clone,fsop.clone)
    //
    public static void Update_vDataKeyValues(this FS_Operations fsOp, IKGD_VDATA node) { Update_vDataKeyValues(fsOp, Enumerable.Repeat(node, 1)); }
    public static int Update_vDataKeyValues(this FS_Operations fsOp, IEnumerable<IKGD_VDATA> nodes)
    {
      //
      // TODO:
      // processare il tutto in transaction
      // processare il tutto in batch da 50-100 record con transaction separate per ciascun batch
      // generare codice SQL da eseguire direttamente e senza LINQ, possibilmente con un operazione di bulk insert
      // per ciascuna transaction:
      //  - cancellare eventuali mapping gia' presenti
      //  - processare ciascun item generando tutti i record nel contesto di un'operazione di bulk insert
      //  - committ
      //

      return 0;
    }



    /// <summary>
    /// Detaches an entity from its existing data context.
    /// </summary>
    /// <typeparam name="T">The entity type.</typeparam>
    /// <param name="entity">The entity instance.
    /// <returns>A copy of the entity in a detached state.</returns>
    public static T Detach<T>(this DataContext context, T entity) where T : class, new()
    {
      if (entity == null)
      {
        return null;
      }
      //
      //create a copy of the entity
      object Copy = new T();
      //
      //enumerate the data member mappings for the entity type
      foreach (MetaDataMember member in context.Mapping.GetMetaType(typeof(T)).DataMembers)
      {
        if (member.IsAssociation || member.IsDeferred)  //skip associations and deferred members
        {
          continue;
        }
        //
        //copy the member value
        member.StorageAccessor.SetBoxedValue(ref Copy, member.StorageAccessor.GetBoxedValue(entity));
      }
      //
      return (T)Copy;
    }


  }

}

