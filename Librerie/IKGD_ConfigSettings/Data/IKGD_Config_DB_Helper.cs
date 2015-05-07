/*
 * 
 * Intranet Ikon
 * 
 * Copyright (C) 2008 Ikon Srl
 * Tutti i Diritti Riservati. All Rights Reserved
 * 
 */


using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

using System.Data.Linq;
using System.Data.Linq.Mapping;
using System.Data;
using System.Reflection;
using System.Linq.Expressions;
using System.ComponentModel;
using System.Configuration;

using Ikon;
using LinqKit;



//
// customizzazioni personali di metodi non generati automaticamente dal LINQ
//
namespace Ikon.Config
{
  public partial class DataContext
  {
    public static readonly string ConnectionStringNameFactory = "GDCS";

    //
    // ritorna una nuova istanza del datacontext con la connectionstring letta da web.config
    //
    public static DataContext Factory()
    {
      if (ConfigurationManager.ConnectionStrings[ConnectionStringNameFactory] != null)
        return new DataContext(ConfigurationManager.ConnectionStrings[ConnectionStringNameFactory].ConnectionString);
      else
        return new DataContext();
    }
  }

}
