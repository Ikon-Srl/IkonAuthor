/*
 * 
 * IkonPortal
 * 
 * Copyright (C) 2012 Ikon Srl
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


namespace Ikon.IKCMS.Library
{

  public static class IkonAuthor_Constants
  {
    //
    public const int VersionMajor = 3;
    public const int VersionMinor = 0;
    public const int VersionLevel = 0;
    public static string Version { get { return string.Format("{0}.{1}.{2}", VersionMajor,VersionMinor,VersionLevel); } }
    //
  }


}

