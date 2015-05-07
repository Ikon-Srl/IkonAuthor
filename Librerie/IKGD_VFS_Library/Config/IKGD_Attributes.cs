/*
 * 
 * IkonPortal
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
using Ikon.IKCMS;


namespace Ikon.IKGD.Library
{

  //
  // interface per la definizione degli attributi associati alla deserializzazione su VFS
  //
  public interface IKGD_DeserializeOnVFS_Attribute_Interface : System.Runtime.InteropServices._Attribute
  {
    Type DeserializerType { get; }
    bool AllowRecursion { get; }
    string BaseKey { get; }
  }


  [global::System.AttributeUsage(AttributeTargets.All, Inherited = true, AllowMultiple = false)]
  public class IKGD_DeserializeOnVFS_BaseAttribute : Attribute, IKGD_DeserializeOnVFS_Attribute_Interface
  {
    public virtual Type DeserializerType { get; set; }
    public virtual bool AllowRecursion { get; set; }
    public virtual string BaseKey { get; set; }

    public IKGD_DeserializeOnVFS_BaseAttribute()
    { }

    public IKGD_DeserializeOnVFS_BaseAttribute(Type deserializerType, bool allowRecursion)
    {
      DeserializerType = deserializerType;
      AllowRecursion = allowRecursion;
    }
  }


  [global::System.AttributeUsage(AttributeTargets.All, Inherited = true, AllowMultiple = false)]
  public class IKGD_DeserializeOnVFS_KVTAttribute : IKGD_DeserializeOnVFS_BaseAttribute
  {
    protected bool? _SkipNullValues;
    public virtual bool SkipNullValues { get { return _SkipNullValues.GetValueOrDefault(true); } set { _SkipNullValues = value; } }

    protected bool? _SkipNullOrEmptyValues;
    public virtual bool SkipNullOrEmptyValues { get { return _SkipNullOrEmptyValues.GetValueOrDefault(true); } set { _SkipNullOrEmptyValues = value; } }

    // genera la deserializzazione del primo livello per tutte le lingue configurate nel VFS, con fallback sul field di default se non presente
    protected bool? _FullLanguageSetDump;
    public virtual bool FullLanguageSetDump { get { return _FullLanguageSetDump.GetValueOrDefault(true); } set { _FullLanguageSetDump = value; } }

    public IKGD_DeserializeOnVFS_KVTAttribute()
      : base(typeof(IKGD_DeserializeOnVFS_KVT), false)
    { }

  }



}

