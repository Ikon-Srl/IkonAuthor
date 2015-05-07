using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Text.RegularExpressions;
using System.Web;
using System.Xml.Linq;
using System.Web.Mvc;
using System.Web.Mvc.Html;
using System.Reflection;
using System.Runtime.Serialization;
using LinqKit;
using Autofac;
using WURFL;

using Ikon;
using Ikon.GD;
using Ikon.IKCMS.Library.Resources;
using Ikon.IKGD.Library.Resources;
using Ikon.IKGD.Library;


namespace Ikon.IKCMS
{

  public static class MobileHelper
  {
    private static Regex MobileDetect_RegExM { get; set; }
    private static Regex MobileDetect_RegEx4 { get; set; }
    //private static Regex MobileDetect_RegExT { get; set; }
    private static bool IsWURFL_Enabled { get; set; }


    static MobileHelper()
    {
      MobileDetect_RegExM = new Regex(IKGD_Config.AppSettings["MobileDetect_RegExM"] ?? @"(android|bb\d+|meego).+mobile|avantgo|bada\/|blackberry|blazer|compal|elaine|fennec|hiptop|iemobile|ip(hone|od)|iris|kindle|lge |maemo|midp|mmp|netfront|opera m(ob|in)i|palm( os)?|phone|p(ixi|re)\/|plucker|pocket|psp|series(4|6)0|symbian|treo|up\.(browser|link)|vodafone|wap|windows (ce|phone)|xda|xiino", RegexOptions.Singleline | RegexOptions.IgnoreCase);
      MobileDetect_RegEx4 = new Regex(IKGD_Config.AppSettings["MobileDetect_RegEx4"] ?? @"1207|6310|6590|3gso|4thp|50[1-6]i|770s|802s|a wa|abac|ac(er|oo|s\-)|ai(ko|rn)|al(av|ca|co)|amoi|an(ex|ny|yw)|aptu|ar(ch|go)|as(te|us)|attw|au(di|\-m|r |s )|avan|be(ck|ll|nq)|bi(lb|rd)|bl(ac|az)|br(e|v)w|bumb|bw\-(n|u)|c55\/|capi|ccwa|cdm\-|cell|chtm|cldc|cmd\-|co(mp|nd)|craw|da(it|ll|ng)|dbte|dc\-s|devi|dica|dmob|do(c|p)o|ds(12|\-d)|el(49|ai)|em(l2|ul)|er(ic|k0)|esl8|ez([4-7]0|os|wa|ze)|fetc|fly(\-|_)|g1 u|g560|gene|gf\-5|g\-mo|go(\.w|od)|gr(ad|un)|haie|hcit|hd\-(m|p|t)|hei\-|hi(pt|ta)|hp( i|ip)|hs\-c|ht(c(\-| |_|a|g|p|s|t)|tp)|hu(aw|tc)|i\-(20|go|ma)|i230|iac( |\-|\/)|ibro|idea|ig01|ikom|im1k|inno|ipaq|iris|ja(t|v)a|jbro|jemu|jigs|kddi|keji|kgt( |\/)|klon|kpt |kwc\-|kyo(c|k)|le(no|xi)|lg( g|\/(k|l|u)|50|54|\-[a-w])|libw|lynx|m1\-w|m3ga|m50\/|ma(te|ui|xo)|mc(01|21|ca)|m\-cr|me(rc|ri)|mi(o8|oa|ts)|mmef|mo(01|02|bi|de|do|t(\-| |o|v)|zz)|mt(50|p1|v )|mwbp|mywa|n10[0-2]|n20[2-3]|n30(0|2)|n50(0|2|5)|n7(0(0|1)|10)|ne((c|m)\-|on|tf|wf|wg|wt)|nok(6|i)|nzph|o2im|op(ti|wv)|oran|owg1|p800|pan(a|d|t)|pdxg|pg(13|\-([1-8]|c))|phil|pire|pl(ay|uc)|pn\-2|po(ck|rt|se)|prox|psio|pt\-g|qa\-a|qc(07|12|21|32|60|\-[2-7]|i\-)|qtek|r380|r600|raks|rim9|ro(ve|zo)|s55\/|sa(ge|ma|mm|ms|ny|va)|sc(01|h\-|oo|p\-)|sdk\/|se(c(\-|0|1)|47|mc|nd|ri)|sgh\-|shar|sie(\-|m)|sk\-0|sl(45|id)|sm(al|ar|b3|it|t5)|so(ft|ny)|sp(01|h\-|v\-|v )|sy(01|mb)|t2(18|50)|t6(00|10|18)|ta(gt|lk)|tcl\-|tdg\-|tel(i|m)|tim\-|t\-mo|to(pl|sh)|ts(70|m\-|m3|m5)|tx\-9|up(\.b|g1|si)|utst|v400|v750|veri|vi(rg|te)|vk(40|5[0-3]|\-v)|vm40|voda|vulc|vx(52|53|60|61|70|80|81|83|85|98)|w3c(\-| )|webc|whit|wi(g |nc|nw)|wmlb|wonu|x700|yas\-|your|zeto|zte\-", RegexOptions.Singleline | RegexOptions.IgnoreCase);
      //MobileDetect_RegExT = new Regex(IKGD_Config.AppSettings["MobileDetect_RegExT"] ?? @"ipad|android.+(mobile){0}", RegexOptions.Singleline | RegexOptions.IgnoreCase);
      IsWURFL_Enabled = System.IO.File.Exists(HttpContext.Current.Server.MapPath("~/App_Data/wurfl.zip"));
    }


    public static IWURFLManager WURFLManager
    {
      get
      {
        return FS_OperationsHelpers.CachedEntityWrapper("__WurflManager", () =>
        {
          IWURFLManager manager = null;
          try
          {
            var wurflDataFile = HttpContext.Current.Server.MapPath("~/App_Data/wurfl.zip");
            var configurer = new WURFL.Config.InMemoryConfigurer().MainFile(wurflDataFile);
            configurer.SetMatchMode(MatchMode.Performance);
            manager = WURFLManagerBuilder.Build(configurer);
          }
          catch { }
          IsWURFL_Enabled = (manager != null);
          return manager;
        }, 3600, null);
      }
    }

    public static IDevice WURFLManagerCurrentDevice
    {
      get
      {
        return (HttpContext.Current.Items["__WurflManagerDevice"] ?? WURFLManagerCurrentDeviceSet(HttpContext.Current.Request.UserAgent)) as IDevice;
      }
    }
    public static IDevice WURFLManagerCurrentDeviceSet(string httpUserAgent) { return WURFLManagerCurrentDeviceSet(httpUserAgent, MatchMode.Performance); }
    public static IDevice WURFLManagerCurrentDeviceSet(string httpUserAgent, MatchMode matchMode)
    {
      return (HttpContext.Current.Items["__WurflManagerDevice"] = WURFLManager.GetDeviceForRequest(httpUserAgent, matchMode)) as IDevice;
    }

    public static bool DeviceIsMobile { get { return Utility.TryParse<bool>(WURFLManagerCurrentDevice.GetCapability("is_wireless_device")); } }
    public static bool DeviceIsTablet { get { return Utility.TryParse<bool>(WURFLManagerCurrentDevice.GetCapability("is_tablet")); } }
    public static string DeviceOs { get { return WURFLManagerCurrentDevice.GetCapability("device_os"); } }
    public static string DeviceOsVersion { get { return WURFLManagerCurrentDevice.GetCapability("device_os_version"); } }
    public static int DeviceResolutionWitdh { get { return Utility.TryParse<int>(WURFLManagerCurrentDevice.GetCapability("resolution_width")); } }
    public static int DeviceResolutionHeight { get { return Utility.TryParse<int>(WURFLManagerCurrentDevice.GetCapability("resolution_height")); } }
    public static int DeviceWitdh_mm { get { return Utility.TryParse<int>(WURFLManagerCurrentDevice.GetCapability("physical_screen_width")); } }
    public static int DeviceHeight_mm { get { return Utility.TryParse<int>(WURFLManagerCurrentDevice.GetCapability("physical_screen_height")); } }


    public static bool IsMobileDevice(string httpUserAgent) { return IsMobileDevice(httpUserAgent, true, true); }
    public static bool IsMobileDevice(string httpUserAgent, bool? checkForPhone, bool? checkForTablet)
    {
      bool result = false;
      try { httpUserAgent = httpUserAgent ?? HttpContext.Current.Request.ServerVariables["HTTP_USER_AGENT"]; }
      catch { }
      //
      try
      {
        IDevice device = WURFLManagerCurrentDeviceSet(httpUserAgent);
        if (device != null)
        {
          if (checkForPhone.GetValueOrDefault(false))
          {
            result |= Utility.TryParse<bool>(device.GetCapability("is_wireless_device"));
          }
          if (checkForTablet.GetValueOrDefault(false))
          {
            result |= Utility.TryParse<bool>(device.GetCapability("is_tablet"));
          }
          return result;
        }
      }
      catch { }
      //
      httpUserAgent = (httpUserAgent ?? string.Empty).ToLower();
      if (checkForPhone.GetValueOrDefault(false) || checkForTablet.GetValueOrDefault(false))
      {
        try
        {
          result |= MobileDetect_RegExM.IsMatch(httpUserAgent);
          if (!result)
            result |= MobileDetect_RegEx4.IsMatch(httpUserAgent.Substring(0, 4));
        }
        catch { }
      }
      if (checkForTablet.GetValueOrDefault(false))
      {
        try
        {
          bool isTablet = false;
          isTablet |= httpUserAgent.IndexOf("iOS", StringComparison.OrdinalIgnoreCase) >= 0;
          isTablet |= (httpUserAgent.IndexOf("android", StringComparison.OrdinalIgnoreCase) >= 0 && httpUserAgent.IndexOf("mobile", StringComparison.OrdinalIgnoreCase) < 0);
          result |= isTablet;
        }
        catch { }
      }
      return result;
    }


    //
    // matches= "mobile", "tablet", "desktop", "tv"
    //
    public static bool IsDeviceType(string httpUserAgent, params string[] matches)
    {
      string dev = GetDeviceType(httpUserAgent);
      if (matches != null && matches.Any())
      {
        return matches.Any(r => string.Equals(r, dev, StringComparison.OrdinalIgnoreCase));
      }
      return false;
    }


    public static string GetDeviceType(string httpUserAgent)
    {
      string result = null;
      try { httpUserAgent = httpUserAgent ?? HttpContext.Current.Request.ServerVariables["HTTP_USER_AGENT"]; }
      catch { }
      //
      try
      {
        // Check if user agent is a smart TV - http://goo.gl/FocDk
        if (Regex.IsMatch(httpUserAgent, @"GoogleTV|SmartTV|Internet.TV|NetCast|NETTV|AppleTV|boxee|Kylo|Roku|DLNADOC|CE\-HTML", RegexOptions.IgnoreCase))
        {
          result = "tv";
        }
        // Check if user agent is a TV Based Gaming Console
        else if (Regex.IsMatch(httpUserAgent, "Xbox|PLAYSTATION.3|Wii", RegexOptions.IgnoreCase))
        {
          result = "tv";
        }
        // Check if user agent is a Tablet
        else if ((Regex.IsMatch(httpUserAgent, "iP(a|ro)d", RegexOptions.IgnoreCase) || (Regex.IsMatch(httpUserAgent, "tablet", RegexOptions.IgnoreCase)) && (!Regex.IsMatch(httpUserAgent, "RX-34", RegexOptions.IgnoreCase)) || (Regex.IsMatch(httpUserAgent, "FOLIO", RegexOptions.IgnoreCase))))
        {
          result = "tablet";
        }
        // Check if user agent is an Android Tablet
        else if ((Regex.IsMatch(httpUserAgent, "Linux", RegexOptions.IgnoreCase)) && (Regex.IsMatch(httpUserAgent, "Android", RegexOptions.IgnoreCase)) && (!Regex.IsMatch(httpUserAgent, "Fennec|mobi|HTC.Magic|HTCX06HT|Nexus.One|SC-02B|fone.945", RegexOptions.IgnoreCase)))
        {
          result = "tablet";
        }
        // Check if user agent is a Kindle or Kindle Fire
        else if ((Regex.IsMatch(httpUserAgent, "Kindle", RegexOptions.IgnoreCase)) || (Regex.IsMatch(httpUserAgent, "Mac.OS", RegexOptions.IgnoreCase)) && (Regex.IsMatch(httpUserAgent, "Silk", RegexOptions.IgnoreCase)))
        {
          result = "tablet";
        }
        // Check if user agent is a pre Android 3.0 Tablet
        else if ((Regex.IsMatch(httpUserAgent, @"GT-P10|SC-01C|SHW-M180S|SGH-T849|SCH-I800|SHW-M180L|SPH-P100|SGH-I987|zt180|HTC(.Flyer|\\_Flyer)|Sprint.ATP51|ViewPad7|pandigital(sprnova|nova)|Ideos.S7|Dell.Streak.7|Advent.Vega|A101IT|A70BHT|MID7015|Next2|nook", RegexOptions.IgnoreCase)) || (Regex.IsMatch(httpUserAgent, "MB511", RegexOptions.IgnoreCase)) && (Regex.IsMatch(httpUserAgent, "RUTEM", RegexOptions.IgnoreCase)))
        {
          result = "tablet";
        }
        // Check if user agent is unique Mobile User Agent
        else if ((Regex.IsMatch(httpUserAgent, "BOLT|Fennec|Iris|Maemo|Minimo|Mobi|mowser|NetFront|Novarra|Prism|RX-34|Skyfire|Tear|XV6875|XV6975|Google.Wireless.Transcoder", RegexOptions.IgnoreCase)))
        {
          result = "mobile";
        }
        // Check if user agent is an odd Opera User Agent - http://goo.gl/nK90K
        else if ((Regex.IsMatch(httpUserAgent, "Opera", RegexOptions.IgnoreCase)) && (Regex.IsMatch(httpUserAgent, "Windows.NT.5", RegexOptions.IgnoreCase)) && (Regex.IsMatch(httpUserAgent, @"HTC|Xda|Mini|Vario|SAMSUNG\-GT\-i8000|SAMSUNG\-SGH\-i9", RegexOptions.IgnoreCase)))
        {
          result = "mobile";
        }
        // Check if user agent is Windows Desktop
        else if ((Regex.IsMatch(httpUserAgent, "Windows.(NT|XP|ME|9)")) && (!Regex.IsMatch(httpUserAgent, "Phone", RegexOptions.IgnoreCase)) || (Regex.IsMatch(httpUserAgent, "Win(9|.9|NT)", RegexOptions.IgnoreCase)))
        {
          result = "desktop";
        }
        // Check if agent is Mac Desktop
        else if ((Regex.IsMatch(httpUserAgent, "Macintosh|PowerPC", RegexOptions.IgnoreCase)) && (!Regex.IsMatch(httpUserAgent, "Silk", RegexOptions.IgnoreCase)))
        {
          result = "desktop";
        }
        // Check if user agent is a Linux Desktop
        else if ((Regex.IsMatch(httpUserAgent, "Linux", RegexOptions.IgnoreCase)) && (Regex.IsMatch(httpUserAgent, "X11", RegexOptions.IgnoreCase)))
        {
          result = "desktop";
        }
        // Check if user agent is a Solaris, SunOS, BSD Desktop
        else if ((Regex.IsMatch(httpUserAgent, "Solaris|SunOS|BSD", RegexOptions.IgnoreCase)))
        {
          result = "desktop";
        }
        // Check if user agent is a Desktop BOT/Crawler/Spider
        else if ((Regex.IsMatch(httpUserAgent, "Bot|Crawler|Spider|Yahoo|ia_archiver|Covario-IDS|findlinks|DataparkSearch|larbin|Mediapartners-Google|NG-Search|Snappy|Teoma|Jeeves|TinEye|facebookexternalhit", RegexOptions.IgnoreCase)) && (!Regex.IsMatch(httpUserAgent, "Mobile", RegexOptions.IgnoreCase)))
        {
          result = "desktop";
        }
        // Otherwise assume it is a Mobile Device
        else
        {
          result = "mobile";
        }
      }
      catch { }
      return result ?? "mobile";
    }


    public static string GetDeviceStringFromDnsbl(string httpUserAgent)
    {
      if (Utility.TryParse<bool>(IKGD_Config.AppSettings["DnsblDeviceInfo_Enabled"], false))
      {
        DnsblDeviceInfo device = GetDeviceFromDnsbl(httpUserAgent);
        if (device != null)
          return device.DeviceStr;
      }
      return GetDeviceType(httpUserAgent);
    }


    public static DnsblDeviceInfo GetDeviceFromDnsbl(string httpUserAgent)
    {
      return (HttpContext.Current.Items["__DnsblDeviceInfo"] = GetDeviceFromDnsblForRequest(httpUserAgent)) as DnsblDeviceInfo;
    }


    public static DnsblDeviceInfo GetDeviceFromDnsblForRequest(string httpUserAgent)
    {
      DnsblDeviceInfo device = null;
      try
      {
        string query = "hs" + Utility.HashMD5((httpUserAgent.NullIfEmpty() ?? System.Web.HttpContext.Current.Request.UserAgent).StringTruncate(255));
        string name = query + (IKGD_Config.AppSettings["DNSBL_DeviceResolverAddress"] ?? ".ua.dnsbl.digitalwebland.com");
        Heijden.DNS.Resolver resolver = new Heijden.DNS.Resolver();
        resolver.UseCache = true; // importante
        resolver.TimeOut = Utility.TryParse<int>(IKGD_Config.AppSettings["DNSBL_ResolverTimeout"], 1); // 1s
        resolver.Retries = 1;
        resolver.TransportType = Heijden.DNS.TransportType.Udp;
        Heijden.DNS.Response resp = resolver.Query(name, Heijden.DNS.QType.TXT, Heijden.DNS.QClass.IN);
        if (resp != null)
        {
          string _data = resp.RecordsTXT.Select(r => r.TXT).FirstOrDefault(r => r.IsNotEmpty());
          if (_data.IsNotEmpty())
          {
            device = IKGD_Serialization.DeSerializeJSON<DnsblDeviceInfo>(_data);
          }
        }
      }
      catch { }
      return device;
    }



    [DataContract()]
    public class DnsblDeviceInfo
    {
      [DataMember]
      public int? Device { get; set; }  // bitmask: "mobile:1", "tablet:2", "desktop:0"
      [DataMember]
      public string OS { get; set; }
      [DataMember]
      public string Ver { get; set; }
      [DataMember]
      public int? W { get; set; }
      [DataMember]
      public int? H { get; set; }
      [DataMember]
      public int? Wmm { get; set; }
      [DataMember]
      public int? Hmm { get; set; }

      public string DeviceStr
      {
        get
        {
          if (IsTablet)
            return "tablet";
          else if (IsMobile)
            return "mobile";
          return "desktop";
        }
      }

      public bool IsMobile { get { return (Device.GetValueOrDefault(0) & 1) == 1; } }
      public bool IsPhone { get { return (Device.GetValueOrDefault(0) & 1) == 1; } }
      public bool IsTablet { get { return (Device.GetValueOrDefault(0) & 2) == 2; } }
      public bool IsDesktop { get { return Device.GetValueOrDefault(0) == 0; } }

    }

  }
}
