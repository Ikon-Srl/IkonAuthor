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
using System.Linq.Expressions;
using System.Threading;
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

using Ikon;
using Ikon.GD;
using Ikon.Log;


namespace Ikon.IKCMS
{

  //
  // Bootstrapper classes:
  // fare riferimento a:
  // http://weblogs.asp.net/rashid/archive/2009/02/17/use-bootstrapper-in-your-asp-net-mvc-application-and-reduce-code-smell.aspx
  // da utilizzare in global.asax
  //
  //protected void Application_Start()
  //{
  //  Ikon.IKCMS.Bootstrapper.Run();
  //}


  //
  // definite in IKGD_VFS_Library
  //
  //public interface IBootStrapperPreTask
  //{
  //  void ExecutePre();
  //}
  //public interface IBootStrapperTask
  //{
  //  void Execute();
  //}
  //public interface IBootStrapperPostTask
  //{
  //  void ExecutePost();
  //}


  public interface IBootStrapperAutofacTask
  {
    void ExecuteAutofac(ContainerBuilder builder);
  }



  public static class Bootstrapper
  {
    static Bootstrapper()
    {
      if (Utility.TryParse<bool>(IKGD_Config.AppSettingsWeb["AssemblyResolveOnDB"], false))
      {
        AppDomain.CurrentDomain.AssemblyResolve += new ResolveEventHandler(Ikon.Support.IKGD_AssemblyManagerHandler.AssemblyResolveHandler);
      }
    }


    public static void Run()
    {
      //var tasks = Utility.FindTypesWithInterfaces(typeof(IBootStrapperTask)).ToList();
      //foreach (var taskType in tasks)
      //{
      //  try
      //  {
      //    IBootStrapperTask task = (IBootStrapperTask)taskType.GetConstructor(Type.EmptyTypes).Invoke(null);
      //    task.Execute();
      //  }
      //  catch { }
      //}

      GetTypeList(typeof(IBootStrapperPreTask)).ForEach(t =>
      {
        IBootStrapperPreTask task = null;
        try { task = task ?? Activator.CreateInstance(t) as IBootStrapperPreTask; }
        catch { }
        try { task = task ?? FormatterServices.GetUninitializedObject(t) as IBootStrapperPreTask; }
        catch { }
        try { task.ExecutePre(); }
        catch { }
      });
      GetTypeList(typeof(IBootStrapperTask)).ForEach(t =>
      {
        IBootStrapperTask task = null;
        try { task = task ?? Activator.CreateInstance(t) as IBootStrapperTask; }
        catch { }
        try { task = task ?? FormatterServices.GetUninitializedObject(t) as IBootStrapperTask; }
        catch { }
        try { task.Execute(); }
        catch { }
      });
      GetTypeList(typeof(IBootStrapperPostTask)).ForEach(t =>
      {
        IBootStrapperPostTask task = null;
        try { task = task ?? Activator.CreateInstance(t) as IBootStrapperPostTask; }
        catch { }
        try { task = task ?? FormatterServices.GetUninitializedObject(t) as IBootStrapperPostTask; }
        catch { }
        try { task.ExecutePost(); }
        catch { }
      });

    }


    public static void Autofac(ContainerBuilder builder)
    {
      // se si marcano i controller con IBootStrapperAutofacTask e questi hanno dei costruttori complessi
      // si e' costretti ad implementare l'interface su una classe separata quindi usiamo un generatore di
      // oggetti non inizializzati (senza chiamare costruttori incasinati o vuoti che non esistono)
      // e chiamo un metodo non static della classe che e' mappato sull'interface per gestire le registrazioni con autofac
      GetTypeList(typeof(IBootStrapperAutofacTask)).ForEach(t =>
      {
        try { (FormatterServices.GetUninitializedObject(t) as IBootStrapperAutofacTask).ExecuteAutofac(builder); }
        catch { }
      });
    }


    // versione per istanze multiple dell'attributo
    private static List<Type> GetTypeList(Type interfaceType)
    {
      List<Type> types = new List<Type>();
      Utility.FindTypesWithInterfaces(interfaceType).Where(t => t.IsClass && !t.IsAbstract).OrderBy(t => t.GetCustomAttributes(true).OfType<IKCMS_ModelCMS_BootStrapperOrderAttribute>().DefaultIfEmpty(new IKCMS_ModelCMS_BootStrapperOrderAttribute(0)).FirstOrDefault().Position).ForEach(ty =>
      {
        if (ty.IsGenericTypeDefinition)
        {
          Utility.GetAttributesFromType<IKCMS_ModelCMS_BootStrapperOpenGenericsAttribute>(ty).ForEach(a =>
          {
            try
            {
              Type t = ty.MakeGenericType(a.Types);
              if (t != null)
                types.Add(t);
            }
            catch { }
          });
        }
        else
          types.Add(ty);
      });
      return types;
    }


  }


  //
  // Attribute per marcare le classi del bootstrapper in modo che nel caso si tratti
  // di Open Generics venga fornita la lista di tipi per creare un Generic completo
  //
  [global::System.AttributeUsage(AttributeTargets.All, Inherited = true, AllowMultiple = true)]
  public sealed class IKCMS_ModelCMS_BootStrapperOpenGenericsAttribute : Attribute
  {
    public Type[] Types { get; private set; }

    public IKCMS_ModelCMS_BootStrapperOpenGenericsAttribute(params Type[] resolvedTypes)
    {
      Types = resolvedTypes.ToArray();
    }
  }


  //
  // attributo per gestire la priorita' delle inizializzazioni del bootstrapper
  //
  [global::System.AttributeUsage(AttributeTargets.All, Inherited = true, AllowMultiple = false)]
  public sealed class IKCMS_ModelCMS_BootStrapperOrderAttribute : Attribute
  {
    public int Position { get; private set; }

    public IKCMS_ModelCMS_BootStrapperOrderAttribute(int priority)
    {
      Position = priority;
    }
  }


}

