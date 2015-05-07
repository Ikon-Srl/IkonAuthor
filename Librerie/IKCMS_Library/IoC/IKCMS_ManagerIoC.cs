/*
 * 
 * Intranet Ikon
 * 
 * Copyright (C) 2010 Ikon Srl
 * Tutti i Diritti Riservati. All Rights Reserved
 * 
 */


using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Reflection;
using System.Web;
using System.Web.Mvc;
using System.Web.Routing;

using Autofac;
using Autofac.Core;
using Autofac.Builder;
using Autofac.Features;
using Autofac.Integration.Web;
using Autofac.Integration.Web.Mvc;
using LinqKit;

using Ikon;


namespace Ikon.IKCMS
{
  using Ikon.Config;
  using Ikon.GD;


  public static class IKCMS_ManagerIoC
  {
    public static IContainerProvider containerProvider { get; private set; }
    public static IContainer applicationContainer { get { return containerProvider.ApplicationContainer; } }
    public static ILifetimeScope requestContainer { get { return containerProvider.RequestLifetime; } }


    static IKCMS_ManagerIoC()
    {
    }


    //
    // metodo statico per il setup del framework IoC AutoFac in un contesto MVC
    // con supporto per lista di assembly con scan automatico dei controllers
    // funzione di inizializzazione customizzabile
    // e setup del controllerbuilder di AutoFac
    // ordine di scan:
    // 1) assembly per i controllers
    // 2) Bootstrapper.Autofac
    // 3) customInitializer
    //
    public static void Setup(Action<ContainerBuilder> customInitializer, params Assembly[] controllerAssemblies)
    {
      //
      ContainerBuilder builder = new ContainerBuilder();
      //
      StandardRegistrationIKCMS(builder);
      //
      // se non specifico nessun assembly viene analizzato l'assembly chiamante
      if (controllerAssemblies.Length > 0)
        builder.RegisterControllers(controllerAssemblies);
      else
      {
        builder.RegisterControllers(Assembly.GetCallingAssembly());
        try
        {
          var assembliesWithControllers = Utility.GetApplicationReferencedAssemblies(true).Where(a => a != null && a.GetCustomAttributes(typeof(IKGD_Assembly_WithControllersAttribute), false).Any()).ToList();
          builder.RegisterControllers(assembliesWithControllers.ToArray());
        }
        catch { }
      }
      //
      Ikon.IKCMS.Bootstrapper.Autofac(builder);
      //
      if (customInitializer != null)
        customInitializer(builder);
      //
      // esempi di registrazione
      //builder.RegisterType<XYZ.Web.Controllers.StaticTemplatesController>().InstancePerDependency();  // equivalente a builder.Register<...>().FactoryScoped();
      //builder.RegisterType<XYZ.Web.Controllers.StaticTemplatesController>().InstancePerHttpRequest();
      //builder.Register(fsOp => new Ikon.GD.FS_Operations()).InstancePerHttpRequest();
      //
      containerProvider = new ContainerProvider(builder.Build());
      //
      ControllerBuilder.Current.SetControllerFactory(new AutofacControllerFactory(containerProvider));
      //
      IKGD_QueueManager.Setup();
      //
    }


    public static void StandardRegistrationIKCMS(ContainerBuilder builder)
    {
      builder.Register(fsOp => new Ikon.GD.FS_Operations()).InstancePerHttpRequest();
      builder.Register(fsOp => new Ikon.GD.FS_Operations(true)).Named<Ikon.GD.FS_Operations>("readonly").InstancePerHttpRequest();
      //
      //usage:
      //private FS_Operations _fsOp = null;
      //public FS_Operations fsOp { get { return _fsOp ?? (_fsOp = IKCMS_ManagerIoC.requestContainer.ResolveNamed<FS_Operations>("readonly")); } }
      //public FS_Operations fsOp { get { return _fsOp ?? (_fsOp = IKCMS_ManagerIoC.requestContainer.Resolve<FS_Operations>()); } }

      builder.Register(c => Ikon.Filters.IKGD_HtmlCleaner.Factory()).InstancePerHttpRequest();

      //
      // TODO:
      // potrebbe essere utile registrare anche un handler per le transactions (con la transaction su onActivating)
      // per il tutto usare il naming per separare bene i contesti operativi
      //
      //builder.Register(fsOp => new Ikon.GD.FS_Operations(true)).Named<Ikon.GD.FS_Operations>("transaction").OnPreparing(e=>e.Context.Resolve ).InstancePerHttpRequest();
    }


    //
    // OBSOLETE: adesso e' supportato nella versione 2.2
    //
    // usage: NEW vedi: http://nblumhardt.com/2010/05/autofac-2-2-released/
    // var updater = new ContainerBuilder();
    // updater.RegisterType<A>();
    // updater.Register(c => new B()).As<IB>();
    // updater.Update(Ikon.IKCMS.IKCMS_ManagerIoC.applicationContainer);  // oppure un altro IContainer
    //
    //public class ContainerUpdater : ContainerBuilder
    //{
    //  ICollection<Action<IComponentRegistry>> _configurationActions = new List<Action<IComponentRegistry>>();
    //  public override void RegisterCallback(Action<IComponentRegistry> configurationAction)
    //  {
    //    _configurationActions.Add(configurationAction);
    //  }
    //  public void Update(IContainer container)
    //  {
    //    foreach (var action in _configurationActions)
    //      action(container.ComponentRegistry);
    //  }
    //}


    //
    // attributo per marcare gli assembly che contengono controllers per l'inizializzazione IoC
    //
    [global::System.AttributeUsage(AttributeTargets.Assembly, Inherited = false, AllowMultiple = false)]
    public sealed class IKGD_Assembly_WithControllersAttribute : Attribute
    {
      public IKGD_Assembly_WithControllersAttribute() { }
    }


  }

}