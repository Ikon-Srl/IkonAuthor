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



  public interface IKCMS_HasMultiStreamInfo4Settings_Interface
  {
    bool HasStream(string stream);
    bool HasStream(string source, string key);
    IEnumerable<MultiStreamInfo4Settings> StreamInfos();
    MultiStreamInfo4Settings StreamInfos(string stream);
    MultiStreamInfo4Settings StreamInfos(string source, string key);
    IEnumerable<string> StreamSources();
    IEnumerable<string> StreamSourceKeys(string source);
    bool IsImage(string stream);
    bool IsFlash(string stream);
  }


  public interface IKCMS_HasGenericBrick_Interface : IKGD_ResourceType_Interface, IKCMS_HasSerializationCMS_Interface, IKCMS_BrickBase_Interface, IKCMS_Widget_Interface, IKCMS_HasPropertiesLanguageKVT_Interface, IKCMS_HasPropertiesKVT_Interface
  {
    IKCMS_HasGenericBrickSettings_Interface ResourceSettingsBase { get; }
  }


  public interface IKCMS_HasGenericBrickSettings_Interface : WidgetSettingsType_FullUrl_Interface, WidgetSettingsType_HasKVTO_Interface, WidgetSettingsType_HasTemplateSelector_Interface, IKCMS_HasMultiStreamInfo4Settings_Interface
  {
  }


  //
  // risorsa tipo widget dati generico CMS
  //
  [Description("Risorsa Generica del CMS")]
  public abstract class IKCMS_ResourceType_GenericBrickBase<T> : IKCMS_ResourceBaseCMS<T>, IKCMS_HasGenericBrick_Interface, IKCMS_IsIndexable_Interface, IKCMS_BrickBase_Interface, IKCMS_HasDeserializeOnVFS_Interface
    where T : class, IKCMS_HasGenericBrickSettings_Interface, new()
  {
    public override string IconEditor { get { return "ResourceType.Html.gif"; } }
    public override bool IsFolder { get { return false; } }
    //
    public virtual IKCMS_HasGenericBrickSettings_Interface ResourceSettingsBase { get { return ResourceSettings; } }
    public virtual KeyValueObjectTree ResourceSettingsKVT { get { return (ResourceSettings as WidgetSettingsTypeGenericBrickBase).Values; } }
    public virtual KeyValueObjectTree ResourceSettingsLanguageKVT(params string[] keys) { return (ResourceSettings as WidgetSettingsTypeGenericBrickBase).Values.KeyFilterTry(IKGD_Language_Provider.Provider.Language, keys); }
    public virtual KeyValueObjectTree ResourceSettingsNoLanguageKVT(params string[] keys) { return (ResourceSettings as WidgetSettingsTypeGenericBrickBase).Values.KeyFilterTry(null, keys); }
    public virtual IEnumerable<KeyValueObjectTree> ResourceSettingsLanguageKVTs(params string[] keys) { return (ResourceSettings as WidgetSettingsTypeGenericBrickBase).Values.KeyFilterTryMulti(IKGD_Language_Provider.Provider.Language, keys); }
    public virtual IEnumerable<KeyValueObjectTree> ResourceSettingsNoLanguageKVTs(params string[] keys) { return (ResourceSettings as WidgetSettingsTypeGenericBrickBase).Values.KeyFilterTryMulti(null, keys); }
    public virtual List<string> ResourceSettingsLanguageKVTss(params string[] keys) { return (ResourceSettings as WidgetSettingsTypeGenericBrickBase).Values.KeyFilterTryMulti(IKGD_Language_Provider.Provider.Language, keys).Select(r => r.ValueString).Where(s => s != null).ToList(); }
    public virtual List<string> ResourceSettingsNoLanguageKVTss(params string[] keys) { return (ResourceSettings as WidgetSettingsTypeGenericBrickBase).Values.KeyFilterTryMulti(null, keys).Select(r => r.ValueString).Where(s => s != null).ToList(); }
    //


    //
    // WidgetSettingsType
    //
    public class WidgetSettingsTypeGenericBrickBase : IKCMS_HasGenericBrickSettings_Interface
    {
      public string TemplateType { get; set; }
      [IKGD_DeserializeOnVFS_KVT()]
      public KeyValueObjectTree Values { get; set; }
      //
      public string LinkUrl { get; set; }
      public string LinkQueryString { get; set; }
      public string LinkTarget { get; set; }
      public int? Link_sNode { get; set; }
      public int? Link_rNode { get; set; }
      //
      public bool HasStream(string stream) { return StreamInfos(stream) != null; }
      public bool HasStream(string source, string key) { return Values.Get(null, IKGD_Constants.IKGD_StreamsProcessorFieldName, source, key) != null; }
      public IEnumerable<MultiStreamInfo4Settings> StreamInfos() { return Values.KeyFilterTry(null, IKGD_Constants.IKGD_StreamsProcessorFieldName).RecurseOnTree.Select(n => n.ValueComplex<MultiStreamInfo4Settings>()).Where(r => r != null); }
      public MultiStreamInfo4Settings StreamInfos(string stream) { var nodes = Values.KeyFilterTry(null, IKGD_Constants.IKGD_StreamsProcessorFieldName).RecurseOnTree.Select(n => n.ValueComplex<MultiStreamInfo4Settings>()).Where(r => r != null); return nodes.FirstOrDefault(n => string.Equals(string.Format("{0}|{1}", n.Source, n.Key), stream, StringComparison.OrdinalIgnoreCase)) ?? nodes.FirstOrDefault(n => string.Equals(n.Key, stream, StringComparison.OrdinalIgnoreCase)); }
      public MultiStreamInfo4Settings StreamInfos(string source, string key) { return Values.KeyFilterTry(null, IKGD_Constants.IKGD_StreamsProcessorFieldName, source, key).ValueComplex<MultiStreamInfo4Settings>(); }
      public IEnumerable<string> StreamSources() { return Values[null][IKGD_Constants.IKGD_StreamsProcessorFieldName].Nodes.Select(n => n.Key); }
      public IEnumerable<string> StreamSourceKeys(string source) { return Values[null][IKGD_Constants.IKGD_StreamsProcessorFieldName][source].Nodes.Select(n => n.Key); }
      public bool IsImage(string stream) { try { return Regex.IsMatch(@"^image/", StreamInfos(stream).Mime, RegexOptions.IgnoreCase); } catch { return false; } }
      public bool IsFlash(string stream) { try { return Regex.IsMatch(@"^application/x-shockwave-flash$", StreamInfos(stream).Mime, RegexOptions.IgnoreCase); } catch { return false; } }
      //
      // Model.StreamInfos("") --> risorsa originale
      // Model.StreamInfos("|") --> risorsa originale
      // Model.StreamInfos(null, "") --> risorsa originale
      // Model.StreamInfos(null, "stream_name") --> ritaglio
      //
      [JsonIgnore]
      public string Title { get { return Values.KeyFilterTry(IKGD_Language_Provider.Provider.Language, "Title").ValueString ?? Values.KeyFilterTry("Title").ValueString; } }
      [JsonIgnore]
      public string Text { get { return Values.KeyFilterTry(IKGD_Language_Provider.Provider.Language, "Text").ValueString ?? Values.KeyFilterTry("Text").ValueString; } }
      //

      public static WidgetSettingsTypeGenericBrickBase DefaultValue { get { return new WidgetSettingsTypeGenericBrickBase(); } }
      public WidgetSettingsTypeGenericBrickBase()
        : base()
      {
        TemplateType = null;
        Values = new KeyValueObjectTree();
        LinkUrl = null;
        LinkQueryString = null;
        LinkTarget = null;
        Link_sNode = null;
      }
    }
  }



  //
  // abstract class per la definizione dei widget custom
  //
  [Description("Widget Custom")]
  public abstract class IKCMS_ResourceType_BrickWidgetBase<T> : IKCMS_ResourceType_GenericBrickBase<T>, IKCMS_BrickBase_Interface, IKCMS_BrickWidget_Interface
    where T : class, IKCMS_HasGenericBrickSettings_Interface, new()
  {
    public override string IconEditor { get { return "ResourceType.Html.gif"; } }

    //public class WidgetSettingsType : WidgetSettingsTypeGenericBrickBase
    //{
    //  public new static WidgetSettingsType DefaultValue { get { return new WidgetSettingsType(); } }
    //  public WidgetSettingsType() : base() { }
    //}
  }

}