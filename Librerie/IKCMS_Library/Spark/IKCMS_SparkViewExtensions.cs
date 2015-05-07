using System;
using System.Collections;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Text.RegularExpressions;
using System.IO;
using System.Web;
using System.Xml.Linq;
using System.Web.Mvc;
using System.Web.Mvc.Html;
using System.Web.Routing;
using System.Reflection;
using Spark;
using Spark.Web.Mvc;
using Spark.Web.Mvc.Descriptors;
using Autofac;
using LinqKit;

using Ikon;
using Ikon.GD;
using Ikon.IKCMS;


namespace Ikon.IKCMS.SparkIKCMS
{

  //
  // classe modificata con le customizzazioni IKCMS per le estensioni dello spark viewengine
  // - supporto per routing semplificato nelle url: <a href="~/@view/car/id=1">View Car</a> 
  //
  public abstract class IKCMS_SparkView : SparkView
  {

    //
    // supporto per la gestione automatica delle action nelle url tipo: ~/@action/
    //
    public new string SiteResource(string value)
    {
      if (value.StartsWith("~/@"))
        return GetActionUrl(value);
      return base.SiteResource(value);
    }


    public string GetActionUrl(string value)
    {
      var values = value.Substring(3).Split('/');
      if (values.Length == 1)
        return Url.Action(values[0]); //Action
      if (values[1].Contains('='))
        return Url.Action(values[0], GetRouteDict(values.Skip(1))); // Action + Route Values
      if (values.Length == 2)
        return Url.Action(values[0], values[1]); // Action + Controller
      return Url.Action(values[0], values[1], GetRouteDict(values.Skip(2))); // Action + Controller + Route Values
    }


    public static RouteValueDictionary GetRouteDict(IEnumerable<string> values)
    {
      var dic = new RouteValueDictionary();
      char[] seps = new char[] { '=' };
      foreach (var item in values)
      {
        var keyval = item.Split(seps, 2);
        if (keyval.Length == 2)
          dic.Add(keyval[0], keyval[1]);
        else
          dic.Add(keyval[0], null);
      }
      return dic;
    }

  }


  public abstract class IKCMS_SparkView<TModel> : IKCMS_SparkView where TModel : class
  {
  }



}
