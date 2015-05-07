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
  using Newtonsoft.Json;


  //
  // Folder CMS per la definizione delle categorie e sottocategorie del catalogo
  // da usare per cataloghi con attributi estesi su database
  //
  [Description("Categoria Catalogo")]
  public class IKCMS_ResourceType_PageCatalogCategoryCMS : Ikon.IKCMS.Library.Resources.IKCMS_ResourceType_PageBaseAbstract<IKCMS_ResourceType_PageCatalogCategoryCMS.WidgetSettingsType>, IKGD_Folder_Interface, IKCMS_Page_Interface, IKCMS_ArchiveRoot_Interface, IKCMS_IsIndexable_Interface
  {
    public override string IconEditor { get { return "CMS3.pagina_prodotto.gif"; } }


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
  // CMS pagina del catalogo
  // da usare per cataloghi con attributi estesi su database
  //
  [Description("Pagina Catalogo CMS (extended)")]
  public class IKCMS_ResourceType_PageCatalogExtendedCMS : Ikon.IKCMS.Library.Resources.IKCMS_ResourceType_PageBaseAbstract<IKCMS_ResourceType_PageCatalogExtendedCMS.WidgetSettingsType>, IKGD_Folder_Interface, IKCMS_BrowsableIndexable_Interface, IKCMS_PageWithPageEditor_Interface, IKCMS_Widget_Interface, IKCMS_IsIndexable_Interface
  {
    public override string IconEditor { get { return "CMS3.pagina_prodotto.gif"; } }
    public override string SearchTitleMember { get { return "TitleHead"; } }
    public override string SearchTextMember { get { return "TitleH1"; } }

    public class WidgetSettingsType : WidgetSettingsTypeBaseKVT
    {
      public string Code { get; set; }

      public new static WidgetSettingsType DefaultValue { get { return new WidgetSettingsType(); } }
      public WidgetSettingsType()
        : base()
      {
        Code = null;
      }
    }
    //
  }


  //
  // CMS pagina del catalogo semplice
  // da usare per cataloghi semplici senza la gestione di attributi estesi
  //
  [Description("Pagina Catalogo CMS")]
  public class IKCMS_ResourceType_PageCatalogCMS : Ikon.IKCMS.Library.Resources.IKCMS_ResourceType_PageBaseAbstract<IKCMS_ResourceType_PageCatalogCMS.WidgetSettingsType>, IKGD_Folder_Interface, IKCMS_BrowsableIndexable_Interface, IKCMS_PageWithPageEditor_Interface, IKCMS_Widget_Interface, IKCMS_IsIndexable_Interface
  {
    public override string IconEditor { get { return "CMS3.pagina_prodotto.gif"; } }
    public override string SearchTitleMember { get { return "TitleHead"; } }
    public override string SearchTextMember { get { return "TitleH1"; } }

    public class WidgetSettingsType : WidgetSettingsTypeBaseKVT
    {
      public string Code { get; set; }

      public new static WidgetSettingsType DefaultValue { get { return new WidgetSettingsType(); } }
      public WidgetSettingsType()
        : base()
      {
        Code = null;
      }
    }
  }




}