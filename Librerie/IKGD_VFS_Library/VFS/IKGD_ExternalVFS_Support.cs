/*
 * 
 * Copyright (C) 2011 Ikon Srl
 * Tutti i Diritti Riservati. All Rights Reserved
 * 
 */

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Security.Principal;

using Ikon.Auth;


namespace Ikon.GD
{
  //
  // Gestione file su File System
  //
  public class IKGD_ExternalVFS_Support : IDisposable
  {
    public static readonly Regex MimePrefixExternalRx = new Regex(@"^(Ext:|ExtNV:)", RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.Singleline);
    public static readonly Regex MimePrefixExternalRxExtract = new Regex(@"^(?<ext>Ext|ExtNV*):", RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.Singleline);
    public static readonly Regex MimePrefixExternalRxCheck = new Regex(@"^(Ext|ExtNV)$", RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.Singleline);
    public static readonly string MimePrefixExternalNoVersioning = "Ext";
    public static readonly string MimePrefixExternalWithVersioning = "ExtNV";


    public string RepositoryPath { get; protected set; }

    public IKGD_ExternalVFS_Support()
      : this(null)
    { }

    public IKGD_ExternalVFS_Support(string customBasePath)
    {
      RepositoryPath = customBasePath ?? IKGD_Config.AppSettings["SharePath_ExternalVFS"] ?? "~/App_Data/ExternalVFS";
      if (RepositoryPath.StartsWith("~") || RepositoryPath.StartsWith("/"))
        RepositoryPath = Utility.vPathMap(RepositoryPath);
      else if (RepositoryPath.StartsWith(@"..\"))
        RepositoryPath = Path.GetFullPath(Path.Combine(Utility.vPathMap("~/"), RepositoryPath));
      //
      // impersonation
      ImpersonationSupport();
      //
    }


    //
    // IDisposable interface implementation: START
    //
    private bool disposed;
    ~IKGD_ExternalVFS_Support()
    {
      this.Dispose(false);
    }

    public void Dispose()
    {
      if (!this.disposed)
      {
        this.Dispose(true);
        this.disposed = true;
        GC.SuppressFinalize(this);
      }
    }

    protected virtual void Dispose(bool disposing)
    {
      if (disposing)
      {
        // clean up managed resources
        if (impersonationContext != null)
        {
          impersonationContext.Undo();
          impersonationContext.Dispose();
        }
      }
      // clean up unmanaged resources
    }
    //
    // IDisposable interface implementation: END
    //


    public bool IsActive()
    {
      try { return (!string.IsNullOrEmpty(RepositoryPath) && Directory.Exists(RepositoryPath)); }
      catch { }
      return false;
    }


    public static string GetMimeType(string savedMime)
    {
      try { return MimePrefixExternalRx.Replace(savedMime ?? string.Empty, string.Empty); }
      catch { return savedMime; }
    }


    public static string GetExternalModeFromMime(string savedMime)
    {
      string ext = null;
      try { ext = MimePrefixExternalRxExtract.Match(savedMime ?? string.Empty).Groups["ext"].Value.NullIfEmpty(); }
      catch { }
      return ext;
    }


    public static bool IsExternalFileFromMime(string savedMime)
    {
      try { return MimePrefixExternalRx.IsMatch(savedMime ?? string.Empty); }
      catch { }
      return false;
    }


    public static bool IsValidFileName(string filename)
    {
      if (!string.IsNullOrEmpty(filename) && filename.IndexOfAny(Path.GetInvalidFileNameChars()) < 0 && filename.IndexOfAny(Path.GetInvalidPathChars()) < 0)
        return true;
      return false;
    }


    public string ResolveFileName(string filename)
    {
      string file_path = null;
      if (IsValidFileName(filename))
      {
        file_path = Path.GetFullPath(Path.Combine(RepositoryPath, filename));
      }
      return file_path;
    }


    public bool FileExists(string filename)
    {
      try
      {
        if (IsValidFileName(filename))
        {
          string file_path = Path.GetFullPath(Path.Combine(RepositoryPath, filename));
          return File.Exists(file_path);
        }
      }
      catch { }
      return false;
    }


    public bool UpdateExternalFile(Stream inputStream, string ExtMode, IKGD_VNODE vNode, IKGD_STREAM uploadStream, string fileNameOrig)
    {
      try
      {
        int rNode = 0;
        // vogliamo essere sicuri che vNode sia bindato
        if (vNode == null)
          return false;
        if (vNode.rnode != 0)
          rNode = vNode.rnode;
        if (rNode == 0 && vNode.IKGD_RNODE != null && vNode.IKGD_RNODE.code != 0)
          rNode = vNode.IKGD_RNODE.code;
        if (rNode == 0)
          return false;
        if (!MimePrefixExternalRxCheck.IsMatch(ExtMode ?? string.Empty))
          return false;
        if (uploadStream == null)
          return false;
        //
        try { fileNameOrig = Utility.PathGetFileNameSanitized(fileNameOrig ?? string.Empty); }
        catch { }
        string fileExt = null;
        try { fileExt = Utility.PathGetExtensionSanitized(fileNameOrig ?? string.Empty); }
        catch { }
        //
        string fileName = null;
        string deleteMask = null;
        //
        if (ExtMode == MimePrefixExternalWithVersioning)
        {
          fileName = string.Format("{0}_{1}.{3}{2}", rNode, uploadStream.source, fileExt, DateTime.Now.ToBinary());
        }
        else if (ExtMode == MimePrefixExternalNoVersioning)
        {
          fileName = string.Format("{0}_{1}{2}", rNode, uploadStream.source, fileExt);
          deleteMask = string.Format("{0}_{1}.*", rNode, uploadStream.source);
        }
        if (string.IsNullOrEmpty(fileName))
          return false;
        //
        DirectoryInfo storageDir = new DirectoryInfo(RepositoryPath);
        if (storageDir == null)
          return false;
        //
        if (!string.IsNullOrEmpty(deleteMask))
        {
          try
          {
            foreach (FileInfo fi in storageDir.GetFiles(deleteMask, SearchOption.TopDirectoryOnly))
            {
              //fi.DeleteManaged();
              try { fi.Delete(); }
              catch (Exception ex) { Elmah.ErrorSignal.FromCurrentContext().Raise(ex); }
            }
          }
          catch { }
        }
        //
        try
        {
          string fullName = Path.GetFullPath(Path.Combine(RepositoryPath, fileName));
          Byte[] buffer = new Byte[10240];
          using (FileStream outputStream = new FileStream(fullName, FileMode.Create, FileAccess.Write, FileShare.ReadWrite))
          {
            inputStream.Seek(0, SeekOrigin.Begin);
            outputStream.Seek(0, SeekOrigin.Begin);
            for (int count = 0; (count = inputStream.Read(buffer, 0, buffer.Length)) > 0; )
            {
              outputStream.Write(buffer, 0, count);
            }
            outputStream.Flush();
            outputStream.Close();
          }
        }
        catch (Exception ex)
        {
          Elmah.ErrorSignal.FromCurrentContext().Raise(ex);
          return false;
        }
        //
        uploadStream.dataAsString = fileName;
        uploadStream.type = string.Format("{0}:{1}", ExtMode, GetMimeType(uploadStream.type));
        //
        return true;
      }
      catch { }
      return false;
    }


    public bool DupExternalFile(string ExtMode, IKGD_XNODE xNodeNew, IKGD_STREAM streamNew)
    {
      try
      {
        if (xNodeNew == null || xNodeNew.rnode == 0)
          return false;
        if (!MimePrefixExternalRxCheck.IsMatch(ExtMode ?? string.Empty))
          return false;
        if (streamNew == null)
          return false;
        //
        string fileNameOrig = null;
        try { fileNameOrig = Utility.PathGetFileNameSanitized(streamNew.dataAsString ?? string.Empty); }
        catch { }
        string fileExt = null;
        try { fileExt = Utility.PathGetExtensionSanitized(fileNameOrig ?? string.Empty); }
        catch { }
        string fileNameNew = null;
        //
        if (ExtMode == MimePrefixExternalWithVersioning)
        {
          fileNameNew = string.Format("{0}_{1}.{3}{2}", xNodeNew.rnode, streamNew.source, fileExt, DateTime.Now.ToBinary());
        }
        else if (ExtMode == MimePrefixExternalNoVersioning)
        {
          fileNameNew = string.Format("{0}_{1}{2}", xNodeNew.rnode, streamNew.source, fileExt);
        }
        if (string.IsNullOrEmpty(fileNameNew))
          return false;
        //
        DirectoryInfo storageDir = new DirectoryInfo(RepositoryPath);
        if (storageDir == null)
          return false;
        //
        try
        {
          string fullNameOrig = Path.GetFullPath(Path.Combine(RepositoryPath, fileNameOrig));
          string fullNameNew = Path.GetFullPath(Path.Combine(RepositoryPath, fileNameNew));
          Byte[] buffer = new Byte[1024 * 1024];
          using (FileStream inputStream = new FileStream(fullNameOrig, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
          {
            inputStream.Seek(0, SeekOrigin.Begin);
            using (FileStream outputStream = new FileStream(fullNameNew, FileMode.Create, FileAccess.Write, FileShare.ReadWrite))
            {
              outputStream.Seek(0, SeekOrigin.Begin);
              for (int count = 0; (count = inputStream.Read(buffer, 0, buffer.Length)) > 0; )
              {
                outputStream.Write(buffer, 0, count);
              }
              outputStream.Flush();
              outputStream.Close();
            }
            inputStream.Close();
          }
          buffer = null;
        }
        catch (Exception ex)
        {
          Elmah.ErrorSignal.FromCurrentContext().Raise(ex);
          return false;
        }
        //
        streamNew.dataAsString = fileNameNew;
        streamNew.type = string.Format("{0}:{1}", ExtMode, GetMimeType(streamNew.type));
        //
        return true;
      }
      catch { }
      return false;
    }


    public bool DownloadExternalStream(System.Web.HttpResponse response, byte[] data)
    {
      if (data != null)
      {
        try { return DownloadExternalStream(response, Utility.LinqBinaryGetStringDB(data)); }
        catch { }
      }
      return false;
    }

    public bool DownloadExternalStream(System.Web.HttpResponse response, string fileName)
    {
      if (fileName.IsNullOrWhiteSpace() || response == null || !response.IsClientConnected)
        return false;
      try
      {
        string filePath = ResolveFileName(fileName);
        if (!string.IsNullOrEmpty(filePath))
        {
          if (response.IsClientConnected)
          {
            Byte[] buffer = new Byte[8192];
            using (FileStream inputStream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            {
              response.AppendHeader("Content-Length", inputStream.Length.ToString());
              inputStream.Seek(0, SeekOrigin.Begin);
              for (int count = 0; (count = inputStream.Read(buffer, 0, buffer.Length)) > 0; )
              {
                response.OutputStream.Write(buffer, 0, count);
                //response.BinaryWrite(buffer);  // attenzione che attacca extra bytes alla fine dell'ultimo chunk
              }
              inputStream.Close();
            }
          }
        }
        return true;
      }
      catch { }
      return false;
    }


    public static long GetSiteStorageUsedSpace()
    {
      long sizeTotal = 0;
      try
      {
        DirectoryInfo di = new DirectoryInfo(Utility.vPathMap("~/"));
        if (di != null)
        {
          FileInfo[] files = di.GetFiles("*", SearchOption.AllDirectories);
          foreach (FileInfo file in files)
            sizeTotal += file.Length;
        }
      }
      catch { }
      return sizeTotal;
    }


    public static long GetExternalStorageUsedSpace()
    {
      long sizeTotal = 0;
      try
      {
        using (IKGD_ExternalVFS_Support extFS = new IKGD_ExternalVFS_Support())
        {
          DirectoryInfo di = new DirectoryInfo(extFS.RepositoryPath);
          if (di != null)
          {
            FileInfo[] files = di.GetFiles("*", SearchOption.AllDirectories);
            foreach (FileInfo file in files)
              sizeTotal += file.Length;
          }
        }
      }
      catch { }
      return sizeTotal;
    }


    public List<string> ClearUnmappedExternalResources(bool clearExtFs, bool clearVFS)
    {
      List<string> messages = new List<string>();
      try
      {
        DirectoryInfo di = new DirectoryInfo(RepositoryPath);
        var filesExtOnFS = di.GetFiles("*", SearchOption.TopDirectoryOnly).ToList();
        //
        using (FS_Operations fsOp = new FS_Operations())
        {
          var filesExt1 = fsOp.DB.IKGD_STREAMs.Where(r => r.type != null && r.type.StartsWith(MimePrefixExternalNoVersioning)).AsEnumerable().Select(r => new { id = r.id, fname = r.dataAsString }).Distinct().ToList();
          var filesExt2 = fsOp.DB.IKGD_STREAMs.Where(r => r.type != null && r.type.StartsWith(MimePrefixExternalWithVersioning)).AsEnumerable().Select(r => new { id = r.id, fname = r.dataAsString }).Distinct().ToList();
          var filesExtOnDB = filesExt1.Concat(filesExt2).ToList();
          //
          var FilesOnFsToDelete = filesExtOnFS.Where(f => !filesExtOnDB.Any(r => string.Equals(r.fname, f.Name, StringComparison.OrdinalIgnoreCase))).ToList();
          var FilesOnDBToDelete = filesExtOnDB.Where(r => !filesExtOnFS.Any(f => string.Equals(r.fname, f.Name, StringComparison.OrdinalIgnoreCase))).ToList();
          //
          if (clearVFS)
          {
            foreach (var chunk in FilesOnDBToDelete.Select(r => r.id).Distinct().Slice(100))
            {
              fsOp.DB.IKGD_STREAMs.DeleteAllOnSubmit(fsOp.DB.IKGD_STREAMs.Where(r => chunk.Contains(r.id)));
            }
            var chg = fsOp.DB.GetChangeSet();
            messages.Add(string.Format("Removed {0} streams from DB pointing to missing external resources.", chg.Deletes.Count));
            fsOp.DB.SubmitChanges();
          }
          //
          if (clearExtFs)
          {
            foreach (var file in FilesOnFsToDelete)
            {
              try
              {
                messages.Add(string.Format("Deleting: {0}", file.FullName));
                file.Delete();
              }
              catch (Exception ex) { messages.Add(ex.Message); }
            }
            messages.Add(string.Format("Removed {0} external resources without mapping on DB streams.", FilesOnFsToDelete.Count));
          }
          //
        }
      }
      catch (Exception ex)
      {
        messages.Add(ex.Message);
      }
      return messages;
    }



    //
    // impersonation support stuff (Lucene code)
    //
    protected WindowsImpersonationContext impersonationContext { get; set; }
    public bool UsingImpersonation { get; protected set; }
    protected bool ImpersonationSupport()
    {
      impersonationContext = null;
      UsingImpersonation = false;
      if (!RepositoryPath.StartsWith(@"\\"))
        return UsingImpersonation;
      //
      string domainOrServer = IKGD_Config.AppSettings["ShareDomain"] ?? IKGD_Config.AppSettings["ShareServer"];
      string user = IKGD_Config.AppSettings["ShareUserName"];
      string pass = IKGD_Config.AppSettings["SharePassword"];
      if (string.IsNullOrEmpty(domainOrServer) || string.IsNullOrEmpty(user) || string.IsNullOrEmpty(pass))
        return UsingImpersonation;
      //
      impersonationContext = NetworkSecurity.ImpersonateUserCached(domainOrServer, user, pass, LogonType.LOGON32_LOGON_NEW_CREDENTIALS, LogonProvider.LOGON32_PROVIDER_DEFAULT);
      UsingImpersonation = true;
      //
      return UsingImpersonation;
    }
  }
}
