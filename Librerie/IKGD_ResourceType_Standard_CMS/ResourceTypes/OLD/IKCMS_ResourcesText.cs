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



  [Description("Testo")]
  public class IKCMS_ResourceType_Text : IKCMS_ResourceBaseCMS<IKCMS_ResourceType_Text.WidgetSettingsType>, IKCMS_Widget_Interface, IKCMS_IsIndexable_Interface
  {
    public override string IconEditor { get { return "CMS3.testo.gif"; } }


    //
    // WidgetSettings
    //
    public class WidgetSettingsType
    {
      public string Text { get; set; }
      //
      public static WidgetSettingsType DefaultValue { get { return new WidgetSettingsType(); } }
      public WidgetSettingsType() { Text = string.Empty; }
    }
    //
  }


}