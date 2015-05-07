/*
 * 
 * Intranet Ikon
 * 
 * Copyright (C) 2010 Ikon Srl
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
using Autofac;
using Autofac.Core;
using Autofac.Builder;
using Autofac.Features;

using Ikon;
using Ikon.IKCMS;


namespace Ikon.IKCMS
{
  using Ikon.Config;
  using Ikon.GD;
  using Ikon.IKGD.Library.Resources;



  public static class IKCMS_ModelCMS_Extensions
  {


    public static IEnumerable<IKCMS_ModelCMS<T>> ModelsWithResourceOfType<T>(this IKCMS_ModelCMS_Interface model, Func<IKCMS_ModelCMS<T>, bool> predicate) where T : class, IKCMS_HasSerializationCMS_Interface
    {
      if (model == null || model.Models == null)
        return Enumerable.Empty<IKCMS_ModelCMS<T>>();
      return model.Models.OfType<IKCMS_ModelCMS<T>>().Where(m => m != null && m.VFS_ResourceObject != null && m.VFS_ResourceObject is T).Where(predicate);
    }


    public static IEnumerable<IKCMS_ModelCMS<T>> ModelsWithResourceOfType<T>(this IKCMS_ModelCMS_Interface model) where T : class, IKCMS_HasSerializationCMS_Interface
    {
      if (model == null || model.Models == null)
        return Enumerable.Empty<IKCMS_ModelCMS<T>>();
      return model.Models.OfType<IKCMS_ModelCMS<T>>().Where(m => m != null && m.VFS_ResourceObject != null && m.VFS_ResourceObject is T);
    }


    public static IEnumerable<T> ResourcesOfType<T>(this IKCMS_ModelCMS_Interface model) where T : class, IKCMS_HasSerializationCMS_Interface
    {
      if (model == null || model.Models == null)
        return Enumerable.Empty<T>();
      return model.Models.Where(m => m != null && m.VFS_ResourceObject != null && m.VFS_ResourceObject is T).Select(m => (T)m.VFS_ResourceObject);
    }


    public static IEnumerable<T> ResourcesOfType<T>(this IKCMS_ModelCMS_Interface model, Func<IKCMS_ModelCMS<T>, bool> predicate) where T : class, IKCMS_HasSerializationCMS_Interface
    {
      if (model == null || model.Models == null)
        return Enumerable.Empty<T>();
      return model.Models.OfType<IKCMS_ModelCMS<T>>().Where(m => m != null && m.VFS_ResourceObject != null && m.VFS_ResourceObject is T).Where(predicate).Select(m => (T)m.VFS_ResourceObject);
    }


    public static IEnumerable<T> NewIfEmpty<T>(this IEnumerable<T> sequence) where T : class, IKCMS_HasSerializationCMS_Interface, new()
    {
      return sequence.DefaultIfEmpty(new T());
    }


    public static T FirstOrNew<T>(this IEnumerable<T> sequence) where T : class, IKCMS_HasSerializationCMS_Interface, new()
    {
      return sequence.FirstOrDefault() ?? new T();
    }


    public static T FirstOrNew<T>(this IEnumerable<T> sequence, Func<T, bool> predicate) where T : class, IKCMS_HasSerializationCMS_Interface, new()
    {
      return sequence.FirstOrDefault(predicate) ?? new T();
    }


  }

}
