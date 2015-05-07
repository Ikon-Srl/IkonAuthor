/*
 * 
 * Intranet Ikon
 * 
 * Copyright (C) 2010 Ikon Srl
 * Tutti i Diritti Riservati. All Rights Reserved
 * 
 */

using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Text.RegularExpressions;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using Newtonsoft.Json;
using LinqKit;

using Ikon;


namespace Ikon.IKCMS
{


  //
  // KeyValueTree: versione specializzata per string con keys case insensitive
  // praticamente reimplementa tutto KeyValueTree<string, object> per problemi nella serializzazione e covarianza
  //
  [Serializable]
  [JsonObject(MemberSerialization.OptIn, IsReference = true)]
  [DataContract(IsReference = true)]
  public class KeyValueObjectTree : IEnumerable<KeyValueObjectTree>
  {
    [DataMember]
    public virtual string Key { get; set; }

    [DataMember]
    public virtual object Value { get; set; }
    public virtual string ValueString { get { return ((object)Value != null ? ((object)Value).ToString() : null); } }
    public virtual string ValueStringNN { get { return ((object)Value ?? string.Empty).ToString(); } }

    public virtual T ValueT<T>(T valueNew) { return (T)((object)(Value = (object)valueNew) ?? default(T)); }
    public virtual T ValueT<T>()
    {
      try { return (T)((object)Value ?? default(T)); }
      catch { }
      try { return (T)Convert.ChangeType((object)Value ?? default(T), typeof(T)); }
      catch { }
      return default(T);
    }

    // gli oggetti complessi (es. classi non di sistema) non vengono deserializzati completamente ma diventano un Newtonsoft.Json.Linq.JContainer
    // che deve essere poi convertito nell'oggeto corretto
    public virtual T ValueComplex<T>()
    {
      try
      {
        return Ikon.GD.IKGD_Serialization.DeSerializeJSON<T>(Value.ToString());
      }
      catch { return default(T); }
    }

    public virtual T TryGetValue<T>() { return TryGetValue(default(T)); }
    public virtual T TryGetValue<T>(T defaultValue)
    {
      try { return (T)((object)Value ?? defaultValue); }
      catch { }
      try { return (T)Convert.ChangeType((object)Value ?? defaultValue, typeof(T)); }
      catch { }
      return defaultValue;
    }


    [DataMember]
    protected List<KeyValueObjectTree> _Nodes;
    public virtual IEnumerable<KeyValueObjectTree> Nodes { get { return _Nodes.OfType<KeyValueObjectTree>(); } }

    // solo per manipolazioni particolari e non per l'utilizzo generale
    public List<KeyValueObjectTree> NodesStorage { get { return _Nodes; } }


    [DataMember]
    protected KeyValueObjectTree _Parent = null;
    public virtual KeyValueObjectTree Parent
    {
      get { return _Parent; }
      set
      {
        if (_Parent != null)
          _Parent._Nodes.Remove(this);
        _Parent = value;
        if (_Parent != null)
          _Parent._Nodes.Add(this);
      }
    }


    public virtual KeyValueObjectTree Root { get { return (_Parent != null) ? _Parent.Root : this; } }


    public KeyValueObjectTree()
    {
      this._Nodes = new List<KeyValueObjectTree>();
    }


    public KeyValueObjectTree(KeyValueObjectTree parentNode, string key, object value)
      : this(parentNode, key, value, null)
    { }


    public KeyValueObjectTree(KeyValueObjectTree parentNode, string key, object value, int? index)
      : this()
    {
      this.Key = key;
      this.Value = value;
      this._Parent = parentNode;
      if (parentNode != null && index != null && index < parentNode._Nodes.Count - 1)
      {
        parentNode._Nodes.Insert(index.Value, this);
      }
      else if (parentNode != null)
      {
        parentNode._Nodes.Add(this);
      }
    }


    public virtual int Level { get { return (Parent == null) ? 0 : Parent.Level + 1; } }


    public override string ToString() { return ToString(true); }
    public virtual string ToString(bool shortFormat)
    {
      try
      {
        if (shortFormat)
        {
          return string.Format("{{{0}[{1}]={2}}}", Level, Utility.Implode(ParentsAndSelf.Select(n => n.Key).Reverse().Skip(1), ","), Value);
        }
        else
        {
          return string.Format("{{({2})[{3},{1}]}}", Key, Value, Level, Utility.Implode(ParentsAndSelf.Select(n => n.Key).Reverse().Skip(1), ","));
        }
      }
      catch { return "NULL"; }
    }


    public virtual string DumpAsString { get { return Utility.Implode(RecurseOnTree.Select(n => n.ToString()), "\n"); } }



    public virtual KeyValueObjectTree this[int idx] { get { return _Nodes[idx]; } }
    public virtual KeyValueObjectTree this[string key]
    {
      get
      {
        return Nodes.FirstOrDefault(n => string.Equals(n.Key, key, StringComparison.OrdinalIgnoreCase)) ?? new KeyValueObjectTree(this, key, null);
      }
    }


    public static Regex SystemKeysRx = new Regex(@"(^__|__$)", RegexOptions.Compiled | RegexOptions.Singleline);
    public virtual bool IsSystemKey { get { return SystemKeysRx.IsMatch(this.Key ?? string.Empty); } }


    public virtual bool ContainsKey(string key)
    {
      return Nodes.Any(n => string.Equals(n.Key, key, StringComparison.OrdinalIgnoreCase));
    }


    public virtual KeyValueObjectTree GetNN(params string[] keys) { return Get(keys) ?? new KeyValueObjectTree(); }
    public virtual KeyValueObjectTree Get(params string[] keys)
    {
      KeyValueObjectTree node = this;
      foreach (string key in keys)
      {
        node = node.Nodes.FirstOrDefault(n => string.Equals(n.Key, key, StringComparison.OrdinalIgnoreCase));
        if (node == null)
          break;
      }
      return node;
    }


    // solo per uso interno
    public virtual IEnumerable<KeyValueObjectTree> GetMulti(params string[] keys)
    {
      IEnumerable<KeyValueObjectTree> nodes = null;
      KeyValueObjectTree node = this;
      foreach (string key in keys)
      {
        nodes = node.Nodes.Where(n => string.Equals(n.Key, key, StringComparison.OrdinalIgnoreCase));
        node = nodes.FirstOrDefault();
        if (node == null)
        {
          nodes = null;
          break;
        }
      }
      return nodes;
    }


    public virtual KeyValueObjectTree Ensure(params string[] keys)
    {
      KeyValueObjectTree node = this;
      keys.ForEach(k => node = node[k]);
      return node;
    }


    public virtual KeyValueObjectTree Remove(params string[] keys)
    {
      KeyValueObjectTree node = this;
      foreach (string key in keys)
      {
        node = node.Nodes.FirstOrDefault(n => string.Equals(n.Key, key, StringComparison.OrdinalIgnoreCase));
        if (node == null)
          break;
      }
      if (node != null)
        node.Parent = null;
      return node;
    }


    public virtual KeyValueObjectTree Unlink()
    {
      this.Parent = null;
      return this;
    }


    public virtual IEnumerable<KeyValueObjectTree> ParentsAndSelf
    {
      get
      {
        yield return this;
        if (_Parent != null)
          foreach (var p in _Parent.ParentsAndSelf)
            yield return p;
      }
    }


    public virtual IEnumerable<KeyValueObjectTree> RecurseOnTree
    {
      get
      {
        yield return this;
        foreach (KeyValueObjectTree node in Nodes)
          foreach (KeyValueObjectTree subNode in node.RecurseOnTree)
            yield return subNode;
      }
    }


    public virtual IEnumerable<KeyValueObjectTree> RecurseOnTreeFiltered(Func<KeyValueObjectTree, bool> filter) { return RecurseOnTreeFiltered(filter, true); }
    public virtual IEnumerable<KeyValueObjectTree> RecurseOnTreeFiltered(Func<KeyValueObjectTree, bool> filter, bool skipFilterForSelf)
    {
      Func<KeyValueObjectTree, bool> _filter = filter ?? PredicateBuilder.True<KeyValueObjectTree>().Compile();
      if (skipFilterForSelf == true || _filter(this))
      {
        yield return this;
        foreach (KeyValueObjectTree node in Nodes.Where(_filter))
          foreach (KeyValueObjectTree subNode in node.RecurseOnTreeFiltered(_filter, false))
            yield return subNode;
      }
    }


    public void Normalize() { Normalize(false); }
    public void Normalize(bool nullOrWhiteSpace)
    {
      foreach (KeyValueObjectTree node in Nodes.ToList())
        node.Normalize(nullOrWhiteSpace);
      if (!this.Nodes.Any() && (this.Value == null || (nullOrWhiteSpace && this.Value != null && this.Value is string && (this.Value as string).IsNullOrWhiteSpace())))
        this.Parent = null;
    }


    public virtual string Serialize() { return Serialize(false); }
    public virtual string Serialize(bool normalize)
    {
      //
      // la serializzazione tipo json convenzionale non funziona nel caso di oggetti ricorsivi con riferimenti
      // ed e' necessario settare attributi alla classe per sistemare la serializzazione.
      // la serializzazione tipo json e' comunque piu' compatta di quella convenzionale con Xml (~ 1/3 - 1/2)
      //
      if (normalize)
        Normalize();
      try { return Ikon.GD.IKGD_Serialization.SerializeToJSON(this); }
      catch { return null; }
      //try { return Ikon.Serialization.Ikon_Serialization.SerializelDCtoXml(this); }
      //catch { return null; }
    }


    public static KeyValueObjectTree Deserialize(string json) { return DeserializeHelper<KeyValueObjectTree>(json); }
    //
    public static T DeserializeHelper<T>(string json) where T : class, new()
    {
      T result = null;
      try { result = Ikon.GD.IKGD_Serialization.DeSerializeJSON<T>(json); }
      catch { }
      //try { result = Ikon.Serialization.Ikon_Serialization.UnSerializeDCfromXml<T>(json); }
      //catch { }
      if (result == null)
        result = new T();
      return result;
    }


    public virtual IEnumerator<KeyValueObjectTree> GetEnumerator() { return Nodes.OfType<KeyValueObjectTree>().GetEnumerator(); }
    System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() { return Nodes.GetEnumerator(); }


    public void Merge<T>(T extKVT) where T : KeyValueObjectTree { Merge(extKVT, false, null); }
    public void Merge<T>(T extKVT, bool processSelf) where T : KeyValueObjectTree { Merge(extKVT, processSelf, null); }
    public void Merge<T>(T extKVT, bool processSelf, bool? processLeafsOnlyOnExt) where T : KeyValueObjectTree
    {
      if (extKVT == null)
        return;
      try
      {
        if (processSelf && string.Equals(this.Key, extKVT.Key, StringComparison.OrdinalIgnoreCase))
        {
          if (processLeafsOnlyOnExt.GetValueOrDefault(false) == false || !extKVT.Nodes.Any())
          {
            this.Key = extKVT.Key;
            this.Value = extKVT.Value;
          }
        }
        //
        //extKVT.Nodes.Select(n => n.Key).Except(this.Nodes.Select(n => n.Key)).ToList().ForEach(k => new KeyValueObjectTree(this, k, null));
        //extKVT.Nodes.Select(n => n.Key).ForEach(k => this[k].Merge(extKVT[k], true));
        //
        foreach (var nodesGrp in extKVT.Nodes.GroupBy(n => n.Key, StringComparer.OrdinalIgnoreCase))
        {
          var nodesExt = nodesGrp.OrderBy(n => extKVT.NodesStorage.IndexOf(n)).ToList();
          var nodes = NodesStorage.Where(n => string.Equals(n.Key, nodesGrp.Key, StringComparison.OrdinalIgnoreCase)).ToList();
          for (int i = 0; i < nodesExt.Count; i++)
          {
            if (i >= nodes.Count)
            {
              nodes.Add(new KeyValueObjectTree(this, nodesExt[i].Key, nodesExt[i].Value));
            }
            nodes[i].Merge(nodesExt[i], true, processLeafsOnlyOnExt);
          }
        }
      }
      catch { }
    }


    // duplicazione di un nodo (senza il parent)
    public KeyValueObjectTree Clone()
    {
      KeyValueObjectTree KVT = new KeyValueObjectTree(null, this.Key, this.Value);
      foreach (KeyValueObjectTree node in this.Nodes)
        node.Clone().Parent = KVT;
      return KVT;
    }


    // duplicazione di un subtree su un nodo esistente
    public KeyValueObjectTree CloneFrom(KeyValueObjectTree SRC)
    {
      if (SRC != null)
      {
        this.Key = SRC.Key;
        this.Value = SRC.Value;
        _Nodes.Clear();
        foreach (KeyValueObjectTree node in SRC.Nodes)
          node.Clone().Parent = this;
      }
      else
      {
        _Nodes.Clear();
      }
      return this;
    }


    public KeyValueObjectTree CloneFromScan(KeyValueObjectTree SRC)
    {
      if (SRC != null)
      {
        var keySet = SRC.ParentsAndSelf.Select(n => n.Key).Reverse().Skip(1).ToArray();
        var node = this.Ensure(keySet);
        return node.CloneFrom(SRC);
      }
      return this;
    }


    //
    // accesso filtrato al KVT per controllare il comportamento in caso di key not found
    //

    public enum EnumFilterNotFoundKVT { Empty, Null, First, FirstOrEmpty, Last, LastOrEmpty }

    public virtual KeyValueObjectTree KeyFilter(string key, string keyIfNull, EnumFilterNotFoundKVT filterMode)
    {
      KeyValueObjectTree node = null;
      try
      {
        node = Nodes.FirstOrDefault(n => string.Equals(n.Key, key, StringComparison.OrdinalIgnoreCase));
        if (node == null && keyIfNull != null)
          node = Nodes.FirstOrDefault(n => string.Equals(n.Key, keyIfNull, StringComparison.OrdinalIgnoreCase));
        if (node == null)
        {
          switch (filterMode)
          {
            case EnumFilterNotFoundKVT.Null:
              break;
            case EnumFilterNotFoundKVT.First:
              node = Nodes.FirstOrDefault();
              break;
            case EnumFilterNotFoundKVT.FirstOrEmpty:
              node = Nodes.FirstOrDefault() ?? new KeyValueObjectTree(this, key, null);
              break;
            case EnumFilterNotFoundKVT.Last:
              node = Nodes.LastOrDefault();
              break;
            case EnumFilterNotFoundKVT.LastOrEmpty:
              node = Nodes.LastOrDefault() ?? new KeyValueObjectTree(this, key, null);
              break;
            case EnumFilterNotFoundKVT.Empty:
            default:
              node = new KeyValueObjectTree(this, key, null);
              break;
          }
        }
      }
      catch { }
      return node;
    }



    // puo' ritornare un null
    public virtual KeyValueObjectTree KeyFilterCheck(params string[] keys)
    {
      KeyValueObjectTree node = this.Get(keys);
      node = node ?? this.Get(new string[] { null }.Concat(keys).ToArray());
      return node;
    }
    public virtual KeyValueObjectTree KeyFilterCheck(string keyMain, params string[] keys)
    {
      KeyValueObjectTree node = this.Get(new string[] { keyMain }.Concat(keys).ToArray());
      if (keyMain != null)
        node = node ?? this.Get(new string[] { null }.Concat(keys).ToArray());
      return node;
    }


    // per accessi solo RO non ritorna mai null, in caso un nuovo nodo non connesso
    //public virtual KeyValueObjectTree KeyFilterTry(params string[] keys)
    //{
    //  KeyValueObjectTree node = this.Get(keys);
    //  node = node ?? this.Get(new string[] { null }.Concat(keys).ToArray());
    //  return node ?? new KeyValueObjectTree();
    //}
    //public virtual KeyValueObjectTree KeyFilterTry(string keyMain, params string[] keys)
    //{
    //  KeyValueObjectTree node = this.Get(new string[] { keyMain }.Concat(keys).ToArray());
    //  if (keyMain != null)
    //    node = node ?? this.Get(new string[] { null }.Concat(keys).ToArray());
    //  return node ?? new KeyValueObjectTree();
    //}


    public virtual KeyValueObjectTree KeyFilterGet(string keyMain, params string[] keys)
    {
      KeyValueObjectTree node = this.Get(new string[] { keyMain }.Concat(keys).ToArray());
      if (keyMain != null)
      {
        // si tenta l'accesso alla risorsa senza language
        node = node ?? this.Get(new string[] { null }.Concat(keys).ToArray());
      }
      node = node ?? Ensure(new string[] { keyMain }.Concat(keys).ToArray());
      return node;
    }


    public virtual IEnumerable<KeyValueObjectTree> KeyFilterTryMulti(string keyMain, params string[] keys)
    {
      IEnumerable<KeyValueObjectTree> nodes = this.GetMulti(new string[] { keyMain }.Concat(keys).ToArray());
      if (keyMain != null)
        nodes = nodes ?? this.GetMulti(new string[] { null }.Concat(keys).ToArray());
      return nodes ?? Enumerable.Empty<KeyValueObjectTree>();
    }


  }


  public static class KeyValueObjectTreeHelper
  {
    public static List<string> keyMainFallbacks { get; private set; }

    static KeyValueObjectTreeHelper()
    {
      keyMainFallbacks = Utility.Explode(Ikon.GD.IKGD_Config.AppSettings["KeyValueObject_KeyMainFallbacks"], ",", " ", true).Concat(new string[] { null }).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
    }


    public static KeyValueObjectTree KeyFilterTry(this KeyValueObjectTree KVT, params string[] keys)
    {
      KeyValueObjectTree node = null;
      if (KVT != null)
      {
        node = KVT.Get(keys);
        if (node == null && keys != null)
        {
          node = KVT.Get(new string[] { null }.Concat(keys).ToArray());
        }
      }
      if (node == null)
        node = new KeyValueObjectTree();
      return node;
    }


    public static KeyValueObjectTree KeyFilterTry(this KeyValueObjectTree KVT, string keyMain, params string[] keys)
    {
      KeyValueObjectTree node = null;
      if (KVT != null)
      {
        node = KVT.Get(new string[] { keyMain }.Concat(keys).ToArray());
        if (node == null && keyMain != null && keys != null)
        {
          foreach (string key in keyMainFallbacks.Skip(keyMainFallbacks.IndexOf(keyMain) + 1))
          {
            node = KVT.Get(new string[] { null }.Concat(keys).ToArray());
            if (node != null)
              break;
          }
        }
      }
      if (node == null)
        node = new KeyValueObjectTree();
      return node;
    }


    public static KeyValueObjectTree FromObject(object source) { return FromObject(null, source); }
    public static KeyValueObjectTree FromObject(KeyValueObjectTree parentNode, object source)
    {
      KeyValueObjectTree result = parentNode ?? new KeyValueObjectTree();
      try
      {
        if (source is IDictionary)
        {
          foreach (DictionaryEntry kv in (source as IDictionary))
          {
            FromObject(result[kv.Key.ToString()], kv.Value);
          }
        }
        else if (source is IDictionary<string, object>)
        {
          foreach (KeyValuePair<string, object> kv in (source as IDictionary<string, object>))
          {
            FromObject(result[kv.Key], kv.Value);
          }
        }
        else
        {
          result.Value = source;
        }
      }
      catch { }
      return result;
    }


  }


}
