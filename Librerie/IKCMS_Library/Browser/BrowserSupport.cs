using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Security;
using System.Web.SessionState;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.IO;
using System.Web.Compilation;
using System.DirectoryServices;
using LinqKit;

using Ikon;
using Ikon.GD;
using Ikon.IKCMS;



namespace Ikon.Support
{

  public static class BrowserInfo
  {
    private static List<string> AgentsMobile = new List<string> { "vodafone", "playstation portable", "palmos", "windows ce", "minimo", "avantgo", "docomo", "up.browser", "j-phone", "sec-sgh", "astel", "eudoraweb", "iphone", "ipad", "ipod", "mobi", "blackberry", "opera mini", "android", "ipaq", "htc", "kindle", "smartphone", "symbian", "wap", "nokia", "samsung", "sie-", "sonyericsson", "symbian" };
    private static List<string> AgentsApple = new List<string> { "iphone", "ipad", "ipod" };
    private static List<string> AgentsAndroid = new List<string> { "android" };


    public static bool IsWap
    {
      get
      {
        try
        {
          if (HttpContext.Current.Request.Browser.IsMobileDevice || HttpContext.Current.Request.ServerVariables["HTTP_X_WAP_PROFILE"] != null)
            return true;
          if (HttpContext.Current.Request.ServerVariables["HTTP_ACCEPT"] != null && HttpContext.Current.Request.ServerVariables["HTTP_ACCEPT"].ToLower().Contains("wap"))
            return true;
        }
        catch { }
        return false;
      }
    }


    public static bool IsMobile
    {
      get
      {
        try
        {
          string userAgent = HttpContext.Current.Request.UserAgent.ToLower();
          return userAgent.Split(' ', ',', ';', '/', '(', ')').Intersect(AgentsMobile).Any();
        }
        catch { }
        return false;
      }
    }


    public static bool IsAndroid
    {
      get
      {
        try
        {
          string userAgent = HttpContext.Current.Request.UserAgent.ToLower();
          return userAgent.Split(' ', ',', ';', '/', '(', ')').Intersect(AgentsAndroid).Any();
        }
        catch { }
        return false;
      }
    }


    public static bool IsApple
    {
      get
      {
        try
        {
          string userAgent = HttpContext.Current.Request.UserAgent.ToLower();
          return userAgent.Split(' ', ',', ';', '/', '(', ')').Intersect(AgentsApple).Any();
        }
        catch { }
        return false;
      }
    }


    public static bool FlashSupported
    {
      get
      {
        try
        {
          if (HttpContext.Current.Request.QueryString["flashOff"] != null)
            return !Utility.TryParse<bool>(HttpContext.Current.Request.QueryString["flashOff"], true);
          if (IKGD_SiteMode.IsAccessible)
            return false;
          if (!IsMobile)
            return true;
          //TODO: mettere dei check piu' sofisticati (versione di android ecc.)
          if (IsAndroid)
            return true;
        }
        catch { }
        return false;
      }
    }

  }

}

