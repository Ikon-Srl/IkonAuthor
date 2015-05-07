/*
 * 
 * Intranet Ikon
 * 
 * Copyright (C) 2009 Ikon Srl
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


namespace Ikon.IKGD.Library.Resources
{

  //
  // interfaces di supporto per la definizione delle caratteristiche dei widget/resources/pages associati al CMS
  //

  //
  // interface per tutto quello cha sara' visibile al CMS
  //
  public interface IKCMS_Base_Interface { }
  public interface IKCMS_Folder_Interface : IKCMS_Base_Interface { }
  public interface IKCMS_Resource_Interface : IKCMS_Base_Interface { }
  public interface IKCMS_ResourceUnStructured_Interface : IKCMS_Resource_Interface { }
  public interface IKCMS_ResourceStructured_Interface : IKCMS_Resource_Interface { }
  public interface IKCMS_HasResourceData_Interface : IKCMS_Base_Interface { }
  public interface IKCMS_HasRelations_Interface : IKCMS_Base_Interface { }
  public interface IKCMS_HasProperties_Interface : IKCMS_Base_Interface { }
  //public interface IKCMS_HasPropertiesKVT_Interface; viene definito nel seguito
  public interface IKCMS_HasPageEditor_Interface : IKCMS_Base_Interface { }

  public interface IKCMS_RootInternal_Interface : IKCMS_Folder_Interface { }
  public interface IKCMS_ArchiveRoot_Interface : IKCMS_RootInternal_Interface { }
  public interface IKCMS_Root_Interface : IKCMS_RootInternal_Interface { }

  public interface IKCMS_ResourceWithOutUrl_Interface { }
  public interface IKCMS_ResourceWithUrl_Interface { }
  public interface IKCMS_PageBase_Interface : IKCMS_Base_Interface, IKCMS_ResourceStructured_Interface, IKCMS_ResourceWithUrl_Interface { }  // non e' necessariamente un folder (es. news)
  public interface IKCMS_PageWithOutFolder_Interface : IKCMS_PageBase_Interface { }
  public interface IKCMS_PageTemplate_Interface : IKCMS_PageBase_Interface, IKCMS_Folder_Interface { }
  public interface IKCMS_Page_Interface : IKCMS_PageBase_Interface, IKCMS_Folder_Interface, IKCMS_HasRelations_Interface, IKCMS_HasProperties_Interface { }
  public interface IKCMS_PageWithPageEditor_Interface : IKCMS_Page_Interface, IKCMS_HasPageEditor_Interface { }
  public interface IKCMS_PageWithoutPageEditor_Interface : IKCMS_Page_Interface { }
  public interface IKCMS_PageNoCMS_Interface : IKCMS_PageBase_Interface { }
  public interface IKCMS_ResourceWithViewer_Interface { }

  public interface IKCMS_Widget_Interface : IKCMS_ResourceStructured_Interface { }
  public interface IKCMS_WidgetCompound_Interface : IKCMS_Widget_Interface { }  // trovare come estenderlo
  public interface IKCMS_WidgetWithRelations_Interface : IKCMS_Widget_Interface, IKCMS_HasRelations_Interface { }
  public interface IKCMS_WidgetWithProperties_Interface : IKCMS_Widget_Interface, IKCMS_HasProperties_Interface { }

  public interface IKCMS_BrickBase_Interface : IKCMS_Widget_Interface { }
  public interface IKCMS_BrickCollectable_Interface : IKCMS_BrickBase_Interface { }
  public interface IKCMS_BrickCollector_Interface : IKCMS_BrickBase_Interface { }
  public interface IKCMS_BrickWidget_Interface : IKCMS_BrickBase_Interface { }
  public interface IKCMS_BrickTeaser_Interface : IKCMS_BrickBase_Interface, IKCMS_BrickCollectable_Interface { }
  public interface IKCMS_BrickMultimedia_Interface : IKCMS_BrickBase_Interface { }
  public interface IKCMS_BrickWithPlaceholder_Interface : IKCMS_BrickBase_Interface { }
  public interface IKCMS_BrickImage_Interface : IKCMS_BrickMultimedia_Interface { }

  public interface IKCMS_IsExternalTemplate_Interface : IKCMS_PageTemplate_Interface { }  // potrebbe anche avere requisiti minori
  public interface IKCMS_HasExternalTemplate_Interface : IKCMS_PageBase_Interface { }

  public interface IKCMS_AggregatorByRelations_Interface : IKCMS_PageBase_Interface { }
  public interface IKCMS_Aggregable_Interface : IKCMS_PageBase_Interface { }
  public interface IKCMS_BrowsableIndexable_Interface : IKCMS_PageBase_Interface { }
  public interface IKCMS_BrowsableModule_Interface : IKCMS_PageBase_Interface { }

  public interface IKCMS_IsIndexable_Interface { }



  //
  // interface base per la definizione dei widget
  //
  // i WidgetSettings vengono definiti come oggetti generici nell'interfaccia, poi nelle classi derivate
  // saranno definiti come istanze specifiche dell'interfaccia e sovrascritti dai metodi
  // della classe base stessa usando il modificatore new nella ridefinizione di WidgetSettings
  // questa volta con il tipo di ritorno corretto
  //
  public class IKCMS_WidgetBase : IKGD_ResourceTypeBase, IKGD_ResourceType_Interface, IKGD_Widget_Interface, IKCMS_Widget_Interface
  {
    public override bool HasInode { get { return false; } }  // generalmente i widget non hanno INODE
    public override bool IsWidget { get { return true; } }  // per evitare reflection
    public override bool IsWidgetSingleton { get { return true; } }  // per evitare reflection
    public override bool IsWidgetBrowse { get { return false; } }  // per evitare reflection
    //
    IKGD_WidgetDataBase IKGD_Widget_Interface.WidgetSettings { get; set; }
    public IKGD_WidgetDataBase WidgetSettings { get { return (this as IKGD_Widget_Interface).WidgetSettings as IKGD_WidgetDataBase; } set { (this as IKGD_Widget_Interface).WidgetSettings = value; } }
    public virtual Type WidgetSettingsType { get { return typeof(IKGD_WidgetDataBase); } }

    public IKCMS_WidgetBase()
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
  // classe base per i widget CMS con dati serializzati con struttura semplificata
  // utilizzabili solo per il CMS e non come widget per il portal
  //
  public enum IKCMS_vDataSerializationMode { MainSourceIsJson_OnSettings, MainSourceIsXml_OnData }
  public enum IKCMS_vDataDataStorageMode { Binary, String, Xml }
  //
  public interface IKCMS_HasSerializationCMS_Interface : IKCMS_Base_Interface
  {
    object ResourceSettingsObject { get; set; }
    Type ResourceSettingsType { get; }
    string ResourceSettings_Serialized { get; }
    //XElement ResourceSettings_SerializedXml { get; }
    object ResourceSettingsObject_DeserializeAuto(IKGD_VDATA vData);
    void ResourceSettings_EnsureValued();
    void ResourceSettings_Constructor(string jsonString);
    void ResourceSettings_Constructor(IKGD_VDATA vData);
    //
    //bool AutoSyncJsonAndXml { get; }
    IKCMS_vDataSerializationMode vDataSerializationMode { get; }
    IKCMS_vDataDataStorageMode vDataDataStorageMode { get; }
    //
    bool Check_IKGD_VDATA_IsTainted(string InitialSerializedSettings);
    bool Update_IKGD_VDATA_Serialized(IKGD_VDATA vData, bool force);
  }
  public interface IKCMS_vDataSerializationCMS_Interface<T> : IKCMS_HasSerializationCMS_Interface
  {
    T ResourceSettings { get; set; }
    T ResourceSettingsDefault { get; }
    T ResourceSettings_DeSerialize(string jsonString, bool defaultOnNull);
  }
  //
  public class IKCMS_ResourceBaseCMS<T> : IKGD_ResourceTypeBase, IKGD_ResourceType_Interface, IKCMS_vDataSerializationCMS_Interface<T> where T : class, new()
  {
    public object ResourceSettingsObject { get; set; }
    public T ResourceSettings { get { return (T)ResourceSettingsObject; } set { ResourceSettingsObject = value; } }
    public Type ResourceSettingsType { get { return typeof(T); } }
    public T ResourceSettingsDefault { get { return Utility.FindPropertyStatic<T>(ResourceSettingsType, "DefaultValue"); } }
    //
    // modalita' di gestione della serializzazione dei dati tra .settings e .data
    //
    //public virtual bool AutoSyncJsonAndXml { get { return false; } }  // autosincronizzazione dei contenuti serializzati in vData.settings in vData.data come xml
    public virtual IKCMS_vDataSerializationMode vDataSerializationMode { get { return IKCMS_vDataSerializationMode.MainSourceIsJson_OnSettings; } }
    public virtual IKCMS_vDataDataStorageMode vDataDataStorageMode { get { return IKCMS_vDataDataStorageMode.Xml; } }


    //
    public IKCMS_ResourceBaseCMS() { ResourceSettings_EnsureValued(); }
    // constructor for custom uses
    public IKCMS_ResourceBaseCMS(string jsonString) { ResourceSettings_Constructor(jsonString); }
    public IKCMS_ResourceBaseCMS(IKGD_VDATA vData) { ResourceSettings_Constructor(vData); }


    //
    // serializzazione e deserializzazione
    //
    public string ResourceSettings_Serialized { get { return IKGD_Serialization.SerializeToJSON(ResourceSettings); } }
    //public XElement ResourceSettings_SerializedXml { get { return IKGD_Serialization.SerializeToXml(ResourceSettings); } }
    //
    public void ResourceSettings_EnsureValued() { ResourceSettings = ResourceSettings ?? ResourceSettingsDefault; }
    public void ResourceSettings_Constructor(string jsonString) { ResourceSettings = IKGD_Serialization.DeSerializeJSON<T>(jsonString, null); }
    public void ResourceSettings_Constructor(IKGD_VDATA vData) { ResourceSettings = (T)ResourceSettingsObject_DeserializeAuto(vData); }
    public object ResourceSettingsObject_DeserializeAuto(IKGD_VDATA vData)
    {
      object result = null;
      try
      {
        if (vDataSerializationMode == IKCMS_vDataSerializationMode.MainSourceIsJson_OnSettings)
        {
          result = IKGD_Serialization.DeSerializeJSON<T>(vData.settings, "DefaultValue");
          return result;
        }
        //else if (vDataSerializationMode == IKCMS_vDataSerializationMode.MainSourceIsXml_OnData && vDataDataStorageMode == IKCMS_vDataDataStorageMode.Xml)
        //{
        //  result = IKGD_Serialization.DeSerializeXml<T>(vData.dataAsXml, "DefaultValue");
        //}
      }
      catch { }
      if (result == null)
        result = Utility.FindPropertyStatic<object>(ResourceSettingsType, "DefaultValue");
      return result;
    }
    //
    public T ResourceSettings_DeSerialize(string jsonString) { return IKGD_Serialization.DeSerializeJSON<T>(jsonString, null); }
    public T ResourceSettings_DeSerialize(string jsonString, bool defaultOnNull) { return IKGD_Serialization.DeSerializeJSON<T>(jsonString, defaultOnNull ? "DefaultValue" : null); }
    //
    public static T ResourceSettings_DeSerializeT(string jsonString) { return IKGD_Serialization.DeSerializeJSON<T>(jsonString, "DefaultValue"); }
    //public static T ResourceSettings_DeSerializeXmlT(XElement xData) { return IKGD_Serialization.DeSerializeXml<T>(xData, "DefaultValue"); }


    //
    // funzione ausiliaria per l'update di IKGD_VDATA
    //
    public bool Check_IKGD_VDATA_IsTainted(string InitialSerializedSettings)
    {
      try { return ResourceSettings_Serialized != InitialSerializedSettings; }
      catch { return true; }
    }


    // nuovo formalismo per la gestione dei settings tipo CMS (v. UC_EditorModule_PropertyGrid)
    public bool Update_IKGD_VDATA_Serialized(IKGD_VDATA vData, bool force)
    {
      try
      {
        string settings = ResourceSettings_Serialized;
        //XElement xData = ResourceSettings_SerializedXml;
        force |= (vDataSerializationMode == IKCMS_vDataSerializationMode.MainSourceIsJson_OnSettings) && (vData.settings != settings);
        //force |= (vDataSerializationMode == IKCMS_vDataSerializationMode.MainSourceIsXml_OnData) && (vData.dataAsXml != xData);
        if (force)
        {
          if (vDataSerializationMode == IKCMS_vDataSerializationMode.MainSourceIsJson_OnSettings)
          {
            vData.settings = settings;
            //if (AutoSyncJsonAndXml)
            //{
            //  vData.dataAsXml = xData;
            //}
            return true;
          }
          //else if (vDataSerializationMode == IKCMS_vDataSerializationMode.MainSourceIsXml_OnData)
          //{
          //  vData.dataAsXml = xData;
          //  if (AutoSyncJsonAndXml)
          //    vData.settings = settings;
          //  return true;
          //}
        }
      }
      catch { }
      return false;
    }


    public bool Update_IKGD_VDATA_SerializeIsTainted(IKGD_VDATA vData)
    {
      try
      {
        string settings = ResourceSettings_Serialized;
        //XElement xData = ResourceSettings_SerializedXml;
        bool tainted = false;
        tainted |= (vDataSerializationMode == IKCMS_vDataSerializationMode.MainSourceIsJson_OnSettings) && (vData.settings != settings);
        //tainted |= (vDataSerializationMode == IKCMS_vDataSerializationMode.MainSourceIsXml_OnData) && (vData.dataAsXml != xData);
        return tainted;
      }
      catch { }
      return true;
    }

  }


  public interface IKCMS_HasDeserializeOnVFS_Interface { }

  public interface IKCMS_HasPropertiesKVT_Interface : IKCMS_HasSerializationCMS_Interface, IKCMS_Base_Interface
  {
    Ikon.IKCMS.KeyValueObjectTree ResourceSettingsKVT { get; }
  }


  public interface IKCMS_HasPropertiesLanguageKVT_Interface : IKCMS_HasPropertiesKVT_Interface
  {
    Ikon.IKCMS.KeyValueObjectTree ResourceSettingsLanguageKVT(params string[] keys);
    Ikon.IKCMS.KeyValueObjectTree ResourceSettingsNoLanguageKVT(params string[] keys);
    IEnumerable<KeyValueObjectTree> ResourceSettingsLanguageKVTs(params string[] keys);
    IEnumerable<KeyValueObjectTree> ResourceSettingsNoLanguageKVTs(params string[] keys);
    List<string> ResourceSettingsLanguageKVTss(params string[] keys);
    List<string> ResourceSettingsNoLanguageKVTss(params string[] keys);
  }


  public class MultiStreamInfo4Settings
  {
    //
    public string Source { get; set; }
    public string Key { get; set; }
    public string StreamName { get { return string.Format("{0}|{1}", Source, Key); } }
    //
    public int Size { get; set; }
    public string Mime { get; set; }
    //
    public string Orig { get; set; }
    public string Ext { get; set; }
    public string ExtMode { get; set; }
    //
    public System.Drawing.Point Dimensions { get; set; }
    public string Description { get; set; }
    //
    public string OrigNoPath { get { return string.IsNullOrEmpty(Orig) ? Orig.NullIfEmpty() : Utility.PathGetFileNameSanitized(Orig); } }
    public string ExtNoPoint { get { return (Ext ?? string.Empty).Trim('.', ' ').NullIfEmpty(); } }
    public string ExtWithPoint { get { return ("." + (ExtNoPoint ?? string.Empty)).TrimEnd('.', ' ').NullIfEmpty(); } }
    public string ExtNoPointNN { get { return (Ext ?? string.Empty).Trim('.', ' ').DefaultIfEmpty("null"); } }
    public string ExtWithPointNN { get { return "." + ExtNoPointNN; } }
    //

    public override string ToString()
    {
      return string.Format("Source={0}, Key={1}, Mime={2}, Length={3} {4} Orig={5}", Source, Key, Mime, Size, ExtMode == null ? null : "Mode={0}:{1}".FormatString(ExtMode, Description), OrigNoPath);
    }

  }


  public static class IKCMS_RegisteredTypes
  {
    public static List<Type> Types_IKCMS_Base_Interface { get; set; }
    public static List<Type> Types_IKCMS_Folder_Interface { get; set; }
    public static List<Type> Types_IKCMS_Resource_Interface { get; set; }
    public static List<Type> Types_IKCMS_ResourceUnStructured_Interface { get; set; }
    public static List<Type> Types_IKCMS_ResourceWithUrl_Interface { get; set; }
    public static List<Type> Types_IKCMS_PageBase_Interface { get; set; }
    public static List<Type> Types_IKCMS_Page_Interface { get; set; }
    public static List<Type> Types_IKCMS_BrickBase_Interface { get; set; }
    public static List<Type> Types_IKCMS_BrickCollectable_Interface { get; set; }
    public static List<Type> Types_IKCMS_BrickCollector_Interface { get; set; }
    public static List<Type> Types_IKCMS_BrickWidget_Interface { get; set; }
    public static List<Type> Types_IKCMS_BrickWithPlaceholder_Interface { get; set; }
    public static List<Type> Types_IKCMS_Widget_Interface { get; set; }
    public static List<Type> Types_IKCMS_BrowsableIndexable_Interface { get; set; }
    public static List<Type> Types_IKCMS_BrowsableModule_Interface { get; set; }
    public static List<Type> Types_IKCMS_HasSerializationCMS_Interface { get; set; }
    public static List<Type> Types_IKCMS_HasDeserializeOnVFS_Interface { get; set; }
    public static List<Type> Types_IKCMS_ResourceWithViewer_Interface { get; set; }

    //
    // solo per la intranet
    public static List<Type> Types_IKGD_ResourceType_Interface { get; set; }
    public static List<Type> Types_IKGD_ResourceTypeCollectable_Interface { get; set; }
    public static List<Type> Types_IKGD_Widget_Interface { get; set; }
    public static List<Type> Types_IKGD_WidgetSingleton_Interface { get; set; }
    public static List<Type> Types_IKGD_WidgetBrowse_Interface { get; set; }
    //

    static IKCMS_RegisteredTypes()
    {
      Types_IKCMS_Base_Interface = Utility.FindTypesWithInterfaces(typeof(IKCMS_Base_Interface)).ToList();
      Types_IKCMS_Folder_Interface = Types_IKCMS_Base_Interface.Where(t => Utility.HasInterface(t, typeof(IKCMS_Folder_Interface))).ToList();
      Types_IKCMS_Resource_Interface = Types_IKCMS_Base_Interface.Where(t => Utility.HasInterface(t, typeof(IKCMS_Resource_Interface))).ToList();
      Types_IKCMS_ResourceUnStructured_Interface = Types_IKCMS_Base_Interface.Where(t => Utility.HasInterface(t, typeof(IKCMS_ResourceUnStructured_Interface))).ToList();
      Types_IKCMS_ResourceWithUrl_Interface = Types_IKCMS_Base_Interface.Where(t => Utility.HasInterface(t, typeof(IKCMS_ResourceWithUrl_Interface)) && !Utility.HasInterface(t, typeof(IKCMS_ResourceWithOutUrl_Interface))).ToList();
      Types_IKCMS_PageBase_Interface = Types_IKCMS_Base_Interface.Where(t => Utility.HasInterface(t, typeof(IKCMS_PageBase_Interface))).ToList();
      Types_IKCMS_Page_Interface = Types_IKCMS_Base_Interface.Where(t => Utility.HasInterface(t, typeof(IKCMS_Page_Interface))).ToList();
      Types_IKCMS_BrickBase_Interface = Types_IKCMS_Base_Interface.Where(t => Utility.HasInterface(t, typeof(IKCMS_BrickBase_Interface))).ToList();
      Types_IKCMS_BrickCollectable_Interface = Types_IKCMS_Base_Interface.Where(t => Utility.HasInterface(t, typeof(IKCMS_BrickCollectable_Interface))).ToList();
      Types_IKCMS_BrickCollector_Interface = Types_IKCMS_Base_Interface.Where(t => Utility.HasInterface(t, typeof(IKCMS_BrickCollector_Interface))).ToList();
      Types_IKCMS_BrickWidget_Interface = Types_IKCMS_Base_Interface.Where(t => Utility.HasInterface(t, typeof(IKCMS_BrickWidget_Interface))).ToList();
      Types_IKCMS_BrickWithPlaceholder_Interface = Types_IKCMS_Base_Interface.Where(t => Utility.HasInterface(t, typeof(IKCMS_BrickWithPlaceholder_Interface))).ToList();
      Types_IKCMS_Widget_Interface = Types_IKCMS_Base_Interface.Where(t => Utility.HasInterface(t, typeof(IKCMS_Widget_Interface))).ToList();
      Types_IKCMS_BrowsableIndexable_Interface = Types_IKCMS_Base_Interface.Where(t => Utility.HasInterface(t, typeof(IKCMS_BrowsableIndexable_Interface))).ToList();
      Types_IKCMS_BrowsableModule_Interface = Types_IKCMS_Base_Interface.Where(t => Utility.HasInterface(t, typeof(IKCMS_BrowsableModule_Interface))).ToList();
      Types_IKCMS_HasSerializationCMS_Interface = Types_IKCMS_Base_Interface.Where(t => Utility.HasInterface(t, typeof(IKCMS_HasSerializationCMS_Interface))).ToList();
      Types_IKCMS_HasDeserializeOnVFS_Interface = Types_IKCMS_Base_Interface.Where(t => Utility.HasInterface(t, typeof(IKCMS_HasDeserializeOnVFS_Interface))).ToList();
      //
      Types_IKCMS_ResourceWithViewer_Interface = Types_IKCMS_Base_Interface.Where(t => !t.IsAbstract && Utility.HasInterface(t, typeof(IKCMS_ResourceWithViewer_Interface))).ToList();
      //Types_IKCMS_ResourceWithViewer_Interface = Types_IKCMS_PageBase_Interface.Where(t => !t.IsAbstract).Intersect(Types_IKCMS_BrickBase_Interface).ToList();
      //
      Types_IKGD_ResourceType_Interface = Utility.FindTypesWithInterfaces(typeof(IKGD_ResourceType_Interface)).ToList();
      Types_IKGD_ResourceTypeCollectable_Interface = Utility.FindTypesWithInterfaces(typeof(IKGD_ResourceTypeCollectable_Interface)).ToList();
      Types_IKGD_Widget_Interface = Types_IKGD_ResourceType_Interface.Where(t => Utility.HasInterface(t, typeof(IKGD_Widget_Interface))).ToList();
      Types_IKGD_WidgetSingleton_Interface = Types_IKGD_ResourceType_Interface.Where(t => Utility.HasInterface(t, typeof(IKGD_WidgetSingleton_Interface))).ToList();
      Types_IKGD_WidgetBrowse_Interface = Types_IKGD_ResourceType_Interface.Where(t => Utility.HasInterface(t, typeof(IKGD_WidgetBrowse_Interface))).ToList();
      //
    }


    //
    // costruzione di un oggetto completo di tipo manager_type con deserializzazione dei dati
    //
    public static IKCMS_HasSerializationCMS_Interface Deserialize_IKCMS_ResourceVFS(FS_Operations.FS_NodeInfo_Interface fsNode)
    {
      if (fsNode != null)
        return Deserialize_IKCMS_ResourceVFS(fsNode.vData);
      return null;
    }
    public static IKCMS_HasSerializationCMS_Interface Deserialize_IKCMS_ResourceVFS(IKGD_VDATA vData)
    {
      try
      {
        Type resourceType = Types_IKCMS_HasSerializationCMS_Interface.FirstOrDefault(t => t.Name == vData.manager_type);
        IKCMS_HasSerializationCMS_Interface resource = (IKCMS_HasSerializationCMS_Interface)Activator.CreateInstance(resourceType);
        //
        //resource.ResourceSettings_EnsureValued();
        resource.ResourceSettings_Constructor(vData);
        //
        return resource;
      }
      catch { }
      return null;
    }


    // da utilizzare per deserializzare i settings di oggetti IKCMS o IKGD
    public static object Deserialize_ResourceVFS(IKGD_VDATA vData)
    {
      object resourceData = null;
      try
      {
        Type resourceType = Types_IKCMS_HasSerializationCMS_Interface.FirstOrDefault(t => t.Name == vData.manager_type);
        if (resourceType != null)
        {
          // oggetto IKCMS
          IKCMS_HasSerializationCMS_Interface resource = (IKCMS_HasSerializationCMS_Interface)Activator.CreateInstance(resourceType);
          resource.ResourceSettings_Constructor(vData);
          resourceData = resource.ResourceSettingsObject;
        }
        else
        {
          // oggetto IKGD
          IKGD_WidgetData_Interface resource = IKGD_WidgetDataImplementation.DeSerializeByType(vData.manager_type, vData.settings, false);
          resourceData = resource.Config;
        }
      }
      catch { }
      return resourceData;
    }


    public static List<KeyValuePair<string, string>> GetTypesWithInterfaceForDDL(Type interfaceType) { return GetTypesWithInterfaceForDDL(interfaceType, false); }
    public static List<KeyValuePair<string, string>> GetTypesWithInterfaceForDDL(Type interfaceType, bool typesWithDescriptionOnly)
    {
      List<KeyValuePair<string, string>> items = new List<KeyValuePair<string, string>>();
      foreach (Type ty in Utility.FindTypesWithInterfaces(interfaceType).Where(t => !t.IsAbstract))
      {
        string desc = null;
        try { desc = ((DescriptionAttribute)ty.GetCustomAttributes(typeof(DescriptionAttribute), false).FirstOrDefault()).Description.NullIfEmpty(); }
        catch { }
        if (typesWithDescriptionOnly && desc.IsNullOrWhiteSpace())
          continue;
        items.Add(new KeyValuePair<string, string>(ty.Name, desc ?? ty.Name));
      }
      return items.OrderBy(r => r.Value).ToList();
    }

  }




  //
  // interfaces specifiche per i resource settings 
  //


  public interface WidgetSettingsType_InternalUrl_Interface
  {
    int? Link_sNode { get; set; }
    int? Link_rNode { get; set; }
  }


  public interface WidgetSettingsType_ExternalUrl_Interface
  {
    string LinkUrl { get; set; }
  }


  public interface WidgetSettingsType_Url_Interface : WidgetSettingsType_InternalUrl_Interface, WidgetSettingsType_ExternalUrl_Interface
  {
  }


  public interface WidgetSettingsType_FullUrl_Interface : WidgetSettingsType_Url_Interface
  {
    string LinkTarget { get; set; }
    string LinkQueryString { get; set; }
  }


  // interfaces per il supporto dei links salvati in TreeKVT (links multilingua o links multipli)
  public interface WidgetSettingsType_FullUrlOnKVT_Interface : WidgetSettingsType_FullUrl_Interface { }
  public interface WidgetSettingsType_FullUrlOnLanguageKVT_Interface : WidgetSettingsType_FullUrlOnKVT_Interface { }


  public class IKCMS_FullUrlStorage : WidgetSettingsType_FullUrl_Interface
  {
    public int? Link_sNode { get; set; }
    public int? Link_rNode { get; set; }
    public string LinkUrl { get; set; }
    public string LinkTarget { get; set; }
    public string LinkQueryString { get; set; }
    //
    public bool HasLinkTarget { get { return !string.IsNullOrEmpty(LinkTarget) && IKGD_SiteMode.IsTargetSupported; } }
    public string LinkTargetFullString { get { return (string.IsNullOrEmpty(LinkTarget) || !IKGD_SiteMode.IsTargetSupported) ? string.Empty : string.Format("target='{0}'", LinkTarget); } }
    //
  }


  public interface WidgetSettingsType_HasTemplateSelector_Interface
  {
    string TemplateType { get; set; }
  }


  public interface WidgetSettingsType_HasKVTO_Interface
  {
    KeyValueObjectTree Values { get; set; }
  }


}
