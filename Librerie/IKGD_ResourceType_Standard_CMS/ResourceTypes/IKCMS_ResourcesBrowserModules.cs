/*
 * 
 * Intranet Ikon
 * 
 * Copyright (C) 2010 Ikon Srl
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
using System.Linq.Expressions;
using System.Web.Caching;
using System.Drawing;
using LinqKit;

using Ikon;
using Ikon.Log;
using Ikon.Support;
using Ikon.GD;


namespace Ikon.IKCMS.Library.Resources
{
  using Ikon.IKGD.Library.Resources;
  using Ikon.IKGD.Library.Collectors;




  //
  // classe base astratta per tutti le risorse derivate dal tipo news V2
  // implementa il supporto comune per l'indicizzazione
  //
  //[Description("Modulo Browse Base per CMS KVT")]
  public abstract class IKCMS_ResourceType_BrowseModuleBaseKVT<T> : IKCMS_ResourceType_GenericBrickBase<T>, IKCMS_IsIndexable_Interface, IKCMS_ResourceWithViewer_Interface
    where T : class, IKCMS_HasGenericBrickSettings_Interface, new()
  {
    public override string IconEditor { get { return "CMS3.news.gif"; } }

    public class WidgetSettingsTypeBase : WidgetSettingsTypeGenericBrickBase, WidgetSettingsType_HasLinksList_Interface
    {
      public DateTime? DateMain { get; set; }
      public DateTime? DateAux { get; set; }
      //
      public List<IKCMS_ResourceType_LinkListElement> Links { get; set; }
      //
      public new static WidgetSettingsTypeBase DefaultValue { get { return new WidgetSettingsTypeBase(); } }
      public WidgetSettingsTypeBase()
        : base()
      {
        Links = new List<IKCMS_ResourceType_LinkListElement>();
      }
    }
  }


  //
  // CMS Modulo Base per teaser tipo News/Eventi
  //
  [Description("Teaser News/Eventi")]
  //public class IKCMS_ResourceType_TeaserNewsEventiKVT : IKCMS_ResourceBaseCMS<IKCMS_ResourceType_TeaserNewsEventiKVT.WidgetSettingsType>, IKCMS_Widget_Interface, IKCMS_HasPropertiesKVT_Interface, IKCMS_IsIndexable_Interface
  public class IKCMS_ResourceType_TeaserNewsEventiKVT : IKCMS_ResourceType_GenericBrickBase<IKCMS_ResourceType_TeaserNewsEventiKVT.WidgetSettingsType>, IKCMS_Widget_Interface, IKCMS_HasPropertiesKVT_Interface, IKCMS_IsIndexable_Interface, IKCMS_BrickWithPlaceholder_Interface
  {
    public override string IconEditor { get { return "CMS3.widget_news.gif"; } }
    //
    //public virtual KeyValueObjectTree ResourceSettingsKVT { get { return ResourceSettings.Values; } }
    //

    //
    // WidgetSettings
    //

    //public class WidgetSettingsType : WidgetSettingsType_HasTemplateSelector_Interface, WidgetSettingsType_HasKVTO_Interface
    public class WidgetSettingsType : WidgetSettingsTypeGenericBrickBase, WidgetSettingsType_HasTemplateSelector_Interface, WidgetSettingsType_HasKVTO_Interface
    {
      public string CollectorType { get; set; }  //IKGD_Teaser_Collector_InterfaceNG
      public string TagManagerType { get; set; }  //ManagerTagFilterBase_Interface
      // in KVT salveremo: Title, MaxItems, msDelay, msTransition
      //
      public new static WidgetSettingsType DefaultValue { get { return new WidgetSettingsType(); } }
      public WidgetSettingsType()
        : base()
      {
        //CollectorType = typeof(IKGD_Teaser_Collector_NewsEventsDateAsc<FS_Operations.FS_NodeInfo_Interface>).Name;
        CollectorType = null;
        TagManagerType = null;
      }
    }
    //
  }


  //
  // CMS Modulo Base tipo Browse/News
  //
  [Description("Pagina News")]
  public class IKCMS_ResourceType_BrowserModuleKVT : IKCMS_ResourceType_PageBaseAbstract<IKCMS_ResourceType_BrowserModuleKVT.WidgetSettingsType>, IKCMS_BrowsableModule_Interface, IKCMS_IsIndexable_Interface
  {
    public override string IconEditor { get { return "CMS3.pagina_news.gif"; } }
    public override string SearchTitleMember { get { return "TitleHead"; } }


    public class WidgetSettingsType : WidgetSettingsTypeBaseKVT
    {
      public string ArchiveFilterType { get; set; }  //IKGD_Archive_Filter_Interface
      public string ArchiveCollectorType { get; set; }  //IKGD_Archive_Collector_Interface
      public string ArchiveFormatterType { get; set; }  //IKGD_Archive_Formatter_Interface
      //

      public new static WidgetSettingsType DefaultValue { get { return new WidgetSettingsType(); } }
      public WidgetSettingsType()
        : base()
      {
        ArchiveFilterType = null;
        //ArchiveCollectorType = typeof(IKGD_Archive_Collector_NewsGeneral<fsNodeT>).Name;
        ArchiveCollectorType = null;
        ArchiveFormatterType = null;
      }
    }
    //
  }



  //
  // classe per la definizione di news con il nuovo engine configurabile
  //
  [Description("News")]
  public class IKCMS_ResourceType_NewsKVT : Ikon.IKCMS.Library.Resources.IKCMS_ResourceType_BrowseModuleBaseKVT<IKCMS_ResourceType_NewsKVT.WidgetSettingsType>, IKCMS_BrowsableIndexable_Interface
  {
    public class WidgetSettingsType : WidgetSettingsTypeBase
    {
      public new static WidgetSettingsType DefaultValue { get { return new WidgetSettingsType(); } }
      public WidgetSettingsType() : base() { }
    }
  }


  //
  // classe per la definizione di eventi (analoghi alle news ma senza un visualizzatore dedicato)
  //
  [Description("Evento")]
  public class IKCMS_ResourceType_EventKVT : IKCMS_ResourceType_NewsKVT, IKCMS_BrowsableIndexable_Interface
  {
  }


  //
  // classe per la definizione di eventi in pagine web
  //
  [Description("Evento (per pagine web)")]
  public class IKCMS_ResourceType_Event4PageKVT : IKCMS_ResourceType_NewsKVT, IKCMS_BrowsableIndexable_Interface
  {
  }


}