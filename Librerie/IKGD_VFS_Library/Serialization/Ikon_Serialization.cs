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
using System.Linq;
using System.Xml.Linq;
using System.Xml;
using System.Collections;
using System.Collections.Generic;
using System.Linq.Dynamic;
using System.Linq.Expressions;
using System.Data.Linq;
using System.IO;
using System.Text;
using System.Reflection;
using System.Diagnostics;
using System.ComponentModel;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;  // assembly System.ServiceModel.Web
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Bson;

using Ikon;
using Ikon.IKGD.Library;
using Ikon.IKCMS;


namespace Ikon.GD
{



  public static class IKGD_Serialization
  {
    //
    //public static JsonSerializerSettings DefaultSerializerSettings { get { return new JsonSerializerSettings() { NullValueHandling = NullValueHandling.Include, ConstructorHandling = ConstructorHandling.AllowNonPublicDefaultConstructor }; } }
    public static JsonSerializerSettings DefaultSerializerSettings { get { return new JsonSerializerSettings() { NullValueHandling = NullValueHandling.Ignore, DefaultValueHandling = DefaultValueHandling.Ignore, ConstructorHandling = ConstructorHandling.AllowNonPublicDefaultConstructor }; } }
    //
    enum JsonImplementationEnum { Json_Net, FastJSON }
    private static JsonImplementationEnum JsonImplementation { get; set; }
    //

    static IKGD_Serialization()
    {
      //JsonImplementationEnum.FastJSON non è sufficientemente robusto da serializzare/deserializzare KeyValueObjectTree
      JsonImplementation = JsonImplementationEnum.Json_Net;
    }


    public static string SerializeToJSON(object obj)
    {
      switch (JsonImplementation)
      {
        case JsonImplementationEnum.FastJSON:
          return fastJSON.JSON.Instance.ToJSON(obj, new fastJSON.JSONParameters() { UseExtensions = false, UsingGlobalTypes = false, EnableAnonymousTypes = true });
        case JsonImplementationEnum.Json_Net:
        default:
          return JsonConvert.SerializeObject(obj);
      }
    }


    public static XElement SerializeToXml(object obj) { return SerializeToXml(obj, null); }
    public static XElement SerializeToXml(object obj, string rootElementName)
    {
      try { return JsonConvert.DeserializeXmlNode(JsonConvert.SerializeObject(obj), rootElementName ?? "root").ToXElement(); }
      catch { return null; }
    }


    public static T DeSerializeJSON<T>(string jsonString)
    {
      switch (JsonImplementation)
      {
        case JsonImplementationEnum.FastJSON:
          return fastJSON.JSON.Instance.ToObject<T>(jsonString);
        case JsonImplementationEnum.Json_Net:
        default:
          return JsonConvert.DeserializeObject<T>(jsonString);
      }
    }
    public static T DeSerializeJSON<T>(string jsonString, string staticDefaultProperty)
    {
      T result = default(T);
      try
      {
        try
        {
          switch (JsonImplementation)
          {
            case JsonImplementationEnum.FastJSON:
              result = fastJSON.JSON.Instance.ToObject<T>(jsonString);
              break;
            case JsonImplementationEnum.Json_Net:
            default:
              result = JsonConvert.DeserializeObject<T>(jsonString, DefaultSerializerSettings);
              break;
          }
        }
        catch { }
        if (result == null && !string.IsNullOrEmpty(staticDefaultProperty))
          result = Utility.FindPropertyStatic<T>(typeof(T), staticDefaultProperty);
        return result;
      }
      catch { return result; }
    }


    public static object DeSerializeJSON(Type ty, string jsonString, string staticDefaultProperty)
    {
      object result = null;
      try
      {
        try
        {
          switch (JsonImplementation)
          {
            case JsonImplementationEnum.FastJSON:
              result = fastJSON.JSON.Instance.ToObject(jsonString, ty);
              break;
            case JsonImplementationEnum.Json_Net:
            default:
              result = JsonConvert.DeserializeObject(jsonString, ty, DefaultSerializerSettings);
              break;
          }
        }
        catch { }
        if (result == null && !string.IsNullOrEmpty(staticDefaultProperty))
        {
          PropertyInfo pi = ty.GetProperty(staticDefaultProperty);
          if (pi != null)
            result = pi.GetValue(null, null);
        }
      }
      catch { }
      return result;
    }


    public static XElement DeSerializeJsonToXml(string jsonString, string rootElementName)
    {
      try { return JsonConvert.DeserializeXmlNode(jsonString, rootElementName ?? "root").ToXElement(); }
      catch { return null; }
    }



    public static T DeSerializeXml<T>(XElement xObject, string staticDefaultProperty)
    {
      T result = default(T);
      try
      {
        XElement xAux = new XElement(xObject);
        xAux.DescendantNodes()
          .Where(n => n.NodeType == XmlNodeType.SignificantWhitespace || n.NodeType == XmlNodeType.Whitespace)
          .Where(n => n.NodeType == XmlNodeType.Text)
          .Where(n => n.PreviousNode != null || n.NextNode != null)
          .Where(n => (n as XText).Value.Trim().Length == 0).Remove();
        //
        string jsonString = JsonConvert.SerializeObject(xAux.ToXmlNode(), new JsonConverter[] { new XmlNodeConverter() });
        jsonString = jsonString.Substring(jsonString.IndexOf('{', 1));
        jsonString = jsonString.Substring(0, jsonString.Length - 1);
        try { result = JsonConvert.DeserializeObject<T>(jsonString, DefaultSerializerSettings); }
        catch { }
        if (result == null && !string.IsNullOrEmpty(staticDefaultProperty))
          result = Utility.FindPropertyStatic<T>(typeof(T), staticDefaultProperty);
        return result;
      }
      catch { return result; }
    }


    public static object DeSerializeXml(Type ty, XElement xObject, string staticDefaultProperty)
    {
      object result = null;
      try
      {
        XElement xAux = new XElement(xObject);
        xAux.DescendantNodes()
          .Where(n => n.NodeType == XmlNodeType.SignificantWhitespace || n.NodeType == XmlNodeType.Whitespace)
          .Where(n => n.NodeType == XmlNodeType.Text)
          .Where(n => n.PreviousNode != null || n.NextNode != null)
          .Where(n => (n as XText).Value.Trim().Length == 0).Remove();
        //
        string jsonString = JsonConvert.SerializeObject(xAux.ToXmlNode(), new JsonConverter[] { new XmlNodeConverter() });
        jsonString = jsonString.Substring(jsonString.IndexOf('{', 1));
        jsonString = jsonString.Substring(0, jsonString.Length - 1);
        try { result = JsonConvert.DeserializeObject(jsonString, ty, DefaultSerializerSettings); }
        catch { }
        if (result == null && !string.IsNullOrEmpty(staticDefaultProperty))
        {
          PropertyInfo pi = ty.GetProperty(staticDefaultProperty);
          if (pi != null)
            result = pi.GetValue(null, null);
        }
        return result;
      }
      catch { return result; }
    }


    public static string SerializeToBSON(object obj)
    {
      using (MemoryStream ms = new MemoryStream())
      {
        JsonSerializer serializer = new JsonSerializer() { NullValueHandling = NullValueHandling.Include, ConstructorHandling = ConstructorHandling.AllowNonPublicDefaultConstructor };
        using (BsonWriter writer = new BsonWriter(ms))
        {
          serializer.Serialize(writer, obj);
        }
        var str01 = Convert.ToBase64String(ms.ToArray());
        //var str02 = BitConverter.ToString(ms.ToArray());
        return str01;
      }
    }
    public static T DeSerializeBSON<T>(string jsonString)
    {
      using (MemoryStream ms = new MemoryStream(Convert.FromBase64String(jsonString)))
      {
        JsonSerializer serializer = new JsonSerializer() { NullValueHandling = NullValueHandling.Include, ConstructorHandling = ConstructorHandling.AllowNonPublicDefaultConstructor };
        using (BsonReader reader = new BsonReader(ms))
        {
          return serializer.Deserialize<T>(reader);
        }
      }
    }
    public static T DeSerializeBSON<T>(string jsonString, string staticDefaultProperty)
    {
      T result = default(T);
      try
      {
        try
        {
          using (MemoryStream ms = new MemoryStream(Convert.FromBase64String(jsonString)))
          {
            JsonSerializer serializer = new JsonSerializer() { NullValueHandling = NullValueHandling.Include, ConstructorHandling = ConstructorHandling.AllowNonPublicDefaultConstructor };
            using (BsonReader reader = new BsonReader(ms))
            {
              result = serializer.Deserialize<T>(reader);
            }
          }
        }
        catch { }
        if (result == null && !string.IsNullOrEmpty(staticDefaultProperty))
          result = Utility.FindPropertyStatic<T>(typeof(T), staticDefaultProperty);
        return result;
      }
      catch { return result; }
    }


    public static T CloneTo<T>(object obj)
    {
      switch (JsonImplementation)
      {
        case JsonImplementationEnum.FastJSON:
          return (T)fastJSON.JSON.Instance.DeepCopy(obj);
        case JsonImplementationEnum.Json_Net:
        default:
          {
            using (MemoryStream ms = new MemoryStream())
            {
              JsonSerializer serializer = new JsonSerializer() { NullValueHandling = NullValueHandling.Include, ConstructorHandling = ConstructorHandling.AllowNonPublicDefaultConstructor };
              using (BsonWriter writer = new BsonWriter(ms))
              {
                serializer.Serialize(writer, obj);
                writer.Flush();
                ms.Seek(0, SeekOrigin.Begin);
                using (BsonReader reader = new BsonReader(ms))
                {
                  return serializer.Deserialize<T>(reader);
                }
              }
            }
          }
        //break;
      }
      //return default(T);
    }


  }




  //
  // interface per la definizione delle classi di supporto per la deserializzazione su VFS
  //
  public interface IKGD_DeserializeOnVFS_Interface
  {
    //
    //TODO: per adesso IKGD_PROPERTY poi diventera' una entity ad hoc IKGD_VDATA_VAR.
    //id (autoinc),rnode,vdata_version,level,key,key_parent,id_parent,type (int),value_int,value_double,value_date,value_string,value_text(MAX)
    //id (autoinc),rnode,vdata_version,level,key,key_parent,value_int,value_double,value_date,value_string,value_text(MAX)
    //creare accessors helpers (generatori di iQuerable per poi crearsi i vari filtri/join):
    //vData - > fields({level},key,{keyparent},{type})
    //
    int DeserializeOnVFS(FS_Operations fsOp, IKGD_VDATA vData, object property, PropertyInfo propertyInfo, IKGD_DeserializeOnVFS_Attribute_Interface deserializeAttribute, int level);
  }


  //
  // classe per la deserializzazione su VFS delle properties KVT
  // da spostare poi in KeyValueObjectTree.cs
  //
  public class IKGD_DeserializeOnVFS_KVT : IKGD_DeserializeOnVFS_Interface
  {

    public int DeserializeOnVFS(FS_Operations fsOp, IKGD_VDATA vData, object property, PropertyInfo propertyInfo, IKGD_DeserializeOnVFS_Attribute_Interface deserializeAttribute, int level)
    {
      int itemsCount = 0;
      if (property is KeyValueObjectTree && deserializeAttribute is IKGD_DeserializeOnVFS_KVTAttribute)
      {
        try
        {
          IKGD_DeserializeOnVFS_KVTAttribute deserAttr = deserializeAttribute as IKGD_DeserializeOnVFS_KVTAttribute;
          KeyValueObjectTree KVT = property as KeyValueObjectTree;
          if (deserAttr.FullLanguageSetDump)
          {
            foreach (string lang in IKGD_Language_Provider.Provider.LanguagesAvailable())
            {
              KeyValueObjectTree KVTL = null;
              KeyValueObjectTree KVTNL = null;
              if (KVT.ContainsKey(lang))
                KVTL = KVT[lang];
              if (KVT.ContainsKey(null))
                KVTNL = KVT[null];
              if (KVTL != null || KVTNL != null)
              {
                itemsCount += DeserializeOnVFS_Worker(fsOp, vData, KVTL, KVTNL, deserAttr, level, deserializeAttribute.BaseKey ?? propertyInfo.Name, lang);
              }
            }
          }
          else
          {
            itemsCount += DeserializeOnVFS_Worker(fsOp, vData, KVT, null, deserAttr, level, deserializeAttribute.BaseKey ?? propertyInfo.Name, KVT.Key);
          }
        }
        catch { }
      }
      return itemsCount;
    }


    public int DeserializeOnVFS_Worker(FS_Operations fsOp, IKGD_VDATA vData, KeyValueObjectTree KVT_Main, KeyValueObjectTree KVT_FallBack, IKGD_DeserializeOnVFS_KVTAttribute deserAttr, int level, string key_parent, string key_forced)
    {
      int itemsCount = 0;
      try
      {
        KeyValueObjectTree KVT = KVT_Main ?? KVT_FallBack;
        if (KVT == null || deserAttr == null || KVT.IsSystemKey)
          return itemsCount;
        //
        string key = key_forced ?? KVT.Key;
        string value = KVT.ValueString;
        if (!(deserAttr.SkipNullValues && value == null) && !(deserAttr.SkipNullOrEmptyValues && value.IsNullOrWhiteSpace()))
        {
          int? value_int = Utility.TryParse<int?>(value);
          double? value_double = Utility.TryParse<double?>(value);

          DateTime? value_date = null;
          DateTime? value_dateExt = null;
          if (value != null && value.IndexOf('|') >= 0)
          {
            var frags = value.Split("|".ToCharArray(), 2);
            value_date = Utility.TryParse<DateTime?>(frags.FirstOrDefault());
            value_dateExt = Utility.TryParse<DateTime?>(frags.Skip(1).FirstOrDefault());
            if (value_dateExt != null && value_dateExt < Utility.DateTimeMinValueDB)
              value_dateExt = null;
            if (value_dateExt != null && value_dateExt > Utility.DateTimeMaxValueDB)
              value_dateExt = null;
          }
          else
          {
            value_date = Utility.TryParse<DateTime?>(value);
          }
          if (value_date != null && value_date < Utility.DateTimeMinValueDB)
            value_date = null;
          if (value_date != null && value_date > Utility.DateTimeMaxValueDB)
            value_date = null;
          //
          IKGD_VDATA_KEYVALUE data = fsOp.Factory_IKGD_VDATA_KEYVALUE(level, key, key_parent, value, value_int, value_double, value_date, value_dateExt);
          data.flag_published = vData.flag_published;
          data.flag_current = vData.flag_current;
          data.modif = vData.version_date;
          if (vData.rnode != 0 && vData.version != 0)
          {
            data.vDataVersion = vData.version;
            data.rNode = vData.rnode;
            fsOp.DB.IKGD_VDATA_KEYVALUEs.InsertOnSubmit(data);
          }
          else if (vData.rnode != 0 && vData.IKGD_RNODE == null)
          {
            data.rNode = vData.rnode;
            data.vDataVersion = vData.version;
            fsOp.DB.IKGD_VDATA_KEYVALUEs.InsertOnSubmit(data);
          }
          else if (vData.IKGD_RNODE != null)
          {
            data.vDataVersion = vData.version;
            data.IKGD_RNODE = vData.IKGD_RNODE;
            vData.IKGD_VDATA_KEYVALUEs.Add(data);
          }
          else
          {
            // se ci si ritrova in questo blocco vuol dire che c'e' qualcosa di strano con i VDATA che non sono mappati e non hanno nemmeno i nodi settati
          }
          itemsCount++;
          //
        }
        //
        List<string> keysMain = (KVT_Main != null) ? KVT_Main.Nodes.Where(n => !n.IsSystemKey).Select(n => n.Key).ToList() : new List<string>();
        List<string> keysFallBack = (KVT_FallBack != null) ? KVT_FallBack.Nodes.Where(n => !n.IsSystemKey).Select(n => n.Key).ToList() : new List<string>();
        //
        // questo schema non consente la gestione dei fields degeneri
        //foreach (string subKey in keysMain.Union(keysFallBack))
        //{
        //  KeyValueObjectTree subKVT_Main = (KVT_Main == null) ? null : KVT_Main.Nodes.FirstOrDefault(n => n.Key == subKey);
        //  KeyValueObjectTree subKVT_FallBack = (KVT_FallBack == null) ? null : KVT_FallBack.Nodes.FirstOrDefault(n => n.Key == subKey);
        //  itemsCount += DeserializeOnVFS_Worker(fsOp, vData, subKVT_Main, subKVT_FallBack, deserAttr, level + 1, key, subKey);
        //}
        //
        // questo schema consente anche la gestione dei fields degeneri
        foreach (string subKey in keysMain.Union(keysFallBack))
        {
          var subKVT_Mains = (KVT_Main == null) ? Enumerable.Empty<KeyValueObjectTree>() : KVT_Main.Nodes.Where(n => n.Key == subKey);
          var subKVT_FallBacks = (KVT_FallBack == null) ? Enumerable.Empty<KeyValueObjectTree>() : KVT_FallBack.Nodes.Where(n => n.Key == subKey);
          var subKVT_FallBackDef = subKVT_FallBacks.FirstOrDefault();
          int range = Math.Max(subKVT_Mains.Count(), subKVT_FallBacks.Count());
          for (int i = 0; i < range; i++)
          {
            KeyValueObjectTree subKVT_Main = subKVT_Mains.Skip(i).FirstOrDefault();
            KeyValueObjectTree subKVT_FallBack = subKVT_FallBacks.Skip(i).FirstOrDefault() ?? subKVT_FallBackDef;
            itemsCount += DeserializeOnVFS_Worker(fsOp, vData, subKVT_Main, subKVT_FallBack, deserAttr, level + 1, key, subKey);
          }
        }
        //
      }
      catch { }
      return itemsCount;
    }

  }




}
