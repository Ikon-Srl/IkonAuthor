using System;
using System.Data;
using System.Linq;
using System.Configuration;
using System.Web;
using System.Collections.Generic;
using System.Text;

using Ikon;
using Ikon.GD;


namespace Ikon.IKCMS.FaceBook
{

  public class FaceBookHelperSimple
  {

    /*
    // estensione dei token a 60gg
    https://graph.facebook.com/oauth/access_token?client_id=APP_ID&client_secret=APP_SECRET&grant_type=fb_exchange_token&fb_exchange_token=EXISTING_ACCESS_TOKEN
    */




    public static string AutoSetAppId(string key)
    {
      //
      // selezione dinamica della key per adattarsi al contesto di lavoro
      //
      string host = HttpContext.Current.Request.Url.Authority;
      string keyBase = "FB_ApiKey_" + (key ?? string.Empty);
      //
      // prova comunque su web.config come primo tentativo
      //
      if (!string.IsNullOrEmpty(ConfigurationManager.AppSettings[keyBase + "_" + host.ToLower()]))
        return ConfigurationManager.AppSettings[keyBase + "_" + host.ToLower()];
      //
      if (!string.IsNullOrEmpty(ConfigurationManager.AppSettings[keyBase]))
        return ConfigurationManager.AppSettings[keyBase];
      //
      return string.Empty;
    }


    private static string AutoGetAppId_Worker(string subcode)
    {
      subcode = subcode.NullIfEmpty() ?? "Auth";
      string data = IKGD_Config.AppSettings["FB_AppId_" + subcode];
      var frags = Utility.Explode(data, ",", " ", true);
      if (frags.Count > 1)
      {
        foreach (var frag in frags)
        {
          string domain = frag.Split(':').FirstOrDefault();
          if (HttpContext.Current.Request.Url.Host.IndexOf(domain, StringComparison.OrdinalIgnoreCase) >= 0)
          {
            string appid = frag.Split(':').Skip(1).FirstOrDefault().TrimSafe();
            return appid;
          }
        }
      }
      return data;
    }


    public static string AutoGetAppId(string subcode)
    {
      string appid = AutoGetAppId_Worker(subcode);
      appid = (appid ?? string.Empty).Split('|').FirstOrDefault();
      return appid ?? string.Empty;
    }


    public static string AutoGetAppSecret(string subcode)
    {
      string appid = AutoGetAppId_Worker(subcode);
      appid = (appid ?? string.Empty).Split('|').Skip(1).FirstOrDefault();
      return appid ?? string.Empty;
    }


    public static string AutoGetAppToken(string subcode)
    {
      string appid = AutoGetAppId_Worker(subcode);
      appid = (appid ?? string.Empty).Split('|').Skip(2).FirstOrDefault();
      return appid ?? string.Empty;
    }

  }


  public class FaceBookContext
  {
    public string OAuthToken { get; set; }
    public string UserId { get; set; }
    public string PageId { get; set; }
    public bool? PageLiked { get; set; }
    public DateTime IssuedAt { get; set; }
    public DateTime Expires { get; set; }


    public FaceBookContext(string signed_request)
    {
      string AppId = Ikon.IKCMS.FaceBook.FaceBookHelperSimple.AutoGetAppId("Auth");
      string AppSecret = Ikon.IKCMS.FaceBook.FaceBookHelperSimple.AutoGetAppSecret("Auth");
      Setup(signed_request, AppId, AppSecret);
    }


    public void Setup(string signed_request, string AppId, string AppSecret)
    {
      try
      {
        var fb_client = new Facebook.FacebookClient();
        fb_client.AppId = AppId;
        fb_client.AppSecret = AppSecret;
        Facebook.JsonObject signed_request_obj = fb_client.ParseSignedRequest(signed_request) as Facebook.JsonObject;
        if (signed_request != null)
        {
          try { OAuthToken = signed_request_obj["oauth_token"] as string; }
          catch { }
          try { UserId = signed_request_obj["user_id"] as string; }
          catch { }
          try
          {
            long expires = (long)signed_request_obj["expires"];
            Expires = Utility.DateTimeFromUnix(expires);
          }
          catch { }
          try
          {
            long issued_at = (long)signed_request_obj["issued_at"];
            IssuedAt = Utility.DateTimeFromUnix(issued_at);
          }
          catch { }
          try
          {
            Facebook.JsonObject page_obj = signed_request_obj["page"] as Facebook.JsonObject;
            if (page_obj != null)
            {
              PageId = page_obj["id"] as string;
              PageLiked = (bool)page_obj["liked"];
            }
          }
          catch { }
        }
      }
      catch { }
    }


  }

}