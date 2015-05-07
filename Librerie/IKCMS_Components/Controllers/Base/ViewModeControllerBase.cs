using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using System.Net.Mail;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Transactions;
using System.Web;
using System.Web.Mvc;
using System.Web.Mvc.Ajax;
using System.Web.Security;
using System.Reflection;
using Newtonsoft.Json;
using Autofac;

using Ikon;
using Ikon.Auth.Login;
using Ikon.GD;
using Ikon.IKCMS;
using Ikon.IKGD.Library.Resources;
using Ikon.IKCMS.Library.Resources;



namespace Ikon.IKCMS
{


  [Microsoft.Web.Mvc.ControllerSessionState(Microsoft.Web.Mvc.ControllerSessionState.ReadOnly)]
  public abstract class ViewModeControllerBase : VFS_Access_Controller
  {
    //
    public static readonly Regex AutoDetectDeviceCheckForMobileRegEx = (IKGD_Config.AppSettings["IKGD_SiteMode_AutoDetectDeviceCheckForMobileRegEx"].IsNullOrWhiteSpace() ? null : new Regex(IKGD_Config.AppSettings["IKGD_SiteMode_AutoDetectDeviceCheckForMobileRegEx"], RegexOptions.IgnoreCase | RegexOptions.Singleline | RegexOptions.Compiled));
    public const string DeviceDetectedKey = "DeviceDetectedKey";
    //


    public virtual ActionResult Get()
    {
      return Content(IKGD_SiteMode.ModeCombined);
    }

    public virtual ActionResult GetSiteMode()
    {
      return Content(IKGD_SiteMode.SiteMode);
    }

    public virtual ActionResult GetViewMode()
    {
      return Content(IKGD_SiteMode.ViewMode);
    }


    public virtual ActionResult Clear()
    {
      IKGD_SiteMode.Clear();
      //
      if (Request.UrlReferrer != null)
        return Redirect(Request.UrlReferrer.ToString());
      return null;
    }

    public virtual ActionResult ClearSiteMode()
    {
      IKGD_SiteMode.ClearSite();
      //
      if (Request.UrlReferrer != null)
        return Redirect(Request.UrlReferrer.ToString());
      return null;
    }

    public virtual ActionResult ClearViewMode()
    {
      IKGD_SiteMode.ClearView();
      //
      if (Request.UrlReferrer != null)
        return Redirect(Request.UrlReferrer.ToString());
      return null;
    }


    public virtual ActionResult Set(string SiteMode, string ViewMode, bool? allowRedirect, string pathAndQuery)
    {
      bool reload = true;
      // se un browser/crawler non supporta le cookie concediamo solo di resettare il ViewMode
      if ((string.IsNullOrEmpty(SiteMode) || string.IsNullOrEmpty(ViewMode)))
      {
        bool IsCookieLess = (Request.Browser.Crawler == true || Request.Browser.Cookies == false);
        if (IsCookieLess == false)
        {
          if (SiteMode != null)
            IKGD_SiteMode.SiteMode = SiteMode;
          if (ViewMode != null)
            IKGD_SiteMode.ViewMode = ViewMode;
        }
        if ((allowRedirect ?? VFS_Access_Controller.AutoDetectDeviceAllowRedirectEnabled).GetValueOrDefault(false))
        {
          reload &= !ProcessSiteModeRedirect(IKGD_SiteMode.ModeCombinedNotForced, pathAndQuery ?? ((Request.UrlReferrer != null && string.Equals(Request.UrlReferrer.DnsSafeHost, Request.Url.DnsSafeHost, StringComparison.OrdinalIgnoreCase)) ? Request.UrlReferrer.PathAndQuery : null));
        }
      }
      if (reload && Request.UrlReferrer != null)
      {
        return Redirect(Request.UrlReferrer.ToString());
      }
      return null;
    }

    public virtual ActionResult SetSiteMode(string id, string pathAndQuery)
    {
      return Set(id, null, true, pathAndQuery);
    }

    public virtual ActionResult SetViewMode(string id)
    {
      return Set(null, id, true, null);
    }


    public virtual ActionResult SetAccessible() { return Set(null, "accessible", null, null); }
    public virtual ActionResult SetMobile() { return Set(null, "mobile", null, null); }
    public virtual ActionResult SetPhone() { return Set(null, "phone", null, null); }
    public virtual ActionResult SetTablet() { return Set(null, "tablet", null, null); }
    public virtual ActionResult SetFacebook() { return Set(null, "facebook", null, null); }
    public virtual ActionResult SetAdmin() { return Set(null, "admin", null, null); }
    public virtual ActionResult SetDesktop() { return Set(null, "desktop", null, null); }



    //
    // check for mobile or tablet with custom handlers
    // and autoset of ViewMode and domain redirect
    //
    public static bool AutoDetectDeviceV1(Func<string> customDetectSite, Func<string> customDetectView) { return AutoDetectDeviceV1(customDetectSite, customDetectView, null, null); }
    public static bool AutoDetectDeviceV1(Func<string> customDetectSite, Func<string> customDetectView, bool? checkForMobileDevices, bool? allowRedirect)
    {
      bool result = false;
      try
      {
        if (System.Web.HttpContext.Current.Request.Cookies[IKGD_SiteMode.ParamNameViewNoDetect] != null || IKGD_SiteMode.DeviceCookie != null)
        {
          return result;
        }
        //
        bool processRedirects = false;
        //
        string detectedSite = null;
        if (!IKGD_SiteMode.IsForcedModeSite())
        {
          if (customDetectSite != null)
          {
            try { detectedSite = customDetectSite(); }
            catch { }
          }
          //
          if (detectedSite != null)
          {
            IKGD_SiteMode.SiteMode = detectedSite;
            processRedirects |= true;
          }
        }
        //
        string detectedDevice = null;
        string detectedView = null;
        if (!IKGD_SiteMode.IsForcedModeView())
        {
          if (customDetectView != null)
          {
            try { detectedView = customDetectView(); }
            catch { }
          }
          //
          // se il custom code non ha individuato un device si procede con il test standard nel caso sia attivo il detect sui devices mobile
          //
          if (detectedView == null && checkForMobileDevices.GetValueOrDefault(false))
          {
            ////if (MobileHelper.DeviceIsMobile)
            //if (MobileHelper.IsMobileDevice(null, true, true))
            //{
            //  detectedView = "mobile";
            //  bool isTablet = MobileHelper.IsMobileDevice(null, false, true);
            //  if (isTablet)
            //  {
            //    detectedView = "tablet";
            //  }
            //  else
            //  {
            //    detectedView = "phone";
            //  }
            //}
            //
            if (Utility.TryParse<bool>(IKGD_Config.AppSettings["DNSBL_DeviceResolverEnabled"], false))
            {
              detectedDevice = MobileHelper.GetDeviceStringFromDnsbl(null);
            }
            else
            {
              detectedDevice = MobileHelper.GetDeviceType(null);
            }
            //
            switch (detectedDevice)
            {
              case "tablet":
                detectedView = "tablet";
                break;
              case "mobile":
                detectedView = "phone";
                break;
            }
            //
            if (detectedView != null && AutoDetectDeviceCheckForMobileRegEx != null && !AutoDetectDeviceCheckForMobileRegEx.IsMatch(detectedView))
            {
              detectedView = null;
            }
          }
          //
          if (detectedView != null)
          {
            IKGD_SiteMode.ViewMode = detectedView;
            processRedirects |= true;
          }
        }
        IKGD_SiteMode.DeviceCookie = detectedDevice ?? string.Empty;
        //
        // gestione dei redirect se abilitati
        //
        if (processRedirects && allowRedirect.GetValueOrDefault(false) && System.Web.HttpContext.Current.Request.Params["ViewModeNoRedirect"] == null)
        {
          result = ProcessSiteModeRedirect(null, null);
        }
      }
      catch { }
      return result;
    }


    public static bool AutoDetectDeviceV2(Func<string> customDetectSite, Func<string> customDetectView) { return AutoDetectDeviceV2(customDetectSite, customDetectView, null, null); }
    public static bool AutoDetectDeviceV2(Func<string> customDetectSite, Func<string> customDetectView, bool? checkForMobileDevices, bool? allowRedirect)
    {
      bool result = false;
      try
      {
        if (System.Web.HttpContext.Current.Request.Cookies[IKGD_SiteMode.ParamNameViewNoDetect] != null || IKGD_SiteMode.DeviceCookie != null)
        {
          return result;
        }
        //
        bool processRedirects = false;
        string detectedDevice = null;
        bool isBot = Utility.CheckIfBOT();
        //
        //if (isBot)
        //{
        //  return result;
        //}
        //
        string detectedSite = null;
        if (!IKGD_SiteMode.IsForcedModeSite())
        {
          if (customDetectSite != null)
          {
            try { detectedSite = customDetectSite(); }
            catch { }
          }
          //
          if (detectedSite != null)
          {
            IKGD_SiteMode.SiteMode = detectedSite;
            processRedirects |= true;
          }
        }
        //
        string detectedView = null;
        if (!IKGD_SiteMode.IsForcedModeView())
        {
          if (customDetectView != null)
          {
            try { detectedView = customDetectView(); }
            catch { }
          }
          //
          // se il custom code non ha individuato un device si procede con il test standard nel caso sia attivo il detect sui devices mobile
          //
          if (detectedView == null)
          {
            detectedDevice = DeviceDetectWorker(checkForMobileDevices);
            switch (detectedDevice)
            {
              case "tablet":
                detectedView = "tablet";
                break;
              case "mobile":
                detectedView = "phone";
                break;
            }
            if (detectedView != null && AutoDetectDeviceCheckForMobileRegEx != null && !AutoDetectDeviceCheckForMobileRegEx.IsMatch(detectedView))
            {
              detectedView = null;
            }
          }
          //
          if (detectedView != null)
          {
            IKGD_SiteMode.ViewMode = detectedView;
            processRedirects |= true;
          }
        }
        //
        processRedirects &= !isBot;
        //
        if (detectedDevice == null)
        {
          try { detectedDevice = (string)System.Web.HttpContext.Current.Items[DeviceDetectedKey] ?? string.Empty; }
          catch { }
        }
        IKGD_SiteMode.DeviceCookie = detectedDevice ?? string.Empty;
        //
        // gestione dei redirect se abilitati
        //
        if (processRedirects && allowRedirect.GetValueOrDefault(false) && System.Web.HttpContext.Current.Request.Params["ViewModeNoRedirect"] == null)
        {
          result = ProcessSiteModeRedirect(null, null);
        }
      }
      catch { }
      return result;
    }


    //
    // wrapper per il detect dei devices con caching sul context
    //
    public static string DeviceDetectWorker(bool? checkForMobileDevices)
    {
      string detectedDevice = null;
      try
      {
        detectedDevice = (string)System.Web.HttpContext.Current.Items[DeviceDetectedKey];
        if (detectedDevice == null && checkForMobileDevices.GetValueOrDefault(false))
        {
          try
          {
            if (Utility.TryParse<bool>(IKGD_Config.AppSettings["DNSBL_DeviceResolverEnabled"], false))
            {
              detectedDevice = MobileHelper.GetDeviceStringFromDnsbl(null);
            }
            else
            {
              detectedDevice = MobileHelper.GetDeviceType(null);
            }
          }
          catch { }
          detectedDevice = detectedDevice ?? string.Empty;
          System.Web.HttpContext.Current.Items[DeviceDetectedKey] = detectedDevice;
        }
      }
      catch { }
      return detectedDevice.NullIfEmpty();
    }


    public static bool ProcessSiteModeRedirect(string modeForced, string urlCurrent)
    {
      try
      {
        var forcingDomains = IKGD_SiteMode.GetForcingDomainsForMode(modeForced ?? IKGD_SiteMode.ModeCombined);
        if (forcingDomains != null && forcingDomains.Any())
        {
          Uri url = System.Web.HttpContext.Current.Request.Url;
          string domainCurrent = url.DnsSafeHost.ToLower();
          if (!forcingDomains.Contains(domainCurrent))
          {
            //var uriBase = new Uri("http://{0}".FormatString(forcingDomains.FirstOrDefault()));
            var uriBase = new Uri("{0}://{1}".FormatString(url.Scheme, forcingDomains.FirstOrDefault()));
            string urlNew = new Uri(uriBase, Utility.ResolveUrl(urlCurrent ?? url.PathAndQuery)).ToString();
            urlNew = Utility.UriSetQuery(urlNew, "ViewModeNoRedirect", true.ToString());
            System.Web.HttpContext.Current.Response.Redirect(urlNew, true);
            return true;
          }
        }
      }
      catch { }
      return false;
    }


  }
}
