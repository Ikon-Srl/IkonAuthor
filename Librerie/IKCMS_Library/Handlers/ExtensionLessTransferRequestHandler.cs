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
  // fare riferimento a:
  // http://blogs.msdn.com/b/tmarq/archive/2010/04/01/asp-net-4-0-enables-routing-of-extensionless-urls-without-impacting-static-requests.aspx
  //

  // This handler only works on IIS 7 in integrated mode and is
  // designed to be used as a "*." handler mapping.  We will refer
  // to "*." as the extensionless handler mapping.  There is a bug
  // in Windows Vista, Windows Server 2008, and Windows Server 2008 R2
  // that prevents the IIS 7 extensionless handler mapping from working
  // correctly, and if you have not already patched your machine you 
  // will need to install this hotfix: http://support.microsoft.com/kb/980368.
  // 
  // WARNING: You may be wondering how this handler works, since it passes
  // the original URL to the TransferRequest method.  Why doesn't it do the
  // same thing the second time the URL is requested?  Well, TransferRequest
  // invokes the IIS 7 API IHttpContext::ExecuteRequest, and includes the
  // EXECUTE_FLAG_IGNORE_CURRENT_INTERCEPTOR flag in the third argument to
  // ExecuteRequest.  This causes the "*.", or extensionless, handler mapping
  // to be skipped, and allows the handler that would have executed originally
  // to serve this request. If you attempt to use this handler without an
  // extensionless handler mapping, it will probably result in recursion.
  // This recurssion will eventually be stopped by IIS once the loop iterates
  // about 12 times, and then IIS will respond with a 500 status, a message 
  // that says "HTTP Error 500.0 - Internal Server Error", and an HRESULT  
  // value of 0x800703e9.  The system error message for this HRESULT is
  // "Recursion too deep; the stack overflowed.".

  public class ExtensionLessTransferRequestHandler : IHttpHandler
  {

    public bool IsReusable { get { return true; } }

    public void ProcessRequest(HttpContext context)
    {
      string transferToUrl = (context.Items["IKCMS_RedirectedUrl"] as string) ?? context.Request.RawUrl;
      context.Server.TransferRequest(transferToUrl, true);
      //context.Server.TransferRequest(context.Request.RawUrl, true);
    }

  }


}
