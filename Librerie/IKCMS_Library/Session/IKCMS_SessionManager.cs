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
using System.Globalization;
using LinqKit;

using Ikon;
using Ikon.GD;
using Ikon.Log;


namespace Ikon.IKCMS
{


  public static class SessionManager
  {
    private static object _lock = new object();
    private static readonly string keySessionManager = "IKCMS_SessionManager";


    public static void Clear()
    {
      lock (_lock)
      {
        HttpContext.Current.Session.Remove(keySessionManager);
      }
    }


    public static SessionData DataNullable { get { return HttpContext.Current.Session[keySessionManager] as SessionData; } }
    public static SessionData Data { get { lock (_lock) { return (SessionData)(HttpContext.Current.Session[keySessionManager] ?? (HttpContext.Current.Session[keySessionManager] = new SessionData())); } } }

    public static Utility.DictionaryMV<string, string> Attributes { get { return Data.Attributes; } }
    public static KeyValueObjectTree KVT { get { return Data.KVT; } }

    //
    // gestione versione/snapshot VFS
    // in cookie temporanea con path / valida per il dominio di II livello
    //
    public static int VFS_Snapshot { get { return FS_OperationsHelpers.VersionFrozenSession; } }


    //
    // funzionalita' ancora da implementare in sessione:
    // user info
    // lazy login full/extra info
    //
    // creare delle funzionalita' di logon/SSO
    // con funzionalita' di trasformazione da anonymous->registered  registered->verified  ->authenticated
    // cancellazione delle role/area cache con FS_OperationsHelpers.ClearCachedData();
    //
    public class SessionData
    {
      public Utility.DictionaryMV<string, string> Attributes { get; private set; }
      public KeyValueObjectTree KVT { get; private set; }


      public SessionData()
      {
        Attributes = new Utility.DictionaryMV<string, string>();
        KVT = new KeyValueObjectTree();
      }

    }

  }


}
