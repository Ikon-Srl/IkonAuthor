using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using System.Xml.Linq;
using Autofac;
using LinqKit;

using Ikon;
using Ikon.Support;
using Ikon.GD;
using Ikon.IKCMS;
using Ikon.IKCMS.Library.Resources;
using Ikon.IKGD.Library.Resources;


namespace Ikon.IKCMS
{

  public interface ShowReelController_Interface
  {
    ActionResult ShowReelXmlGenerator(string mode, string rNodeList, string hash);
    ActionResult ShowReelXmlGeneratorAutoV1(string xmlPath, string rNodeList, string lang, string hash);
    //string ShowReelXmlGeneratorWorker(string mode, string rNodeList);
  }


  [Microsoft.Web.Mvc.ControllerSessionState(Microsoft.Web.Mvc.ControllerSessionState.ReadOnly)]
  public abstract class ShowReelControllerBase : VFS_Access_Controller, ShowReelController_Interface
  {

    public virtual ActionResult ShowReelXmlGeneratorAutoV1(string xmlPath, string rNodeList, string lang, string hash)
    {
      string xmlStr = null;
      if (!string.IsNullOrEmpty(xmlPath) && System.IO.File.Exists(Utility.vPathMap(xmlPath)) && !string.IsNullOrEmpty(hash))
      {
        //
        // uso anche host nella cache key perche' il contenuto dell'xml contiene anche url assolute
        // flash e' schizzignoso e non lavora con stream da domini esterni ancge se ci sono i files .xml di sblocco degli accessi
        string cacheKey = FS_OperationsHelpers.ContextHash("ShowReelXmlGeneratorAutoV1", xmlPath, Request.Url.Host, rNodeList, hash);
        xmlStr = FS_OperationsHelpers.CachedEntityWrapper<string>(
          cacheKey, () => { return ShowReelXmlGeneratorAutoV1Worker(xmlPath, rNodeList, lang); },
          3600, FS_OperationsHelpers.Const_CacheDependencyIKGD_vNode_vData_iNode);
      }
      // forzatura del caching per il browser
      Ikon.Handlers.HttpHelper.CacheResponse(86400);
      if (xmlStr != null)
        return Content(xmlStr, "text/xml");
      return null;
    }


    //
    // ridefinibile nelle classi derivate
    //
    [NonAction]
    public virtual string ShowReelXmlGeneratorAutoV1Worker(string xmlPath, string rNodeList, string language)
    {
      XElement xData = null;
      try
      {
        xData = Utility.FileReadXmlVirtual(xmlPath);
        XElement xPivot = xData.Element("elementi");
        XElement xConfigCMS = xData.Element("ConfigCMS") ?? new XElement("ConfigCMS");
        xConfigCMS.Remove();  //per pulire l'xml di riferimento
        //
        // TODO:
        // lettura dei dati per streams prima dai dati in cache di IKCMS_ModelScannerParentBase e in caso mancassero
        // creando un model (o direttamente fsNode deserializzato) cached (o meglio vautare se crearlo cached o diretto)
        // completare il modulo di wrapping in spark con il supporto per rendering con solo image senza flash per iPad/stampa
        // sincronizzare il codice anche per TFVG come sito di riferimento
        //
        // scan dei nodi mantenendo l'ordine specificato in rNodeList
        var nodes = Utility.ExplodeT<int>(rNodeList, ",|;");
        var fsNodes = fsOp.Get_NodesInfoFiltered(vn => nodes.Contains(vn.rnode), null, null, FS_Operations.FilterVFS.ACL | FS_Operations.FilterVFS.Language).ToList().Distinct((n1, n2) => n1.rNode == n2.rNode).OrderBy(n => nodes.IndexOfSortable(n.vNode.rnode)).ToList();
        if (fsNodes.Any())
          xPivot.Elements().Remove();
        foreach (var fsNode in fsNodes)
        {
          try
          {
            XElement xItem = new XElement("item");
            IKCMS_HasSerializationCMS_Interface vfsObj = IKCMS_RegisteredTypes.Deserialize_IKCMS_ResourceVFS(fsNode);

            if (vfsObj is IKCMS_HasGenericBrick_Interface)
            {
              var wdg = vfsObj as IKCMS_HasGenericBrick_Interface;
              //
              xItem.SetAttributeValue("fit_mode", wdg.ResourceSettingsNoLanguageKVT(xConfigCMS.ElementValue("field_fit_mode", "FitMode")).ValueString ?? xConfigCMS.ElementValue("fit_mode"));
              if (!string.IsNullOrEmpty(wdg.ResourceSettingsNoLanguageKVT(xConfigCMS.ElementValue("field_attesa", "Delay")).ValueString))
              {
                try { xItem.SetAttributeValue("attesa", (int)Math.Round(Utility.TryParse<int>(wdg.ResourceSettingsNoLanguageKVT(xConfigCMS.ElementValue("field_attesa", "Delay")).ValueString, 0) / 1000.0)); }
                catch { }
              }
              xItem.SetAttributeValue("link_target", wdg.ResourceSettingsBase.LinkTarget.DefaultIfEmpty(string.Empty));
              if (wdg.ResourceSettingsBase.Link_sNode != null)
              {
                try { xItem.SetAttributeValue("link", IKCMS_RouteUrlManager.GetMvcUrlGeneral(null, wdg.ResourceSettingsBase.Link_sNode, null, true, true)); }
                catch { }
              }
              else if (!string.IsNullOrEmpty(wdg.ResourceSettingsBase.LinkUrl))
                xItem.SetAttributeValue("link", wdg.ResourceSettingsBase.LinkUrl);
              //
              string field_title = xConfigCMS.ElementValue("field_titolo", "Title");
              string field_text = xConfigCMS.ElementValue("field_testo", "Text");
              string title = wdg.ResourceSettingsKVT.KeyFilterTry(language, field_title).ValueString ?? wdg.ResourceSettingsKVT.KeyFilterTry(null, field_title).ValueString ?? string.Empty;
              string text = wdg.ResourceSettingsKVT.KeyFilterTry(language, field_text).ValueString ?? wdg.ResourceSettingsKVT.KeyFilterTry(null, field_text).ValueString ?? string.Empty;
              xItem.Add(new XElement("titolo", new XCData(title.Trim(' ', '\n', '\r', '\t'))));
              xItem.Add(new XElement("testo", new XCData(text.Trim(' ', '\n', '\r', '\t'))));
              //

              string stream_spot = Utility.Explode(xConfigCMS.ElementValue("stream_spot"), ",", " ", true).FirstOrDefault(s => wdg.ResourceSettingsBase.HasStream(s));
              string stream_full = Utility.Explode(xConfigCMS.ElementValue("stream_spot_full"), ",", " ", true).FirstOrDefault(s => wdg.ResourceSettingsBase.HasStream(s));
              //
              if (stream_spot != null)
                xItem.SetAttributeValue("spot", IKCMS_RouteUrlManager.GetUrlProxyVFS(fsNode.vData.rnode, null, stream_spot, null, null, true, null));
              if (stream_full != null)
                xItem.SetAttributeValue("spot_full", IKCMS_RouteUrlManager.GetUrlProxyVFS(fsNode.vData.rnode, null, stream_full, null, null, true, null));
              //
            }
            else
              continue;
            xPivot.Add(xItem);
          }
          catch { }
        }
      }
      catch { }
      return xData != null ? xData.ToString() : null;
    }



    //
    // supporto per la generazione dell'xml per gli showreel
    //
    public virtual ActionResult ShowReelXmlGenerator(string mode, string rNodeList, string hash)
    {
      string xmlStr = null;
      if (!string.IsNullOrEmpty(mode) && !string.IsNullOrEmpty(rNodeList))
      {
        //
        // uso anche host nella cache key perche' il contenuto dell'xml contiene anche url assolute
        // flash e' schizzignoso e non lavora con stream da domini esterni ancge se ci sono i files .xml di sblocco degli accessi
        string cacheKey = FS_OperationsHelpers.ContextHash("ShowReelXmlGenerator", mode, Request.Url.Host, rNodeList, hash);
        xmlStr = FS_OperationsHelpers.CachedEntityWrapper<string>(
          cacheKey, () => { return ShowReelXmlGeneratorWorker(mode, rNodeList); },
          3600, FS_OperationsHelpers.Const_CacheDependencyIKGD_vNode_vData_iNode);
      }
      // forzatura del caching per il browser
      Ikon.Handlers.HttpHelper.CacheResponse(86400);
      if (xmlStr != null)
        return Content(xmlStr, "text/xml");
      return null;
    }


    public virtual ActionResult ShowReelXmlGeneratorNoCache(string mode, string rNodeList)
    {
      string xmlStr = null;
      if (!string.IsNullOrEmpty(mode) && !string.IsNullOrEmpty(rNodeList))
      {
        xmlStr = ShowReelXmlGeneratorWorker(mode, rNodeList);
      }
      if (xmlStr != null)
        return Content(xmlStr, "text/xml");
      return null;
    }


    //
    // da ridefinire nelle classi derivate
    //
    [NonAction]
    public virtual string ShowReelXmlGeneratorWorker(string mode, string rNodeList)
    {
      throw new NotImplementedException();

      /*
      try
      {
        string fileName = "~/Content/Flash/data_it.xml";
        switch (mode)
        {
          case "header":
            fileName = "~/Content/Flash/DataHeaderBase.xml";
            break;
        }
        XElement xData = Utility.FileReadXmlVirtual(fileName);
        //
        XElement xPivot = xData.Element("elementi");
        // scan dei nodi mantenendo l'ordine specificato in rNodeList
        var nodes = Utility.ExplodeT<int>(rNodeList, ",");
        var fsNodes = fsOp.Get_NodesInfoACL(nodes, true, false).ToList().OrderBy(n => nodes.IndexOf(n.vNode.snode)).ToList();
        foreach (var fsNode in fsNodes)
        {
          try
          {
            XElement xItem = new XElement("item");
            IKCMS_HasSerializationCMS_Interface vfsObj = IKCMS_RegisteredTypes.Deserialize_IKCMS_ResourceVFS(fsNode);
            if (vfsObj is IKCMS_ResourceType_ShowReelElementV1)
            {
              var wdg = vfsObj as IKCMS_ResourceType_ShowReelElementV1;
              KeyValueObjectTree Values = wdg.ResourceSettings.Values;
              //
              xItem.SetAttributeValue("fit_mode", Values["FitMode"].ValueString);
              if (!string.IsNullOrEmpty(Values["Delay"].ValueString))
              {
                try { xItem.SetAttributeValue("attesa", (int)Math.Round(Utility.TryParse<int>(Values["Delay"].ValueString, 0) / 1000.0)); }
                catch { }
              }
              xItem.SetAttributeValue("link_target", wdg.ResourceSettings.LinkTarget.DefaultIfEmpty(string.Empty));
              if (wdg.ResourceSettings.Link_sNode != null)
              {
                try { xItem.SetAttributeValue("link", IKCMS_RouteUrlManager.GetMvcUrlGeneral(null, wdg.ResourceSettings.Link_sNode, null, true, true)); }
                catch { }
              }
              else if (!string.IsNullOrEmpty(wdg.ResourceSettings.LinkUrl))
                xItem.SetAttributeValue("link", wdg.ResourceSettings.LinkUrl);
              //
              if (fsNode.iNode.IKGD_MSTREAMs.Any(m => m.IKGD_STREAM.source == "main" && m.IKGD_STREAM.key == "orig_main"))
              {
                xItem.SetAttributeValue("spot", Utility.UriSetQuery(IKCMS_RouteUrlManager.GetUrlProxyVFS(fsNode.vData.rnode, null, "main|orig_main", null, null, true, null), "fake", string.Empty));
              }

              if (fsNode.iNode.IKGD_MSTREAMs.Any(m => m.IKGD_STREAM.source == "fullscreen" && m.IKGD_STREAM.key == "orig_fscr"))
              {
                xItem.SetAttributeValue("spot_full", Utility.UriSetQuery(IKCMS_RouteUrlManager.GetUrlProxyVFS(fsNode.vData.rnode, null, "fullscreen|orig_fscr", null, null, true, null), "fake", string.Empty));
              }
              //
              string title = Values["Title"].ValueStringNN.Trim(' ', '\n', '\r', '\t');
              xItem.Add(new XElement("titolo", new XCData(title)));
              //xItem.Add(new XElement("testo", new XCData(Values["Text"].ValueStringNN.Trim(' ', '\n', '\r', '\t'))));
              //
              try
              {
                if (xData.Element("setup").ElementValueNN("titolo").Trim(' ', '\n', '\r').Length == 0)
                {
                  xData.Element("setup").SetElementValue("titolo", title);
                }
              }
              catch { }
              //
            }
            else
              continue;
            xPivot.Add(xItem);
          }
          catch { }
        }
        //
        return xData.ToString();
      }
      catch { return null; }
      */
    }


  }


}
