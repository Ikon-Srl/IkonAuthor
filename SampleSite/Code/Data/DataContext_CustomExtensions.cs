/*
 * 
 * Intranet Ikon
 * 
 * Copyright (C) 2011 Ikon Srl
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
using System.ComponentModel;
using System.Reflection;
using System.Text;
using System.Web.Security;
using System.Transactions;
using Autofac;
using Autofac.Core;
using Autofac.Integration.Web;
using LinqKit;


/// <summary>
/// Summary description for IkonGD_dataBase
/// </summary>


namespace Custom.DB
{
  using Ikon;
  using Ikon.IKCMS;
  using Ikon.GD;


  partial class DataContext_Custom : Ikon.IKCMS.IBootStrapperAutofacTask
  {
    public static readonly string ConnectionStringName = "GDCS";
    public const string ContextStringDB = "Ikon.GD.IKCAT.DB";
    private static MappingSource GD_MappingSource;

    public static string ConnectionString { get { return ConfigurationManager.ConnectionStrings[ConnectionStringName].ConnectionString; } }

    public static DataContext_Custom Factory() { return Factory(false); }
    public static DataContext_Custom Factory(bool forceNewconnection)
    {
      if (GD_MappingSource != null && !forceNewconnection)
        return new DataContext_Custom(ConnectionString, GD_MappingSource);
      else
      {
        DataContext_Custom newContext = new DataContext_Custom(ConnectionString);
        if (GD_MappingSource == null)
        {
          GD_MappingSource = newContext.Mapping.MappingSource;
        }
        return newContext;
      }
    }
    public static DataContext_Custom Factory(DataContext baseDB) { return new DataContext_Custom(baseDB.Connection, baseDB.Mapping.MappingSource); }
    public static DataContext_Custom Factory(Ikon.GD.FS_Operations fsOp) { return new DataContext_Custom(fsOp.DB.Connection); }

    partial void OnCreated()
    {
      IKCMS_ExecutionProfiler.AddMessage("DB: {0}.OnCreated()".FormatString(this.GetType().FullName));
    }


    public void ExecuteAutofac(ContainerBuilder builder)
    {
      // per registrare eventuali tipi custom in autofac
      //var DC = Ikon.IKCMS.IKCMS_ManagerIoC.requestContainer.Resolve<Custom.DB.DataContext_Custom>();
      //builder.Register(c => new DataContext_Custom()).InstancePerHttpRequest();
      builder.Register(c => DataContext_Custom.Factory()).InstancePerHttpRequest();
    }

  }


  partial class DataContext_Custom : Ikon.GD.ILazyLoginDataContext
  {
    public Type LazyLoginMapperType { get { return typeof(LazyLoginMapper); } }
  }


  //
  // LazyLogin support stuff
  //

  public partial class LazyLoginMapper : DataContextGeneric<DataContext_Custom>, ILazyLoginMapper, ILazyLoginMapperInitialize
  {
    public void InitializeLL()
    {
      this.Creat = DateTime.Now;
      this.flag_active = true;
    }
  }


  public partial class LazyLogin_Log : DataContextGeneric<DataContext_Custom>, ILazyLoginMapperOneToMany, ILazyLoginMapperInitialize
  {
    public ILazyLoginMapper LazyLoginMapperFK { get { return this.LazyLoginMapper; } set { this.LazyLoginMapper = value as LazyLoginMapper; } }

    public void InitializeLL()
    {
      this.Date = DateTime.Now;
    }
  }


  public partial class LazyLogin_Vote : DataContextGeneric<DataContext_Custom>, ILazyLoginMapperOneToMany, ILazyLoginMapperInitialize
  {
    public ILazyLoginMapper LazyLoginMapperFK { get { return this.LazyLoginMapper; } set { this.LazyLoginMapper = value as LazyLoginMapper; } }

    public void InitializeLL()
    {
      this.Date = DateTime.Now;
    }
  }


  public partial class LazyLogin_AnagraficaMain : DataContextGeneric<DataContext_Custom>, ILazyLoginMapperOneToOne, ILazyLoginMapperInitialize
  {
    public ILazyLoginMapper LazyLoginMapperFK { get { return this.LazyLoginMapper; } set { this.LazyLoginMapper = value as LazyLoginMapper; } }

    public void InitializeLL()
    {
      this.Creat = this.Modif = DateTime.Now;
    }
  }


  public partial class LazyLogin_Setting : DataContextGeneric<DataContext_Custom>, ILazyLoginMapperOneToOne, ILazyLoginMapperInitialize
  {
    public ILazyLoginMapper LazyLoginMapperFK { get { return this.LazyLoginMapper; } set { this.LazyLoginMapper = value as LazyLoginMapper; } }

    public void InitializeLL()
    {
      this.Modif = DateTime.Now;
      //this.LanguageSite = IKGD_Language_Provider.Provider.LanguageNN;
      //this.PriceVisible = null;
    }
  }


  //
  // migrazione dei dati relativi alle estensioni Lazylogin
  //
  public class IKGD_MembershipAnonymousDataMigration_SampleSite : I_IKGD_MembershipAnonymousDataMigration
  {
    public static int MigrateAnonymousData(IKGD_DataContext DB, MembershipUser userOld, ILazyLoginMapper UserLL_Old, MembershipUser userNew, ILazyLoginMapper UserLL_New)
    {
      /*
      var DC = Ikon.IKCMS.IKCMS_ManagerIoC.requestContainer.Resolve<Custom.DB.DataContext_Custom>();
      using (TransactionScope ts = IKGD_TransactionFactory.Transaction(null))
      {
        try
        {
          if (UserLL_Old != null && UserLL_New != null)
          {
            var chg = DC.GetChangeSet();
            DC.SubmitChanges();
          }
          //
          ts.Complete();
        }
        catch { }
      }
      */
      return 0;
    }
  }



}
