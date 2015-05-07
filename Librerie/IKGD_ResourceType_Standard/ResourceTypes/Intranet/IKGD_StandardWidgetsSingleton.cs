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
  // WidgetSettings XYZ (da usare come dummy)
  //
  [DataContract]
  public class IKGD_WidgetData_XYZ : IKGD_WidgetDataBase
  {
    public new ClassConfig_XYZ Config { get { return (this as IKGD_WidgetData_Interface).Config as ClassConfig_XYZ; } set { (this as IKGD_WidgetData_Interface).Config = value; } }
    public new ClassSettings_XYZ Settings { get { return (this as IKGD_WidgetData_Interface).Settings as ClassSettings_XYZ; } set { (this as IKGD_WidgetData_Interface).Settings = value; } }
    //
    public override Type[] knownTypes { get { return new Type[] { typeof(ClassWindow), typeof(ClassConfig_XYZ), typeof(ClassSettings_XYZ), typeof(ClassData) }; } }
    public static IKGD_WidgetData_XYZ DefaultValue { get { return new IKGD_WidgetData_XYZ { Window = ClassWindow.DefaultValue, Config = ClassConfig_XYZ.DefaultValue, Settings = ClassSettings_XYZ.DefaultValue, Data = ClassData.DefaultValue }; } }
    //
    // classi inline per la definizione dei parametri del widget
    //
    [DataContract]
    public class ClassConfig_XYZ
    {
      [DataMember]
      public string XYZ { get; set; }
      //
      public static ClassConfig_XYZ DefaultValue { get { return new ClassConfig_XYZ { XYZ = string.Empty }; } }
    }
    //
    [DataContract]
    public class ClassSettings_XYZ
    {
      [DataMember]
      public string XYZ { get; set; }
      //
      public static ClassSettings_XYZ DefaultValue { get { return new ClassSettings_XYZ { XYZ = string.Empty }; } }
    }
  }


  //
  // Widget Menù
  //
  public class IKGD_Widget_Menu : IKGD_WidgetBaseSingleton
  {
    public new IKGD_WidgetData_Menu WidgetSettings { get { return (this as IKGD_Widget_Interface).WidgetSettings as IKGD_WidgetData_Menu; } set { (this as IKGD_Widget_Interface).WidgetSettings = value; } }
    public override Type WidgetSettingsType { get { return typeof(IKGD_WidgetData_Menu); } }
    public override string IconEditor { get { return "ResourceType.Menu.gif"; } }

    //
    // WidgetSettings
    //
    [DataContract]
    public class IKGD_WidgetData_Menu : IKGD_WidgetDataBase
    {
      public new ClassConfig_Menu Config { get { return (this as IKGD_WidgetData_Interface).Config as ClassConfig_Menu; } set { (this as IKGD_WidgetData_Interface).Config = value; } }
      //
      public override Type[] knownTypes { get { return new Type[] { typeof(ClassWindow), typeof(ClassConfig_Menu), typeof(ClassSettings), typeof(ClassData) }; } }
      public static IKGD_WidgetData_Menu DefaultValue { get { return new IKGD_WidgetData_Menu { Window = ClassWindow.DefaultValue, Config = ClassConfig_Menu.DefaultValue, Settings = ClassSettings.DefaultValue, Data = ClassData.DefaultValue }; } }
      //
      // classi inline per la definizione dei parametri del widget (per ora non c'e' necessita' di customizzazioni)
      //
      [DataContract]
      public class ClassConfig_Menu
      {
        //[DataMember]
        //public string Text { get; set; }
        //
        public static ClassConfig_Menu DefaultValue { get { return new ClassConfig_Menu { }; } }
      }
    }
    //
  }


  //
  // Widget Search
  //
  public class IKGD_Widget_Search : IKGD_WidgetBaseSingleton
  {
    public new IKGD_WidgetData_Search WidgetSettings { get { return (this as IKGD_Widget_Interface).WidgetSettings as IKGD_WidgetData_Search; } set { (this as IKGD_Widget_Interface).WidgetSettings = value; } }
    public override Type WidgetSettingsType { get { return typeof(IKGD_WidgetData_Search); } }
    public override string IconEditor { get { return "ResourceType.Search.gif"; } }

    //
    // WidgetSettings
    //
    [DataContract]
    public class IKGD_WidgetData_Search : IKGD_WidgetDataBase
    {
      public new ClassConfig_Search Config { get { return (this as IKGD_WidgetData_Interface).Config as ClassConfig_Search; } set { (this as IKGD_WidgetData_Interface).Config = value; } }
      //
      public override Type[] knownTypes { get { return new Type[] { typeof(ClassWindow), typeof(ClassConfig_Search), typeof(ClassSettings), typeof(ClassData) }; } }
      public static IKGD_WidgetData_Search DefaultValue { get { return new IKGD_WidgetData_Search { Window = ClassWindow.DefaultValue, Config = ClassConfig_Search.DefaultValue, Settings = ClassSettings.DefaultValue, Data = ClassData.DefaultValue }; } }
      //
      // classi inline per la definizione dei parametri del widget (per ora non c'e' necessita' di customizzazioni)
      //
      [DataContract]
      public class ClassConfig_Search
      {
        //[DataMember]
        //public string Text { get; set; }
        //
        public static ClassConfig_Search DefaultValue { get { return new ClassConfig_Search { }; } }
      }
    }
    //
  }


  //
  // Widget HTML
  //
  public class IKGD_Widget_HTML : IKGD_WidgetBaseSingleton, IKCMS_Widget_Interface, IKCMS_HasResourceData_Interface, IKCMS_IsIndexable_Interface
  {
    public override bool HasInode { get { return true; } }  // questo widget ha un INODE
    //
    public new IKGD_WidgetData_HTML WidgetSettings { get { return (this as IKGD_Widget_Interface).WidgetSettings as IKGD_WidgetData_HTML; } set { (this as IKGD_Widget_Interface).WidgetSettings = value; } }
    public override Type WidgetSettingsType { get { return typeof(IKGD_WidgetData_HTML); } }
    public override string IconEditor { get { return "ResourceType.Html.gif"; } }

    //
    // WidgetSettings
    //
    [DataContract]
    public class IKGD_WidgetData_HTML : IKGD_WidgetDataBase
    {
      public new ClassConfig_HTML Config { get { return (this as IKGD_WidgetData_Interface).Config as ClassConfig_HTML; } set { (this as IKGD_WidgetData_Interface).Config = value; } }
      //
      public override Type[] knownTypes { get { return new Type[] { typeof(ClassWindow), typeof(ClassConfig_HTML), typeof(ClassSettings), typeof(ClassData) }; } }
      public static IKGD_WidgetData_HTML DefaultValue { get { return new IKGD_WidgetData_HTML { Window = ClassWindow.DefaultValue, Config = ClassConfig_HTML.DefaultValue, Settings = ClassSettings.DefaultValue, Data = ClassData.DefaultValue }; } }
      //
      // classi inline per la definizione dei parametri del widget
      //
      [DataContract]
      public class ClassConfig_HTML
      {
        [DataMember]
        public string Text { get; set; }
        //
        public static ClassConfig_HTML DefaultValue { get { return new ClassConfig_HTML { Text = string.Empty }; } }
      }
    }
    //
  }


  //
  // Widget Web Site
  //
  public class IKGD_Widget_WebSite : IKGD_WidgetBaseSingleton, IKCMS_IsIndexable_Interface
  {
    public new IKGD_WidgetData_WebSite WidgetSettings { get { return (this as IKGD_Widget_Interface).WidgetSettings as IKGD_WidgetData_WebSite; } set { (this as IKGD_Widget_Interface).WidgetSettings = value; } }
    public override Type WidgetSettingsType { get { return typeof(IKGD_WidgetData_WebSite); } }
    public override string IconEditor { get { return "ResourceType.App.gif"; } }

    public override string SearchTitleMember { get { return "UrlSite"; } }

    //
    // WidgetSettings
    //
    [DataContract]
    public class IKGD_WidgetData_WebSite : IKGD_WidgetDataBase
    {
      public new ClassConfig_WebSite Config { get { return (this as IKGD_WidgetData_Interface).Config as ClassConfig_WebSite; } set { (this as IKGD_WidgetData_Interface).Config = value; } }
      //
      public override Type[] knownTypes { get { return new Type[] { typeof(ClassWindow), typeof(ClassConfig_WebSite), typeof(ClassSettings), typeof(ClassData) }; } }
      public static IKGD_WidgetData_WebSite DefaultValue { get { return new IKGD_WidgetData_WebSite { Window = ClassWindow.DefaultValue, Config = ClassConfig_WebSite.DefaultValue, Settings = ClassSettings.DefaultValue, Data = ClassData.DefaultValue }; } }
      //
      // classi inline per la definizione dei parametri del widget
      //
      [DataContract]
      public class ClassConfig_WebSite
      {
        [DataMember]
        public string UrlSite { get; set; }
        [DataMember]
        public string Text { get; set; }
        [DataMember]
        public bool TargetBlank { get; set; }
        [DataMember]
        public bool IntranetOnly { get; set; }
        [DataMember]
        public string UrlSiteExternal { get; set; }
        //
        public static ClassConfig_WebSite DefaultValue { get { return new ClassConfig_WebSite { UrlSite = "http://", Text = string.Empty, TargetBlank = false, IntranetOnly = false, UrlSiteExternal = string.Empty }; } }
      }
    }
    //
  }


  //
  // Widget Contenuto Multimediale
  //
  public class IKGD_Widget_Multimedia : IKGD_WidgetBaseSingleton, IKCMS_Widget_Interface, IKCMS_HasResourceData_Interface, IKCMS_IsIndexable_Interface
  {
    public new IKGD_WidgetData_Multimedia WidgetSettings { get { return (this as IKGD_Widget_Interface).WidgetSettings as IKGD_WidgetData_Multimedia; } set { (this as IKGD_Widget_Interface).WidgetSettings = value; } }
    public override Type WidgetSettingsType { get { return typeof(IKGD_WidgetData_Multimedia); } }
    public override string IconEditor { get { return "ResourceType.Multimediale.gif"; } }

    public override string SearchTitleMember { get { return "UrlSite"; } }

    //
    // WidgetSettings
    //
    [DataContract]
    public class IKGD_WidgetData_Multimedia : IKGD_WidgetDataBase
    {
      public new ClassConfig_Multimedia Config { get { return (this as IKGD_WidgetData_Interface).Config as ClassConfig_Multimedia; } set { (this as IKGD_WidgetData_Interface).Config = value; } }
      //
      public override Type[] knownTypes { get { return new Type[] { typeof(ClassWindow), typeof(ClassConfig_Multimedia), typeof(ClassSettings), typeof(ClassData) }; } }
      public static IKGD_WidgetData_Multimedia DefaultValue { get { return new IKGD_WidgetData_Multimedia { Window = ClassWindow.DefaultValue, Config = ClassConfig_Multimedia.DefaultValue, Settings = ClassSettings.DefaultValue, Data = ClassData.DefaultValue }; } }

      //
      // classi inline per la definizione dei parametri del widget
      //
      [DataContract]
      public class ClassConfig_Multimedia
      {
        [DataMember]
        public string UrlSite { get; set; }
        [DataMember]
        public string Text { get; set; }
        [DataMember]
        public string WidgetQS { get; set; }
        [DataMember]
        public int Width { get; set; }
        [DataMember]
        public int Height { get; set; }
        [DataMember]
        public bool Loop { get; set; }
        //
        public static ClassConfig_Multimedia DefaultValue { get { return new ClassConfig_Multimedia { UrlSite = string.Empty, Text = "VAI", WidgetQS = string.Empty, Width = 300, Height = 200, Loop = true }; } }
      }
    }
    //
  }


  //
  // Widget Feed RSS
  //
  public class IKGD_Widget_RSS : IKGD_WidgetBaseSingleton, IKCMS_Widget_Interface, IKCMS_HasResourceData_Interface
  {
    public new IKGD_WidgetData_RSS WidgetSettings { get { return (this as IKGD_Widget_Interface).WidgetSettings as IKGD_WidgetData_RSS; } set { (this as IKGD_Widget_Interface).WidgetSettings = value; } }
    public override Type WidgetSettingsType { get { return typeof(IKGD_WidgetData_RSS); } }
    public override string IconEditor { get { return "ResourceType.FeedRSS.gif"; } }

    //
    // WidgetSettings
    //
    [DataContract]
    public class IKGD_WidgetData_RSS : IKGD_WidgetDataBase
    {
      public new ClassConfig_RSS Config { get { return (this as IKGD_WidgetData_Interface).Config as ClassConfig_RSS; } set { (this as IKGD_WidgetData_Interface).Config = value; } }
      public new ClassSettings_RSS Settings { get { return (this as IKGD_WidgetData_Interface).Settings as ClassSettings_RSS; } set { (this as IKGD_WidgetData_Interface).Settings = value; } }
      //
      public override Type[] knownTypes { get { return new Type[] { typeof(ClassWindow), typeof(ClassConfig_RSS), typeof(ClassSettings_RSS), typeof(ClassData) }; } }
      public static IKGD_WidgetData_RSS DefaultValue { get { return new IKGD_WidgetData_RSS { Window = ClassWindow.DefaultValue, Config = ClassConfig_RSS.DefaultValue, Settings = ClassSettings_RSS.DefaultValue, Data = ClassData.DefaultValue }; } }
      //
      // classi inline per la definizione dei parametri del widget
      //
      [DataContract]
      public class ClassConfig_RSS
      {
        [DataMember]
        public string UrlRSS { get; set; }
        [DataMember]
        public string UrlHome { get; set; }
        //
        public static ClassConfig_RSS DefaultValue { get { return new ClassConfig_RSS { UrlRSS = "http://", UrlHome = "http://" }; } }
      }
      //
      [DataContract]
      public class ClassSettings_RSS
      {
        [DataMember]
        public int ItemsCount { get; set; }
        //
        public static ClassSettings_RSS DefaultValue { get { return new ClassSettings_RSS { ItemsCount = 5 }; } }
      }
    }
    //
  }


  //
  // Widget Google gadget
  //
  public class IKGD_Widget_iGoogle : IKGD_WidgetBaseSingleton
  {
    public new IKGD_WidgetData_iGoogle WidgetSettings { get { return (this as IKGD_Widget_Interface).WidgetSettings as IKGD_WidgetData_iGoogle; } set { (this as IKGD_Widget_Interface).WidgetSettings = value; } }
    public override Type WidgetSettingsType { get { return typeof(IKGD_WidgetData_iGoogle); } }
    public override string IconEditor { get { return "ResourceType.iGoogle.gif"; } }

    //
    // WidgetSettings
    //
    [DataContract]
    public class IKGD_WidgetData_iGoogle : IKGD_WidgetDataBase
    {
      public new ClassConfig_iGoogle Config { get { return (this as IKGD_WidgetData_Interface).Config as ClassConfig_iGoogle; } set { (this as IKGD_WidgetData_Interface).Config = value; } }
      //
      public override Type[] knownTypes { get { return new Type[] { typeof(ClassWindow), typeof(ClassConfig_iGoogle), typeof(ClassSettings), typeof(ClassData) }; } }
      public static IKGD_WidgetData_iGoogle DefaultValue { get { return new IKGD_WidgetData_iGoogle { Window = ClassWindow.DefaultValue, Config = ClassConfig_iGoogle.DefaultValue, Settings = ClassSettings.DefaultValue, Data = ClassData.DefaultValue }; } }
      //
      // classi inline per la definizione dei parametri del widget
      //
      [DataContract]
      public class ClassConfig_iGoogle
      {
        [DataMember]
        public string UrlGadget { get; set; }
        //
        [DataMember]
        public string XmlGadget { get; set; }
        //
        public static ClassConfig_iGoogle DefaultValue { get { return new ClassConfig_iGoogle { UrlGadget = "http://www.google.com/ig", XmlGadget = null }; } }
      }
    }
    //
  }


  //
  // Widget IkonHelpDeskTicket
  //
  public class IKGD_Widget_IkonHelpDeskTicket : IKGD_WidgetBaseSingleton
  {
    public new IKGD_WidgetData_IkonHelpDeskTicket WidgetSettings { get { return (this as IKGD_Widget_Interface).WidgetSettings as IKGD_WidgetData_IkonHelpDeskTicket; } set { (this as IKGD_Widget_Interface).WidgetSettings = value; } }
    public override Type WidgetSettingsType { get { return typeof(IKGD_WidgetData_IkonHelpDeskTicket); } }
    public override string IconEditor { get { return "ResourceType.Organigramma.gif"; } }

    //
    // WidgetSettings
    //
    [DataContract]
    public class IKGD_WidgetData_IkonHelpDeskTicket : IKGD_WidgetDataBase
    {
      public new ClassConfig Config { get { return (this as IKGD_WidgetData_Interface).Config as ClassConfig; } set { (this as IKGD_WidgetData_Interface).Config = value; } }
      public new ClassSettings Settings { get { return (this as IKGD_WidgetData_Interface).Settings as ClassSettings; } set { (this as IKGD_WidgetData_Interface).Settings = value; } }
      //
      public override Type[] knownTypes { get { return new Type[] { typeof(ClassWindow), typeof(ClassConfig), typeof(ClassSettings), typeof(ClassData) }; } }
      public static IKGD_WidgetData_IkonHelpDeskTicket DefaultValue { get { return new IKGD_WidgetData_IkonHelpDeskTicket { Window = ClassWindow.DefaultValue, Config = ClassConfig.DefaultValue, Settings = ClassSettings.DefaultValue, Data = ClassData.DefaultValue }; } }
      //
      // classi inline per la definizione dei parametri del widget (che non ci sono...)
      //
    }
    //
  }


  //
  // Widget IkonAnagrafica
  //
  public class IKGD_Widget_IkonAnagrafica : IKGD_WidgetBaseSingleton
  {
    public new IKGD_WidgetData_IkonAnagrafica WidgetSettings { get { return (this as IKGD_Widget_Interface).WidgetSettings as IKGD_WidgetData_IkonAnagrafica; } set { (this as IKGD_Widget_Interface).WidgetSettings = value; } }
    public override Type WidgetSettingsType { get { return typeof(IKGD_WidgetData_IkonAnagrafica); } }
    public override string IconEditor { get { return "ResourceType.Organigramma.gif"; } }

    //
    // WidgetSettings
    //
    [DataContract]
    public class IKGD_WidgetData_IkonAnagrafica : IKGD_WidgetDataBase
    {
      public new ClassConfig Config { get { return (this as IKGD_WidgetData_Interface).Config as ClassConfig; } set { (this as IKGD_WidgetData_Interface).Config = value; } }
      public new ClassSettings Settings { get { return (this as IKGD_WidgetData_Interface).Settings as ClassSettings; } set { (this as IKGD_WidgetData_Interface).Settings = value; } }
      //
      public override Type[] knownTypes { get { return new Type[] { typeof(ClassWindow), typeof(ClassConfig), typeof(ClassSettings), typeof(ClassData) }; } }
      public static IKGD_WidgetData_IkonAnagrafica DefaultValue { get { return new IKGD_WidgetData_IkonAnagrafica { Window = ClassWindow.DefaultValue, Config = ClassConfig.DefaultValue, Settings = ClassSettings.DefaultValue, Data = ClassData.DefaultValue }; } }
      //
      // classi inline per la definizione dei parametri del widget (che non ci sono...)
      //
    }
    //
  }





}