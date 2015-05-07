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
using System.Linq.Expressions;
using System.Web.Caching;
using System.Drawing;
using LinqKit;
using Newtonsoft.Json;

using Ikon;
using Ikon.Log;
using Ikon.Support;
using Ikon.GD;


namespace Ikon.IKCMS.Library.Resources
{
  using Ikon.IKGD.Library.Resources;
  using Ikon.IKCMS.Library.Resources;
  using Ikon.IKGD.Library;



  // TODO: componente ancora da convertire in brick
  [Description("Flash")]
  public class IKCMS_ResourceType_Flash : IKCMS_ResourceBaseCMS<IKCMS_ResourceType_Flash.WidgetSettingsType>, IKCMS_Widget_Interface, IKCMS_IsIndexable_Interface
  {
    public override string IconEditor { get { return "CMS3.flash.gif"; } }

    //
    // WidgetSettings
    //
    public class WidgetSettingsType : WidgetSettingsType_FullUrl_Interface
    {
      public string Title { get; set; }
      //
      // interfaces: WidgetSettingsType_FullUrl_Interface
      public string LinkUrl { get; set; }
      public string LinkQueryString { get; set; }
      public string LinkTarget { get; set; }
      public int? Link_sNode { get; set; }
      public int? Link_rNode { get; set; }
      //
      public int? Width { get; set; }
      public int? Height { get; set; }
      public string MimeType { get; set; }
      public KeyValueObjectTree Data { get; set; }
      //
      public static WidgetSettingsType DefaultValue { get { return new WidgetSettingsType(); } }
      public WidgetSettingsType()
      {
        Title = null;
        Link_sNode = null;
        LinkUrl = null;
        LinkQueryString = null;
        LinkTarget = null;
        Width = null;
        Height = null;
        MimeType = null;
        Data = new KeyValueObjectTree();
      }
    }
  }




}