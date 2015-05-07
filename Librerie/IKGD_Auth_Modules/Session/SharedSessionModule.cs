/*
 * 
 * Intranet Ikon
 * 
 * Copyright (C) 2008 Ikon Srl
 * Tutti i Diritti Riservati. All Rights Reserved
 * 
 */


using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Web;
using System.Web.Configuration;
using System.Configuration;
using System.Reflection;

using Ikon.GD;


namespace Ikon.Handlers
{
  //
  // per registrare i moduli automaticamente con .NET4 senza intervenire sul web.config
  // http://blog.davidebbo.com/2011/02/register-your-http-modules-at-runtime.html
  //

  //
  // modulo per la condivisione delle sessioni tra piu' applicazioni sullo stesso server
  // integrare nel web.config le seguenti configurazioni
  //
  //<appSettings>
  //  <add key="ApplicationName" value="Intranet"/>
  //</appSettings>
  //
  //<httpModules>
  //  <add name="SharedSessionModule" type="Ikon.Handlers.SharedSessionModule, IKGD_Utility"/>
  //</httpModules>
  //
  public class SharedSessionModule : IHttpModule
  {
    public void Init(HttpApplication context)
    {
      try
      {
        // "ApplicationName" viene letto da web.config e non dal config su database
        string appName = IKGD_Config.AppSettingsWeb["ApplicationName"];
        if (!string.IsNullOrEmpty(appName))
        {
          FieldInfo runtimeInfo = typeof(HttpRuntime).GetField("_theRuntime", BindingFlags.Static | BindingFlags.NonPublic);
          HttpRuntime theRuntime = (HttpRuntime)runtimeInfo.GetValue(null);
          FieldInfo appNameInfo = typeof(HttpRuntime).GetField("_appDomainAppId", BindingFlags.Instance | BindingFlags.NonPublic);
          appNameInfo.SetValue(theRuntime, appName);
        }
      }
      catch { }
    }

    public void Dispose()
    {
    }
  }

}
