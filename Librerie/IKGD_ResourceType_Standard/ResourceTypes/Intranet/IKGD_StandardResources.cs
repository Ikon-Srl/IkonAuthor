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


  public class IKGD_ResourceType_News : IKGD_ResourceType_NewsBase
  {
    public override string IconEditor { get { return "ResourceType.News.gif"; } }
  }


  public class IKGD_ResourceType_FAQ : IKGD_ResourceType_NewsBase
  {
    public override string IconEditor { get { return "ResourceType.FAQ.gif"; } }
  }


  public class IKGD_ResourceType_Modulistica : IKGD_ResourceType_NewsBase
  {
    public override string IconEditor { get { return "ResourceType.Modulistica.gif"; } }
  }


  public class IKGD_ResourceType_CosaFareSe : IKGD_ResourceType_NewsBase
  {
    public override string IconEditor { get { return "ResourceType.CosaFareSe.gif"; } }
  }


  public class IKGD_ResourceType_Circolari : IKGD_ResourceType_NewsBase
  {
    public override string IconEditor { get { return "ResourceType.Circolari.gif"; } }
  }


  public class IKGD_ResourceType_Documentazione : IKGD_ResourceType_NewsBase
  {
    public override string IconEditor { get { return "ResourceType.DocSupporto.gif"; } }
  }


  public class IKGD_ResourceType_Normativa : IKGD_ResourceType_NewsBase
  {
    public override string IconEditor { get { return "ResourceType.Normativa.gif"; } }
  }


  public class IKGD_ResourceType_Calendar : IKGD_WidgetBase, IKCMS_IsIndexable_Interface, IKGD_ResourceTypeCollectable_Interface
  {
    public new IKGD_WidgetData_Calendar WidgetSettings { get { return (this as IKGD_Widget_Interface).WidgetSettings as IKGD_WidgetData_Calendar; } set { (this as IKGD_Widget_Interface).WidgetSettings = value; } }
    public override Type WidgetSettingsType { get { return typeof(IKGD_WidgetData_Calendar); } }
    public override string IconEditor { get { return "ResourceType.Calendario.gif"; } }

    //
    // WidgetSettings
    //
    [DataContract]
    public class IKGD_WidgetData_Calendar : IKGD_WidgetDataBase
    {
      public new ClassConfig_Calendar Config { get { return (this as IKGD_WidgetData_Interface).Config as ClassConfig_Calendar; } set { (this as IKGD_WidgetData_Interface).Config = value; } }
      //
      public override Type[] knownTypes { get { return new Type[] { typeof(ClassWindow), typeof(ClassConfig_Calendar), typeof(ClassSettings), typeof(ClassData) }; } }
      public static IKGD_WidgetData_Calendar DefaultValue { get { return new IKGD_WidgetData_Calendar { Window = ClassWindow.DefaultValue, Config = ClassConfig_Calendar.DefaultValue, Settings = ClassSettings.DefaultValue, Data = ClassData.DefaultValue }; } }
      //
      // classi inline per la definizione dei parametri del widget
      //
      [DataContract]
      public class ClassConfig_Calendar
      {
        [DataMember]
        public string Title { get; set; }
        [DataMember]
        public string Orario { get; set; }
        [DataMember]
        public DateTime DateStart { get; set; }
        [DataMember]
        public DateTime DateEnd { get; set; }
        //
        public static ClassConfig_Calendar DefaultValue { get { return new ClassConfig_Calendar { Title = string.Empty, Orario = string.Empty, DateStart = DateTime.Now, DateEnd = DateTime.Now }; } }
      }
    }
    //
  }


  public class IKGD_ResourceType_Contatto : IKGD_ResourceTypeBase, IKCMS_IsIndexable_Interface
  {
    public override string IconEditor { get { return "ResourceType.Contatti.gif"; } }
  }


  public class IKGD_ResourceType_Link : IKGD_WidgetBase, IKCMS_IsIndexable_Interface
  {
    public new IKGD_WidgetData_Link WidgetSettings { get { return (this as IKGD_Widget_Interface).WidgetSettings as IKGD_WidgetData_Link; } set { (this as IKGD_Widget_Interface).WidgetSettings = value; } }
    public override Type WidgetSettingsType { get { return typeof(IKGD_WidgetData_Link); } }
    public override string IconEditor { get { return "ResourceType.Link.gif"; } }

    public override string SearchTitleMember { get { return "UrlSite"; } }

    //
    // WidgetSettings
    //
    [DataContract]
    public class IKGD_WidgetData_Link : IKGD_WidgetDataBase
    {
      public new ClassConfig_Link Config { get { return (this as IKGD_WidgetData_Interface).Config as ClassConfig_Link; } set { (this as IKGD_WidgetData_Interface).Config = value; } }
      //
      public override Type[] knownTypes { get { return new Type[] { typeof(ClassWindow), typeof(ClassConfig_Link), typeof(ClassSettings), typeof(ClassData) }; } }
      public static IKGD_WidgetData_Link DefaultValue { get { return new IKGD_WidgetData_Link { Window = ClassWindow.DefaultValue, Config = ClassConfig_Link.DefaultValue, Settings = ClassSettings.DefaultValue, Data = ClassData.DefaultValue }; } }
      //
      // classi inline per la definizione dei parametri del widget
      //
      [DataContract]
      public class ClassConfig_Link
      {
        [DataMember]
        public string UrlSite { get; set; }
        [DataMember]
        public string UrlLabel { get; set; }
        [DataMember]
        public string Text { get; set; }
        [DataMember]
        //
        public static ClassConfig_Link DefaultValue { get { return new ClassConfig_Link { UrlSite = string.Empty, UrlLabel = string.Empty, Text = string.Empty }; } }
      }
    }
    //
  }




  public class IKGD_ResourceType_Photo : IKGD_WidgetBase, IKCMS_Widget_Interface, IKCMS_IsIndexable_Interface
  {
    public new IKGD_WidgetData_Photo WidgetSettings { get { return (this as IKGD_Widget_Interface).WidgetSettings as IKGD_WidgetData_Photo; } set { (this as IKGD_Widget_Interface).WidgetSettings = value; } }
    public override Type WidgetSettingsType { get { return typeof(IKGD_WidgetData_Photo); } }
    public override string IconEditor { get { return "ResourceType.Multimediale.gif"; } }
    public override bool HasInode { get { return true; } }

    //
    // WidgetSettings
    //
    [DataContract]
    public class IKGD_WidgetData_Photo : IKGD_WidgetDataBase
    {
      public new ClassConfig Config { get { return (this as IKGD_WidgetData_Interface).Config as ClassConfig; } set { (this as IKGD_WidgetData_Interface).Config = value; } }
      //
      public override Type[] knownTypes { get { return new Type[] { typeof(ClassWindow), typeof(ClassConfig), typeof(ClassSettings), typeof(ClassData) }; } }
      public static IKGD_WidgetData_Photo DefaultValue { get { return new IKGD_WidgetData_Photo { Window = ClassWindow.DefaultValue, Config = ClassConfig.DefaultValue, Settings = ClassSettings.DefaultValue, Data = ClassData.DefaultValue }; } }
      //
      // classi inline per la definizione dei parametri del widget
      //
      [DataContract]
      public new class ClassConfig
      {
        [DataMember]
        public string Title { get; set; }
        [DataMember]
        public string Text { get; set; }
        [DataMember]
        public Dictionary<string, System.Drawing.Point> Dimensions { get; set; }
        [DataMember]
        public Dictionary<string, int> Sizes { get; set; }
        //
        public static ClassConfig DefaultValue { get { return new ClassConfig { Title = string.Empty, Text = string.Empty, Dimensions = new Dictionary<string, System.Drawing.Point>(), Sizes = new Dictionary<string, int>() }; } }
      }
    }
    //
  }



}