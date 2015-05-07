/*
 * 
 * Intranet Ikon
 * 
 * Copyright (C) 2008 Ikon Srl
 * Tutti i Diritti Riservati. All Rights Reserved
 * 
 */


using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Data;
using System.Configuration;
using System.Web;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security;
using System.Security.Principal;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;
using System.ComponentModel;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;  // assembly System.ServiceModel.Web

using Ikon;
using Ikon.Log;
using Ikon.Support;
using Ikon.GD;
using Ikon.IKCMS;
using Ikon.Indexer;


namespace Ikon.IKGD.Library.Resources
{

  //
  // interface base per la definizione delle risorse/widget
  //
  public interface IKGD_ResourceType_Interface
  {
    bool HasInode { get; }
    bool IsFolder { get; }
    bool IsUnstructured { get; }
    bool IsSelectable { get; }
    bool IsWidget { get; }
    bool IsWidgetSingleton { get; }
    bool IsWidgetBrowse { get; }
    bool IsCollection { get; }
    bool IsCompatibleWith(IKGD_ResourceType_Interface testObj);
    //
    string IconEditor { get; }
    //
    bool IsIndexable { get; }
    // eseguire l'override dei getter seguenti per customizzare le proprieta'/elementi da utilizzare per il search engine
    string SearchTitleMember { get; }  // member su resourceData
    string SearchTextMember { get; }  // member su resourceData
    // nel caso non bastino i Search*Member e' sempre possibile eseguire l'override dei metodi generali
    void GetSearchInfoTitle(FS_Operations fsOp, Ikon.Filters.IKGD_HtmlCleaner xHtmlCleaner, List<IKCMS_LuceneRecordData> records);
    void GetSearchInfoTexts(FS_Operations fsOp, Ikon.Filters.IKGD_HtmlCleaner xHtmlCleaner, List<IKCMS_LuceneRecordData> records);
  }


  public interface IKGD_ResourceTypeCollectable_Interface { }


  //
  // interface base per la definizione dei widget
  //
  // i WidgetSettings vengono definiti come oggetti generici nell'interfaccia, poi nelle classi derivate
  // saranno definiti come istanze specifiche dell'interfaccia e sovrascritti dai metodi
  // della classe base stessa usando il modificatore new nella ridefinizione di WidgetSettings
  // questa volta con il tipo di ritorno corretto
  //
  public interface IKGD_Widget_Interface
  {
    IKGD_WidgetDataBase WidgetSettings { get; set; }
    Type WidgetSettingsType { get; }
  }
  //
  public class IKGD_WidgetBase : IKGD_ResourceTypeBase, IKGD_ResourceType_Interface, IKGD_Widget_Interface
  {
    public override bool HasInode { get { return false; } }  // generalmente i widget non hanno INODE
    public override bool IsWidget { get { return true; } }  // per evitare reflection
    //
    IKGD_WidgetDataBase IKGD_Widget_Interface.WidgetSettings { get; set; }
    public IKGD_WidgetDataBase WidgetSettings { get { return (this as IKGD_Widget_Interface).WidgetSettings as IKGD_WidgetDataBase; } set { (this as IKGD_Widget_Interface).WidgetSettings = value; } }
    public virtual Type WidgetSettingsType { get { return typeof(IKGD_WidgetDataBase); } }

    public IKGD_WidgetBase()
    {
      // inizializzazione dei WidgetSettings
      try
      {
        Type ty = this.GetType().GetProperties().FirstOrDefault(p => p.Name == "WidgetSettings").PropertyType;
        PropertyInfo pi = ty.GetProperty("DefaultValue");
        WidgetSettings = pi.GetValue(null, null) as IKGD_WidgetDataBase;
      }
      catch { }
    }
  }


  //
  // interface base per la definizione dei widget associati ad una risorsa e senza possibilita' di browsing
  //
  public interface IKGD_WidgetSingleton_Interface
  {
  }
  //
  public class IKGD_WidgetBaseSingleton : IKGD_WidgetBase, IKGD_WidgetSingleton_Interface
  {
    public override bool IsWidgetSingleton { get { return true; } }  // per evitare reflection
    public override bool IsWidgetBrowse { get { return false; } }  // per evitare reflection
  }


  //
  // interface base per la definizione dei widget associati ad una cartella e con la possibilita' di browsing
  //
  public interface IKGD_WidgetBrowse_Interface : IKGD_Folder_Interface
  {
  }


  //
  // interface base per la definizione dei folders/collection/widget browse
  //
  public interface IKGD_Folder_Interface
  {
  }


  //
  // interface base per la definizione delle collections
  //
  public interface IKGD_FolderCollection_Interface : IKGD_Folder_Interface
  {
  }


  //
  // interface base per la definizione delle proprieta' serializzabili dei widget
  // vengono definite come oggetti generici nell'interfaccia, poi nella classe base
  // saranno definiti come istanze specifiche dell'interfaccia e sovrascritti dai metodi
  // della classe base stessa. Nelle classi derivate bastera' usare il modificatore
  // new nella ridefinizione delle properties Window,Config,Settings,Data che consente
  // di modificare anche il tipo di ritorno...
  //
  public interface IKGD_WidgetData_Interface
  {
    object Window { get; set; }
    object Config { get; set; }
    object Settings { get; set; }
    object Data { get; set; }
    //
    Type[] knownTypes { get; }
    //
    void EnsureValued();
    string Serialize();
  }


  //
  // classe base per la tefinizione dei DataType riconsciuti dal gestore documentale
  //
  public abstract class IKGD_ResourceTypeBase : IKGD_ResourceType_Interface
  {
    //
    // proprieta' standard di IKGD_ResourceType_Interface
    //
    public virtual bool HasInode { get { return true; } }
    public virtual bool IsUnstructured { get { return false; } }
    public virtual bool IsSelectable { get { return true; } }
    public virtual bool IsWidget { get { return this.GetType().GetInterface("IKGD_Widget_Interface") != null; } }
    public virtual bool IsWidgetSingleton { get { return this.GetType().GetInterface("IKGD_WidgetSingleton_Interface") != null; } }
    public virtual bool IsWidgetBrowse { get { return this.GetType().GetInterface("IKGD_WidgetBrowse_Interface") != null; } }
    public virtual bool IsCollection { get { return this.GetType().GetInterface("IKGD_FolderCollection_Interface") != null; } }
    public virtual bool IsFolder { get { return this.GetType().GetInterface("IKGD_Folder_Interface") != null; } }
    public virtual bool IsIndexable { get { return (this is IKCMS_IsIndexable_Interface); } }
    //
    public virtual string IconEditor { get { return IsFolder ? "VFS.Folder.gif" : "VFS.File.gif"; } }
    public static Type IKGD_ResourceEditorConfigBaseType;
    //
    // memeber / elements(xml) dai quali estrarre le informazioni testuali per i testi del search engine
    public virtual string SearchTitleMember { get { return null; } }
    public virtual string SearchTextMember { get { return null; } }



    public virtual bool IsCompatibleWith(IKGD_ResourceType_Interface testObj)
    {
      bool res = true;
      res &= testObj.IsFolder == IsFolder;
      res &= testObj.IsUnstructured == IsUnstructured;
      //
      // verifico che i due oggetti abbiano interfaces compatibili
      //
      if (res && !this.IsFolder)
      {
        try
        {
          if (this.GetType().IsAssignableTo(testObj.GetType()) || this.GetType().IsAssignableFrom(testObj.GetType()))
            return res;
          var intf01 = this.GetType().GetInterfaces().Where(t => t.GetMembers().Any()).Select(t => t.IsGenericType ? t.GetGenericTypeDefinition() : t).ToList();
          var intf02 = testObj.GetType().GetInterfaces().Where(t => t.GetMembers().Any()).Select(t => t.IsGenericType ? t.GetGenericTypeDefinition() : t).ToList();
          res &= intf01.SequenceEqual(intf02);
        }
        catch { return false; }
      }
      //
      return res;
    }


    public IKGD_ResourceTypeBase()
    {
    }


    //
    // crea una nuova istanza del tipo specificato dall'oggetto della configurazione dell'editor
    // referenceObject deve essere un oggetto derivato da Ikon.IKGD.Library.Editor.IKGD_ResourceEditorConfigBase
    // con interface IKGD_ResourceEditorConfig_Interface
    // non posso definire il tipo dell'oggetto in quanto dipende da altri progetti
    //
    public static IKGD_ResourceTypeBase CreateInstanceFromEditor(object referenceObject)
    {
      try
      {
        if (IKGD_ResourceEditorConfigBaseType == null)
        {
          IKGD_ResourceEditorConfigBaseType = Utility.FindType("IKGD_ResourceEditorConfigBase");
          if (IKGD_ResourceEditorConfigBaseType == null)
            throw new NotImplementedException("Il tipo IKGD_ResourceEditorConfigBase non è definito nel sistema");
        }
        Type ref_ty = referenceObject.GetType();
        if (!ref_ty.IsSubclassOf(IKGD_ResourceEditorConfigBaseType))
          return null;
        Type ty = ref_ty.GetProperties().FirstOrDefault(p => p.Name == "ResourceObject").PropertyType;
        ConstructorInfo tyci = ty.GetConstructor(Type.EmptyTypes);
        IKGD_ResourceTypeBase objNew = tyci.Invoke(null) as IKGD_ResourceTypeBase;
        return objNew;
      }
      catch { }
      return null;
    }
    //
    // crea una nuova istanza del tipo specificato con il costruttore di default
    //
    public static IKGD_ResourceTypeBase CreateInstance(Type ty)
    {
      try { return Activator.CreateInstance(ty) as IKGD_ResourceTypeBase; }
      catch { return null; }
      //ConstructorInfo tyci = ty.GetConstructor(Type.EmptyTypes);
      //IKGD_ResourceTypeBase objNew = tyci.Invoke(null) as IKGD_ResourceTypeBase;
      //return objNew;
    }


    //
    // ritorna tutti i resource type registrati [IKGD_ResourceType_Interface]
    //
    public static IEnumerable<Type> FindRegisteredResourceTypes() { return FindRegisteredResourceTypes(false); }
    public static IEnumerable<Type> FindRegisteredResourceTypes(bool allowAbstractClasses)
    {
      var lista = Utility.FindTypesWithInterfaces(typeof(IKGD_ResourceType_Interface));
      if (!allowAbstractClasses)
        lista = lista.Where(ty => !ty.IsAbstract);
      return lista;
    }


    public virtual string Description
    {
      get
      {
        try { return (this.GetType().GetCustomAttributes(typeof(DescriptionAttribute), true).FirstOrDefault() as DescriptionAttribute).Description; }
        catch { return string.Empty; }
      }
    }


    protected object GetSearchInfoResourceData(FS_Operations fsOp, FS_Operations.FS_NodeInfo_Interface fsNode)
    {
      object resourceData = null;
      try
      {
        if (this is IKGD_Widget_Interface)
          resourceData = IKGD_WidgetDataImplementation.DeSerializeByType((this as IKGD_Widget_Interface).WidgetSettingsType, fsNode.vData.settings, true).Config;
        else if (this is IKCMS_HasSerializationCMS_Interface)
          resourceData = IKCMS_RegisteredTypes.Deserialize_IKCMS_ResourceVFS(fsNode).ResourceSettingsObject;
      }
      catch { }
      return resourceData;
    }


    //
    // processa i nodi preparati dal sistema ed estrae i titoli per le risorse
    //
    public virtual void GetSearchInfoTitle(FS_Operations fsOp, Ikon.Filters.IKGD_HtmlCleaner xHtmlCleaner, List<IKCMS_LuceneRecordData> records)
    {
      try
      {
        var props = Utility.Explode(SearchTitleMember.DefaultIfEmptyTrim(IKGD_Config.AppSettings["LuceneIndex_PropsTitle"] ?? "Title,Titolo"), ",", " ", true);
        foreach (var record in records)
        {
          if (string.IsNullOrEmpty(record.Name) && record.fsNode != null)
            record.Name = record.fsNode.Name;
          if (!string.IsNullOrEmpty(record.Title))
            continue;
          if (record.Fields != null && record.Fields.Any())
          {
            var field = record.Fields.FirstOrDefault(r => props.Any(n => string.Equals(r.Key, n, StringComparison.OrdinalIgnoreCase)));
            if (field.Key != null)
              record.Title = field.Value;
          }
          if (!string.IsNullOrEmpty(record.Title))
            continue;
          if (record.resourceData != null)
          {
            foreach (PropertyInfo pi in record.resourceData.GetType().GetProperties().Where(p => props.Any(n => string.Equals(p.Name, n, StringComparison.OrdinalIgnoreCase)) && p.PropertyType.IsAssignableTo(typeof(string))))
            {
              try
              {
                record.Title = pi.GetValue(record.resourceData, null) as string;
                if (!string.IsNullOrEmpty(record.Title))
                  break;
              }
              catch { }
            }
          }
          record.Title = record.Title ?? record.fsNode.Name ?? string.Empty;
        }
        records.ForEach(r => r.Title = r.Title.DefaultIfEmptyTrim(string.Empty));
        if (xHtmlCleaner != null)
        {
          foreach (var record in records)
          {
            try { record.Title = xHtmlCleaner.Parse(record.Title).DefaultIfEmptyTrim(record.Title); }
            catch { }
          }
        }
      }
      catch { }
    }


    //
    // processa i nodi preparati dal sistema ed estrae i testi per le risorse
    //
    public virtual void GetSearchInfoTexts(FS_Operations fsOp, Ikon.Filters.IKGD_HtmlCleaner xHtmlCleaner, List<IKCMS_LuceneRecordData> records)
    {
      try
      {
        //
        var props = Utility.Explode(SearchTextMember.DefaultIfEmptyTrim(IKGD_Config.AppSettings["LuceneIndex_PropsText"] ?? "Title,Text,Abstract,Orario"), ",", " ", true);
        var trimChars = " \r\n\t".ToCharArray();
        var textBlockSeparator = " \n";
        //
        foreach (var record in records)
        {
          if (!string.IsNullOrEmpty(record.Text))
            continue;
          if (record.Fields == null)
            record.Fields = new List<IKCMS_LuceneRecordData.FieldStorage>();
          // integriamo la lista dei fields con i valori delle properties riconosciute
          //if (!record.Fields.Any() && record.resourceData != null)
          if (record.resourceData != null)
          {
            foreach (PropertyInfo pi in record.resourceData.GetType().GetProperties().Where(p => props.Any(n => string.Equals(p.Name, n, StringComparison.OrdinalIgnoreCase)) && p.PropertyType.IsAssignableTo(typeof(string))))
            {
              try
              {
                IKCMS_LuceneRecordData.FieldStorage kv = new IKCMS_LuceneRecordData.FieldStorage(pi.Name, pi.GetValue(record.resourceData, null) as string);
                if (!string.IsNullOrEmpty(kv.Value))
                  record.Fields.Add(kv);
              }
              catch { }
            }
          }
        }
        // processing dei fields (no stream) con il filtro per html e scrematura dei field vuoti
        foreach (var record in records)
        {
          record.Fields = record.Fields.Select(r =>
          {
            try { return xHtmlCleaner == null ? r : new IKCMS_LuceneRecordData.FieldStorage(r.Key, xHtmlCleaner.Parse(r.Value).DefaultIfEmptyTrim(r.Value)); }
            catch { return r; }
          }).Where(r => (r.Value ?? string.Empty).Trim(trimChars).Length > 0).OrderBy(r => props.IndexOfSortable(r.Key)).ToList();
          //record.Streams.RemoveAll(r => (r.Value ?? string.Empty).Trim(trimChars).Length == 0);
          //record.Text = Utility.Implode((record.Fields ?? Enumerable.Empty<IKCMS_LuceneRecordData.FieldStorage>()).Select(r => r.Value).Concat((record.Streams ?? Enumerable.Empty<IKCMS_LuceneRecordData.StreamStorage>()).Select(r => r.Value)), textBlockSeparator, null, true, true);
          record.Text = Utility.Implode((record.Fields ?? Enumerable.Empty<IKCMS_LuceneRecordData.FieldStorage>()).Select(r => r.Value).Concat(new string[] { record.StreamsText }), textBlockSeparator, null, true, true);
        }
      }
      catch { }
    }


    //
    // standard handlers for notification on COW operations
    //


    public virtual void OpHandlerDelete(FS_Operations fsOp, IEnumerable<IKGD_VNODE> vNodes, IEnumerable<IKGD_VDATA> vDatas, IEnumerable<IKGD_INODE> iNodes)
    {
    }


    public virtual void OpHandlerUpdate(FS_Operations fsOp, IKGD_VNODE vNode, IKGD_VDATA vData, IKGD_INODE iNode, IEnumerable<IKGD_PROPERTY> properties, IEnumerable<IKGD_RELATION> relations, bool? isNewResource, bool? isNewSymLink)
    {
    }


    public virtual void OpHandlerPublish(FS_Operations fsOp, IKGD_VNODE vNode, IKGD_VDATA vData, IKGD_INODE iNode, IEnumerable<IKGD_PROPERTY> properties, IEnumerable<IKGD_RELATION> relations)
    {
    }



  }



  //
  // classe base per l'implementazione delle funzionalita' definite nell'interfaccia
  // poi verra' estesa per gestire i settings per la widget window e quindi per i widget customizzati
  //
  [DataContract]
  public abstract class IKGD_WidgetDataImplementation : IKGD_WidgetData_Interface
  {
    //
    // members serialized as defined in interface
    //
    [DataMember]
    object IKGD_WidgetData_Interface.Window { get; set; }
    [DataMember]
    object IKGD_WidgetData_Interface.Config { get; set; }
    [DataMember]
    object IKGD_WidgetData_Interface.Settings { get; set; }
    [DataMember]
    object IKGD_WidgetData_Interface.Data { get; set; }

    public virtual Type[] knownTypes { get { return new Type[] { }; } }

    public virtual void EnsureValued()
    {
      object defVal = Utility.FindPropertyStatic<IKGD_WidgetDataBase>(this.GetType(), "DefaultValue");
      if (defVal != null)
      {
        if ((this as IKGD_WidgetData_Interface).Window == null)
          (this as IKGD_WidgetData_Interface).Window = (defVal as IKGD_WidgetData_Interface).Window;
        if ((this as IKGD_WidgetData_Interface).Config == null)
          (this as IKGD_WidgetData_Interface).Config = (defVal as IKGD_WidgetData_Interface).Config;
        if ((this as IKGD_WidgetData_Interface).Settings == null)
          (this as IKGD_WidgetData_Interface).Settings = (defVal as IKGD_WidgetData_Interface).Settings;
        if ((this as IKGD_WidgetData_Interface).Data == null)
          (this as IKGD_WidgetData_Interface).Data = (defVal as IKGD_WidgetData_Interface).Data;
      }
    }

    //
    // serializzazione e deserializzazione
    //
    public string Serialize()
    {
      // assembly System.ServiceModel.Web // System.Runtime.Serialization.Json
      DataContractJsonSerializer serializer = new DataContractJsonSerializer(this.GetType(), knownTypes);
      using (MemoryStream ms = new MemoryStream())
      {
        serializer.WriteObject(ms, this);
        return Encoding.UTF8.GetString(ms.ToArray());
      }
    }

    public static T DeSerialize<T>(string json) where T : class, IKGD_WidgetData_Interface, new()
    {
      // assembly System.ServiceModel.Web // System.Runtime.Serialization.Json
      DataContractJsonSerializer serializer = new DataContractJsonSerializer(typeof(T), new T().knownTypes);
      using (MemoryStream ms = new MemoryStream(Encoding.UTF8.GetBytes(json)))
      {
        return serializer.ReadObject(ms) as T;
      }
    }

    public static IKGD_WidgetData_Interface DeSerializeByType(string typeName, string json, bool newOnNull) { return DeSerializeByType(Utility.FindType(typeName, typeof(IKGD_WidgetData_Interface)), json, newOnNull); }
    public static IKGD_WidgetData_Interface DeSerializeByType(Type ty, string json, bool newOnNull)
    {
      // assembly System.ServiceModel.Web // System.Runtime.Serialization.Json
      IKGD_WidgetData_Interface result = null;
      object objNew = null;
      if (ty != null)
      {
        try
        {
          ConstructorInfo tyci = ty.GetConstructor(Type.EmptyTypes);
          objNew = tyci.Invoke(null);
          var types = Utility.FindPropertySafe<Type[]>(objNew, "knownTypes");
          DataContractJsonSerializer serializer = new DataContractJsonSerializer(ty, types);
          using (MemoryStream ms = new MemoryStream(Encoding.UTF8.GetBytes(json)))
          {
            result = (IKGD_WidgetData_Interface)serializer.ReadObject(ms);
          }
        }
        catch { }
        if (result == null && newOnNull)
          result = (IKGD_WidgetData_Interface)objNew;
      }
      return result;
    }

  }


  [DataContract]
  public class IKGD_WidgetDataBase : IKGD_WidgetDataImplementation
  {
    //
    // members non serialized as wrappers on serialized interface data
    // da ridefinire nelle classi derivate con il modificatore new
    // e con il tipo di ritorno corretto
    //
    public ClassWindow Window { get { return (this as IKGD_WidgetData_Interface).Window as ClassWindow; } set { (this as IKGD_WidgetData_Interface).Window = value; } }
    public ClassConfig Config { get { return (this as IKGD_WidgetData_Interface).Config as ClassConfig; } set { (this as IKGD_WidgetData_Interface).Config = value; } }
    public ClassSettings Settings { get { return (this as IKGD_WidgetData_Interface).Settings as ClassSettings; } set { (this as IKGD_WidgetData_Interface).Settings = value; } }
    public ClassData Data { get { return (this as IKGD_WidgetData_Interface).Data as ClassData; } set { (this as IKGD_WidgetData_Interface).Data = value; } }
    //
    // templates da ridefinire nelle classi derivate
    //
    //public override Type[] knownTypes { get { return new Type[] { typeof(ClassWindow), typeof(ClassConfig), typeof(ClassSettings), typeof(ClassData) }; } }
    //public static IKGD_Widget_XYZ DefaultValue { get { return new IKGD_Widget_XYZ { Window = ClassWindow.DefaultValue, Config = ClassConfig.DefaultValue, Settings = ClassSettings.DefaultValue, Data = ClassData.DefaultValue }; } }
    //
    // per serializzare le classi derivate
    //
    public override Type[] knownTypes { get { return new Type[] { typeof(ClassWindow), typeof(ClassConfig), typeof(ClassSettings), typeof(ClassData) }; } }


    //
    // classe inline per la definizione dei parametri della window dei widget
    // da ridefinire nelle classi derivate con il modificatore new
    //
    [DataContract]
    public class ClassWindow
    {
      //[DataMember]
      //public bool Minimized { get; set; }
      [DataMember]
      public bool FullScreen { get; set; }
      [DataMember]
      public bool Minimizable { get; set; }
      [DataMember]
      public bool Closable { get; set; }
      [DataMember]
      public bool Draggable { get; set; }
      [DataMember]
      public bool HasSettings { get; set; }
      [DataMember]
      public bool Active { get; set; }
      //
      // default values for the window settings
      public static ClassWindow DefaultValue { get { return new ClassWindow { Minimizable = true, Closable = true, Draggable = true, HasSettings = true, FullScreen = true, Active = true }; } }
    }
    //
    [DataContract]
    public class ClassConfig { public static ClassConfig DefaultValue { get { return new ClassConfig { }; } } }
    //
    [DataContract]
    public class ClassSettings { public static ClassSettings DefaultValue { get { return new ClassSettings { }; } } }
    //
    [DataContract]
    public class ClassData { public static ClassData DefaultValue { get { return new ClassData { }; } } }
  }


}
