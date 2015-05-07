using System;
using System.Text;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using System.IO;
using Newtonsoft.Json;


namespace IkonWeb.Controllers
{
  public class JsonFilterAttribute : ActionFilterAttribute
  {
    public Type ParameterType { get; set; }
    public string ParameterName { get; set; }

    public override void OnActionExecuting(ActionExecutingContext filterContext)
    {
      object result = null;
      try
      {
        using (var sr = new StreamReader(filterContext.HttpContext.Request.InputStream))
        {
          sr.BaseStream.Seek(0, SeekOrigin.Begin);  //attenzione che con .NET4 o MVC3 lo stream non e' piu' all'inizio
          string jsonString = sr.ReadToEnd();
          JsonSerializerSettings DefaultSerializerSettings = new JsonSerializerSettings() { NullValueHandling = NullValueHandling.Include, ConstructorHandling = ConstructorHandling.AllowNonPublicDefaultConstructor };
          result = JsonConvert.DeserializeObject(jsonString, ParameterType, DefaultSerializerSettings);
          //JsonSerializer serializer = JsonSerializer.Create(new JsonSerializerSettings { });
          //var result = serializer.Deserialize(sr, ParameterType);
        }
      }
      catch { }
      filterContext.ActionParameters[ParameterName] = result;
    }
  }


}
