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
using System.Web.UI;
using System.Web.Security;
using System.Xml.Linq;
using System.Data.Linq;
using System.IO;
using System.Text;
using System.Linq.Expressions;
using LinqKit;

using System.Reflection;
using System.Data.Linq.Mapping;
using System.Diagnostics;
using System.Linq.Dynamic;
using System.Transactions;
using System.Web.Caching;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;  // assembly System.ServiceModel.Web

using Ikon;
using Ikon.GD;
using Ikon.Log;


namespace Ikon.GD
{


  //
  // metodi per l'utilizzo senza using e registrazione dell'handler di unload per il dispose
  //
  public static class IKGD_VFS_WebPage
  {
    public static readonly string ctxtBaseStr = "IKGD_VFS_WebPage_";
    public static object _lock = new object();

    //
    // esempio di utilizzo in una pagina
    //
    // protected FS_Operations fsOp { get { return this.Auto_FS_Operations(); } }
    // protected FS_Operations fsOp { get { return this.Auto_FS_Operations(-1, false, true); } }
    //
    public static FS_Operations Auto_FS_Operations(this Page page) { return page.Auto_FS_Operations(null, false, false); }
    public static FS_Operations Auto_FS_Operations(this Page page, int? VersionSelector, bool disableObjectTracking, bool forceRoot)
    {
      lock (_lock)
      {
        FS_Operations fsOp = null;
        try
        {
          fsOp = (FS_Operations)HttpContext.Current.Items[ctxtBaseStr + "fsOp"];
          if (fsOp == null)
          {
            fsOp = new FS_Operations(VersionSelector, disableObjectTracking, forceRoot);
            HttpContext.Current.Items[ctxtBaseStr + "fsOp"] = fsOp;
            page.Unload += (o, e) => Unload_Auto_FS_Operations();
          }
        }
        catch { }
        return fsOp;
      }
    }


    public static void Unload_Auto_FS_Operations()
    {
      lock (_lock)
      {
        try
        {
          FS_Operations fsOp = (FS_Operations)HttpContext.Current.Items[ctxtBaseStr + "fsOp"];
          if (fsOp != null)
          {
            HttpContext.Current.Items.Remove(ctxtBaseStr + "fsOp");
            fsOp.Dispose();
            fsOp = null;
          }
        }
        catch { }
      }
    }


  }

}
