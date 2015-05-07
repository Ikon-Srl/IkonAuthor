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
  // classe base per i browsing widget
  //
  public class IKGD_WidgetBaseBrowse : IKGD_WidgetBase, IKGD_WidgetBrowse_Interface, IKCMS_ArchiveRoot_Interface
  {
    public new IKGD_WidgetData_Browser WidgetSettings { get { return (this as IKGD_Widget_Interface).WidgetSettings as IKGD_WidgetData_Browser; } set { (this as IKGD_Widget_Interface).WidgetSettings = value; } }
    public override Type WidgetSettingsType { get { return typeof(IKGD_WidgetData_Browser); } }
    public override string IconEditor { get { return "VFS.WidgetFolder.gif"; } }
    //
    public override bool HasInode { get { return false; } }
    public override bool IsUnstructured { get { return false; } }
    public override bool IsWidget { get { return true; } }
    public override bool IsFolder { get { return true; } }
    public override bool IsWidgetSingleton { get { return false; } }  // per evitare reflection
    public override bool IsWidgetBrowse { get { return true; } }  // per evitare reflection
    //
    // WidgetSettings
    //
    [DataContract]
    public class IKGD_WidgetData_Browser : IKGD_WidgetDataBase
    {
      public new ClassConfig_Browser Config { get { return (this as IKGD_WidgetData_Interface).Config as ClassConfig_Browser; } set { (this as IKGD_WidgetData_Interface).Config = value; } }
      public new ClassSettings_Browser Settings { get { return (this as IKGD_WidgetData_Interface).Settings as ClassSettings_Browser; } set { (this as IKGD_WidgetData_Interface).Settings = value; } }
      //
      public override Type[] knownTypes { get { return new Type[] { typeof(ClassWindow), typeof(ClassConfig_Browser), typeof(ClassSettings_Browser), typeof(ClassData) }; } }
      public static IKGD_WidgetData_Browser DefaultValue { get { return new IKGD_WidgetData_Browser { Window = ClassWindow.DefaultValue, Config = ClassConfig_Browser.DefaultValue, Settings = ClassSettings_Browser.DefaultValue, Data = ClassData.DefaultValue }; } }
      //
      // classi inline per la definizione dei parametri del widget
      //
      [DataContract]
      public class ClassConfig_Browser
      {
        public enum DisplayModes
        {
          [DescriptionAttribute("Visualizzazione normale")]
          Normal,
          [DescriptionAttribute("Visualizzazione con foto")]
          WithThumbnail
        };

        //[DataMember]
        //public string UrlFS { get; set; }
        [DataMember]
        public DisplayModes DisplayMode { get; set; }
        //
        public static ClassConfig_Browser DefaultValue { get { return new ClassConfig_Browser { DisplayMode = DisplayModes.Normal }; } }
      }
      //
      [DataContract]
      public class ClassSettings_Browser
      {
        [DataMember]
        public int ItemsCount { get; set; }
        //
        public static ClassSettings_Browser DefaultValue { get { return new ClassSettings_Browser { ItemsCount = 3 }; } }
      }
    }
    //
  }



  //
  // Widget News
  //
  public class IKGD_Widget_News : IKGD_WidgetBaseBrowse
  {
  }


  //
  // Widget FAQ
  //
  public class IKGD_Widget_FAQ : IKGD_WidgetBaseBrowse
  {
  }


  //
  // Widget Circolari
  //
  public class IKGD_Widget_Circolari : IKGD_WidgetBaseBrowse
  {
  }


  //
  // Widget Cosa Fare Se
  //
  public class IKGD_Widget_CosaFareSe : IKGD_WidgetBaseBrowse
  {
  }


  //
  // Widget Documentazione di supporto
  //
  public class IKGD_Widget_Documentazione : IKGD_WidgetBaseBrowse
  {
  }


  //
  // Widget Modulistica
  //
  public class IKGD_Widget_Modulistica : IKGD_WidgetBaseBrowse
  {
  }


  //
  // Widget Normativa
  //
  public class IKGD_Widget_Normativa : IKGD_WidgetBaseBrowse
  {
  }


  //
  // Widget Calendar
  //
  public class IKGD_Widget_Calendar : IKGD_WidgetBaseBrowse
  {
  }


  //
  // Widget InEvidenza
  //
  public class IKGD_Widget_InEvidenza : IKGD_WidgetBaseBrowse
  {
  }


  //
  // Widget Links
  //
  public class IKGD_Widget_Links : IKGD_WidgetBaseBrowse
  {
  }


  //
  // Widget Contatti
  //
  public class IKGD_Widget_Contatti : IKGD_WidgetBaseBrowse
  {
    public override bool HasInode { get { return true; } }
  }


  //
  // Widget PhotoGallery
  //
  public class IKGD_Widget_PhotoGallery : IKGD_WidgetBaseBrowse, IKCMS_HasResourceData_Interface
  {
    public new IKGD_WidgetData_PhotoGallery WidgetSettings { get { return (this as IKGD_Widget_Interface).WidgetSettings as IKGD_WidgetData_PhotoGallery; } set { (this as IKGD_Widget_Interface).WidgetSettings = value; } }
    public override Type WidgetSettingsType { get { return typeof(IKGD_WidgetData_PhotoGallery); } }
    public override string IconEditor { get { return "VFS.WidgetFolder.gif"; } }

    //
    // WidgetSettings
    //
    [DataContract]
    public class IKGD_WidgetData_PhotoGallery : IKGD_WidgetDataBase
    {
      public new ClassConfig Config { get { return (this as IKGD_WidgetData_Interface).Config as ClassConfig; } set { (this as IKGD_WidgetData_Interface).Config = value; } }
      public new ClassSettings Settings { get { return (this as IKGD_WidgetData_Interface).Settings as ClassSettings; } set { (this as IKGD_WidgetData_Interface).Settings = value; } }
      //
      public override Type[] knownTypes { get { return new Type[] { typeof(ClassWindow), typeof(ClassConfig), typeof(ClassSettings), typeof(ClassData) }; } }
      public static IKGD_WidgetData_PhotoGallery DefaultValue { get { return new IKGD_WidgetData_PhotoGallery { Window = ClassWindow.DefaultValue, Config = ClassConfig.DefaultValue, Settings = ClassSettings.DefaultValue, Data = ClassData.DefaultValue }; } }
      //
      // classi inline per la definizione dei parametri del widget
      //
      [DataContract]
      public new class ClassConfig
      {
        public enum SlideShowModes
        {
          [DescriptionAttribute("Ordine impostato nel BackEnd")]
          OrderVFS,
          [DescriptionAttribute("Immagini più cliccate")]
          MostClicked,
          [DescriptionAttribute("Ordine di creazione")]
          FirstCreated,
          [DescriptionAttribute("Ultimi inserimenti")]
          LastModified
        };

        [DataMember]
        public SlideShowModes SlideShowMode { get; set; }
        [DataMember]
        public double? SlideShowDelayTime { get; set; }
        [DataMember]
        public double? SlideShowTransitionTime { get; set; }
        //
        public static ClassConfig DefaultValue { get { return new ClassConfig { SlideShowMode = SlideShowModes.OrderVFS, SlideShowDelayTime = null, SlideShowTransitionTime = null }; } }
      }
      //
      // per gli user settings del widget
      //
      [DataContract]
      public new class ClassSettings
      {
        [DataMember]
        public int ItemsCount { get; set; }
        //
        public static ClassSettings DefaultValue { get { return new ClassSettings { ItemsCount = 10 }; } }
      }
    }
    //
  }


  //
  // Widget Aggregatore di risorse filtrate per area
  //
  public class IKGD_Widget_AggregatorByArea : IKGD_WidgetBaseBrowse
  {
  }


}