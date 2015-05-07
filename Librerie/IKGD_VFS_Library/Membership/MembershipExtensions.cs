using System;
using System.Collections;
using System.Collections.Generic;
using System.Web.Security;
using System.Linq;
using System.Text;
using System.Web;
using System.Xml.Linq;
using System.Reflection;
//using System.Web.Mvc;
//using System.Web.Mvc.Html;
//using Microsoft.Web.Mvc;

using Ikon;


namespace Ikon.IKCMS
{


  //
  // wrapper per MembershipUser con supporto di proprieta' serializzate con KVT in Comments (che poi e' un campo tipo ntext senza limitazioni di lunghezza)
  //
  public class MembershipUserKVT : MembershipUser
  {
    private Ikon.IKCMS.KeyValueObjectTree _KVT = null;
    public MembershipUser User { get; protected set; }


    public MembershipUserKVT(MembershipUser user)
    {
      Setup(user);
    }


    public void Setup(MembershipUser user)
    {
      _KVT = null;
      User = user;
      // check per la presenza di dati serializzati con formato JSON
      if (User != null && User.Comment != null && User.Comment.StartsWith("{\""))
      {
        _KVT = Ikon.IKCMS.KeyValueObjectTree.Deserialize(User.Comment);
      }
    }


    public void UpdateKVT(bool? normalize)
    {
      if (_KVT != null && User != null)
        User.Comment = _KVT.Serialize(normalize ?? true);
    }


    public bool IsKVT { get { return (_KVT != null); } }

    public Ikon.IKCMS.KeyValueObjectTree EnsureKVT()
    {
      if (User != null)
      {
        lock (User)
        {
          if (_KVT == null)
          {
            _KVT = new Ikon.IKCMS.KeyValueObjectTree();
            FullName = User.Comment;
          }
          return _KVT;
        }
      }
      else
        return (_KVT = new Ikon.IKCMS.KeyValueObjectTree());
    }

    public Ikon.IKCMS.KeyValueObjectTree KVT { get { return _KVT; } set { _KVT = value; } }

    public Ikon.IKCMS.KeyValueObjectTree this[string key] { get { return EnsureKVT()[key]; } }


    public string FullNameNN
    {
      get { return ((_KVT != null || User == null) ? EnsureKVT()["FullName"].Value as string : User.Comment).NullIfEmpty() ?? User.UserName; }
      set
      {
        if (_KVT != null || User == null)
          EnsureKVT()["FullName"].Value = value;
        else
          User.Comment = value;
      }
    }

    public string FullName
    {
      get { return ((_KVT != null || User == null) ? EnsureKVT()["FullName"].Value as string : User.Comment).NullIfEmpty(); }
      set
      {
        if (_KVT != null || User == null)
          EnsureKVT()["FullName"].Value = value;
        else
          User.Comment = value;
      }
    }


  }




}
