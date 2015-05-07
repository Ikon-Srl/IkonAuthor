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
using LinqKit;

using Ikon;
using Ikon.Log;
using Ikon.Support;
using Ikon.GD;


namespace Ikon.IKCMS.Library.Resources
{
  using Ikon.IKGD.Library;
  using Ikon.IKGD.Library.Resources;
  using Newtonsoft.Json;



  public interface WidgetSettingsType_HasLinksList_Interface
  {
    List<IKCMS_ResourceType_LinkListElement> Links { get; set; }
  }



  //
  // CMS PageBaseAbstract
  // classe base da cui derivare le risorse tipo page del CMS (Page CMS, Page Static, Page Catalog, ...)
  //
  public abstract class IKCMS_ResourceType_PageBaseAbstract<T> : IKCMS_ResourceBaseCMS<T>, IKCMS_HasGenericBrick_Interface, IKGD_Folder_Interface, IKCMS_PageWithPageEditor_Interface, IKCMS_Widget_Interface, IKCMS_HasPropertiesLanguageKVT_Interface, IKCMS_IsIndexable_Interface, IKCMS_ResourceWithViewer_Interface, IKCMS_HasDeserializeOnVFS_Interface
    where T : class, IKCMS_HasGenericBrickSettings_Interface, new()
  {
    public override bool HasInode { get { return false; } }
    public override bool IsUnstructured { get { return false; } }
    public override bool IsFolder { get { return true; } }
    public override string IconEditor { get { return "CMS3.pagina_web.gif"; } }
    //
    public virtual IKCMS_HasGenericBrickSettings_Interface ResourceSettingsBase { get { return ResourceSettings; } }
    public virtual KeyValueObjectTree ResourceSettingsKVT { get { return (ResourceSettings as WidgetSettingsTypeBaseKVT).Values; } }
    public virtual KeyValueObjectTree ResourceSettingsLanguageKVT(params string[] keys) { return (ResourceSettings as WidgetSettingsTypeBaseKVT).Values.KeyFilterTry(IKGD_Language_Provider.Provider.Language, keys); }
    public virtual KeyValueObjectTree ResourceSettingsNoLanguageKVT(params string[] keys) { return (ResourceSettings as WidgetSettingsTypeBaseKVT).Values.KeyFilterTry(null, keys); }
    public virtual IEnumerable<KeyValueObjectTree> ResourceSettingsLanguageKVTs(params string[] keys) { return (ResourceSettings as WidgetSettingsTypeBaseKVT).Values.KeyFilterTryMulti(IKGD_Language_Provider.Provider.Language, keys); }
    public virtual IEnumerable<KeyValueObjectTree> ResourceSettingsNoLanguageKVTs(params string[] keys) { return (ResourceSettings as WidgetSettingsTypeBaseKVT).Values.KeyFilterTryMulti(null, keys); }
    public virtual List<string> ResourceSettingsLanguageKVTss(params string[] keys) { return (ResourceSettings as WidgetSettingsTypeBaseKVT).Values.KeyFilterTryMulti(IKGD_Language_Provider.Provider.Language, keys).Select(r => r.ValueString).Where(s => s != null).ToList(); }
    public virtual List<string> ResourceSettingsNoLanguageKVTss(params string[] keys) { return (ResourceSettings as WidgetSettingsTypeBaseKVT).Values.KeyFilterTryMulti(null, keys).Select(r => r.ValueString).Where(s => s != null).ToList(); }
    //

    public class WidgetSettingsTypeBaseKVT : IKCMS_HasGenericBrickSettings_Interface
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

      public static WidgetSettingsTypeBaseKVT DefaultValue { get { return new WidgetSettingsTypeBaseKVT(); } }
      public WidgetSettingsTypeBaseKVT()
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
  // pagine generali del CMS
  // si basano completamente sul supporto KVT
  // con degli accessor a properties standardizzate per semplificarne l'utilizzo e migliorare la compatibilita' con l'infrastruttura esistente
  //
  [Description("Pagina Web")]
  public class IKCMS_ResourceType_PageCMS : IKCMS_ResourceType_PageBaseAbstract<IKCMS_ResourceType_PageCMS.WidgetSettingsType>
  {
    public override string SearchTitleMember { get { return "TitleHead"; } }
    public override string SearchTextMember { get { return "TitleH1"; } }


    public class WidgetSettingsType : WidgetSettingsTypeBaseKVT
    {
      public new static WidgetSettingsType DefaultValue { get { return new WidgetSettingsType(); } }
      public WidgetSettingsType()
        : base()
      {
      }
    }
  }


  //
  // pagine statiche integrate nel CMS
  // con solamente la URL come proprieta' configurabile
  // (continuiamo comunque ad utilizzare il supporto KVT per eventuali estensioni custom)
  //
  [Description("Pagina Speciale")]
  //public class IKCMS_ResourceType_PageStatic : IKCMS_ResourceType_PageBaseAbstract<IKCMS_ResourceType_PageStatic.WidgetSettingsType>
  public class IKCMS_ResourceType_PageStatic : IKCMS_ResourceType_PageBaseAbstract<IKCMS_ResourceType_PageStatic.WidgetSettingsType>
  {
    //public class WidgetSettingsType : WidgetSettingsTypeBaseKVT
    public class WidgetSettingsType : WidgetSettingsTypeBaseKVT
    {
      public string UrlExternal { get; set; }
      public string UrlMatchRegEx { get; set; }

      public new static WidgetSettingsType DefaultValue { get { return new WidgetSettingsType(); } }
      public WidgetSettingsType()
        : base()
      {
        UrlExternal = null;
        UrlMatchRegEx = null;
      }
    }
  }


  //
  // pagine multiple per il CMS
  // servono per aggregare piu' pagine in un model unico ricorsivo a piu' livelli (es. mega pagine per siti orizzontali o cataloghi)
  //
  [Description("MultiPagina Web")]
  public class IKCMS_ResourceType_MultiPageCMS : IKCMS_ResourceType_PageBaseAbstract<IKCMS_ResourceType_MultiPageCMS.WidgetSettingsType>
  {
    public override string SearchTitleMember { get { return "TitleHead"; } }
    public override string SearchTextMember { get { return "TitleH1"; } }


    public class WidgetSettingsType : WidgetSettingsTypeBaseKVT
    {
      public int? RecursionLevelMax { get; set; }

      public new static WidgetSettingsType DefaultValue { get { return new WidgetSettingsType(); } }
      public WidgetSettingsType()
        : base()
      {
        RecursionLevelMax = null;
      }
    }
  }


  //
  // pagine generali del CMS
  // si basano completamente sul supporto KVT
  // con degli accessor a properties standardizzate per semplificarne l'utilizzo e migliorare la compatibilita' con l'infrastruttura esistente
  // e fetch dei dati in IKATT_AttributeMapper
  //
  [Description("Prodotto Web")]
  public class IKCMS_ResourceType_ProductCMS : IKCMS_ResourceType_PageBaseAbstract<IKCMS_ResourceType_ProductCMS.WidgetSettingsType>
  {
      public override string SearchTitleMember { get { return "TitleHead"; } }
      public override string SearchTextMember { get { return "TitleH1"; } }


      public class WidgetSettingsType : WidgetSettingsTypeBaseKVT
      {
          public new static WidgetSettingsType DefaultValue { get { return new WidgetSettingsType(); } }
          public WidgetSettingsType()
              : base()
          {
          }
      }
  }


  //
  // Folder Web per il supporto dei flag aggiuntivi per i menu da usare nei siti Web
  //
  [Description("Nodo di menù")]
  public class IKCMS_FolderType_FolderWeb : IKGD_ResourceTypeBase, IKGD_Folder_Interface, IKCMS_ArchiveRoot_Interface
  {
    public override bool HasInode { get { return false; } }
    public override bool IsUnstructured { get { return false; } }
    public override bool IsFolder { get { return true; } }
    public override string IconEditor { get { return "CMS3.nodo_menu.gif"; } }
  }


  //
  // Folder speciale per la definizione della root degli archivi (browse/news)
  // gestire un sottotipo "archive_documents" per la gestione dei widget di documentazione in pagina
  //
  [Description("Archivio")]
  public class IKCMS_FolderType_ArchiveRoot : IKGD_ResourceTypeBase, IKGD_Folder_Interface, IKCMS_ArchiveRoot_Interface
  {
    public override bool HasInode { get { return false; } }
    public override bool IsUnstructured { get { return false; } }
    public override bool IsFolder { get { return true; } }
    public override string IconEditor { get { return "CMS3.pagina_archivio.gif"; } }
  }




  public class IKCMS_ResourceType_LinkListElement
  {
    public int? rNode { get; set; }
    public int? sNode { get; set; }
    public string Url { get; set; }
    public string Text { get; set; }
    public string Target { get; set; }
  }

}