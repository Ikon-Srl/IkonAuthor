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

using org.apache.lucene;
using org.apache.lucene.analysis;
using org.apache.lucene.analysis.standard;
using org.apache.lucene.document;
using org.apache.lucene.index;
using org.apache.lucene.queryParser;
using org.apache.lucene.search;
using org.apache.lucene.search.spans;
using org.apache.lucene.search.highlight;
using org.apache.lucene.store;
using org.apache.lucene.util;

using Ikon.GD;
using Ikon.Filters;
using Ikon.Auth;
using IKGD.Library.Resources;





namespace Ikon.IndexerV1
{


  //
  // classe di gestione dell'indicizzazione con supporto automatico per l'imperonation nel caso
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
    public int MaxResults { get { return 100; } }


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
        GC.SuppressFinalize(this);
        this.disposed = true;
      }
    }

    protected virtual void Dispose(bool disposing)
    {
      if (disposing)
      {
        // clean up managed resources
        if (IKGD_IndexSearcher != null)
        {
          IKGD_IndexSearcher.close();
        }
        if (IKGD_IndexWriter != null)
        {
          IKGD_IndexWriter.flush();
          IKGD_IndexWriter.close();
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
    { }


    public LuceneIndexer(bool openForUpdate)
    {
      IKGD_IndexDirectoryPath = IKGD_Config.AppSettings["LuceneShareIndexPath"] ?? "~/App_Data/Indexes";
      if (IKGD_IndexDirectoryPath.StartsWith("~") || IKGD_IndexDirectoryPath.StartsWith("/"))
        IKGD_IndexDirectoryPath = Utility.vPathMap(IKGD_IndexDirectoryPath);
      //
      ImpersonationSupport();
      //
      IKGD_IndexTainted = false;
      IKGD_IndexDir = FSDirectory.getDirectory(IKGD_IndexDirectoryPath);
      IKGD_Analizer = new StandardAnalyzer();
      //
      if (openForUpdate)
      {
        IKGD_IndexWriter = new IndexWriter(IKGD_IndexDir, IKGD_Analizer);
        IKGD_IndexWriter.setMaxFieldLength(1000000);
        //IKGD_IndexWriter.setMergeFactor(10);
      }
      //
      ReOpenSearcher();
      //
    }


    public void ReOpenSearcher()
    {
      try
      {
        if (IKGD_IndexSearcher != null)
          IKGD_IndexSearcher.close();
        IKGD_IndexSearcher = new IndexSearcher(IKGD_IndexDir);
        IKGD_IndexReader = IKGD_IndexSearcher.getIndexReader();
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
      string server = IKGD_Config.AppSettings["LuceneShareServer"];
      string user = IKGD_Config.AppSettings["LuceneShareUserName"];
      string pass = IKGD_Config.AppSettings["LuceneSharePassword"];
      if (string.IsNullOrEmpty(server) || string.IsNullOrEmpty(user) || string.IsNullOrEmpty(pass))
        return UsingImpersonation;
      //
      impersonationContext = NetworkSecurity.ImpersonateUser(server, user, pass, LogonType.LOGON32_LOGON_NEW_CREDENTIALS, LogonProvider.LOGON32_PROVIDER_DEFAULT);
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
        IKGD_IndexWriter.setMergeScheduler(new SerialMergeScheduler());
        //
        IKGD_IndexWriter.flush();
        IKGD_IndexWriter.optimize(true);
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
      try { return IKGD_IndexReader.numDocs(); }
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
        TermEnum termEnum = IKGD_IndexReader.terms(new Term(fieldName, string.Empty));
        Term term = null;
        while ((term = termEnum.term()) != null && term.field() == fieldName)
        {
          if (!ignoreEmpty || !string.IsNullOrEmpty(term.text()))
            if (!lista.Contains(term.text()))
              lista.Add(term.text());
          //int term_freq = termEnum.DocFreq();
          if (!termEnum.next())
            break;
        }
        termEnum.close();
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
          TermDocs tds = IKGD_IndexReader.termDocs(new Term(key, val));
          while (tds != null && tds.next())
            idxs.Add(tds.doc());
          if (tds != null)
            tds.close();
          if (ip == 0)
            docIdxs.AddRange(idxs);
          else
            docIdxs = new List<int>(Enumerable.Intersect(docIdxs, idxs));
        }
        foreach (int idx in docIdxs)
        {
          string val = IKGD_IndexReader.document(idx).getField(fieldName).stringValue();
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
    public bool IKGD_AddResource(FS_Operations.FS_NodeInfo fsNode, string Title, string Text)
    {
      if (IKGD_IndexWriter == null)
        return false;
      try
      {
        bool flag_published = fsNode.vData.flag_published;
        bool flag_current = fsNode.vData.flag_current;
        string iNode_version = string.Empty;
        //
        if (fsNode.iNode != null)
        {
          iNode_version = fsNode.iNode.version.ToString();
          flag_published &= fsNode.iNode.flag_published;
          flag_current |= fsNode.iNode.flag_current;
        }
        //
        string textStream = Text;
        if (textStream.IndexOf(Title, StringComparison.OrdinalIgnoreCase) == -1)
          textStream += "\n" + Title;
        //
        //string date_str = DateTools.dateToString(ToJavaDate(fsNode.vData.date_node), DateTools.Resolution.MILLISECOND);
        string date_str = fsNode.vData.date_node.ToString("u");
        //
        Document doc = new Document();
        doc.add(new Field("guid", Guid.NewGuid().ToString(), Field.Store.YES, Field.Index.UN_TOKENIZED));  // per un piu' facile accesso ai singoli record
        doc.add(new Field("rNode", fsNode.vData.rnode.ToString(), Field.Store.YES, Field.Index.UN_TOKENIZED));
        doc.add(new Field("vData", fsNode.vData.version.ToString(), Field.Store.YES, Field.Index.UN_TOKENIZED));
        doc.add(new Field("iNode", iNode_version, Field.Store.YES, Field.Index.UN_TOKENIZED));
        doc.add(new Field("area", fsNode.vData.area, Field.Store.YES, Field.Index.UN_TOKENIZED));
        doc.add(new Field("manager_type", fsNode.vData.manager_type ?? string.Empty, Field.Store.YES, Field.Index.UN_TOKENIZED));
        doc.add(new Field("flag_published", flag_published.ToString(), Field.Store.YES, Field.Index.UN_TOKENIZED));
        doc.add(new Field("flag_current", flag_current.ToString(), Field.Store.YES, Field.Index.UN_TOKENIZED));
        doc.add(new Field("flag_unstructured", fsNode.vData.flag_unstructured.ToString(), Field.Store.YES, Field.Index.UN_TOKENIZED));
        doc.add(new Field("language", string.Empty, Field.Store.YES, Field.Index.UN_TOKENIZED));
        doc.add(new Field("date", date_str, Field.Store.YES, Field.Index.UN_TOKENIZED));
        //
        doc.add(new Field("title", Title, Field.Store.YES, Field.Index.TOKENIZED));
        doc.add(new Field("text", textStream, Field.Store.YES, Field.Index.TOKENIZED));
        //
        IKGD_IndexWriter.addDocument(doc);
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
          TermDocs tdsEnum = IKGD_IndexReader.termDocs(new Term("rNode", rNode.ToString()));
          while (tdsEnum != null && tdsEnum.next())
            docs.Add(IKGD_IndexReader.document(tdsEnum.doc()));
          if (tdsEnum != null)
            tdsEnum.close();
          //
          var docsInfo = docs.Select(d => new { doc = d, vData = d.getField("vData").stringValue(), iNode = d.getField("iNode").stringValue() }).ToList();
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


    //
    // rigenerazione completa dell'indice
    //
    public int IKGD_ReindexAll()
    {
      int vDataMax = 0;
      int iNodeMax = 0;
      IKGD_SEARCH searchrec = new IKGD_SEARCH { date_op = DateTime.Now, message = "full rebuild", server = string.Empty, status = "running", version_vnode = 0, version_vdata = vDataMax, version_inode = iNodeMax };
      using (FS_Operations fsOp = new FS_Operations())
      {
        fsOp.DB.IKGD_SEARCHes.InsertOnSubmit(searchrec);
        fsOp.DB.SubmitChanges();
      }
      int res = IKGD_PartialUpdateWorker(vDataMax, iNodeMax, out vDataMax, out iNodeMax);
      using (FS_Operations fsOp = new FS_Operations())
      {
        fsOp.DB.IKGD_SEARCHes.Attach(searchrec);
        //
        searchrec.version_vdata = vDataMax;
        searchrec.version_inode = iNodeMax;
        searchrec.message = string.Format("Full rebuild, resource count: {0}", res);
        searchrec.status = "done";
        //
        fsOp.DB.SubmitChanges();
        //
        var recordsToClear = fsOp.DB.IKGD_SEARCHes.Where(r => r.status == "running");
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
      int psStep = -1;
      try
      {
        using (FS_Operations fsOp = new FS_Operations(-1, false, true))
        {
          IKGD_SEARCH searchrec = null;
          TransactionOptions trsop = new TransactionOptions();
          trsop.IsolationLevel = System.Transactions.IsolationLevel.Serializable;
          trsop.Timeout = TimeSpan.FromSeconds(300);
          using (TransactionScope ts = new TransactionScope(TransactionScopeOption.Required, trsop))
          {
            psStep = -2;
            //
            var openRun = fsOp.DB.IKGD_SEARCHes.Where(r => r.status == "running").OrderByDescending(r => r.id).FirstOrDefault();
            if (openRun != null && (DateTime.Now - openRun.date_op) < TimeSpan.FromSeconds(3600))
              return -100;
            var lastRun = fsOp.DB.IKGD_SEARCHes.OrderByDescending(r => r.id).FirstOrDefault();
            if (lastRun == null)
              return -101;
            searchrec = new IKGD_SEARCH { date_op = DateTime.Now, message = "index updating", server = string.Empty, status = "running", version_vnode = lastRun.version_vnode, version_vdata = lastRun.version_vdata, version_inode = lastRun.version_inode };
            //
            fsOp.DB.IKGD_SEARCHes.InsertOnSubmit(searchrec);
            fsOp.DB.SubmitChanges();
            //
            ts.Complete();
          }
          //
          psStep = -3;
          //
          int vDataMax = searchrec.version_vdata.Value;
          int iNodeMax = searchrec.version_inode.Value;
          int res = IKGD_PartialUpdateWorker(vDataMax, iNodeMax, out vDataMax, out iNodeMax);
          //
          psStep = -4;
          //
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
      catch { }
      return psStep;
    }


    //
    // update parziale dell'indice
    //
    public int IKGD_PartialUpdateWorker(int last_vData, int last_iNode, out int vDataMax, out int iNodeMax)
    {
      int psStep = -1;
      vDataMax = last_vData;
      iNodeMax = last_iNode;
      if (IKGD_IndexWriter == null)
        return psStep;
      //
      // controllo se si tratta di un nuovo indice eventualmente derivante da una cancellazione dei files
      // e predispongo per il reset delle info anche sul DB
      //
      int docCount = IKGD_IndexWriter.docCount();
      if (docCount == 0)
      {
        vDataMax = last_vData = 0;
        iNodeMax = last_iNode = 0;
      }
      //
      int searchCount = 0;
      try
      {
        //
        Dictionary<string, Type> resTypes = IKGD_ResourceTypeBase.FindRegisteredResourceTypes().ToDictionary(t => t.Name);
        IKGD_HtmlCleaner xHtmlCleaner = new IKGD_HtmlCleaner();
        //CMS_HtmlCleaner HtmlCleaner = new CMS_HtmlCleaner(CMS_HtmlCleaner.stripMode.NoHtml);
        //
        using (FS_Operations fsOp = new FS_Operations(-1, false, true))
        {
          //
          psStep = -2;
          //
          // NB: sono filtrate le risorse che hanno vData o iNode ma non hanno piu' nessun vNode attivo
          //
          var rNodes01 = fsOp.DB.IKGD_VDATAs.Where(n => n.version > last_vData && n.version_frozen != null).Select(n => n.rnode);
          var rNodes02 = fsOp.DB.IKGD_INODEs.Where(n => n.version > last_iNode && n.version_frozen != null).Select(n => n.rnode);
          var rNodes03 =
            from vData in fsOp.NodesActive<IKGD_VDATA>(-1, false).Where(n => !n.flag_inactive && !n.flag_archived).Where(n => n.version > last_vData)
            where fsOp.NodesActive<IKGD_VNODE>(-1, false).Any(n => n.rnode == vData.rnode && !n.flag_folder)  // NB filtro anche i folders
            select vData.rnode;
          var rNodes04 =
            from iNode in fsOp.NodesActive<IKGD_INODE>(-1, false).Where(n => n.version > last_iNode)
            from vData in fsOp.NodesActive<IKGD_VDATA>(-1, false).Where(n => n.rnode == iNode.rnode).Where(n => !n.flag_inactive && !n.flag_archived)
            where fsOp.NodesActive<IKGD_VNODE>(-1, false).Any(n => n.rnode == iNode.rnode && !n.flag_folder)  // NB filtro anche i folders
            select vData.rnode;
          var rNodesList = rNodes01.Union(rNodes02).Union(rNodes03).Union(rNodes04).ToList();
          //
          psStep = -3;
          //
          // pulizia parziale o totale degli indici di Lucene prima del partial/full update
          //
          try
          {
            if (last_vData == 0 && last_iNode == 0)
            {
              //
              // cancellazione dell'indice corrente
              // full clean/update
              //
              IKGD_IndexWriter.deleteDocuments(new Term("flag_unstructured", true.ToString()));
              IKGD_IndexWriter.deleteDocuments(new Term("flag_unstructured", false.ToString()));
            }
            else
            {
              //
              // cancellazione parziale dell'indice corrente (solo le risorse che saranno aggiornate)
              // partial clean/update
              //
              foreach (int rNode in rNodesList)
                IKGD_IndexWriter.deleteDocuments(new Term("rNode", rNode.ToString()));
            }
          }
          catch { return psStep; }
          //
          psStep = -4;
          //
          // lista dei folders visibili e non da mettere in lost+found per escludere i
          // contenuti non raggiungibili
          //
          List<int> rNodesFolderActive = null;
          try { rNodesFolderActive = LuceneIndexerSupport.ScanFolderStructure(); }
          catch { }
          //
          // loop sulle risorse trovate dopo la prepulizia degli indici
          //
          int chunkSize = 500;
          for (int idx = 0; idx < rNodesList.Count; idx += chunkSize)
          {
            //
            var rNodeSet = rNodesList.Skip(idx).Take(chunkSize).ToList();
            //
            //TODO: filtrare meglio le risorse, 1 o 0 per tutti i check non funziona!
            var nodes_publ2 =
              from vData in fsOp.NodesActive<IKGD_VDATA>(0, false).Where(n => !n.flag_inactive && !n.flag_archived).Where(n => n.manager_type != null && n.manager_type != string.Empty).Where(n => rNodeSet.Contains(n.rnode))
              from iNode in fsOp.NodesActive<IKGD_INODE>(0, false).Where(n => n.rnode == vData.rnode).DefaultIfEmpty()
              from vNode in fsOp.NodesActive<IKGD_VNODE>(0, false).Where(n => n.rnode == vData.rnode)
              select new FS_Operations.FS_NodeInfo { vData = vData, iNode = iNode, vNode = vNode };
            var nodes_curr2 =
              from vData in fsOp.NodesActive<IKGD_VDATA>(-1, false).Where(n => !n.flag_inactive && !n.flag_archived).Where(n => n.manager_type != null && n.manager_type != string.Empty).Where(n => rNodeSet.Contains(n.rnode))
              from iNode in fsOp.NodesActive<IKGD_INODE>(-1, false).Where(n => n.rnode == vData.rnode).DefaultIfEmpty()
              from vNode in fsOp.NodesActive<IKGD_VNODE>(-1, false).Where(n => n.rnode == vData.rnode)
              select new FS_Operations.FS_NodeInfo { vData = vData, iNode = iNode, vNode = vNode };
            var nodes_all2 = nodes_publ2.Union(nodes_curr2);
            //
            foreach (FS_Operations.FS_NodeInfo node in nodes_all2)
            {
              //
              // e' ammesso solo un run per ciascun rNode
              // ma nella lista potrebbero esserci piu' link simbolici allo stesso contenuto
              // ed eventualmente anche solo da folder irraggiungibili
              //
              if (!rNodeSet.Contains(node.vData.rnode))
                continue;
              if (rNodesFolderActive != null)
              {
                if (node.vNode != null && node.vNode.parent != null && !rNodesFolderActive.Contains(node.vNode.parent.Value))
                  continue;
              }
              //
              // passati i filtri lo tolgo dalla lista degli rNodes processabili
              //
              rNodeSet.Remove(node.vData.rnode);
              //
              if (!resTypes.ContainsKey(node.vData.manager_type))
                continue;
              Type resType = resTypes[node.vData.manager_type];
              IKGD_ResourceTypeBase resObj = IKGD_ResourceTypeBase.CreateInstance(resType);
              if (resObj == null || !resObj.IsIndexable)
                continue;
              string Title = resObj.GetSearchInfoTitle(fsOp, node);
              string Text = resObj.GetSearchInfoText(fsOp, node, xHtmlCleaner);
              if (Title == null || Text == null)
                continue;
              bool res02 = IKGD_AddResource(node, Title, Text);
              if (res02)
                searchCount++;
            }
            try { vDataMax = nodes_all2.Max(n => n.vData.version); }
            catch { }
            try { iNodeMax = nodes_all2.Max(n => n.iNode.version); }
            catch { }
          }
          //
          psStep = -4;
          //
        }
        //
        IKGD_IndexOptimize(true);
        //
        psStep = -5;
        //
        ReOpenSearcher();
        //
        return searchCount;
      }
      catch { }
      return psStep;
    }



    //
    // update parziale dell'indice versione per DEBUG (attenzione che lascia gli indici incoerenti...)
    //
    public XElement IKGD_PartialUpdateDebugger(int? maxRecords, int last_vData, int last_iNode, out int vDataMax, out int iNodeMax)
    {
      //
      XElement xResult = new XElement("Indexer");
      XElement xMessages = new XElement("Messages");
      XElement xRecords = new XElement("Records");
      XElement xErrors = new XElement("Errors");
      xResult.Add(xMessages);
      xResult.Add(xErrors);
      xResult.Add(xRecords);
      //
      maxRecords = maxRecords ?? int.MaxValue;
      vDataMax = last_vData;
      iNodeMax = last_iNode;
      if (IKGD_IndexWriter == null)
      {
        xErrors.Add(new XElement("abort", "IKGD_IndexWriter == null"));
        return xResult;
      }
      //
      // controllo se si tratta di un nuovo indice eventualmente derivante da una cancellazione dei files
      // e predispongo per il reset delle info anche sul DB
      //
      int docCount = IKGD_IndexWriter.docCount();
      if (docCount == 0)
      {
        vDataMax = last_vData = 0;
        iNodeMax = last_iNode = 0;
      }
      //
      int searchCount = 0;
      try
      {
        //
        Dictionary<string, Type> resTypes = IKGD_ResourceTypeBase.FindRegisteredResourceTypes().ToDictionary(t => t.Name);
        IKGD_HtmlCleaner xHtmlCleaner = new IKGD_HtmlCleaner();
        //CMS_HtmlCleaner HtmlCleaner = new CMS_HtmlCleaner(CMS_HtmlCleaner.stripMode.NoHtml);
        //
        using (FS_Operations fsOp = new FS_Operations(-1, false, true))
        {
          //
          // NB: sono filtrate le risorse che hanno vData o iNode ma non hanno piu' nessun vNode attivo
          //
          var rNodes01 = fsOp.DB.IKGD_VDATAs.Where(n => n.version > last_vData && n.version_frozen != null).Select(n => n.rnode);
          var rNodes02 = fsOp.DB.IKGD_INODEs.Where(n => n.version > last_iNode && n.version_frozen != null).Select(n => n.rnode);
          var rNodes03 =
            from vData in fsOp.NodesActive<IKGD_VDATA>(-1, false).Where(n => !n.flag_inactive && !n.flag_archived).Where(n => n.version > last_vData)
            where fsOp.NodesActive<IKGD_VNODE>(-1, false).Any(n => n.rnode == vData.rnode && !n.flag_folder)  // NB filtro anche i folders
            select vData.rnode;
          var rNodes04 =
            from iNode in fsOp.NodesActive<IKGD_INODE>(-1, false).Where(n => n.version > last_iNode)
            from vData in fsOp.NodesActive<IKGD_VDATA>(-1, false).Where(n => n.rnode == iNode.rnode).Where(n => !n.flag_inactive && !n.flag_archived)
            where fsOp.NodesActive<IKGD_VNODE>(-1, false).Any(n => n.rnode == iNode.rnode && !n.flag_folder)  // NB filtro anche i folders
            select vData.rnode;
          var rNodesList = rNodes01.Union(rNodes02).Union(rNodes03).Union(rNodes04).Take(maxRecords.Value).ToList();
          //
          xMessages.Add(new XElement("VFS_rNodes", rNodesList.Count));
          //
          // pulizia parziale o totale degli indici di Lucene prima del partial/full update
          //
          try
          {
            if (last_vData == 0 && last_iNode == 0)
            {
              //
              // cancellazione dell'indice corrente
              // full clean/update
              //
              IKGD_IndexWriter.deleteDocuments(new Term("flag_unstructured", true.ToString()));
              IKGD_IndexWriter.deleteDocuments(new Term("flag_unstructured", false.ToString()));
              xMessages.Add(new XElement("IKGD_IndexWriter", "full clean"));
            }
            else
            {
              //
              // cancellazione parziale dell'indice corrente (solo le risorse che saranno aggiornate)
              // partial clean/update
              //
              foreach (int rNode in rNodesList)
                IKGD_IndexWriter.deleteDocuments(new Term("rNode", rNode.ToString()));
              xMessages.Add(new XElement("IKGD_IndexWriter", "partial clean"));
            }
          }
          catch (Exception ex)
          {
            xErrors.Add(new XElement("exception", ex.Message));
            return xResult;
          }
          //
          xMessages.Add(new XElement("IKGD_IndexWriter", "cleaned"));
          //
          // lista dei folders visibili e non da mettere in lost+found per escludere i
          // contenuti non raggiungibili
          //
          List<int> rNodesFolderActive = null;
          try { rNodesFolderActive = LuceneIndexerSupport.ScanFolderStructure(); }
          catch { }
          //
          // loop sulle risorse trovate dopo la prepulizia degli indici
          //
          int chunkSize = 500;
          for (int idx = 0; idx < rNodesList.Count; idx += chunkSize)
          {
            //
            var rNodeSet = rNodesList.Skip(idx).Take(chunkSize).ToList();
            //
            //TODO: filtrare meglio le risorse, 1 o 0 per tutti i check non funziona!
            var nodes_publ2 =
              from vData in fsOp.NodesActive<IKGD_VDATA>(0, false).Where(n => !n.flag_inactive && !n.flag_archived).Where(n => n.manager_type != null && n.manager_type != string.Empty).Where(n => rNodeSet.Contains(n.rnode))
              from iNode in fsOp.NodesActive<IKGD_INODE>(0, false).Where(n => n.rnode == vData.rnode).DefaultIfEmpty()
              from vNode in fsOp.NodesActive<IKGD_VNODE>(0, false).Where(n => n.rnode == vData.rnode)
              select new FS_Operations.FS_NodeInfo { vData = vData, iNode = iNode, vNode = vNode };
            var nodes_curr2 =
              from vData in fsOp.NodesActive<IKGD_VDATA>(-1, false).Where(n => !n.flag_inactive && !n.flag_archived).Where(n => n.manager_type != null && n.manager_type != string.Empty).Where(n => rNodeSet.Contains(n.rnode))
              from iNode in fsOp.NodesActive<IKGD_INODE>(-1, false).Where(n => n.rnode == vData.rnode).DefaultIfEmpty()
              from vNode in fsOp.NodesActive<IKGD_VNODE>(-1, false).Where(n => n.rnode == vData.rnode)
              select new FS_Operations.FS_NodeInfo { vData = vData, iNode = iNode, vNode = vNode };
            var nodes_all2 = nodes_publ2.Union(nodes_curr2);
            //
            foreach (FS_Operations.FS_NodeInfo node in nodes_all2)
            {
              //
              // e' ammesso solo un run per ciascun rNode
              // ma nella lista potrebbero esserci piu' link simbolici allo stesso contenuto
              // ed eventualmente anche solo da folder irraggiungibili
              //
              if (!rNodeSet.Contains(node.vData.rnode))
                continue;
              XElement xNode = new XElement("node");
              xNode.SetAttributeValue("status", "skipped");
              if (node.vData != null)
              {
                xNode.SetAttributeValue("rNode", node.vData.rnode);
                xNode.SetAttributeValue("vData", node.vData.version);
              }
              if (node.iNode != null)
                xNode.SetAttributeValue("iNode", node.iNode.version);
              if (node.vNode != null)
                xNode.SetAttributeValue("vNode", node.vNode.version);
              //
              xRecords.Add(xNode);
              //
              if (rNodesFolderActive != null)
              {
                if (node.vNode != null && node.vNode.parent != null && !rNodesFolderActive.Contains(node.vNode.parent.Value))
                {
                  xNode.SetAttributeValue("status", "skipped: lost+found");
                  continue;
                }
              }
              //
              // passati i filtri lo tolgo dalla lista degli rNodes processabili
              //
              rNodeSet.Remove(node.vData.rnode);
              //
              if (!resTypes.ContainsKey(node.vData.manager_type))
                continue;
              Type resType = resTypes[node.vData.manager_type];
              IKGD_ResourceTypeBase resObj = IKGD_ResourceTypeBase.CreateInstance(resType);
              if (resObj == null || !resObj.IsIndexable)
                continue;
              xNode.SetAttributeValue("status", "text_processing: null");
              string Title = resObj.GetSearchInfoTitle(fsOp, node);
              string Text = resObj.GetSearchInfoText(fsOp, node, xHtmlCleaner);
              if (Title == null || Text == null)
                continue;
              xNode.SetAttributeValue("title", Title);
              xNode.Value = Utility.StringTruncate(Text, 500);
              xNode.SetAttributeValue("status", "save_lucene");
              bool res02 = IKGD_AddResource(node, Title, Text);
              xNode.SetAttributeValue("status", "OK");
              //xNode.Remove();
              if (res02)
                searchCount++;
            }
            try { vDataMax = nodes_all2.Max(n => n.vData.version); }
            catch { }
            try { iNodeMax = nodes_all2.Max(n => n.iNode.version); }
            catch { }
          }
          //
        }
        //
        xRecords.SetAttributeValue("itemsAll", xRecords.Elements("node").Count());
        xRecords.SetAttributeValue("itemsOK", xRecords.Elements("node").Where(x => x.AttributeValue("status") == "OK").Count());
        //
        IKGD_IndexOptimize(true);
        //
        ReOpenSearcher();
        //
        return xResult;
      }
      catch { }
      return xResult;
    }


    public IEnumerable<IKGD_LuceneHit> IKGD_Search(string strSearch, List<string> allowedAreas, string manager_type, int? sNodeFolder, DateTime? dateStart, DateTime? dateEnd, bool searchPreview, bool formatMatches)
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


    public IEnumerable<IKGD_LuceneHit> IKGD_SearchVFS(List<string> allowedAreas, string manager_type, int? sNodeFolder, DateTime? dateStart, DateTime? dateEnd, bool searchPreview)
    {
      if (allowedAreas != null && allowedAreas.Count == 0)
        yield break;
      dateStart = Utility.GetNullDateIfInvalidDateDB(dateStart);
      dateEnd = Utility.GetNullDateIfInvalidDateDB(dateEnd);
      //
      //
      Dictionary<string, Type> resTypes = IKGD_ResourceTypeBase.FindRegisteredResourceTypes().ToDictionary(t => t.Name);
      IKGD_HtmlCleaner xHtmlCleaner = new IKGD_HtmlCleaner();
      //CMS_HtmlCleaner HtmlCleaner = new CMS_HtmlCleaner(CMS_HtmlCleaner.stripMode.NoHtml);
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
          vDataFilterAll = vDataFilterAll.And(n => allowedAreas.Contains(n.area));
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
          lastF = (from vNode in fsOp.NodesActive<IKGD_VNODE>(true).Where(n => n.flag_folder)
                   from vData in fsOp.NodesActive<IKGD_VDATA>(true).Where(vDataFilterAll)
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
           select new FS_Operations.FS_NodeInfo { vNode = vNode, vData = vData, iNode = iNode }).Take(MaxResults);
        //
        foreach (FS_Operations.FS_NodeInfo node in fsNodes)
        {
          string Title = null;
          string Text = null;
          try
          {
            Type resType = resTypes[node.vData.manager_type];
            IKGD_ResourceTypeBase resObj = IKGD_ResourceTypeBase.CreateInstance(resType);
            if (resObj == null || !resObj.IsIndexable)
              continue;
            Title = resObj.GetSearchInfoTitle(fsOp, node);
            Text = resObj.GetSearchInfoText(fsOp, node, xHtmlCleaner);
            if (Title == null || Text == null)
              continue;
            //
            if (!resObj.IsUnstructured)
            {
              Text = xHtmlCleaner.ParseAndTruncate(Text, textLen, true, "...");
            }
            else if (!string.IsNullOrEmpty(Text))
            {
              Text = IKGD_HtmlCleaner.TruncateSimple(Text, textLen, true, "...");
            }
            Text = Text ?? string.Empty;
          }
          catch { }
          if (Title != null && Text != null)
            yield return new IKGD_LuceneHit(node, Title, Text);
        }
      }
    }


    public IEnumerable<IKGD_LuceneHit> IKGD_SearchLucene(string strSearch, List<string> allowedAreas, string manager_type, int? sNodeFolder, DateTime? dateStart, DateTime? dateEnd, bool searchPreview, bool formatMatches)
    {
      if (string.IsNullOrEmpty(strSearch))
        yield break;
      if (allowedAreas != null && allowedAreas.Count == 0)
        yield break;
      dateStart = Utility.GetNullDateIfInvalidDateDB(dateStart);
      dateEnd = Utility.GetNullDateIfInvalidDateDB(dateEnd);
      //
      BooleanQuery queryFilterFrags = new BooleanQuery();
      //
      // selettore per lo stato di pubblicazione richiesto
      //
      queryFilterFrags.add(new TermQuery(new Term(searchPreview ? "flag_current" : "flag_published", true.ToString())), BooleanClause.Occur.MUST);
      //
      bool? unstructuredSelector = null;
      if (unstructuredSelector.HasValue)
        queryFilterFrags.add(new TermQuery(new Term("flag_unstructured", unstructuredSelector.Value.ToString())), BooleanClause.Occur.MUST);
      //
      if (!string.IsNullOrEmpty(manager_type))
        queryFilterFrags.add(new TermQuery(new Term("manager_type", manager_type)), BooleanClause.Occur.MUST);
      //
      if (dateStart != null || dateEnd != null)
      {
        DateTime dateStartL = dateStart ?? DateTime.MinValue;
        DateTime dateEndL = dateEnd ?? DateTime.MaxValue;
        queryFilterFrags.add(new RangeQuery(new Term("date", dateStartL.ToString("u")), new Term("date", dateEndL.ToString("u")), true), BooleanClause.Occur.MUST);
      }
      //
      QueryParser queryParser = new QueryParser("text", IKGD_Analizer);
      Query query = queryParser.parse(strSearch);
      QueryWrapperFilter queryFilter = new QueryWrapperFilter(queryFilterFrags);
      FilteredQuery queryFiltered = new FilteredQuery(query, queryFilter);
      Query query_rewrite = query.rewrite(IKGD_IndexReader);  //required to expand search terms
      //
      Hits hits = IKGD_IndexSearcher.search(queryFiltered);
      int results_count = hits.length();
      //
      SimpleHTMLFormatter formatter = new SimpleHTMLFormatter("<b>", "</b>");
      SimpleFragmenter fragmenter = new SimpleFragmenter(Utility.TryParse<int>(IKGD_Config.AppSettings["LuceneFragmentsLength"], 100));
      Highlighter highlighter = new Highlighter(formatter, new QueryScorer(query, "text"));
      highlighter.setTextFragmenter(fragmenter);
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
            vDataFilterAll = vDataFilterAll.And(n => allowedAreas.Contains(n.area));
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
            lastF = (from vNode in fsOp.NodesActive<IKGD_VNODE>(true).Where(n => n.flag_folder)
                     from vData in fsOp.NodesActive<IKGD_VDATA>(true).Where(vDataFilterAll)
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
        HitIterator item_iterator = (HitIterator)hits.iterator();
        while (item_iterator.hasNext())
        {
          Hit item = (Hit)item_iterator.next();
          if (item == null)
            continue;
          IKGD_LuceneHit hit = new IKGD_LuceneHit(item, formatMatches ? this : null);
          if (allowedAreas != null && !allowedAreas.Contains(hit.area))
            continue;
          if (rNodes != null && !rNodes.Contains(hit.rNode))
            continue;
          if (counter++ >= MaxResults)
            break;
          yield return hit;
        }
      }
    }


    public IEnumerable<IKGD_LuceneHit> IKGD_SearchLuceneDBG(string strSearch, bool searchPreview, bool formatMatches)
    {
      if (string.IsNullOrEmpty(strSearch))
        yield break;
      //
      BooleanQuery queryFilterFrags = new BooleanQuery();
      queryFilterFrags.add(new TermQuery(new Term(searchPreview ? "flag_current" : "flag_published", true.ToString())), BooleanClause.Occur.MUST);
      //
      QueryParser queryParser = new QueryParser("text", IKGD_Analizer);
      Query query = queryParser.parse(strSearch);
      QueryWrapperFilter queryFilter = new QueryWrapperFilter(queryFilterFrags);
      FilteredQuery queryFiltered = new FilteredQuery(query, queryFilter);
      Query query_rewrite = query.rewrite(IKGD_IndexReader);  //required to expand search terms
      //
      Hits hits = IKGD_IndexSearcher.search(queryFiltered);
      int results_count = hits.length();
      //
      using (FS_Operations fsOp = new FS_Operations(searchPreview ? -1 : 0, false, true))
      {
        int counter = 0;
        HitIterator item_iterator = (HitIterator)hits.iterator();
        while (item_iterator.hasNext())
        {
          Hit item = (Hit)item_iterator.next();
          if (item == null)
            continue;
          IKGD_LuceneHit hit = new IKGD_LuceneHit(item, formatMatches ? this : null);
          if (counter++ >= MaxResults)
            break;
          yield return hit;
        }
      }
    }



  }  // LuceneIndexer



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
    public List<ResourceData> resourceData { get; set; }
    public object paths { get; set; }  // ha difficolta' a passare i dati VFS attraverso il webservice

    //
    // constructors
    //
    public IKGD_LuceneHit()
    {
      resourceData = new List<ResourceData>();
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


    public IKGD_LuceneHit(Hit item, LuceneIndexer indexer)
      : this()
    {
      try
      {
        Document document = item.getDocument();
        doc = document;
        id = item.getId();
        score = item.getScore();
        //
        rNode = Utility.TryParse<int>(document.getField("rNode").stringValue());
        iNode = Utility.TryParse<int>(document.getField("iNode").stringValue());
        vData = Utility.TryParse<int>(document.getField("vData").stringValue());
        guid = document.getField("guid").stringValue();
        area = document.getField("area").stringValue();
        manager_type = document.getField("manager_type").stringValue();
        language = document.getField("language").stringValue();
        flag_published = Utility.TryParse<bool>(document.getField("flag_published").stringValue(), false);
        flag_current = Utility.TryParse<bool>(document.getField("flag_current").stringValue(), false);
        flag_unstructured = Utility.TryParse<bool>(document.getField("flag_unstructured").stringValue(), false);
        date = Utility.TryParse<DateTime>(document.getField("date").stringValue(), DateTime.Now);
        //
        title = document.getField("title").stringValue();
        text = string.Empty;
        string textStream = document.getField("text").stringValue();
        if (indexer != null && indexer.IKGD_Highlighter != null)
        {
          int fragments_count = Utility.TryParse<int>(IKGD_Config.AppSettings["LuceneFragmentsCount"], 3);
          text = Utility.Implode(indexer.IKGD_Highlighter.getBestFragments(indexer.IKGD_Analizer, "text", textStream, fragments_count), " ... ");
        }
        else
        {
          int len = Math.Max(Utility.TryParse<int>(IKGD_Config.AppSettings["LuceneFragmentsCount"], 3) * Utility.TryParse<int>(IKGD_Config.AppSettings["LuceneFragmentsLength"], 100), 50);
          text = Utility.StringTruncate(textStream, len, "...");
        }
      }
      catch { }
    }


    //
    // classe di supporto per la generazione dei link alle risorse
    //
    public class ResourceData
    {
      public string path { get; set; }
      public int sNodeResource { get; set; }
      public int sNodeFolder { get; set; }
      //
      // TODO: aggiungere un riferimento al tipo per istanziare l'oggetto e creare l'url di lancio
      //
    }
  }



  //
  // classe di supporto per l'indexer e i webservices della UI
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
        Dictionary<string, Type> resTypes = IKGD_ResourceTypeBase.FindRegisteredResourceTypes().ToDictionary(t => t.Name);
        foreach (Type resType in resTypes.Values)
        {
          IKGD_ResourceTypeBase resObj = IKGD_ResourceTypeBase.CreateInstance(resType);
          if (resObj == null || !resObj.IsIndexable)
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




  public class PathExt
  {
    public string Path { get; set; }
    int? sNodeResource { get; set; }
    int? sNodeFolder { get; set; }
    int? sNodeCollection { get; set; }
    string manager_type { get; set; }
  }


}
