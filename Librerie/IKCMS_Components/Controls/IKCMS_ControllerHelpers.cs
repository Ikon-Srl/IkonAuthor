/*
 * 
 * Intranet Ikon
 * 
 * Copyright (C) 2009 Ikon Srl
 * Tutti i Diritti Riservati. All Rights Reserved
 * 
 */


using System;
using System.Data;
using System.Configuration;
using System.Collections;
using System.Collections.Specialized;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;
using System.Data.Linq;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Security;
using System.Linq.Expressions;
using System.Reflection;
using System.Data.Linq.Mapping;
using System.Diagnostics;
using System.Transactions;
using System.Web;
using System.Web.Caching;
using System.Web.UI.WebControls;
using System.Web.Mvc;
using System.Web.Mvc.Html;
using System.Web.Mvc.Ajax;
using System.Web.Routing;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;  // assembly System.ServiceModel.Web
using LinqKit;

using Ikon;
using Ikon.Config;
using Ikon.GD;


namespace Ikon.IKCMS
{


  public static class IKCMS_ControllerHelper_Extension
  {


    public static T GetParameterFromController<T>(this ControllerBase controller, string parameterName) { return GetParameterFromController<T>(controller, parameterName, default(T)); }
    public static T GetParameterFromController<T>(this ControllerBase controller, string parameterName, T defaultT)
    {
      T result = defaultT;
      try { return (T)controller.ValueProvider.GetValue(parameterName).ConvertTo(typeof(T)); }
      catch { }
      try { return (T)controller.ValueProvider.GetValue(parameterName).ConvertTo(typeof(T), System.Globalization.CultureInfo.InvariantCulture); }
      catch { }
      return defaultT;
    }


  }

}