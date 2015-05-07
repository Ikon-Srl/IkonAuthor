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


  // eredita anche da IKCMS_PageWithOutFolder_Interface perche' viene usato per pagine senza folder
  [Description("Elemento")]
  public class IKCMS_ResourceType_BrickCMS : IKCMS_ResourceType_GenericBrickBase<IKCMS_ResourceType_BrickCMS.WidgetSettingsType>, IKCMS_PageWithOutFolder_Interface, IKCMS_ResourceWithViewer_Interface
  {
    public override string IconEditor { get { return "ResourceType.Html.gif"; } }

    public class WidgetSettingsType : WidgetSettingsTypeGenericBrickBase
    {
      public new static WidgetSettingsType DefaultValue { get { return new WidgetSettingsType(); } }
      public WidgetSettingsType() : base() { }
    }
  }


  [Description("File")]
  public class IKCMS_ResourceType_FileCMS : IKCMS_ResourceType_GenericBrickBase<IKCMS_ResourceType_FileCMS.WidgetSettingsType>, IKCMS_PageWithOutFolder_Interface, IKCMS_ResourceWithViewer_Interface, IKCMS_BrickWithPlaceholder_Interface
  {
    public override string IconEditor { get { return "CMS3.documento.gif"; } }

    public class WidgetSettingsType : WidgetSettingsTypeGenericBrickBase
    {
      public new static WidgetSettingsType DefaultValue { get { return new WidgetSettingsType(); } }
      public WidgetSettingsType() : base() { }
    }
  }


  //
  // i widget non sono marcati come aggregabili con un visualizzatore
  //
  [Description("Widget")]
  public class IKCMS_ResourceType_BrickWidgetGeneric : IKCMS_ResourceType_BrickWidgetBase<IKCMS_ResourceType_BrickWidgetGeneric.WidgetSettingsType>, IKCMS_BrickWithPlaceholder_Interface
  {
    public override string IconEditor { get { return "CMS3.widget_teaser.gif"; } }

    public class WidgetSettingsType : WidgetSettingsTypeGenericBrickBase
    {
      public new static WidgetSettingsType DefaultValue { get { return new WidgetSettingsType(); } }
      public WidgetSettingsType() : base() { }
    }
  }


  //
  // i widget non sono marcati come aggregabili con un visualizzatore
  //
  [Description("Visualizzatore di teaser a Tag")]
  // elemento migrato da IKCMS_ResourceType_BrickWidgetGeneric a IKCMS_ResourceType_TeaserTag
  public class IKCMS_ResourceType_TeaserTag : IKCMS_ResourceType_BrickWidgetBase<IKCMS_ResourceType_TeaserTag.WidgetSettingsType>, IKCMS_BrickWithPlaceholder_Interface
  {
    public override string IconEditor { get { return "CMS3.widget_teaser.gif"; } }

    public class WidgetSettingsType : WidgetSettingsTypeGenericBrickBase
    {
      public new static WidgetSettingsType DefaultValue { get { return new WidgetSettingsType(); } }
      public WidgetSettingsType() : base() { }
    }
  }



  //
  // risorsa tipo paragrafo CMS
  //
  [Description("Paragrafo")]
  public class IKCMS_ResourceType_ParagraphKVT : IKCMS_ResourceType_GenericBrickBase<IKCMS_ResourceType_ParagraphKVT.WidgetSettingsType>, IKCMS_BrickWithPlaceholder_Interface
  {
    public override string IconEditor { get { return "ResourceType.Html.gif"; } }

    public class WidgetSettingsType : WidgetSettingsTypeGenericBrickBase
    {
      public new static WidgetSettingsType DefaultValue { get { return new WidgetSettingsType(); } }
      public WidgetSettingsType() : base() { }
    }
  }


  //
  // risorsa tipo link
  //
  [Description("Link")]
  public class IKCMS_ResourceType_LinkKVT : IKCMS_ResourceType_GenericBrickBase<IKCMS_ResourceType_LinkKVT.WidgetSettingsType>
  {
    public override string IconEditor { get { return "ResourceType.Contatti.gif"; } }

    public class WidgetSettingsType : WidgetSettingsTypeGenericBrickBase
    {
      public new static WidgetSettingsType DefaultValue { get { return new WidgetSettingsType(); } }
      public WidgetSettingsType() : base() { }
    }
  }


  //
  // risorsa tipo elemento contatto
  //
  [Description("Contatto")]
  public class IKCMS_ResourceType_ContactKVT : IKCMS_ResourceType_GenericBrickBase<IKCMS_ResourceType_ContactKVT.WidgetSettingsType>, IKCMS_BrickWithPlaceholder_Interface
  {
    public override string IconEditor { get { return "ResourceType.Contatti.gif"; } }

    public class WidgetSettingsType : WidgetSettingsTypeGenericBrickBase
    {
      public new static WidgetSettingsType DefaultValue { get { return new WidgetSettingsType(); } }
      public WidgetSettingsType() : base() { }
    }
  }


  //
  // risorsa tipo elemento showreel CMS
  //
  [Description("ShowReel")]
  public class IKCMS_ResourceType_ShowReelElementV1 : IKCMS_ResourceType_GenericBrickBase<IKCMS_ResourceType_ShowReelElementV1.WidgetSettingsType>, IKCMS_BrickCollectable_Interface, IKCMS_BrickWithPlaceholder_Interface
  {
    public override string IconEditor { get { return "ResourceType.TeaserElement.gif"; } }   //TODO:ICONA

    public class WidgetSettingsType : WidgetSettingsTypeGenericBrickBase, WidgetSettingsType_FullUrlOnKVT_Interface
    {
      public new static WidgetSettingsType DefaultValue { get { return new WidgetSettingsType(); } }
      public WidgetSettingsType() : base() { }
    }
  }


  //
  // risorsa tipo elemento teaser
  // per inclusione nelle collezioni di teaser o eventualmentre per utilizzo diretto
  //
  [Description("Teaser")]
  public class IKCMS_ResourceType_TeaserElementKVT : IKCMS_ResourceType_GenericBrickBase<IKCMS_ResourceType_TeaserElementKVT.WidgetSettingsType>, IKCMS_BrickCollectable_Interface, IKCMS_BrickTeaser_Interface
  {
    public override string IconEditor { get { return "CMS3.teaser.gif"; } }

    public class WidgetSettingsType : WidgetSettingsTypeGenericBrickBase
    {
      public new static WidgetSettingsType DefaultValue { get { return new WidgetSettingsType(); } }
      public WidgetSettingsType() : base() { }
    }
  }


  //
  // Modulo CMS per la gestione delle collezioni di teaser
  //
  [Description("Visualizzatore di teaser")]
  public class IKCMS_ResourceType_TeaserCollection : IKCMS_ResourceType_GenericBrickBase<IKCMS_ResourceType_TeaserCollection.WidgetSettingsType>, IKCMS_BrickCollector_Interface, IKCMS_BrickWithPlaceholder_Interface
  {
    public override string IconEditor { get { return "CMS3.widget_teaser.gif"; } }

    public class WidgetSettingsType : WidgetSettingsTypeGenericBrickBase
    {
      public new static WidgetSettingsType DefaultValue { get { return new WidgetSettingsType(); } }
      public WidgetSettingsType() : base() { }
    }
  }


  //
  // Modulo CMS per la gestione delle collezioni di documenti
  //
  [Description("Visualizzatore di allegati")]
  public class IKCMS_ResourceType_DocumentCollection : IKCMS_ResourceType_GenericBrickBase<IKCMS_ResourceType_DocumentCollection.WidgetSettingsType>, IKCMS_BrickWithPlaceholder_Interface
  {
    public override string IconEditor { get { return "CMS3.widget_teaser.gif"; } }

    public class WidgetSettingsType : WidgetSettingsTypeGenericBrickBase
    {
      public new static WidgetSettingsType DefaultValue { get { return new WidgetSettingsType(); } }
      public WidgetSettingsType() : base() { }
    }
  }


}