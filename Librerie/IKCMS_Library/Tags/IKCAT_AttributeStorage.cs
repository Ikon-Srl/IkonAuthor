using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Text.RegularExpressions;
using System.Web;
using System.Xml.Linq;
using System.Web.Mvc;
using System.Web.Mvc.Html;
using System.Web.Caching;
using System.Reflection;
using System.Diagnostics;
using LinqKit;
using Autofac;

using Ikon;
using Ikon.GD;
using Ikon.IKGD.Library;


namespace Ikon.IKCMS
{

  //
  // storage degli attributi tramite dictionary e tree
  //
  public static class IKCAT_AttributeStorage
  {
    public static Utility.DictionaryMV<int, FS_Operations.FS_TreeNode<IKCAT_Attribute>> AttributesAll { get { return DataStorageCached.AttributesAll; } }
    public static FS_Operations.FS_TreeNode<IKCAT_Attribute> TreeAttributes { get { return DataStorageCached.TreeAttributes; } }
    //
    public static IEnumerable<IKCAT_Attribute> Attributes { get { return DataStorageCached.Attributes; } }
    public static IEnumerable<IKCAT_Attribute> AttributesNoACL { get { return DataStorageCached.AttributesNoACL; } }
    //
    private static object _lock = new object();
    //


    static IKCAT_AttributeStorage()
    {
    }


    public static void Reset() { FS_OperationsHelpers.CachedEntityClear(_lock, typeof(IKCAT_AttributeStorage).Name); }

    public static DataStorage DataStorageCached
    {
      get
      {
        return FS_OperationsHelpers.CachedEntityWrapperLock(_lock, typeof(IKCAT_AttributeStorage).Name
          , () => { return new DataStorage(); }
          , m => m != null && m.Initialized
        , Utility.TryParse<int>(IKGD_Config.AppSettings["CachingIKCMS_Models"], 3600), new string[] { "IKCAT_Attribute" });
      }
    }


    public static FS_Operations.FS_TreeNode<IKCAT_Attribute> FindTag(int? Id, string AttributeType, string AttributeCode)
    {
      try
      {
        if (Id != null)
          return AttributesAll[Id.Value];
        else
        {
          //return DataStorageCached.AttributesLookup[AttributeType + "|" + AttributeCode].FirstOrDefault(a => a.Data.AttributeType == AttributeType && a.Data.AttributeCode == AttributeCode);
          return AttributesAll.Values.FirstOrDefault(a => a.Data.AttributeType == AttributeType && a.Data.AttributeCode == AttributeCode);
        }
      }
      catch { return null; }
    }


    public static IKCAT_Attribute GetTag(int? Id, string AttributeType, string AttributeCode)
    {
      try { return FindTag(Id, AttributeType, AttributeCode).Data; }
      catch { return null; }
    }


    public static int? GetTagId(string AttributeType, string AttributeCode)
    {
      try { return FindTag(null, AttributeType, AttributeCode).Data.AttributeId; }
      catch { return null; }
    }


    public static IKCAT_Attribute GetTagCategory(int? Id, string AttributeType)
    {
      try
      {
        if (Id != null)
          AttributeType = AttributesAll[Id.Value].Data.AttributeType;
        return Attributes.FirstOrDefault(a => a.AttributeType == AttributeType && a.AttributeCode == null);
      }
      catch { return null; }
    }


    public static IEnumerable<IKCAT_Attribute> GetTags(IEnumerable<int> Ids)
    {
      try { return AttributesAll.Values.Join(Ids, a => a.Data.AttributeId, i => i, (a, i) => a.Data); }
      catch { return Enumerable.Empty<IKCAT_Attribute>(); }
    }


    public static IEnumerable<IKCAT_Attribute> GetTagsOrdered(IEnumerable<int> Ids)
    {
      try { return AttributesAll.Values.Join(Ids, a => a.Data.AttributeId, i => i, (a, i) => a.Data).OrderBy(a => Ids.ToList().IndexOfSortable(a.AttributeId)); }
      catch { return Enumerable.Empty<IKCAT_Attribute>(); }
    }


    public static IEnumerable<FS_Operations.FS_TreeNode<IKCAT_Attribute>> GetTagsNodes(IEnumerable<int> Ids)
    {
      try { return AttributesAll.Values.Join(Ids, a => a.Data.AttributeId, i => i, (a, i) => a); }
      catch { return Enumerable.Empty<FS_Operations.FS_TreeNode<IKCAT_Attribute>>(); }
    }


    public static IEnumerable<FS_Operations.FS_TreeNode<IKCAT_Attribute>> GetTagsNodesOrdered(IEnumerable<int> Ids)
    {
      try { return AttributesAll.Values.Join(Ids, a => a.Data.AttributeId, i => i, (a, i) => a).OrderBy(a => Ids.ToList().IndexOfSortable(a.Data.AttributeId)); }
      catch { return Enumerable.Empty<FS_Operations.FS_TreeNode<IKCAT_Attribute>>(); }
    }


    // per i Tags semplici non passare nessuna key
    public static string GetTagLabel(int Id, params string[] keys)
    {
      try { return AttributesAll[Id].Data.LabelsLanguageKVT(keys).ValueString; }
      catch { return null; }
    }


    //
    // da una lista di AttributeIds seleziona la label del primo attributo con l'AttributeType specificato
    public static string GetTagLabel(IEnumerable<int> Ids, string AttributeType, params string[] keys)
    {
      try { return AttributesAll.Values.Where(a => a.Data.AttributeType == AttributeType).Join(Ids, a => a.Data.AttributeId, i => i, (a, i) => a.Data).FirstOrDefault().LabelsLanguageKVT(keys).ValueString; }
      catch { return null; }
    }


    // filtra una lista di AttributeIds per una categoria specifica
    public static IEnumerable<int> FilterTagsByType(IEnumerable<int> Ids, string AttributeType)
    {
      try { return AttributesAll.Values.Where(a => a.Data.AttributeType == AttributeType).Join(Ids, a => a.Data.AttributeId, i => i, (a, i) => i); }
      catch { return Enumerable.Empty<int>(); }
    }


    //
    // normalizzatore per i codici dei tags:
    // sostituisce le accentate sostituisce qualsiasi spazio e carattere speciale con _ e condensa i _ multipli
    //
    private static Regex TagCodeNormalizer_RegEx01 = new Regex(@"_{2,}", RegexOptions.Compiled);  // condensazione dei _ multipli
    public static string TagCodeNormalizer(string code)
    {
      if (!string.IsNullOrEmpty(code))
      {
        StringBuilder sb = new StringBuilder();
        foreach (char c in code.ReplaceAccents())
          sb.Append(char.IsLetterOrDigit(c) ? c : '_');
        code = TagCodeNormalizer_RegEx01.Replace(sb.ToString(), "_").Trim(' ', '_');
      }
      return code;
    }


    public static IKCAT_Attribute_Comparer Comparer { get { return new IKCAT_Attribute_Comparer(); } }
    public static IKCAT_AttributeNode_Comparer ComparerTree { get { return new IKCAT_AttributeNode_Comparer(); } }


    public static SelectListItem ToSelectListItem(this IKCAT_Attribute attribute, List<int> tagsSelected)
    {
      SelectListItem item = new SelectListItem();
      if (attribute != null)
      {
        item.Value = attribute.AttributeId.ToString();
        item.Text = attribute.Labels.KeyFilterTry(IKGD_Language_Provider.Provider.LanguageNN).ValueString ?? attribute.Labels.ValueString;
        if (tagsSelected != null && tagsSelected.Any())
        {
          item.Selected = tagsSelected.Contains(attribute.AttributeId);
        }
      }
      return item;
    }


    public static string GetUrlStream(this IKCAT_Attribute attribute, string stream) { return GetUrlStream(attribute, stream, null); }
    public static string GetUrlStream(this IKCAT_Attribute attribute, string stream, bool? fullUrl)
    {
      return IKCMS_RouteUrlManager.GetUrlProxyATTR(false, attribute.AttributeId, stream, null, fullUrl.GetValueOrDefault(false), null, null, attribute.Name);
    }


    public class DataStorage : IKGD_CachingHelper_CacheItemHasRemovedCallback_Interface
    {
      public Utility.DictionaryMV<int, FS_Operations.FS_TreeNode<IKCAT_Attribute>> AttributesAll { get; protected set; }
      public FS_Operations.FS_TreeNode<IKCAT_Attribute> TreeAttributes { get; protected set; }
      //public ILookup<string, FS_Operations.FS_TreeNode<IKCAT_Attribute>> AttributesLookup { get; protected set; }
      //
      public IEnumerable<IKCAT_Attribute> Attributes { get { return AttributesAll.Values.Where(a => a.Data.FlagActive).Select(a => a.Data); } }
      public IEnumerable<IKCAT_Attribute> AttributesNoACL { get { return AttributesAll.Values.Select(a => a.Data); } }
      //
      public bool Initialized { get; set; }
      //


      public DataStorage()
      {
        Setup();
      }


      public void Setup()
      {
        try
        {
          AttributesAll = new Utility.DictionaryMV<int, FS_Operations.FS_TreeNode<IKCAT_Attribute>>();
          TreeAttributes = new FS_Operations.FS_TreeNode<IKCAT_Attribute>(null, null);
          //
          //FS_Operations fsOp = IKCMS_ManagerIoC.requestContainer.ResolveNamed<FS_Operations>();
          FS_Operations fsOp = IKCMS_ManagerIoC.requestContainer.ResolveNamed<FS_Operations>("readonly");
          fsOp.EnsureOpenConnection();
          bool useRealTransactions = Utility.TryParse<bool>(IKGD_Config.AppSettings["IKGD_ConfigVFS_TransactionsEnabled"], false);
          using (System.Transactions.TransactionScope ts = useRealTransactions ? IKGD_TransactionFactory.TransactionReadUncommitted(600) : IKGD_TransactionFactory.TransactionNone(600))
          {
            //
            fsOp.DB.IKCAT_Attributes.ForEach(a => AttributesAll.Add(a.AttributeId, new FS_Operations.FS_TreeNode<IKCAT_Attribute>(null, new IKCAT_Attribute
            {
              AttributeId = a.AttributeId,
              AttributeType = a.AttributeType,
              AttributeCode = a.AttributeCode,
              ParentAttributeId = a.ParentAttributeId,
              FlagSystem = a.FlagSystem,
              FlagActive = a.FlagActive,
              Flags = a.Flags,
              DepthLevel = a.DepthLevel,
              DataType = a.DataType,
              Position = a.Position,
              Name = a.Name,
              Text = a.Text,
              //Modif = a.Modif,  // attualmente non utilizzati
              //Value = a.Value,  // attualmente non utilizzati
              Data = a.Data
            })));
            //
            ts.Committ();
          }
          //
          AttributesAll.Values.Where(a => a.Data != null).OrderBy(a => a.Data.ParentAttributeId).ThenBy(a => a.Data.Position).ThenBy(a => a.Data.AttributeTypeAndCode).GroupJoin(AttributesAll.Values.Where(a => a.Data != null), a => a.Data.ParentAttributeId, a => a.Data.AttributeId, (attr, parents) => new { attr, parents }).ForEach(r => r.attr.Parent = r.parents.OrderBy(a => a.Data.Position).FirstOrDefault() ?? TreeAttributes);
          //
          //ILookup<string, FS_Operations.FS_TreeNode<IKCAT_Attribute>> AttributesLookup = AttributesAll.ToLookup(r => r.Value.Data.AttributeType + "|" + r.Value.Data.AttributeCode, r => r.Value);
          //
          Initialized = true;
          //
        }
        catch (Exception ex)
        {
          Elmah.ErrorSignal.FromCurrentContext().Raise(ex);
        }
        //catch { }
      }


      public CacheItemRemovedCallback CachingHelper_onRemoveCallback
      {
        get
        {
          return (key, value, reason) =>
          {
            try
            {
              (value as DataStorage).AttributesAll.ClearCachingFriendly();
              // TODO: implementare pulizia caching per tree storage
              //(value as DataStorage).TreeAttributes.Reverse().ClearEnumerableCachingFriendly(m => m.Clear());
              //(value as DataStorage).TreeAttributes.Reverse().ClearEnumerableCachingFriendly(m => m.Clear());
            }
            catch { }
          };
        }
      }


    }


  }



  public class IKCAT_Attribute_Comparer : IComparer<IKCAT_Attribute>
  {
    protected IKCAT_AttributeNode_Comparer comparerTreeNode = new IKCAT_AttributeNode_Comparer();

    public int Compare(IKCAT_Attribute x, IKCAT_Attribute y)
    {
      if (x == null && y == null)
        return 0;
      else if (x == null)
        return -1;
      else if (y == null)
        return +1;
      else if (x.AttributeId == y.AttributeId)
        return 0;
      if (x.ParentAttributeId == y.ParentAttributeId)
        return Comparer<double>.Default.Compare(x.Position, y.Position);
      try { return comparerTreeNode.Compare(IKCAT_AttributeStorage.AttributesAll[x.AttributeId], IKCAT_AttributeStorage.AttributesAll[y.AttributeId]); }
      catch { return 0; }
    }
  }


  public class IKCAT_AttributeNode_Comparer : IComparer<FS_Operations.FS_TreeNode<IKCAT_Attribute>>
  {
    public int Compare(FS_Operations.FS_TreeNode<IKCAT_Attribute> x, FS_Operations.FS_TreeNode<IKCAT_Attribute> y)
    {
      if (x == null && y == null)
        return 0;
      else if (x == null)
        return -1;
      else if (y == null)
        return +1;
      else if (x.Data.AttributeId == y.Data.AttributeId)
        return 0;
      try
      {
        //var lastCommon = x.BackRecurseOnTree.FirstOrDefault(xb => y.BackRecurseOnTree.Any(yb => (xb.Data == null && yb.Data == null) || (xb.Data != null && yb.Data != null && yb.Data.AttributeId == xb.Data.AttributeId)));
        var lastCommon = x.BackRecurseOnTree.Intersect(y.BackRecurseOnTree).FirstOrDefault();
        int idx1 = x.BackRecurseOnTree.Max(a => lastCommon.Nodes.IndexOf(a));
        int idx2 = y.BackRecurseOnTree.Max(a => lastCommon.Nodes.IndexOf(a));
        return idx1 - idx2;
      }
      catch { return 0; }
    }
  }



}
