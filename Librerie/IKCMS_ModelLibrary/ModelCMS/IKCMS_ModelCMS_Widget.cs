/*
 * 
 * Intranet Ikon
 * 
 * Copyright (C) 2010 Ikon Srl
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
using System.Web.UI;
using System.Web.Security;
using System.Xml.Linq;
using System.Data.Linq;
using System.IO;
using System.Text;
using System.Linq.Expressions;
using System.Reflection;
using System.Data.Linq.Mapping;
using System.Diagnostics;
using System.Linq.Dynamic;
using System.Transactions;
using System.Web.Caching;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;  // assembly System.ServiceModel.Web

using LinqKit;
using Autofac;
using Autofac.Core;
using Autofac.Builder;
using Autofac.Features;

using Ikon;
using Ikon.IKCMS;


namespace Ikon.IKCMS
{
  using Ikon.Config;
  using Ikon.GD;
  using Ikon.IKGD.Library.Resources;
  using Ikon.IKCMS.Library.Resources;



  [IKCMS_ModelCMS_ResourceTypes(typeof(IKCMS_Widget_Interface), typeof(IKCMS_HasSerializationCMS_Interface))]
  [IKCMS_ModelCMS_RecursionMode(ModelRecursionModeEnum.RecursionNone)]
  [IKCMS_ModelCMS_fsNodeMode(vfsNodeFetchModeEnum.vNode_vData_iNode)]
  [IKCMS_ModelCMS_Priority(-2999999)]
  public class IKCMS_ModelCMS_WidgetCMS<T> : IKCMS_ModelCMS<T>, IKCMS_ModelCMS_Widget_Interface
    where T : class, IKCMS_HasSerializationCMS_Interface
  {
  }


  [IKCMS_ModelCMS_ResourceTypes(typeof(IKCMS_Widget_Interface), typeof(IKCMS_HasSerializationCMS_Interface), typeof(IKCMS_HasPropertiesKVT_Interface))]
  [IKCMS_ModelCMS_BootStrapperOpenGenerics(typeof(IKCMS_HasPropertiesKVT_Interface))]
  [IKCMS_ModelCMS_RecursionMode(ModelRecursionModeEnum.RecursionNone)]
  [IKCMS_ModelCMS_fsNodeMode(vfsNodeFetchModeEnum.vNode_vData_iNode)]
  [IKCMS_ModelCMS_Priority(-2999998)]
  public class IKCMS_ModelCMS_WidgetCMS_KVT<T> : IKCMS_ModelCMS_WidgetCMS<T>, IKCMS_ModelCMS_VFS_KVT_Interface
    where T : class, IKCMS_HasPropertiesKVT_Interface
  {
    public KeyValueObjectTree VFS_ResourceKVT { get { return ResourceSettingsKVT_Wrapper; } }
  }


  [IKCMS_ModelCMS_ResourceTypes(typeof(IKCMS_Widget_Interface), typeof(IKCMS_HasSerializationCMS_Interface), typeof(IKCMS_HasPropertiesKVT_Interface), typeof(IKCMS_HasPropertiesLanguageKVT_Interface))]
  [IKCMS_ModelCMS_BootStrapperOpenGenerics(typeof(IKCMS_HasPropertiesLanguageKVT_Interface))]
  [IKCMS_ModelCMS_RecursionMode(ModelRecursionModeEnum.RecursionNone)]
  [IKCMS_ModelCMS_fsNodeMode(vfsNodeFetchModeEnum.vNode_vData_iNode)]
  [IKCMS_ModelCMS_Priority(-2999997)]
  public class IKCMS_ModelCMS_WidgetCMS_LanguageKVT<T> : IKCMS_ModelCMS_WidgetCMS_KVT<T>, IKCMS_ModelCMS_VFS_LanguageKVT_Interface
    where T : class, IKCMS_HasPropertiesLanguageKVT_Interface
  {
    public virtual KeyValueObjectTree VFS_ResourceLanguageKVT(params string[] keys) { return ResourceSettingsKVT_Wrapper.KeyFilterTry(Language ?? IKGD_Language_Provider.Provider.Language, keys); }
    public virtual KeyValueObjectTree VFS_ResourceNoLanguageKVT(params string[] keys) { return ResourceSettingsKVT_Wrapper.KeyFilterTry(null, keys); }
    public virtual IEnumerable<KeyValueObjectTree> VFS_ResourceLanguageKVTs(params string[] keys) { return ResourceSettingsKVT_Wrapper.KeyFilterTryMulti(Language ?? IKGD_Language_Provider.Provider.Language, keys); }
    public virtual IEnumerable<KeyValueObjectTree> VFS_ResourceNoLanguageKVTs(params string[] keys) { return ResourceSettingsKVT_Wrapper.KeyFilterTryMulti(null, keys); }
    public virtual List<string> VFS_ResourceLanguageKVTss(params string[] keys) { return ResourceSettingsKVT_Wrapper.KeyFilterTryMulti(Language ?? IKGD_Language_Provider.Provider.Language, keys).Select(r => r.ValueString).Where(s => s != null).ToList(); }
    public virtual List<string> VFS_ResourceNoLanguageKVTss(params string[] keys) { return ResourceSettingsKVT_Wrapper.KeyFilterTryMulti(null, keys).Select(r => r.ValueString).Where(s => s != null).ToList(); }
  }




  //
  // customizzazioni per widget specifici
  //


  [IKCMS_ModelCMS_ResourceTypes(typeof(IKCMS_ResourceType_Text))]
  [IKCMS_ModelCMS_RecursionMode(ModelRecursionModeEnum.RecursionNone)]
  [IKCMS_ModelCMS_fsNodeMode(vfsNodeFetchModeEnum.vNode_vData_iNode)]
  [IKCMS_ModelCMS_Priority(-2899997)]
  public class IKCMS_ModelCMS_WidgetCMS_Text : IKCMS_ModelCMS_WidgetCMS<IKCMS_ResourceType_Text>
  {
  }



}
