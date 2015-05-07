/*
 * 
 * Intranet Ikon
 * 
 * Copyright (C) 2013 Ikon Srl
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
using System.Text.RegularExpressions;
using System.Linq.Expressions;
using System.Reflection;
using System.Data.Linq.Mapping;
using System.Diagnostics;
using System.Linq.Dynamic;
using System.Transactions;
using System.Web.Caching;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;  // assembly System.ServiceModel.Web
using LinqKit;

using Ikon;
using Ikon.GD;
using Ikon.Log;


namespace Ikon.GD
{


  public static class IKGD_SiteMode
  {
    private static object _lock = new object();
    private static readonly string ParamNameSite = "IKGD_SiteMode";
    private static readonly string ParamNameView = "IKGD_ViewMode";
    private static readonly string ParamNameHash = "IKGD_SiteModeHash";
    private static readonly string ParamKeySite = "SiteMode";
    private static readonly string ParamKeyView = "ViewMode";
    private static readonly string ParamKeyDeviceDetected = "Device";
    private static List<SiteModeDataElement> _RegisteredModeDataSite { get; set; }
    public static IList<SiteModeDataElement> RegisteredModeDataSite { get { return _RegisteredModeDataSite; } }
    private static List<SiteModeDataElement> _RegisteredModeDataView { get; set; }
    public static IList<SiteModeDataElement> RegisteredModeDataView { get { return _RegisteredModeDataView; } }
    //
    public static Dictionary<string, string> DomainsMappingSite { get; private set; }
    public static Dictionary<string, string> DomainsMappingView { get; private set; }
    //
    public static readonly string ParamNameViewNoDetect = "IKGD_NoViewMode";  // nome della cookie per disabilitare la gestione del viewmode (es. da popup con JS). Non opera sulle forzature daterminate dal dominio o dai forcing
    //
    public static string SiteModeForced { get; set; }
    public static string SiteModeDefault { get; set; }
    public static string ViewModeForced { get; set; }
    public static string ViewModeDefault { get; set; }
    //
    public static SiteModeDataElement NullEntry { get; private set; }
    //
    //public static Dictionary<string, string> DomainsMapping { get; private set; }
    public static List<KeyValuePair<string, string>> DomainsMapping2 { get; private set; }
    //


    static IKGD_SiteMode()
    {
      NullEntry = new SiteModeDataElement(string.Empty);
      //
      // selezione del sottosito
      //
      _RegisteredModeDataSite = new List<SiteModeDataElement>();
      _RegisteredModeDataSite.Add(NullEntry);
      //
      // modalita' di visualizzazione del sito
      //
      _RegisteredModeDataView = new List<SiteModeDataElement>();
      _RegisteredModeDataView.Add(new SiteModeDataElement(string.Empty));
      _RegisteredModeDataView.Add(new SiteModeDataElement("phone", ".phone", ".mobi"));
      _RegisteredModeDataView.Add(new SiteModeDataElement("tablet", ".tablet", ".mobi"));
      _RegisteredModeDataView.Add(new SiteModeDataElement("mobile", ".mobi"));
      _RegisteredModeDataView.Add(new SiteModeDataElement("desktop", ".desktop"));  // non si sa mai...
      _RegisteredModeDataView.Add(new SiteModeDataElement("admin", ".admin"));  // per funzionalita' di admin
      _RegisteredModeDataView.Add(new SiteModeDataElement("fb", ".fb"));
      _RegisteredModeDataView.Add(new SiteModeDataElement("facebook", ".fb"));
      _RegisteredModeDataView.Add(new SiteModeDataElement("acc", ".acc"));
      _RegisteredModeDataView.Add(new SiteModeDataElement("accessible", ".acc"));
      //
      DomainsMappingSite = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
      DomainsMappingView = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
      //
      SiteModeForced = IKGD_Config.AppSettings["IKGD_SiteMode_ForcedSite"];
      ViewModeForced = IKGD_Config.AppSettings["IKGD_SiteMode_ForcedView"];
      //
      SiteModeDefault = IKGD_Config.AppSettings["IKGD_SiteMode_DefaultSite"];
      ViewModeDefault = IKGD_Config.AppSettings["IKGD_SiteMode_DefaultView"];
      //
      //DomainsMapping = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
      DomainsMapping2 = new List<KeyValuePair<string, string>>();
      try
      {
        foreach (var mapping in Utility.Explode(IKGD_Config.AppSettings["IKGD_SiteMode_DomainsMapping"], ", ", " ", true))
        {
          try
          {
            var frags = mapping.Split(":".ToCharArray(), 2);
            var fragsM = (frags.FirstOrDefault() ?? string.Empty).Split("|".ToCharArray(), 2);
            string siteMode = SiteModeDataElement.ExtensionNormalizer(fragsM.FirstOrDefault());
            string viewMode = SiteModeDataElement.ExtensionNormalizer(fragsM.Skip(1).FirstOrDefault());
            string mode = string.Format("{0}|{1}", siteMode, viewMode);
            string domain = frags.Skip(1).FirstOrDefault().TrimSafe().ToLower();
            if (domain.IsNotEmpty())
            {
              //DomainsMapping[domain] = mode;
              DomainsMapping2.Add(new KeyValuePair<string, string>(domain, mode));
            }
          }
          catch { }
        }
      }
      catch { }
      //
    }


    public static string SiteMode
    {

      get
      {
        lock (_lock)
        {
          string modeCode = SiteModeForced;
          if (modeCode == null)
          {
            try { modeCode = (HttpContext.Current.Request.QueryString[ParamNameSite] as string) ?? (HttpContext.Current.Items[ParamNameSite] as string); }
            catch { }
          }
          if (modeCode == null)
          {
            try
            {
              if (DomainsMappingSite.ContainsKey(HttpContext.Current.Request.Url.DnsSafeHost))
              {
                modeCode = DomainsMappingSite[HttpContext.Current.Request.Url.DnsSafeHost];
              }
              else
              {
                modeCode = UpdateDomainMapping(DomainsMappingSite, IKGD_Config.AppSettings["IKGD_SiteMode_DomainsMapperSite"], HttpContext.Current.Request.Url.DnsSafeHost);
              }
            }
            catch { }
          }
          if (modeCode == null)
          {
            modeCode = FS_OperationsHelpers.CustomSessionGet(ParamKeySite);
          }
          return modeCode ?? SiteModeDefault ?? string.Empty;
        }
      }

      set
      {
        lock (_lock)
        {
          string code = SiteModeDataElement.ExtensionNormalizer(value);
          string currentMode = SiteMode;
          var modeData = _RegisteredModeDataSite.FirstOrDefault(r => string.Equals(r.Mode, code, StringComparison.OrdinalIgnoreCase));
          if (modeData == null)
          {
            _RegisteredModeDataSite.Add(modeData = new SiteModeDataElement(code));
          }
          if (modeData != null && code != currentMode)
          {
            HttpContext.Current.Items.Remove(ParamNameHash);
            HttpContext.Current.Items[ParamNameSite] = modeData.Mode;
            FS_OperationsHelpers.CustomSessionSet(ParamKeySite, modeData.Mode);
          }
          //else
          //{
          //  ClearSite();
          //}
        }
      }

    }


    public static string ViewMode
    {

      get
      {
        lock (_lock)
        {
          string viewCode = ViewModeForced;
          if (viewCode == null)
          {
            try { viewCode = (HttpContext.Current.Request.QueryString[ParamNameView] as string) ?? (HttpContext.Current.Items[ParamNameView] as string); }
            catch { }
          }
          if (viewCode == null)
          {
            try
            {
              if (DomainsMappingView.ContainsKey(HttpContext.Current.Request.Url.DnsSafeHost))
              {
                viewCode = DomainsMappingView[HttpContext.Current.Request.Url.DnsSafeHost];
              }
              else
              {
                viewCode = UpdateDomainMapping(DomainsMappingView, IKGD_Config.AppSettings["IKGD_SiteMode_DomainsMapperView"], HttpContext.Current.Request.Url.DnsSafeHost);
              }
            }
            catch { }
          }
          if (viewCode == null && HttpContext.Current.Request.Cookies[ParamNameViewNoDetect] == null)
          {
            viewCode = FS_OperationsHelpers.CustomSessionGet(ParamKeyView);
          }
          return viewCode ?? ViewModeDefault ?? string.Empty;
        }
      }

      set
      {
        lock (_lock)
        {
          string code = SiteModeDataElement.ExtensionNormalizer(value);
          string currentMode = SiteMode;
          var modeData = _RegisteredModeDataView.FirstOrDefault(r => string.Equals(r.Mode, code, StringComparison.OrdinalIgnoreCase));
          if (modeData == null)
          {
            _RegisteredModeDataView.Add(modeData = new SiteModeDataElement(code));
          }
          if (modeData != null && code != currentMode)
          {
            HttpContext.Current.Items[ParamNameView] = modeData.Mode;
            FS_OperationsHelpers.CustomSessionSet(ParamKeyView, modeData.Mode);
          }
          //else
          //{
          //  ClearView();
          //}
        }
      }

    }

    public static string ModeCombined
    {
      get
      {
        string siteMode = SiteMode ?? string.Empty;
        string viewMode = ViewMode ?? string.Empty;
        return siteMode + "|" + viewMode;
      }
      set
      {
        var frags = (value ?? string.Empty).Split("|".ToCharArray(), 2);
        string siteMode = SiteMode;
        string viewMode = ViewMode;
        if (siteMode != frags.FirstOrDefault())
        {
          SiteMode = frags.FirstOrDefault();
        }
        if (viewMode != frags.Skip(1).FirstOrDefault())
        {
          ViewMode = frags.Skip(1).FirstOrDefault();
        }
      }
    }


    public static string SiteModeNotForced
    {
      get
      {
        lock (_lock)
        {
          string mode = FS_OperationsHelpers.CustomSessionGet(ParamKeySite);
          return mode ?? SiteModeDefault ?? string.Empty;
        }
      }
    }

    public static string ViewModeNotForced
    {
      get
      {
        lock (_lock)
        {
          string mode = FS_OperationsHelpers.CustomSessionGet(ParamKeyView);
          return mode ?? ViewModeDefault ?? string.Empty;
        }
      }
    }

    public static string ModeCombinedNotForced
    {
      get
      {
        string siteMode = SiteModeNotForced;
        string viewMode = ViewModeNotForced;
        return siteMode + "|" + viewMode;
      }
    }


    public static SiteModeDataElement SiteModeData
    {
      get
      {
        lock (_lock)
        {
          SiteModeDataElement data = null;
          try
          {
            string mode = SiteMode;
            data = _RegisteredModeDataSite.FirstOrDefault(r => string.Equals(r.Mode, mode, StringComparison.OrdinalIgnoreCase));
            if (data == null)
            {
              data = new SiteModeDataElement(mode);
              _RegisteredModeDataSite.Add(data);
            }
          }
          catch { }
          return data;
        }
      }
    }


    public static SiteModeDataElement ViewModeData
    {
      get
      {
        lock (_lock)
        {
          SiteModeDataElement data = null;
          try
          {
            string mode = ViewMode;
            data = _RegisteredModeDataView.FirstOrDefault(r => string.Equals(r.Mode, mode, StringComparison.OrdinalIgnoreCase));
            if (data == null)
            {
              data = new SiteModeDataElement(mode);
              _RegisteredModeDataView.Add(data);
            }
          }
          catch { }
          return data;
        }
      }
    }


    //
    // basta settare il valore (anche string.Empty) solo per disabilitare i check successivi sui device
    //
    public static string DeviceCookie
    {
      get
      {
        string status = null;
        try { status = HttpContext.Current.Items[ParamKeyDeviceDetected] as string; }
        catch { }
        if (status == null)
        {
          status = FS_OperationsHelpers.CustomSessionGet(ParamKeyDeviceDetected);
        }
        return status;
      }
      set
      {
        lock (_lock)
        {
          if (value == null)
          {
            HttpContext.Current.Items.Remove(ParamKeyDeviceDetected);
          }
          else
          {
            HttpContext.Current.Items[ParamKeyDeviceDetected] = value;
          }
          FS_OperationsHelpers.CustomSessionSet(ParamKeyDeviceDetected, value);
        }

      }
    }


    public static void ClearSite()
    {
      lock (_lock)
      {
        HttpContext.Current.Items.Remove(ParamNameHash);
        HttpContext.Current.Items.Remove(ParamNameSite);
        FS_OperationsHelpers.CustomSessionSet(ParamKeySite, null);
      }
    }


    public static void ClearView()
    {
      lock (_lock)
      {
        HttpContext.Current.Items.Remove(ParamNameView);
        FS_OperationsHelpers.CustomSessionSet(ParamKeyView, null);
      }
    }


    public static void ClearDevice()
    {
      lock (_lock)
      {
        HttpContext.Current.Items.Remove(ParamKeyDeviceDetected);
        FS_OperationsHelpers.CustomSessionSet(ParamKeyDeviceDetected, null);
      }
    }


    public static void Clear()
    {
      ClearSite();
      ClearView();
    }


    public static bool IsModeView(string mode)
    {
      string code = SiteModeDataElement.ExtensionNormalizer(mode);
      return (code == ViewMode);
    }


    public static bool IsModeSite(string mode)
    {
      string code = SiteModeDataElement.ExtensionNormalizer(mode);
      return (code == SiteMode);
    }


    public static bool HasExtMode(string ext)
    {
      string extN = SiteModeDataElement.ExtensionNormalizer(ext);
      if (extN.IsNotEmpty())
      {
        extN = "." + extN;
      }
      return SiteModeExtList.Contains(extN);
    }


    public static bool HasExtView(string ext)
    {
      string extN = SiteModeDataElement.ExtensionNormalizer(ext);
      if (extN.IsNotEmpty())
      {
        extN = "." + extN;
      }
      return ViewModeExtList.Contains(extN);
    }


    public static string SiteModeExt { get { return (SiteModeData ?? NullEntry).Extension; } }
    public static IList<string> SiteModeExtList { get { return (SiteModeData ?? NullEntry).ExtensionsList; } }


    public static string ViewModeExt { get { return (ViewModeData ?? NullEntry).Extension; } }
    public static IList<string> ViewModeExtList { get { return (ViewModeData ?? NullEntry).ExtensionsList; } }

    public static string ViewModeExtOfAllowed(params string[] allowedExts)
    {
      string ext = ViewModeExt;
      if (allowedExts != null && allowedExts.Any(r => string.Equals(r, ext, StringComparison.OrdinalIgnoreCase)))
        return ext;
      return string.Empty;
    }


    public static IList<string> GetVfsExtList
    {
      get
      {
        //var ext1 = (SiteModeData ?? NullEntry).ExtensionsList.DefaultIfEmpty(string.Empty);
        //var ext2 = (ViewModeData ?? NullEntry).ExtensionsList.DefaultIfEmpty(string.Empty);
        var ext1 = (SiteModeData ?? NullEntry).ExtensionsList.Concat(new string[] { string.Empty }).Distinct();
        var ext2 = (ViewModeData ?? NullEntry).ExtensionsList.Concat(new string[] { string.Empty }).Distinct();
        var exts =
          (from e1 in ext1
           from e2 in ext2
           select e1 + e2).Distinct().Where(s => s.IsNotEmpty()).ToList();
        return exts;
      }
    }


    public static IList<string> GetVfsExtListWithEmpty
    {
      get
      {
        return GetVfsExtList.Concat(new string[] { string.Empty }).Distinct().ToList();
      }
    }


    public static bool IsForcedModeSite() { return IsForcedModeSite(null); }
    public static bool IsForcedModeSite(bool? chechForCookie)
    {
      lock (_lock)
      {
        string modeCode = SiteModeForced;
        if (modeCode.IsNotEmpty())
          return true;
        try { modeCode = (HttpContext.Current.Request.QueryString[ParamNameSite] as string) ?? (HttpContext.Current.Items[ParamNameSite] as string); }
        catch { }
        if (modeCode.IsNotEmpty())
          return true;
        try
        {
          if (DomainsMappingSite.ContainsKey(HttpContext.Current.Request.Url.DnsSafeHost))
          {
            modeCode = DomainsMappingSite[HttpContext.Current.Request.Url.DnsSafeHost];
          }
          else
          {
            modeCode = UpdateDomainMapping(DomainsMappingSite, IKGD_Config.AppSettings["IKGD_SiteMode_DomainsMapperSite"], HttpContext.Current.Request.Url.DnsSafeHost);
          }
        }
        catch { }
        if (modeCode.IsNotEmpty())
          return true;
        if (chechForCookie.GetValueOrDefault(true))
        {
          modeCode = FS_OperationsHelpers.CustomSessionGet(ParamKeySite);
          if (modeCode.IsNotEmpty())
            return true;
        }
        //if (SiteModeDefault.IsNotEmpty())
        //  return true;
      }
      return false;
    }


    public static bool IsForcedModeView() { return IsForcedModeView(null); }
    public static bool IsForcedModeView(bool? chechForCookie)
    {
      lock (_lock)
      {
        string modeCode = ViewModeForced;
        if (modeCode.IsNotEmpty())
          return true;
        try { modeCode = (HttpContext.Current.Request.QueryString[ParamNameView] as string) ?? (HttpContext.Current.Items[ParamNameView] as string); }
        catch { }
        if (modeCode.IsNotEmpty())
          return true;
        try
        {
          if (DomainsMappingView.ContainsKey(HttpContext.Current.Request.Url.DnsSafeHost))
          {
            modeCode = DomainsMappingView[HttpContext.Current.Request.Url.DnsSafeHost];
          }
          else
          {
            modeCode = UpdateDomainMapping(DomainsMappingView, IKGD_Config.AppSettings["IKGD_SiteMode_DomainsMapperView"], HttpContext.Current.Request.Url.DnsSafeHost);
          }
        }
        catch { }
        if (modeCode.IsNotEmpty())
          return true;
        if (chechForCookie.GetValueOrDefault(true))
        {
          if (HttpContext.Current.Request.Cookies[ParamNameViewNoDetect] == null)
          {
            modeCode = FS_OperationsHelpers.CustomSessionGet(ParamKeyView);
            if (modeCode.IsNotEmpty())
              return true;
          }
        }
        //if (ViewModeDefault.IsNotEmpty())
        //  return true;
      }
      return false;
    }


    public static string GetSiteHash
    {
      get
      {
        string hash = HttpContext.Current.Items[ParamNameHash] as string;
        if (hash == null)
        {
          string mode = SiteMode;
          int idx = _RegisteredModeDataSite.FindIndex(r => r.Mode == mode) + 1;
          HttpContext.Current.Items[ParamNameHash] = (hash = idx.ToString());
        }
        return hash;
      }
    }


    private static string UpdateDomainMapping(Dictionary<string, string> mapper, string configData, string domain)
    {
      string code = null;
      try
      {
        foreach (var mapping in Utility.Explode(configData, " ", null, true))
        {
          try
          {
            var frags = mapping.Split(":".ToCharArray(), 2);
            string mode = SiteModeDataElement.ExtensionNormalizer(frags.FirstOrDefault());
            string rxDomain = frags.Skip(1).FirstOrDefault().TrimSafe();
            if (string.Equals(domain, rxDomain, StringComparison.OrdinalIgnoreCase))
            {
              code = mode;
              break;
            }
            else if (rxDomain.IsNotNullOrWhiteSpace())
            {
              if (Regex.IsMatch(domain, rxDomain, RegexOptions.Singleline | RegexOptions.IgnoreCase))
              {
                code = mode;
                break;
              }
            }
          }
          catch { }
        }
      }
      catch { }
      mapper[domain] = code;
      return code;
    }


    public static List<string> GetForcingDomainsForMode(string siteMode, string viewMode)
    {
      string combinedMode = string.Format("{0}|{1}", siteMode, viewMode);
      return GetForcingDomainsForMode(combinedMode);
    }
    public static List<string> GetForcingDomainsForMode(string combinedMode)
    {
      //return DomainsMapping.Where(r => string.Equals(r.Value, combinedMode, StringComparison.OrdinalIgnoreCase)).Select(r => r.Key).ToList();
      return DomainsMapping2.Where(r => string.Equals(r.Value, combinedMode, StringComparison.OrdinalIgnoreCase)).Select(r => r.Key).ToList();
    }


    public static string GetConfig4SiteMode(string configData)
    {
      try
      {
        var items = Utility.Explode(configData, "; ", " ", true).Select(f =>
        {
          string key = null;
          string value = f;
          var frags = f.Split(":".ToCharArray(), 2);
          if (frags.Length == 2)
          {
            key = SiteModeDataElement.ExtensionNormalizer(frags.FirstOrDefault());
            value = frags.Skip(1).FirstOrDefault().TrimSafe();
          }
          return new { key, value };
        }).ToList();
        string currentMode = SiteMode;
        var item = items.FirstOrDefault(r => r.key == currentMode) ?? items.FirstOrDefault(r => r.key == null) ?? items.FirstOrDefault();
        if (item != null)
        {
          return item.value;
        }
      }
      catch { }
      return configData;
    }


    public static List<T> GetConfig4SiteMode<T>(string configData, string separator)
    {
      string configStr = GetConfig4SiteMode(configData);
      return Utility.ExplodeT<T>(configStr, separator, " ", true);
    }


    public static bool IsAccessible
    {
      get
      {
        switch (IKGD_SiteMode.ViewMode)
        {
          case "accessible":
          case "acc":
            return true;
        }
        return false;
      }
    }

    public static bool IsTargetSupported { get { return Utility.TryParse<bool>(IKGD_Config.AppSettings["EnableLinksTarget"], true) && (IsAccessible == false); } }



    //
    // TODO:
    // aggiungere helpers per la gestione delle configurazioni associate al site
    // per agevolare il parsing delle info relative ai nodes (roots varie, lucene, ecc)
    // per lo storage di lucene provare ad usare il site messo nella stringa con un {0}
    // aggiungere il supporto per GetSiteHash negli helper VFS per le cachingKeys
    //


    public class SiteModeDataElement
    {
      public string Mode { get; protected set; }
      public string Extension { get { return ExtensionsList.FirstOrDefault() ?? string.Empty; } }
      public IList<string> ExtensionsList { get; protected set; }


      // la lista di estensioni puo' anche essere assente o con elementi privi di punto
      public SiteModeDataElement(string mode, params string[] extensionsList)
      {
        this.Mode = ExtensionNormalizer(mode ?? string.Empty);
        ExtensionsList = (extensionsList ?? new string[] { mode }).DefaultIfEmpty(mode).Select(s => ExtensionNormalizer(s)).Where(s => s.IsNotNullOrWhiteSpace()).Select(s => "." + s).Distinct().ToList();
      }


      public override int GetHashCode()
      {
        return (Mode ?? string.Empty).GetHashCode();
      }


      public static string ExtensionNormalizer(string extStr)
      {
        return Regex.Replace(extStr ?? string.Empty, @"\W", string.Empty, RegexOptions.IgnoreCase | RegexOptions.Singleline).ToLowerInvariant();
      }


    }


  }

}
