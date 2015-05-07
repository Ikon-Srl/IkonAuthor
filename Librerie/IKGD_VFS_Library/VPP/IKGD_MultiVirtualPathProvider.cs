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
using System.Text.RegularExpressions;
using System.Transactions;
using System.Web.SessionState;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;  // assembly System.ServiceModel.Web
using System.Data.Common;
using System.Data.Sql;
using System.Data.SqlClient;
using System.Data.SqlTypes;
using System.Reflection;
using System.Web.Hosting;
using LinqKit;

using ICSharpCode.SharpZipLib.Zip;

using Ikon;
using Ikon.GD;
using Ikon.Log;
using Ikon.IKCMS;



namespace Ikon.Handlers
{

  public static class IKGD_MultiVirtualPathProviderHelper
  {
    public static List<VirtualPathProvider> Providers { get; private set; }


    static IKGD_MultiVirtualPathProviderHelper()
    {
      Providers = new List<VirtualPathProvider>();
      //
      RegisterProviderWrapper(new IKGD_MultiVirtualPathProvider());
      //
    }


    public static void Setup() { }


    public static void RegisterVPP(VirtualPathProvider provider)
    {
      if (provider == null)
        return;
      if (provider is IKGD_MultiVirtualPathProvider)  // per evitare ricorsioni
        return;
      if (Providers.Any(p => p.GetType() == provider.GetType()))
        return;
      Providers.Add(provider);
    }


    public static void UnRegisterVPP(VirtualPathProvider provider)
    {
      if (provider != null)
      {
        Providers.RemoveAll(p => p.GetType() == provider.GetType());
      }
    }


    //
    // helper per la registrazione dei virtual path providers anche per siti precompilati
    //
    private static void RegisterProviderWrapper(VirtualPathProvider providerInstance)
    {
      HostingEnvironment hostingEnvironmentInstance = (HostingEnvironment)typeof(HostingEnvironment).InvokeMember("_theHostingEnvironment", BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.GetField, null, null, null);
      if (hostingEnvironmentInstance == null)
        return;
      MethodInfo mi = typeof(HostingEnvironment).GetMethod("RegisterVirtualPathProviderInternal", BindingFlags.NonPublic | BindingFlags.Static);
      if (mi == null)
        return;
      mi.Invoke(hostingEnvironmentInstance, new object[] { providerInstance });
    }


  }


  public class IKGD_MultiVirtualPathProvider : VirtualPathProvider, IBootStrapperTask
  {

    public IKGD_MultiVirtualPathProvider()
      : base()
    { }


    //
    // per inizializzare il VPP senza chiamarlo dal Global.asax
    //
    public void Execute()
    {
      IKGD_MultiVirtualPathProviderHelper.Setup();
    }


    public override bool FileExists(string virtualPath)
    {
      bool result = false;
      if (this.Previous != null)
      {
        result |= this.Previous.FileExists(virtualPath);
      }
      else
      {
        result |= base.FileExists(virtualPath);
      }
      if (!result)
        result |= IKGD_MultiVirtualPathProviderHelper.Providers.Any(p => p.FileExists(virtualPath));
      return result;
    }


    public override bool DirectoryExists(string virtualPath)
    {
      bool result = false;
      if (this.Previous != null)
      {
        result |= this.Previous.DirectoryExists(virtualPath);
      }
      else
      {
        result |= base.DirectoryExists(virtualPath);
      }
      if (!result)
      {
        result |= IKGD_MultiVirtualPathProviderHelper.Providers.Any(p => p.DirectoryExists(virtualPath));
      }
      return result;
    }


    public override VirtualFile GetFile(string virtualPath)
    {
      VirtualFile result = null;
      // ci sono dei problemi nel passaggio dei path dal dotless che non li normalizza correttamente
      if (virtualPath.IsNotEmpty() && (virtualPath.StartsWith("./") || virtualPath.StartsWith("../")))
      {
        try
        {
          string basePath = HttpContext.Current.Request.Path;
          var frags = basePath.Trim(' ', '/').Split('/');
          if (virtualPath.StartsWith("../"))
          {
            virtualPath = Utility.Implode(frags.SkipLast(2), "/", null, false) + virtualPath.Substring(2);
          }
          else if (virtualPath.StartsWith("./"))
          {
            virtualPath = Utility.Implode(frags.SkipLast(1), "/", null, false) + virtualPath.Substring(1);
          }
          virtualPath = "/" + virtualPath.TrimStart('/');
        }
        catch { }
      }
      if (this.Previous != null)
      {
        if (this.Previous.FileExists(virtualPath))
        {
          result = this.Previous.GetFile(virtualPath);
        }
      }
      else if (base.FileExists(virtualPath))
      {
        result = base.GetFile(virtualPath);
      }
      if (result == null)
      {
        foreach (var provider in IKGD_MultiVirtualPathProviderHelper.Providers)
        {
          result = provider.FileExists(virtualPath) ? provider.GetFile(virtualPath) : null;
          if (result != null)
            break;
        }
      }
      return result;
    }


    public override VirtualDirectory GetDirectory(string virtualPath)
    {
      MultiVirtualDirectory directory = new MultiVirtualDirectory(virtualPath);
      if (this.Previous != null)
      {
        if (this.Previous.DirectoryExists(virtualPath))
        {
          try { this.Previous.GetDirectory(virtualPath).Children.OfType<VirtualFileBase>().ForEach(f => directory.AddItem(f)); }
          catch { }
        }
      }
      else if (base.DirectoryExists(virtualPath))
      {
        try { base.GetDirectory(virtualPath).Children.OfType<VirtualFileBase>().ForEach(f => directory.AddItem(f)); }
        catch { }
      }
      foreach (var provider in IKGD_MultiVirtualPathProviderHelper.Providers)
      {
        if (provider.DirectoryExists(virtualPath))
        {
          try { provider.GetDirectory(virtualPath).Children.OfType<VirtualFileBase>().ForEach(f => directory.AddItem(f)); }
          catch { }
        }
      }
      return directory;
    }


    public override string GetCacheKey(string virtualPath)
    {
      string result = null;
      if (this.Previous != null)
      {
        if (this.Previous.FileExists(virtualPath))
        {
          result = this.Previous.GetCacheKey(virtualPath);
        }
      }
      else if (base.FileExists(virtualPath))
      {
        result = base.GetCacheKey(virtualPath);
      }
      if (result == null)
      {
        foreach (var provider in IKGD_MultiVirtualPathProviderHelper.Providers)
        {
          result = provider.FileExists(virtualPath) ? provider.GetCacheKey(virtualPath) : null;
          if (result != null)
            break;
        }
      }
      return result;
    }


    public override string GetFileHash(string virtualPath, IEnumerable virtualPathDependencies)
    {
      string result = null;
      if (this.Previous != null)
      {
        if (this.Previous.FileExists(virtualPath))
        {
          result = this.Previous.GetFileHash(virtualPath, virtualPathDependencies);
        }
      }
      else if (base.FileExists(virtualPath))
      {
        result = base.GetFileHash(virtualPath, virtualPathDependencies);
      }
      if (result == null)
      {
        foreach (var provider in IKGD_MultiVirtualPathProviderHelper.Providers)
        {
          result = provider.FileExists(virtualPath) ? provider.GetFileHash(virtualPath, virtualPathDependencies) : null;
          if (result != null)
            break;
        }
      }
      return result;
    }


    public override CacheDependency GetCacheDependency(string virtualPath, IEnumerable virtualPathDependencies, DateTime utcStart)
    {
      CacheDependency result = null;
      if (this.Previous != null)
      {
        if (this.Previous.FileExists(virtualPath))
        {
          result = this.Previous.GetCacheDependency(virtualPath, virtualPathDependencies, utcStart);
        }
      }
      else if (base.FileExists(virtualPath))
      {
        result = base.GetCacheDependency(virtualPath, virtualPathDependencies, utcStart);
      }
      if (result == null)
      {
        foreach (var provider in IKGD_MultiVirtualPathProviderHelper.Providers)
        {
          result = provider.FileExists(virtualPath) ? provider.GetCacheDependency(virtualPath, virtualPathDependencies, utcStart) : null;
          if (result != null)
            break;
        }
      }
      return result;
    }


    public override string CombineVirtualPaths(string basePath, string relativePath)
    {
      string result = null;
      if (this.Previous != null)
      {
        result = this.Previous.CombineVirtualPaths(basePath, relativePath);
      }
      else
      {
        result = base.CombineVirtualPaths(basePath, relativePath);
      }
      if (result == null)
      {
        foreach (var provider in IKGD_MultiVirtualPathProviderHelper.Providers)
        {
          result = provider.CombineVirtualPaths(basePath, relativePath);
          if (result != null)
            break;
        }
      }
      return result;
    }



    private class MultiVirtualDirectory : VirtualDirectory
    {
      public List<VirtualFileBase> Items { get; protected set; }

      public MultiVirtualDirectory(string virtualPath)
        : base(virtualPath)
      {
        Items = new List<VirtualFileBase>();
      }


      public void AddItem(VirtualFileBase item)
      {
        // attenzione che Files ritorna una lista con items potenzialmente doppi
        if (item != null)
        {
          string nameNorm = item.VirtualPath.Replace('/', '.');
          if (!Items.Any(f => string.Equals(f.VirtualPath.Replace('/', '.'), item.Name, StringComparison.OrdinalIgnoreCase)))
            Items.Add(item);
        }
      }


      public override IEnumerable Children { get { return Items; } }
      public override IEnumerable Directories { get { return Items.Where(f => f.IsDirectory); } }
      public override IEnumerable Files { get { return Items.Where(f => !f.IsDirectory); } }

    }

  }

}
