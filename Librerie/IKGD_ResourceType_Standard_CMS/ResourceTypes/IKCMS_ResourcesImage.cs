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



  //
  // risorsa base per la gestione delle risorse tipo immagine
  //
  public abstract class IKCMS_ResourceType_ImageBaseAbstract<T> : IKCMS_ResourceType_GenericBrickBase<T>, IKCMS_HasGenericBrick_Interface, IKCMS_BrickWithPlaceholder_Interface
    where T : class, IKCMS_HasGenericBrickSettings_Interface, new()
  {
    public override string IconEditor { get { return "CMS3.immagine.gif"; } }

    //
    // WidgetSettings
    //
    public class WidgetSettingsTypeBaseKVT : WidgetSettingsTypeGenericBrickBase, IKCMS_HasGenericBrickSettings_Interface
    {
      //
      // auto language settings wrappers
      // Title e Text sono definiti nella classe base
      [JsonIgnore]
      public string Alt { get { return (Values.KeyFilterCheck(IKGD_Language_Provider.Provider.Language, "Alt") ?? Values.KeyFilterTry(IKGD_Language_Provider.Provider.Language, "Title")).ValueString; } }
      //
      // no language settings wrappers
      [JsonIgnore]
      public string Author { get { return Values[null]["Author"].ValueString; } }
      [JsonIgnore]
      public string Location { get { return Values[null]["Location"].ValueString; } }
      [JsonIgnore]
      public bool PopupEnabled { get { return Values[null]["PopupEnabled"].ValueT<bool>(); } }
      [JsonIgnore]
      public bool UseOriginalStream { get { return Values[null]["UseOriginalStream"].ValueT<bool>(); } }
      //
      public new static WidgetSettingsTypeBaseKVT DefaultValue { get { return new WidgetSettingsTypeBaseKVT(); } }
      public WidgetSettingsTypeBaseKVT()
      {
      }
    }

  }


  [Description("Immagine")]
  public class IKCMS_ResourceType_ImageCMS : IKCMS_ResourceType_ImageBaseAbstract<IKCMS_ResourceType_ImageCMS.WidgetSettingsType>
  {
    public class WidgetSettingsType : WidgetSettingsTypeBaseKVT
    {
      public new static WidgetSettingsType DefaultValue { get { return new WidgetSettingsType(); } }
      public WidgetSettingsType() : base() { }
    }
  }



}