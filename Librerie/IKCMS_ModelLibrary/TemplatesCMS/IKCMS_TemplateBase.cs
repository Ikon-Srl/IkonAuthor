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
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.UI;
using System.Web.Security;
using System.Xml.Linq;
using System.Data.Linq;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Linq.Expressions;
using System.Reflection;
using System.Data.Linq.Mapping;
using System.Diagnostics;
using System.Linq.Dynamic;
using System.Transactions;
using System.Web.Caching;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;  // assembly System.ServiceModel.Web
using LinqKit;

using Ikon;
using Ikon.GD;
using Ikon.GD.Config;
using Ikon.IKCMS;
using Ikon.Log;
using Ikon.IKGD.Library.Resources;



namespace Ikon.IKCMS
{



  public interface IKCMS_PageCMS_Template_Interface
  {
    string TemplateType { get; }
    string Name { get; }
    string Description { get; }
    string ViewPath { get; }
    bool Selectable { get; }
    Ikon.Utility.DictionaryMV<string, string> ViewPaths { get; }
    List<Type> ResourceTypes { get; }
    ILookup<Type, string> ResourceTypesCategory { get; set; }
    List<IKCMS_PageCMS_Placeholder_Interface> Placeholders { get; set; }
    int? Width { get; set; }
    int? Height { get; set; }
    string IconPath { get; }
  }


  public class IKCMS_PageCMS_Template : IKCMS_PageCMS_Template_Interface
  {
    public virtual string TemplateType { get; set; }
    public virtual string Name { get { return TemplateType; } }
    public virtual string Description { get; set; }
    public virtual string ViewPath { get { return ViewPaths.Values.FirstOrDefault(); } }
    public bool Selectable { get; set; }
    public virtual Ikon.Utility.DictionaryMV<string, string> ViewPaths { get; set; }
    public virtual List<Type> ResourceTypes { get; set; }
    public virtual ILookup<Type, string> ResourceTypesCategory { get; set; }
    public virtual List<IKCMS_PageCMS_Placeholder_Interface> Placeholders { get; set; }
    public virtual int? Width { get; set; }
    public virtual int? Height { get; set; }
    public virtual string IconPath { get; set; }
  }


  public interface IKCMS_PageCMS_Placeholder_Interface
  {
    string Code { get; }
    string Description { get; }
    string IconPath { get; set; }
    bool Selectable { get; }
  }


  public class IKCMS_PageCMS_Placeholder : IKCMS_PageCMS_Placeholder_Interface
  {
    public virtual string Code { get; set; }
    public virtual string Description { get; set; }
    public virtual string IconPath { get; set; }
    public bool Selectable { get; set; }
  }



  public static class IKCMS_TemplatesTypeHelper
  {
    private static List<IKCMS_PageCMS_Template_Interface> TemplatesRegistered { get; set; }
    private static List<IKCMS_PageCMS_Placeholder_Interface> PlaceholdersRegistered { get; set; }
    //
    private static Utility.DictionaryMV<Type, IKCMS_PageCMS_Template_Interface> TemplateForTypes { get; set; }
    private static Utility.DictionaryMV<Type, IKCMS_PageCMS_Template_Interface> TemplateForTypesGeneric { get; set; }
    private static ILookup<Type, IKCMS_PageCMS_Template_Interface> TemplatesMapping { get; set; }
    //
    private static string IdConfig;
    private static object _lock = new object();


    static IKCMS_TemplatesTypeHelper()
    {
      Setup();
    }


    public static void Setup()
    {
      lock (_lock)
      {
        try
        {
          IdConfig = IKGD_Config.IdConfig;
          PlaceholdersRegistered = new List<IKCMS_PageCMS_Placeholder_Interface>();
          TemplatesRegistered = new List<IKCMS_PageCMS_Template_Interface>();
          TemplateForTypes = new Utility.DictionaryMV<Type, IKCMS_PageCMS_Template_Interface>();
          TemplateForTypesGeneric = new Utility.DictionaryMV<Type, IKCMS_PageCMS_Template_Interface>();
          //
          Regex genericsRegEx = new Regex(@"<(,)*>*", RegexOptions.IgnoreCase | RegexOptions.Singleline);
          //
          IKGD_ResourceTypeXmlConfig xConfigPlaceholders = new IKGD_ResourceTypeXmlConfig(new Type[] { typeof(IKCMS_PageCMS_Placeholder_Interface) });
          if (xConfigPlaceholders != null)
          {
            foreach (XElement xT in xConfigPlaceholders.xSection.Elements("Placeholder").Where(x => x.ElementValueNN("Code").Trim().Length > 0).Distinct((x1, x2) => x1.ElementValueNN("Code").Trim() == x2.ElementValueNN("Code").Trim()))
            {
              PlaceholdersRegistered.Add(new IKCMS_PageCMS_Placeholder
              {
                Code = xT.ElementValue("Code").Trim(),
                Selectable = Utility.TryParse<bool>(xT.AttributeValue("Selectable"), true),
                IconPath = xT.ElementValueNN("IconPath").Trim().NullIfEmpty(),
                Description = xT.ElementValue("Description", xT.ElementValueNN("Code").Trim())
              });
            }
          }
          //
          IKGD_ResourceTypeXmlConfig xConfigTemplates = new IKGD_ResourceTypeXmlConfig(new Type[] { typeof(IKCMS_PageCMS_Template_Interface) });
          foreach (XElement xT in xConfigTemplates.xSection.Elements("Template").Where(x => x.ElementValueNN("Type").Trim().Length > 0).Distinct((x1, x2) => x1.ElementValueNN("Type").Trim() == x2.ElementValueNN("Type").Trim()))
          {
            IKCMS_PageCMS_Template tt = null;
            TemplatesRegistered.Add(tt = new IKCMS_PageCMS_Template
            {
              TemplateType = xT.ElementValue("Type").Trim(),
              ViewPaths = new Ikon.Utility.DictionaryMV<string, string>(xT.Elements("ViewPath").ToDictionary(x => x.AttributeValueNN("code").Trim(), x => x.Value.Trim())),
              Selectable = Utility.TryParse<bool>(xT.AttributeValue("Selectable"), true),
              ResourceTypes = new List<Type>(),
              Description = xT.ElementValue("Description", xT.ElementValueNN("Type").Trim()),
              Width = xT.Element("Size") != null ? (int?)Utility.TryParse<int?>(xT.Element("Size").AttributeValue("Width")) : null,
              Height = xT.Element("Size") != null ? (int?)Utility.TryParse<int?>(xT.Element("Size").AttributeValue("Height")) : null,
              IconPath = xT.ElementValueNN("IconPath").Trim().NullIfEmpty(),
              Placeholders = xT.ElementValue("Placeholders") == null ? PlaceholdersRegistered.ToList() : PlaceholdersRegistered.Where(r => Utility.Explode(xT.ElementValue("Placeholders"), ",", " ", true).Contains(r.Code)).ToList()
            });
            //
            List<TupleW<Type, string>> maps = new List<TupleW<Type, string>>();
            foreach (XElement x in xT.Elements("ResourceTypes"))
            {
              var categories = Utility.Explode(x.AttributeValue("Categories"), ",", " ", true);
              var typeNames = Utility.Explode(x.Value, ",", " ", true).Select(t =>
              {
                string tn = t;
                try { tn = genericsRegEx.Replace(t, m => { return "`" + (m.Groups[1].Captures.Count + 1).ToString(); }); }
                catch { }
                return tn;
              }).Distinct().ToList();
              var typesInherited = typeNames.Where(t => !t.StartsWith("^") && !t.StartsWith("~") && !t.StartsWith("#")).SelectMany(t => Utility.FindTypesDerivedFrom(false, t.Trim("^#!~".ToCharArray()))).Distinct().Where(t => t != null && !t.IsAbstract && !t.IsInterface).ToList();
              var typesExactMatch = typeNames.Where(t => t.StartsWith("#")).Select(t => Utility.FindTypeCachedExt(t.Trim("^#!~".ToCharArray()), false)).Distinct().Where(t => t != null && !t.IsAbstract && !t.IsInterface).ToList();
              var typesForbidden = typeNames.Where(t => t.StartsWith("^") || t.StartsWith("~")).SelectMany(t => Utility.FindTypesDerivedFrom(false, t.Trim("^#!~".ToCharArray()))).Distinct().Where(t => t != null && !t.IsAbstract && !t.IsInterface).ToList();
              var typesFiltered = typesInherited.Concat(typesExactMatch).Except(typesForbidden).Distinct().ToList();
              foreach (Type ty in typesFiltered)
              {
                maps.AddRange(categories.Select(c => new TupleW<Type, string>(ty, c)));
                if (!tt.ResourceTypes.Contains(ty))
                  tt.ResourceTypes.Add(ty);
                if (!TemplateForTypes.ContainsKey(ty))
                  TemplateForTypes[ty] = tt;
                if (ty.IsGenericType)
                {
                  Type tyg = ty.GetGenericTypeDefinition();
                  maps.AddRange(categories.Select(c => new TupleW<Type, string>(tyg, c)));
                  if (!TemplateForTypes.ContainsKey(tyg))
                    TemplateForTypesGeneric[tyg] = tt;
                }
              }
            }
            //
            tt.ResourceTypesCategory = maps.Distinct().ToLookup(m => m.Item1, m => m.Item2);
            //
          }
          //
          TemplatesMapping = TemplatesRegistered.SelectMany(tt => tt.ResourceTypes.Select(t => new { type = t, template = tt })).Concat(TemplatesRegistered.SelectMany(tt => tt.ResourceTypes.Where(t => t.IsGenericType).Select(t => new { type = t.GetGenericTypeDefinition(), template = tt }))).Distinct().ToLookup(m => m.type, m => m.template);
          //
        }
        catch { }
      }
    }


    public static IKCMS_PageCMS_Template_Interface GetTemplate(string vfsTemplateType) { return GetTemplateForType(null, vfsTemplateType, null, null); }
    public static IKCMS_PageCMS_Template_Interface GetTemplateForType(Type resourceType, string vfsTemplateType, string category, string placeholder)
    {
      lock (_lock)
      {
        //
        if (IdConfig != IKGD_Config.IdConfig)
          Setup();
        //
        IKCMS_PageCMS_Template_Interface template = null;
        try
        {
          if (!string.IsNullOrEmpty(vfsTemplateType))
            template = TemplatesRegistered.FirstOrDefault(t => t.TemplateType == vfsTemplateType);
          if (template == null)
          {
            List<IKCMS_PageCMS_Template_Interface> templates = new List<IKCMS_PageCMS_Template_Interface>();
            if (TemplatesMapping.Contains(resourceType))
              templates.AddRange(TemplatesMapping[resourceType]);
            if (resourceType.IsGenericType && TemplatesMapping.Contains(resourceType.MakeGenericType()))
              templates.AddRange(TemplatesMapping[resourceType.MakeGenericType()]);
            if (category != null)
            {
              templates = templates.Where(tt => !tt.ResourceTypesCategory.Any() || tt.ResourceTypesCategory[resourceType].Contains(category)).ToList();
              templates = templates.Where(tt => tt.ResourceTypesCategory[resourceType].Contains(category)).Concat(templates.Where(tt => !tt.ResourceTypesCategory.Any())).ToList();
            }
            if (placeholder != null)
            {
              templates.RemoveAll(r => r.Placeholders != null && r.Placeholders.Any() && !r.Placeholders.Any(p => p.Code == placeholder));
            }
            template = templates.FirstOrDefault();
          }
          // old stuff
          if (template == null)
            template = TemplateForTypes[resourceType] ?? TemplateForTypesGeneric[resourceType];
          if (template == null && resourceType.IsGenericType)
          {
            resourceType = resourceType.GetGenericTypeDefinition();
            template = TemplateForTypes[resourceType] ?? TemplateForTypesGeneric[resourceType];
          }
        }
        catch { }
        return template ?? TemplatesRegistered.FirstOrDefault();
      }
    }


    public static List<IKCMS_PageCMS_Template_Interface> TemplatesAvailableForResource(Type resourceType, string category, string placeholder) { return TemplatesAvailableForResource(resourceType, category, placeholder, false); }
    public static List<IKCMS_PageCMS_Template_Interface> TemplatesAvailableForResource(Type resourceType, string category, string placeholder, bool selectablesOnly)
    {
      lock (_lock)
      {
        //
        if (IdConfig != IKGD_Config.IdConfig)
          Setup();
        //
        List<IKCMS_PageCMS_Template_Interface> templates = null;
        try
        {
          templates = TemplatesRegistered.Where(tt => tt.Selectable || tt.Selectable == selectablesOnly).Where(tt => tt.ResourceTypes.Any(rt => rt == resourceType)).ToList();
          if (!templates.Any() && resourceType.IsGenericType && !resourceType.IsGenericTypeDefinition)
          {
            resourceType = resourceType.GetGenericTypeDefinition();
            templates = TemplatesRegistered.Where(tt => tt.ResourceTypes.Any(rt => rt == resourceType)).ToList();
          }
          if (category != null)
          {
            templates = templates.Where(tt => !tt.ResourceTypesCategory.Any() || tt.ResourceTypesCategory[resourceType].Contains(category)).ToList();
            templates = templates.Where(tt => tt.ResourceTypesCategory[resourceType].Contains(category)).Concat(templates.Where(tt => !tt.ResourceTypesCategory.Any())).ToList();
          }
          if (placeholder != null)
          {
            templates.RemoveAll(r => r.Placeholders != null && r.Placeholders.Any() && !r.Placeholders.Any(p => p.Code == placeholder));
          }
        }
        catch { }
        return templates;
      }
    }


    public static List<IKCMS_PageCMS_Placeholder_Interface> PlaceholdersAvailableForResource(Type resourceType, string category) { return PlaceholdersAvailableForResource(resourceType, category, false); }
    public static List<IKCMS_PageCMS_Placeholder_Interface> PlaceholdersAvailableForResource(Type resourceType, string category, bool selectablesOnly)
    {
      lock (_lock)
      {
        //
        if (IdConfig != IKGD_Config.IdConfig)
          Setup();
        //
        List<IKCMS_PageCMS_Placeholder_Interface> placeholders = null;


        //la lista dei placeholders validi e' quella che e' compatibile con almeno uno dei templates selezionabili [OR] (template e' salvato in vData placeholder in vNode)
        try
        {
          placeholders = TemplatesAvailableForResource(resourceType, category, null, selectablesOnly).SelectMany(r => r.Placeholders).Distinct().OrderBy(r => PlaceholdersRegistered.IndexOfSortable(r)).ToList();
        }
        catch { }

        /*
        //la lista dei placeholders validi e' quella che e' compatibile con tutti i templates selezionabili [AND] (template e' salvato in vData placeholder in vNode)
        placeholders = PlaceholdersRegistered.ToList();
        try
        {
          foreach (var tmpl in TemplatesAvailableForResource(resourceType, category, null, selectablesOnly))
          {
            placeholders = placeholders.Intersect(tmpl.Placeholders).ToList();
          }
        }
        catch { }
        */

        return placeholders ?? new List<IKCMS_PageCMS_Placeholder_Interface>();
      }
    }

  }



}  //namespace
