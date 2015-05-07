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
using System.Web.Hosting;
using System.Xml.Linq;
using System.Data.Linq;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Security;
using System.Linq.Expressions;
using LinqKit;

using System.Reflection;
using System.Data.Linq.Mapping;
using System.Diagnostics;
using System.Transactions;
using System.Web.Caching;

using Ikon;
using Ikon.GD;


namespace Ikon.IKCMS
{

  //
  // virtual path provider per il supporto di views e resources embedded negli assembly
  //

  //
  // attributo per marcare gli assembly che contengono risorse associate ad un VPP
  //
  [global::System.AttributeUsage(AttributeTargets.Assembly, Inherited = false, AllowMultiple = false)]
  public sealed class IKCMS_Assembly_WithEmbeddedResourcesVPPAttribute : Attribute
  {
    public List<string> Roots { get; private set; }
    public IKCMS_Assembly_WithEmbeddedResourcesVPPAttribute(params string[] rootPaths)
    {
      Roots = new List<string>(rootPaths ?? Enumerable.Empty<string>());
    }
  }


  public static class EmbeddedResourcesVPP_Helper
  {
    private static List<EmbeddedResourceInfoVPP> ResourcesData { get; set; }


    static EmbeddedResourcesVPP_Helper()
    {
      ResourcesData = new List<EmbeddedResourceInfoVPP>();
      try
      {
        if (Utility.TryParse<bool>(IKGD_Config.AppSettings["EmbeddedResourcesVPP_Enabled"], true) == false)
        {
          return;
        }
        //
        //System.Web.Hosting.HostingEnvironment.RegisterVirtualPathProvider(new EmbeddedResourcesVPP_ViewPathProvider());
        Ikon.Handlers.IKGD_MultiVirtualPathProviderHelper.RegisterVPP(new EmbeddedResourcesVPP_ViewPathProvider());
        //
        var baseFolders = new List<string> { "Base" };
        try { baseFolders.InsertRange(0, Utility.Explode(Utility.FileReadVirtual("~/App_Data/EmbeddedResourcesVPP.list").TrimSafe(' ', '\r', '\n', '\t'), ", \r\n\t", " ", true)); }
        catch { baseFolders.Insert(0, "Version_100"); }
        var activeFolders = baseFolders.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        //
        List<EmbeddedResourceInfoVPP> datas = new List<EmbeddedResourceInfoVPP>();
        try
        {
          var assemblies = Utility.GetApplicationReferencedAssemblies(true).Where(a => a != null && a.GetCustomAttributes(typeof(IKCMS_Assembly_WithEmbeddedResourcesVPPAttribute), false).Any()).ToList();
          foreach (var assembly in assemblies)
          {
            var resources = assembly.GetManifestResourceNames().OrderBy(s => s).ToList();
            var foldersDeclared = resources.Where(r => r.EndsWith(".__folder__")).Select(r => r.Substring(0, r.Length - "__folder__".Length)).Distinct().ToList();  //lasciamo il . alla fine
            resources.RemoveAll(r => r.EndsWith(".__folder__"));
            var resourceRoots = assembly.GetCustomAttributes(typeof(IKCMS_Assembly_WithEmbeddedResourcesVPPAttribute), false).OfType<IKCMS_Assembly_WithEmbeddedResourcesVPPAttribute>().SelectMany(a => a.Roots).Select(s => s.TrimSafe()).Where(s => s.IsNotEmpty()).Distinct().ToList();
            foreach (var root in resourceRoots)
            {
              foreach (var folder in activeFolders)
              {
                string frag = "." + Regex.Replace(string.Format("{0}.{1}", root, folder), @"\./", ".").Trim(" .".ToCharArray()) + ".";
                var foldersDeclared2 = foldersDeclared.Where(r => r.IndexOf(frag, StringComparison.OrdinalIgnoreCase) >= 0).Select(r => r.Substring(r.IndexOf(frag, StringComparison.OrdinalIgnoreCase) + frag.Length - 1)).Distinct().ToList();
                foreach (var resource in resources)
                {
                  int idx = resource.IndexOf(frag, StringComparison.OrdinalIgnoreCase);
                  if (idx >= 0)
                  {
                    var vPath = resource.Substring(idx + frag.Length - 1);
                    var vFolder = foldersDeclared2.Where(r => vPath.IndexOf(r, StringComparison.OrdinalIgnoreCase) == 0).OrderByDescending(r => r.Length).FirstOrDefault() ?? ".";
                    datas.Add(new EmbeddedResourceInfoVPP { assembly = assembly, resource = resource, root = folder, vPath = vPath, vFolder = vFolder });
                  }
                }
              }
            }
          }
        }
        catch (Exception ex)
        {
          Elmah.ErrorSignal.FromCurrentContext().Raise(ex);
        }
        ResourcesData = datas.OrderBy(r => activeFolders.IndexOfSortable(r.root)).ThenBy(r => r.resource).Distinct((r1, r2) => string.Equals(r1.vPath, r2.vPath, StringComparison.OrdinalIgnoreCase)).ToList();
        //StringBuilder sb = new StringBuilder();
        //ResourcesData.OrderBy(r => r.root).ThenBy(r => r.vFolder).ThenBy(r => r.vPath).ForEach(r => sb.AppendFormat("{0} :: {1} --> {2}\n", r.root, r.vFolder, r.vPath));
        //var resourcesList = sb.ToString();
        //Elmah.ErrorSignal.FromCurrentContext().Raise(new Exception("EmbeddedResourcesVPP initialized with {0} resources.".FormatString(ResourcesData.Count)));
      }
      catch (Exception ex)
      {
        Elmah.ErrorSignal.FromCurrentContext().Raise(ex);
      }
    }


    // definito solo per per poter chiamare il costruttore static
    public static void Setup() { }


    public static string GetNormalizedPath(string virtualPath)
    {
      string vPath = virtualPath;
      if (virtualPath.IsNotEmpty() && !virtualPath.StartsWith("."))
      {
        try { vPath = VirtualPathUtility.ToAppRelative(virtualPath).Replace("~/", "/"); }
        catch { }
      }
      string resourceNameNormalized = vPath.Replace("/", ".");
      return resourceNameNormalized;
    }


    public static bool ResourceFileExists(string virtualPath)
    {
      bool found = false;
      try
      {
        string resourceName = GetNormalizedPath(virtualPath);
        found = ResourcesData.Any(r => string.Equals(r.vPath, resourceName, StringComparison.OrdinalIgnoreCase));
      }
      catch { }
      return found;
    }


    public static bool ResourceDirectoryExists(string virtualPath)
    {
      bool found = false;
      try
      {
        string resourceName = GetNormalizedPath(virtualPath).TrimEnd('.') + ".";
        found = ResourcesData.Any(r => r.vPath.IndexOf(resourceName, StringComparison.OrdinalIgnoreCase) >= 0);
      }
      catch { }
      return found;
    }


    public static IEnumerable<VirtualFile> ResourceDirectoryFiles(string virtualPath)
    {
      try
      {
        string resourceName = GetNormalizedPath(virtualPath).TrimEnd('.') + ".";
        return ResourcesData.Where(r => string.Equals(r.vFolder, resourceName, StringComparison.OrdinalIgnoreCase)).Select(r => new EmbeddedResourcesVPP_VirtualFile(r.vPath)).OfType<VirtualFile>();
      }
      catch { }
      return Enumerable.Empty<VirtualFile>();
    }


    public static EmbeddedResourceInfoVPP GetResourceData(string virtualPath)
    {
      EmbeddedResourceInfoVPP resourceData = null;
      try
      {
        string resourceName = GetNormalizedPath(virtualPath);
        resourceData = ResourcesData.FirstOrDefault(r => string.Equals(r.vPath, resourceName, StringComparison.OrdinalIgnoreCase));
      }
      catch { }
      return resourceData;
    }


    public static Stream ResourceFileOpenStream(string virtualPath)
    {
      Stream resourceStream = null;
      try
      {
        string resourceName = GetNormalizedPath(virtualPath);
        var data = ResourcesData.FirstOrDefault(r => string.Equals(r.vPath, resourceName, StringComparison.OrdinalIgnoreCase));
        if (data != null)
        {
          resourceStream = data.assembly.GetManifestResourceStream(data.resource);
        }
      }
      catch { }
      return resourceStream;
    }


    public static string GetResourceOrFileAsString(string virtualPath)
    {
      try
      {
        if (File.Exists(Utility.vPathMap(virtualPath)))
        {
          return Utility.FileReadVirtual(virtualPath);
        }
        else if (ResourceFileExists(virtualPath))
        {
          using (var stream = ResourceFileOpenStream(virtualPath))
          {
            using (StreamReader reader = new StreamReader(stream, Encoding.UTF8))
            {
              return reader.ReadToEnd();
            }
          }
        }
      }
      catch { }
      return null;
    }


    public static string GetResourceOrFileLastWriteTime(string virtualPath)
    {
      try
      {
        if (File.Exists(Utility.vPathMap(virtualPath)))
        {
          var physicalPath = HttpContext.Current.Server.MapPath(virtualPath);
          return new System.IO.FileInfo(physicalPath).LastWriteTime.ToString("yyyyMMddhhmmss");
        }
        else if (ResourceFileExists(virtualPath))
        {
          string resourceName = GetNormalizedPath(virtualPath);
          var data = ResourcesData.FirstOrDefault(r => string.Equals(r.vPath, resourceName, StringComparison.OrdinalIgnoreCase));
          if (data != null)
          {
            return data.assembly.ManifestModule.ModuleVersionId.GetHashCode().ToString("x");
          }
        }
      }
      catch { }
      return DateTime.MinValue.ToString("yyyyMMddhhmmss");
    }


    public class EmbeddedResourceInfoVPP
    {
      public Assembly assembly { get; set; }
      public string resource { get; set; }
      public string vPath { get; set; }
      public string vFolder { get; set; }
      public string root { get; set; }
    }

  }



  public class EmbeddedResourcesVPP_VirtualFile : VirtualFile
  {
    public EmbeddedResourcesVPP_VirtualFile(string virtualPath)
      : base(virtualPath)
    { }

    public override Stream Open()
    {
      return EmbeddedResourcesVPP_Helper.ResourceFileOpenStream(this.VirtualPath);
    }
  }


  public class EmbeddedResourcesVPP_VirtualDirectory : VirtualDirectory
  {
    public EmbeddedResourcesVPP_VirtualDirectory(string virtualPath)
      : base(virtualPath)
    { }

    public override IEnumerable Children { get { return Files; } }
    public override IEnumerable Directories { get { return Enumerable.Empty<EmbeddedResourcesVPP_VirtualFile>(); } }
    public override IEnumerable Files { get { return EmbeddedResourcesVPP_Helper.ResourceDirectoryFiles(this.VirtualPath); } }

  }



  public class EmbeddedResourcesVPP_ViewPathProvider : VirtualPathProvider, IBootStrapperTask
  {

    public EmbeddedResourcesVPP_ViewPathProvider()
      : base()
    { }


    // per inizializzare il VPP senza chiamarlo dal Global.asax
    public void Execute()
    {
      EmbeddedResourcesVPP_Helper.Setup();
    }


    public override bool FileExists(string virtualPath)
    {
      return EmbeddedResourcesVPP_Helper.ResourceFileExists(virtualPath);
    }


    public override VirtualFile GetFile(string virtualPath)
    {
      return new EmbeddedResourcesVPP_VirtualFile(virtualPath);
    }


    public override bool DirectoryExists(string virtualPath)
    {
      return EmbeddedResourcesVPP_Helper.ResourceDirectoryExists(virtualPath);
    }


    public override VirtualDirectory GetDirectory(string virtualPath)
    {
      return new EmbeddedResourcesVPP_VirtualDirectory(virtualPath);
    }


    public override string GetFileHash(string virtualPath, IEnumerable virtualPathDependencies)
    {
      var hashes = new List<int> { virtualPath.GetHashCode() };
      if (virtualPathDependencies != null)
      {
        foreach (var obj in virtualPathDependencies)
          if (obj != null)
            hashes.Add(obj.GetHashCode());
      }
      return Utility.Implode(hashes, "|");
    }


    public override CacheDependency GetCacheDependency(string virtualPath, IEnumerable virtualPathDependencies, DateTime utcStart)
    {
      try
      {
        EmbeddedResourcesVPP_Helper.EmbeddedResourceInfoVPP data = EmbeddedResourcesVPP_Helper.GetResourceData(virtualPath);
        if (data != null)
        {
          return new CacheDependency(data.assembly.Location);
        }
      }
      catch { }
      return null;
    }


  }





}
