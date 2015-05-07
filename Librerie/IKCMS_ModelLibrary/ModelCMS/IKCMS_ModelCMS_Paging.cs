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
using System.Linq;
using System.Web;
using System.Web.UI;
using System.Web.Security;
using System.Xml.Linq;
using System.Data.Linq;
using System.IO;
using System.Text;
using System.Linq.Expressions;
using System.Reflection;
using System.Data.Linq.Mapping;
using System.Diagnostics;
using System.Linq.Dynamic;
using System.Transactions;
using System.Web.Caching;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;  // assembly System.ServiceModel.Web

using LinqKit;
using Autofac;
using Autofac.Core;
using Autofac.Builder;
using Autofac.Features;

using Ikon;
using Ikon.IKCMS;


namespace Ikon.IKCMS
{
  using Ikon.Config;
  using Ikon.GD;


  //
  // v. Ikon.IKCMS.Pagers.PagingHelperExtensions
  //
  public class IKCMS_ModelCMS_PagingHandler
  {
    public int? PageSize { get; set; }
    public int? PageCount { get; set; }
    public int? ItemsCount { get; set; }
    public int? PageCurrent { get; set; }
    public int? StartIndex { get; set; }
    public string PageUrlFirst { get; set; }
    public string PageUrlPrev { get; set; }
    public string PageUrlNext { get; set; }
    public string PageUrlLast { get; set; }


    public IKCMS_ModelCMS_PagingHandler(IEnumerable pagedItems, int? pageSize, string queryStringParamName)
      : this((pagedItems == null) ? 0 : pagedItems.AsQueryable().Count(), pageSize, queryStringParamName)
    { }

    public IKCMS_ModelCMS_PagingHandler(int itemsCount, int? pageSize, string queryStringParamName)
    {
      pageSize = pageSize ?? 5;
      queryStringParamName = queryStringParamName ?? "PagerStartIndex";
      //
      PageSize = pageSize.Value;
      ItemsCount = itemsCount;
      PageCount = 1;
      PageCurrent = 0;
      StartIndex = 0;
      PageUrlFirst = "javascript:;";
      PageUrlPrev = "javascript:;";
      PageUrlNext = "javascript:;";
      PageUrlLast = "javascript:;";
      //
      if (ItemsCount < pageSize.Value)
        return;
      //
      PageCount = (ItemsCount.Value + PageSize.Value - 1) / PageSize.Value;
      StartIndex = Utility.TryParse<int?>(HttpContext.Current.Request.QueryString[queryStringParamName], null);
      PageCurrent = StartIndex.GetValueOrDefault(0) / PageSize.Value;
      StartIndex = PageCurrent.Value * PageSize.Value;
      if (StartIndex.Value - PageSize.Value >= 0)
      {
        PageUrlPrev = Utility.UriSetQuery(HttpContext.Current.Request.RawUrl, queryStringParamName, (StartIndex.Value - PageSize.Value).ToString());
        PageUrlFirst = Utility.UriSetQuery(HttpContext.Current.Request.RawUrl, queryStringParamName, 0.ToString());
      }
      if (StartIndex.Value + PageSize.Value < ItemsCount.Value)
      {
        PageUrlNext = Utility.UriSetQuery(HttpContext.Current.Request.RawUrl, queryStringParamName, (StartIndex.Value + PageSize.Value).ToString());
        PageUrlLast = Utility.UriSetQuery(HttpContext.Current.Request.RawUrl, queryStringParamName, (((ItemsCount.Value - 1) / PageSize.Value) * PageSize.Value).ToString());
      }
    }

  }



}
