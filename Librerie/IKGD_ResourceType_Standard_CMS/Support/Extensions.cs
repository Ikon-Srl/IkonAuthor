/*
 * 
 * IkonPortal
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
using LinqKit;

using Ikon;
using Ikon.Log;
using Ikon.Support;
using Ikon.GD;
using Ikon.IKCMS;
using Ikon.IKCMS.Library.Resources;


namespace Ikon.IKGD.Library.Resources
{

  public static class ResourcesExtensionsCollectionLinks
  {


    public static bool HasLinkUrlFromResourceSettings(this WidgetSettingsType_Url_Interface obj)
    {
      try
      {
        var data = obj as WidgetSettingsType_Url_Interface;
        if (!string.IsNullOrEmpty(data.LinkUrl))
          return data.LinkUrl.Trim().Length > 0;
        else if (data.Link_sNode != null || data.Link_rNode != null)
          return true;
      }
      catch { }
      return false;
    }


    public static string GetUrlFromResourceSettings(this WidgetSettingsType_Url_Interface obj, bool resolveUrl) { return GetUrlFromResourceSettings(obj, null , false, resolveUrl); }
    public static string GetUrlFromResourceSettings(this WidgetSettingsType_Url_Interface obj, string language, bool mapOutcomingUrl, bool resolveUrl)
    {
      string LinkUrl = null;
      try
      {
        var data = obj as WidgetSettingsType_Url_Interface;
        if (!string.IsNullOrEmpty(data.LinkUrl))
        {
          LinkUrl = data.LinkUrl.Trim();
          if (resolveUrl)
            LinkUrl = Utility.ResolveUrl(LinkUrl);
        }
        if (mapOutcomingUrl && LinkUrl.IsNullOrEmpty())
        {
          // attenzione che puo' essere pesante se chiamata in una view e non per risirse che poi vengono fruite da cache
          LinkUrl = IKGD_SEO_Manager.MapOutcomingUrl(data.Link_sNode, data.Link_rNode, language);
        }
        if (LinkUrl.IsNullOrEmpty())
        {
          if (data.Link_rNode != null)
          {
            LinkUrl = IKCMS_RouteUrlManager.GetMvcUrlGeneralRNODEV2(language, data.Link_rNode.Value, null, null, false, new int?[] { data.Link_sNode }.Where(n => n != null).Select(n => n.Value).ToArray());
            //LinkUrl = IKCMS_RouteUrlManager.GetMvcUrlGeneralRNODE_WithHints(data.Link_rNode.Value, new int?[] { data.Link_sNode }.Where(n => n != null).Select(n => n.Value).ToArray());
          }
          else if (data.Link_sNode != null)
          {
            LinkUrl = IKCMS_RouteUrlManager.GetMvcUrlGeneralV2(language, data.Link_sNode.Value, null, null, false);
            //LinkUrl = IKCMS_RouteUrlManager.GetMvcUrlGeneral(data.Link_sNode.Value);
          }
        }
      }
      catch { }
      return LinkUrl;
    }


    public static string GetUrlTargetFromResourceSettings(this WidgetSettingsType_Url_Interface obj)
    {
      string target = null;
      try
      {
        if (obj is WidgetSettingsType_FullUrl_Interface)
          target = (obj as WidgetSettingsType_FullUrl_Interface).LinkTarget;
        if (!string.IsNullOrEmpty(target))
          return target;
      }
      catch { }
      return null;
    }


  }


}

