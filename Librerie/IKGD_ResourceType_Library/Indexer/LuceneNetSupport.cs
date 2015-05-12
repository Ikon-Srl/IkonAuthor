/*
 * 
 * Intranet Ikon
 * 
 * Copyright (C) 2010 Ikon Srl
 * Tutti i Diritti Riservati. All Rights Reserved
 * 
 */


using System;
using System.Data;
using System.Configuration;
using System.Collections;
using System.Collections.Generic;
using System.Web;
using System.Web.Security;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Security.Principal;
using System.ComponentModel;
using System.Web.Caching;
using System.Linq;
using System.Linq.Expressions;
using System.Xml.Linq;
using System.Transactions;
using LinqKit;


using Ikon.GD;
using Ikon.Filters;
using Ikon.Auth;
using Ikon.IKGD.Library.Resources;



namespace Ikon.Indexer
{
  using Lucene.Net;
  using Lucene.Net.Store;
  using Lucene.Net.Search;
  using Lucene.Net.Analysis;
  using Lucene.Net.Analysis.Standard;
  using Lucene.Net.Index;
  using Lucene.Net.Documents;
  using Lucene.Net.Messages;
  using Lucene.Net.QueryParsers;
  using Lucene.Net.Util;
  using Lucene.Net.Search.Highlight;
  using Ikon.IKCMS;


  //
  // classe di gestione dell'indicizzazione con supporto automatico per l'impersonation nel caso
  // di accesso a share di rete per lo store degli indici
  //
  public class LuceneIndexer : IDisposable
  {
    public string IKGD_IndexDirectoryPath { get; protected set; }
    public FSDirectory IKGD_IndexDir { get; protected set; }
    public IndexSearcher IKGD_IndexSearcher { get; protected set; }
    public IndexReader IKGD_IndexReader { get; protected set; }
    public IndexWriter IKGD_IndexWriter { get; protected set; }
    public Analyzer IKGD_Analizer { get; protected set; }
    public Highlighter IKGD_Highlighter { get; set; }
    public bool IKGD_IndexTainted { get; set; }
    public int MaxResults { get; set; }
    public int MaxResultsCMS { get; set; }
    public int LastMatchesCount { get; set; }


    //
    // IDisposable interface implementation: START
    //
    private bool disposed;
    ~LuceneIndexer()
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
        if (IKGD_IndexSearcher != null)
        {
          IKGD_IndexSearcher.Dispose();
        }
        if (IKGD_IndexWriter != null)
        {
          IKGD_IndexWriter.Commit();
          IKGD_IndexWriter.Dispose();
        }
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


    //
    // constructors
    //
    public LuceneIndexer()
      : this(false)
    {
    }


    public LuceneIndexer(bool openForUpdate)
    {
      IKGD_IndexDirectoryPath = IKGD_Config.AppSettings["SharePath_Lucene"] ?? IKGD_Config.AppSettings["LuceneShareIndexPath"] ?? "~/App_Data/Indexes";
      if (IKGD_IndexDirectoryPath.StartsWith("~") || IKGD_IndexDirectoryPath.StartsWith("/"))
        IKGD_IndexDirectoryPath = Utility.vPathMap(IKGD_IndexDirectoryPath);
      else if (IKGD_IndexDirectoryPath.StartsWith(@"..\"))
        IKGD_IndexDirectoryPath = Path.Combine(Utility.vPathMap("~/"), IKGD_IndexDirectoryPath);
      //
      ImpersonationSupport();
      //
      IKGD_IndexTainted = false;
      IKGD_IndexDir = FSDirectory.Open(new DirectoryInfo(IKGD_IndexDirectoryPath));
      IKGD_Analizer = new StandardAnalyzer(Version.LUCENE_30);
      //IKGD_Analizer = new StandardAnalyzer(Version.LUCENE_CURRENT);
      //
      if (openForUpdate)
      {
        IKGD_IndexWriter = new IndexWriter(IKGD_IndexDir, IKGD_Analizer, IndexWriter.MaxFieldLength.UNLIMITED);
        //IKGD_IndexWriter = new IndexWriter(IKGD_IndexDir, IKGD_Analizer);
        //IKGD_IndexWriter.SetMaxFieldLength(1000000);
        //IKGD_IndexWriter.setMergeFactor(10);
      }
      //
      ReOpenSearcher();
      //
      MaxResults = Utility.TryParse<int>(IKGD_Config.AppSettings["LuceneMaxresults"], 1000);
      MaxResultsCMS = Utility.TryParse<int>(IKGD_Config.AppSettings["LuceneMaxResultsCMS"], MaxResults / 10);
      LastMatchesCount = 0;
      //
    }


    public void ReOpenSearcher()
    {
      try
      {
        if (IKGD_IndexSearcher != null)
        {
          try { IKGD_IndexSearcher.Dispose(); }
          catch { }
        }
        IKGD_IndexSearcher = new IndexSearcher(IKGD_IndexDir, true);
        IKGD_IndexReader = IKGD_IndexSearcher.IndexReader;
      }
      catch { }
    }


    //
    // impersonation support stuff
    //
    protected WindowsImpersonationContext impersonationContext { get; set; }
    public bool UsingImpersonation { get; protected set; }
    protected bool ImpersonationSupport()
    {
      impersonationContext = null;
      UsingImpersonation = false;
      if (!IKGD_IndexDirectoryPath.StartsWith(@"\\"))
        return UsingImpersonation;
      //
      string domainOrServer = IKGD_Config.AppSettings["ShareDomain"] ?? IKGD_Config.AppSettings["ShareServer"] ?? IKGD_Config.AppSettings["LuceneShareServer"];
      string user = IKGD_Config.AppSettings["ShareUserName"] ?? IKGD_Config.AppSettings["LuceneShareUserName"];
      string pass = IKGD_Config.AppSettings["SharePassword"] ?? IKGD_Config.AppSettings["LuceneSharePassword"];
      if (string.IsNullOrEmpty(domainOrServer) || string.IsNullOrEmpty(user) || string.IsNullOrEmpty(pass))
        return UsingImpersonation;
      //
      impersonationContext = NetworkSecurity.ImpersonateUserCached(domainOrServer, user, pass, LogonType.LOGON32_LOGON_NEW_CREDENTIALS, LogonProvider.LOGON32_PROVIDER_DEFAULT);
      UsingImpersonation = true;
      //
      return UsingImpersonation;
    }


    //
    // ottimizzazione degli indici dopo l'update di nuove risorse
    //
    public void IKGD_IndexOptimize(bool force)
    {
      if (IKGD_IndexTainted || force)
      {
        //
        // se si lascia lo scheduler di default l'optimize genera eccezioni quando si esegue il tutto su IIS
        // (su VS2008 e' tutto OK) [problema riscontrato lavorando su share remote, non so se accade anche in locale]
        //
        IKGD_IndexWriter.SetMergeScheduler(new SerialMergeScheduler());
        //
        IKGD_IndexWriter.Commit();
        IKGD_IndexWriter.Optimize(true);
        ReOpenSearcher();
      }
    }


    //
    // helper methods for java interop
    //
    //public static java.util.Date ToJavaDate(DateTime ts)
    //{
    //  java.util.Date dtj = new java.util.Date(ts.Year, ts.Month, ts.Day, ts.Hour, ts.Minute, ts.Second);
    //  return dtj;
    //}


    public int Count()
    {
      try { return IKGD_IndexReader.NumDocs(); }
      catch { }
      return 0;
    }


    //
    // funzioni per ritornare la lista di valori unici per un field
    //
    public List<string> GetDistinctFieldValues(string fieldName, bool ignoreEmpty)
    {
      List<string> lista = new List<string>();
      try
      {
        using (TermEnum termEnum = IKGD_IndexReader.Terms(new Term(fieldName, string.Empty)))
        {
          try
          {
            Term term = null;
            while ((term = termEnum.Term) != null && term.Field == fieldName)
            {
              if (!ignoreEmpty || !string.IsNullOrEmpty(term.Text))
                if (!lista.Contains(term.Text))
                  lista.Add(term.Text);
              //int term_freq = termEnum.DocFreq();
              if (!termEnum.Next())
                break;
            }
          }
          catch { }
        }
      }
      catch { }
      return lista;
    }


    //
    // funzione per ritornare la lista di valori unici per un field
    // sottoposto a criteri di filtro AND:
    // keyValList e' una lista con coppie consecutive campo/valore da usare come filtro
    // per generare la lista di valori unici per il field
    //
    public List<string> GetDistinctFieldValuesFilter(string fieldName, params string[] keyValList)
    {
      List<string> lista = new List<string>();
      try
      {
        List<int> docIdxs = new List<int>();
        for (int ip = 0; ip < keyValList.Length - 1; ip += 2)
        {
          string key = keyValList[ip];
          string val = keyValList[ip + 1];
          List<int> idxs = new List<int>();
          using (TermDocs tds = IKGD_IndexReader.TermDocs(new Term(key, val)))
          {
            while (tds != null && tds.Next())
              idxs.Add(tds.Doc);
          }
          if (ip == 0)
            docIdxs.AddRange(idxs);
          else
            docIdxs = new List<int>(Enumerable.Intersect(docIdxs, idxs));
        }
        foreach (int idx in docIdxs)
        {
          string val = IKGD_IndexReader.Document(idx).GetField(fieldName).StringValue;
          if (!lista.Contains(val))
            lista.Add(val);
        }
        if (keyValList.Length < 2)
          return GetDistinctFieldValues(fieldName, true);
      }
      catch { }
      return lista;
    }


    //
    // aggiunge una singola risorsa all'indice
    //
    public bool IKGD_AddResource(FS_Operations.FS_NodeInfo_Interface fsNode, IKGD_Path pathNode, IEnumerable<IKGD_Path> pathsNode, string Name, string Title, string Text, string language_forced)
    {
      if (IKGD_IndexWriter == null)
        return false;
      try
      {
        //
        bool flag_CMS_isPage = IKCMS_RegisteredTypes.Types_IKCMS_ResourceWithViewer_Interface.Any(t => t.Name == fsNode.vData.manager_type);
        //bool flag_CMS_isPage = IKCMS_RegisteredTypes.Types_IKCMS_PageBase_Interface.Any(t => t.Name == fsNode.vData.manager_type);
        bool flag_CMS_isPageWidget = !flag_CMS_isPage;
        flag_CMS_isPageWidget &= IKCMS_RegisteredTypes.Types_IKCMS_Widget_Interface.Any(t => t.Name == fsNode.vData.manager_type);
        //IKGD_Path_Fragment fragParent = pathNode.Fragments[Math.Max(pathNode.Fragments.Count - 2, 0)];
        //flag_CMS_isPageWidget &= IKCMS_RegisteredTypes.Types_IKCMS_Page_Interface.Any(t => t.Name == fragParent.ManagerType);  // e' troppo restrittiva
        flag_CMS_isPageWidget &= IKCMS_RegisteredTypes.Types_IKCMS_PageBase_Interface.Any(t => pathsNode.Any(p => p.PreLastFragment.ManagerType == t.Name));
        //
        string textStream = Text ?? string.Empty;
        if (Title != null && textStream.IndexOf(Title, StringComparison.OrdinalIgnoreCase) == -1)
          textStream += "\n" + Title;
        //
        //string date_str = DateTools.dateToString(ToJavaDate(fsNode.vData.date_node), DateTools.Resolution.MILLISECOND);
        string date_str = fsNode.vData.date_node.ToString("u");
        //
        Document doc = new Document();
        doc.Add(new Field("guid", Guid.NewGuid().ToString(), Field.Store.YES, Field.Index.NOT_ANALYZED));  // per un piu' facile accesso ai singoli record
        doc.Add(new Field("rNode", fsNode.vData.rnode.ToString(), Field.Store.YES, Field.Index.NOT_ANALYZED));
        doc.Add(new Field("sNode", fsNode.vNode.snode.ToString(), Field.Store.YES, Field.Index.NOT_ANALYZED));
        doc.Add(new Field("folder", flag_CMS_isPage ? fsNode.vNode.rnode.ToString() : fsNode.vNode.folder.ToString(), Field.Store.YES, Field.Index.NOT_ANALYZED));
        doc.Add(new Field("vNodeVersion", fsNode.vNode.version.ToString(), Field.Store.YES, Field.Index.NOT_ANALYZED));
        doc.Add(new Field("vDataVersion", fsNode.vData.version.ToString(), Field.Store.YES, Field.Index.NOT_ANALYZED));
        doc.Add(new Field("iNodeVersion", (fsNode.iNode != null ? fsNode.iNode.version.ToString() : string.Empty), Field.Store.YES, Field.Index.NOT_ANALYZED));
        doc.Add(new Field("area", fsNode.vData.area, Field.Store.YES, Field.Index.NOT_ANALYZED));
        doc.Add(new Field("manager_type", fsNode.vData.manager_type ?? string.Empty, Field.Store.YES, Field.Index.NOT_ANALYZED));
        doc.Add(new Field("category", fsNode.vData.category ?? string.Empty, Field.Store.YES, Field.Index.NOT_ANALYZED));
        doc.Add(new Field("language", language_forced ?? fsNode.Language ?? string.Empty, Field.Store.YES, Field.Index.NOT_ANALYZED));
        doc.Add(new Field("flag_published", fsNode.IsPublished.ToString(), Field.Store.YES, Field.Index.NOT_ANALYZED));
        doc.Add(new Field("flag_current", fsNode.IsCurrent.ToString(), Field.Store.YES, Field.Index.NOT_ANALYZED));
        doc.Add(new Field("flag_unstructured", (fsNode.vData.flag_unstructured || IKCMS_RegisteredTypes.Types_IKCMS_ResourceUnStructured_Interface.Any(t => t.Name == fsNode.vData.manager_type)).ToString(), Field.Store.YES, Field.Index.NOT_ANALYZED));
        doc.Add(new Field("flag_CMS_isPage", flag_CMS_isPage.ToString(), Field.Store.YES, Field.Index.NOT_ANALYZED));
        doc.Add(new Field("flag_CMS_isPageWidget", flag_CMS_isPageWidget.ToString(), Field.Store.YES, Field.Index.NOT_ANALYZED));
        doc.Add(new Field("date", date_str, Field.Store.YES, Field.Index.NOT_ANALYZED));
        //
        doc.Add(new Field("name", Name ?? fsNode.Name, Field.Store.YES, Field.Index.NOT_ANALYZED));
        doc.Add(new Field("title", Title ?? string.Empty, Field.Store.YES, Field.Index.ANALYZED));
        doc.Add(new Field("text", textStream ?? string.Empty, Field.Store.YES, Field.Index.ANALYZED));
        //
        IKGD_IndexWriter.AddDocument(doc);
        //
        return true;
      }
      catch { }
      return false;
    }


    //
    //
    //
    public bool IKGD_UpdateStatus(List<int> rNodes)
    {
      try
      {
        if (IKGD_IndexWriter == null)
          return false;
        foreach (int rNode in rNodes)
        {
          List<Document> docs = new List<Document>();
          //
          using (TermDocs tdsEnum = IKGD_IndexReader.TermDocs(new Term("rNode", rNode.ToString())))
          {
            while (tdsEnum != null && tdsEnum.Next())
              docs.Add(IKGD_IndexReader.Document(tdsEnum.Doc));
          }
          //
          var docsInfo = docs.Select(d => new { doc = d, vData = d.GetField("vDataVersion").StringValue, iNode = d.GetField("iNodeVersion").StringValue }).ToList();
          //
          // TODO: completare
          //
        }

        return true;
      }
      catch { }
      return false;
    }



    //
    // per updateIndex
    // dato version last, trovare la lista di rnodes interessati (compresi quelli eliminati), poi ripulire l'indice da
    // tutti gli rnode trovati, quindi riottenere tutti gli fsNode delle risorse pubblicate e preview per la lista di rnodes
    // e reinserire tutti i documenti corrispondenti (quindi anche la roba piu' vecchia cancellata prima)
    // attenzione ad includere anche Any sui vNode per controllare di non includere nei documenti delle risorse che non
    // abbiano piu' collegamenti attivi sul VFS
    //


    protected string IKGD_GetIndexServerCode()
    {
      string serverCode = IKGD_Config.AppSettings["LuceneApplicationName"];
      if (string.IsNullOrEmpty(serverCode))
        serverCode = string.Format("[{0}]{1}", System.Environment.MachineName, Utility.vPathMap(IKGD_Config.AppSettings["LuceneShareIndexPath"] ?? "~/App_Data/Indexes"));
      return serverCode;
    }


    //
    // pulizia completa degli streams ausiliari di Lucene nel CMS
    //
    public int IKGD_ClearAllStreams()
    {
      int rows = -1;
      try
      {
        using (FS_Operations fsOp = new FS_Operations())
        {
          rows = fsOp.DB.ExecuteCommand("DELETE FROM [IKGD_STREAM] WHERE [source] = 'lucene'");
        }
      }
      catch { }
      return rows;
    }


    //
    // rigenerazione completa dell'indice
    //
    public int IKGD_ReindexAll()
    {
      string serverName = IKGD_GetIndexServerCode();
      int vNodeMax = 0;
      int vDataMax = 0;
      int iNodeMax = 0;
      IKGD_SEARCH searchrec = new IKGD_SEARCH { date_op = DateTime.Now, message = "full rebuild", server = serverName, status = "running", version_vnode = 0, version_vdata = vDataMax, version_inode = iNodeMax };
      using (FS_Operations fsOp = new FS_Operations())
      {
        fsOp.DB.IKGD_SEARCHes.InsertOnSubmit(searchrec);
        fsOp.DB.SubmitChanges();
      }
      int res = IKGD_PartialUpdateWorker(vNodeMax, vDataMax, iNodeMax, out vNodeMax, out vDataMax, out iNodeMax);
      using (FS_Operations fsOp = new FS_Operations())
      {
        fsOp.DB.IKGD_SEARCHes.Attach(searchrec);
        //
        searchrec.version_vnode = vNodeMax;
        searchrec.version_vdata = vDataMax;
        searchrec.version_inode = iNodeMax;
        searchrec.message = string.Format("Full rebuild, resource count: {0}", res);
        searchrec.status = "done";
        //
        fsOp.DB.SubmitChanges();
        //
        var recordsToClear = fsOp.DB.IKGD_SEARCHes.Where(r => r.server == serverName).Where(r => r.status == "running");
        if (recordsToClear != null && recordsToClear.Count() > 0)
        {
          recordsToClear.ForEach(r => r.status = "done");
          fsOp.DB.SubmitChanges();
        }
      }
      return res;
    }


    //
    // update parziale dell'indice
    //
    public int IKGD_IndexUpdate()
    {
      try
      {
        string serverName = IKGD_GetIndexServerCode();
        using (FS_Operations fsOp = new FS_Operations(-1, false, true))
        {
          IKGD_SEARCH searchrec = null;
          using (TransactionScope ts = IKGD_TransactionFactory.TransactionSerializable(300))
          {
            var openRun = fsOp.DB.IKGD_SEARCHes.Where(r => r.server == serverName).Where(r => r.status == "running").OrderByDescending(r => r.id).FirstOrDefault();
            if (openRun != null && (DateTime.Now - openRun.date_op) < TimeSpan.FromSeconds(3600))
              return -100;
            var lastRun = fsOp.DB.IKGD_SEARCHes.Where(r => r.server == serverName).OrderByDescending(r => r.id).FirstOrDefault();
            lastRun = lastRun ?? new IKGD_SEARCH { version_vnode = 0, version_vdata = 0, version_inode = 0 };
            searchrec = new IKGD_SEARCH { date_op = DateTime.Now, message = "index updating", server = string.Empty, status = "running", version_vnode = lastRun.version_vnode, version_vdata = lastRun.version_vdata, version_inode = lastRun.version_inode };
            //
            fsOp.DB.IKGD_SEARCHes.InsertOnSubmit(searchrec);
            fsOp.DB.SubmitChanges();
            //
            ts.Committ();
          }
          int vNodeMax = searchrec.version_vnode.Value;
          int vDataMax = searchrec.version_vdata.Value;
          int iNodeMax = searchrec.version_inode.Value;
          int res = IKGD_PartialUpdateWorker(vNodeMax, vDataMax, iNodeMax, out vNodeMax, out vDataMax, out iNodeMax);
          //
          searchrec.version_vnode = vNodeMax;
          searchrec.version_vdata = vDataMax;
          searchrec.version_inode = iNodeMax;
          searchrec.message = string.Format("Index updated resource count: {0}", res);
          searchrec.status = "done";
          //
          fsOp.DB.SubmitChanges();
          //
          return res;
        }
      }
      catch (Exception ex) { throw ex; }
    }


    private Regex RxInValidKeysKVT = new Regex(@"(^[:space:]*$|^_|_$|streaminfo|^geolat|^geolon|^georadius)", RegexOptions.IgnoreCase | RegexOptions.Singleline);
    private Regex RxInValidValuesKVT = new Regex(@"^([:digit:]|[:punct:]|[:space:])*$", RegexOptions.IgnoreCase);
    //
    // worker per l'estrazione dei testi
    //
    public int IKGD_PartialUpdateWorker(int last_vNode, int last_vData, int last_iNode, out int vNodeMax, out int vDataMax, out int iNodeMax)
    {
      vNodeMax = last_vNode;
      vDataMax = last_vData;
      iNodeMax = last_iNode;
      if (IKGD_IndexWriter == null)
        throw new Exception("IKGD_IndexWriter is Null");
      //
      // controllo se si tratta di un nuovo indice eventualmente derivante da una cancellazione dei files
      // e predispongo per il reset delle info anche sul DB
      //
      int docCount = IKGD_IndexWriter.MaxDoc();
      if (docCount == 0)
      {
        vNodeMax = last_vNode = 0;
        vDataMax = last_vData = 0;
        iNodeMax = last_iNode = 0;
      }
      //
      IKGD_ExternalVFS_Support extFS = null;
      //
      try
      {
        //
        //Dictionary<string, Type> resTypes = IKGD_ResourceTypeBase.FindRegisteredResourceTypes().ToDictionary(t => t.Name);
        Dictionary<string, Type> resTypes = IKCMS_RegisteredTypes.Types_IKCMS_Base_Interface.Where(t =>
        {
          try
          {
            IKGD_ResourceTypeBase resObj = IKGD_ResourceTypeBase.CreateInstance(t);
            return (resObj != null && resObj.IsIndexable);
          }
          catch { return false; }
        }).ToDictionary(t => t.Name, StringComparer.OrdinalIgnoreCase);
        //
        List<string> LanguagesAvailable = IKGD_Language_Provider.Provider.LanguagesAvailable().ToList();
        //
        IKGD_HtmlCleaner xHtmlCleaner = new IKGD_HtmlCleaner();
        //
        var validRootNodes = IKGD_ConfigVFS.Config.RootsVFS_folders.ToList();
        if (!validRootNodes.Any())
          validRootNodes = null;
        int maxBinSize = Utility.TryParse<int>(IKGD_Config.AppSettings["IndexerMaxBinarySize"], int.MaxValue);
        //
        List<int> rNodesProcessed = new List<int>();
        //
        using (FS_Operations fsOp = new FS_Operations(-1, true, true))
        {
          //
          fsOp.DB.CommandTimeout = 3600;
          //
          // NB: sono filtrate le risorse che hanno vData o iNode ma non hanno piu' nessun vNode attivo
          //
          var rNodes01 = fsOp.DB.IKGD_VDATAs.Where(n => n.version > last_vData && n.version_frozen != null).Select(n => n.rnode);  // risorse cancellabili o gia' pubblicate
          var rNodes02 = fsOp.DB.IKGD_INODEs.Where(n => n.version > last_iNode && n.version_frozen != null).Select(n => n.rnode);  // risorse cancellabili o gia' pubblicate
          // risorse in preview e non ancora pubblicate
          var rNodes03 =
            from vNode in fsOp.NodesActive<IKGD_VNODE>(-1, false)
            from vData in fsOp.NodesActive<IKGD_VDATA>(-1, false).Where(n => n.rnode == vNode.rnode).Where(n => !n.flag_inactive).Where(n => n.manager_type != null && n.manager_type != string.Empty)
            from iNode in fsOp.NodesActive<IKGD_INODE>(-1, false).Where(n => n.rnode == vNode.rnode).DefaultIfEmpty()
            where (iNode == null || iNode.version > last_iNode) && (vData.version > last_vData)
            select vNode.rnode;
          var rNodesList = rNodes01.Union(rNodes02).Union(rNodes03).ToList();
          //
          //debugging code
          //rNodesList = rNodesList.Intersect(new int[] { 11788, 11789 }).ToList();
          //
          //
          // pulizia parziale o totale degli indici di Lucene prima del partial/full update
          //
          if (last_vData == 0 && last_iNode == 0)
          {
            //
            // cancellazione dell'indice corrente
            // full clean/update
            //
            IKGD_IndexWriter.DeleteDocuments(new Term("flag_unstructured", true.ToString()));
            IKGD_IndexWriter.DeleteDocuments(new Term("flag_unstructured", false.ToString()));
          }
          else
          {
            //
            // cancellazione parziale dell'indice corrente (solo le risorse che saranno aggiornate)
            // partial clean/update
            //
            foreach (int rNode in rNodesList)
              IKGD_IndexWriter.DeleteDocuments(new Term("rNode", rNode.ToString()));
          }
          //
          // processing del resource set alterato
          //
          if (rNodesList.Any())
          {
            //
            extFS = new IKGD_ExternalVFS_Support();
            //
            using (IKGD_DataContext DB_RW = IKGD_DBH.GetDB(true))
            {
              //
              DB_RW.CommandTimeout = 600;
              //
              int chunkSize = 50;
              for (int idx = 0; idx < rNodesList.Count; idx += chunkSize)
              {
                //
                var rNodeSet = rNodesList.Skip(idx).Take(chunkSize).ToList();
                //
                var nodes_publ2 =
                  (from vData in fsOp.NodesActive<IKGD_VDATA>(0, false).Where(n => !n.flag_inactive).Where(n => n.manager_type != null && n.manager_type != string.Empty).Where(n => rNodeSet.Contains(n.rnode))
                   from iNode in fsOp.NodesActive<IKGD_INODE>(0, false).Where(n => n.rnode == vData.rnode).DefaultIfEmpty()
                   from vNode in fsOp.NodesActive<IKGD_VNODE>(0, false).Where(n => n.rnode == vData.rnode)
                   select new FS_Operations.FS_NodeInfoStreams { vData = vData, iNode = iNode, vNode = vNode, Streams = null }).ToList();
                var nodes_curr2 =
                  (from vData in fsOp.NodesActive<IKGD_VDATA>(-1, false).Where(n => !n.flag_inactive).Where(n => n.manager_type != null && n.manager_type != string.Empty).Where(n => rNodeSet.Contains(n.rnode))
                   from iNode in fsOp.NodesActive<IKGD_INODE>(-1, false).Where(n => n.rnode == vData.rnode).DefaultIfEmpty()
                   from vNode in fsOp.NodesActive<IKGD_VNODE>(-1, false).Where(n => n.rnode == vData.rnode)
                   select new FS_Operations.FS_NodeInfoStreams { vData = vData, iNode = iNode, vNode = vNode, Streams = null }).ToList();
                var nodes_all2 = nodes_publ2.Union(nodes_curr2).Where(n => resTypes.Keys.Contains(n.ManagerType)).Distinct((n1, n2) => n1.VersionVNODE == n2.VersionVNODE && n1.VersionVDATA == n2.VersionVDATA && n1.VersionINODE == n2.VersionINODE).ToList();
                //
                // non usare la ricerca di path ottimizzata perche' non e' particolarmente efficiente per le bulk search
                //
                // sembra che ci siano dei problemi con le risorse nelle cartelle __Upload__
                // se si usa la versione ottimizzata dei path non si entra in queste cartelle e lucene non processa gli allegati
                // per testarlo vedere la intranet autovie, su rigenerazione completa lucene intercettare l'allegato con rNode=5442
                //
                //bool forceNoOptPathSearch = false;
                bool forceNoOptPathSearch = Utility.TryParse<bool>(IKGD_Config.AppSettings["Lucene_UseNoOptPathScan"], false);
                //
                List<IKGD_Path> pathsPublished = fsOp.PathsRefineV2(nodes_publ2.Select(n => new IKGD_Path(n.vNode.snode)).ToList(), false, true, null, true, forceNoOptPathSearch, 0);
                List<IKGD_Path> pathsCurrent = fsOp.PathsRefineV2(nodes_curr2.Select(n => new IKGD_Path(n.vNode.snode)).ToList(), false, true, null, true, forceNoOptPathSearch, -1);
                //
                // TODO: modificare la gestione dei folder __Upload__ che non devono essere disattivati ma utilizzare i flags_menu (anche per IkonPortal)
                pathsPublished = pathsPublished.FilterCustom(IKGD_Path_Helper.FilterByRootVFS).Where(p => p.Fragments.All(f => f.flag_active || f.Name == FS_Operations.UploadFolderName)).ToList();
                pathsCurrent = pathsCurrent.FilterCustom(IKGD_Path_Helper.FilterByRootVFS).Where(p => p.Fragments.All(f => f.flag_active || f.Name == FS_Operations.UploadFolderName)).ToList();
                //
                nodes_all2 = nodes_all2.Where(n => (n.IsPublished && pathsPublished.Any(p => p.sNode == n.sNode)) || (n.IsCurrent && pathsCurrent.Any(p => p.sNode == n.sNode))).ToList();
                //
                var nodes_inodes = nodes_all2.Where(n => n.iNode != null).Select(n => n.iNode.version).Distinct().ToList();
                var nodes_streams = fsOp.DB.IKGD_STREAMs.Where(s => s.inode != null && nodes_inodes.Contains(s.inode.Value)).Select(s => new { id = s.id, inode = s.inode, source = s.source, key = s.key, type = s.type }).ToList();
                nodes_all2.ForEach(n => n.Streams = n.iNode == null ? new List<IKGD_STREAM>() : nodes_streams.Where(s => s.inode == n.iNode.version).Select(r => new IKGD_STREAM { id = r.id, inode = r.inode, source = r.source, key = r.key, type = r.type }).ToList());
                //
                foreach (var nodesGroup in nodes_all2.GroupBy(n => n.rNode))
                {
                  try
                  {
                    //
                    // lettura di tutti gli streams validi associati al nodeset
                    //List<IKCMS_LuceneRecordData.StreamStorage> StreamsGroup = new List<IKCMS_LuceneRecordData.StreamStorage>();
                    Utility.DictionaryMV<int, string> StreamsParsed = new Utility.DictionaryMV<int, string>();
                    if (nodesGroup.SelectMany(n => n.Streams).Any())
                    {
                      //
                      var streamsIds = nodesGroup.SelectMany(n => n.Streams.Where(s => !Ikon.Filters.FilterReader.FileMimeDeny.IsMatch(s.type)).Select(s => s.id)).Distinct().ToList();
                      //
                      foreach (var nodes in nodesGroup.Where(n => n.Streams.Any()).GroupBy(n => n.iNode != null ? (int?)n.iNode.version : null).Where(g => g.Key != null))
                      {
                        var luceneStream = nodes.SelectMany(n => n.Streams).Where(s => s.source == "lucene" && s.key == "luceneText").OrderBy(s => s.id).FirstOrDefault();
                        int maxId = nodes.Max(n => n.Streams.Max(s => s.id));
                        int? stream_inode = luceneStream == null ? nodes.FirstOrDefault(n => n.iNode != null).iNode.version : (int?)luceneStream.inode;
                        int? stream_id = luceneStream == null ? null : (int?)luceneStream.id;
                        if (luceneStream != null && luceneStream.id < maxId)
                        {
                          // lucene stream esistente ma da cancellare
                          // lo togliamo dalla lista degli streamsIds e lo rigeneriamo
                          var ids = nodes.SelectMany(n => n.Streams).Where(s => s.source == "lucene" && s.key == "luceneText").Select(s => s.id).Distinct().ToList();
                          streamsIds.RemoveAll(s => ids.Contains(s));
                          stream_id = null;
                          //DB_RW.IKGD_MSTREAMs.DeleteAllOnSubmit(DB_RW.IKGD_STREAMs.Where(s => ids.Contains(s.id)).SelectMany(s => s.IKGD_MSTREAMs).Distinct());  // non dovrebbe essere necessario in quanto dipendente dal cascade definito per IKGD_STREAM
                          DB_RW.IKGD_STREAMs.DeleteAllOnSubmit(DB_RW.IKGD_STREAMs.Where(s => ids.Contains(s.id)));
                        }
                        if (stream_id == null)
                        {
                          var ids = nodes.SelectMany(n => n.Streams.Where(s => streamsIds.Contains(s.id) && s.source != "lucene")).Distinct().ToList();
                          var texts = ids.Distinct((s1, s2) => s1.id == s2.id).Select(s =>
                          {
                            // supporto dell'external storage
                            string fName = null;
                            byte[] data = fsOp.Get_STREAM_NoLinq(s.id);
                            string mime = s.type;
                            if (IKGD_ExternalVFS_Support.IsExternalFileFromMime(mime))
                            {
                              mime = IKGD_ExternalVFS_Support.GetMimeType(mime);
                              fName = Utility.LinqBinaryGetStringDB(data);
                              fName = extFS.ResolveFileName(fName);
                            }
                            return Ikon.Filters.FilterReader.StreamParserV2(fName, data, null, s.type);
                          }).ToList();
                          string text = Utility.Implode(texts, "\n", null, true, true);
                          IKGD_STREAM stream = new IKGD_STREAM { inode = stream_inode, source = "lucene", key = "luceneText", type = "text" };
                          stream.IKGD_MSTREAMs.Add(new IKGD_MSTREAM { inode = stream_inode.Value });
                          stream.dataAsString = text;
                          StreamsParsed[stream_inode.Value] = text;
                          DB_RW.IKGD_STREAMs.InsertOnSubmit(stream);
                          DB_RW.SubmitChanges();
                        }
                        else
                        {
                          StreamsParsed[stream_inode.Value] = Utility.LinqBinaryGetStringDB(fsOp.Get_STREAM_NoLinq(stream_id.Value));
                        }
                      }
                    }
                    //
                    foreach (var nodes in nodesGroup.GroupBy(n => new TupleW<int, int?>(n.vData.version, n.iNode != null ? (int?)n.iNode.version : null)))
                    {
                      //
                      List<IKCMS_LuceneRecordData> records = new List<IKCMS_LuceneRecordData>();
                      //
                      FS_Operations.FS_NodeInfoStreams fsNode = nodes.FirstOrDefault();
                      //
                      // subset di lingue attive da deserializzare per il set (se anche una sola risorsa e' senza lingua associata allora restano tutte valide)
                      //var Streams = StreamsGroup.Where(s => fsNode.Streams.Contains(s.Key)).ToList();
                      string StreamsText = fsNode.iNode == null ? null : StreamsParsed[fsNode.iNode.version];
                      //
                      Type resType = resTypes[fsNode.vData.manager_type];
                      IKGD_ResourceTypeBase resObj = IKGD_ResourceTypeBase.CreateInstance(resType);
                      //
                      object resourceData = null;
                      KeyValueObjectTree ValuesKVT = null;
                      try
                      {
                        if (resObj is IKGD_Widget_Interface)
                          resourceData = IKGD_WidgetDataImplementation.DeSerializeByType((this as IKGD_Widget_Interface).WidgetSettingsType, fsNode.vData.settings, true).Config;
                        else if (resObj is IKCMS_HasSerializationCMS_Interface)
                        {
                          resObj = IKCMS_RegisteredTypes.Deserialize_IKCMS_ResourceVFS(fsNode) as IKGD_ResourceTypeBase;
                          resourceData = (resObj as IKCMS_HasSerializationCMS_Interface).ResourceSettingsObject;
                        }
                        if (resObj is IKCMS_HasPropertiesKVT_Interface)
                          ValuesKVT = (resObj as IKCMS_HasPropertiesKVT_Interface).ResourceSettingsKVT;
                      }
                      catch { }
                      //
                      // loop sugli fsNode del group
                      //
                      foreach (FS_Operations.FS_NodeInfoStreams node in nodes)
                      {
                        var languages = (string.IsNullOrEmpty(node.Language) ? LanguagesAvailable : Enumerable.Repeat(node.Language, 1)).ToList();
                        foreach (string language in languages)
                        {
                          var record = new IKCMS_LuceneRecordData() { Language = language, fsNode = node, resObj = resObj, resourceData = resourceData, Fields = new List<IKCMS_LuceneRecordData.FieldStorage>(), StreamsText = StreamsText };
                          //var record = new IKCMS_LuceneRecordData() { Language = language, fsNode = node, resObj = resObj, resourceData = resourceData, Streams = Streams, Fields = new List<IKCMS_LuceneRecordData.FieldStorage>(), StreamsText = StreamsText };
                          //
                          if (ValuesKVT != null && resObj is IKCMS_HasPropertiesLanguageKVT_Interface)
                          {
                            var KVT = ValuesKVT.KeyFilterCheck((string)null);
                            if (KVT != null)
                            {
                              record.Fields.AddRange(KVT.RecurseOnTreeFiltered(r => !RxInValidKeysKVT.IsMatch(r.Key ?? string.Empty)).Where(r => !RxInValidValuesKVT.IsMatch(r.ValueStringNN)).Select(r => new IKCMS_LuceneRecordData.FieldStorage(r.Key, r.ValueString)));
                            }
                            KVT = ValuesKVT.KeyFilterCheck(language);
                            if (KVT != null)
                            {
                              record.Fields.AddRange(KVT.RecurseOnTreeFiltered(r => !RxInValidKeysKVT.IsMatch(r.Key ?? string.Empty)).Where(r => !RxInValidValuesKVT.IsMatch(r.ValueStringNN)).Select(r => new IKCMS_LuceneRecordData.FieldStorage(r.Key, r.ValueString)));
                            }
                          }
                          else if (ValuesKVT != null && resObj is IKCMS_HasPropertiesKVT_Interface)
                          {
                            record.Fields.AddRange(ValuesKVT.RecurseOnTreeFiltered(r => !RxInValidKeysKVT.IsMatch(r.Key ?? string.Empty)).Where(r => !RxInValidValuesKVT.IsMatch(r.ValueStringNN)).Select(r => new IKCMS_LuceneRecordData.FieldStorage(r.Key, r.ValueString)));
                          }
                          if (record.Fields.Any())
                            record.Fields = record.Fields.ReverseT().Distinct((f1, f2) => f1.Key == f2.Key).Reverse().ToList();
                          //
                          records.Add(record);
                        }
                      }
                      //
                      records = records.Distinct((r1, r2) => r1.Language == r2.Language && r1.fsNode.sNode == r2.fsNode.sNode && r1.fsNode.IsPublished == r2.fsNode.IsPublished && r1.fsNode.IsCurrent == r2.fsNode.IsCurrent).ToList();
                      //
                      // funzioni customizzabili tramite override nelle risorse custom
                      resObj.GetSearchInfoTitle(fsOp, xHtmlCleaner, records);
                      resObj.GetSearchInfoTexts(fsOp, xHtmlCleaner, records);
                      //
                      // insert records into lucene index
                      //
                      foreach (var record in records)
                      {
                        var paths = (record.fsNode.IsPublished ? pathsPublished : pathsCurrent).Where(p => p.sNode == record.fsNode.sNode).Where(p => p.IsLanguageAccessible(record.Language));
                        var pathNode = paths.FirstOrDefault();
                        if (pathNode != null)
                        {
                          bool res01 = IKGD_AddResource(record.fsNode, pathNode, paths, record.Name, record.Title, record.Text, record.Language);
                        }
                      }
                      //
                    }
                    rNodesProcessed.Add(nodesGroup.FirstOrDefault().rNode);
                  }
                  catch { }
                }
                //
                try { vNodeMax = Math.Max(vNodeMax, nodes_all2.Max(n => n.vNode.version)); }
                catch { }
                try { vDataMax = Math.Max(vDataMax, nodes_all2.Max(n => n.vData.version)); }
                catch { }
                try { iNodeMax = Math.Max(iNodeMax, nodes_all2.Where(n => n.iNode != null).Max(n => n.iNode.version)); }
                catch { }
              }  //for
            }  //using (IKGD_DataContext DB_RW
          }  //if
        }  //using (FS_Operations fsOp
        //
        IKGD_IndexOptimize(true);
        ReOpenSearcher();
        //
        HttpContext.Current.Trace.Write("rNodesProcessed", rNodesProcessed.Count.ToString());
        return rNodesProcessed.Count;
      }
      catch (Exception ex) { throw ex; }
      finally
      {
        if (extFS != null)
        {
          extFS.Dispose();
          extFS = null;
        }
      }
    }


    public int IKGD_LuceneStreamsCleaner()
    {
      try
      {
        using (FS_Operations fsOp = new FS_Operations(-1, true, true))
        {
          int res = fsOp.DB.ExecuteCommand("DELETE FROM IKGD_STREAM WHERE [source]='lucene' AND [key]='luceneStream'");
          return res;
        }
      }
      catch { }
      return -1;
    }


    //
    // funzione di ricerca per IkonPortal
    //
    public IEnumerable<IKGD_LuceneHit> IKGD_Search(string strSearch, FS_Areas_Extended allowedAreas, string manager_type, int? sNodeFolder, DateTime? dateStart, DateTime? dateEnd, bool searchPreview, bool formatMatches)
    {
      strSearch = (strSearch ?? string.Empty).Trim();
      if (strSearch.Length == 0)
      {
        //
        // ricerca diretta su VFS con enumeratore deterministico delle risorse
        //
        foreach (IKGD_LuceneHit hit in IKGD_SearchVFS(allowedAreas, manager_type, sNodeFolder, dateStart, dateEnd, searchPreview))
          yield return hit;
      }
      else
      {
        foreach (IKGD_LuceneHit hit in IKGD_SearchLucene(strSearch, allowedAreas, manager_type, sNodeFolder, dateStart, dateEnd, searchPreview, formatMatches))
          yield return hit;
      }
    }


    public IEnumerable<IKGD_LuceneHit> IKGD_SearchVFS(FS_Areas_Extended allowedAreas, string manager_type, int? sNodeFolder, DateTime? dateStart, DateTime? dateEnd, bool searchPreview)
    {
      dateStart = Utility.GetNullDateIfInvalidDateDB(dateStart);
      dateEnd = Utility.GetNullDateIfInvalidDateDB(dateEnd);
      //
      //
      Dictionary<string, Type> resTypes = IKGD_ResourceTypeBase.FindRegisteredResourceTypes().ToDictionary(t => t.Name);
      IKGD_HtmlCleaner xHtmlCleaner = new IKGD_HtmlCleaner();
      int textLen = Math.Max(Utility.TryParse<int>(IKGD_Config.AppSettings["LuceneFragmentsCount"], 3) * Utility.TryParse<int>(IKGD_Config.AppSettings["LuceneFragmentsLength"], 100), 50);
      //
      using (FS_Operations fsOp = new FS_Operations(searchPreview ? -1 : 0, false, true))
      {
        sNodeFolder = sNodeFolder ?? FS_Operations.sNodeCodeFirstFolder;
        IKGD_VNODE vNodeRoot = fsOp.NodeActive(sNodeFolder.Value);
        vNodeRoot = fsOp.Get_FolderCurrentFallBack(vNodeRoot);
        //
        Expression<Func<IKGD_VDATA, bool>> vDataFilterAll = PredicateBuilder.True<IKGD_VDATA>();
        if (allowedAreas != null)
        {
          //vDataFilterAll = vDataFilterAll.And(n => allowedAreas.Contains(n.area));
          if (allowedAreas.AreaMatchMode == FS_Areas_Extended.AreaMatchModeEnum.FilterByAllowed)
            vDataFilterAll = vDataFilterAll.And(n => allowedAreas.AreasAllowed.Contains(n.area));
          else if (allowedAreas.AreaMatchMode == FS_Areas_Extended.AreaMatchModeEnum.FilterByDenied)
            vDataFilterAll = vDataFilterAll.And(n => !allowedAreas.AreasDenied.Contains(n.area));
        }
        //
        Expression<Func<IKGD_VDATA, bool>> vDataFilterAll2 = vDataFilterAll;
        // solo accesso a risorse non strutturate da VFS, non ha senso il browsing degli allegati per una ricerca senza testo
        vDataFilterAll2 = vDataFilterAll2.And(n => n.flag_unstructured == false);
        //
        if (!string.IsNullOrEmpty(manager_type))
          vDataFilterAll2 = vDataFilterAll2.And(n => n.manager_type == manager_type);
        else
          vDataFilterAll2 = vDataFilterAll2.And(n => n.manager_type != null && n.manager_type != string.Empty);
        //
        if (dateStart != null)
          vDataFilterAll2 = vDataFilterAll2.And(n => n.date_node >= dateStart);
        if (dateEnd != null)
          vDataFilterAll2 = vDataFilterAll2.And(n => n.date_node <= dateEnd);
        //
        List<IKGD_VNODE> subFolders = new List<IKGD_VNODE> { vNodeRoot };
        for (List<IKGD_VNODE> lastF = subFolders; lastF != null && lastF.Count > 0; )
        {
          lastF = (from vNode in fsOp.NodesActive<IKGD_VNODE>().Where(n => n.flag_folder)
                   from vData in fsOp.NodesActive<IKGD_VDATA>().Where(vDataFilterAll)
                   where vNode.rnode == vData.rnode && lastF.Where(n => !n.flag_deleted).Select(n => n.folder).Contains(vNode.parent.Value)
                   select vNode).ToList();
          if (lastF != null)
            subFolders.AddRange(lastF);
        }
        List<int> subFoldersCodes = subFolders.Select(n => n.folder).Distinct().ToList();
        var fsNodes =
          (from vNode in fsOp.NodesActive<IKGD_VNODE>().Where(n => (!n.flag_folder && subFoldersCodes.Contains(n.folder)))
           from vData in fsOp.NodesActive<IKGD_VDATA>().Where(vDataFilterAll2).Where(n => n.rnode == vNode.rnode)
           from iNode in fsOp.NodesActive<IKGD_INODE>().Where(n => n.rnode == vNode.rnode).DefaultIfEmpty()
           orderby vNode.name, vNode.folder, vNode.position
           select new FS_Operations.FS_NodeInfoStreams { vNode = vNode, vData = vData, iNode = iNode }).Take(MaxResults);
        //
        foreach (FS_Operations.FS_NodeInfoStreams fsNode in fsNodes)
        {
          Type resType = resTypes[fsNode.vData.manager_type];
          IKGD_ResourceTypeBase resObj = IKGD_ResourceTypeBase.CreateInstance(resType);
          //
          object resourceData = null;
          KeyValueObjectTree ValuesKVT = null;
          try
          {
            if (resObj is IKGD_Widget_Interface)
              resourceData = IKGD_WidgetDataImplementation.DeSerializeByType((this as IKGD_Widget_Interface).WidgetSettingsType, fsNode.vData.settings, true).Config;
            else if (resObj is IKCMS_HasSerializationCMS_Interface)
            {
              resObj = IKCMS_RegisteredTypes.Deserialize_IKCMS_ResourceVFS(fsNode) as IKGD_ResourceTypeBase;
              resourceData = (resObj as IKCMS_HasSerializationCMS_Interface).ResourceSettingsObject;
            }
            if (resObj is IKCMS_HasPropertiesKVT_Interface)
              ValuesKVT = (resObj as IKCMS_HasPropertiesKVT_Interface).ResourceSettingsKVT;
          }
          catch { }
          string language = fsNode.Language ?? IKGD_Language_Provider.Provider.LanguageNN;
          var record = new IKCMS_LuceneRecordData() { Language = language, fsNode = fsNode, resObj = resObj, resourceData = resourceData, Fields = new List<IKCMS_LuceneRecordData.FieldStorage>() };
          //var record = new IKCMS_LuceneRecordData() { Language = language, fsNode = fsNode, resObj = resObj, resourceData = resourceData, Streams = new List<IKCMS_LuceneRecordData.StreamStorage>(), Fields = new List<IKCMS_LuceneRecordData.FieldStorage>() };
          //
          if (ValuesKVT != null && resObj is IKCMS_HasPropertiesLanguageKVT_Interface)
          {
            var KVT = ValuesKVT.KeyFilterCheck(language);
            if (KVT != null)
            {
              record.Fields.AddRange(KVT.RecurseOnTreeFiltered(r => !RxInValidKeysKVT.IsMatch(r.Key ?? string.Empty)).Where(r => !RxInValidValuesKVT.IsMatch(r.ValueStringNN)).Select(r => new IKCMS_LuceneRecordData.FieldStorage(r.Key, r.ValueString)));
            }
          }
          else if (ValuesKVT != null && resObj is IKCMS_HasPropertiesKVT_Interface)
          {
            record.Fields.AddRange(ValuesKVT.RecurseOnTreeFiltered(r => !RxInValidKeysKVT.IsMatch(r.Key ?? string.Empty)).Where(r => !RxInValidValuesKVT.IsMatch(r.ValueStringNN)).Select(r => new IKCMS_LuceneRecordData.FieldStorage(r.Key, r.ValueString)));
          }
          //
          var records = new List<IKCMS_LuceneRecordData>() { record };
          resObj.GetSearchInfoTitle(fsOp, xHtmlCleaner, records);
          resObj.GetSearchInfoTexts(fsOp, xHtmlCleaner, records);
          //resObj.GetSearchInfoTitle(fsOp, null, records);
          //resObj.GetSearchInfoTexts(fsOp, null, records);
          //
          if (!resObj.IsUnstructured)
          {
            record.Text = xHtmlCleaner.ParseAndTruncate(record.Text, textLen, true, "...");
          }
          else if (!string.IsNullOrEmpty(record.Text))
          {
            record.Text = IKGD_HtmlCleaner.TruncateSimple(record.Text, textLen, true, "...");
          }
          record.Text = record.Text ?? string.Empty;
          //
          //TODO: verificare yield con inizializzazione pesante
          if (record.Title != null && record.Text != null)
            yield return new IKGD_LuceneHit(fsNode, record.Title, record.Text);
        }
      }
    }


    public IEnumerable<IKGD_LuceneHit> IKGD_SearchLucene(string strSearch, FS_Areas_Extended allowedAreas, string manager_type, int? sNodeFolder, DateTime? dateStart, DateTime? dateEnd, bool searchPreview, bool formatMatches)
    {
      if (string.IsNullOrEmpty(strSearch))
        yield break;
      dateStart = Utility.GetNullDateIfInvalidDateDB(dateStart);
      dateEnd = Utility.GetNullDateIfInvalidDateDB(dateEnd);
      //
      BooleanQuery queryFilterFrags = new BooleanQuery();
      //
      // selettore per lo stato di pubblicazione richiesto
      //
      queryFilterFrags.Add(new TermQuery(new Term(searchPreview ? "flag_current" : "flag_published", true.ToString())), Occur.MUST);
      //
      bool? unstructuredSelector = null;
      if (unstructuredSelector.HasValue)
        queryFilterFrags.Add(new TermQuery(new Term("flag_unstructured", unstructuredSelector.Value.ToString())), Occur.MUST);
      //
      if (!string.IsNullOrEmpty(manager_type))
        queryFilterFrags.Add(new TermQuery(new Term("manager_type", manager_type)), Occur.MUST);
      //
      if (dateStart != null || dateEnd != null)
      {
        DateTime dateStartL = dateStart ?? DateTime.MinValue;
        DateTime dateEndL = dateEnd ?? DateTime.MaxValue;
        queryFilterFrags.Add(new TermRangeQuery("date", dateStartL.ToString("u"), dateEndL.ToString("u"), true, true), Occur.MUST);
      }
      //
      QueryParser queryParser = new QueryParser(Version.LUCENE_30, "text", IKGD_Analizer);
      Query query = queryParser.Parse(strSearch);
      QueryWrapperFilter queryFilter = new QueryWrapperFilter(queryFilterFrags);
      Query query_rewrite = query.Rewrite(IKGD_IndexReader);  //required to expand search terms
      //
      int maxResults = MaxResults;
      if (maxResults <= 0)
        maxResults = IKGD_IndexSearcher.MaxDoc;
      TopScoreDocCollector collector = TopScoreDocCollector.Create(maxResults, false);
      IKGD_IndexSearcher.Search(query, queryFilter, collector);
      int resultsCount = collector.TotalHits;
      List<ScoreDoc> luceneDocs = collector.TopDocs().ScoreDocs.ToList();
      //
      SimpleHTMLFormatter formatter = new SimpleHTMLFormatter("<b>", "</b>");
      SimpleFragmenter fragmenter = new SimpleFragmenter(Utility.TryParse<int>(IKGD_Config.AppSettings["LuceneFragmentsLength"], 100));
      Highlighter highlighter = new Highlighter(formatter, new QueryScorer(query, "text"));
      highlighter.TextFragmenter = fragmenter;
      //
      using (FS_Operations fsOp = new FS_Operations(searchPreview ? -1 : 0, false, true))
      {
        //
        // setup dei filtri sul VFS
        //
        List<int> rNodes = null;
        if (sNodeFolder != null)
        {
          IKGD_VNODE vNodeRoot = fsOp.NodeActive(sNodeFolder.Value);
          vNodeRoot = fsOp.Get_FolderCurrentFallBack(vNodeRoot);
          //
          Expression<Func<IKGD_VDATA, bool>> vDataFilterAll = PredicateBuilder.True<IKGD_VDATA>();
          if (allowedAreas != null)
          {
            //vDataFilterAll = vDataFilterAll.And(n => allowedAreas.Contains(n.area));
            if (allowedAreas.AreaMatchMode == FS_Areas_Extended.AreaMatchModeEnum.FilterByAllowed)
              vDataFilterAll = vDataFilterAll.And(n => allowedAreas.AreasAllowed.Contains(n.area));
            else if (allowedAreas.AreaMatchMode == FS_Areas_Extended.AreaMatchModeEnum.FilterByDenied)
              vDataFilterAll = vDataFilterAll.And(n => !allowedAreas.AreasDenied.Contains(n.area));
          }
          //
          Expression<Func<IKGD_VDATA, bool>> vDataFilterAll2 = vDataFilterAll;
          //
          if (!string.IsNullOrEmpty(manager_type))
            vDataFilterAll2 = vDataFilterAll2.And(n => n.manager_type == manager_type);
          else
            vDataFilterAll2 = vDataFilterAll2.And(n => n.manager_type != null && n.manager_type != string.Empty);
          //
          if (dateStart != null)
            vDataFilterAll2 = vDataFilterAll2.And(n => n.date_node >= dateStart);
          if (dateEnd != null)
            vDataFilterAll2 = vDataFilterAll2.And(n => n.date_node <= dateEnd);
          //
          List<IKGD_VNODE> subFolders = new List<IKGD_VNODE> { vNodeRoot };
          for (List<IKGD_VNODE> lastF = subFolders; lastF != null && lastF.Count > 0; )
          {
            lastF = (from vNode in fsOp.NodesActive<IKGD_VNODE>().Where(n => n.flag_folder)
                     from vData in fsOp.NodesActive<IKGD_VDATA>().Where(vDataFilterAll)
                     where vNode.rnode == vData.rnode && lastF.Where(n => !n.flag_deleted).Select(n => n.folder).Contains(vNode.parent.Value)
                     select vNode).ToList();
            if (lastF != null)
              subFolders.AddRange(lastF);
          }
          List<int> subFoldersCodes = subFolders.Select(n => n.folder).Distinct().ToList();
          //
          // lista degli rNodes attivi nel subtree specificato per la ricerca
          //
          rNodes =
            (from vNode in fsOp.NodesActive<IKGD_VNODE>().Where(n => (!n.flag_folder && subFoldersCodes.Contains(n.folder)))
             from vData in fsOp.NodesActive<IKGD_VDATA>().Where(vDataFilterAll2).Where(n => n.rnode == vNode.rnode)
             select vNode.rnode).Distinct().ToList();
        }
        //
        // loop sui risultati di Lucene
        //
        int counter = 0;
        foreach (ScoreDoc luceneDoc in luceneDocs)
        {
          Document document = IKGD_IndexSearcher.Doc(luceneDoc.Doc);
          IKGD_LuceneHit hit = new IKGD_LuceneHit(luceneDoc, document, formatMatches ? this : null);
          if (allowedAreas != null && !allowedAreas.AreasAllowed.Contains(hit.area))
            continue;
          if (rNodes != null && !rNodes.Contains(hit.rNode))
            continue;
          if (counter++ >= MaxResults)
            break;
          //TODO: verificare yield con inizializzazione pesante
          yield return hit;
        }
      }
    }


    //
    // funzione di ricerca per il CMS
    //
    public IEnumerable<IKGD_LuceneDocCollection> IKGD_SearchLuceneCMS(string strSearch, int? maxResults)
    {
      LastMatchesCount = 0;
      IEnumerable<IKGD_LuceneDocCollection> results = IKGD_SearchLuceneCMS(strSearch,
        null, null,
        IKGD_Language_Provider.Provider.LanguageNN,
        (IKGD_Config.AppSettings["Lucene_Roots"] != null) ? IKGD_SiteMode.GetConfig4SiteMode<int>(IKGD_Config.AppSettings["Lucene_Roots"], ",") : IKGD_ConfigVFS.Config.RootsCMS_folders.ToList(),
        //(IKGD_Config.AppSettings["Lucene_Roots"] != null) ? IKGD_SiteMode.GetConfig4SiteMode<int>(IKGD_Config.AppSettings["Lucene_Roots"], ",") : IKGD_ConfigVFS.Config.RootsVFS_folders.ToList(),
        Utility.TryParse<int?>(IKGD_Config.AppSettings["Lucene_VersionFrozen"]),
        null, null,
        maxResults);
      return results;
    }


    public IEnumerable<IKGD_LuceneDocCollection> IKGD_SearchLuceneCMS(string strSearch, IEnumerable<string> allowedAreas, IEnumerable<string> manager_types, string language, IEnumerable<int> rootNodes, int? versionFrozen, DateTime? dateStart, DateTime? dateEnd, int? maxResults)
    {
      LastMatchesCount = 0;
      if (rootNodes != null && !rootNodes.Any())
        yield break;
      //
      Query query;
      QueryWrapperFilter queryFilter;
      Query query_rewrite;  //required to expand search terms
      IKGD_SearchLuceneQueryBuilder(out query, out queryFilter, out query_rewrite, strSearch, allowedAreas, manager_types, language, versionFrozen, dateStart, dateEnd);
      maxResults = maxResults.GetValueOrDefault(MaxResults);
      if (maxResults <= 0)
        maxResults = IKGD_IndexSearcher.MaxDoc;
      TopScoreDocCollector collector = TopScoreDocCollector.Create(maxResults.Value, false);
      IKGD_IndexSearcher.Search(query, queryFilter, collector);
      int hitsCount = collector.TotalHits;
      //
      if (hitsCount == 0)
        yield break;
      //
      LastMatchesCount = hitsCount;
      //
      SimpleHTMLFormatter formatter = new SimpleHTMLFormatter("<b>", "</b>");
      SimpleFragmenter fragmenter = new SimpleFragmenter(Utility.TryParse<int>(IKGD_Config.AppSettings["LuceneFragmentsLength"], 100));
      IKGD_Highlighter = new Highlighter(formatter, new QueryScorer(query, "text"));
      IKGD_Highlighter.TextFragmenter = fragmenter;
      //
      int outputCounter = 0;
      int chunkSize = 10;
      using (FS_Operations fsOp = new FS_Operations(versionFrozen, true, true))
      {
        //
        IEnumerable<IKGD_LuceneDoc> luceneDocs = collector.TopDocs().ScoreDocs.OrderByDescending(d => d.Score).ThenBy(d => d.Doc).Select(d => new IKGD_LuceneDoc(d, this));
        //
        foreach (var luceneDocsChunk in luceneDocs.GroupBy(r => r.groupingKey).Select(g => new IKGD_LuceneDocCollection(g)).OrderByDescending(g => g.Score).Slice(chunkSize))
        {
          List<int> rNodes = luceneDocsChunk.SelectMany(dc => dc.rNodes).Distinct().ToList();
          List<IKGD_Path> paths = fsOp.PathsFromNodesExt(null, rNodes, false, true, false).FilterPathsByLanguage(language).FilterPathsByExpiry().ToList();  // full access e' richiesto per trattare i files presenti nel folders speciali es: upload
          //List<IKGD_Path> paths = fsOp.PathsFromNodesExt(null, rNodes, false, true, true, false).FilterPathsByLanguage(language).ToList();  // forzatura NoOpt
          //
          if (manager_types != null)
            paths.RemoveAll(p => !manager_types.Contains(p.LastFragment.ManagerType));
          if (rootNodes != null)
            paths.RemoveAll(p => !p.Fragments.Any(f => rootNodes.Contains(f.rNode)));
          // vengono trattati i path con folder speciali inattivi (es. allegati in __Upload__ che non sarebbero visibili)
          //paths.RemoveAll(p => p.Fragments.Any(f => !f.flag_active && !((string.Equals(f.Name, FS_Operations.UploadFolderName, StringComparison.OrdinalIgnoreCase) || SpecialFoldersRegEx.IsMatch(f.Name)) && f.flag_folder)));
          paths = paths.Where(p => p.Fragments.All(f => f.flag_active) || (SpecialFoldersRegEx.IsMatch(p.FolderFragment.Name) && p.IsFile && p.LastFragment.flag_unstructured)).ToList();
          //
          if (!paths.Any())
            continue;
          //
          IKGD_HtmlCleaner htmlCleaner = null;
          if (Utility.TryParse<bool>(IKGD_Config.AppSettings["LuceneResultsHtmlFilterEnabled"], false))
          {
            htmlCleaner = new IKGD_HtmlCleaner();
          }
          //
          foreach (IKGD_LuceneDocCollection luceneDocsColl in luceneDocsChunk)
          {
            List<int> rNodes2 = luceneDocsColl.Docs.Select(d => d.rNode).ToList();
            luceneDocsColl.Paths = paths.Where(p => luceneDocsColl.rNodes.Contains(p.LastFragment.rNode)).ToList();
            if (luceneDocsColl.Paths.Count == 0)
              continue;
            luceneDocsColl.Process(fsOp, this, htmlCleaner);
            if (!luceneDocsColl.isPage)
              continue;
            //
            //TODO: verificare yield con inizializzazione pesante
            if (outputCounter++ < MaxResultsCMS || MaxResultsCMS <= 0)
            {
              yield return luceneDocsColl;
            }
            else
            {
              yield break;
            }
            //
          }
        }  // chunk
      }  // fsOp
    }


    public IEnumerable<IKGD_LuceneDoc> IKGD_SearchLuceneRaw(string strSearch, IEnumerable<string> allowedAreas, IEnumerable<string> manager_types, string language, int? versionFrozen, DateTime? dateStart, DateTime? dateEnd, int? maxResults)
    {
      if (string.IsNullOrEmpty(strSearch))
        yield break;
      if (allowedAreas != null && !allowedAreas.Any())
        yield break;
      if (manager_types != null && !manager_types.Any())
        yield break;
      //
      Query query;
      QueryWrapperFilter queryFilter;
      Query query_rewrite;  //required to expand search terms
      IKGD_SearchLuceneQueryBuilder(out query, out queryFilter, out query_rewrite, strSearch, allowedAreas, manager_types, language, versionFrozen, dateStart, dateEnd);
      //
      maxResults = maxResults.GetValueOrDefault(MaxResults);
      if (maxResults <= 0)
        maxResults = IKGD_IndexSearcher.MaxDoc;
      TopScoreDocCollector collector = TopScoreDocCollector.Create(maxResults.Value, false);
      IKGD_IndexSearcher.Search(query, queryFilter, collector);
      LastMatchesCount = collector.TotalHits;
      //TODO: verificare yield con inizializzazione pesante
      foreach (var doc in collector.TopDocs().ScoreDocs)
        yield return new IKGD_LuceneDoc(doc, this);
    }


    public static Regex SpecialFoldersRegEx = new Regex(@"^__.+__$", RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.Singleline);
    public void IKGD_SearchLuceneQueryBuilder(out Query query, out QueryWrapperFilter queryFilter, out Query query_rewrite, string strSearch, IEnumerable<string> allowedAreas, IEnumerable<string> manager_types, string language, int? versionFrozen, DateTime? dateStart, DateTime? dateEnd)
    {
      dateStart = Utility.GetNullDateIfInvalidDateDB(dateStart);
      dateEnd = Utility.GetNullDateIfInvalidDateDB(dateEnd);
      versionFrozen = versionFrozen ?? FS_OperationsHelpers.VersionFrozenSession;
      List<string> areas = null;
      if (allowedAreas != null)
        areas = allowedAreas.Concat(new string[] { string.Empty }).Distinct().ToList();
      //
      BooleanQuery queryFilterFrags = new BooleanQuery();
      //
      // selettore per lo stato di pubblicazione richiesto
      //
      queryFilterFrags.Add(new TermQuery(new Term((versionFrozen < 0) ? "flag_current" : "flag_published", true.ToString())), Occur.MUST);
      //
      bool? unstructuredSelector = null;
      if (unstructuredSelector.HasValue)
        queryFilterFrags.Add(new TermQuery(new Term("flag_unstructured", unstructuredSelector.Value.ToString())), Occur.MUST);
      //
      // selettore per la lingua
      //
      if (!string.IsNullOrEmpty(language))
      {
        BooleanQuery subQuery = new BooleanQuery();
        subQuery.Add(new TermQuery(new Term("language", "")), Occur.SHOULD);
        subQuery.Add(new TermQuery(new Term("language", language.ToLower())), Occur.SHOULD);
        queryFilterFrags.Add(subQuery, Occur.MUST);
      }
      //
      if (allowedAreas != null && allowedAreas.Count() > 0)
      {
        BooleanQuery subQuery = new BooleanQuery();
        subQuery.Add(new TermQuery(new Term("area", "")), Occur.SHOULD);
        allowedAreas.ForEach(a => subQuery.Add(new TermQuery(new Term("area", a)), Occur.SHOULD));
        queryFilterFrags.Add(subQuery, Occur.MUST);
      }
      //
      if (manager_types != null && manager_types.Count() > 0)
      {
        BooleanQuery subQuery = new BooleanQuery();
        manager_types.ForEach(m => subQuery.Add(new TermQuery(new Term("manager_type", m)), Occur.SHOULD));
        queryFilterFrags.Add(subQuery, Occur.MUST);
      }
      //
      if (dateStart != null || dateEnd != null)
      {
        DateTime dateStartL = dateStart ?? DateTime.MinValue;
        DateTime dateEndL = dateEnd ?? DateTime.MaxValue;
        queryFilterFrags.Add(new TermRangeQuery("date", dateStartL.ToString("u"), dateEndL.ToString("u"), true, true), Occur.MUST);
      }
      //
      QueryParser queryParser = new QueryParser(Version.LUCENE_30, "text", IKGD_Analizer);
      //
      query = queryParser.Parse(strSearch);
      queryFilter = new QueryWrapperFilter(queryFilterFrags);
      query_rewrite = query.Rewrite(IKGD_IndexReader);  //required to expand search terms
      //
    }




  }  // LuceneIndexer



  public class IKGD_LuceneDocCollection
  {
    public List<IKGD_LuceneDoc> Docs { get; protected set; }
    public IEnumerable<IKGD_LuceneDoc> DocsUnique { get { return Docs.Distinct((d1, d2) => d1.rNode == d2.rNode); } }
    public IEnumerable<IKGD_LuceneDoc> DocsUniqueSortedByPath { get { return Docs.Distinct((d1, d2) => d1.rNode == d2.rNode).OrderBy(d => Paths.FindIndex(p => p.sNode == d.sNode)); } }
    public IKGD_LuceneDoc DocMaster { get; protected set; }
    public float Score { get { return Docs.Max(d => d.score); } }
    //public float score { get { return Docs.Sum(d => d.score); } }
    public bool isUnstructured { get; protected set; }
    public bool hasPage { get; protected set; }
    public bool isPage { get; protected set; }
    //
    public string Title { get; protected set; }
    public string Text { get; protected set; }
    public string TextFormatted { get; protected set; }
    //
    public int rNode { get; set; }
    public int rNodePage { get; set; }
    public int sNode { get; set; }
    public IKGD_Path_Fragment FragmentMaster { get; protected set; }
    public List<int> sNodes { get; set; }
    public List<int> rNodes { get; set; }
    public List<IKGD_Path> Paths { get; set; }  //viene assegnato esternamente
    //
    //public string Url { get { return Urls.FirstOrDefault(); } }
    //public List<string> Urls { get; set; }
    public string Url { get; protected set; }
    public string UrlRNODE { get; protected set; }
    public string UrlSEO { get; protected set; }
    public string UrlDownload { get; protected set; }
    //


    public IKGD_LuceneDocCollection(IEnumerable<IKGD_LuceneDoc> docs)
    {
      rNodes = docs.Select(d => d.rNode).Distinct().ToList();
      Docs = docs.OrderByDescending(d => d.score).ThenBy(d => d.id).ToList();
      DocMaster = Docs.FirstOrDefault(d => d.isPage) ?? Docs.FirstOrDefault();
      isUnstructured = Docs.All(d => d.isUnstructured);
      hasPage = Docs.Any(d => d.hasPage);
      rNode = DocMaster.rNode;
      rNodePage = DocMaster.isPage ? DocMaster.rNode : DocMaster.folder;
    }


    public void Process(FS_Operations fsOp, LuceneIndexer Indexer, IKGD_HtmlCleaner htmlCleaner)
    {
      Title = DocMaster.title;
      if (hasPage && !DocMaster.isPage)
      {
        // ottenere il titolo della pagina e non della risorsa
        // ottenere url della pagina e non della risorsa
        //try { Title = Paths.SelectMany(p => p.Fragments.Where(f => f.rNode == rNodePage)).FirstOrDefault().Name; }
        //catch { }
        string titleMenu = null;
        try { titleMenu = titleMenu ?? Paths.SelectMany(p => p.Fragments.Where(f => f.rNode == rNodePage)).FirstOrDefault().Name; }
        catch { }
        try { titleMenu = titleMenu ?? Paths.FirstOrDefault().FolderFragment.Name; }
        catch { }
        Title = titleMenu ?? Title;
      }
      //
      Text = Utility.Implode(DocsUniqueSortedByPath.Select(d => d.text), "\n");
      //
      string textFmt = null;
      if (htmlCleaner != null)
      {
        try { textFmt = htmlCleaner.Text(Text); }
        catch { }
      }
      if (textFmt.IsNullOrWhiteSpace())
        textFmt = Text;
      //
      if (textFmt.IsNotNullOrWhiteSpace())
      {
        if (Indexer != null && Indexer.IKGD_Highlighter != null)
        {
          int fragments_count = Utility.TryParse<int>(IKGD_Config.AppSettings["LuceneFragmentsCount"], 3);
          textFmt = Utility.Implode(Indexer.IKGD_Highlighter.GetBestFragments(Indexer.IKGD_Analizer, "text", textFmt, fragments_count), " ... ");
        }
        else
        {
          int len = Math.Max(Utility.TryParse<int>(IKGD_Config.AppSettings["LuceneFragmentsCount"], 3) * Utility.TryParse<int>(IKGD_Config.AppSettings["LuceneFragmentsLength"], 100), 50);
          textFmt = Utility.StringTruncate(textFmt, len, "...");
        }
      }
      TextFormatted = textFmt;
      //
      // generazione delle Urls
      //
      //IKCMS_RegisteredTypes.Types_IKCMS_PageBase_Interface
      rNodes = Enumerable.Repeat(rNodePage, 1).Concat(rNodes.Intersect(Paths.Select(p => p.rNode))).Distinct().ToList();
      // nella ricerca dell'sNode corretto devo trovare una risorsa di tipo pagina
      //sNodes = Paths.SelectMany(p => p.Fragments.Where(f => f.rNode == rNodePage).Select(f => f.sNode)).Distinct().ToList();
      sNodes = Paths.SelectMany(p => p.Fragments.Where(f => rNodes.Contains(f.rNode) && IKCMS_RegisteredTypes.Types_IKCMS_PageBase_Interface.Any(t => t.Name == f.ManagerType)).Select(f => f.sNode)).Distinct().ToList();
      sNode = sNodes.Any() ? sNodes.FirstOrDefault() : Paths.FirstOrDefault().sNode;
      //
      FragmentMaster = Paths.SelectMany(p => p.Fragments.Where(f => f.sNode == sNode)).FirstOrDefault();
      if (FragmentMaster != null && IKCMS_RegisteredTypes.Types_IKCMS_ResourceWithViewer_Interface.Any(t => t.Name == FragmentMaster.ManagerType))
      {
        isPage = true;
      }
      //
      // mapping diretto delle url SEO integrato nei risultati
      //Urls = sNodes.Select(n =>
      //{
      //  string url = IKGD_SEO_Manager.MapOutcomingUrl(n);
      //  if (string.IsNullOrEmpty(url))
      //  {
      //    if (isUnstructured)
      //    {
      //      int? frozen = null;
      //      try { frozen = Paths.SelectMany(p => p.Fragments.Where(f => f.sNode == n)).FirstOrDefault().flag_published ? null : (int?)-1; }
      //      catch { }
      //      url = IKCMS_RouteUrlManager.GetUrlProxyVFS(rNode, null, string.Empty, null, frozen, false, null);
      //    }
      //    else
      //      url = IKCMS_RouteUrlManager.GetMvcUrlGeneral(null, n, null, true);
      //  }
      //  return url;
      //}).ToList();
      if (isUnstructured)
      {
        int? frozen = null;
        try { frozen = Paths.SelectMany(p => p.Fragments.Where(f => f.sNode == sNode)).FirstOrDefault().flag_published ? null : (int?)-1; }
        catch { }
        Url = IKCMS_RouteUrlManager.GetUrlProxyVFS(rNode, null, string.Empty, null, frozen, false, null);
        UrlDownload = IKCMS_RouteUrlManager.GetUrlProxyVFS(rNode, null, string.Empty, null, frozen, false, null, true);
      }
      else
      {
        Url = IKCMS_RouteUrlManager.GetMvcUrlGeneral(null, sNode, Utility.UrlEncodeIndexPathForSEO(FragmentMaster.Name), true);
      }
      string lang = Paths.Select(p => p.FirstLanguage).Where(l => l.IsNotEmpty()).FirstOrDefault();
      UrlDownload = UrlDownload ?? Url;
      UrlSEO = IKGD_SEO_Manager.MapOutcomingUrl(sNode, rNode, lang) ?? Url;
      UrlRNODE = IKCMS_RouteUrlManager.GetMvcUrlGeneralRNODE_WithHints(rNodePage, sNode);
      //
    }

  }



  public class IKGD_LuceneDoc
  {
    public ScoreDoc scoreDoc { get; protected set; }
    public Document Doc { get; protected set; }
    public int id { get { return scoreDoc.Doc; } }
    public float score { get { return scoreDoc.Score; } }
    //
    public string this[string field] { get { return Doc.Get(field); } }
    //
    public int folder { get; protected set; }
    public int rNode { get; protected set; }
    public int sNode { get; protected set; }
    public bool isUnstructured { get; protected set; }
    public bool isPage { get; protected set; }
    public bool isPageWidget { get; protected set; }
    public bool hasPage { get { return (isPage || isPageWidget); } }
    public int groupingKey { get { return isPage ? rNode : folder; } }
    //public int groupingKey { get { return hasPage ? folder : rNode; } }
    //
    public DateTime date { get { return Utility.TryParse<DateTime>(this["date"], DateTime.Now); } }
    public bool IsPublished { get { return Utility.TryParse<bool>(this["flag_published"]); } }
    public bool IsCurrent { get { return Utility.TryParse<bool>(this["flag_current"]); } }
    public string language { get { return this["language"]; } }
    public string name { get { return this["name"]; } }
    public string title { get { return this["title"]; } }
    public string text { get { return this["text"]; } }


    public IKGD_LuceneDoc(ScoreDoc scoreDoc, LuceneIndexer indexer)
    {
      this.scoreDoc = scoreDoc;
      this.Doc = indexer.IKGD_IndexSearcher.Doc(this.scoreDoc.Doc);
      //
      folder = Utility.TryParse<int>(this["folder"]);
      rNode = Utility.TryParse<int>(this["rNode"]);
      sNode = Utility.TryParse<int>(this["sNode"]);
      isUnstructured = Utility.TryParse<bool>(this["flag_unstructured"], false);
      isPage = Utility.TryParse<bool>(this["flag_CMS_isPage"], false);
      isPageWidget = Utility.TryParse<bool>(this["flag_CMS_isPageWidget"], false);
    }


    public override string ToString()
    {
      return string.Format("[{0}] [{1}/{2}/{3}] [{10}/{11}] [{4}/{5}/{6}] [{7}] {8} -> {9}", id, folder, rNode, sNode, isUnstructured, isPage, isPageWidget, name, title, text, IsPublished, IsCurrent);
    }

  }



  public class IKGD_LuceneHit
  {
    public int id { get; set; }
    public Document doc { get; set; }
    public float score { get; set; }
    //
    public string guid { get; set; }
    public int rNode { get; set; }
    public int vData { get; set; }
    public int? sNode { get; set; }
    public int? iNode { get; set; }
    public string area { get; set; }
    public string manager_type { get; set; }
    public bool flag_published { get; set; }
    public bool flag_current { get; set; }
    public bool flag_unstructured { get; set; }
    public string language { get; set; }
    public DateTime date { get; set; }
    //
    public string title { get; set; }
    public string text { get; set; }
    //
    public object paths { get; set; }  // ha difficolta' a passare i dati VFS attraverso il webservice

    //
    // constructors
    //
    public IKGD_LuceneHit()
    {
    }


    public IKGD_LuceneHit(FS_Operations.FS_NodeInfo node, string Title, string Text)
      : this()
    {
      try
      {
        if (node.vNode != null)
          sNode = node.vNode.snode;
        rNode = node.vData.rnode;
        if (node.iNode != null)
          iNode = node.iNode.version;
        vData = node.vData.version;
        area = node.vData.area;
        manager_type = node.vData.manager_type;
        language = string.Empty;
        date = node.vData.date_node;
        flag_unstructured = node.vData.flag_unstructured;
        //
        title = Title;
        int len = Math.Max(Utility.TryParse<int>(IKGD_Config.AppSettings["LuceneFragmentsCount"], 3) * Utility.TryParse<int>(IKGD_Config.AppSettings["LuceneFragmentsLength"], 100), 50);
        text = Utility.StringTruncate(Text, len, "...");
      }
      catch { }
    }


    public IKGD_LuceneHit(ScoreDoc item, Document document, LuceneIndexer indexer)
      : this()
    {
      try
      {
        doc = document;
        id = item.Doc;
        score = item.Score;
        //
        rNode = Utility.TryParse<int>(document.GetField("rNode").StringValue);
        iNode = Utility.TryParse<int>(document.GetField("iNodeVersion").StringValue);
        vData = Utility.TryParse<int>(document.GetField("vDataVersion").StringValue);
        guid = document.GetField("guid").StringValue;
        area = document.GetField("area").StringValue;
        manager_type = document.GetField("manager_type").StringValue;
        language = document.GetField("language").StringValue;
        flag_published = Utility.TryParse<bool>(document.GetField("flag_published").StringValue, false);
        flag_current = Utility.TryParse<bool>(document.GetField("flag_current").StringValue, false);
        flag_unstructured = Utility.TryParse<bool>(document.GetField("flag_unstructured").StringValue, false);
        date = Utility.TryParse<DateTime>(document.GetField("date").StringValue, DateTime.Now);
        //
        title = document.GetField("title").StringValue;
        text = string.Empty;
        string textStream = document.GetField("text").StringValue;
        if (indexer != null && indexer.IKGD_Highlighter != null)
        {
          int fragments_count = Utility.TryParse<int>(IKGD_Config.AppSettings["LuceneFragmentsCount"], 3);
          text = Utility.Implode(indexer.IKGD_Highlighter.GetBestFragments(indexer.IKGD_Analizer, "text", textStream, fragments_count), " ... ");
        }
        else
        {
          int len = Math.Max(Utility.TryParse<int>(IKGD_Config.AppSettings["LuceneFragmentsCount"], 3) * Utility.TryParse<int>(IKGD_Config.AppSettings["LuceneFragmentsLength"], 100), 50);
          text = Utility.StringTruncate(textStream, len, "...");
        }
      }
      catch { }
    }


  }



  public class IKCMS_LuceneRecordData
  {
    public FS_Operations.FS_NodeInfo_Interface fsNode { get; set; }
    public string Language { get; set; }
    public string Name { get; set; }
    public string Title { get; set; }
    public string Text { get; set; }
    //
    public List<FieldStorage> Fields { get; set; }
    //public List<StreamStorage> Streams { get; set; }
    public IKGD_ResourceTypeBase resObj { get; set; }
    public string StreamsText { get; set; }
    public object resourceData { get; set; }
    //

    public class FieldStorage
    {
      public string Key { get; set; }
      public string Value { get; set; }

      public FieldStorage(string Key, string Value)
      {
        this.Key = Key;
        this.Value = Value;
      }
    }

    public class StreamStorage
    {
      public IKGD_STREAM Key { get; set; }
      public string Value { get; set; }

      public StreamStorage(IKGD_STREAM Key, string Value)
      {
        this.Key = Key;
        this.Value = Value;
      }
    }

  }



  public class LuceneAsyncSupport : IBootStrapperTask
  {

    //
    // bootstrapper per la registrazione degli handler di processing asincrono sugli elementi del catalogo
    //
    public void Execute()
    {
      if (IKGD_QueueManager.IsAsyncProcessingEnabled && Utility.TryParse<bool>(IKGD_Config.AppSettings["IKGD_QueueManager_UpdateLucene"], true))
        IKGD_QueueManager.RegisterHandler(HandlerAsyncUpdateLucene, null);
    }


    public static bool HandlerAsyncUpdateLucene(FS_Operations fsOp, IEnumerable<OpHandlerCOW_OperationEnum> opsCOW, IEnumerable<FS_Operations.FS_NodeInfo_Interface> fsNodes)
    {
      bool status = true;
      System.Diagnostics.Stopwatch timer = System.Diagnostics.Stopwatch.StartNew();
      try
      {
        using (Ikon.Indexer.LuceneIndexer indexer = new Ikon.Indexer.LuceneIndexer(true))
        {
          int res = indexer.IKGD_IndexUpdate();
          if (res < 0)
            status = false;
        }
      }
      catch { status = false; }
      timer.Stop();
      var dt = timer.Elapsed.TotalSeconds;
      return status;
    }
  }



  //
  // classe di supporto per l'indexer e i webservices della UI
  // viene utilizzata solamente dalle funzioni relative ai widget della intranet
  //
  public static class LuceneIndexerSupport
  {

    //
    // metodo di supporto per generare la lista delle aree gestite dal search engine
    //
    public static XElement GetAreasList(bool addNull, bool searchPreview)
    {
      XElement xItems = new XElement("root");
      if (addNull)
        xItems.Add(new XElement("item",
            new XAttribute("snode", string.Empty),
            new XAttribute("rnode", string.Empty),
            new XAttribute("text", string.Empty)
          ));
      try
      {
        List<IKGD_VNODE> areaNodes = new List<IKGD_VNODE>();
        List<string> sNodeCodes = Utility.Explode(IKGD_Config.AppSettings["AreasSelector"], ",", " ", true);
        using (FS_Operations fsOp = new FS_Operations(searchPreview ? -1 : 0, false, true))
        {
          foreach (string sNodeCodeStr in sNodeCodes)
          {
            int sNodeCode = Utility.TryParse<int>(sNodeCodeStr);
            if (sNodeCodeStr.StartsWith("+"))
            {
              // ricerca subtree (solo i child diretti del nodo)
              IKGD_VNODE folderNode = fsOp.NodeActive(sNodeCode);
              if (folderNode != null && folderNode.flag_folder)
              {
                var childNodes =
                  from vNode in fsOp.NodesActive<IKGD_VNODE>().Where(n => n.flag_folder && n.parent == folderNode.folder)
                  orderby vNode.position, vNode.name
                  select vNode;
                areaNodes.AddRange(childNodes);
              }
            }
            else
            {
              // ricerca secca
              areaNodes.Add(fsOp.NodeActive(sNodeCode));
            }
          }
        }
        areaNodes = areaNodes.Where(n => n != null && n.flag_folder).ToList();
        foreach (IKGD_VNODE node in areaNodes)
          xItems.Add(new XElement("item",
            new XAttribute("snode", node.snode),
            new XAttribute("rnode", node.rnode),
            new XAttribute("text", node.name)
            ));
      }
      catch { }
      return xItems;
    }


    //
    // metodo di supporto per generare la lista dei tipi di risorsa selezionabili dal search engine
    //
    public static XElement GetResourceTypeList(bool addNull)
    {
      XElement xItems = new XElement("root");
      if (addNull)
        xItems.Add(new XElement("item",
          new XAttribute("type", string.Empty),
          new XAttribute("text", string.Empty)
          ));
      try
      {
        Dictionary<string, Type> resTypes = IKGD_ResourceTypeBase.FindRegisteredResourceTypes().Where(t => !t.IsGenericType && !t.IsAbstract).ToDictionary(t => t.Name);
        foreach (Type resType in resTypes.Values)
        {
          IKGD_ResourceTypeBase resObj = IKGD_ResourceTypeBase.CreateInstance(resType);
          if (resObj == null)
            continue;
          if (!(resObj.IsIndexable || resObj is IKCMS_IsIndexable_Interface))
            continue;
          DescriptionAttribute descrAttr = resType.GetCustomAttributes(typeof(DescriptionAttribute), true).FirstOrDefault() as DescriptionAttribute;
          if (descrAttr == null)
            continue;
          //
          xItems.Add(new XElement("item",
            new XAttribute("type", resType.Name),
            new XAttribute("text", descrAttr.Description)
            ));
        }
      }
      catch { }
      return xItems;
    }


    //
    // versione ottimizzata con build non ricorsivo dei nodi di navigazione
    // e non legge i leaf folders per la costruzione del tree limitando la dimensione del dataset di lavoro
    //
    public static List<int> ScanFolderStructure()
    {
      string sNodeRootsString = IKGD_Config.AppSettings["MenuRootNode"];
      List<int> sNodesRoot = Utility.ExplodeT<int>(sNodeRootsString, ",", " ", true);
      if (sNodesRoot == null || sNodesRoot.Count == 0)
        return null;
      //
      List<int> rNodesGlobal = new List<int>();
      //
      using (FS_Operations fsOp = new FS_Operations(-1, false, true))
      {
        foreach (int versionFrozen in new int[] { 0, -1 })
        {
          FS_Operations.FS_TreeNodeData RootNode = new FS_Operations.FS_TreeNodeData();
          //
          // lettura del dataset attivo
          //
          List<FS_Operations.FS_TreeNodeData> foldersActive =
          (from vNode in fsOp.NodesActive<IKGD_VNODE>(versionFrozen, false).Where(n => n.flag_folder)
           where fsOp.NodesActive<IKGD_VNODE>(versionFrozen, false).Where(n => n.flag_folder).Any(n => n.parent.Value == vNode.folder)
           select new FS_Operations.FS_TreeNodeData { vNode = vNode }).ToList();
          //
          // join tra tutti i nodi presenti
          //
          var joins = from frag in foldersActive.Where(n => n.Parent == null)
                      from node in foldersActive
                      where node.vNode.folder == frag.vNode.parent.Value
                      select new { node = node, frag = frag };
          foreach (var join in joins)
          {
            if (sNodesRoot.Contains(join.node.vNode.snode))
            {
              join.frag.Parent = RootNode;
              RootNode.Nodes.Add(join.frag);
              continue;
            }
            join.frag.Parent = join.node;
            join.node.Nodes.Add(join.frag);
          }
          //
          // eliminazione dei rami secchi che non portano a collezioni
          //
          while (foldersActive.RemoveAll(n => n.Parent == null && !sNodesRoot.Contains(n.vNode.snode)) > 0)
          { }
          List<int> rNodesState = foldersActive.Select(n => n.vNode.rnode).Distinct().ToList();
          rNodesGlobal = rNodesGlobal.Concat(rNodesState).Distinct().ToList();
        }
      }
      //
      return rNodesGlobal;
    }


  }



}
