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

  public static class IKGD_Constants
  {
    public const string IKCAT_TagPropertyName = "IKCAT_Tag";
    public const string IKGD_StandardRelationName = "link";  //attualmente non viene più utilizzato
    public const string IKGD_LinkRelationName = "link";
    public const string IKGD_ArchiveRelationName = "archive";
    public const string IKGD_AttachmentRelationName = "attachment";
    //
    public const string IKGD_StreamsProcessorFieldName = "__StreamInfos__";
    public const string IKGD_StreamsProcessorExternalFieldName = "__StreamInfosExternal__";
    //
    public const int VersionMajor = 3;
    public const int VersionMinor = 0;
    public const int VersionLevel = 0;
    public static string Version { get { return string.Format("{0}.{1}.{2}", VersionMajor,VersionMinor,VersionLevel); } }
    //
  }


}

