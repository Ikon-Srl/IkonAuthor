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
using LinqKit;
using Autofac;

using Ikon;
using Ikon.GD;
using Ikon.IKGD.Library.Resources;


namespace Ikon.IKCMS
{

  public static class TeasersHelperExtension
  {
    //
    public static FS_Operations fsOp { get { return IKCMS_ManagerIoC.requestContainer.Resolve<FS_Operations>(); } }
    //


    public static string ViewDataChildCodeName { get { return "ChildCode"; } }
    public static string ViewDataPlaceholderName { get { return "PlaceHolderName"; } }
    public static string ViewDataContainerTemplateName { get { return "ContainerTemplateName"; } }
    public static string ViewDataContainerCssClass { get { return "ContainerCssClass"; } }  // viene usato per passare ai teaser items la classe applicata al teaser container nel placeholder
    public static string ViewDataContainerCssClassForced { get { return "ContainerCssClassForced"; } }  // viene usato per consentire di specificare la classe da applicare al teaser container
    public static string ViewDataContainerCssClassDefault { get { return "ContainerCssClassDefault"; } }  // viene usato per consentire di specificare la classe da applicare al teaser container
    public static string ViewDataContainerVisibleItemsCount { get { return "ContainerVisibleItemsCount"; } }
    public static string ViewDataContentCssClass { get { return "ContentCssClass"; } }  // viene usato per passare ai teaser items la classe applicata al teaser container nel placeholder
    public static string ViewDataStreamSelector { get { return "StreamSelector"; } }  // per la selezione dello stream delle immagini
    public static string ViewDataItemIndex { get { return "ItemCounter"; } }  // per la selezione dello stream delle immagini
    public static string ViewDataWidthAggregatorContextName { get { return "WidthAggregatorContext"; } }


    /*
    public static ViewDataDictionary TeaserViewerViewDataManager(this ViewDataDictionary containerViewData, string childCode, params object[] KeyAndValuesPairs)
    {
      ViewDataDictionary result = new ViewDataDictionary();
      try
      {
        result[ViewDataChildCodeName] = childCode;
        try { result[ViewDataPlaceholderName] = containerViewData[ViewDataPlaceholderName]; }
        catch { }
        if (KeyAndValuesPairs != null && KeyAndValuesPairs.Any())
        {
          for (int i = 0; i < KeyAndValuesPairs.Length - 1; i += 2)
          {
            if (KeyAndValuesPairs[i] != null)
              result[KeyAndValuesPairs[i].ToString()] = KeyAndValuesPairs[i + 1];
          }
        }
      }
      catch { }
      return result;
    }


    public static string TeaserViewerViewDataGetCode(this ViewDataDictionary viewData) { return TeaserViewerViewDataGetCode(viewData, string.Empty); }
    public static string TeaserViewerViewDataGetCode(this ViewDataDictionary viewData, string defaultValue)
    {
      string code = null;
      if (viewData != null)
      {
        try { code = viewData[ViewDataChildCodeName] as string; }
        catch { }
      }
      return code ?? defaultValue;
    }
    */


    public enum TeaserSizeAccumulatorDirection { Horizontal, Vertical };
    public enum TeaserSizeAccumulatorSequence { PreCheck, PostCheck };
    public static bool TeaserSizeAccumulatorPreVertical(IKCMS_ModelCMS_HasTemplateInfo_Interface model, ref int AccumulatedSize, int? maxSize) { return TeaserSizeAccumulator(model, ref AccumulatedSize, maxSize, TeaserSizeAccumulatorDirection.Vertical, TeaserSizeAccumulatorSequence.PreCheck); }
    public static bool TeaserSizeAccumulatorPreHorizontal(IKCMS_ModelCMS_HasTemplateInfo_Interface model, ref int AccumulatedSize, int? maxSize) { return TeaserSizeAccumulator(model, ref AccumulatedSize, maxSize, TeaserSizeAccumulatorDirection.Horizontal, TeaserSizeAccumulatorSequence.PreCheck); }
    public static bool TeaserSizeAccumulatorPostVertical(IKCMS_ModelCMS_HasTemplateInfo_Interface model, ref int AccumulatedSize, int? maxSize) { return TeaserSizeAccumulator(model, ref AccumulatedSize, maxSize, TeaserSizeAccumulatorDirection.Vertical, TeaserSizeAccumulatorSequence.PostCheck); }
    public static bool TeaserSizeAccumulatorPostHorizontal(IKCMS_ModelCMS_HasTemplateInfo_Interface model, ref int AccumulatedSize, int? maxSize) { return TeaserSizeAccumulator(model, ref AccumulatedSize, maxSize, TeaserSizeAccumulatorDirection.Horizontal, TeaserSizeAccumulatorSequence.PostCheck); }
    public static bool TeaserSizeAccumulator(IKCMS_ModelCMS_HasTemplateInfo_Interface model, ref int AccumulatedSize, int? maxSize, TeaserSizeAccumulatorDirection? direction, TeaserSizeAccumulatorSequence? sequence)
    {
      bool result = false;
      try
      {
        if (model == null || model.TemplateInfo == null)
          return false;
        if (sequence.GetValueOrDefault(TeaserSizeAccumulatorSequence.PreCheck) == TeaserSizeAccumulatorSequence.PreCheck)
        {
          if (AccumulatedSize >= maxSize.GetValueOrDefault(int.MaxValue))
          {
            AccumulatedSize = 0;
            result = true;
          }
        }
        AccumulatedSize += ((direction.GetValueOrDefault(TeaserSizeAccumulatorDirection.Horizontal) == TeaserSizeAccumulatorDirection.Horizontal) ? model.TemplateInfo.Width.GetValueOrDefault(0) : model.TemplateInfo.Height.GetValueOrDefault(0));
        if (sequence.GetValueOrDefault(TeaserSizeAccumulatorSequence.PreCheck) == TeaserSizeAccumulatorSequence.PostCheck)
        {
          if (AccumulatedSize >= maxSize.GetValueOrDefault(int.MaxValue))
          {
            AccumulatedSize = 0;
            result = true;
          }
        }
      }
      catch { }
      return result;
    }

  }

}
