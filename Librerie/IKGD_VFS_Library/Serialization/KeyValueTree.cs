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
using System.Text;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using Newtonsoft.Json;
using LinqKit;

using Ikon;


namespace Ikon.IKCMS
{

  public interface KeyValueTree_Interface<K, V> : IEnumerable<KeyValueTree_Interface<K, V>>
  {
    K Key { get; set; }
    V Value { get; set; }
    string ValueString { get; }
    string ValueStringNN { get; }
    //
    T ValueT<T>();
    T ValueT<T>(T valueNew);
    //
    IEnumerable<KeyValueTree_Interface<K, V>> Nodes { get; }
    KeyValueTree_Interface<K, V> Parent { get; set; }
    int Level { get; }
    KeyValueTree_Interface<K, V> this[K key] { get; }
    bool ContainsKey(K key);
    IEnumerable<KeyValueTree_Interface<K, V>> RecurseOnTree { get; }
    void Normalize();
    string Serialize();
    string Serialize(bool normalize);
    //
    void Merge<T>(T extKVT) where T : KeyValueTree_Interface<K, V>;
    void Merge<T>(T extKVT, bool processSelf) where T : KeyValueTree_Interface<K, V>;

  }


  /*
  //
  // KeyValueTree: versione specializzata per string con keys case insensitive
  //
  [Serializable]
  [DataContract(IsReference = true)]  // non viene ereditato e si deve specificarlo nuovamente
  public class KeyValueTree : KeyValueTree<string, string>
  {

    public KeyValueTree()
      : base()
    { }

    public KeyValueTree(KeyValueTree parentNode, string key, string value)
      : base(parentNode, key, value)
    { }


    public override KeyValueTree_Interface<string, string> this[string key]
    {
      get
      {
        return Nodes.FirstOrDefault(n => string.Equals(n.Key, key, StringComparison.OrdinalIgnoreCase)) ?? new KeyValueTree(this, key, null);
      }
    }

    public static new KeyValueTree Deserialize(string json) { return DeserializeHelper<KeyValueTree>(json); }

  }
  */


  [Serializable]
  [JsonObject(MemberSerialization.OptIn, IsReference = true)]
  [DataContract(IsReference = true)]
  public class KeyValueTree<K, V> : KeyValueTree_Interface<K, V>
    where K : IEquatable<K>
  {
    [DataMember]
    public virtual K Key { get; set; }

    [DataMember]
    public virtual V Value { get; set; }
    public virtual string ValueString { get { return ((object)Value != null ? ((object)Value).ToString() : null); } }
    public virtual string ValueStringNN { get { return ((object)Value ?? string.Empty).ToString(); } }

    public virtual T ValueT<T>() { return (T)((object)Value ?? default(T)); }
    public virtual T ValueT<T>(T valueNew) { return (T)((object)(Value = (V)(object)valueNew) ?? default(T)); }


    [DataMember]
    protected List<KeyValueTree<K, V>> _Nodes;
    public virtual IEnumerable<KeyValueTree_Interface<K, V>> Nodes { get { return _Nodes.OfType<KeyValueTree_Interface<K, V>>(); } }


    [DataMember]
    protected KeyValueTree<K, V> _Parent = null;
    public virtual KeyValueTree_Interface<K, V> Parent
    {
      get { return _Parent; }
      set
      {
        if (_Parent != null)
          _Parent._Nodes.Remove(this);
        _Parent = value as KeyValueTree<K, V>;
        if (_Parent != null)
          _Parent._Nodes.Add(this);
      }
    }


    public KeyValueTree()
    {
      this._Nodes = new List<KeyValueTree<K, V>>();
    }


    public KeyValueTree(KeyValueTree<K, V> parentNode, K key, V value)
      : this()
    {
      this.Key = key;
      this.Value = value;
      this.Parent = parentNode;
    }


    public virtual int Level { get { return (Parent == null) ? 0 : Parent.Level + 1; } }


    public override string ToString()
    {
      try { return string.Format("{{[{0},{1}]}}", Key, Value); }
      catch { return "NULL"; }
    }



    public virtual KeyValueTree_Interface<K, V> this[K key]
    {
      get
      {
        return Nodes.FirstOrDefault(n => n.Key.Equals(key)) ?? new KeyValueTree<K, V>(this, key, default(V));
      }
    }


    public virtual bool ContainsKey(K key)
    {
      return Nodes.Any(n => n.Key.Equals(key));
    }


    public virtual IEnumerable<KeyValueTree_Interface<K, V>> RecurseOnTree
    {
      get
      {
        yield return this;
        foreach (KeyValueTree_Interface<K, V> node in Nodes)
          foreach (KeyValueTree_Interface<K, V> subNode in node.RecurseOnTree)
            yield return subNode;
      }
    }


    //TODO: riformulare per bene in modo da fare le correzioni senza incasinare le liste
    public void Normalize()
    {
      foreach (KeyValueTree_Interface<K, V> node in Nodes.ToList())
        node.Normalize();
      if (!this.Nodes.Any() && this.Value == null)
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


    public static KeyValueTree<K, V> Deserialize(string json) { return DeserializeHelper<KeyValueTree<K, V>>(json); }
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


    public virtual IEnumerator<KeyValueTree_Interface<K, V>> GetEnumerator() { return Nodes.OfType<KeyValueTree_Interface<K, V>>().GetEnumerator(); }
    System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() { return Nodes.GetEnumerator(); }


    public void Merge<T>(T extKVT) where T : KeyValueTree_Interface<K, V> { Merge(extKVT, false); }
    public void Merge<T>(T extKVT, bool processSelf) where T : KeyValueTree_Interface<K, V>
    {
      if (extKVT == null)
        return;
      try
      {
        if (processSelf && object.Equals(this.Key, extKVT.Key))
          this.Value = extKVT.Value;
        extKVT.Nodes.Select(n => n.Key).Except(this.Nodes.Select(n => n.Key)).ToList().ForEach(k => Activator.CreateInstance(typeof(T), this, k, default(V)));
        extKVT.Nodes.Select(n => n.Key).ForEach(k => this[k].Merge(extKVT[k], true));
      }
      catch { }
    }


  }  //class KeyValueTree<K, V>


}
