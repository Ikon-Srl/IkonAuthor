using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Security;
using System.Web.SessionState;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.IO;

using Ikon;


namespace Ikon.Support
{


  [global::System.AttributeUsage(AttributeTargets.Assembly, Inherited = false, AllowMultiple = false)]
  public sealed class IKGD_Assembly_BrowsableAttribute : Attribute
  {
    public IKGD_Assembly_BrowsableAttribute() { }
  }


  [global::System.AttributeUsage(AttributeTargets.Assembly, Inherited = false, AllowMultiple = false)]
  public sealed class IKGD_Assembly_EmbeddableAttribute : Attribute
  {
    public string AssemblyGroup { get; private set; }

    public IKGD_Assembly_EmbeddableAttribute(string assemblyGroup)
    {
      this.AssemblyGroup = assemblyGroup;
    }
  }


  [global::System.AttributeUsage(AttributeTargets.Assembly, Inherited = false, AllowMultiple = true)]
  public sealed class IKGD_Assembly_EmbeddableDependancyAttribute : Attribute
  {
    public string AssemblyGroup { get; private set; }
    public Regex AssemblySelector { get; private set; }

    public IKGD_Assembly_EmbeddableDependancyAttribute(string assemblyGroup, string assemblySelector)
    {
      this.AssemblyGroup = assemblyGroup;
      this.AssemblySelector = new Regex(assemblySelector);
    }
  }

}

