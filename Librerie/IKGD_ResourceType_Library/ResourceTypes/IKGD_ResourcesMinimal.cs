/*
 * 
 * Intranet Ikon
 * 
 * Copyright (C) 2008 Ikon Srl
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

using Ikon;
using Ikon.Log;
using Ikon.Support;
using Ikon.GD;


namespace Ikon.IKGD.Library.Resources
{

  //
  // Folder
  //
  [Description("Cartella")]
  public class IKGD_Folder_Folder : IKGD_ResourceTypeBase, IKGD_Folder_Interface, IKCMS_Folder_Interface
  {
    public override bool HasInode { get { return false; } }
    public override bool IsUnstructured { get { return false; } }
    public override bool IsFolder { get { return true; } }

    public override string IconEditor { get { return "VFS.Folder.gif"; } }
  }


  //
  // tipo di documento non selezionabile da usare come VOID preset nell'editor
  //
  [Description("Tipo di risorsa non selezionato")]
  public class IKGD_ResourceTypeNone : IKGD_ResourceTypeBase, IKCMS_Base_Interface
  {
    public override bool IsSelectable { get { return false; } }
    public override string IconEditor { get { return "VFS.File.gif"; } }

    // se non ho ancora selezionato la risorsa posso sceglierne una qualunque
    public override bool IsCompatibleWith(IKGD_ResourceType_Interface testObj)
    {
      return true;
    }
  }


  [Description("Allegato")]
  public class IKGD_ResourceTypeDocument : IKGD_ResourceTypeBase, IKCMS_ResourceUnStructured_Interface, IKCMS_IsIndexable_Interface, IKCMS_ResourceWithUrl_Interface
  {
    public override bool IsUnstructured { get { return true; } }
    public override string IconEditor { get { return "VFS.File.gif"; } }
  }


  [Description("| Allegato per moduli tipo news")]
  public class IKGD_ResourceTypeAttachment : IKGD_ResourceTypeDocument
  {
  }





}