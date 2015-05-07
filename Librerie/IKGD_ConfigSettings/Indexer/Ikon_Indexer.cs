/*
 * 
 * Intranet Ikon
 * 
 * Copyright (C) 2008 Ikon Srl
 * Tutti i Diritti Riservati. All Rights Reserved
 * 
 */


using System;
using System.Collections;
using System.Text;
using System.Configuration;
using System.Web;
using System.Web.UI;
using System.Web.Services;
using System.Web.Services.Protocols;

using Ikon.GD;


namespace Ikon.Support
{
  using Ikon;

  public static class WebServiceExtender
  {
    public static void AutoConfigUrl(this System.Web.Services.Protocols.SoapHttpClientProtocol service, string configAppKey)
    {
      try
      {
        if (string.IsNullOrEmpty(configAppKey) || string.IsNullOrEmpty(IKGD_Config.AppSettings[configAppKey]))
          return;
        string url = IKGD_Config.AppSettings[configAppKey];
        if (url.StartsWith("~/"))
          url = VirtualPathUtility.ToAbsolute(url);
        if (url.StartsWith("/"))
          url = new Uri(HttpContext.Current.Request.Url, url).ToString();
        if (service != null)
          service.Url = url;
      }
      catch { }
    }


  }


}
