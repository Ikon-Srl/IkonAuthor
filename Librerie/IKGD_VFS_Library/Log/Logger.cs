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


namespace Ikon.GD
{

  public static class VFS_EventLogger
  {
    public enum LoggerActions { Exception = 1, Login, LoginRefresh, Logout, LoginError, SearchQuery };


    //
    // funzione per l'accumulazione di log generici (login/logout, search, ...)
    // action:
    //          1 --> exception
    //          2 --> login
    //          3 --> login refresh
    //          4 --> logout
    //          5 --> search
    //
    public static void ProcessLogger(string username, LoggerActions action, string data)
    {
      try
      {
        using (IKGD_DataContext DB = IKGD_DBH.GetDB())
        {
          //
          // non uso LINQ direttamente perche' la tabella non dispone di una primary key e non posso fare inserimenti/update/cancellazioni
          //
          username = username ?? Ikon.GD.MembershipHelper.UserName;
          data = data ?? string.Empty;
          username = Utility.StringTruncate(username, 100);
          data = Utility.StringTruncate(data, 250);
          int sessionHash = 0;
          try { sessionHash = HttpContext.Current.Session.SessionID.GetHashCode(); }
          catch { }
          int res = DB.ExecuteCommand("INSERT INTO [IKG_LOGGER] ([username],[action],[data],[sessionHash]) VALUES ({0},{1},{2},{3})", username, (int)action, data, sessionHash);
        }
      }
      catch { }
    }


    //
    // registrazione degli hit logs
    // NB:
    //     action == 0   --> resource click
    //     action == 1   --> tab click  --> sNodeWidget = previous TAB, sNodeResource = NEW TAB
    //     action == 2   --> aggiunta widget
    //
    public static void ProcessHit(int sNodeWidget, int sNodeResource, int action, int code)
    {
      try
      {
        if (code == 0 && HttpContext.Current.Session != null)
          code = HttpContext.Current.Session.SessionID.GetHashCode();
        using (IKGD_DataContext DB = IKGD_DBH.GetDB())
        {
          //
          // non uso LINQ direttamente perche' la tabella non dispone di una primary key e non posso fare inserimenti/update/cancellazioni
          //
          int res = DB.ExecuteCommand("INSERT INTO [IKG_HITLOG] ([wID],[resID],[action],[code]) VALUES ({0},{1},{2},{3})", sNodeWidget, sNodeResource, action, code);
        }
      }
      catch { }
    }
    //
    public static void ProcessHit(FS_Operations fsOp, int sNodeWidget, int sNodeResource, int action, int code)
    {
      try
      {
        if (code == 0 && HttpContext.Current.Session != null)
          code = HttpContext.Current.Session.SessionID.GetHashCode();
        int res = fsOp.DB.ExecuteCommand("INSERT INTO [IKG_HITLOG] ([wID],[resID],[action],[code]) VALUES ({0},{1},{2},{3})", sNodeWidget, sNodeResource, action, code);
      }
      catch { }
    }

  
  }

}



