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

using Ikon;
using Ikon.Log;
using Ikon.Support;
using Ikon.GD;


namespace Ikon.IKCMS.Library.Resources
{
  using Ikon.IKGD.Library.Resources;


  [Description("Widget Sondaggio")]
  public class IKCMS_ResourceType_ESurvey : IKCMS_ResourceType_GenericBrickBase<IKCMS_ResourceType_ESurvey.WidgetSettingsType>, IKCMS_BrickWithPlaceholder_Interface
  {
    public override string IconEditor { get { return "ResourceType.Poll.gif"; } }

    //
    // WidgetSettings
    //
    // fare riferimento all'uso di FlagsMenuEnum
    [Flags]
    public enum ESurveyDisplayModeEnum {
      [Description("Conteggio dei voti visibile")]
      PollCountVisible = 1 << 0,
      [Description("Risultato dei voti visibile")]
      PollResultsVisible = 1 << 1,
      [Description("Campo per il testo libero")]
      FreeTextInput = 1 << 2,
      [Description("Modifica di una votazione già registrata non consentita")]
      MultiPollDenied = 1 << 3
    }

    public class WidgetSettingsType : WidgetSettingsTypeGenericBrickBase
    {
      public new string Title { get; set; }
      public new string Text { get; set; }
      public int MinAnswers { get; set; }
      public int MaxAnswers { get; set; }
      public ESurveyDisplayModeEnum ESurveyDisplayMode { get; set; }
      public Utility.DictionaryMV<int, string> Answers { get; set; }
      //
      public new static WidgetSettingsType DefaultValue { get { return new WidgetSettingsType(); } }
      public WidgetSettingsType()
        : base()
      {
        Title = null;
        Text = null;
        MinAnswers = 1;
        MaxAnswers = 1;
        Answers = new Utility.DictionaryMV<int, string>();
      }
    }
    //
  }




}