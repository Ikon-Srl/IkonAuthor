/*
 * 
 * Intranet Ikon
 * 
 * Copyright (C) 2009 Ikon Srl
 * Tutti i Diritti Riservati. All Rights Reserved
 * 
 */


using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Web.Hosting;
using System.Web;
using System.Reflection;

using ICSharpCode.SharpZipLib.Zip;



namespace Ikon.IKGD.VirtualPathProviders
{

  //
  // static manager to manage virtual path provider registration outside of global.asax
  // to use in library modules or Author modules
  //
  public static class ZipFileVirtualPathProviderManager
  {
    public static List<string> RegisteredZipFiles { get; private set; }

    static ZipFileVirtualPathProviderManager()
    {
      RegisteredZipFiles = new List<string>();
    }


    public static void EnsureZipFileVirtualPathProvider(string zipFilename, string regExPathFilter)
    {
      if (RegisteredZipFiles.Contains(zipFilename.ToLower()))
        return;
      try
      {
        RegisteredZipFiles.Add(zipFilename.ToLower());
        if (zipFilename.StartsWith("~/"))
        {
          var vpp = new Ikon.IKGD.VirtualPathProviders.ZipFileVirtualPathProvider(HostingEnvironment.MapPath(zipFilename), regExPathFilter, true);
        }
        else
        {
          //var resources = Assembly.GetExecutingAssembly().GetManifestResourceNames();
          var zipStream = Assembly.GetCallingAssembly().GetManifestResourceStream(zipFilename);
          if (zipStream != null)
          {
            var vpp = new Ikon.IKGD.VirtualPathProviders.ZipFileVirtualPathProvider(zipStream, regExPathFilter, true);
          }
        }
      }
      catch { }
    }
  }



  //
  // Usage:
  // in Global.asax: Application_Start()
  // var vpp = new ZipFileVirtualPathProvider(Server.MapPath("ZippedWebSite.zip"), @"/vpp/", true);
  // attenzione che la registrazione normale (con System.Web.Hosting.HostingEnvironment.RegisterVirtualPathProvider(vpp))
  // non funziona con i siti precompilati in quanto RegisterVirtualPathProvider non gira nel caso di siti precompilati
  // in tal caso e' necessario utilizzare la reflection
  //
  public class ZipFileVirtualPathProvider : System.Web.Hosting.VirtualPathProvider
  {
    public ZipFile vZipFile { get; private set; }
    public Regex vPathFilter { get; private set; }
    private FileStream _zipFstream;


    public ZipFileVirtualPathProvider(string zipFilename, string regExPathFilter, bool autoRegister)
      : base()
    {
      _zipFstream = new FileStream(zipFilename, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);  // deve restare aperto per gli accessi successivi
      Setup(_zipFstream, regExPathFilter, autoRegister);
    }

    public ZipFileVirtualPathProvider(Stream zipStream, string regExPathFilter, bool autoRegister)
      : base()
    {
      Setup(zipStream, regExPathFilter, autoRegister);
    }


    ~ZipFileVirtualPathProvider()
    {
      try
      {
        vZipFile.Close();
        if (_zipFstream != null)
        {
          _zipFstream.Close();
          _zipFstream.Dispose();
        }
      }
      catch { }
    }


    private void Setup(Stream zipStream, string regExPathFilter, bool autoRegister)
    {
      vZipFile = new ZipFile(zipStream);
      vPathFilter = new Regex(regExPathFilter, RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.Singleline);
      if (autoRegister)
      {
        //RegisterProviderHelper();
        Ikon.Handlers.IKGD_MultiVirtualPathProviderHelper.RegisterVPP(this);
      }
    }


    public override bool FileExists(string virtualPath)
    {
      if (vPathFilter.IsMatch(virtualPath))
      {
        string zipPath = Util.ConvertVirtualPathToZipPath(virtualPath, true);
        ZipEntry zipEntry = vZipFile.GetEntry(zipPath);
        if (zipEntry != null && !zipEntry.IsDirectory)
          return !zipEntry.IsDirectory;
      }
      return base.FileExists(virtualPath);
    }


    public override bool DirectoryExists(string virtualDir)
    {
      if (vPathFilter.IsMatch(virtualDir))
      {
        string zipPath = Util.ConvertVirtualPathToZipPath(virtualDir, false);
        ZipEntry zipEntry = vZipFile.GetEntry(zipPath);
        if (zipEntry != null && zipEntry.IsDirectory)
          return zipEntry.IsDirectory;
      }
      return base.DirectoryExists(virtualDir);
    }


    public override VirtualFile GetFile(string virtualPath)
    {
      if (vPathFilter.IsMatch(virtualPath))
      {
        if (base.FileExists(virtualPath))
          return base.GetFile(virtualPath);
        return new ZipVirtualFile(virtualPath, vZipFile);
      }
      return base.GetFile(virtualPath);
    }


    public override VirtualDirectory GetDirectory(string virtualDir)
    {
      if (vPathFilter.IsMatch(virtualDir))
      {
        return new ZipVirtualDirectory(virtualDir, vZipFile);
      }
      return base.GetDirectory(virtualDir);
    }


    public override string GetFileHash(string virtualPath, System.Collections.IEnumerable virtualPathDependencies)
    {
      if (vPathFilter.IsMatch(virtualPath))
        return null;
      return base.GetFileHash(virtualPath, virtualPathDependencies);
    }


    public override System.Web.Caching.CacheDependency GetCacheDependency(String virtualPath, System.Collections.IEnumerable virtualPathDependencies, DateTime utcStart)
    {
      if (vPathFilter.IsMatch(virtualPath))
        return null;
      return base.GetCacheDependency(virtualPath, virtualPathDependencies, utcStart);
    }




    //
    // VPP support types
    //

    private enum VirtualPathType
    {
      Files, Directories, All
    }


    internal static class Util
    {
      public static string ConvertVirtualPathToZipPath(String virtualPath, bool isFile)
      {
        string basePath = VirtualPathUtility.ToAbsolute("~/");
        if (virtualPath.StartsWith(basePath, StringComparison.OrdinalIgnoreCase))
          return virtualPath.Substring(basePath.Length) + (isFile ? string.Empty : "/");
        else
          return virtualPath;
        //if (virtualPath[0] == '/')
        //{
        //  if (!isFile)
        //    return virtualPath.Substring(1) + "/";
        //  else
        //    return virtualPath.Substring(1);
        //}
        //else
        //  return virtualPath;
      }

      public static string ConvertZipPathToVirtualPath(String zipPath)
      {
        return VirtualPathUtility.ToAbsolute("~/") + zipPath;
        //return "/" + zipPath;
      }
    }


    private class ZipVirtualPathCollection : MarshalByRefObject, IEnumerable
    {
      ZipFile _zipFile;
      ArrayList _paths;
      String _virtualPath;
      VirtualPathType _requestType;


      public ZipVirtualPathCollection(String virtualPath, VirtualPathType requestType, ZipFile zipFile)
      {
        _paths = new ArrayList();
        _virtualPath = virtualPath;
        _requestType = requestType;
        _zipFile = zipFile;

        PerformEnumeration();
      }


      private void PerformEnumeration()
      {
        String zipPath = Util.ConvertVirtualPathToZipPath(_virtualPath, false);

        if (zipPath[zipPath.Length - 1] != '/')
        {
          ZipEntry entry = _zipFile.GetEntry(zipPath);
          if (entry != null)
            _paths.Add(new ZipVirtualFile(zipPath, _zipFile));
          return;
        }
        else
        {
          foreach (ZipEntry entry in _zipFile)
          {
            Console.WriteLine(entry.Name);
            if (entry.Name == zipPath)
              continue;
            if (entry.Name.StartsWith(zipPath))
            {
              // if we're looking for files and current entry is a directory, skip it
              if (_requestType == VirtualPathType.Files && entry.IsDirectory)
                continue;
              // if we're looking for directories and current entry its not one, skip it
              if (_requestType == VirtualPathType.Directories && !entry.IsDirectory)
                continue;

              int pos = entry.Name.IndexOf('/', zipPath.Length);
              if (pos != -1)
              {
                if (entry.Name.Length > pos + 1)
                  continue;
              }
              //    continue;
              if (entry.IsDirectory)
                _paths.Add(new ZipVirtualDirectory(Util.ConvertZipPathToVirtualPath(entry.Name), _zipFile));
              else
                _paths.Add(new ZipVirtualFile(Util.ConvertZipPathToVirtualPath(entry.Name), _zipFile));
            }
          }
        }
      }


      public override object InitializeLifetimeService()
      {
        return null;
      }


      IEnumerator IEnumerable.GetEnumerator()
      {
        return _paths.GetEnumerator();
      }

    }


    private class ZipVirtualDirectory : VirtualDirectory
    {
      ZipFile _zipFile;


      public ZipVirtualDirectory(String virtualDir, ZipFile file)
        : base(virtualDir)
      {
        _zipFile = file;
      }


      public override System.Collections.IEnumerable Children
      {
        get
        {
          return new ZipVirtualPathCollection(base.VirtualPath, VirtualPathType.All, _zipFile);
        }
      }


      public override System.Collections.IEnumerable Directories
      {
        get
        {
          return new ZipVirtualPathCollection(base.VirtualPath, VirtualPathType.Directories, _zipFile);
        }
      }


      public override System.Collections.IEnumerable Files
      {
        get
        {
          return new ZipVirtualPathCollection(base.VirtualPath, VirtualPathType.Files, _zipFile);
        }
      }

    }


    private class ZipVirtualFile : VirtualFile
    {
      ZipFile _zipFile;


      public ZipVirtualFile(String virtualPath, ZipFile zipFile)
        : base(virtualPath)
      {
        _zipFile = zipFile;
      }


      public override System.IO.Stream Open()
      {
        ZipEntry entry = _zipFile.GetEntry(Util.ConvertVirtualPathToZipPath(base.VirtualPath, true));
        using (Stream st = _zipFile.GetInputStream(entry))
        {
          MemoryStream ms = new MemoryStream();
          ms.SetLength(entry.Size);
          byte[] buf = new byte[2048];
          while (true)
          {
            int r = st.Read(buf, 0, 2048);
            if (r == 0)
              break;
            ms.Write(buf, 0, r);
          }
          ms.Seek(0, SeekOrigin.Begin);
          return ms;
        }
      }
    }


  }


}

