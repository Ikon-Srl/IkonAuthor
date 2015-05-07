using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Security;
using System.Web.SessionState;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.IO;
using System.Web.Compilation;
using LinqKit;

using Ikon;
using Ikon.GD;



namespace Ikon.Support
{

  public static class IKGD_AssemblyManagerHandler
  {
    public static Dictionary<string, Assembly> dynamicAssemblies { get; private set; }

    static IKGD_AssemblyManagerHandler()
    {
      dynamicAssemblies = new Dictionary<string, Assembly>(StringComparer.OrdinalIgnoreCase);
    }


    public static Assembly AssemblyResolveHandler(object sender, ResolveEventArgs args)
    {
      Assembly asmOut = null;
      try
      {
        string ShortName = Utility.Explode(args.Name, ",", " ", true).FirstOrDefault();
        //
        if (dynamicAssemblies.ContainsKey(ShortName))
          return dynamicAssemblies[ShortName];
        //
        //var test01 = AppDomain.CurrentDomain.GetAssemblies().ToLookup(a => Utility.Explode(a.FullName, ",", " ", true).FirstOrDefault(), StringComparer.OrdinalIgnoreCase).Select(r => string.Format("[{1}] -> {0}", r.Key, r.Count())).OrderByDescending(r => r).ToList();
        // questa versione genera eccezioni nel caso di assembly duplicati
        Dictionary<string, Assembly> asmDict = BuildManager.GetReferencedAssemblies().OfType<Assembly>().Concat(AppDomain.CurrentDomain.GetAssemblies()).Distinct().OrderBy(o => o.FullName).ToLookup(a => Utility.Explode(a.FullName, ",", " ", true).FirstOrDefault(), StringComparer.OrdinalIgnoreCase).ToDictionary(r => r.Key, r => r.LastOrDefault(), StringComparer.OrdinalIgnoreCase);
        //Dictionary<string, Assembly> asmDict = AppDomain.CurrentDomain.GetAssemblies().ToLookup(a => Utility.Explode(a.FullName, ",", " ", true).FirstOrDefault(), StringComparer.OrdinalIgnoreCase).ToDictionary(r=>r.Key, r=>r.LastOrDefault(), StringComparer.OrdinalIgnoreCase);
        if (asmDict.ContainsKey(ShortName))
          return asmDict[ShortName];
        //
        using (IKGD_DataContext DB = IKGD_DBH.GetDB())
        {
          var asmGrp = DB.IKGD_ASSEMBLies.GroupBy(a => a.AssembliesMain).Where(g => g.Any(a => a.Name.ToLower() == ShortName.ToLower())).FirstOrDefault();
          foreach (IKGD_ASSEMBLY asmDB in asmGrp)
          {
            Assembly asm = Assembly.Load(asmDB.AssemblyStream.ToArray());
            Utility.ClearCachedApplicationReferencedAssemblies();
            dynamicAssemblies[asmDB.Name] = asm;
            if (ShortName.Equals(asmDB.Name, StringComparison.OrdinalIgnoreCase))
              asmOut = asm;
          }
        }
      }
      catch { }
      return asmOut;
    }

  }




  public class IKGD_AssemblyManagerHelper
  {

    public void IKGD_AssemblySet_ClearDB()
    {
      using (IKGD_DataContext DB = IKGD_DBH.GetDB(true))
      {
        DB.IKGD_ASSEMBLies.DeleteAllOnSubmit(DB.IKGD_ASSEMBLies);
        var chg = DB.GetChangeSet();
        DB.SubmitChanges();
      }
    }


    public void IKGD_AssemblySet_DB2Folder(bool clearFiles)
    {
      List<FileInfo> files = IKGD_AssemblySet_GetFiles();
      if (clearFiles)
        files.ForEach(f => File.WriteAllText(f.FullName, string.Empty));
      DirectoryInfo diBase = IKGD_AssemblySet_BaseDir();
      using (IKGD_DataContext DB = IKGD_DBH.GetDB(true))
      {
        foreach (IKGD_ASSEMBLY asmDB in DB.IKGD_ASSEMBLies)
        {
          DirectoryInfo di = new DirectoryInfo(Path.Combine(diBase.FullName, asmDB.AssembliesMain));
          if (!di.Exists)
            di.Create();
          string fName = Path.Combine(di.FullName, asmDB.Name + ".dll");
          File.WriteAllBytes(fName, asmDB.AssemblyStream.ToArray());
        }
      }
    }


    public void IKGD_AssemblySet_Folder2DB(bool clearDB)
    {
      using (IKGD_DataContext DB = IKGD_DBH.GetDB(true))
      {
        if (clearDB)
        {
          DB.IKGD_ASSEMBLies.DeleteAllOnSubmit(DB.IKGD_ASSEMBLies);
          var chg = DB.GetChangeSet();
          DB.SubmitChanges();
        }
        List<FileInfo> files = IKGD_AssemblySet_GetFiles();
        foreach (FileInfo file in files)
        {
          byte[] data = null;
          using (FileStream fstreamIn = new FileStream(file.FullName, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
          {
            data = new byte[(int)fstreamIn.Length];
            int len = fstreamIn.Read(data, 0, data.Length);
            if (data.Length == 0 || data.Length != len)
              continue;
          }
          Assembly asm = null;
          if (asm == null)
          {
            try { asm = Assembly.ReflectionOnlyLoad(data); }
            catch { }
          }
          if (asm == null)
          {
            try { asm = AppDomain.CurrentDomain.GetAssemblies().FirstOrDefault(a => file.Name.Equals(a.ManifestModule.Name, StringComparison.OrdinalIgnoreCase)); }
            catch { }
          }
          //
          string ShortName = Utility.Explode(asm.FullName, ",", " ", true).FirstOrDefault();
          IKGD_ASSEMBLY asmDB = DB.IKGD_ASSEMBLies.FirstOrDefault(a => a.Name.ToLower() == ShortName.ToLower());
          if (asmDB == null)
          {
            asmDB = new IKGD_ASSEMBLY { Enabled = true, modif = DateTime.Now };
            DB.IKGD_ASSEMBLies.InsertOnSubmit(asmDB);
          }
          //
          int hash = Convert.ToBase64String(new System.Security.Cryptography.MD5CryptoServiceProvider().ComputeHash(data)).GetHashCode();
          if (asmDB.Hash != hash)
          {
            PortableExecutableKinds pek01;
            ImageFileMachine imgfm;
            asm.GetModules().FirstOrDefault().GetPEKind(out pek01, out imgfm);
            asmDB.AssemblyStream = data;  // new System.Data.Linq.Binary(data);
            asmDB.Hash = hash;
            asmDB.modif = DateTime.Now;
            asmDB.Arch = (pek01 == PortableExecutableKinds.ILOnly) ? null : imgfm.ToString();
            asmDB.Name = ShortName;
            asmDB.FullName = asm.FullName;
            asmDB.AssembliesMain = file.Directory.Name;
            //
            var chg = DB.GetChangeSet();
            DB.SubmitChanges();
          }
        }
      }
    }


    public void IKGD_AssemblySet_Running2Folder()
    {
      var assembliesAll = AppDomain.CurrentDomain.GetAssemblies().Where(a => { try { return a.Location != null; } catch { return false; } }).ToList();
      var assembliesDump = assembliesAll.Where(a => a.GetCustomAttributes(typeof(IKGD_Assembly_EmbeddableAttribute), false).Any()).Select(a => new TupleW<string, Assembly>(a.GetCustomAttributes(typeof(IKGD_Assembly_EmbeddableAttribute), false).OfType<IKGD_Assembly_EmbeddableAttribute>().FirstOrDefault().AssemblyGroup, a)).ToList();
      var filterGroups = assembliesAll.Where(a => a.GetCustomAttributes(typeof(IKGD_Assembly_EmbeddableDependancyAttribute), false).Any()).SelectMany(a => a.GetCustomAttributes(typeof(IKGD_Assembly_EmbeddableDependancyAttribute), false).OfType<IKGD_Assembly_EmbeddableDependancyAttribute>().Select(aa => new TupleW<string, Regex>(aa.AssemblyGroup, aa.AssemblySelector))).Distinct().ToList();
      //
      // normalizzazione delle tuples in modo che non ci siano doppioni negli assemby o nelle regex mappati su gruppi diversi
      assembliesDump = assembliesDump.GroupBy(t => t.Item2).Select(g => g.OrderBy(t => t.Item1).FirstOrDefault()).ToList();
      filterGroups = filterGroups.GroupBy(t => t.Item2).Select(g => g.OrderBy(t => t.Item1).FirstOrDefault()).ToList();
      var assembliesDep =
        (from asm in assembliesAll
         from dep in filterGroups.Where(f => f.Item2 != null)
         let name = asm.FullName.Split(',').FirstOrDefault().Trim()
         where dep.Item2.IsMatch(name)
         select new TupleW<string, Assembly>(dep.Item1, asm)).ToList();
      assembliesDep = assembliesDep.GroupBy(t => t.Item2).Select(g => g.OrderBy(t => t.Item1).FirstOrDefault()).ToList();
      var assembliesToWrite = assembliesDump.Union(assembliesDep).GroupBy(t => t.Item2).Select(g => g.OrderBy(t => t.Item1).FirstOrDefault()).ToList();
      //
      DirectoryInfo di = IKGD_AssemblySet_BaseDir();
      assembliesToWrite.Select(t => t.Item1).Distinct().ForEach(d =>
      {
        if (!Directory.Exists(Path.Combine(di.FullName, d)))
          Directory.CreateDirectory(Path.Combine(di.FullName, d));
      });
      //
      foreach (var item in assembliesToWrite)
      {
        FileInfo file = new FileInfo(Path.Combine(Path.Combine(di.FullName, item.Item1), item.Item2.ManifestModule.Name));
        using (FileStream fstreamOut = file.OpenWrite())
        {
          byte[] data = null;
          using (FileStream fstreamIn = new FileStream(item.Item2.Location, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
          {
            data = new byte[(int)fstreamIn.Length];
            int len = fstreamIn.Read(data, 0, data.Length);
          }
          fstreamOut.Write(data, 0, data.Length);
        }
      }
      //
    }


    public DirectoryInfo IKGD_AssemblySet_BaseDir()
    {
      string folderName = IKGD_Config.AppSettingsWeb["AssemblyResolveOnDB_DumpFolder"] ?? "~/App_Data/DLLs";
      if (folderName.StartsWith("~/"))
        folderName = Utility.vPathMap(folderName);
      DirectoryInfo di = new DirectoryInfo(folderName);
      return di;
    }


    public List<FileInfo> IKGD_AssemblySet_GetFiles()
    {
      return IKGD_AssemblySet_BaseDir().GetFiles("*.dll", SearchOption.AllDirectories).ToList();
    }


    //public static void IKGD_RegisterAssemblySet(string MainAssembly, IEnumerable<string> regexParams, IEnumerable<string> dllFilesParams, bool forceUpdate)
    //{
    //  if (string.IsNullOrEmpty(MainAssembly))
    //    return;
    //  //
    //  List<Regex> filters = regexParams.Select(r => new Regex(r, RegexOptions.Compiled | RegexOptions.IgnoreCase)).ToList();
    //  List<Assembly> asmToStore = new List<Assembly>();
    //  foreach (Assembly asm in AppDomain.CurrentDomain.GetAssemblies())
    //  {
    //    var match = filters.FirstOrDefault(f => f.IsMatch(asm.FullName));
    //    if (match == null)
    //      continue;
    //    asmToStore.Add(asm);
    //  }
    //  //
    //  // caricamento manuale di pdftotext.dll a 64bit
    //  //
    //  if (dllFilesParams != null)
    //  {
    //    foreach (string file in dllFilesParams)
    //    {
    //      try { asmToStore.Add(Assembly.ReflectionOnlyLoadFrom(file)); }
    //      catch { }
    //    }
    //  }
    //  //
    //  Dictionary<string, Assembly> asmDict = asmToStore.ToDictionary(a => Utility.Explode(a.FullName, ",", " ", true).FirstOrDefault());
    //  using (IKGD_DataContext DB = IKGD_DBH.GetDB(true))
    //  {
    //    foreach (string name in asmDict.Keys.Except(DB.IKGD_ASSEMBLies.Select(a => a.Name)))
    //    {
    //      Assembly asm = asmDict[name];
    //      DB.IKGD_ASSEMBLies.InsertOnSubmit(new IKGD_ASSEMBLY { FullName = asm.FullName, Name = name, modif = DateTime.Now, Enabled = true, AssembliesMain = string.Empty, Hash = 0 });
    //    }
    //    var chg01 = DB.GetChangeSet();
    //    DB.SubmitChanges();
    //    //
    //    foreach (KeyValuePair<string, Assembly> kv in asmDict)
    //    {
    //      Assembly asm = kv.Value;
    //      byte[] data = null;
    //      using (FileStream fstream = new FileStream(asm.Location, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
    //      {
    //        data = new byte[(int)fstream.Length];
    //        int len = fstream.Read(data, 0, data.Length);
    //      }
    //      int hash = Convert.ToBase64String(new System.Security.Cryptography.MD5CryptoServiceProvider().ComputeHash(data)).GetHashCode();
    //      IKGD_ASSEMBLY asmDB = DB.IKGD_ASSEMBLies.FirstOrDefault(a => a.FullName == asm.FullName) ?? DB.IKGD_ASSEMBLies.FirstOrDefault(a => a.Name == kv.Key);
    //      if (asmDB != null && (asmDB.Hash != hash || forceUpdate))
    //      {
    //        PortableExecutableKinds pek01;
    //        ImageFileMachine imgfm;
    //        asm.GetModules().FirstOrDefault().GetPEKind(out pek01, out imgfm);
    //        asmDB.AssemblyStream = data;  // new System.Data.Linq.Binary(data);
    //        asmDB.Hash = hash;
    //        asmDB.modif = DateTime.Now;
    //        asmDB.Arch = (pek01 == PortableExecutableKinds.ILOnly) ? null : imgfm.ToString();
    //        asmDB.Name = kv.Key;
    //        asmDB.FullName = asm.FullName;
    //        DB.SubmitChanges();
    //      }
    //    }
    //    //
    //    // generazione dei mapping tra DLL
    //    //
    //    try
    //    {
    //      MainAssembly = asmDict.FirstOrDefault(r => r.Key.Equals(MainAssembly, StringComparison.OrdinalIgnoreCase)).Key;
    //      Assembly asm = asmDict[MainAssembly];
    //      DB.IKGD_ASSEMBLies.Where(a => a.AssembliesMain.Contains(MainAssembly)).ToList().ForEach(a => a.AssembliesMain = Utility.Implode(Utility.Explode(a.AssembliesMain, ",", "' ", true).Except(new string[] { MainAssembly }).Distinct(), ",", "'", true));
    //      var chg02 = DB.GetChangeSet();
    //      DB.SubmitChanges();
    //      DB.IKGD_ASSEMBLies.Where(a => asmDict.Keys.Contains(a.Name)).ToList().ForEach(a => a.AssembliesMain = Utility.Implode(Utility.Explode(a.AssembliesMain, ",", "' ", true).Concat(new string[] { MainAssembly }).Distinct(), ",", "'", true));
    //      var chg03 = DB.GetChangeSet();
    //      DB.SubmitChanges();
    //    }
    //    catch { }
    //  }
    //  return;
    //}


  }


}

