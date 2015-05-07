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
using System.Web.UI;
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
using LinqKit;

using Ikon;
using Ikon.GD;
using Ikon.Log;


namespace Ikon.GD
{


  public static class IKGD_Config
  {
    private static object _lock = new object();
    private static object _lockL1 = new object();
    private static object _lockL2 = new object();
    private static string keyNameAll = "cacheKey_IKGD_Config_All";
    private static string keyNameAppSettings = "cacheKey_IKGD_Config_AppSettings";
    private static string keyNameConfigAuthor = "cacheKey_IKGD_Config_ConfigAuthor";
    private static string dumpPath { get { return IKGD_Config.GetAuthorConfigFile("ConfigDB.{0}.xml"); } }
    //
    public static string IdConfig { get; private set; }
    //


    static IKGD_Config()
    {
      ApplicationName = AppSettingsWeb["IKGD_Application"] ?? string.Empty;  // e' una primary key e non deve essere null
      // pulizia generale
      IKGD_Config.Clear();
      IKGD_KeyStorage.Clear();
    }

    public static string ApplicationFullName { get { return ApplicationName + ((ApplicationInstanceName != null) ? "_" + ApplicationInstanceName : ""); } }

    //
    // InstanceName non e' modificabile e viene letta esclusivamente da webconfig
    //
    public static string ApplicationInstanceName { get { return string.IsNullOrEmpty(AppSettingsWeb["IKGD_Instance"]) ? null : AppSettingsWeb["IKGD_Instance"]; } }

    //
    // ApplicationName e' modificabile per il supporto di un editor unico multisito
    //
    //private static string key_application = "IKGD_ApplicationName";
    //public static string ApplicationName
    //{
    //  get { lock (_lockL1) { try { return (string)HttpContext.Current.Application[key_application]; } catch { return null; } } }
    //  set
    //  {
    //    lock (_lockL1)
    //    {
    //      try
    //      {
    //        if ((string)HttpContext.Current.Application[key_application] == value)
    //          return;
    //        // eseguo prima il clear nel caso IKGD_KeyStorage e IKGD_Config dipendano da application
    //        IKGD_KeyStorage.Clear();
    //        IKGD_Config.Clear();
    //        HttpContext.Current.Application[key_application] = value;
    //      }
    //      catch { }
    //    }
    //  }
    //}
    private static string _ApplicationName;
    public static string ApplicationName
    {
      get { lock (_lockL1) { return _ApplicationName; } }
      set
      {
        lock (_lockL1)
        {
          try
          {
            if (_ApplicationName == value)
              return;
            // eseguo prima il clear nel caso IKGD_KeyStorage e IKGD_Config dipendano da application
            IKGD_KeyStorage.Clear();
            IKGD_Config.Clear();
            _ApplicationName = value;
          }
          catch { }
        }
      }
    }


    public static T GetElement<T>(string elementType, string key)
    {
      return Utility.TryParse<T>(GetElement(elementType, key, true).Value);
    }

    public static Element GetElement(string elementType, string key, bool noNull)
    {
      Element element = Elements.Where(e => e.ElementType == elementType).Where(e => e.Key == key).FirstOrDefault();
      return noNull ? (element ?? new Element()) : element;
    }

    public static IEnumerable<Element> GetElements(string elementType, string key)
    {
      IEnumerable<Element> elements = Elements;
      if (elementType != null)
        elements = elements.Where(e => e.ElementType == elementType);
      if (key != null)
        elements = elements.Where(e => e.Key == key);
      return elements;
    }

    public static IEnumerable<T> GetElements<T>(string elementType, string key)
    {
      return GetElements(elementType, key).Select(e => Utility.TryParse<T>(e.Value));
    }

    public static IEnumerable<T> GetElements<T>(string elementType, string key, T defaultValue)
    {
      return GetElements(elementType, key).Select(e => Utility.TryParse<T>(e.Value, defaultValue));
    }


    public static void Clear()
    {
      lock (_lock)
      {
        IdConfig = Guid.NewGuid().ToString();
        try { HttpRuntime.Cache.Remove(keyNameAll); }
        catch { }
        try { HttpRuntime.Cache.Remove(keyNameAppSettings); }
        catch { }
        try { HttpRuntime.Cache.Remove(keyNameConfigAuthor); }
        catch { }
        //IKGD_ConfigVFS.Clear();
      }
    }


    public static List<Element> Elements
    {
      get
      {
        lock (_lock)
        {
          List<Element> elements = (List<Element>)HttpRuntime.Cache[keyNameAll];
          try
          {
            if (elements == null)
            {
              try { HttpRuntime.Cache.Remove(keyNameAppSettings); }
              catch { }
              try
              {
                if (ApplicationName.IsNotEmpty())
                {
                  using (Ikon.Config.DataContext DB = Ikon.Config.DataContext.Factory())
                  {
                    elements = DB.IKGD_CONFIGs.Where(r => r.application == ApplicationName && (r.instance == null || r.instance == ApplicationInstanceName)).Where(r => r.flag_active).OrderBy(r => r.instance).ThenBy(r => r.type).ThenBy(r => r.key).ThenBy(r => r.id).Select(r => new Element { Id = r.id, ElementType = r.type, Key = r.key, Value = r.value, Description = r.description }).ToList();
                  }
                }
                else
                {
                  elements = new List<Element>();
                }
              }
              catch { }
              if (elements != null)
              {
                AggregateCacheDependency sqlDeps = new AggregateCacheDependency();
                sqlDeps.Add(new SqlCacheDependency("GDCS", "IKGD_CONFIG"));
                HttpRuntime.Cache.Insert(keyNameAll, elements, sqlDeps, Cache.NoAbsoluteExpiration, Cache.NoSlidingExpiration, CacheItemPriority.High, (key, value, reason) => { (value as List<Element>).Clear(); });
              }
              else
              {
                elements = new List<Element>();
                HttpRuntime.Cache.Insert(keyNameAll, elements, null, DateTime.Now.AddSeconds(3600), Cache.NoSlidingExpiration, CacheItemPriority.High, (key, value, reason) => { (value as List<Element>).Clear(); });
              }
            }
          }
          catch { }
          return elements;
        }
      }
    }


    //
    // property per convertire l'accesso ai settings con la stessa API di ConfigurationManager
    // usage:
    // IKGD_Config.AppSettings[key] --> Ikon.GD.IKGD_Config.IKGD_Config.AppSettings[key]
    // WebIKGD_Config.AppSettings[key] --> Ikon.GD.IKGD_Config.IKGD_Config.AppSettings[key]
    //
    public static System.Collections.Specialized.NameValueCollection AppSettingsWeb { get { return ConfigurationManager.AppSettings; } }
    public static DictionaryConfigIKGD AppSettings
    {
      get
      {
        lock (_lockL2)
        {
          DictionaryConfigIKGD appsettings = (DictionaryConfigIKGD)HttpRuntime.Cache[keyNameAppSettings];
          try
          {
            if (appsettings == null)
            {
              try { appsettings = new DictionaryConfigIKGD(Elements.GroupBy(k => k.Key).ToDictionary(k => k.Key, v => v.OrderBy(r => r.ElementType).ThenByDescending(r => r.Id).FirstOrDefault().Value)); }
              catch { }
              if (appsettings != null)
              {
                AggregateCacheDependency sqlDeps = new AggregateCacheDependency();
                sqlDeps.Add(new SqlCacheDependency("GDCS", "IKGD_CONFIG"));
                HttpRuntime.Cache.Insert(keyNameAppSettings, appsettings, sqlDeps, Cache.NoAbsoluteExpiration, Cache.NoSlidingExpiration, CacheItemPriority.High, (key, value, reason) => { (value as DictionaryConfigIKGD).Clear(); });
              }
              else
              {
                // se non ho connessione al DB si impalla ogni volta, metto una entry a vuoto e continuo ad utilizzare la fallback su web.config
                appsettings = new DictionaryConfigIKGD();
                HttpRuntime.Cache.Insert(keyNameAppSettings, appsettings, null, DateTime.Now.AddSeconds(3600), Cache.NoSlidingExpiration, CacheItemPriority.High, (key, value, reason) => { (value as DictionaryConfigIKGD).Clear(); });
              }
            }
          }
          catch { }
          return appsettings;
        }
      }
    }


    public static string GetAuthorConfigFile(string partialFileName)
    {
      string cfgPath = VirtualPathUtility.Combine((AppSettingsWeb["AuthorBasePath"] ?? "~/"), (AppSettingsWeb["AuthorConfigBasePath"] ?? "Author/Config/"));
      return VirtualPathUtility.Combine(VirtualPathUtility.ToAbsolute(cfgPath), partialFileName);
    }


    //
    // lettura + caching della configurazione per Author
    //
    public static XElement xConfigAuthor
    {
      get
      {
        lock (_lock)
        {
          XElement xConfig = HttpRuntime.Cache[keyNameConfigAuthor] as XElement;
          try
          {
            if (xConfig == null)
            {
              try { HttpRuntime.Cache.Remove(keyNameConfigAuthor); }
              catch { }
              string fileName = null;
              try
              {
                try
                {
                  if (!string.IsNullOrEmpty(IKGD_Config.AppSettingsWeb["IKGD_ApplicationConfig"]))
                  {
                    fileName = Utility.vPathMap(IKGD_Config.GetAuthorConfigFile(string.Format("SiteConfig.{0}.xml", IKGD_Config.AppSettingsWeb["IKGD_ApplicationConfig"])));
                    xConfig = Utility.FileReadXml(fileName);
                  }
                }
                catch { }
                try
                {
                  if (xConfig == null)
                  {
                    fileName = Utility.vPathMap(IKGD_Config.GetAuthorConfigFile(string.Format("SiteConfig.{0}.xml", IKGD_Config.ApplicationName)));
                    xConfig = Utility.FileReadXml(fileName);
                  }
                }
                catch { }
                if (xConfig == null)
                {
                  fileName = Utility.vPathMap(IKGD_Config.GetAuthorConfigFile("SiteConfig.xml"));
                  try { xConfig = Utility.FileReadXml(fileName); }
                  catch { }
                }
                //
                // registriamo un monitor handler sull'oggetto per far scadere la cache in caso di modifica
                // cosi' non si generano eccezioni e le altre istanze hanno sempre il config corretto
                //
                xConfig.Changed += (o, e) =>
                {
                  HttpRuntime.Cache.Remove(keyNameConfigAuthor);
                  //throw new Exception("xConfigAuthor has been modified");
                };
                //
              }
              catch { }
              if (xConfig != null)
              {
                Clear();
                AggregateCacheDependency sqlDeps = new AggregateCacheDependency();
                sqlDeps.Add(new CacheDependency(fileName));
                HttpRuntime.Cache.Insert(keyNameConfigAuthor, xConfig, sqlDeps, Cache.NoAbsoluteExpiration, Cache.NoSlidingExpiration, CacheItemPriority.High, null);
              }
            }
          }
          catch { }
          return xConfig;
        }
      }
    }


    public static bool IsLocalRequestWrapper
    {
      get
      {
        if (HttpContext.Current.Request.IsLocal)
          return true;
        if (Utility.CheckNetMaskIP(Utility.GetRequestAddressExt(null), AppSettings["LocalRequestAddresses"]))
          return true;
        try
        {
          if (HttpContext.Current.Session["RegisteredLocalConnection"] != null && (bool)HttpContext.Current.Session["RegisteredLocalConnection"])
            return true;
        }
        catch { }
        return HttpContext.Current.Request.IsLocal;
      }
    }


    public static bool IsLocalCronRequestWrapper
    {
      get
      {
        if (HttpContext.Current.Request.IsLocal)
          return true;
        if (AppSettings["LocalCronRequestAddresses"].IsNotNullOrWhiteSpace())
        {
          if (Utility.CheckNetMaskIP(Utility.GetRequestAddressExt(null), AppSettings["LocalCronRequestAddresses"]))
            return true;
        }
        return false;
      }
    }


    public static bool IsBatchRequestAllowedWrapper
    {
      get
      {
        if (HttpContext.Current.Request.IsLocal)
          return true;
        if (AppSettings["BatchRequestAllowedAddresses"].IsNotNullOrWhiteSpace())
        {
          if (Utility.CheckNetMaskIP(Utility.GetRequestAddressExt(null), AppSettings["BatchRequestAllowedAddresses"]))
            return true;
        }
        return false;
      }
    }



    public static XElement ExportXML() { return ExportXML(Utility.vPathMap(string.Format(dumpPath, "export"))); }
    public static XElement ExportXML(string fileNameToSave)
    {
      // accesso alle funzionalita' di import export solo da root e con connessioni locali
      if (!HttpContext.Current.Request.IsLocal || !HttpContext.Current.User.Identity.IsAuthenticated || HttpContext.Current.User.Identity.Name != "root")
        throw new UnauthorizedAccessException("Utilizzo di ExportXML() non autorizzato.");
      XElement xRoot = new XElement("IKGD_Config");
      xRoot.SetAttributeValue("clear", true);
      using (Ikon.Config.DataContext DB = Ikon.Config.DataContext.Factory())
      {
        var elements = DB.IKGD_CONFIGs;
        foreach (var app_inst in elements.GroupBy(r => new { application = r.application, instance = r.instance }).OrderBy(r => r.Key.application).ThenBy(r => r.Key.instance))
        {
          XElement xApp = new XElement("Application");
          xRoot.Add(xApp);
          xApp.SetAttributeValue("application", app_inst.Key.application);
          xApp.SetAttributeValue("instance", app_inst.Key.instance);
          xApp.SetAttributeValue("clear", true);
          foreach (var type in app_inst.GroupBy(r => r.type))
          {
            XElement xType = new XElement("Type");
            xApp.Add(xType);
            xType.SetAttributeValue("type", type.Key ?? "");
            xType.SetAttributeValue("clear", true);
            //foreach (var elem in type.OrderBy(r => r.key).ThenBy(r => r.id))
            foreach (var elem in type.OrderBy(r => r.id))
            {
              XElement xKey = new XElement("Key");
              xType.Add(xKey);
              xKey.SetAttributeValue("key", elem.key);
              xKey.SetAttributeValue("value", elem.value);
              // output solo nel caso sia differente dal default
              if (elem.flag_active == false)
                xKey.SetAttributeValue("active", elem.flag_active);
              // output solo nel caso sia differente dal default
              if (elem.flag_system == true)
                xKey.SetAttributeValue("system", elem.flag_system);
              if (string.IsNullOrEmpty(elem.description))
                elem.description = null;
              xKey.SetAttributeValue("description", elem.description);
            }
          }
        }
      }
      try
      {
        if (!string.IsNullOrEmpty(fileNameToSave))
          xRoot.Save(fileNameToSave);
      }
      catch { }
      return xRoot;
    }


    public static bool ImportXML() { return ImportXML(Utility.vPathMap(string.Format(dumpPath, "import"))); }
    public static bool ImportXML(string fileNameToRead) { return ImportXML(Utility.FileReadXml(fileNameToRead)); }
    public static bool ImportXML(XElement xRoot)
    {
      // accesso alle funzionalita' di import export solo da root e con connessioni locali
      if (!HttpContext.Current.Request.IsLocal || !HttpContext.Current.User.Identity.IsAuthenticated || HttpContext.Current.User.Identity.Name != "root")
        throw new UnauthorizedAccessException("Utilizzo di ExportXML() non autorizzato.");
      try
      {
        if (xRoot == null || xRoot.Name.LocalName != "IKGD_Config")
          return false;
        lock (_lock)
        {
          using (Ikon.Config.DataContext DB = Ikon.Config.DataContext.Factory())
          {
            using (TransactionScope ts = new TransactionScope())
            {
              if (Utility.TryParse<bool>(xRoot.AttributeValue("clear"), false))
              {
                DB.IKGD_CONFIGs.DeleteAllOnSubmit(DB.IKGD_CONFIGs);
              }
              foreach (XElement xApp in xRoot.Elements("Application"))
              {
                string applicationName = xApp.AttributeValue("application");
                string instanceName = xApp.AttributeValue("instance");
                if (applicationName == null)
                  throw new Exception("Config Block with application==null");
                if (Utility.TryParse<bool>(xApp.AttributeValue("clear"), false))
                {
                  DB.IKGD_CONFIGs.DeleteAllOnSubmit(DB.IKGD_CONFIGs.Where(r => r.application == applicationName && r.instance == instanceName));
                }
                //
                foreach (XElement xType in xApp.Elements().Where(x => x.Name.LocalName.ToLower() == "type"))
                {
                  string typeSection = xType.AttributeValue("type");
                  if (string.IsNullOrEmpty(typeSection))
                    typeSection = null;
                  var setToDelete = DB.IKGD_CONFIGs.Where(r => r.application == applicationName && r.instance == instanceName && r.type == typeSection);
                  if (Utility.TryParse<bool>(xType.AttributeValue("clear"), false))
                  {
                    DB.IKGD_CONFIGs.DeleteAllOnSubmit(setToDelete);
                  }
                  //
                  foreach (XElement xKey in xType.Elements().Where(x => x.Name.LocalName.ToLower() == "key" || x.Name.LocalName.ToLower() == "add"))
                  {
                    Ikon.Config.IKGD_CONFIG item = new Ikon.Config.IKGD_CONFIG { application = applicationName, instance = instanceName, modif = DateTime.Now, type = typeSection };
                    item.key = xKey.AttributeValueNN("key");
                    item.value = xKey.AttributeValueNN("value");
                    item.description = xKey.AttributeValue("description");
                    item.flag_active = Utility.TryParse<bool>(xKey.AttributeValue("active"), true);
                    item.flag_system = Utility.TryParse<bool>(xKey.AttributeValue("system"), false);
                    if (string.IsNullOrEmpty(item.description))
                      item.description = null;
                    DB.IKGD_CONFIGs.InsertOnSubmit(item);
                  }
                }
              }
              var chg = DB.GetChangeSet();
              DB.SubmitChanges();
              ts.Complete();
            }  // using transaction
          }  // using DB
        }  // lock
        Clear();
        return true;
      }
      catch (Exception ex)
      {
        HttpContext.Current.Trace.Write("Exception", ex.Message);
      }
      return false;
    }


    public class Element
    {
      public int Id { get; set; }   // per poter mantenere l'ordine di creazione degli elementi quando devo estrarre uno solo tra i valori degeneri
      public string ElementType { get; set; }
      public string Key { get; set; }
      public string Value { get; set; }
      public int? ValueInt { get { return Utility.TryParse<int?>(Value, null); } }
      public bool? ValueBool { get { return Utility.TryParse<bool?>(Value, null); } }
      public XElement ValueXML { get { return XElement.Parse(Value); } }
      public string Description { get; set; }
    }


    public class DictionaryConfigIKGD : System.Collections.Specialized.NameValueCollection
    {
      public DictionaryConfigIKGD() : base() { }
      public DictionaryConfigIKGD(IDictionary<string, string> dictionary)
        : base(dictionary.Count)
      {
        dictionary.ForEach(r => this[r.Key] = r.Value);
      }


      public new string this[string key]
      {
        get
        {
          string val = null;
          try { val = ConfigurationManager.AppSettings[key] ?? base[key]; }
          catch { }
          return val;
        }
        set { base[key] = value; }
      }

    }


  }


}
