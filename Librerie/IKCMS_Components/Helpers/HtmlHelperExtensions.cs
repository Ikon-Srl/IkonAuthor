using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Web;
using System.Xml.Linq;
using System.Web.Mvc;
using System.Web.Mvc.Html;
using System.IO;
using System.Reflection;
using Microsoft.Web.Mvc;
using LinqKit;
using Autofac;

using Ikon;
using Ikon.GD;


namespace Ikon.IKCMS
{

  public static class HtmlHelperExtension
  {


    public static Ikon.Filters.IKGD_HtmlCleaner HtmlFilter(this HtmlHelper helper)
    {
      return new Ikon.Filters.IKGD_HtmlCleaner();
    }


    // HtmlHelper helper puo' anche essere nullo
    public static string NormalizeObjectHtmlTag(this HtmlHelper helper, string source)
    {
      XElement xSource = null;
      try
      {
        xSource = XElement.Parse(source);
        XElement xEmbed = xSource.Elements().FirstOrDefault(x => x.Name.LocalName.ToLower() == "embed");
        XElement xParam = xSource.Elements().FirstOrDefault(x => x.Name.LocalName.ToLower() == "param" && x.Attributes().Any(a => a.Name.LocalName.ToLower() == "wmode"));
        if (xParam == null)
          xSource.AddFirst(new XElement("param", new XAttribute("name", "wmode"), new XAttribute("value", "transparent")));
        if (xEmbed != null && !xEmbed.Attributes().Any(a => a.Name.LocalName.ToLower() == "wmode"))
          xEmbed.Add(new XAttribute("wmode", "transparent"));
      }
      catch { }
      return (xSource != null) ? xSource.ToString() : source;
    }

    public static string NormalizeYoutubeIframe(string source, bool disable, bool autostart)
    {
      var result = source;
      try
      {
        //non posso usare XElement.Parse perchè di default non è formattato in modo corretto
        var indexOfSrc = source.IndexOf("src=");
        var src = System.Text.RegularExpressions.Regex.Replace(source.Substring(indexOfSrc, source.IndexOf(" ", indexOfSrc) - indexOfSrc), @"^""|""$|\\n?", "");
        var url = src.Substring(5, src.Length - 5);
        if (url.IndexOf("?") > 0)
        {
          url += "&wmode=transparent"; //forse bisogna mettere sempre al primo posto come querystring. vedi: http://www.electrictoolbox.com/float-div-youtube-iframe/
        }
        else
        {
          url += "?wmode=transparent";
        }
        if (autostart)
          url += "&autoplay=1";
        if (disable)
        {
          result = result.Replace(src.Substring(5, src.Length - 5), "javascript:;");
          result = result.Insert(result.IndexOf(" "), " data-src=\'" + url + "\'");
        }
        else
        {
          result = result.Replace(src.Substring(5, src.Length - 5), url);
        }

      }
      catch { }
      return result;
    }


    public static string RenderViewToString(ControllerContext controllerContext, ViewDataDictionary ViewData, string viewPath, string masterPath, bool? renderAsPartial)
    {
      string html = null;

      ViewEngineResult viewResult = null;
      try
      {
        if (controllerContext == null)
        {
          //creazione di un fakeControllerContext nel caso non sia disponibile nel caller
          var fakeController = new IkonWeb.Controllers.ProxyVFSController();
          controllerContext = new ControllerContext { HttpContext = new HttpContextWrapper(new HttpContext(System.Web.HttpContext.Current.Request, System.Web.HttpContext.Current.Response)) };
          controllerContext.RouteData.Values["controller"] = fakeController.GetType().Name;
          controllerContext.Controller = fakeController;
        }
        using (var sw = new StringWriter())
        {
          if (renderAsPartial.GetValueOrDefault(true))
          {
            viewResult = ViewEngines.Engines.FindPartialView(controllerContext, viewPath);
          }
          else
          {
            viewResult = ViewEngines.Engines.FindView(controllerContext, viewPath, masterPath);
          }
          var viewContext = new ViewContext(controllerContext, viewResult.View, ViewData, new TempDataDictionary(), sw);
          viewResult.View.Render(viewContext, sw);
          viewResult.ViewEngine.ReleaseView(controllerContext, viewResult.View);
          html = sw.GetStringBuilder().ToString();
        }
      }
      catch { }
      return html;
    }



    //
    // partialview with caching support
    //
    public static MvcHtmlString PartialCached(this HtmlHelper htmlHelper, string partialViewName, object model, string cacheKey, int? secondsDuration, IEnumerable<string> tablesDependencies)
    {
      return FS_OperationsHelpers.CachedEntityWrapper(cacheKey, () =>
      {
        return htmlHelper.Partial(partialViewName, model);
      }, secondsDuration, tablesDependencies);
    }

    public static MvcHtmlString PartialCached(this HtmlHelper htmlHelper, string partialViewName, ViewDataDictionary viewData, string cacheKey, int? secondsDuration, IEnumerable<string> tablesDependencies)
    {
      return FS_OperationsHelpers.CachedEntityWrapper(cacheKey, () =>
      {
        return htmlHelper.Partial(partialViewName, viewData);
      }, secondsDuration, tablesDependencies);
    }

    public static MvcHtmlString PartialCached(this HtmlHelper htmlHelper, string partialViewName, object model, ViewDataDictionary viewData, string cacheKey, int? secondsDuration, IEnumerable<string> tablesDependencies)
    {
      return FS_OperationsHelpers.CachedEntityWrapper(cacheKey, () =>
      {
        return htmlHelper.Partial(partialViewName, model, viewData);
      }, secondsDuration, tablesDependencies);
    }


    public static MvcHtmlString PartialCachedAutoKey(this HtmlHelper htmlHelper, string partialViewName, object model, int? secondsDuration, IEnumerable<string> tablesDependencies, params object[] cacheKeyFrags)
    {
      return FS_OperationsHelpers.CachedEntityWrapper(FS_OperationsHelpers.ContextHashNN(cacheKeyFrags), () =>
      {
        return htmlHelper.Partial(partialViewName, model);
      }, secondsDuration, tablesDependencies);
    }

    public static MvcHtmlString PartialCachedAutoKey(this HtmlHelper htmlHelper, string partialViewName, ViewDataDictionary viewData, int? secondsDuration, IEnumerable<string> tablesDependencies, params object[] cacheKeyFrags)
    {
      return FS_OperationsHelpers.CachedEntityWrapper(FS_OperationsHelpers.ContextHashNN(cacheKeyFrags), () =>
      {
        return htmlHelper.Partial(partialViewName, viewData);
      }, secondsDuration, tablesDependencies);
    }

    public static MvcHtmlString PartialCachedAutoKey(this HtmlHelper htmlHelper, string partialViewName, object model, ViewDataDictionary viewData, int? secondsDuration, IEnumerable<string> tablesDependencies, params object[] cacheKeyFrags)
    {
      return FS_OperationsHelpers.CachedEntityWrapper(FS_OperationsHelpers.ContextHashNN(cacheKeyFrags), () =>
      {
        return htmlHelper.Partial(partialViewName, model, viewData);
      }, secondsDuration, tablesDependencies);
    }


    //
    // helpers per il rendering dei placeholders
    //

    public static MvcHtmlString RenderPlaceholderCached(this HtmlHelper htmlHelper, IKCMS_ModelCMS_Interface model, PlaceholderRenderParams options, int? secondsDuration) { return RenderPlaceholderCached(htmlHelper, model, options, secondsDuration, FS_OperationsHelpers.Const_CacheDependencyIKGD_vNode_vData_iNode_Relation_Property); }
    public static MvcHtmlString RenderPlaceholderCached(this HtmlHelper htmlHelper, IKCMS_ModelCMS_Interface model, PlaceholderRenderParams options, int? secondsDuration, IEnumerable<string> tablesDependencies)
    {
      string cacheKey = FS_OperationsHelpers.ContextHashNN(model.sNode, options.ContainerPlaceHolderName, options.Recursive, options.SkipItems, options.MaxItems, options.ContainerCssClassForced, options.ContainerCssClassDefault);
      return FS_OperationsHelpers.CachedEntityWrapper(cacheKey, () =>
      {
        return RenderPlaceholder(htmlHelper, model, options);
      }, secondsDuration, tablesDependencies);
    }

    public static MvcHtmlString RenderPlaceholder(this HtmlHelper htmlHelper, IKCMS_ModelCMS_Interface model, PlaceholderRenderParams options) { return RenderPlaceholder(htmlHelper, model, null, options); }
    // attenzione al filter perche' la ricorsione viene fermata prima di applicare il filter
    public static MvcHtmlString RenderPlaceholder(this HtmlHelper htmlHelper, IKCMS_ModelCMS_Interface model, Func<IKCMS_ModelCMS_GenericBrickInterface, bool> filter, PlaceholderRenderParams options)
    {
      MvcHtmlString result = null;
      try
      {
        if (options != null)
        {
          List<IKCMS_ModelCMS_GenericBrickInterface> elements = null;
          model = model ?? (htmlHelper.ViewData.Model as IKCMS_ModelCMS_Interface);
          if (options.UseModelRootAsReference.GetValueOrDefault(true) && model != null)
          {
            model = model.ModelRootOrContext ?? model;
          }
          if (filter == null)
            filter = m => true;
          bool recursive = options.Recursive.GetValueOrDefault(false);
          if (recursive)
          {
            elements = IKCMS_ManagerIoC.applicationContainer.Resolve<IKCMS_ModelScannerParentPlaceholder_Bricks>(new NamedParameter("placeholders", options.ContainerPlaceHolderName)).FindModels(model).OfType<IKCMS_ModelCMS_GenericBrickInterface>().Where(filter).ToList();
          }
          else
          {
            var placeholders = Utility.Explode(options.ContainerPlaceHolderName, ",", " ", true);
            if (placeholders.Any())
            {
              elements = model.Models.OfType<IKCMS_ModelCMS_GenericBrickInterface>().Where(m => placeholders.Contains(m.Placeholder)).Where(filter).ToList();
            }
            else
            {
              elements = model.Models.OfType<IKCMS_ModelCMS_GenericBrickInterface>().Where(m => m.Placeholder == options.ContainerPlaceHolderName).Where(filter).ToList();
            }
          }
          result = RenderPlaceholderWorker(htmlHelper, model, elements, options);
        }
      }
      catch { }
      return result;
    }


    public static MvcHtmlString RenderPlaceholderWorker(this HtmlHelper htmlHelper, IKCMS_ModelCMS_Interface model, List<IKCMS_ModelCMS_GenericBrickInterface> elements, PlaceholderRenderParams options)
    {
      StringBuilder sb = new StringBuilder();
      //
      try
      {
        if (options != null && options.ContainerPlaceHolderName.IsNotNullOrWhiteSpace())
        {
          model = model ?? (htmlHelper.ViewData.Model as IKCMS_ModelCMS_Interface);
          var skipItems = options.SkipItems ?? 0;
          var maxItems = options.MaxItems ?? int.MaxValue;
          if (elements != null && elements.Any())
          {
            //
            ViewDataDictionary viewData = new ViewDataDictionary(htmlHelper.ViewData);
            viewData[TeasersHelperExtension.ViewDataPlaceholderName] = options.ContainerPlaceHolderName;
            viewData[TeasersHelperExtension.ViewDataContainerCssClassForced] = options.ContainerCssClassForced;
            viewData[TeasersHelperExtension.ViewDataContainerCssClassDefault] = options.ContainerCssClassDefault;
            viewData[TeasersHelperExtension.ViewDataContainerCssClass] = options.ContainerCssClass;
            viewData[TeasersHelperExtension.ViewDataContentCssClass] = options.ContentCssClass;
            viewData[TeasersHelperExtension.ViewDataStreamSelector] = options.StreamSelector;
            viewData[TeasersHelperExtension.ViewDataContainerVisibleItemsCount] = Math.Max(options.VisibleItemsCount.GetValueOrDefault(1), 1);
            //
            int rowNumber = 0;
            int accumulatedSize = 0;
            int? maxRowWidth = options.MaxRowWidth;
            if (options.TemplateRowSeparator.IsNullOrWhiteSpace())
            {
              maxRowWidth = null;
            }
            //
            int index = 0;
            foreach (var element in elements)
            {
              string template = options.TemplateForced ?? element.TemplateInfo.ViewPath ?? options.TemplateDefault;
              //
              //sb.AppendLine(string.Format("{0}:{1}:{2}:{3}<br/>\n", element.sNode, Utility.Implode(element.Relations.Select(r => r.rnode_dst), ","), element.vfsNode.GetType(), template));
              //
              if (template.IsNotNullOrWhiteSpace())
              {
                try
                {
                  viewData[TeasersHelperExtension.ViewDataItemIndex] = index;
                  if (element != null && element is IKCMS_ModelCMS_HasTemplateInfo_Interface)
                  {
                    try { viewData[TeasersHelperExtension.ViewDataContainerTemplateName] = (element as IKCMS_ModelCMS_HasTemplateInfo_Interface).TemplateInfo.Name; }
                    catch { }
                  }
                  string markup = null;
                  try { markup = htmlHelper.Partial(template, element, viewData).ToString().TrimSafe(" \n\r\t".ToCharArray()); }
                  catch (Exception ex) { markup = ex.Message; }
                  if (markup.IsNotEmpty())
                  {
                    if (skipItems-- > 0)
                      continue;
                    try
                    {
                      if (maxRowWidth > 0 && TeasersHelperExtension.TeaserSizeAccumulator(element, ref accumulatedSize, maxRowWidth.Value, options.teaserSizeAccumulatorDirection, options.teaserSizeAccumulatorSequence))
                      {
                        sb.AppendLine(htmlHelper.Partial(options.TemplateRowSeparator, rowNumber++).ToString());
                      }
                    }
                    catch { }
                    index++;
                    sb.AppendLine(markup);
                    if (--maxItems < 0)
                      break;
                  }
                }
                catch { }
              }
            }
            //
            if (options.RenderClosingRowSeparator.GetValueOrDefault(false) && maxRowWidth > 0 && sb.Length > 0 && accumulatedSize > 0)
            {
              try { sb.AppendLine(htmlHelper.Partial(options.TemplateRowSeparator, rowNumber++).ToString()); }
              catch { }
            }
          }
          else if (options.TemplateVOID.IsNotNullOrWhiteSpace())
          {
            try { sb.AppendLine(htmlHelper.Partial(options.TemplateVOID).ToString()); }
            catch { }
          }
        }
      }
      catch { }
      return MvcHtmlString.Create(sb.ToString());
    }


    public static MvcHtmlString RenderGenericBrick(this HtmlHelper htmlHelper, IKCMS_ModelCMS_Interface model, PlaceholderRenderParams options)
    {
      MvcHtmlString markup = null;
      try
      {
        if (model != null)
        {
          ViewDataDictionary viewData = null;
          string containerTemplate = options.ContainerPlaceHolderName;
          if (containerTemplate.IsNullOrWhiteSpace() && model.ModelParent != null && model.ModelParent is IKCMS_ModelCMS_HasTemplateInfo_Interface)
          {
            containerTemplate = (model.ModelParent as IKCMS_ModelCMS_HasTemplateInfo_Interface).TemplateInfo.Name;
          }
          if (containerTemplate.IsNullOrWhiteSpace())
          {
            viewData = htmlHelper.ViewData;
          }
          else
          {
            viewData = new ViewDataDictionary(htmlHelper.ViewData);
            viewData[TeasersHelperExtension.ViewDataContainerTemplateName] = containerTemplate;
          }
          //
          viewData[TeasersHelperExtension.ViewDataContainerCssClass] = options.ContainerCssClass ?? viewData[TeasersHelperExtension.ViewDataContainerCssClass];
          viewData[TeasersHelperExtension.ViewDataContentCssClass] = options.ContentCssClass ?? viewData[TeasersHelperExtension.ViewDataContentCssClass];
          viewData[TeasersHelperExtension.ViewDataStreamSelector] = options.StreamSelector ?? viewData[TeasersHelperExtension.ViewDataStreamSelector];
          viewData[TeasersHelperExtension.ViewDataContainerVisibleItemsCount] = Math.Max(options.VisibleItemsCount.GetValueOrDefault((int)(viewData[TeasersHelperExtension.ViewDataContainerVisibleItemsCount] ?? 1)), 1);
          viewData[TeasersHelperExtension.ViewDataItemIndex] = options.Index;
          //
          string modelTemplate = null;
          if (model is IKCMS_ModelCMS_HasTemplateInfo_Interface)
          {
            var templateInfo = (model as IKCMS_ModelCMS_HasTemplateInfo_Interface).TemplateInfo;
            if (templateInfo != null)
            {
              if (templateInfo.Name == "NULL" && options.TemplateDefault.IsNotNullOrWhiteSpace())
                modelTemplate = null;
              else
                modelTemplate = templateInfo.ViewPath;
            }
          }
          string template = options.TemplateForced.NullIfEmpty() ?? modelTemplate.NullIfEmpty() ?? options.TemplateDefault;
          if (template.IsNotNullOrWhiteSpace())
          {
            try { markup = htmlHelper.Partial(template, model, viewData); }
            catch { }
          }
          if ((markup == null || MvcHtmlString.IsNullOrEmpty(markup)) && options.TemplateVOID.IsNotNullOrWhiteSpace())
          {
            try { markup = htmlHelper.Partial(options.TemplateVOID); }
            catch { }
          }
        }
      }
      catch { }
      return markup;
    }




  }


  public class PlaceholderRenderParams
  {
    //
    public bool? UseModelRootAsReference { get; set; }
    //
    public string ContainerPlaceHolderName { get; set; }  //nome del placeholder o del containerTemplateName nel caso di rendering di teaser items (che il placeholder lo hanno gia' nel viewdata)
    public bool? Recursive { get; set; }
    public int? SkipItems { get; set; }
    public int? MaxItems { get; set; }
    public int? VisibleItemsCount { get; set; }
    //
    public int Index { get; set; }
    //
    public string TemplateDefault { get; set; }  //per specificare un template nel caso non fosse definito dal model
    public string TemplateForced { get; set; }  //per la forzatura del template ignorando quello definito dai models
    public string TemplateVOID { get; set; }  //template da usare nel caso non ci siano elementi da renderizzare
    //
    public string StreamSelector { get; set; }  //per la gestione delle immagini
    //
    public string ContentCssClass { get; set; }
    public string ContainerCssClassForced { get; set; }  //nome della classe CSS custom assegnata al container dei teaser
    public string ContainerCssClass { get; set; }  //nome della classe CSS custom assegnata al container dei teaser
    public string ContainerCssClassDefault { get; set; }  //nome della classe CSS custom assegnata al container dei teaser
    //
    public int? MaxRowWidth { get; set; }
    public string TemplateRowSeparator { get; set; }
    public bool? RenderClosingRowSeparator { get; set; }
    public TeasersHelperExtension.TeaserSizeAccumulatorDirection? teaserSizeAccumulatorDirection { get; set; }  // per default lasciare a null
    public TeasersHelperExtension.TeaserSizeAccumulatorSequence? teaserSizeAccumulatorSequence { get; set; }  // per default lasciare a null
    //
  }


}
