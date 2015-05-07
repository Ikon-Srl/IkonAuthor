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
using System.Linq;
using System.Xml.Linq;
using System.Collections;
using System.Collections.Generic;
using System.Linq.Dynamic;
using System.Linq.Expressions;
using System.Data.Linq;
using System.IO;
using System.Text;
using System.Reflection;
using System.Diagnostics;
using System.ComponentModel;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;  // assembly System.ServiceModel.Web

using Ikon;
using Ikon.Log;


namespace Ikon.Serialization
{

  public static class Ikon_Serialization
  {
    static Ikon_Serialization()
    {
    }



    public static string SerializeDC<T>(T obj) where T : class
    {
      // marcando la classe T con [DataContract] e i soli campi da serializzare con [DataMember] posso generare un Json decente
      // assembly System.ServiceModel.Web // System.Runtime.Serialization.Json
      DataContractJsonSerializer serializer = new DataContractJsonSerializer(typeof(T));
      using (MemoryStream ms = new MemoryStream())
      {
        serializer.WriteObject(ms, obj);
        return Encoding.UTF8.GetString(ms.ToArray());
      }
    }


    public static T UnSerializeDC<T>(string jsonString) where T : class { return UnSerializeDCanon(typeof(T), jsonString) as T; }
    public static object UnSerializeDCanon(Type ty, string jsonString)
    {
      // assembly System.ServiceModel.Web // System.Runtime.Serialization.Json
      DataContractJsonSerializer serializer = new DataContractJsonSerializer(ty);
      using (MemoryStream ms = new MemoryStream(Encoding.UTF8.GetBytes(jsonString)))
      {
        return serializer.ReadObject(ms);
      }
    }


    public static string SerializelDCtoXml<T>(T obj) where T : class
    {
      DataContractSerializer serializer = new DataContractSerializer(typeof(T));
      using (MemoryStream ms = new MemoryStream())
      {
        serializer.WriteObject(ms, obj);
        return Encoding.UTF8.GetString(ms.ToArray());
      }
    }


    public static T UnSerializeDCfromXml<T>(string jsonString) where T : class { return UnSerializeDCanonFromXml(typeof(T), jsonString) as T; }
    public static object UnSerializeDCanonFromXml(Type ty, string jsonString)
    {
      DataContractSerializer serializer = new DataContractSerializer(ty);
      using (MemoryStream ms = new MemoryStream(Encoding.UTF8.GetBytes(jsonString)))
      {
        return serializer.ReadObject(ms);
      }
    }


  }




  [DataContract]
  public class Serializable_BaseDataContract
  {
    //[DataMember]
    //public string field { get; set; }

    public ExtensionDataObject ExtensionData { get; set; }



    // per UnSerialize utilizzare il membro statico Ikon_Serialization.UnSerializeDC
    public string Serialize() { return Ikon_Serialization.SerializeDC(this); }
  }






}
