using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Text.RegularExpressions;
using System.Web;
using System.Xml.Linq;
using System.Web.Mvc;
using System.Web.Mvc.Html;
using System.Reflection;
using System.Web.Routing;
using System.IO;
using LinqKit;
using Autofac;

using Ikon;
using Ikon.GD;
using Ikon.IKCMS.Library.Resources;
using Ikon.IKGD.Library.Resources;


namespace Ikon.IKCMS
{


  public static class IKCMS_ModelGenericBricksHelpers
  {
    //private static FS_Operations fsOp { get { return IKCMS_ManagerIoC.requestContainer.Resolve<FS_Operations>(); } }


    //
    // usage examples:
    //
    // # var bricks = Model.Models.OfType<IKCMS_ModelCMS_GenericBrickInterface>().ToList();
    // ${bricks.FirstOrDefault().ImageCMS("thumb_small")}
    // ${bricks.FirstOrDefault().ImageCMS_WithLink("thumb_small", "thumb_home", null, "autoPopup", null, new { rel = "gallery" })}
    // <div class="autoHideIfNoTags">${bricks.FirstOrDefault().ImageCMS_WithLink("thumb_small", "thumb_home", null, "autoPopup", null, new { rel = "gallery" })}</div>
    //


    public static IEnumerable<IKCMS_ModelCMS_GenericBrickInterface> FilterSubModels(this IKCMS_ModelCMS_Interface model, Regex FilterManagerType, Regex FilterCategory)
    {
      try
      {
        IEnumerable<IKCMS_ModelCMS_GenericBrickInterface> models = model.Models.OfType<IKCMS_ModelCMS_GenericBrickInterface>();
        if (FilterManagerType != null)
          models = models.Where(m => FilterManagerType.IsMatch(m.ManagerType));
        if (FilterCategory != null)
          models = models.Where(m => FilterCategory.IsMatch(m.Category));
        return models;
      }
      catch { }
      return Enumerable.Empty<IKCMS_ModelCMS_GenericBrickInterface>();
    }


    //public static bool FilterModel(this IKCMS_ModelCMS_Interface model, Regex FilterManagerType, Regex FilterCategory)
    //{
    //  bool result = true;
    //  try
    //  {
    //    if (result && FilterManagerType != null)
    //      result &= FilterManagerType.IsMatch(model.ManagerType);
    //    if (result && FilterCategory != null)
    //      result &= FilterCategory.IsMatch(model.Category);
    //  }
    //  catch { }
    //  return result;
    //}


    //
    // render di immagini CMS
    //
    public static MvcHtmlString ImageCMS(this IKCMS_ModelCMS_GenericBrickInterface model, string stream) { return ImageCMS(model, stream, null, null, null, null, null); }
    public static MvcHtmlString ImageCMS(this IKCMS_ModelCMS_GenericBrickInterface model, string stream, object htmlAttributes) { return ImageCMS(model, stream, null, null, new RouteValueDictionary(htmlAttributes), null, null); }
    public static MvcHtmlString ImageCMS(this IKCMS_ModelCMS_GenericBrickInterface model, string stream, string ClassCSS, object htmlAttributes) { return ImageCMS(model, stream, null, ClassCSS, new RouteValueDictionary(htmlAttributes), null, null); }
    public static MvcHtmlString ImageCMS(this IKCMS_ModelCMS_GenericBrickInterface model, string stream, string ClassCSS, object htmlAttributes, bool? fullUrl) { return ImageCMS(model, stream, null, ClassCSS, new RouteValueDictionary(htmlAttributes), null, fullUrl); }

    public static MvcHtmlString ImageCMS_WithDefault(this IKCMS_ModelCMS_GenericBrickInterface model, string stream, string defaultImage) { return ImageCMS(model, stream, defaultImage, null, null, null, null); }
    public static MvcHtmlString ImageCMS_WithDefault(this IKCMS_ModelCMS_GenericBrickInterface model, string stream, string defaultImage, object htmlAttributes) { return ImageCMS(model, stream, defaultImage, null, new RouteValueDictionary(htmlAttributes), null, null); }
    public static MvcHtmlString ImageCMS_WithDefault(this IKCMS_ModelCMS_GenericBrickInterface model, string stream, string defaultImage, string ClassCSS, object htmlAttributes) { return ImageCMS(model, stream, defaultImage, ClassCSS, new RouteValueDictionary(htmlAttributes), null, null); }

    public static MvcHtmlString ImageCMS(this IKCMS_ModelCMS_GenericBrickInterface model, string stream, string defaultImage, string ClassCSS, IDictionary<string, object> htmlAttributes, bool? streamCheck) { return ImageCMS(model, stream, defaultImage, ClassCSS, htmlAttributes, streamCheck, null); }
    public static MvcHtmlString ImageCMS(this IKCMS_ModelCMS_GenericBrickInterface model, string stream, string defaultImage, string ClassCSS, IDictionary<string, object> htmlAttributes, bool? streamCheck, bool? fullUrl)
    {
      TagBuilder tag = ImageCMS_Worker(model, stream, defaultImage, ClassCSS, htmlAttributes, streamCheck, fullUrl);
      if (tag != null && tag.Attributes.ContainsKey("src"))
        return MvcHtmlString.Create(tag.ToString(TagRenderMode.SelfClosing));
      return MvcHtmlString.Create(null);
    }

    public static MvcHtmlString ImageCMS_Delayed(this IKCMS_ModelCMS_GenericBrickInterface model, string stream, string defaultImage, string ClassCSS, IDictionary<string, object> htmlAttributes, bool? streamCheck, bool? activateDelayedLoading)
    {
      TagBuilder tag = ImageCMS_Worker(model, stream, defaultImage, ClassCSS, htmlAttributes, streamCheck, null);
      if (tag != null && tag.Attributes.ContainsKey("src"))
      {
        if (activateDelayedLoading == true)
        {
          //tag.MergeAttribute("rel", tag.Attributes["src"], true);  // non e' supportato con la validazione strict
          //tag.MergeAttribute("src", "javascript:;", true);
          //tag.Attributes.Remove("alt");  // rimozione dell'attributo alt per evitare la comparsa di testi fuoricontrollo prima del loading delle risorse
          string url = tag.Attributes["src"];
          tag.MergeAttribute("src", "javascript:;//" + url, true);
        }
        return MvcHtmlString.Create(tag.ToString(TagRenderMode.SelfClosing));
      }
      return MvcHtmlString.Create(null);
    }


    //
    // render di un tag <a> contenente un tag <img>
    // se <a> non ha un href valido renderizza solo il tag <img>
    //
    public static MvcHtmlString ImageCMS_WithLink(this IKCMS_ModelCMS_GenericBrickInterface model, string StreamForImage, string StreamForLink, string ImageClassCSS, string LinkClassCSS, object ImageHtmlAttributes, object LinkHtmlAttributes) { return ImageCMS_WithLink(model, StreamForImage, StreamForLink, ImageClassCSS, LinkClassCSS, new RouteValueDictionary(ImageHtmlAttributes), new RouteValueDictionary(LinkHtmlAttributes), null); }
    public static MvcHtmlString ImageCMS_WithLink(this IKCMS_ModelCMS_GenericBrickInterface model, string StreamForImage, string StreamForLink, string ImageClassCSS, string LinkClassCSS, object ImageHtmlAttributes, object LinkHtmlAttributes, bool? forceDownload) { return ImageCMS_WithLink(model, StreamForImage, StreamForLink, ImageClassCSS, LinkClassCSS, new RouteValueDictionary(ImageHtmlAttributes), new RouteValueDictionary(LinkHtmlAttributes), forceDownload); }
    public static MvcHtmlString ImageCMS_WithLink(this IKCMS_ModelCMS_GenericBrickInterface model, string StreamForImage, string StreamForLink, string ImageClassCSS, string LinkClassCSS, IDictionary<string, object> ImageHtmlAttributes, IDictionary<string, object> LinkHtmlAttributes, bool? forceDownload)
    {
      TagBuilder builder = new TagBuilder("a");
      if (!string.IsNullOrEmpty(LinkClassCSS))
        builder.AddCssClass(LinkClassCSS);
      MultiStreamInfo4Settings streamInfo = model.StreamInfos(StreamForLink);
      if (streamInfo != null)
      {
        string extraPathInfo = null;
        string fileExt = null;
        if (streamInfo != null)
        {
          string fileNameOrig = (model.vfsNode != null && model.vfsNode.iNode != null) ? model.vfsNode.iNode.filename : null;
          NormalizeFilenameAndExt(streamInfo, fileNameOrig, out extraPathInfo, out fileExt);
        }
        string url = IKCMS_RouteUrlManager.GetUrlProxyVFS(model.rNode, null, StreamForLink, null, null, false, null, forceDownload, extraPathInfo);
        string mime = (streamInfo.Mime ?? string.Empty).ToLowerInvariant();
        if (mime.StartsWith("image/"))
        {
          url = Utility.UriAddQuery(url, "ext", fileExt ?? ("." + mime.Substring("image/".Length)));
        }
        builder.MergeAttribute("href", url, true);
      }
      //
      if (LinkHtmlAttributes != null && LinkHtmlAttributes.Any())
        builder.MergeAttributes<string, object>(LinkHtmlAttributes, false);
      //
      TagBuilder tagImg = ImageCMS_Worker(model, StreamForImage, null, ImageClassCSS, ImageHtmlAttributes, null, null);
      if (builder.Attributes.ContainsKey("href"))
      {
        if (tagImg != null && tagImg.Attributes.ContainsKey("src"))
        {
          builder.InnerHtml = tagImg.ToString(TagRenderMode.SelfClosing);  //TODO: verificare se serve l'encoding
        }
        return MvcHtmlString.Create(builder.ToString(TagRenderMode.Normal));
      }
      else if (tagImg != null && tagImg.Attributes.ContainsKey("src"))
      {
        return MvcHtmlString.Create(tagImg.ToString(TagRenderMode.SelfClosing));
      }
      return MvcHtmlString.Create(null);
    }


    private static TagBuilder ImageCMS_Worker(this IKCMS_ModelCMS_GenericBrickInterface model, string stream, string defaultImage, string ClassCSS, IDictionary<string, object> htmlAttributes, bool? streamCheck, bool? fullUrl)
    {
      TagBuilder builder = new TagBuilder("img");
      try
      {
        string url = defaultImage;
        //builder.InnerHtml = !string.IsNullOrEmpty(linkText) ? HttpUtility.HtmlEncode(linkText) : string.Empty;
        //
        MultiStreamInfo4Settings streamInfo = null;
        if (model != null)
          streamInfo = model.StreamInfos(stream);
        IKCMS_ResourceType_ImageCMS resource = null;
        if (model != null && model.VFS_ResourceObject is IKCMS_ResourceType_ImageCMS)
          resource = (model.VFS_ResourceObject as IKCMS_ResourceType_ImageCMS);
        //
        if (!string.IsNullOrEmpty(ClassCSS))
          builder.AddCssClass(ClassCSS);
        htmlAttributes = htmlAttributes ?? new RouteValueDictionary();
        //
        if (!htmlAttributes.ContainsKey("alt"))
        {
          if (resource != null)
          {
            string alt = resource.ResourceSettings.Alt;
            //alt = alt ?? Utility.FindPropertySafe<string>(model.VFS_ResourceObjectData, "Alt");
            //alt = alt ?? model.VFS_ResourceLanguageKVT("Alt").ValueString;
            alt = alt ?? model.Name;
            htmlAttributes["alt"] = alt;
          }
          else if (model != null)
          {
            htmlAttributes["alt"] = model.Name;
          }
          else
          {
            htmlAttributes["alt"] = ".";
          }
        }
        //
        if (htmlAttributes.Any())
          builder.MergeAttributes<string, object>(htmlAttributes, false);
        //
        // rendering del link per la generazione del tag flash
        //
        if (streamInfo != null && (streamInfo.Mime ?? string.Empty).IndexOf("flash", StringComparison.OrdinalIgnoreCase) >= 0)
        {
          TagBuilder builderFlash = new TagBuilder("a");
          builderFlash.MergeAttributes(builder.Attributes, true);
          string extraPathInfo = (streamInfo != null && IKGD_ExternalVFS_Support.IsValidFileName(streamInfo.OrigNoPath)) ? streamInfo.OrigNoPath : null;
          url = IKCMS_RouteUrlManager.GetUrlProxyVFS(model.rNode, null, stream, null, null, false, null, false, extraPathInfo);
          if (fullUrl.GetValueOrDefault(false))
            url = Utility.ResolveUrlFull(url);
          else if (url.StartsWith("~/"))
            url = Utility.ResolveUrl(url);
          builderFlash.MergeAttribute("href", url, true);
          List<string> flashAttrs = new List<string>();
          if (streamInfo.Dimensions.X > 0 && streamInfo.Dimensions.Y > 0)
          {
            flashAttrs.Add(string.Format("width:{0}", streamInfo.Dimensions.X));
            flashAttrs.Add(string.Format("height:{0}", streamInfo.Dimensions.Y));
          }
          flashAttrs.Add("attrs:{ wmode:'transparent', scale:'noscale', allowFullScreen:'true', quality:'high', allowScriptAccess:'sameDomain', bgColor:'#ffffff' }");
          builderFlash.AddCssClass(" {" + Utility.Implode(flashAttrs, ", ") + "}");
          return builderFlash;
        }
        //
        if (streamCheck.GetValueOrDefault(true) == false || (streamInfo != null && !string.IsNullOrEmpty(streamInfo.Mime) && streamInfo.Mime.StartsWith("image/", StringComparison.OrdinalIgnoreCase)))
        {
          //string extraPathInfo = (streamInfo != null && IKGD_ExternalVFS_Support.IsValidFileName(streamInfo.OrigNoPath)) ? streamInfo.OrigNoPath : null;
          string extraPathInfo = null;
          string fileExt = null;
          if (streamInfo != null)
          {
            string fileNameOrig = (model.vfsNode != null && model.vfsNode.iNode != null) ? model.vfsNode.iNode.filename : null;
            NormalizeFilenameAndExt(streamInfo, fileNameOrig, out extraPathInfo, out fileExt);
          }
          url = IKCMS_RouteUrlManager.GetUrlProxyVFS(model.rNode, null, stream, null, null, false, null, false, extraPathInfo);
          if (streamInfo != null && (streamInfo.Mime ?? string.Empty).StartsWith("image/", StringComparison.OrdinalIgnoreCase))
          {
            url = Utility.UriAddQuery(url, "ext", fileExt ?? ("." + streamInfo.Mime.Substring("image/".Length)));
          }
          if (streamInfo != null && streamInfo.Dimensions.X > 0 && streamInfo.Dimensions.Y > 0)
          {
            builder.MergeAttribute("width", streamInfo.Dimensions.X.ToString(), false);
            builder.MergeAttribute("height", streamInfo.Dimensions.Y.ToString(), false);
          }
        }
        if (!string.IsNullOrEmpty(url))
        {
          if (fullUrl.GetValueOrDefault(false))
            url = Utility.ResolveUrlFull(url);
          else if (url.StartsWith("~/"))
            url = Utility.ResolveUrl(url);
          builder.MergeAttribute("src", url, true);
        }
        //
        if (!string.IsNullOrEmpty(url))
          return builder;
        //
      }
      catch { }
      return builder;
    }


    public static string GetStreamUrl(this IKCMS_ModelCMS_GenericBrickInterface model, string stream) { return GetStreamUrl(model, stream, null, null); }
    public static string GetStreamUrl(this IKCMS_ModelCMS_GenericBrickInterface model, string stream, bool? streamCheck, bool? fullUrl)
    {
      string url = null;
      try
      {
        if (model != null)
        {
          MultiStreamInfo4Settings streamInfo = null;
          if (model != null)
            streamInfo = model.StreamInfos(stream);
          //
          if (streamCheck.GetValueOrDefault(true) == false || streamInfo != null)
          {
            //string extraPathInfo = (streamInfo != null && IKGD_ExternalVFS_Support.IsValidFileName(streamInfo.OrigNoPath)) ? streamInfo.OrigNoPath : null;
            string extraPathInfo = null;
            string fileExt = null;
            if (streamInfo != null)
            {
              string fileNameOrig = (model.vfsNode != null && model.vfsNode.iNode != null) ? model.vfsNode.iNode.filename : null;
              NormalizeFilenameAndExt(streamInfo, fileNameOrig, out extraPathInfo, out fileExt);
            }
            url = IKCMS_RouteUrlManager.GetUrlProxyVFS(model.rNode, null, stream, null, null, false, null, false, extraPathInfo);
            if (!string.IsNullOrEmpty(fileExt))
            {
              url = Utility.UriAddQuery(url, "ext", fileExt);
            }
          }
          if (!string.IsNullOrEmpty(url))
          {
            if (fullUrl.GetValueOrDefault(false))
            {
              url = Utility.ResolveUrlFull(url);
            }
            else
            {
              if (url.StartsWith("~/"))
                url = Utility.ResolveUrl(url);
            }
          }
        }
      }
      catch { }
      return url;
    }


    public static string GetStreamFNameOrig(this IKCMS_ModelCMS_GenericBrickInterface model, string stream)
    {
      string fname = null;
      try
      {
        if (model != null)
        {
          MultiStreamInfo4Settings streamInfo = null;
          if (model != null)
            streamInfo = model.StreamInfos(stream);
          //
          if (streamInfo != null)
          {
            string extraPathInfo = null;
            string fileExt = null;
            if (streamInfo != null)
            {
              string fileNameOrig = (model.vfsNode != null && model.vfsNode.iNode != null) ? model.vfsNode.iNode.filename : null;
              NormalizeFilenameAndExt(streamInfo, fileNameOrig, out extraPathInfo, out fileExt);
              fname = extraPathInfo;
            }
          }
        }
      }
      catch { }
      return fname;
    }


    public static bool NormalizeFilenameAndExt(MultiStreamInfo4Settings streamInfo, string originalFilename, out string fileName, out string fileExt)
    {
      fileName = originalFilename;
      fileExt = null;
      try
      {
        fileName = (streamInfo != null && IKGD_ExternalVFS_Support.IsValidFileName(streamInfo.OrigNoPath)) ? streamInfo.OrigNoPath : originalFilename;
        fileExt = (streamInfo != null && !string.IsNullOrEmpty(streamInfo.ExtWithPoint)) ? streamInfo.ExtWithPoint : Ikon.Mime.MimeExtensionHelper.FindExtensionWithPoint(streamInfo.Mime ?? string.Empty);
        if (!string.IsNullOrEmpty(fileExt) && !fileName.EndsWith(fileExt, StringComparison.OrdinalIgnoreCase))
        {
          fileName = Path.GetFileNameWithoutExtension(fileName) + fileExt;
        }
        return true;
      }
      catch { }
      return false;
    }


    private static bool IsImage(this IKCMS_ModelCMS_GenericBrickInterface model, string stream)
    {
      try
      {
        MultiStreamInfo4Settings streamInfo = model.StreamInfos(stream);
        if (streamInfo != null && (streamInfo.Mime ?? string.Empty).StartsWith("image/", StringComparison.OrdinalIgnoreCase))
          return true;
      }
      catch { }
      return false;
    }


    private static bool IsFlash(this IKCMS_ModelCMS_GenericBrickInterface model, string stream)
    {
      try
      {
        MultiStreamInfo4Settings streamInfo = model.StreamInfos(stream);
        if (streamInfo != null && (streamInfo.Mime ?? string.Empty).IndexOf("flash", StringComparison.OrdinalIgnoreCase) >= 0)
          return true;
      }
      catch { }
      return false;
    }


    private static string StreamType(this IKCMS_ModelCMS_GenericBrickInterface model, string stream)
    {
      string mime = null;
      try
      {
        MultiStreamInfo4Settings streamInfo = model.StreamInfos(stream);
        if (streamInfo != null)
          mime = streamInfo.Mime;
      }
      catch { }
      return mime ?? string.Empty;
    }


    //
    // helper per link? NO solo in spark con partial standard
    //
    //


  }
}
