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
using System.Linq.Expressions;
using System.Web;
using System.Xml.Linq;
using System.Data.Linq;
using System.IO;
using System.Text;
using System.Reflection;
using System.Diagnostics;
using System.Linq.Dynamic;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;  // assembly System.ServiceModel.Web


using Ikon;
using Ikon.GD;
using Ikon.Log;


namespace Ikon.IKGD.Library
{


  [DataContract]
  public class FS_TagData
  {
    [DataMember]
    public int vNode { get; set; }
    [DataMember]
    public int sNode { get; set; }
    [DataMember]
    public int rNode { get; set; }
    [DataMember]
    public int Folder { get; set; }
    //
    [DataMember]
    public bool IsFolder { get; set; }
    [DataMember]
    public bool IsNew { get; set; }
    [DataMember]
    public bool IsLocked { get; set; }
    [DataMember]
    public bool IsDeleted { get; set; }
    //
    [DataMember]
    public int ChildCount { get; set; }
    [DataMember]
    public FS_ACL_Reduced.AclType ACL { get; set; }


    public FS_TagData(FS_FileInfo fsNode)
    {
      if (fsNode == null)
        return;
      vNode = fsNode.Code_VNODE;
      sNode = fsNode.Code_SNODE;
      rNode = fsNode.Code_RNODE;
      Folder = fsNode.Code_FOLDER;
      IsFolder = fsNode.IsFolder;
      IsLocked = fsNode.rNode.locked.HasValue;
      IsDeleted = fsNode.IsDeleted;
      ChildCount = fsNode.ChildCount;
      ACL = fsNode.ACL.Flags;
    }


    public string Serialize()
    {
      // marcando la classe con [DataContract] e i soli campi da serializzare con [DataMember] posso generare un Json decente
      // assembly System.ServiceModel.Web // System.Runtime.Serialization.Json
      DataContractJsonSerializer serializer = new DataContractJsonSerializer(typeof(FS_TagData));
      using (MemoryStream ms = new MemoryStream())
      {
        serializer.WriteObject(ms, this);
        return Encoding.UTF8.GetString(ms.ToArray());
      }
    }

    public static FS_TagData UnSerialize(string json)
    {
      // assembly System.ServiceModel.Web // System.Runtime.Serialization.Json
      DataContractJsonSerializer serializer = new DataContractJsonSerializer(typeof(FS_TagData));
      using (MemoryStream ms = new MemoryStream(Encoding.UTF8.GetBytes(json)))
      {
        return serializer.ReadObject(ms) as FS_TagData;
      }
    }

  }

}
