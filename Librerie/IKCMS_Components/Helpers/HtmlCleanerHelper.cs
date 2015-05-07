/*
 * 
 * Intranet Ikon
 * 
 * Copyright (C) 2012 Ikon Srl
 * Tutti i Diritti Riservati. All Rights Reserved
 * 
 */


using System;
using System.Data;
using System.Configuration;
using System.Collections;
using System.Web;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Net.Configuration;
using System.Collections.Generic;
using System.Xml;
using System.Xml.Linq;
using System.Linq;
using System.Linq.Expressions;
using LinqKit;
using Autofac;
using Autofac.Core;
//using Autofac.Integration.Web;

using Ikon;
using Ikon.GD;
using Ikon.IKCMS;



namespace Ikon.Filters
{

  //
  // classe per il filtraggio e sanitizzazione dell'html basata su HtmlTidy.NET e LINQ2XML
  //
  public static class HtmlCleanerHelper
  {

    public static IKGD_HtmlCleaner GetHtmlCleaner()
    {
      return IKCMS_ManagerIoC.requestContainer.Resolve<IKGD_HtmlCleaner>();
    }


    public static string Text(string rawHtml) { return GetHtmlCleaner().Text(rawHtml); }

    public static string Truncate(int maxLength, bool wholeWords, string ellipsis) { return GetHtmlCleaner().Truncate(maxLength, wholeWords, ellipsis); }

    public static string Parse(string rawHtml) { return GetHtmlCleaner().Parse(rawHtml); }

    public static string Parse(string rawHtml, bool filterScriptTag, bool throwExceptions) { return GetHtmlCleaner().Parse(rawHtml, filterScriptTag, throwExceptions); }

    public static string ParseAndTruncate(string rawHtml, int maxLength) { return GetHtmlCleaner().ParseAndTruncate(rawHtml, maxLength); }

    public static string ParseAndTruncate(string rawHtml, int maxLength, bool wholeWords, string ellipsis) { return GetHtmlCleaner().ParseAndTruncate(rawHtml, maxLength, wholeWords, ellipsis); }

  }




}  // Ikon.Filters