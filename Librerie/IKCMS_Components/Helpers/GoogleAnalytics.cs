using System;
using System.Collections.Generic;
using System.Text;
using System.Linq;
using System.Web;
using System.Xml.Linq;
using System.Web.Mvc;
using System.Web.Mvc.Html;
using System.Reflection;

using Ikon;
using Ikon.GD;
using Ikon.IKCMS;
using Ikon.IKCMS.Library.Resources;


namespace Ikon.IKCMS
{
  public static class GoogleAnalytics_Extensions
  {

    //
    // per il tracking con i nuovi analytics universal
    //
    public static IList<GA_Info> GA_GetTrackingCodesUniversal(this HtmlHelper helper)
    {
      return FS_OperationsHelpers.CachedEntityWrapper("GA_GetTrackingCodesUniversal", () =>
      {
        List<GA_Info> codes = null;
        try
        {
          var codesGA = new List<string>();
          foreach (var baseName in new string[] { "GoogleAnalyticsCodeUniversal", "GoogleAnalyticsCode" })
          {
            codesGA.AddRange(Utility.Explode(IKGD_Config.AppSettings[baseName + "-" + IKGD_Language_Provider.Provider.LanguageNN] ?? IKGD_Config.AppSettings[baseName], ",", " ", true));
            var codesExtra = IKGD_Config.AppSettings[baseName + "-" + HttpContext.Current.Request.Url.Host.ToLower()];
            if (codesExtra.IsNotEmpty())
            {
              codesGA.AddRange(Utility.Explode(codesExtra, ",", " ", true));
            }
            if (codesGA.Any())
            {
              break;
            }
          }
          codes = codesGA.Distinct().Select((c, i) => new GA_Info { Code = c, Prefix = ((i > 0) ? string.Format("ga{0}.", i + 1) : string.Empty) }).ToList();
        }
        catch { }
        return codes ?? new List<GA_Info>();
      }, 3600, null);
    }


    public static IList<GA_Info> GA_GetTrackingCodes(this HtmlHelper helper)
    {
      return FS_OperationsHelpers.CachedEntityWrapper("GA_GetTrackingCodes", () =>
      {
        List<GA_Info> codes = null;
        try
        {
          var codesGA = Utility.Explode(IKGD_Config.AppSettings["GoogleAnalyticsCode-" + IKGD_Language_Provider.Provider.LanguageNN] ?? IKGD_Config.AppSettings["GoogleAnalyticsCode"], ",", " ", true);
          var codesExtra = IKGD_Config.AppSettings["GoogleAnalyticsCode-" + HttpContext.Current.Request.Url.Host.ToLower()];
          if (codesExtra.IsNotEmpty())
          {
            codesGA.AddRange(Utility.Explode(codesExtra, ",", " ", true));
          }
          codes = codesGA.Select((c, i) => new GA_Info { Code = c, Prefix = ((i > 0) ? "." + Convert.ToChar(i + 97) : string.Empty) }).ToList();
        }
        catch { }
        return codes ?? new List<GA_Info>();
      }, 3600, null);
    }


    public static string GA_LoadScripts(this HtmlHelper helper)
    {
      return @"(function() {var ga = document.createElement('script'); ga.type = 'text/javascript'; ga.async = true; ga.src = ('https:' == document.location.protocol ? 'https://ssl' : 'http://www') + '.google-analytics.com/ga.js'; var s = document.getElementsByTagName('script')[0]; s.parentNode.insertBefore(ga, s); })();";
    }


    public class GA_Info
    {
      public string Code { get; set; }
      public string Prefix { get; set; }

      public string CodeJS_Create() { return CodeJS_Create("none"); }
      public string CodeJS_Create(string domain)
      {
        string extra = string.Empty;
        if (Prefix.IsNotEmpty())
        {
          extra = string.Format(", {{'name': '{0}'}}", Prefix.Trim(' ', '.'));
        }
        return string.Format("ga('create', '{0}', '{1}'{2});", Code, domain, extra);
      }

      public string CodeJS_Cmd(string cmd, string type, params object[] args)
      {
        List<string> prms = new List<string>() { "'{1}{0}'".FormatString(cmd, Prefix), "'{0}'".FormatString(type) };
        if (args != null)
        {
          prms.AddRange(args.Select(r => string.Format((r is string) ? "'{0}'" : "{0}", r)));
        }
        return string.Format("ga({0});", Utility.Implode(prms, ",", null, true, true));
      }

    }

  }
}
