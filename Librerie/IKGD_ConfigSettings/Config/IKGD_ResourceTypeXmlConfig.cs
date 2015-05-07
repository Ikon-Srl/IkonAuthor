/*
 * 
 * Intranet Ikon
 * 
 * Copyright (C) 2010 Ikon Srl
 * Tutti i Diritti Riservati. All Rights Reserved
 * 
 */


using System;
using System.Collections;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Linq;
using System.Xml.Linq;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Diagnostics;
using System.Reflection;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Formatters.Binary;
using System.Web;
using LinqKit;

using Ikon;
using Ikon.GD;
using Ikon.Config;


namespace Ikon.GD.Config
{


  public class IKGD_ResourceTypeXmlConfig
  {
    public XElement xConfig { get; protected set; }
    public XElement xSection { get; protected set; }
    public List<XElement> xSections { get; protected set; }
    //
    protected List<XElement> _xModuleElements = new List<XElement>();
    public List<XElement> xModuleElements { get { return _xModuleElements; } }
    //


    public IKGD_ResourceTypeXmlConfig(params Type[] types)
    {
      Setup(types.Select(t => t.Name).ToArray());
    }


    public IKGD_ResourceTypeXmlConfig(params string[] names)
    {
      Setup(names);
    }


    public void Setup(params string[] names)
    {
      //
      xConfig = IKGD_Config.xConfigAuthor;  // cached value with invalidation on file change or object modified in client code
      //
      xSection = null;
      foreach (string name in names)
      {
        xSection = xConfig.Element(name);
        if (xSection != null)
          break;
      }
      xSections = new List<XElement> { xSection, xConfig.Element("Global") }.Where(x => x != null).ToList();
      if (!xSections.Any())
        xSections = new List<XElement> { new XElement("Global") };
      xSection = xSections.FirstOrDefault();
    }


    public XElement GetSubSection(string elementName, string tagValue, bool firstIfNull, bool notNull)
    {
      XElement xSubSection = null;
      xSubSection = xSubSection ?? xSections.Elements(elementName).Where(x => x.AttributeValue("tag") == tagValue).FirstOrDefault();
      if (firstIfNull)
        xSubSection = xSubSection ?? xSections.Elements(elementName).FirstOrDefault();
      if (notNull)
        xSubSection = xSubSection ?? new XElement(elementName);
      return xSubSection;
    }


    public List<XElement> SetupModule(string moduleCategory)
    {
      _xModuleElements = new List<XElement>();
      try
      {
        _xModuleElements = xSection.Elements().Where(x => x.Name.LocalName != "Module").ToList();
        XElement xModule = xSection.Elements("Module").FirstOrDefault(m => m.AttributeValue("Category") == moduleCategory);
        if (xModule == null && string.IsNullOrEmpty(moduleCategory))
          xModule = xSection.Elements("Module").FirstOrDefault();
        if (xModule != null)
        {
          var names = xModule.Elements().Select(x => x.Name.LocalName).Distinct().ToList();
          _xModuleElements.RemoveAll(x => names.Contains(x.Name.LocalName));
          _xModuleElements.AddRange(xModule.Elements());
        }
      }
      catch { }
      return _xModuleElements;
    }


    //
    // versione di SetupModule con supporto per files .xml di configurazione di default/base embedded negli assembly dell'author
    //
    protected Func<XElement, string> xModuleMergeElementHashFunc = (x) => { return string.Format("{0}|{1}|{2}|{3}", x.Name.LocalName, x.AttributeValue("Name"), x.AttributeValue("Area"), x.AttributeValue("MergeOp")); };
    public List<XElement> SetupModuleWithEmbed(string moduleCategory, Assembly EditorTypeAssembly, Type ResourceType)
    {
      _xModuleElements = new List<XElement>();
      try
      {
        List<XElement> xElementsMerged = GetxElementsMerged(EditorTypeAssembly, ResourceType);
        //
        _xModuleElements = xElementsMerged.Where(x => x.Name.LocalName != "Module").ToList();
        //
        XElement xModule = xElementsMerged.FirstOrDefault(m => m.Name.LocalName == "Module" && string.Equals(m.AttributeValue("Category"), moduleCategory, StringComparison.OrdinalIgnoreCase));
        //
        if (xModule == null)
          xModule = xElementsMerged.FirstOrDefault(m => m.Name.LocalName == "Module");
        //if (xModule == null && string.IsNullOrEmpty(moduleCategory))
        //  xModule = xElementsMerged.FirstOrDefault(m => m.Name.LocalName == "Module");
        //
        if (xModule != null)
        {
          _xModuleElements.AddRange(xModule.Elements());
        }
        _xModuleElements = _xModuleElements.ReverseT().Distinct((e1, e2) => string.Equals(xElementsMergedElementHashFunc(e1), xElementsMergedElementHashFunc(e2), StringComparison.OrdinalIgnoreCase)).Reverse().Where(x => Utility.TryParse<bool>(x.AttributeValue("Enabled"), true)).ToList();
      }
      catch { }
      return _xModuleElements;
    }


    public List<KeyValuePair<string, string>> GetAvailableSubTypes(Assembly EditorTypeAssembly, Type ResourceType, bool selectablesOnly)
    {
      List<XElement> xElementsMerged = GetxElementsMerged(EditorTypeAssembly, ResourceType);
      List<KeyValuePair<string, string>> subTypesAll = xElementsMerged.Where(x => x.Name.LocalName == "Module").Where(x => !selectablesOnly || Utility.TryParse<bool>(x.AttributeValue("Selectable"), true)).Select(x => new KeyValuePair<string, string>(x.AttributeValue("Category"), x.AttributeValue("Description", x.AttributeValue("Category")))).ToList();
      return subTypesAll;
    }


    protected XElement GetEmbeddedConfig(Assembly EditorTypeAssembly, Type ResourceType)
    {
      XElement xEmbeddedConfig = null;
      try
      {
        string AssemblyName = EditorTypeAssembly.FullName.Split(',', ' ').FirstOrDefault();
        using (StreamReader sr = new StreamReader(EditorTypeAssembly.GetManifestResourceStream(AssemblyName + ".Editors.EditorConfigXml." + ResourceType.Name + ".xml")))
        {
          xEmbeddedConfig = XElement.Parse(sr.ReadToEnd());
        }
      }
      catch { }
      return xEmbeddedConfig;
    }


    protected Func<XElement, string> xElementsMergedElementHashFunc = (x) => { return string.Format("{0}|{1}|{2}|{3}|{4}", x.Name.LocalName, x.AttributeValue("Name"), x.AttributeValue("Category"), x.AttributeValue("Area"), x.AttributeValue("MergeOp")); };
    protected List<XElement> GetxElementsMerged(Assembly EditorTypeAssembly, Type ResourceType)
    {
      List<string> precedence = new List<string> { "Flags", "EditorTabs", "Field", "Module" };
      XElement xEmbeddedConfig = GetEmbeddedConfig(EditorTypeAssembly, ResourceType);
      // attenzione all'utilizzo dei reverse che servono per dare precedenza prima all'xml sull'embed e per chiascuno di questi dare precedenza all'ultima occorrenza di items degeneri
      var elements = xSection.Elements().Reverse().Concat((xEmbeddedConfig == null ? Enumerable.Empty<XElement>() : xEmbeddedConfig.Elements()).Reverse()).Where(x => x != null);
      var elementsFiltered = elements.Distinct((e1, e2) => string.Equals(xElementsMergedElementHashFunc(e1), xElementsMergedElementHashFunc(e2), StringComparison.OrdinalIgnoreCase)).Where(x => Utility.TryParse<bool>(x.AttributeValue("Enabled"), true)).Reverse().ToList();
      var elementsFilteredOrdered = elementsFiltered.OrderBy(x => precedence.IndexOfSortable(x.Name.LocalName)).ThenBy(m => Utility.TryParse<double>(m.AttributeValue("Position"))).ThenBy(m => elementsFiltered.IndexOf(m)).ToList();
      return elementsFilteredOrdered;
    }

  }



}