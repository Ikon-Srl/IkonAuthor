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
using System.Web;
using System.Web.Caching;
using System.Web.Security;
using System.Linq;
using System.Xml.Linq;
using System.Data.Linq;
using System.Data.Linq.Mapping;
using System.Linq.Expressions;
using System.Net;
using System.IO;
using System.Text;
using System.Transactions;
using System.Web.SessionState;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;  // assembly System.ServiceModel.Web
using System.Data.Common;
using System.Data.Sql;
using System.Data.SqlClient;
using System.Data.SqlTypes;
using LinqKit;

using Ikon;
using Ikon.GD;
using Ikon.Log;
using System.Security.Principal;
using Ikon.Auth;



namespace Ikon
{

  public static class ImpersonationHelpers
  {

    /*
    public static bool FilePathWorker(string filePath, Action<string> worker) { return FilePathWorker(filePath, worker, null, null, null); }
    public static bool FilePathWorker(string filePath, Action<string> worker, string ShareServer, string ShareUserName, string SharePassword)
    {
      bool result = true;
      if (filePath.IsNullOrEmpty() || worker == null)
        return result;
      WindowsImpersonationContext impersonationContext = null;
      try
      {
        bool impersonationRequired = false;
        if (filePath.StartsWith("~") || filePath.StartsWith("/"))
          filePath = Utility.vPathMap(filePath);
        else if (filePath.StartsWith(@"..\"))
          filePath = System.IO.Path.GetFullPath(System.IO.Path.Combine(Utility.vPathMap("~/"), filePath));
        if (filePath.StartsWith(@"\\"))
        {
          string domainOrServer = ShareServer ?? IKGD_Config.AppSettings["ShareDomain"] ?? IKGD_Config.AppSettings["ShareServer"];
          string user = ShareUserName ?? IKGD_Config.AppSettings["ShareUserName"];
          string pass = SharePassword ?? IKGD_Config.AppSettings["SharePassword"];
          if (!string.IsNullOrEmpty(domainOrServer) && !string.IsNullOrEmpty(user) && !string.IsNullOrEmpty(pass))
          {
            impersonationRequired = true;
            impersonationContext = NetworkSecurity.ImpersonateUserCached(domainOrServer, user, pass, LogonType.LOGON32_LOGON_NEW_CREDENTIALS, LogonProvider.LOGON32_PROVIDER_DEFAULT);
          }
        }
        if (impersonationRequired == true && impersonationContext == null)
        {
          filePath = null;
        }
        if (filePath != null)
        {
          worker(filePath);
        }
      }
      catch
      {
        result = false;
      }
      finally
      {
        if (impersonationContext != null)
        {
          try
          {
            impersonationContext.Undo();
            impersonationContext.Dispose();
          }
          catch { }
        }
      }
      return result;
    }
    */


    public static bool FilePathWorker(string filePath, Action<string> worker) { return FilePathWorker(filePath, worker, null, null, null); }
    public static bool FilePathWorker(string filePath, Action<string> worker, string ShareServer, string ShareUserName, string SharePassword)
    {
      bool result = true;
      if (filePath.IsNullOrEmpty() || worker == null)
        return result;
      ImpersonationWorker impersonationWorker = null;
      try
      {
        impersonationWorker = ImpersonationWorker.Factory(filePath, ShareServer, ShareUserName, SharePassword);
        if (impersonationWorker.FilePath != null)
        {
          worker(impersonationWorker.FilePath);
        }
      }
      catch
      {
        result = false;
      }
      finally
      {
        impersonationWorker.Undo();
      }
      return result;
    }


    public class ImpersonationWorker
    {
      public WindowsImpersonationContext impersonationContext { get; set; }
      public string FilePath { get; set; }


      public static ImpersonationWorker Factory(string filePath) { return Factory(filePath, null, null, null); }
      public static ImpersonationWorker Factory(string filePath, string ShareServer, string ShareUserName, string SharePassword)
      {
        ImpersonationWorker result = new ImpersonationWorker();
        if (filePath.IsNullOrEmpty())
          return result;
        try
        {
          bool impersonationRequired = false;
          if (filePath.StartsWith("~") || filePath.StartsWith("/"))
            filePath = Utility.vPathMap(filePath);
          else if (filePath.StartsWith(@"..\"))
            filePath = System.IO.Path.GetFullPath(System.IO.Path.Combine(Utility.vPathMap("~/"), filePath));
          if (filePath.StartsWith(@"\\"))
          {
            string domainOrServer = ShareServer ?? IKGD_Config.AppSettings["ShareDomain"] ?? IKGD_Config.AppSettings["ShareServer"];
            string user = ShareUserName ?? IKGD_Config.AppSettings["ShareUserName"];
            string pass = SharePassword ?? IKGD_Config.AppSettings["SharePassword"];
            if (!string.IsNullOrEmpty(domainOrServer) && !string.IsNullOrEmpty(user) && !string.IsNullOrEmpty(pass))
            {
              impersonationRequired = true;
              result.impersonationContext = NetworkSecurity.ImpersonateUserCached(domainOrServer, user, pass, LogonType.LOGON32_LOGON_NEW_CREDENTIALS, LogonProvider.LOGON32_PROVIDER_DEFAULT);
            }
          }
          if (impersonationRequired == true && result.impersonationContext == null)
          {
            filePath = null;
          }
          result.FilePath = filePath;
        }
        catch { }
        return result;
      }

    }


    public static void Undo(this ImpersonationWorker impersonationWorker)
    {
      if (impersonationWorker != null && impersonationWorker.impersonationContext != null)
      {
        try
        {
          impersonationWorker.impersonationContext.Undo();
          impersonationWorker.impersonationContext.Dispose();
        }
        catch { }
      }
    }


  }

}
