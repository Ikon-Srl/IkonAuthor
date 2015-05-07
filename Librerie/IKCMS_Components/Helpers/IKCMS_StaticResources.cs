using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Web;
using System.Xml.Linq;
using System.Web.Mvc;
using System.Web.Mvc.Html;
using System.Reflection;
using System.Globalization;
using System.Resources;
using LinqKit;
using Autofac;

using Ikon;
using Ikon.GD;
using Ikon.IKCMS.Library.Resources;
using Ikon.IKGD.Library.Resources;
using Ikon.IKGD.Library;


namespace Ikon.IKCMS
{

  public static class IKCMS_StaticResources
  {
    private static List<ResourceManager> ResourceManagers { get; set; }

    static IKCMS_StaticResources()
    {
      ResourceManagers = new List<ResourceManager>();
      try
      {
        var typeNames = Utility.Explode(IKGD_Config.AppSettings["StaticResourcesCmsHelper_Types"] ?? "ResourcesCustom,ResourcesExtra,ResourcesSite,ResourcesBase,ResourceIKCMS_Components", ",| ", " ", true).ToList();
        var types = typeNames.SelectMany(t => Utility.FindTypes(t)).Distinct().ToList();
        var props = types.Select(t => t.GetProperty("ResourceManager", BindingFlags.Static | BindingFlags.Public | BindingFlags.GetProperty)).Where(r => r != null).ToList();
        ResourceManagers = props.Select(r => r.GetValue(null, null)).Where(r => r != null).OfType<System.Resources.ResourceManager>().ToList();
      }
      catch { }
    }


    public static string GetString(string name)
    {
      return ResourceManagers.Select(r => r.GetString(name)).FirstOrDefault(s => s != null);
    }


    public static string GetString(string name, CultureInfo culture)
    {
      return ResourceManagers.Select(r => r.GetString(name, culture)).FirstOrDefault(s => s != null);
    }


  }

}
