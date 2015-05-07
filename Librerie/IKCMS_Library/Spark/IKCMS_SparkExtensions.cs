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

  public static class IKCMS_SparkInitializer
  {
    /*
    public static void RegisterSparkViewEngine()
    {
      SparkServiceContainer sparkServices = (SparkServiceContainer)SparkEngineStarter.CreateContainer();
      DefaultDescriptorBuilder descriptorsBuilder = (DefaultDescriptorBuilder)sparkServices.GetService<IDescriptorBuilder>();
      //var srv02 = sparkServices.GetService<Spark.FileSystem.IViewFolder>();
      //var srv03 = sparkServices.GetService<ISparkSettings>();
      descriptorsBuilder.Filters.Add(new IKCMS_SparkBrainDamaged_DescriptorFilter());
      //descriptorsBuilder.Filters.Add(Spark.Web.Mvc.Descriptors.LanguageDescriptorFilter.For(c => "it"));
      //descriptorsBuilder.Filters.Add(Spark.Web.Mvc.Descriptors.LanguageDescriptorFilter.For(c => "it-IT"));
      //
      SparkEngineStarter.RegisterViewEngine(ViewEngines.Engines, sparkServices);
      //
    }
    */

    public static void RegisterSparkViewEngine() { RegisterSparkViewEngine(null, null); }
    public static void RegisterSparkViewEngine(Action<SparkServiceContainer> sparkServicesConfigurator, Action<DefaultDescriptorBuilder> descriptorBuilderConfigurator)
    {
      SparkServiceContainer sparkServices = (SparkServiceContainer)SparkEngineStarter.CreateContainer();
      DefaultDescriptorBuilder descriptorsBuilder = (DefaultDescriptorBuilder)sparkServices.GetService<IDescriptorBuilder>();
      //var srv02 = sparkServices.GetService<Spark.FileSystem.IViewFolder>();
      //var srv03 = sparkServices.GetService<ISparkSettings>();
      //descriptorsBuilder.Filters.Add(Spark.Web.Mvc.Descriptors.LanguageDescriptorFilter.For(c => "it"));
      //descriptorsBuilder.Filters.Add(Spark.Web.Mvc.Descriptors.LanguageDescriptorFilter.For(c => "it-IT"));
      //
      if (sparkServicesConfigurator != null)
      {
        try { sparkServicesConfigurator(sparkServices); }
        catch { }
      }
      //
      descriptorsBuilder.Filters.Add(new IKCMS_SparkBrainDamaged_DescriptorFilter());
      //
      if (descriptorBuilderConfigurator != null)
      {
        try { descriptorBuilderConfigurator(descriptorsBuilder); }
        catch { }
      }
      //
      SparkEngineStarter.RegisterViewEngine(ViewEngines.Engines, sparkServices);
      //
    }

  }



  //
  // il view engine di spark combina delle cazzate belle e buone quando si tratta di cercare le partial views sul filesystem
  // e scoppia se uno tenta di caricare una partial view con
  // #Html.RenderPartial("~/Views/Home/Prova/_HeaderTest", ViewData.Model);
  // per cui deve essere registrato un handler che riconosca l'uso di un virtualpath e lo tratti adeguatamente
  // nel caso si debba usare un virtualpath NON si deve usare l'estensione .spark altrimenti il sistema usa il pagebuilder di asp.net
  //
  public class IKCMS_SparkBrainDamaged_DescriptorFilter : DescriptorFilterBase
  {

    public override void ExtraParameters(ControllerContext context, IDictionary<string, object> extra)
    {
      string areaName = GetAreaName(context.RouteData);
      if (!string.IsNullOrEmpty(areaName))
        extra["area"] = areaName;
      //
      // per gestire dinamicamente il mapping delle lingue sulle view/partial
      extra["language"] = IKGD_Language_Provider.Provider.LanguageNN;
      //
      // per gestire dinamicamente i templates accessibili e' necessario customizzare il Dictionary extra
      // in quanto PotentialLocations non viene piu' chiamata dopo che e' stata risolta una view per un Dictionary extra
      string viewMode = IKGD_SiteMode.ModeCombined;
      //
      if (!string.IsNullOrEmpty(viewMode))
        extra["viewMode"] = viewMode;
    }


    private static Regex Rx_ModeSubExt = new Regex(@"\.spark$", RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.Singleline);
    public override IEnumerable<string> PotentialLocations(IEnumerable<string> locations, IDictionary<string, object> extra)
    {
      try
      {
        var exts = IKGD_SiteMode.GetVfsExtListWithEmpty;
        string lang = (extra != null && extra.ContainsKey("language")) ? (string)extra["language"] : null;
        if (lang.IsNotEmpty())
        {
          exts = exts.SelectMany(r => new string[] { r + "." + lang, r }).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        }
        locations = locations.Where(s => s.IsNotEmpty()).SelectMany(s => !Rx_ModeSubExt.IsMatch(s) ? Enumerable.Repeat(s, 1) : exts.Select(f => Rx_ModeSubExt.Replace(s, f + ".spark"))).Distinct();
        if (locations.Any(p => p.IndexOf('~') >= 0))
        {
          return locations.Select(p => p.Substring(p.IndexOf('~'))).Distinct();
        }
      }
      catch { }
      //
      string areaName;
      TryGetString(extra, "area", out areaName);
      return string.IsNullOrEmpty(areaName) ? locations : locations.Select(x => Path.Combine(areaName, x)).Concat(locations);
    }


    private static string GetAreaName(RouteBase route)
    {
      var routeWithArea = route as IRouteWithArea;
      if (routeWithArea != null)
      {
        return routeWithArea.Area;
      }

      var castRoute = route as Route;
      if (castRoute != null && castRoute.DataTokens != null)
      {
        return castRoute.DataTokens["area"] as string;
      }

      return null;
    }


    private static string GetAreaName(RouteData routeData)
    {
      object area;
      if (routeData.DataTokens.TryGetValue("area", out area))
      {
        return area as string;
      }
      if (routeData.Values.TryGetValue("area", out area))
      {
        return area as string;
      }

      return GetAreaName(routeData.Route);
    }

  }



}
