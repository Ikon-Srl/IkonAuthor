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
using System.Linq;
using System.Web;
using System.Xml.Linq;
using System.Data.Linq;
using System.Data.Linq.Mapping;
using System.ComponentModel;
using System.Reflection;
using System.Text;
using System.Transactions;


/// <summary>
/// Summary description for IkonGD_dataBase
/// </summary>


namespace Ikon.GD
{
  using Ikon.IKCMS;

  //
  // classe utilizzata per ottenere risultati da query che ritornano uno scalar value con LINQ
  //
  public class ScalarResultDB<T>
  {
    public T value { get; set; }
  }


  public partial class IKGD_DataContext
  {
    public virtual bool TransactionIsolationSent { get; protected set; }
    public virtual TransactionScope TransactionScopeGlobal { get; protected set; }

    //
    // ritorna una nuova istanza del datacontext mediante helper IKGD_DBH
    //
    public static IKGD_DataContext Factory() { return IKGD_DBH.GetDB(); }
    //


    public virtual void SetTransactionIsolation()
    {
      try
      {
        TransactionScopeGlobal = new TransactionScope(TransactionScopeOption.Required, new TransactionOptions { IsolationLevel = System.Transactions.IsolationLevel.ReadUncommitted });
        TransactionIsolationSent = true;
      }
      catch { }
    }


    protected override void Dispose(bool disposing)
    {
      if (TransactionIsolationSent && TransactionScopeGlobal != null)
      {
        try
        {
          TransactionIsolationSent = false;
          TransactionScopeGlobal.Complete();  // verificare se e' necessario per le operazioni di scrittura
        }
        catch { }
      }
      base.Dispose(disposing);
    }


    partial void OnCreated()
    {
      IKCMS_ExecutionProfiler.AddMessage("DB: {0}.OnCreated()".FormatString(this.GetType().FullName));
    }

  }


  public static class IKGD_DBH
  {
    private const string ConnectionStringNameDefault = "GDCS";
    public const string ContextStringDB = "Ikon.GD.IKGD_DBH.DB";
    private static MappingSource GD_MappingSource;
    //public const string ApplicationID = "0EDCE74B-C86D-4cc8-BB77-6BE2F8468C81";
    //public readonly static Guid ApplicationGuid = new Guid(ApplicationID);
    //
    public static readonly DateTime DateTimeMinValue;
    public static readonly DateTime DateTimeMaxValue;

    static IKGD_DBH()
    {
      //DateTimeMinValue = DateTime.Parse("1753-01-01");
      //DateTimeMaxValue = DateTime.Parse("9999-12-31");
      DateTimeMinValue = Utility.DateTimeMinValueDB;
      DateTimeMaxValue = Utility.DateTimeMaxValueDB;
    }

    public static string ConnectionStringName
    {
      get
      {
        try { return (string)HttpContext.Current.Session[ContextStringDB + "_CS"] ?? ConnectionStringNameDefault; }
        catch { return ConnectionStringNameDefault; }
      }
    }
    public static void OverrideConnectionStringName(string CS) { try { HttpContext.Current.Session[ContextStringDB + "_CS"] = CS; } catch { } }
    public static void OverrideDataBaseName(string DBname) { try { HttpContext.Current.Session[ContextStringDB + "_DB"] = DBname; } catch { } }
    //
    public static string ConnectionString { get { return ConfigurationManager.ConnectionStrings[ConnectionStringName].ConnectionString; } }

    public static IKGD_DataContext GetDB() { return GetDB(false, false); }
    public static IKGD_DataContext GetDB(bool forceNewconnection) { return GetDB(forceNewconnection, false); }
    public static IKGD_DataContext GetDB(bool forceNewconnection, bool disableObjectTracking)
    {
      IKGD_DataContext newContext = null;
      try
      {
        if (GD_MappingSource != null && !forceNewconnection)
          newContext = new IKGD_DataContext(ConnectionString, GD_MappingSource);
        else
        {
          newContext = new IKGD_DataContext(ConnectionString);
          if (GD_MappingSource == null)
          {
            //VerifyMappings();
            GD_MappingSource = newContext.Mapping.MappingSource;
          }
        }
        if (HttpContext.Current.Session != null && !string.IsNullOrEmpty((string)HttpContext.Current.Session[ContextStringDB + "_DB"]))
        {
          string newDBname = (string)HttpContext.Current.Session[ContextStringDB + "_DB"];
          if (!string.Equals(newContext.Connection.Database, newDBname, StringComparison.OrdinalIgnoreCase))
          {
            if (newContext.Connection.State != ConnectionState.Open)
              newContext.Connection.Open();  // e' necessario aprire il DB perche' funzioni ChangeDatabase
            newContext.Connection.ChangeDatabase(newDBname);
          }
        }
      }
      catch { }
      //
      // http://www.hanselman.com/blog/GettingLINQToSQLAndLINQToEntitiesToUseNOLOCK.aspx
      // http://msdn.microsoft.com/en-us/library/ms179599.aspx
      //
      // TODO: verificare il funzionamento
      // verificare anche in Dispose() se l'istruzione TransactionScopeGlobal.Complete() e' effettivamente necessaria anche per gli update
      //try { newContext.SetTransactionIsolation(); }
      //catch { }
      //
      // eseguire la seguente istruzione ad ogni connessione in alternativa all'uso generalizzato delle transactions (ma non per context RO)
      if (newContext != null && !disableObjectTracking)
      {
        try { newContext.ExecuteCommand("SET TRANSACTION ISOLATION LEVEL READ UNCOMMITTED"); }
        catch { }
      }
      //
      // per abilitare il logging delle query LINQ
      // visualizzabile con ~/trace.axd  (attivare il trace in web.config <trace enabled="true" requestLimit="50"/>)
      //newContext.Log = new Ikon.Log.LINQ_Logger();
      //
      return newContext;
    }

    public static IKGD_DataContext GetDB(IKGD_DataContext baseDB) { return new IKGD_DataContext(baseDB.Connection, baseDB.Mapping.MappingSource); }


    public static IKGD_DataContext DB
    {
      get
      {
        if (HttpContext.Current == null || HttpContext.Current.Items == null)
          return GetDB();
        if (HttpContext.Current.Items[ContextStringDB] == null)
          HttpContext.Current.Items[ContextStringDB] = GetDB();
        return HttpContext.Current.Items[ContextStringDB] as IKGD_DataContext;
      }
      set { HttpContext.Current.Items[ContextStringDB] = value; }
    }

    public static void DB_AutoDispose()
    {
      if (HttpContext.Current.Items[ContextStringDB] != null)
      {
        try
        {
          (HttpContext.Current.Items[ContextStringDB] as IKGD_DataContext).Dispose();
        }
        catch { }
        finally
        {
          HttpContext.Current.Items[ContextStringDB] = null;
        }
      }
    }


    public static bool VerifyMappings()
    {
      try
      {
        IKGD_VNODE.VerifyMappings();
        IKGD_RNODE.VerifyMappings();
        IKGD_SNODE.VerifyMappings();
        IKGD_RELATION.VerifyMappings();
        return true;
      }
      catch { }
      return false;
    }


    //
    // NB utilizzare query con un alias "value" per il valore scalare da estrarre del tipo:
    // SELECT count(*) AS value FROM ....
    // attenzione che SELECT IDENT_CURRENT('IKGD_VNODE') AS value   ritorna decimal invece di int!
    // quindi chiamare con T=decimal e poi fare un cast
    //
    public static T GetScalarValue<T>(this IKGD_DataContext db, string query, params object[] parameters)
    {
      try
      {
        return db.ExecuteQuery<ScalarResultDB<T>>(query, parameters).FirstOrDefault().value;
      }
      catch { }
      return default(T);
    }
    public static T GetScalarValueSimple<T>(this IKGD_DataContext db, string query, params object[] parameters)
    {
      try
      {
        return db.ExecuteQuery<T>(query, parameters).FirstOrDefault();
      }
      catch { }
      return default(T);
    }


    public static void Update<T>(T obj, Action<T> update) where T : class
    {
      using (var db = GetDB())
      {
        db.GetTable<T>().Attach(obj);
        update(obj);
        db.SubmitChanges();
      }
    }

    public static void UpdateAll<T>(List<T> items, Action<T> update) where T : class
    {
      using (var db = GetDB())
      {
        Table<T> table = db.GetTable<T>();
        foreach (T item in items)
        {
          table.Attach(item);
          update(item);
        }

        db.SubmitChanges();
      }
    }

    public static void Delete<T>(T entity) where T : class, new()
    {
      using (var db = GetDB())
      {
        Table<T> table = db.GetTable<T>();
        table.Attach(entity);
        table.DeleteOnSubmit(entity);
        db.SubmitChanges();
      }
    }

    public static void Insert<T>(T obj) where T : class
    {
      using (var db = GetDB())
      {
        db.GetTable<T>().InsertOnSubmit(obj);
        db.SubmitChanges();
      }
    }


    //
    // cancellazione di record senza dover passare l'intero record ma solo la PK
    //
    public static void DeleteByPK<TSource, TPK>(this DataContext dc, TPK pk) where TSource : class
    {
      Table<TSource> table = dc.GetTable<TSource>();
      TableDef tableDef = GetTableDef<TSource>();

      dc.ExecuteCommand("DELETE FROM [" + tableDef.TableName + "] WHERE [" + tableDef.PKFieldName + "] = {0}", pk);
    }

    public static void DeleteByPK<TSource, TPK>(this DataContext dc, IEnumerable<TPK> pkList) where TSource : class
    {
      Table<TSource> table = dc.GetTable<TSource>();
      TableDef tableDef = GetTableDef<TSource>();

      var buffer = new StringBuilder();
      buffer.Append("DELETE FROM [").Append(tableDef.TableName).Append("] WHERE [").Append(tableDef.PKFieldName).Append("] IN (");
      foreach (TPK item in pkList)
        buffer.Append('\'').Append(item.ToString()).Append('\'').Append(',');

      buffer.Length--;
      buffer.Append(')');

      dc.ExecuteCommand(buffer.ToString());
    }


    internal static Dictionary<Type, TableDef> _TableDefCache = new Dictionary<Type, TableDef>();

    internal static TableDef GetTableDef<TEntity>() where TEntity : class
    {
      Type entityType = typeof(TEntity);
      if (!_TableDefCache.ContainsKey(entityType))
      {
        lock (_TableDefCache)
        {
          if (!_TableDefCache.ContainsKey(entityType))
          {
            object[] attributes = entityType.GetCustomAttributes(typeof(TableAttribute), true);
            string tableName = (attributes[0] as TableAttribute).Name;
            if (tableName.StartsWith("dbo."))
              tableName = tableName.Substring("dbo.".Length);
            string pkFieldName = "ID";

            // Find the property which is the primary key so that we can find the 
            // primary key field name in database
            foreach (PropertyInfo prop in entityType.GetProperties())
            {
              object[] columnAttributes = prop.GetCustomAttributes(typeof(ColumnAttribute), true);
              if (columnAttributes.Length > 0)
              {
                ColumnAttribute columnAtt = columnAttributes[0] as ColumnAttribute;
                if (columnAtt.IsPrimaryKey)
                  pkFieldName = columnAtt.Storage.TrimStart('_');
              }
            }

            var tableDef = new TableDef { TableName = tableName, PKFieldName = pkFieldName };
            _TableDefCache.Add(entityType, tableDef);
            return tableDef;
          }
          else
          {
            return _TableDefCache[entityType];
          }
        }
      }
      else
      {
        return _TableDefCache[entityType];
      }
    }



    //
    // factory methods per la creazione standardizzata di entities sul VFS
    //
    public static IKGD_RNODE Factory_IKGD_RNODE(this FS_Operations fsOp) { return new IKGD_RNODE { date_creat = fsOp.DateTimeContext, username = fsOp.CurrentUser }; }
    public static IKGD_SNODE Factory_IKGD_SNODE(this FS_Operations fsOp) { return new IKGD_SNODE { date_creat = fsOp.DateTimeContext, username = fsOp.CurrentUser }; }
    public static IKGD_VNODE Factory_IKGD_VNODE(this FS_Operations fsOp) { return new IKGD_VNODE { version_date = fsOp.DateTimeContext, username = fsOp.CurrentUser, flag_folder = false, flag_published = false, flag_current = true, flag_deleted = false, flag_noDelete = false, name = string.Empty, position = 0 }; }
    public static IKGD_VDATA Factory_IKGD_VDATA(this FS_Operations fsOp) { return new IKGD_VDATA { version_date = fsOp.DateTimeContext, date_node = fsOp.DateTimeContext, username = fsOp.CurrentUser, flag_published = false, flag_current = true, flag_inactive = false, flag_autoDeleteOnRels = false, flag_deleted = false, flag_unstructured = false, flags_menu = 0 }; }
    public static IKGD_INODE Factory_IKGD_INODE(this FS_Operations fsOp) { return new IKGD_INODE { version_date = fsOp.DateTimeContext, username = fsOp.CurrentUser, flag_current = true, flag_published = false, flag_deleted = false }; }
    public static IKGD_STREAM Factory_IKGD_STREAM(this FS_Operations fsOp) { return new IKGD_STREAM { }; }
    public static IKGD_RELATION Factory_IKGD_RELATION(this FS_Operations fsOp) { return new IKGD_RELATION { version_date = fsOp.DateTimeContext, username = fsOp.CurrentUser, flag_published = false, flag_current = true, flag_deleted = false }; }
    public static IKGD_PROPERTY Factory_IKGD_PROPERTY(this FS_Operations fsOp) { return new IKGD_PROPERTY { version_date = fsOp.DateTimeContext, username = fsOp.CurrentUser, flag_published = false, flag_current = true, flag_deleted = false }; }
    //
    public static IKGD_VDATA_KEYVALUE Factory_IKGD_VDATA_KEYVALUE(this FS_Operations fsOp) { return new IKGD_VDATA_KEYVALUE { modif = fsOp.DateTimeContext, flag_published = false, flag_current = false, Level = 0 }; }
    public static IKGD_VDATA_KEYVALUE Factory_IKGD_VDATA_KEYVALUE(this FS_Operations fsOp, int Level, string Key, string KeyParent, string ValueString, int? ValueInt, double? ValueDouble, DateTime? ValueDateTime, DateTime? ValueDateTimeExt)
    {
      return new IKGD_VDATA_KEYVALUE
      {
        modif = fsOp.DateTimeContext,
        Level = Level,
        Key = Utility.StringTruncate(Key, 50),
        KeyParent = Utility.StringTruncate(KeyParent, 50),
        ValueInt = ValueInt,
        ValueDouble = ValueDouble,
        ValueDate = ValueDateTime,
        ValueDateExt = ValueDateTimeExt,
        ValueString = Utility.StringTruncate(ValueString, 255),
        ValueText = ValueString
      };
    }
    //
    public static IKG_HITACC Factory_IKG_HITACC(this FS_Operations fsOp) { return new IKG_HITACC { LastUpdate = fsOp.DateTimeContext, Category = 0, rNode = 0, Hits = null, Value = null }; }

  }


  //
  // creazione di un'interfaccia comune per i vari tipi di elementi presenti nel VFS
  // ed estensione delle classi generate da SqlMetal
  //

  //
  public interface IKGD_hasRNODE
  {
    int rnode { get; set; }
    IKGD_RNODE IKGD_RNODE { get; set; }
  }
  //
  public interface IKGD_XNODE : IKGD_hasRNODE
  {
    //Binary ts { get; set; }
    int version { get; set; }
    int? version_frozen { get; set; }
    DateTime version_date { get; set; }
    bool flag_published { get; set; }
    bool flag_current { get; set; }
    bool flag_deleted { get; set; }
    string username { get; set; }
    //
    event PropertyChangingEventHandler PropertyChanging;
    event PropertyChangedEventHandler PropertyChanged;
  }
  public interface IKGD_hasFolderInfo
  {
    bool flag_folder { get; set; }
  }
  //
  public interface IKGD_STREAM_Dictionary
  {
    string type { get; set; }
    string source { get; set; }
    string key { get; set; }
  }
  //
  //
  public partial class IKGD_RNODE : IKGD_hasFolderInfo { }
  public partial class IKGD_SNODE : IKGD_hasRNODE, IKGD_hasFolderInfo { }
  public partial class IKGD_VNODE : IKGD_XNODE, IKGD_hasFolderInfo { }
  public partial class IKGD_INODE : IKGD_XNODE { }
  public partial class IKGD_VDATA : IKGD_XNODE { }
  public partial class IKGD_ACL : IKGD_XNODE { }
  public partial class IKGD_PROPERTY : IKGD_XNODE { }
  public partial class IKGD_RELATION : IKGD_XNODE { }
  public partial class IKGD_STREAM : IKGD_STREAM_Dictionary { }



  internal class TableDef
  {
    public string TableName;
    public string PKFieldName;
  }


  //
  // altre estensione per alcuni tipi di nodi
  //
  [Flags]
  public enum FlagsMenuEnum
  {
    [Description("   Nodo di menù normale")]
    None = 0,
    [Description(" 1. Nascondi il nodo di menù")]
    HiddenNode = 1 << 1,
    [Description(" 2. Interrompi l'espansione del menù dopo questo nodo")]
    BreakRecurse = 1 << 0,
    [Description(" 3. Interrompi l'espansione della mappa del sito dopo questo nodo")]
    BreakSiteMapRecurse = 1 << 5,
    [Description(" 4. Ignora questo nodo nelle briciole di pane")]
    SkipBreadCrumbs = 1 << 6,
    [Description(" 5. Rendi questa pagina non selezionabile dal menù")]
    UnSelectableNode = 1 << 7,
    [Description(" 6. Ricerca automatica nel menù del primo nodo cliccabile")]
    FindFirstValidNode = 1 << 3,
    [Description(" 7. Definisci un nodo iniziale per menù secondari")]
    SubTreeRoot = 1 << 2,
    [Description(" 8. Apri la pagina in un nuovo browser")]
    TargetBlank = 1 << 8,
    [Description(" 9. Pagina visibile nel menù anche senza avere le ACL")]
    VisibleWithoutACL = 1 << 9,
    [Description("10. Accesso alla pagina solo per utenti loggati")]
    LoginRequired = 1 << 10,
    [Description("11. Accesso alla pagina solo per utenti non anonimi")]
    LazyLoginNoAnonymous = 1 << 12,
    [Description("12. Disabilita il rewrite della Url (usa il codice CMS)")]
    UseCodeCmsForUrl = 1 << 11
    //[Description(" 12. Integra il menù news nel menù principale")]
    //HasExternalSubTree = 1 << 4
  };


  //
  // altre estensione per alcuni tipi di nodi
  //

  public partial class IKGD_VDATA
  {
    //public string dataAsString { get { return Utility.LinqBinaryGetStringDB(this.data); } set { this.data = Utility.LinqBinarySetStringDB(value); } }
    //public XElement dataAsXml { get { try { return XElement.Parse(Utility.LinqBinaryGetStringDB(this.data)); } catch { return null; } } set { try { this.data = Utility.LinqBinarySetStringDB(value.ToString()); } catch { } } }

    //
    // NOTE:
    // dataAsString (o meglio .data convertito in stringa) viene usato attualmente solo dal widget iGoogle e dal UC_Settings_iGoogle.ascx.cs
    // viene usato per salvare l'xml letto dalla url esterna. Il tutto andrebbe convertito con il supporto nei settings serializzati
    // v. class IKG_WidgetUI (.ikgd_data , .ikgd_dataAsString e relativi riferimenti) attenzione che non viene usato in maniera diretta ma con altri
    // tipi di accessor che devono essere cercati nel codice (anzi, sarebbe meglio eliminarli per rintracciare tutte le dipendenze e verificarli con una compilazione per deploy degli .ascx)
    //

    //
    // funzionamento del vecchio set di flags per i menu'
    //
    // flag_menu:
    //      null -> visibile nel menu'
    //      true -> visibile nel menu'
    //      false -> nodo non incluso nel menu
    //
    // flag_treeRecurse:
    //      null -> il flag viene ignorato (quindi si tratta di un nodo non speciale)
    //      true -> si tratta di un marker per un subTree (da usare per cataloghi o submenu' in pagina bindati ad un parent specifico  piuttosto che ad un level fissato)
    //      false -> break della ricorsione (non espande i child, corrisponde al break del CMS 2.0, hide viene gestito con flag_menu oppure con active)
    //
    // flag_treeAux: (riservato per utilizzi futuri)
    //

    // per ottenere un dictionary con le descrizioni estese usare: Utility.EnumGetDictionary<Ikon.GD.FlagsMenuEnum>();
    public FlagsMenuEnum FlagsMenu { get { return (FlagsMenuEnum)flags_menu; } set { flags_menu = (int)value; } }

    public double? GeoLonX { get { return geoLonX; } set { geoLonX = value; } }
    public double? GeoLatY { get { return geoLatY; } set { geoLatY = value; } }
    public double? GeoRangeM { get { return geoRangeM; } set { geoRangeM = value; } }

    //
    // gestione degli attributi in IKGD_VDATA
    //
    //private Utility.DictionaryMV<string, string> _Attributes { get; set; }
    //public Utility.DictionaryMV<string, string> AttributesDictionary
    //{
    //  get
    //  {
    //    try
    //    {
    //      if (_Attributes != null)
    //        return _Attributes;
    //      if (!string.IsNullOrEmpty(this.attributes))
    //        _Attributes = Newtonsoft.Json.JsonConvert.DeserializeObject<Utility.DictionaryMV<string, string>>(this.attributes) ?? new Utility.DictionaryMV<string, string>();
    //    }
    //    catch { _Attributes = new Utility.DictionaryMV<string, string>(); }
    //    return _Attributes;
    //  }
    //}
    //public void AttributesUpdate()
    //{
    //  try
    //  {
    //    if (_Attributes != null)
    //    {
    //      this.attributes = Newtonsoft.Json.JsonConvert.SerializeObject(_Attributes);
    //      return;
    //    }
    //  }
    //  catch { }
    //  this.attributes = null;
    //}

    public T DeserializeSettings<T>()
    {
      object result = null;
      try
      {
        result = IKGD_Serialization.DeSerializeJSON<T>(settings, "DefaultValue");
        if (result != null)
        {
          return (T)result;
        }
      }
      catch { }
      return default(T);
    }


  }


  public partial class IKGD_STREAM
  {
    public string sourceKey { get { return string.Format("{0}|{1}", this.source, this.key); } }
    public string dataAsString { get { return Utility.LinqBinaryGetStringDB(this.data); } set { this.data = Utility.LinqBinarySetStringDB(value); } }
  }


  public partial class IKGD_INODE
  {
  }


  //
  // partial classes per la generazione corretta dei mapping FK
  // deve essere verificata con gli assert in VerifyMappings
  //

  public partial class IKGD_VNODE
  {
    //[Association(Name = "IKGD_RNODE_IKGD_VNODE", Storage = "_IKGD_RNODE", ThisKey = "rnode", OtherKey = "code", IsForeignKey = true)]
    //public IKGD_RNODE IKGD_RNODE_node { get { return this._IKGD_RNODE.Entity; } set { this.IKGD_RNODE = value; } }

    //[Association(Name = "IKGD_RNODE_IKGD_VNODE1", Storage = "_IKGD_RNODE1", ThisKey = "folder", OtherKey = "code", IsForeignKey = true)]
    //public IKGD_RNODE IKGD_RNODE_folder { get { return this._IKGD_RNODE1.Entity; } set { this.IKGD_RNODE1 = value; } }

    //[Association(Name = "IKGD_RNODE_IKGD_VNODE2", Storage = "_IKGD_RNODE2", ThisKey = "parent", OtherKey = "code", IsForeignKey = true)]
    //public IKGD_RNODE IKGD_RNODE_parent { get { return this._IKGD_RNODE2.Entity; } set { this.IKGD_RNODE2 = value; } }


    public static void VerifyMappings()
    {
      {
        //
        // IKGD_VNODE.IKGD_RNODE --> deve mappare all'rNode della risorsa e non a uno dei due altri mapping
        // nel caso LINQ non riuscisse a mapparlo correttamente sara' necessario intervenire manualmente
        // sul file .dbml per correggere il mapping generato da sqlmetal
        //
        // <Association Name="IKGD_RNODE_IKGD_VNODE_xyz" Member="IKGD_RNODE" ThisKey="rnode" OtherKey="code" Type="IKGD_RNODE" IsForeignKey="true" />
        //
        PropertyInfo pi = typeof(IKGD_VNODE).GetProperty("IKGD_RNODE");
        AssociationAttribute attr = (AssociationAttribute)pi.GetCustomAttributes(typeof(AssociationAttribute), false).FirstOrDefault();
        System.Diagnostics.Debug.Assert(
          attr.ThisKey == "rnode" && attr.OtherKey == "code" && pi.PropertyType == typeof(IKGD_RNODE),
          string.Format("File *.dbml ASSOCIATION ERROR: ENTITY:[{0}] RELATION:[{1}] TYPE:[{2}] ThisKey:[{3}] OtherKey:[{4}] AssociationName:[{5}]", pi.DeclaringType, pi.Name, pi.PropertyType, attr.ThisKey, attr.OtherKey, attr.Name));
      }
      {
        // IKGD_VNODE.IKGD_RNODE_folder
        PropertyInfo pi = typeof(IKGD_VNODE).GetProperty("IKGD_RNODE_folder");
        AssociationAttribute attr = (AssociationAttribute)pi.GetCustomAttributes(typeof(AssociationAttribute), false).FirstOrDefault();
        System.Diagnostics.Debug.Assert(
          attr.ThisKey == "folder" && attr.OtherKey == "code" && pi.PropertyType == typeof(IKGD_RNODE),
          string.Format("File *.dbml ASSOCIATION ERROR: ENTITY:[{0}] RELATION:[{1}] TYPE:[{2}] ThisKey:[{3}] OtherKey:[{4}] AssociationName:[{5}]", pi.DeclaringType, pi.Name, pi.PropertyType, attr.ThisKey, attr.OtherKey, attr.Name));
      }
      {
        // IKGD_VNODE.IKGD_RNODE_parent
        PropertyInfo pi = typeof(IKGD_VNODE).GetProperty("IKGD_RNODE_parent");
        AssociationAttribute attr = (AssociationAttribute)pi.GetCustomAttributes(typeof(AssociationAttribute), false).FirstOrDefault();
        System.Diagnostics.Debug.Assert(
          attr.ThisKey == "parent" && attr.OtherKey == "code" && pi.PropertyType == typeof(IKGD_RNODE),
          string.Format("File *.dbml ASSOCIATION ERROR: ENTITY:[{0}] RELATION:[{1}] TYPE:[{2}] ThisKey:[{3}] OtherKey:[{4}] AssociationName:[{5}]", pi.DeclaringType, pi.Name, pi.PropertyType, attr.ThisKey, attr.OtherKey, attr.Name));
      }
    }

  }


  public partial class IKGD_RNODE
  {
    //[Association(Name = "IKGD_RNODE_IKGD_VNODE", Storage = "_IKGD_VNODEs", ThisKey = "code", OtherKey = "rnode")]
    //public EntitySet<IKGD_VNODE> IKGD_VNODEs_node { get { return this._IKGD_VNODEs; } set { this._IKGD_VNODEs.Assign(value); } }

    //[Association(Name = "IKGD_RNODE_IKGD_VNODE1", Storage = "_IKGD_VNODEs1", ThisKey = "code", OtherKey = "folder")]
    //public EntitySet<IKGD_VNODE> IKGD_VNODEs_folder { get { return this._IKGD_VNODEs1; } set { this._IKGD_VNODEs1.Assign(value); } }

    //[Association(Name = "IKGD_RNODE_IKGD_VNODE2", Storage = "_IKGD_VNODEs2", ThisKey = "code", OtherKey = "parent")]
    //public EntitySet<IKGD_VNODE> IKGD_VNODEs_parent { get { return this._IKGD_VNODEs2; } set { this._IKGD_VNODEs2.Assign(value); } }


    public static void VerifyMappings()
    {
      {
        // IKGD_RNODE.IKGD_VNODEs
        PropertyInfo pi = typeof(IKGD_RNODE).GetProperty("IKGD_VNODEs");
        AssociationAttribute attr = (AssociationAttribute)pi.GetCustomAttributes(typeof(AssociationAttribute), false).FirstOrDefault();
        System.Diagnostics.Debug.Assert(
          attr.ThisKey == "code" && attr.OtherKey == "rnode" && pi.PropertyType == typeof(EntitySet<IKGD_VNODE>),
          string.Format("File *.dbml ASSOCIATION ERROR: ENTITY:[{0}] RELATION:[{1}] TYPE:[{2}] ThisKey:[{3}] OtherKey:[{4}] AssociationName:[{5}]", pi.DeclaringType, pi.Name, pi.PropertyType, attr.ThisKey, attr.OtherKey, attr.Name));
      }
      {
        // IKGD_RNODE.IKGD_VNODEs_folder
        PropertyInfo pi = typeof(IKGD_RNODE).GetProperty("IKGD_VNODEs_folder");
        AssociationAttribute attr = (AssociationAttribute)pi.GetCustomAttributes(typeof(AssociationAttribute), false).FirstOrDefault();
        System.Diagnostics.Debug.Assert(
          attr.ThisKey == "code" && attr.OtherKey == "folder" && pi.PropertyType == typeof(EntitySet<IKGD_VNODE>),
          string.Format("File *.dbml ASSOCIATION ERROR: ENTITY:[{0}] RELATION:[{1}] TYPE:[{2}] ThisKey:[{3}] OtherKey:[{4}] AssociationName:[{5}]", pi.DeclaringType, pi.Name, pi.PropertyType, attr.ThisKey, attr.OtherKey, attr.Name));
      }
      {
        // IKGD_RNODE.IKGD_VNODEs_parent
        PropertyInfo pi = typeof(IKGD_RNODE).GetProperty("IKGD_VNODEs_parent");
        AssociationAttribute attr = (AssociationAttribute)pi.GetCustomAttributes(typeof(AssociationAttribute), false).FirstOrDefault();
        System.Diagnostics.Debug.Assert(
          attr.ThisKey == "code" && attr.OtherKey == "parent" && pi.PropertyType == typeof(EntitySet<IKGD_VNODE>),
          string.Format("File *.dbml ASSOCIATION ERROR: ENTITY:[{0}] RELATION:[{1}] TYPE:[{2}] ThisKey:[{3}] OtherKey:[{4}] AssociationName:[{5}]", pi.DeclaringType, pi.Name, pi.PropertyType, attr.ThisKey, attr.OtherKey, attr.Name));
      }
    }
  }


  public partial class IKGD_SNODE
  {
    //[Association(Name = "IKGD_SNODE_IKGD_RELATION999", Storage = "_IKGD_RELATIONs1", ThisKey = "code", OtherKey = "snode_src")]
    //public EntitySet<IKGD_RELATION> IKGD_RELATIONs_src { get { return this._IKGD_RELATIONs1; } set { this._IKGD_RELATIONs1.Assign(value); } }

    //[Association(Name = "IKGD_SNODE_IKGD_RELATION998", Storage = "_IKGD_RELATIONs", ThisKey = "code", OtherKey = "snode_dst")]
    //public EntitySet<IKGD_RELATION> IKGD_RELATIONs_dst { get { return this._IKGD_RELATIONs; } set { this._IKGD_RELATIONs.Assign(value); } }


    public static void VerifyMappings()
    {
      {
        // IKGD_SNODE.IKGD_RELATIONs_src
        PropertyInfo pi = typeof(IKGD_SNODE).GetProperty("IKGD_RELATIONs_src");
        AssociationAttribute attr = (AssociationAttribute)pi.GetCustomAttributes(typeof(AssociationAttribute), false).FirstOrDefault();
        System.Diagnostics.Debug.Assert(
          attr.ThisKey == "code" && attr.OtherKey == "snode_src" && pi.PropertyType == typeof(EntitySet<IKGD_RELATION>),
          string.Format("File *.dbml ASSOCIATION ERROR: ENTITY:[{0}] RELATION:[{1}] TYPE:[{2}] ThisKey:[{3}] OtherKey:[{4}] AssociationName:[{5}]", pi.DeclaringType, pi.Name, pi.PropertyType, attr.ThisKey, attr.OtherKey, attr.Name));
      }
      {
        // IKGD_SNODE.IKGD_RELATION_dst
        PropertyInfo pi = typeof(IKGD_SNODE).GetProperty("IKGD_RELATIONs_dst");
        AssociationAttribute attr = (AssociationAttribute)pi.GetCustomAttributes(typeof(AssociationAttribute), false).FirstOrDefault();
        System.Diagnostics.Debug.Assert(
          attr.ThisKey == "code" && attr.OtherKey == "snode_dst" && pi.PropertyType == typeof(EntitySet<IKGD_RELATION>),
          string.Format("File *.dbml ASSOCIATION ERROR: ENTITY:[{0}] RELATION:[{1}] TYPE:[{2}] ThisKey:[{3}] OtherKey:[{4}] AssociationName:[{5}]", pi.DeclaringType, pi.Name, pi.PropertyType, attr.ThisKey, attr.OtherKey, attr.Name));
      }
    }
  }


  public partial class IKGD_RELATION
  {
    //[Association(Name = "IKGD_SNODE_IKGD_RELATION999", Storage = "_IKGD_SNODE1", ThisKey = "snode_src", OtherKey = "code", IsForeignKey = true)]
    //public IKGD_SNODE IKGD_SNODE_src { get { return this._IKGD_SNODE1.Entity; } set { this.IKGD_SNODE1 = value; } }

    //[Association(Name = "IKGD_SNODE_IKGD_RELATION998", Storage = "_IKGD_SNODE", ThisKey = "snode_dst", OtherKey = "code", IsForeignKey = true, DeleteOnNull = true, DeleteRule = "CASCADE")]
    //public IKGD_SNODE IKGD_SNODE_dst { get { return this._IKGD_SNODE.Entity; } set { this.IKGD_SNODE = value; } }


    public static void VerifyMappings()
    {
      {
        // IKGD_RELATION.IKGD_SNODE_src
        PropertyInfo pi = typeof(IKGD_RELATION).GetProperty("IKGD_SNODE_src");
        AssociationAttribute attr = (AssociationAttribute)pi.GetCustomAttributes(typeof(AssociationAttribute), false).FirstOrDefault();
        System.Diagnostics.Debug.Assert(
          attr.ThisKey == "snode_src" && attr.OtherKey == "code" && pi.PropertyType == typeof(IKGD_SNODE),
          string.Format("File *.dbml ASSOCIATION ERROR: ENTITY:[{0}] RELATION:[{1}] TYPE:[{2}] ThisKey:[{3}] OtherKey:[{4}] AssociationName:[{5}]", pi.DeclaringType, pi.Name, pi.PropertyType, attr.ThisKey, attr.OtherKey, attr.Name));
      }
      {
        // IKGD_RELATION.IKGD_SNODE_dst
        PropertyInfo pi = typeof(IKGD_RELATION).GetProperty("IKGD_SNODE_dst");
        AssociationAttribute attr = (AssociationAttribute)pi.GetCustomAttributes(typeof(AssociationAttribute), false).FirstOrDefault();
        System.Diagnostics.Debug.Assert(
          attr.ThisKey == "snode_dst" && attr.OtherKey == "code" && pi.PropertyType == typeof(IKGD_SNODE),
          string.Format("File *.dbml ASSOCIATION ERROR: ENTITY:[{0}] RELATION:[{1}] TYPE:[{2}] ThisKey:[{3}] OtherKey:[{4}] AssociationName:[{5}]", pi.DeclaringType, pi.Name, pi.PropertyType, attr.ThisKey, attr.OtherKey, attr.Name));
      }
    }
  }


  public partial class IKGD_PROPERTY
  {
  }



  //
  // Flags per la definizione dei tipi di attributo di IKCAT
  //
  [Flags]
  public enum IKCAT_AttributeFlagsEnum
  {
    //[Description("---")]
    //None = 0,
    //[Description("Attributo non editabile")]
    //NotEditable = 1 << 0,
    [Description("Attributo attivo per i filtri di ricerca")]
    SearchFilter = 1 << 1,
    [Description("Attributo autogenerato dal CMS")]
    AutoGenerated = 1 << 2,
    [Description("Attributo ausiliario utilizzato solo per definire il tree ma non associabile al catalogo")]
    NotBindable = 1 << 3
    // e altri...
  };

  [Flags]
  public enum IKCAT_TagFlagsEnum
  {
    //[Description("---")]
    //None = 0,
    //[Description("Attributo non editabile")]
    //NotEditable = 1 << 0,
    [Description("Tag attivo per i filtri di ricerca")]
    SearchFilter = 1 << 1,
    [Description("Tag autogenerato dal CMS")]
    AutoGenerated = 1 << 2,
    [Description("Tag cartella (vale solo per la definizione dell'albero)")]
    NotBindable = 1 << 3
    // e altri...
  };

  public enum IKCAT_AttributeDataTypeEnum
  {
    [Description("Attributo senza valore associato")]
    None,
    [Description("Attributo con valore tipo Text")]
    String,
    [Description("Attributo con valore tipo Boolean")]
    Boolean,
    [Description("Attributo con valore tipo Integer")]
    Int,
    [Description("Attributo con valore tipo Real")]
    Double
    // e altri...
  };


  public partial class IKCAT_Attribute
  {
    // per ottenere un dictionary con le descrizioni estese usare: Utility.EnumGetDictionary<Ikon.Catalog.IKCAT_AttributeFlagsEnum>();
    public Ikon.GD.IKCAT_AttributeFlagsEnum FlagsEnum { get { return (Ikon.GD.IKCAT_AttributeFlagsEnum)Flags; } set { Flags = (int)value; } }
    public Ikon.GD.IKCAT_AttributeDataTypeEnum DataTypeEnum { get { return (Ikon.GD.IKCAT_AttributeDataTypeEnum)DataType; } set { DataType = (int)value; } }
    //
    public string DataAsString { get { return Utility.LinqBinaryGetStringDB(this.Data); } set { this.Data = Utility.LinqBinarySetStringDB(value); } }
    //
    private KeyValueObjectTree _Labels = null;
    public KeyValueObjectTree Labels { get { return _Labels ?? (_Labels = KeyValueObjectTree.Deserialize(DataAsString)); } }
    public void LabelsUpdate() { DataAsString = Labels.Serialize(); }
    public KeyValueObjectTree LabelsLanguageKVT(params string[] keys) { return Labels.KeyFilterTry(IKGD_Language_Provider.Provider.LanguageNN, keys); }
    //
    public Ikon.GD.IKCAT_TagFlagsEnum TagFlagsEnum { get { return (Ikon.GD.IKCAT_TagFlagsEnum)Flags; } set { Flags = (int)value; } }
    //
    public bool IsCategory { get { return this.AttributeCode == null; } }
    public string AttributeTypeAndCode { get { return (AttributeType ?? string.Empty) + "|" + (AttributeCode ?? string.Empty); } }
    //
    public override string ToString() { return string.Format("[{0}] {1}", AttributeId, AttributeTypeAndCode); }
    //
  }

  //
  // Flags per la definizione dei tipi di attributo di IKATT
  //
  [Flags]
  public enum IKATT_AttributeFlagsEnum
  {
    //[Description("---")]
    //None = 0,
    //[Description("Attributo non editabile")]
    //NotEditable = 1 << 0,
    [Description("Attributo attivo per i filtri di ricerca")]
    SearchFilter = 1 << 1,
    //[Description("Attributo autogenerato dal CMS")]
    //AutoGenerated = 1 << 2,
    //[Description("Attributo ausiliario utilizzato solo per definire il tree ma non associabile al catalogo")]
    //NotBindable = 1 << 3
    // e altri...
  };

  public partial class IKATT_Attribute
  {
    public Ikon.GD.IKATT_AttributeFlagsEnum FlagsEnum { get { return (Ikon.GD.IKATT_AttributeFlagsEnum)Flags; } set { Flags = (int)value; } }

    public string DataAsString { get { return Utility.LinqBinaryGetStringDB(this.Data); } set { this.Data = Utility.LinqBinarySetStringDB(value); } }
    //
    private KeyValueObjectTree _Labels = null;
    public KeyValueObjectTree Labels { get { return _Labels ?? (_Labels = KeyValueObjectTree.Deserialize(DataAsString)); } }
    public void LabelsUpdate() { DataAsString = Labels.Serialize(); }
    public KeyValueObjectTree LabelsLanguageKVT(params string[] keys) { return Labels.KeyFilterTry(IKGD_Language_Provider.Provider.LanguageNN, keys); }
    //
    public Ikon.GD.IKATT_AttributeFlagsEnum VarianteFlagsEnum { get { return (Ikon.GD.IKATT_AttributeFlagsEnum)Flags; } set { Flags = (int)value; } }
    //
    public bool IsCategory { get { return this.AttributeCode == null; } }
    public string AttributeTypeAndCode { get { return (AttributeType ?? string.Empty) + "|" + (AttributeCode ?? string.Empty); } }
    //
    public override string ToString() { return string.Format("[{0}] {1}", AttributeId, AttributeTypeAndCode); }
    //
  }

  public partial class SSO_KEYVALUE
  {
    public enum SSO_KEYVALUE_TypeEnum { String = 0, Integer = 1, Double = 2, DateTime = 3, DateTimeRange = 4, Boolean = 5 };
    public SSO_KEYVALUE_TypeEnum TypeEnum { get { return (SSO_KEYVALUE_TypeEnum)Type; } set { Type = (int)value; } }
  }



  public partial class IKG_HITLOG
  {
    public int IKCMS_UserIdLL { get { return wID; } set { wID = value; } }  // user id da mapping su lazy login
    public int IKCMS_ResourceCode { get { return resID; } set { resID = value; } }  // sNode/rNode della risorsa (secondo il tipo di operazione loggata)
    public int? IKCMS_ActionCode { get { return action; } set { action = value; } }  // tipo di action loggata (es: page hit, ajax operation, ....)
    public int? IKCMS_ActionSubCode { get { return code; } set { code = value; } }  // sub type della action (es: page type (catalog, ambienti, ...), oppure ajax action type, ...)
  }


  public partial class IKG_HITACC
  {
    public double ValueSum { get { return Hits.GetValueOrDefault() * Value.GetValueOrDefault(); } }
  }



  public enum IKGD_QueueMetaStatusEnum
  {
    [Description("Elemento in attesa di esecuzione")]
    Queued,
    [Description("Elemento in corso di esecuzione")]
    Processing,
    [Description("Elemento completato")]
    Processed,
    [Description("Elemento non completato a causa di errori durante l'esecuzione")]
    Error
  };


  //
  // codici per le operazioni registrate in IKGD_QueueMeta
  // i codici < 0 sono associati ad operazioni gestite dal sistema, per le operazioni custom utilizzare codici propri > 0
  //
  public enum IKGD_QueueMetaTypeEnum
  {
    VFS_COW = -1,
    IKCAT_Update = -2,
    VFS_Cleaning = -1000,
    DummyOperation = int.MinValue
  };


  public partial class IKGD_QueueMeta
  {
    public Ikon.GD.IKGD_QueueMetaStatusEnum StatusEnum { get { return (Ikon.GD.IKGD_QueueMetaStatusEnum)Status; } set { Status = (int)value; } }
    public Ikon.GD.IKGD_QueueMetaTypeEnum TypeEnum { get { return (Ikon.GD.IKGD_QueueMetaTypeEnum)Type; } set { Type = (int)value; } }
  }


  public partial class IKGD_QueueData
  {
    //
    private KeyValueObjectTree _DataKVT = null;
    public KeyValueObjectTree DataKVT { get { return _DataKVT ?? (_DataKVT = KeyValueObjectTree.Deserialize(Data)); } }
    public void LabelsUpdate() { Data = DataKVT.Serialize(); }
    //
  }



}

