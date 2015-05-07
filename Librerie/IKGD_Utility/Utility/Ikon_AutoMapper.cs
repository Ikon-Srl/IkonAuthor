/*
 * 
 * Intranet Ikon
 * 
 * Copyright (C) 2008 Ikon Srl
 * Tutti i Diritti Riservati. All Rights Reserved
 * 
 */


using System;
using System.Data;
using System.Configuration;
using System.Linq;
using System.Xml.Linq;
using System.Collections;
using System.Collections.Generic;
using System.Linq.Dynamic;
using System.Linq.Expressions;
using System.Data.Linq;
using System.IO;
using System.Text;
using System.Reflection;
using System.Diagnostics;
using System.ComponentModel;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;  // assembly System.ServiceModel.Web

using Ikon;
using Ikon.Log;


namespace Ikon
{

  public static class AutoMapperWrapper
  {
    static AutoMapperWrapper()
    {
    }


    //
    // support function for AutoMapper
    // e' meglio non utilizzare AutoMapper.Mapper.DynamicMap<T> perche' poi prende comunque il primo mapping accettabile
    //

    public static void AutoRegister(Type sourceType, Type destinationType)
    {
      try
      {
        var typeMap = AutoMapper.Mapper.FindTypeMapFor(sourceType, destinationType);
        if (typeMap == null || typeMap.SourceType != sourceType || typeMap.DestinationType != destinationType)
        {
          AutoMapper.IMappingExpression map = AutoMapper.Mapper.CreateMap(sourceType, destinationType);
        }
      }
      catch { }
    }

    public static void AutoRegister<Tsource, Tdestination>() { AutoRegister<Tsource, Tdestination>(null); }
    public static void AutoRegister<Tsource, Tdestination>(Action<AutoMapper.IMappingExpression<Tsource, Tdestination>> configure)
    {
      try
      {
        var typeMap = AutoMapper.Mapper.FindTypeMapFor<Tsource, Tdestination>();
        if (typeMap == null || typeMap.SourceType != typeof(Tsource) || typeMap.DestinationType != typeof(Tdestination))
        {
          if (configure != null)
            configure(AutoMapper.Mapper.CreateMap<Tsource, Tdestination>());
          else
            AutoMapper.Mapper.CreateMap<Tsource, Tdestination>();
        }
      }
      catch { }
    }


    public static T Map<T>(object source) where T : class
    {
      if (source == null)
        return default(T);
      try
      {
        Type sourceType = source.GetType();
        Type destType = typeof(T);
        AutoRegister(sourceType, destType);
        return AutoMapper.Mapper.Map(source, sourceType, destType) as T;
      }
      catch { }
      return default(T);
    }

  }





}
