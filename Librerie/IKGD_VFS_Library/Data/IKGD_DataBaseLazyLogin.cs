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
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Xml.Linq;
using System.Data.Linq;
using System.Data.Linq.Mapping;
using System.Linq.Dynamic;
using System.ComponentModel;
using System.Reflection;
using System.Text;


/// <summary>
/// Summary description for IkonGD_dataBase
/// </summary>


namespace Ikon.GD
{
  using Ikon.IKCMS;


  //
  // Interfaces per definire il supporto della master table LazyLoginMapper
  // da assegnare alle tabelle dipendenti da LazyLoginMapper
  //

  public interface ILazyLoginMapperInitialize
  {
    void InitializeLL();
  }


  public interface ILazyLoginMapperFK
  {
    int IdLL { get; set; }  // foreign key to ILazyLoginMapper
    ILazyLoginMapper LazyLoginMapperFK { get; set; }  // foreign key
  }


  public interface ILazyLoginMapperOneToOne : ILazyLoginMapperFK
  {
  }


  public interface ILazyLoginMapperOneToMany : ILazyLoginMapperFK
  {
  }


  public interface ILazyLoginMapperHasRNODE
  {
    int? rNode { get; set; }
  }


  public interface ILazyLoginMapperHasRNODE_NN
  {
    int rNode { get; set; }
  }


  public interface ILazyLoginDataContext
  {
    Type LazyLoginMapperType { get; }
  }


  //
  // base interface for LazyLoginMapper
  //
  public interface ILazyLoginMapper
  {
    int Id { get; set; }
    Guid UserId { get; set; }
    bool flag_active { get; set; }
    DateTime Creat { get; set; }
  }


  public interface ILazyLoginMapperHasTypeDC
  {
    Type DataContextType { get; }
  }


  public class DataContextGeneric<DC> : ILazyLoginMapperHasTypeDC
    where DC : ILazyLoginDataContext
  {
    public Type DataContextType { get { return typeof(DC); } }
  }


  public partial class IKGD_DataContext : ILazyLoginDataContext
  {
    public Type LazyLoginMapperType { get { return typeof(LazyLoginMapper); } }
  }


  public partial class LazyLoginMapper : DataContextGeneric<IKGD_DataContext>, ILazyLoginMapper, ILazyLoginMapperInitialize
  {
    public void InitializeLL()
    {
      this.Creat = DateTime.Now;
      this.flag_active = true;
    }
  }


  public partial class LazyLogin_Log : DataContextGeneric<IKGD_DataContext>, ILazyLoginMapperOneToMany, ILazyLoginMapperInitialize
  {
    public ILazyLoginMapper LazyLoginMapperFK { get { return this.LazyLoginMapper; } set { this.LazyLoginMapper = value as LazyLoginMapper; } }

    public void InitializeLL()
    {
      this.Date = DateTime.Now;
    }
  }


  public partial class LazyLogin_Vote : DataContextGeneric<IKGD_DataContext>, ILazyLoginMapperOneToMany, ILazyLoginMapperInitialize
  {
    public ILazyLoginMapper LazyLoginMapperFK { get { return this.LazyLoginMapper; } set { this.LazyLoginMapper = value as LazyLoginMapper; } }

    public void InitializeLL()
    {
      this.Date = DateTime.Now;
      //this.Category = 0;
    }

    private KeyValueObjectTree _DataKVT = null;
    public void DataUpdate() { Text = DataKVT.Serialize(); }
    public KeyValueObjectTree DataKVT { get { return _DataKVT ?? (_DataKVT = KeyValueObjectTree.Deserialize(Text)); } }
    public KeyValueObjectTree DataLanguageKVT(params string[] keys) { return DataKVT.KeyFilterTry(IKGD_Language_Provider.Provider.LanguageNN, keys); }
    public KeyValueObjectTree DataNoLanguageKVT(params string[] keys) { return DataKVT.KeyFilterTry(null, keys); }

  }





  public static class LazyLoginDataContextExtensions
  {
    private static Ikon.Utility.DictionaryMV<Type, Type> CachedDataContextMappings = new Utility.DictionaryMV<Type, Type>();
    public static Type GetLazyLoginMapperType(this ILazyLoginDataContext DC) { return CachedDataContextMappings[DC.GetType()] ?? (CachedDataContextMappings[DC.GetType()] = (DC as DataContext).Mapping.GetTables().FirstOrDefault(t => typeof(ILazyLoginMapper).IsAssignableFrom(t.RowType.Type)).RowType.Type); }


    public static ILazyLoginMapper GetLazyLoginMapper(this ILazyLoginDataContext DC) { return GetLazyLoginMapper(DC, MembershipHelper.ProviderUserKeyGuid, null); }
    public static ILazyLoginMapper GetLazyLoginMapper(this ILazyLoginDataContext DC, bool? autoSubmit) { return GetLazyLoginMapper(DC, MembershipHelper.ProviderUserKeyGuid, autoSubmit); }
    public static ILazyLoginMapper GetLazyLoginMapper(this ILazyLoginDataContext DC, Guid guid, bool? autoSubmit)
    {
      ILazyLoginMapper llMapper = null;
      try
      {
        Type llmType = CachedDataContextMappings[DC.GetType()] ?? (CachedDataContextMappings[DC.GetType()] = (DC as DataContext).Mapping.GetTables().FirstOrDefault(t => typeof(ILazyLoginMapper).IsAssignableFrom(t.RowType.Type)).RowType.Type);
        //attenzione: CPU HOG che viene tradotto in -->
        // SELECT TOP (1) [t0].* FROM [LazyLoginMapper] AS [t0] WHERE (CONVERT(NVarChar(MAX),[t0].[UserId])) = '1F99ABCB-5F0B-4E13-AB38-BE50907A6CC9'
        // e genera un full index scan con conversione del dato per ogni record!!!
        //llMapper = (DC as DataContext).GetTable(llmType).Where("UserId.ToString()=@0", guid.ToString()).Cast<ILazyLoginMapper>().FirstOrDefault();
        // versione con accesso ottimizzato e corretto (un solo index seek)
        llMapper = (DC as DataContext).ExecuteQuery(llmType, "SELECT TOP (1) * FROM [LazyLoginMapper] WHERE ([UserId] = {0})", guid.ToString()).OfType<ILazyLoginMapper>().FirstOrDefault();
        //
        if (llMapper == null)
        {
          llMapper = Activator.CreateInstance(llmType) as ILazyLoginMapper;
          llMapper.UserId = guid;
          if (llMapper is ILazyLoginMapperInitialize)
            (llMapper as ILazyLoginMapperInitialize).InitializeLL();
          (DC as DataContext).GetTable(llmType).InsertOnSubmit(llMapper);
          if (autoSubmit != false)
          {
            (DC as DataContext).SubmitChanges();
          }
        }
      }
      catch { }
      return llMapper;
    }


    //
    // example:
    //
    // XYZ.DB.DataContext_XYZ DC = Ikon.IKCMS.IKCMS_ManagerIoC.requestContainer.Resolve<XYZ.DB.DataContext_XYZ>();
    // var llm01 = DC.GetLazyLoginMapper();
    // var tmp01 = DC.GetLazyLoginMapperChild<XYZ.DB.LazyLogin_Log>(llm01, null);
    //
    public static T GetLazyLoginMapperChild<T>(this ILazyLoginDataContext DC, ILazyLoginMapper llMapper)
      where T : class, ILazyLoginMapperFK, new()
    {
      return DC.GetLazyLoginMapperChild<T>(llMapper, null);
    }

    public static T GetLazyLoginMapperChild<T>(this ILazyLoginDataContext DC, ILazyLoginMapper llMapper, bool? autoSubmit)
      where T : class, ILazyLoginMapperFK, new()
    {
      T entity = null;
      try
      {
        llMapper = llMapper ?? DC.GetLazyLoginMapper(autoSubmit);
        entity = (DC as DataContext).GetTable<T>().Where(r => r.IdLL == llMapper.Id).FirstOrDefault();
        if (entity == null)
        {
          entity = new T();
          if (entity is ILazyLoginMapperInitialize)
            (entity as ILazyLoginMapperInitialize).InitializeLL();
          entity.LazyLoginMapperFK = llMapper;
          (DC as DataContext).GetTable<T>().InsertOnSubmit(entity);
          if (autoSubmit != false)
          {
            (DC as DataContext).SubmitChanges();
          }
        }
        return entity;
      }
      catch { }
      return null;
    }


    public static bool EnsureChildDependencies(this ILazyLoginMapper llMapper, params Type[] tables)
    {
      bool IsTainted = false;
      if (llMapper == null)
        return IsTainted;
      try
      {
        Type llty = llMapper.GetType();
        var matchedProps = llty.GetProperties().Join(tables, k1 => k1.PropertyType, k2 => k2, (t1, t2) => t1).ToList();
        foreach (PropertyInfo pi in matchedProps)
        {
          object val = pi.GetValue(llMapper, null);
          if (pi.PropertyType.IsAssignableTo(typeof(ILazyLoginMapperOneToOne)))
          {
            if (val == null)
            {
              object rel = pi.PropertyType.GetConstructor(Type.EmptyTypes).Invoke(null);
              if (rel is ILazyLoginMapperInitialize)
                (rel as ILazyLoginMapperInitialize).InitializeLL();
              if (rel != null)
              {
                pi.SetValue(llMapper, rel, null);
                IsTainted = true;
              }
            }
          }
        }
      }
      catch { }
      return IsTainted;
    }


  }


}

