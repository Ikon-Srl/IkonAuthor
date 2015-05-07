/*
 * 
 * Intranet Ikon
 * 
 * Copyright (C) 2009 Ikon Srl
 * Tutti i Diritti Riservati. All Rights Reserved
 * 
 */


using System;
using System.Data;
using System.Configuration;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.UI;
using System.Web.Security;
using System.Xml.Linq;
using System.Data.Linq;
using System.IO;
using System.Text;
using System.Linq.Expressions;
using System.Threading;
using System.Reflection;
using System.Data.Linq.Mapping;
using System.Diagnostics;
using System.Linq.Dynamic;
using System.Transactions;
using System.Web.Caching;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;  // assembly System.ServiceModel.Web
using System.Globalization;
using LinqKit;

using Ikon;
using Ikon.GD;
using Ikon.Log;


namespace Ikon.IKCMS
{


  //
  // TODO: eliminare alla fine del refactoring
  //
  /*
  public static class IKCMS_SiteViewMode_ORIG
  {
    private static object _lock = new object();
    private static readonly string ParamName = "IKCMS_ViewMode";
    private static readonly string ParamKey = "ViewMode";
    private static List<ViewModeData> _RegisteredViewModeData { get; set; }
    public static IList<ViewModeData> RegisteredViewModeData { get { return _RegisteredViewModeData; } }
    public static Dictionary<string, string> DomainsMapping { get; private set; }


    static IKCMS_SiteViewMode_ORIG()
    {
      _RegisteredViewModeData = new List<ViewModeData>()
      {
        new ViewModeData("Accessible", ".acc"),
        new ViewModeData("Mobile", ".mobi"),
        new ViewModeData("Tablet", ".tab"),
        new ViewModeData("Facebook", ".fb")
      };
      DomainsMapping = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
      try
      {
        foreach (var mapping in Utility.Explode(IKGD_Config.AppSettings["ViewMode_DomainsMapper"], ", ", " ", true))
        {
          try
          {
            string mode = mapping.Split(':').FirstOrDefault().TrimSafe().ToLower();
            string domain = mapping.Split(":".ToCharArray(), 2).Skip(1).FirstOrDefault().TrimSafe().ToLower();
            if (domain.IsNotEmpty())
            {
              DomainsMapping[domain] = mode;
            }
          }
          catch { }
        }
      }
      catch { }
    }


    public static void Clear()
    {
      lock (_lock)
      {
        //HttpContext.Current.Items.Remove(CookieName);
        //Utility.CookieRemoveFromCurrentRequest(CookieName);
        //Utility.CookieRemove(CookieName);
        FS_OperationsHelpers.CustomSessionSet(ParamKey, null);
      }
    }


    public static ViewModeData SetMode(string mode)
    {
      lock (_lock)
      {
        var modeData = _RegisteredViewModeData.FirstOrDefault(r => string.Equals(r.Mode, mode, StringComparison.OrdinalIgnoreCase));
        if (modeData != null && modeData != ModeNotForced)
        {
          //HttpContext.Current.Items[CookieName] = modeData.Mode;
          //Utility.CookieUpdateKeyValue(CookieName, CookieKey, modeData.Mode, true);
          FS_OperationsHelpers.CustomSessionSet(ParamKey, modeData.Mode);
        }
        else
          Clear();
        return Mode;
      }
    }


    public static ViewModeData Mode
    {
      get
      {
        lock (_lock)
        {
          string modeCode = null;
          try
          {
            if (DomainsMapping.ContainsKey(HttpContext.Current.Request.Url.DnsSafeHost))
            {
              modeCode = DomainsMapping[HttpContext.Current.Request.Url.DnsSafeHost];
            }
          }
          catch { }
          modeCode = modeCode ?? (HttpContext.Current.Request.QueryString[ParamName] as string) ?? (HttpContext.Current.Items[ParamName] as string);
          if (modeCode == null)
          {
            //modeCode = HttpContext.Current.Request.Cookies.Value(CookieName, CookieKey);
            modeCode = FS_OperationsHelpers.CustomSessionGet(ParamKey);
          }
          return _RegisteredViewModeData.FirstOrDefault(r => string.Equals(r.Mode, modeCode, StringComparison.OrdinalIgnoreCase));
        }
      }
    }


    public static ViewModeData ModeNotForced
    {
      get
      {
        lock (_lock)
        {
          string modeCode = (HttpContext.Current.Request.QueryString[ParamName] as string) ?? (HttpContext.Current.Items[ParamName] as string);
          if (modeCode == null)
          {
            //modeCode = HttpContext.Current.Request.Cookies.Value(CookieName, CookieKey);
            modeCode = FS_OperationsHelpers.CustomSessionGet(ParamKey);
          }
          return _RegisteredViewModeData.FirstOrDefault(r => string.Equals(r.Mode, modeCode, StringComparison.OrdinalIgnoreCase));
        }
      }
    }


    public static string ModeString
    {
      get
      {
        ViewModeData modeView = Mode;
        return modeView != null ? modeView.Mode : null;
      }
    }


    public static string ModeStringNN
    {
      get
      {
        ViewModeData modeView = Mode;
        return modeView != null ? modeView.Mode : string.Empty;
      }
    }


    public static string ModeExt
    {
      get
      {
        ViewModeData modeView = Mode;
        return modeView != null ? modeView.ViewNameSubExtension : string.Empty;
      }
    }


    public static bool IsViewMode(string mode)
    {
      ViewModeData modeView = Mode;
      if (string.IsNullOrEmpty(mode) && modeView == null)
        return true;
      return modeView != null && string.Equals(modeView.Mode, mode, StringComparison.OrdinalIgnoreCase);
    }


    public static bool IsModeForced
    {
      get
      {
        try { return DomainsMapping.ContainsKey(HttpContext.Current.Request.Url.DnsSafeHost); }
        catch { return false; }
      }
    }


    public static List<string> GetForcingDomainsForMode(string modeString)
    {
      return DomainsMapping.Where(r => string.Equals(r.Value, modeString, StringComparison.OrdinalIgnoreCase)).Select(r => r.Key).ToList();
    }


    public static bool IsAccessible { get { return IsViewMode("Accessible"); } }


    public static bool IsTargetSupported { get { return Utility.TryParse<bool>(IKGD_Config.AppSettings["EnableLinksTarget"], true) && (IsViewMode("Accessible") == false); } }



    public class ViewModeData
    {
      public string Mode { get; protected set; }
      public string ViewNameSubExtension { get; protected set; }  // es. ".acc" per trsformare Index.spark in Index.acc.spark

      public ViewModeData(string mode, string viewNameSubExtension)
      {
        this.Mode = (mode ?? string.Empty).ToLowerInvariant();
        this.ViewNameSubExtension = viewNameSubExtension;
      }

    }

  }
  */

}
