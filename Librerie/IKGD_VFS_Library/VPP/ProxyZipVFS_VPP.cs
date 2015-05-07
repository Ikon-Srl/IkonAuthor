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



namespace Ikon.Handlers
{

  public static class ProxyZipVFS_Helper
  {
    public static Dictionary<string, ZipFile> RegisteredZips { get; private set; }
    public static Dictionary<string, string> RegisteredZipFileNameDependancies { get; private set; }
    public static Dictionary<string, Regex> zipFileRegexPattern { get; private set; }
    public static Dictionary<string, string> zipFileOverridePath { get; private set; }
    private static Dictionary<string, Stream> zipFileStreams { get; set; }
    //
    public static ZipFileVirtualPathProviderV3 VPP_Instance { get; private set; }
    //

    static ProxyZipVFS_Helper()
    {
      RegisteredZips = new Dictionary<string, ZipFile>(StringComparer.OrdinalIgnoreCase);
      RegisteredZipFileNameDependancies = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
      zipFileRegexPattern = new Dictionary<string, Regex>(StringComparer.OrdinalIgnoreCase);
      zipFileOverridePath = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
      zipFileStreams = new Dictionary<string, Stream>(StringComparer.OrdinalIgnoreCase);
      //
      VPP_Instance = new ZipFileVirtualPathProviderV3();
      IKGD_MultiVirtualPathProviderHelper.RegisterVPP(VPP_Instance);
    }


    public static bool RegisterZipFile(string keyVFS, string zipFileName, string regExPathFilter, string zipOverridePath) { return RegisterZipFile(keyVFS, zipFileName, regExPathFilter, zipOverridePath, null); }
    public static bool RegisterZipFile(string keyVFS, string zipFileName, string regExPathFilter, string zipOverridePath, Assembly assembly)
    {
      if (string.IsNullOrEmpty(keyVFS) || string.IsNullOrEmpty(zipFileName))
        throw new ArgumentException("Invalid Arguments.");
      try
      {
        if (RegisteredZips.ContainsKey(keyVFS))
          return true;
        Stream _zipFstream = null;
        if (zipFileName.StartsWith("~/"))
        {
          zipFileName = Utility.vPathMap(zipFileName);
          RegisteredZipFileNameDependancies[keyVFS] = zipFileName;
          _zipFstream = new FileStream(zipFileName, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);  // deve restare aperto per gli accessi successivi
        }
        else
        {
          assembly = assembly ?? Assembly.GetCallingAssembly();
          if (assembly != null)
            RegisteredZipFileNameDependancies[keyVFS] = assembly.Location;
          _zipFstream = assembly.GetManifestResourceStream(zipFileName);
        }
        if (_zipFstream == null)
          throw new Exception("Invalid Zip Stream.");
        //
        zipOverridePath = VirtualPathUtility.AppendTrailingSlash(VirtualPathUtility.ToAbsolute(zipOverridePath.NullIfEmpty() ?? "~/"));
        try { zipFileRegexPattern[keyVFS] = new Regex(regExPathFilter, RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.Singleline); }
        catch { }
        //
        RegisteredZips[keyVFS] = new ZipFile(_zipFstream);
        zipFileStreams[keyVFS] = _zipFstream;  // per evitarne il rilascio e chiusura da parte del GC
        zipFileOverridePath[keyVFS] = zipOverridePath;
        //
        return true;
      }
      catch { }
      return false;
    }


  }


  public class ProxyZipVFS : IHttpHandler
  {
    public bool IsReusable { get { return false; } }


    public void ProcessRequest(HttpContext context)
    {
      Stream outStream = null;
      try
      {
        string prefixPath = context.Request.AppRelativeCurrentExecutionFilePath;
        string reqPath = context.Request.PathInfo;
        List<string> frags = prefixPath.Split('/').Skip(1).Reverse().Skip(1).ToList();  // toglie ~ , ProxyZipVFS.axd e inverte il path
        string keyZip = frags.FirstOrDefault(f => ProxyZipVFS_Helper.RegisteredZips.ContainsKey(f));
        if (string.IsNullOrEmpty(keyZip))
          return;

        if (!string.IsNullOrEmpty(ProxyZipVFS_Helper.zipFileOverridePath[keyZip]))
        {
          string reqName = VirtualPathUtility.Combine(ProxyZipVFS_Helper.zipFileOverridePath[keyZip], reqPath);
          string fName = Utility.vPathMap(reqPath);
          if (File.Exists(fName))
            outStream = new FileStream(fName, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        }
        //
        ZipEntry zipEntry = ProxyZipVFS_Helper.RegisteredZips[keyZip].GetEntry(reqPath);
        if (zipEntry != null && !zipEntry.IsDirectory)
        {
          outStream = ProxyZipVFS_Helper.RegisteredZips[keyZip].GetInputStream(zipEntry);
        }
        //
        if (outStream != null)
        {
          byte[] buf = new byte[(int)outStream.Length];
          int len = outStream.Read(buf, 0, buf.Length);
          context.Response.OutputStream.Write(buf, 0, len);
        }
      }
      catch { }
      finally
      {
        if (outStream != null)
        {
          outStream.Close();
          outStream.Dispose();
        }
        context.Response.Flush();
        //context.Response.End();
        context.ApplicationInstance.CompleteRequest(); // da usare al posto di .Response.End();
      }
      return;
    }


  }



  //
  // VPP con supporto per files Zip multipli
  //
  public class ZipFileVirtualPathProviderV3 : VirtualPathProvider
  {

    public ZipFileVirtualPathProviderV3()
      : base()
    { }



    public override bool FileExists(string virtualPath)
    {
      try
      {
        foreach (var kv in ProxyZipVFS_Helper.zipFileRegexPattern.Where(r => r.Value.IsMatch(virtualPath)))
        {
          try
          {
            string zipPath = VirtualPathUtility.RemoveTrailingSlash(kv.Value.Match(virtualPath).Groups[1].Value);
            ZipFile vZipFile = ProxyZipVFS_Helper.RegisteredZips[kv.Key];
            ZipEntry zipEntry = vZipFile.GetEntry(zipPath);
            if (zipEntry != null && !zipEntry.IsDirectory)
              return !zipEntry.IsDirectory;
          }
          catch { }
        }
        //
        // il VPP si "mangia" la gestione degli handler per cui devo integrare la gestione del ProxyVFS handler
        // il problema deriva dalla gestione delle risorse con FCKeditor che se usa /ProxyVFS.axd non funziona nel caso il sito
        // non sia configurato in root mentre utilizzando ProxyVFS.axd (senza /) viene "mangiato" dal VPP
        //
        if (virtualPath.IndexOf("/ProxyVFS.axd", StringComparison.OrdinalIgnoreCase) > 0)
        {
          HttpContext.Current.Response.Redirect("~" + HttpContext.Current.Request.Url.PathAndQuery.Substring(HttpContext.Current.Request.Url.PathAndQuery.IndexOf("/ProxyVFS.axd", StringComparison.OrdinalIgnoreCase)), false);
          HttpContext.Current.ApplicationInstance.CompleteRequest();  // da usare al posto di .Response.End();
          return true;
        }
      }
      catch { }
      return false;
    }


    public override bool DirectoryExists(string virtualPath)
    {
      try
      {
        foreach (var kv in ProxyZipVFS_Helper.zipFileRegexPattern.Where(r => r.Value.IsMatch(virtualPath)))
        {
          try
          {
            string zipPath = VirtualPathUtility.AppendTrailingSlash(kv.Value.Match(virtualPath).Groups[1].Value);
            ZipFile vZipFile = ProxyZipVFS_Helper.RegisteredZips[kv.Key];
            ZipEntry zipEntry = vZipFile.GetEntry(zipPath);
            if (zipEntry != null && !zipEntry.IsDirectory)
              return !zipEntry.IsDirectory;
          }
          catch { }
        }
      }
      catch { }
      return false;
    }


    public override string GetFileHash(string virtualPath, System.Collections.IEnumerable virtualPathDependencies)
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


    public override System.Web.Caching.CacheDependency GetCacheDependency(String virtualPath, System.Collections.IEnumerable virtualPathDependencies, DateTime utcStart)
    {
      try
      {
        foreach (var kv in ProxyZipVFS_Helper.zipFileRegexPattern.Where(r => r.Value.IsMatch(virtualPath)))
        {
          try
          {
            string zipPath = VirtualPathUtility.RemoveTrailingSlash(kv.Value.Match(virtualPath).Groups[1].Value);
            ZipFile vZipFile = ProxyZipVFS_Helper.RegisteredZips[kv.Key];
            ZipEntry zipEntry = vZipFile.GetEntry(zipPath);
            if (zipEntry != null && !zipEntry.IsDirectory)
            {
              return new CacheDependency(ProxyZipVFS_Helper.RegisteredZipFileNameDependancies[kv.Key]);
            }
          }
          catch { }
        }
      }
      catch { }
      return null;
    }


    public override VirtualFile GetFile(string virtualPath)
    {
      try
      {
        foreach (var kv in ProxyZipVFS_Helper.zipFileRegexPattern.Where(r => r.Value.IsMatch(virtualPath)))
        {
          try
          {
            string zipPath = VirtualPathUtility.RemoveTrailingSlash(kv.Value.Match(virtualPath).Groups[1].Value);
            //
            try
            {
              string fName = VirtualPathUtility.Combine(ProxyZipVFS_Helper.zipFileOverridePath[kv.Key], zipPath);
              if (base.FileExists(fName))
                return base.GetFile(fName);
            }
            catch { }
            //
            ZipFile vZipFile = ProxyZipVFS_Helper.RegisteredZips[kv.Key];
            ZipEntry zipEntry = vZipFile.GetEntry(zipPath);
            if (zipEntry != null && !zipEntry.IsDirectory)
              return new ZipVirtualFile(virtualPath, vZipFile, zipEntry);
          }
          catch { }
        }
      }
      catch { }
      return base.GetFile(virtualPath);
    }


    //
    // TODO: motodo non implementato
    //
    public override VirtualDirectory GetDirectory(string virtualPath)
    {
      try
      {
        foreach (var kv in ProxyZipVFS_Helper.zipFileRegexPattern.Where(r => r.Value.IsMatch(virtualPath)))
        {
          try
          {
            string zipPath = VirtualPathUtility.RemoveTrailingSlash(kv.Value.Match(virtualPath).Groups[1].Value);
            ZipFile vZipFile = ProxyZipVFS_Helper.RegisteredZips[kv.Key];
            ZipEntry zipEntry = vZipFile.GetEntry(zipPath);
            if (zipEntry != null && zipEntry.IsDirectory)
            {
              //return new ZipVirtualDirectory(virtualPath, vZipFile, zipEntry);
            }
          }
          catch { }
        }
      }
      catch { }
      return base.GetDirectory(virtualPath);
    }


    private class ZipVirtualDirectory : VirtualDirectory
    {
      public ZipFile zipFile { get; private set; }
      public ZipEntry zipEntry { get; private set; }


      public ZipVirtualDirectory(string virtualPath, ZipFile zipFile, ZipEntry zipEntry)
        : base(virtualPath)
      {
        this.zipFile = zipFile;
        this.zipEntry = zipEntry;
      }


      public override IEnumerable Children { get { return PerformEnumeration(true, true); } }

      public override IEnumerable Directories { get { return PerformEnumeration(false, true); } }

      public override IEnumerable Files { get { return PerformEnumeration(true, false); } }


      public IEnumerable PerformEnumeration(bool files, bool dirs)
      {
        //TODO: implementare
        return null;
      }

    }


    private class ZipVirtualFile : VirtualFile
    {
      public ZipFile zipFile { get; private set; }
      public ZipEntry zipEntry { get; private set; }


      public ZipVirtualFile(string virtualPath, ZipFile zipFile, ZipEntry zipEntry)
        : base(virtualPath)
      {
        this.zipFile = zipFile;
        this.zipEntry = zipEntry;
      }


      public override System.IO.Stream Open()
      {
        using (Stream st = zipFile.GetInputStream(zipEntry))
        {
          MemoryStream ms = new MemoryStream();
          ms.SetLength(zipEntry.Size);
          byte[] buf = new byte[2048];
          while (true)
          {
            int r = st.Read(buf, 0, buf.Length);
            if (r == 0)
              break;
            ms.Write(buf, 0, r);
          }
          ms.Seek(0, SeekOrigin.Begin);
          return ms;
        }
      }

    }  // ZipVirtualFile


  }  // ZipFileVirtualPathProviderV3


}
