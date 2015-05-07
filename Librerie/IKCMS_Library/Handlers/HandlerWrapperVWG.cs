/*
 * 
 * Intranet Ikon
 * 
 * Copyright (C) 2010 Ikon Srl
 * Tutti i Diritti Riservati. All Rights Reserved
 * 
 */


using System;
using System.Data;
using System.Configuration;
using System.Collections;
using System.Collections.Generic;
using System.Web;
using System.Web.Caching;
using System.Web.Security;
using System.Linq;
using System.Xml.Linq;
using System.Data.Linq;
using System.Data.Linq.Mapping;
using System.Linq.Expressions;
using System.Net;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using LinqKit;

using Ikon;
using Ikon.GD;
using Ikon.Log;



namespace Ikon.Handlers
{
  //
  // per registrare i moduli automaticamente con .NET4 senza intervenire sul web.config
  // http://blog.davidebbo.com/2011/02/register-your-http-modules-at-runtime.html
  //

  //
  // Http Module per la manipolazione della CurrentUICulture per le librerie VWG
  // VWG ha problemi per le culture che non sono nel formato "ab-CD" per cui facciamo in modo di
  // intercettare le richieste a VWG e manipoliamo la CurrentUICulture se necessario
  // da registrare direttamente nel web.config
  //
  //  <httpModules>
  //    <add name="ModuleHandlerWrapperVWG" type="Ikon.Handlers.ModuleHandlerWrapperVWG, IKCMS_Library"/>
  //  </httpModules>
  //
  public class ModuleHandlerWrapperVWG : IHttpModule
  {
    public void Dispose() { }

    public void Init(HttpApplication app)
    {
      //app.BeginRequest += new EventHandler(requestHandler_WrapperVWG);
      app.AcquireRequestState += new EventHandler(requestHandler_WrapperVWG);
    }

    void requestHandler_WrapperVWG(object sender, EventArgs e)
    {
      try
      {
        HttpApplication app = ((HttpApplication)(sender));
        string path = app.Request.Path;
        if (path.EndsWith(".wgx", StringComparison.OrdinalIgnoreCase))
        {
          //var culture01 = System.Threading.Thread.CurrentThread.CurrentCulture;
          //var culture02 = System.Threading.Thread.CurrentThread.CurrentUICulture;
          if (System.Threading.Thread.CurrentThread.CurrentUICulture.IetfLanguageTag.IndexOf('-') < 0)
            System.Threading.Thread.CurrentThread.CurrentUICulture = System.Threading.Thread.CurrentThread.CurrentCulture;
        }
      }
      catch { }
    }

  }


}
