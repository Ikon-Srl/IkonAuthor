using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System.Text;
using System.Web;
using System.Xml.Linq;
using System.Web.Mvc;
using System.Web.Mvc.Html;
using System.Reflection;
//using Microsoft.Web.Mvc;

using Ikon;
using Ikon.GD;


namespace Ikon.IKCMS.Pagers
{

  public static class PagingHelperExtensions
  {

    public static PagerSimple<T> FactoryPagerSimple<T>(IEnumerable<T> pagedItems, int? pagerPageSize, string pagingVarQueryString)
    {
      return new PagerSimple<T>(pagedItems, pagerPageSize, pagingVarQueryString);
    }

    public static PagerSimple<T> FactoryPagerSimple<T>(IEnumerable<T> pagedItems, int? pagerPageSize, string pagingVarQueryString, NameValueCollection ArgsSet, string baseUrl)
    {
      return new PagerSimple<T>(pagedItems, pagerPageSize, pagingVarQueryString, ArgsSet, baseUrl);
    }

  }



  public interface PagerSimpleInterface
  {
    int? PagerPageSize { get; }
    int? PagerPageCount { get; }
    int? PagerItemsCount { get; }
    int? PagerPageCurrent { get; }
    int? PagerStartIndex { get; }
    string PagerPageUrlPrev { get; }
    string PagerPageUrlNext { get; }
    string PagerPageUrlFirst { get; }
    string PagerPageUrlLast { get; }
    string PagerPageUrlDynamicPrev { get; }
    string PagerPageUrlDynamicNext { get; }
    string PagerPageUrlDynamicFirst { get; }
    string PagerPageUrlDynamicLast { get; }
    string BaseUrl { get; }
  }



  public class PagerSimple<T> : PagerSimpleInterface
  {
    public int? PagerPageSize { get; set; }
    public int? PagerPageCount { get; set; }
    public int? PagerItemsCount { get; set; }
    public int? PagerPageCurrent { get; set; }
    public int? PagerStartIndex { get; set; }
    public string PagerPageUrlPrev { get; set; }
    public string PagerPageUrlNext { get; set; }
    public string PagerPageUrlFirst { get; set; }
    public string PagerPageUrlLast { get; set; }
    public string PagingVarQueryString { get; protected set; }
    //
    public static int DefaultPagerPageSize { get { return 10; } }
    //
    //
    // attenzione a non usare HttpContext.Current.Request.Url che non rispecchia le url riscritte per il SEO, usare HttpContext.Current.Request.RawUrl
    protected string _BaseUrl = null;
    public string BaseUrl { get { return _BaseUrl ?? HttpContext.Current.Request.RawUrl; } set { _BaseUrl = value; } }
    //
    public IEnumerable<T> ItemsVisible { get; set; }
    //


    public PagerSimple()
    {
      PagerPageSize = DefaultPagerPageSize;
      PagerItemsCount = 0;
      PagerPageCount = 1;
      PagerPageCurrent = 0;
      PagerStartIndex = 0;
      PagerPageUrlPrev = null;
      PagerPageUrlNext = null;
      PagerPageUrlFirst = null;
      PagerPageUrlLast = null;
      PagingVarQueryString = null;
    }


    public PagerSimple(IEnumerable<T> fullResults, int? pagerPageSize, string pagingVarQueryString) { Setup(fullResults, pagerPageSize, pagingVarQueryString); }
    public PagerSimple(IEnumerable<T> fullResults, int? pagerPageSize, string pagingVarQueryString, NameValueCollection ArgsSet, string baseUrl) { Setup(fullResults, pagerPageSize, pagingVarQueryString, ArgsSet, baseUrl); }
    public PagerSimple(IEnumerable<T> fullResults, int? fullResultsCountIfKnown, int? pagerPageSize, string pagingVarQueryString, NameValueCollection ArgsSet, string baseUrl) { Setup(fullResults, fullResultsCountIfKnown, pagerPageSize, pagingVarQueryString, ArgsSet, baseUrl); }


    public virtual void Setup(IEnumerable<T> fullResults, int? pagerPageSize, string pagingVarQueryString) { Setup(fullResults, null, pagerPageSize, pagingVarQueryString, HttpContext.Current.Request.QueryString, null); }
    public virtual void Setup(IEnumerable<T> fullResults, int? pagerPageSize, string pagingVarQueryString, NameValueCollection ArgsSet, string baseUrl) { Setup(fullResults, null, pagerPageSize, pagingVarQueryString, ArgsSet, baseUrl); }
    public virtual void Setup(IEnumerable<T> fullResults, int? fullResultsCountIfKnown, int? pagerPageSize, string pagingVarQueryString, NameValueCollection ArgsSet, string baseUrl)
    {
      ItemsVisible = fullResults;
      try
      {
        //
        BaseUrl = baseUrl;
        PagingVarQueryString = pagingVarQueryString ?? "PagerStartIndex";
        //
        PagerPageSize = pagerPageSize ?? PagerPageSize ?? DefaultPagerPageSize;
        PagerPageSize = Math.Max(PagerPageSize.Value, 1);
        PagerItemsCount = 0;
        try { PagerItemsCount = fullResultsCountIfKnown ?? fullResults.Count(); }
        catch { }
        //
        if (PagerItemsCount.Value < PagerPageSize.Value)
          return;
        //
        PagerPageCount = (PagerItemsCount.Value + PagerPageSize.Value - 1) / PagerPageSize.Value;
        PagerStartIndex = Utility.TryParse<int?>(ArgsSet[PagingVarQueryString], null);
        PagerPageCurrent = PagerStartIndex.GetValueOrDefault(0) / PagerPageSize.Value;
        PagerStartIndex = PagerPageCurrent.Value * PagerPageSize.Value;
        //
        PagerPageUrlFirst = Utility.UriSetQuery(BaseUrl, PagingVarQueryString, null);
        PagerPageUrlLast = Utility.UriSetQuery(BaseUrl, PagingVarQueryString, (((PagerItemsCount.Value - 1) / PagerPageSize.Value) * PagerPageSize.Value).ToString());
        //
        if (PagerStartIndex.Value - PagerPageSize.Value == 0)
          PagerPageUrlPrev = PagerPageUrlFirst;
        else if (PagerStartIndex.Value - PagerPageSize.Value >= 0)
          PagerPageUrlPrev = Utility.UriSetQuery(BaseUrl, PagingVarQueryString, (PagerStartIndex.Value - PagerPageSize.Value).ToString());
        //
        if (PagerStartIndex.Value + PagerPageSize.Value < PagerItemsCount.Value)
          PagerPageUrlNext = Utility.UriSetQuery(BaseUrl, PagingVarQueryString, (PagerStartIndex.Value + PagerPageSize.Value).ToString());
        //
        if (fullResults != null)
          ItemsVisible = fullResults.Skip(PagerStartIndex.Value).Take(PagerPageSize.Value);
        //
      }
      catch { }
    }


    public string PagerPageUrlDynamicFirst { get { return Utility.UriSetQuery(BaseUrl, PagingVarQueryString, null); } }
    public string PagerPageUrlDynamicLast { get { return Utility.UriSetQuery(BaseUrl, PagingVarQueryString, (((PagerItemsCount.Value - 1) / PagerPageSize.Value) * PagerPageSize.Value).ToString()); } }
    public string PagerPageUrlDynamicPrev
    {
      get
      {
        try
        {
          if (PagerStartIndex.Value - PagerPageSize.Value == 0)
            return PagerPageUrlDynamicFirst;
          else if (PagerStartIndex.Value - PagerPageSize.Value >= 0)
            return Utility.UriSetQuery(BaseUrl, PagingVarQueryString, (PagerStartIndex.Value - PagerPageSize.Value).ToString());
        }
        catch { }
        return null;
      }
    }
    public string PagerPageUrlDynamicNext
    {
      get
      {
        try
        {
          if (PagerStartIndex.Value + PagerPageSize.Value < PagerItemsCount.Value)
            return Utility.UriSetQuery(BaseUrl, PagingVarQueryString, (PagerStartIndex.Value + PagerPageSize.Value).ToString());
        }
        catch { }
        return null;
      }
    }


  }
}
