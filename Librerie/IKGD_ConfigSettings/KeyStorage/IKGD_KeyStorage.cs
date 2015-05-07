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
using System.Xml.Linq;
using System.Data.Linq;
using System.IO;
using System.Text;
using System.Security;
using System.Linq.Expressions;
using LinqKit;

using System.Reflection;
using System.Data.Linq.Mapping;
using System.Diagnostics;
using System.Transactions;
using System.Web.Caching;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;  // assembly System.ServiceModel.Web

using Ikon;
using Ikon.GD;


namespace Ikon.GD
{


  public static class IKGD_KeyStorage
  {
    private static object _lock = new object();
    private static string keyName = "cacheKey_IKGD_KeyStorage";
    private static string dumpPath { get { return IKGD_Config.GetAuthorConfigFile("KeyStorageDB.{0}.xml"); } }



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
        try { HttpRuntime.Cache.Remove(keyName); }
        catch { }
      }
    }


    private static List<Element> Elements
    {
      get
      {
        lock (_lock)
        {
          List<Element> elements = (List<Element>)HttpRuntime.Cache[keyName];
          try
          {
            if (elements == null)
            {
              using (Ikon.Config.DataContext DB = Ikon.Config.DataContext.Factory())
              {
                elements = DB.IKGD_KEYSTORAGEs.Where(r => r.application == IKGD_Config.ApplicationName).Where(r => r.flag_active).OrderBy(r => r.type).ThenBy(r => r.position).ThenBy(r => r.key).Select(r => new Element { ElementType = r.type, Key = r.key, Value = r.value, Position = r.position, DescriptionNeutral = r.description, Descriptions = r.IKGD_KEYSTORAGE_MAPs.ToDictionary(m => m.language, m => m.description) }).ToList();
              }
              if (elements != null)
              {
                AggregateCacheDependency sqlDeps = new AggregateCacheDependency();
                sqlDeps.Add(new SqlCacheDependency("GDCS", "IKGD_KEYSTORAGE"), new SqlCacheDependency("GDCS", "IKGD_KEYSTORAGE_MAP"));
                HttpRuntime.Cache.Insert(keyName, elements, sqlDeps, Cache.NoAbsoluteExpiration, Cache.NoSlidingExpiration, CacheItemPriority.High, (key, value, reason) => { (value as List<Element>).Clear(); });
              }
            }
          }
          catch { }
          return elements;
        }
      }
    }


    public static XElement ExportXML() { return ExportXML(Utility.vPathMap(string.Format(dumpPath, "export"))); }
    public static XElement ExportXML(string fileNameToSave)
    {
      // accesso alle funzionalita' di import export solo da root e con connessioni locali
      if (!HttpContext.Current.Request.IsLocal || !HttpContext.Current.User.Identity.IsAuthenticated || HttpContext.Current.User.Identity.Name != "root")
        throw new UnauthorizedAccessException("Utilizzo di ExportXML() non autorizzato.");
      XElement xRoot = new XElement("IKGD_KeyStorage");
      xRoot.SetAttributeValue("clear", true);
      using (Ikon.Config.DataContext DB = Ikon.Config.DataContext.Factory())
      {
        var elements = DB.IKGD_KEYSTORAGEs;
        foreach (var app_inst in elements.GroupBy(r => r.application).OrderBy(r => r.Key))
        {
          XElement xApp = new XElement("Application");
          xRoot.Add(xApp);
          xApp.SetAttributeValue("application", app_inst.Key);
          xApp.SetAttributeValue("clear", true);
          foreach (var type in app_inst.GroupBy(r => r.type))
          {
            XElement xType = new XElement("Type");
            xApp.Add(xType);
            xType.SetAttributeValue("type", type.Key ?? "");
            xType.SetAttributeValue("clear", true);
            foreach (var elem in type.OrderBy(r => r.position).ThenBy(r => r.key))
            {
              XElement xKey = new XElement("Key");
              xType.Add(xKey);
              xKey.SetAttributeValue("key", elem.key);
              xKey.SetAttributeValue("value", elem.value);
              xKey.SetAttributeValue("position", elem.position.ToString(System.Globalization.CultureInfo.InvariantCulture));
              // output solo nel caso sia differente dal default
              if (elem.flag_active == false)
                xKey.SetAttributeValue("active", elem.flag_active);
              // output solo nel caso sia differente dal default
              if (elem.flag_system == true)
                xKey.SetAttributeValue("system", elem.flag_system);
              if (string.IsNullOrEmpty(elem.description))
                elem.description = null;
              xKey.SetAttributeValue("description", elem.description);
              foreach (var map in elem.IKGD_KEYSTORAGE_MAPs.Where(d => !string.IsNullOrEmpty(d.description) && d.language.Length == 2))
                xKey.SetAttributeValue("description_" + map.language, map.description);
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
        if (xRoot == null || xRoot.Name.LocalName != "IKGD_KeyStorage")
          return false;
        string langDescBase = "description_";
        lock (_lock)
        {
          using (Ikon.Config.DataContext DB = Ikon.Config.DataContext.Factory())
          {
            using (TransactionScope ts = new TransactionScope())
            {
              if (Utility.TryParse<bool>(xRoot.AttributeValue("clear"), false))
              {
                DB.IKGD_KEYSTORAGEs.DeleteAllOnSubmit(DB.IKGD_KEYSTORAGEs);
              }
              foreach (XElement xApp in xRoot.Elements("Application"))
              {
                string applicationName = xApp.AttributeValue("application");
                if (applicationName == null)
                  throw new Exception("Config Block with application==null");
                if (Utility.TryParse<bool>(xApp.AttributeValue("clear"), false))
                {
                  DB.IKGD_KEYSTORAGEs.DeleteAllOnSubmit(DB.IKGD_KEYSTORAGEs.Where(r => r.application == applicationName));
                }
                //
                foreach (XElement xType in xApp.Elements().Where(x => x.Name.LocalName.ToLower() == "type"))
                {
                  string typeSection = xType.AttributeValue("type");
                  if (string.IsNullOrEmpty(typeSection))
                    typeSection = null;
                  var setToDelete = DB.IKGD_KEYSTORAGEs.Where(r => r.application == applicationName && r.type == typeSection);
                  if (Utility.TryParse<bool>(xType.AttributeValue("clear"), false))
                  {
                    DB.IKGD_KEYSTORAGEs.DeleteAllOnSubmit(setToDelete);
                  }
                  else
                  {
                    // cancellazione delle key che dovranno essere sovrascritte, per evitare errori con key duplicate (solo per KeyStorage)
                    var keysToDelete = xType.Elements().Where(x => x.Name.LocalName.ToLower() == "key" || x.Name.LocalName.ToLower() == "add").Select(x => x.AttributeValue("key")).Distinct().ToList();
                    DB.IKGD_KEYSTORAGEs.DeleteAllOnSubmit(setToDelete.Where(r => keysToDelete.Contains(r.key)));
                  }
                  //
                  DB.SubmitChanges();
                  double positionMax = 0.0;
                  try { positionMax = DB.IKGD_KEYSTORAGEs.Where(r => r.application == applicationName && r.type == typeSection).Max(r => r.position); }
                  catch { }
                  //
                  foreach (XElement xKey in xType.Elements().Where(x => x.Name.LocalName.ToLower() == "key" || x.Name.LocalName.ToLower() == "add"))
                  {
                    double positionNew = Utility.TryParse<double>(xKey.AttributeValue("position"), positionMax + 1.0);
                    positionMax = Math.Max(positionMax, positionNew);
                    Ikon.Config.IKGD_KEYSTORAGE item = new Ikon.Config.IKGD_KEYSTORAGE { application = applicationName, modif = DateTime.Now, type = typeSection };
                    item.key = xKey.AttributeValueNN("key");
                    item.value = xKey.AttributeValueNN("value");
                    item.position = positionNew;
                    item.description = xKey.AttributeValue("description");
                    item.flag_active = Utility.TryParse<bool>(xKey.AttributeValue("active"), true);
                    item.flag_system = Utility.TryParse<bool>(xKey.AttributeValue("system"), false);
                    if (string.IsNullOrEmpty(item.description))
                      item.description = null;
                    DB.IKGD_KEYSTORAGEs.InsertOnSubmit(item);
                    var maps = xKey.Attributes().Where(x => x.Name.LocalName.StartsWith(langDescBase) && x.Name.LocalName.Length > langDescBase.Length).Select(x => new Ikon.Config.IKGD_KEYSTORAGE_MAP { language = x.Name.LocalName.Substring(langDescBase.Length), description = x.Value }).Where(d => !string.IsNullOrEmpty(d.description) && d.language.Length == 2).Distinct();
                    item.IKGD_KEYSTORAGE_MAPs.AddRange(maps);
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
      public string ElementType { get; set; }
      public string Key { get; set; }
      public double Position { get; set; }
      public string Value { get; set; }
      public int? ValueInt { get { return Utility.TryParse<int?>(Value, null); } }
      public bool? ValueBool { get { return Utility.TryParse<bool?>(Value, null); } }
      public XElement ValueXML { get { return XElement.Parse(Value); } }
      public string DescriptionNeutral { get; set; }
      public Dictionary<string, string> Descriptions { get; set; }

      public string Description(string language) { return Descriptions.ContainsKey(language) ? Descriptions[language] : (!string.IsNullOrEmpty(DescriptionNeutral) ? DescriptionNeutral : Value); }
      //TODO: overload con language ottenuto dallo status
    }

  }

}
