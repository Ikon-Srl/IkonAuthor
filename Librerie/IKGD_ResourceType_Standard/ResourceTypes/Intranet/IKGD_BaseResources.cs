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


namespace Ikon.IKGD.Library.Resources
{


  //
  // Collection
  //
  [Description("| Collezione di widget")]
  public class IKGD_Folder_Collection : IKGD_WidgetBase, IKGD_FolderCollection_Interface, IKGD_Folder_Interface, IKCMS_Folder_Interface, IKCMS_HasResourceData_Interface
  {
    public new IKGD_WidgetData_Collection WidgetSettings { get { return (this as IKGD_Widget_Interface).WidgetSettings as IKGD_WidgetData_Collection; } set { (this as IKGD_Widget_Interface).WidgetSettings = value; } }
    public override Type WidgetSettingsType { get { return typeof(IKGD_WidgetData_Collection); } }
    public override string IconEditor { get { return "VFS.Collezione.gif"; } }

    public override bool HasInode { get { return false; } }
    public override bool IsUnstructured { get { return false; } }
    public override bool IsFolder { get { return true; } }
    public override bool IsCollection { get { return true; } }

    //
    // WidgetSettings
    //
    [DataContract]
    public class IKGD_WidgetData_Collection : IKGD_WidgetDataBase
    {
      public new ClassConfig_Collection Config { get { return (this as IKGD_WidgetData_Interface).Config as ClassConfig_Collection; } set { (this as IKGD_WidgetData_Interface).Config = value; } }
      //
      public override Type[] knownTypes { get { return new Type[] { typeof(ClassWindow), typeof(ClassConfig_Collection), typeof(ClassSettings), typeof(ClassData) }; } }
      public static IKGD_WidgetData_Collection DefaultValue { get { return new IKGD_WidgetData_Collection { Window = ClassWindow.DefaultValue, Config = ClassConfig_Collection.DefaultValue, Settings = ClassSettings.DefaultValue, Data = ClassData.DefaultValue }; } }
      //
      // classi inline per la definizione dei parametri della Collection
      //
      [DataContract]
      public class ClassConfig_Collection
      {
        [DataMember]
        public int Layout { get; set; }
        // per default uso il layout = 5 che e' quello a 3 colonne
        public static ClassConfig_Collection DefaultValue { get { return new ClassConfig_Collection { Layout = 5 }; } }
      }
    }
    //
  }


  //
  // classe base astratta per tutti le risorse derivate dal tipo news
  // implementa il supporto comune per l'indicizzazione
  //
  public abstract class IKGD_ResourceType_NewsBase : IKGD_ResourceTypeBase, IKGD_ResourceTypeCollectable_Interface, IKCMS_PageWithOutFolder_Interface, IKCMS_HasExternalTemplate_Interface, IKCMS_Aggregable_Interface, IKCMS_IsIndexable_Interface
  {
    public override string IconEditor { get { return "ResourceType.News.gif"; } }
  }


}