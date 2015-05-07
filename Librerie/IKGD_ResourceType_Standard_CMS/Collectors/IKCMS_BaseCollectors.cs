/*
 * 
 * Ikon CMS
 * 
 * Copyright (C) 2010 Ikon Srl
 * Tutti i Diritti Riservati. All Rights Reserved
 * 
 */


using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Data;
using System.Configuration;
using System.Web;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security;
using System.Security.Principal;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;
using System.ComponentModel;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;  // assembly System.ServiceModel.Web
using System.Linq.Expressions;
using System.Web.Caching;
using LinqKit;

using Ikon;
using Ikon.Log;
using Ikon.Support;
using Ikon.GD;


namespace Ikon.IKGD.Library.Collectors
{
  using Ikon.IKGD.Library.Resources;
  using Ikon.IKCMS.Library.Resources;
  using Ikon.IKCMS;
  using Newtonsoft.Json;



  class BootStrapCollectors : IBootStrapperTask
  {
    public void Execute()
    {
      AutoMapperWrapper.AutoRegister<FS_Operations.FS_NodeInfo_Interface, IKCMS_TreeBrowser_fsNodeElement_Interface>();
      AutoMapperWrapper.AutoRegister<FS_Operations.FS_NodeInfo, IKCMS_TreeBrowser_fsNodeElement_Interface>();
    }
  }


  //
  // interface base per le funzionalita' associate agli archivi
  //
  public interface IKGD_Archive_Action_Interface
  {
  }


  //
  // interface base per la definizione dei filters per i moduli tipo browse
  //
  public interface IKGD_Archive_Filter_Interface : IKGD_Archive_Action_Interface
  {
    bool FilterBrowsableItemsOnly { get; }

    Expression<Func<IKGD_VNODE, bool>> vNodeFilter { get; }
    Expression<Func<IKGD_VDATA, bool>> vDataFilter { get; }
  }


  //
  // interface base per la definizione dei collectors per i teaser tipo news/eventi
  //
  public interface IKGD_Teaser_Collector_InterfaceNG : IKGD_Archive_Filter_Interface, IKGD_Archive_Action_Interface { }  // ci sono dei problemi con la reflection e lo scan per generic interfaces cosi' definiamo un interface custom
  public interface IKGD_Teaser_Collector_Interface<fsNodeT> : IKGD_Teaser_Collector_InterfaceNG
    where fsNodeT : class, FS_Operations.FS_NodeInfo_Interface
  {
    bool ScanSubTree { get; }  // se il teaser non ha relations ad archivi allora si esegue lo scan su tutto il VFS
    IQueryable<fsNodeT> Sorter(IQueryable<fsNodeT> items);
  }


  //
  // specificando questa interface dopo il sort e tage degli items il sort verra' invertito
  // da usare per alcuni sort tipo: selezione degli N prossimi eventi
  //
  public interface IKGD_Collector_ReverseResultsAfterTake { }


  //
  // interface base per la definizione dei collectors per i moduli tipo browse
  //
  public interface IKGD_Archive_Collector_InterfaceNG : IKGD_Archive_Action_Interface { }  // ci sono dei problemi con la reflection e lo scan per generic interfaces cosi' definiamo un interface custom
  public interface IKGD_Archive_Collector_Interface<fsNodeT> : IKGD_Archive_Collector_InterfaceNG
    where fsNodeT : class, FS_Operations.FS_NodeInfo_Interface
  {
    bool RenderTree { get; }
    bool ScanSubTree { get; }
    bool ForceUrlsToLeaf { get; }
    List<bool> AggregatorsOrderByDescendant { get; }
    List<Func<fsNodeT, object>> Aggregators { get; }
    List<Func<fsNodeT, string>> Formatters { get; }
    IQueryable<fsNodeT> Sorter(IQueryable<fsNodeT> items);
    //IQueryable<FS_Operations.FS_NodeInfo_Interface> Sorter(IQueryable<FS_Operations.FS_NodeInfo_Interface> items);
  }


  //
  // interface base per la definizione dei menu formatter per i moduli tipo browse
  //
  public interface IKGD_Archive_Formatter_Interface : IKGD_Archive_Action_Interface
  {
  }



  //
  // interface base per la definizione degli elementi che definiscono il tree generato dai collectors
  // dovrebbe trattarsi di elementi di un tree mantenuto in cache
  //
  //public interface IKCMS_TreeBrowser_Element_Interface : IEquatable<IKCMS_TreeBrowser_Element_Interface>
  public interface IKCMS_TreeBrowser_Element_Interface
  {
    int sNode { get; set; }
    int? folderNode { get; set; }
    object frag { get; set; }
    string fragString { get; set; }
    string url { get; set; }
  }


  public class IKCMS_TreeBrowser_Element : IKCMS_TreeBrowser_Element_Interface
  {
    public int sNode { get; set; }
    public int? folderNode { get; set; }
    public object frag { get; set; }
    public string fragString { get; set; }
    public string url { get; set; }


    /*
    public override bool Equals(object obj)
    {
      if (obj == null) return base.Equals(obj);
      if (obj is IKCMS_TreeBrowser_Element_Interface)
        return this.Equals((IKCMS_TreeBrowser_Element_Interface)obj);
      return false;
    }
    public bool Equals(IKCMS_TreeBrowser_Element_Interface obj)
    {
      if (ReferenceEquals(null, obj)) return false;
      if (ReferenceEquals(this, obj)) return true;
      return Equals(obj.sNode, sNode) && Equals(obj.folderNode, folderNode) && Equals(obj.fragString, fragString) && Equals(obj.url, url);
      //return Equals(obj.sNode, sNode) && Equals(obj.folderNode, folderNode) && Equals(obj.fragString, fragString) && Equals(obj.url, url) && Equals(obj.frag, frag);
    }
    public static bool operator ==(IKCMS_TreeBrowser_Element obj1, IKCMS_TreeBrowser_Element_Interface obj2)
    {
      if (ReferenceEquals(obj1, obj2)) return true;
      if (ReferenceEquals(null, obj1)) return false;
      return obj1.Equals(obj2);
    }
    public static bool operator !=(IKCMS_TreeBrowser_Element obj1, IKCMS_TreeBrowser_Element_Interface obj2)
    {
      if (ReferenceEquals(obj1, obj2)) return false;
      if (ReferenceEquals(null, obj1)) return true;
      return (!obj1.Equals(obj2));
    }
    public override int GetHashCode()
    {
      return TupleW.GetHashCode(sNode, folderNode, frag, fragString, url);
    }
    */


    public override string ToString()
    {
      return string.Format("{0}|{1}|{2}|{3}|{4}", sNode, folderNode, frag, fragString, url);
    }

  }


  //
  // interface base per la definizione degli elementi utilizzati dai filtri e collectors
  //
  public interface IKCMS_TreeBrowser_fsNodeElement_Interface : FS_Operations.FS_NodeInfo_Interface
  {
    object frag { get; set; }
    string fragString { get; set; }
    string url { get; set; }
  }


  public class IKCMS_TreeBrowser_fsNodeElement : FS_Operations.FS_NodeInfo, IKCMS_TreeBrowser_fsNodeElement_Interface
  {
    public virtual object frag { get; set; }
    public virtual string fragString { get; set; }
    public virtual string url { get; set; }
  }


  //
  // filtro nullo
  //
  [Description("Nessun filtro attivo")]
  public class IKGD_Archive_Filter_NULL : IKGD_Archive_Filter_Interface
  {
    public virtual bool FilterBrowsableItemsOnly { get { return true; } }

    public virtual Expression<Func<IKGD_VNODE, bool>> vNodeFilter { get { return null; } }
    public virtual Expression<Func<IKGD_VDATA, bool>> vDataFilter { get { return null; } }
  }


  //
  // filtro per selezionare solo i messaggi che risultano attivi come daterange
  //
  [Description("Filtro per attivare il range di validità sugli elementi")]
  public class IKGD_Archive_Filter_DateRange : IKGD_Archive_Filter_Interface
  {
    public virtual bool FilterBrowsableItemsOnly { get { return true; } }

    public virtual Expression<Func<IKGD_VNODE, bool>> vNodeFilter { get { return null; } }
    public virtual Expression<Func<IKGD_VDATA, bool>> vDataFilter { get { return n => (n.date_activation == null || n.date_activation <= FS_OperationsHelpers.DateTimeSession) && (n.date_expiry == null || FS_OperationsHelpers.DateTimeSession <= n.date_expiry); } }
  }


  //
  // filtro per selezionare solo i messaggi con data >= a quella odierna
  //
  [Description("Filtro per visualizzare solo gli elementi con data futura")]
  public class IKGD_Archive_Filter_DateFuture : IKGD_Archive_Filter_Interface
  {
    public virtual bool FilterBrowsableItemsOnly { get { return true; } }

    public virtual Expression<Func<IKGD_VNODE, bool>> vNodeFilter { get { return null; } }
    public virtual Expression<Func<IKGD_VDATA, bool>> vDataFilter { get { return n => n.date_node >= FS_OperationsHelpers.DateTimeSession && (n.date_activation == null || n.date_activation <= FS_OperationsHelpers.DateTimeSession) && (n.date_expiry == null || FS_OperationsHelpers.DateTimeSession <= n.date_expiry); } }
    //public virtual Expression<Func<IKGD_VDATA, bool>> vDataFilter { get { return n => n.date_node >= DateTime.Now; } }
  }


  //
  // filtro per selezionare solo i messaggi con data <= a quella odierna
  //
  [Description("Filtro per visualizzare solo gli elementi con data passata")]
  public class IKGD_Archive_Filter_DatePast : IKGD_Archive_Filter_Interface
  {
    public virtual bool FilterBrowsableItemsOnly { get { return true; } }

    public virtual Expression<Func<IKGD_VNODE, bool>> vNodeFilter { get { return null; } }
    public virtual Expression<Func<IKGD_VDATA, bool>> vDataFilter { get { return n => n.date_node <= FS_OperationsHelpers.DateTimeSession && (n.date_activation == null || n.date_activation <= FS_OperationsHelpers.DateTimeSession) && (n.date_expiry == null || FS_OperationsHelpers.DateTimeSession <= n.date_expiry); } }
    //public virtual Expression<Func<IKGD_VDATA, bool>> vDataFilter { get { return n => n.date_node <= DateTime.Now; } }
  }


  //
  // aggregator per raggruppare gli items sia per folder tree che per ordine cronologico con grouping (news + categorie)
  //
  [Description("Aggregazione tipo news con categorie, anno e mese")]
  public class IKGD_Archive_Collector_NewsWithFoldersGeneral<fsNodeT> : IKGD_Archive_Collector_Interface<fsNodeT>
    where fsNodeT : class, FS_Operations.FS_NodeInfo_Interface
  {
    public virtual bool RenderTree { get { return true; } }
    public virtual bool ScanSubTree { get { return true; } }
    public virtual bool ForceUrlsToLeaf { get { return true; } }

    public virtual List<bool> AggregatorsOrderByDescendant { get { return new List<bool> { true, true }; } }
    public virtual List<Func<fsNodeT, object>> Aggregators
    {
      get
      {
        return new List<Func<fsNodeT, object>> {
          n=>n.vData.date_node.Year,
          n=>n.vData.date_node.Month
        };
      }
    }

    public virtual List<Func<fsNodeT, string>> Formatters
    {
      get
      {
        return new List<Func<fsNodeT, string>> {
          n=>n.vData.date_node.ToString("yyyy"),
          n=>n.vData.date_node.ToString("MMMM")
        };
      }
    }

    public virtual IQueryable<fsNodeT> Sorter(IQueryable<fsNodeT> items)
    {
      return items.OrderByDescending(n => n.vData.date_node).ThenBy(n => n.vNode.position).ThenByDescending(n => n.vNode.version);
    }

  }


  //
  // aggregator per raggruppare gli items solo per cartella come nei moduli FAQ
  //
  [Description("Aggregazione tipo FAQ con categorie")]
  public class IKGD_Archive_Collector_FoldersTree<fsNodeT> : IKGD_Archive_Collector_Interface<fsNodeT>
    where fsNodeT : class, FS_Operations.FS_NodeInfo_Interface
  {
    public virtual bool RenderTree { get { return true; } }
    public virtual bool ScanSubTree { get { return true; } }
    public virtual bool ForceUrlsToLeaf { get { return true; } }

    //public virtual List<bool> AggregatorsOrderByDescendant { get { return null; } }
    //public virtual List<Func<fsNodeT, object>> Aggregators { get { return null; } }
    //public virtual List<Func<fsNodeT, string>> Formatters { get { return null; } }
    //
    // gli index manager sembrano avere dei problemi con queste liste settate a null
    //
    public virtual List<bool> AggregatorsOrderByDescendant { get { return new List<bool>(); } }
    public virtual List<Func<fsNodeT, object>> Aggregators { get { return new List<Func<fsNodeT, object>>(); } }
    public virtual List<Func<fsNodeT, string>> Formatters { get { return new List<Func<fsNodeT, string>>(); } }

    public virtual IQueryable<fsNodeT> Sorter(IQueryable<fsNodeT> items)
    {
      return items.OrderBy(n => n.vNode.position).ThenBy(n => n.vNode.name).ThenBy(n => n.vNode.version);
    }
  }


  //
  // aggregator per raggruppare gli items solo per data come nei moduli news semplici
  //
  [Description("Aggregazione tipo news con anno e mese")]
  public class IKGD_Archive_Collector_NewsGeneral<fsNodeT> : IKGD_Archive_Collector_NewsWithFoldersGeneral<fsNodeT>
    where fsNodeT : class, FS_Operations.FS_NodeInfo_Interface
  {
    public override bool RenderTree { get { return false; } }
    public override bool ScanSubTree { get { return true; } }
  }


  //
  // aggregator per raggruppare gli items sia per folder tree che per ordine cronologico con grouping (news + categorie)
  //
  [Description("Aggregazione tipo news con categorie e anno")]
  public class IKGD_Archive_Collector_NewsWithFolders_yyyy<fsNodeT> : IKGD_Archive_Collector_Interface<fsNodeT>
    where fsNodeT : class, FS_Operations.FS_NodeInfo_Interface
  {
    public virtual bool RenderTree { get { return true; } }
    public virtual bool ScanSubTree { get { return true; } }
    public virtual bool ForceUrlsToLeaf { get { return true; } }

    public virtual List<bool> AggregatorsOrderByDescendant { get { return new List<bool> { true }; } }
    public virtual List<Func<fsNodeT, object>> Aggregators
    {
      get
      {
        return new List<Func<fsNodeT, object>> {
          n=>n.vData.date_node.Year
        };
      }
    }

    public virtual List<Func<fsNodeT, string>> Formatters
    {
      get
      {
        return new List<Func<fsNodeT, string>> {
          n=>n.vData.date_node.ToString("yyyy")
        };
      }
    }

    public virtual IQueryable<fsNodeT> Sorter(IQueryable<fsNodeT> items)
    {
      return items.OrderByDescending(n => n.vData.date_node).ThenBy(n => n.vNode.position).ThenByDescending(n => n.vNode.version);
    }

  }

  [Description("Aggregazione tipo news con anno ")]
  public class IKGD_Archive_Collector_NewsGeneral_yyyy<fsNodeT> : IKGD_Archive_Collector_NewsWithFolders_yyyy<fsNodeT>
    where fsNodeT : class, FS_Operations.FS_NodeInfo_Interface
  {
    public override bool RenderTree { get { return false; } }
    public override bool ScanSubTree { get { return true; } }
  }




  //
  // filtro per formattare i menu' di browsing
  //
  [Description("Boh?")]
  public class IKGD_Archive_Formatter_News01 : IKGD_Archive_Formatter_Interface
  {
  }



  //
  // Aggregator/Collector per la gestione di teaser tipo news/eventi: ordinamento per data crescente
  //
  [Description("Aggregazione tipo news/eventi con verifica delle date di validità e ordinamento per data meno recente")]
  public class IKGD_Teaser_Collector_NewsEventsDateAsc<fsNodeT> : IKGD_Teaser_Collector_Interface<fsNodeT>
    where fsNodeT : class, FS_Operations.FS_NodeInfo_Interface
  {
    public virtual bool ScanSubTree { get { return true; } }
    public virtual bool FilterBrowsableItemsOnly { get { return true; } }

    public virtual Expression<Func<IKGD_VNODE, bool>> vNodeFilter { get { return null; } }
    public virtual Expression<Func<IKGD_VDATA, bool>> vDataFilter { get { return n => (n.date_activation == null || n.date_activation <= FS_OperationsHelpers.DateTimeSession) && (n.date_expiry == null || FS_OperationsHelpers.DateTimeSession <= n.date_expiry); } }
    //public virtual Expression<Func<IKGD_VDATA, bool>> vDataFilter { get { return n => (n.date_activation == null || n.date_activation <= DateTime.Now) && (n.date_expiry == null || DateTime.Now <= n.date_expiry); } }

    public virtual IQueryable<fsNodeT> Sorter(IQueryable<fsNodeT> items)
    {
      return items.OrderBy(n => n.vData.date_node).ThenBy(n => n.vNode.position).ThenByDescending(n => n.vNode.version);
    }

  }


  [Description("Aggregazione tipo news/eventi con verifica delle date di validità e ordinamento per data più recente")]
  public class IKGD_Teaser_Collector_NewsEventsDateDesc<fsNodeT> : IKGD_Teaser_Collector_Interface<fsNodeT>
    where fsNodeT : class, FS_Operations.FS_NodeInfo_Interface
  {
    public virtual bool ScanSubTree { get { return true; } }
    public virtual bool FilterBrowsableItemsOnly { get { return true; } }

    public virtual Expression<Func<IKGD_VNODE, bool>> vNodeFilter { get { return null; } }
    public virtual Expression<Func<IKGD_VDATA, bool>> vDataFilter { get { return n => (n.date_activation == null || n.date_activation <= FS_OperationsHelpers.DateTimeSession) && (n.date_expiry == null || FS_OperationsHelpers.DateTimeSession <= n.date_expiry); } }
    //public virtual Expression<Func<IKGD_VDATA, bool>> vDataFilter { get { return n => (n.date_activation == null || n.date_activation <= DateTime.Now) && (n.date_expiry == null || DateTime.Now <= n.date_expiry); } }

    public virtual IQueryable<fsNodeT> Sorter(IQueryable<fsNodeT> items)
    {
      return items.OrderByDescending(n => n.vData.date_node).ThenBy(n => n.vNode.position).ThenByDescending(n => n.vNode.version);
    }

  }


  [Description("Aggregazione tipo news/eventi con verifica delle date di validità e selezione solo dei prossimi eventi")]
  public class IKGD_Teaser_Collector_NewsEventsNext<fsNodeT> : IKGD_Teaser_Collector_Interface<fsNodeT>, IKGD_Collector_ReverseResultsAfterTake
    where fsNodeT : class, FS_Operations.FS_NodeInfo_Interface
  {
    public virtual bool ScanSubTree { get { return true; } }
    public virtual bool FilterBrowsableItemsOnly { get { return true; } }

    public virtual Expression<Func<IKGD_VNODE, bool>> vNodeFilter { get { return null; } }
    public virtual Expression<Func<IKGD_VDATA, bool>> vDataFilter { get { return n => (n.date_activation == null || n.date_activation.Value <= FS_OperationsHelpers.DateTimeSession) && (n.date_expiry == null || FS_OperationsHelpers.DateTimeSession <= n.date_expiry.Value) && ((FS_OperationsHelpers.DateTimeSession <= n.date_node) || (n.date_node_aux != null && FS_OperationsHelpers.DateTimeSession <= n.date_node_aux.Value)); } }

    public virtual IQueryable<fsNodeT> Sorter(IQueryable<fsNodeT> items)
    {
      return items.OrderBy(n => n.vData.date_node).ThenBy(n => n.vNode.position).ThenByDescending(n => n.vNode.version);
    }

  }


  //
  // Aggregator/Collector per la gestione di teaser tipo news/eventi: ordinamento per posizione nel VFS
  //
  [Description("Aggregazione tipo news/eventi con verifica delle date di validità e ordinamento per posizione file")]
  public class IKGD_Teaser_Collector_NewsEventsOrderVFS<fsNodeT> : IKGD_Teaser_Collector_NewsEventsDateAsc<fsNodeT>
    where fsNodeT : class, FS_Operations.FS_NodeInfo_Interface
  {
    public override bool ScanSubTree { get { return false; } }

    public override IQueryable<fsNodeT> Sorter(IQueryable<fsNodeT> items)
    {
      return items.OrderBy(n => n.vNode.position).ThenBy(n => n.vNode.name).ThenByDescending(n => n.vNode.version);
    }
  }



  //
  // Aggregator/Collector per la gestione di teaser tipo news/eventi: ordinamento per data crescente
  //
  [Description("Aggregazione per eventi definiti nelle pagine web")]
  public class IKGD_Teaser_Collector_Events4PageDateAsc<fsNodeT> : IKGD_Teaser_Collector_Interface<fsNodeT>
    where fsNodeT : class, FS_Operations.FS_NodeInfo_Interface
  {
    public virtual bool ScanSubTree { get { return false; } }
    public virtual bool FilterBrowsableItemsOnly { get { return false; } }

    public virtual Expression<Func<IKGD_VNODE, bool>> vNodeFilter { get { return null; } }
    public virtual Expression<Func<IKGD_VDATA, bool>> vDataFilter { get { return n => (n.manager_type == typeof(IKCMS_ResourceType_Event4PageKVT).Name) && (n.date_activation == null || n.date_activation <= FS_OperationsHelpers.DateTimeSession) && (n.date_expiry == null || FS_OperationsHelpers.DateTimeSession <= n.date_expiry); } }
    //public virtual Expression<Func<IKGD_VDATA, bool>> vDataFilter { get { return n => (n.manager_type == typeof(IKCMS_ResourceType_Event4PageKVT).Name) && (n.date_activation == null || n.date_activation <= DateTime.Now) && (n.date_expiry == null || DateTime.Now <= n.date_expiry); } }

    public virtual IQueryable<fsNodeT> Sorter(IQueryable<fsNodeT> items)
    {
      return items.OrderBy(n => n.vData.date_node).ThenBy(n => n.vData.date_node_aux ?? n.vData.date_node).ThenBy(n => n.vNode.snode);
    }

  }




  public static class IKCMS_Browser_Support
  {

    public static string GetInfoPath(this FS_Operations.FS_TreeNode<IKCMS_TreeBrowser_fsNodeElement_Interface> node, int fragsToSkip)
    {
      try { return "/" + Utility.Implode(node.BackRecurseOnData.Where(n => n != null).Reverse().Select(n => n.frag).Skip(fragsToSkip), "/").TrimStart('/'); }
      catch { return "/"; }
    }



    //
    // gli elementi generati per il tree con automapper implementano un'interface proxy che non appartiene affettivamente a nessuna classe
    // quindi risulta particolarmente subdolo il confronto tra elementi di strutture generate dall'automapper, il sistema crea sempre oggetti nuovi
    // e non avro' mai corrispondenza tra oggetti generati in run differenti anche se dalla stessa istanza dell'oggetto madre
    //
    public static bool EqualAutoMapper(IKCMS_TreeBrowser_Element_Interface obj1, IKCMS_TreeBrowser_Element_Interface obj2)
    {
      if (ReferenceEquals(obj1, obj2)) return true;
      if (ReferenceEquals(null, obj1)) return false;
      if (ReferenceEquals(null, obj2)) return false;
      return Equals(obj1.sNode, obj2.sNode) && Equals(obj1.folderNode, obj2.folderNode) && Equals(obj1.fragString, obj2.fragString) && Equals(obj1.url, obj2.url);
    }


    public static bool EqualAutoMapper(FS_Operations.FS_TreeNode<IKCMS_TreeBrowser_Element_Interface> obj1, FS_Operations.FS_TreeNode<IKCMS_TreeBrowser_Element_Interface> obj2)
    {
      if (ReferenceEquals(obj1, obj2)) return true;
      if (ReferenceEquals(null, obj1)) return false;
      if (ReferenceEquals(null, obj2)) return false;
      return EqualAutoMapper(obj1.Data, obj2.Data);
    }



  }


}