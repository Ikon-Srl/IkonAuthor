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
using System.Linq;
using System.Linq.Expressions;
using System.Web;
using System.Web.Security;
using System.Web.Configuration;
using System.Security;
using System.Security.Principal;
using System.Text;
using System.Text.RegularExpressions;
using System.Net;
using System.Data.Common;
using LinqKit;


namespace Ikon.Auth
{

  //
  // classe base per il membershipMapper che utilizza un'estensione del provider SQL
  // e viene utilizzata per aggiungere la gestione di ruoli e aree ai provider esterni che ne sono privi
  //
  public class RolesMapper : SqlRoleProvider
  {
    protected string IKGD_ConnectionStringName { get; private set; }


    private SqlMembershipProvider _relatedMembershipProvider = null;
    public SqlMembershipProvider relatedMembershipProvider
    {
      get
      {
        if (_relatedMembershipProvider == null)
        {
          _relatedMembershipProvider = Membership.Providers.OfType<SqlMembershipProvider>().FirstOrDefault(p => p.ApplicationName == this.ApplicationName);
        }
        return _relatedMembershipProvider;
      }
    }


    public override void Initialize(string name, NameValueCollection config)
    {
      string specifiedConnectionString = config["connectionStringName"];
      if (string.IsNullOrEmpty(specifiedConnectionString))
        throw new ArgumentException("connectionStringName Not Specified for [Ikon.Auth.RolesMapper] Provider");
      IKGD_ConnectionStringName = specifiedConnectionString;
      //if (WebConfigurationManager.ConnectionStrings[specifiedConnectionString] == null)
      //  throw new ArgumentException("ConnectionString Name Not Found for [Ikon.Auth.RolesMapper] Provider");
      //IKGD_ConnectionString = WebConfigurationManager.ConnectionStrings[specifiedConnectionString].ConnectionString;
      //
      base.Initialize(name, config);
    }


    public override void AddUsersToRoles(string[] usernames, string[] roleNames)
    {
      if (relatedMembershipProvider != null)
      {
        foreach (string un in usernames)
        {
          try
          {
            if (relatedMembershipProvider.GetUser(un, false) == null)
            {
              MembershipCreateStatus status = MembershipCreateStatus.Success;
              MembershipUser user = relatedMembershipProvider.CreateUser(un, Guid.NewGuid().ToString(), string.Empty, null, null, true, Guid.NewGuid(), out status);
            }
          }
          catch { }
        }
      }
      try { base.AddUsersToRoles(usernames, roleNames); }
      catch { }
    }


    public override void RemoveUsersFromRoles(string[] usernames, string[] roleNames)
    {
      base.RemoveUsersFromRoles(usernames, roleNames);
    }



    public bool SetIsPublicFlag(Guid? roleId, string roleName, bool flag)
    {
      bool IsTainted = false;
      try
      {
        using (Ikon.Auth.ExtraDB.DataContext DB = Ikon.Auth.ExtraDB.DataContext.Factory())
        {
          List<Ikon.Auth.ExtraDB.aspnet_Role> roleRecords = null;
          if (roleId != null)
          {
            roleRecords = DB.aspnet_Roles.Where(r => r.RoleId == roleId.Value && r.aspnet_Application.LoweredApplicationName == ApplicationName.ToLower()).ToList();
          }
          else if (roleName != null)
          {
            roleRecords = DB.aspnet_Roles.Where(r => roleName.ToLower() == r.LoweredRoleName && r.aspnet_Application.LoweredApplicationName == ApplicationName.ToLower()).ToList();
          }
          if (roleRecords != null && roleRecords.Any())
          {
            foreach (var roleRecord in roleRecords)
            {
              string currentDescription = roleRecord.Description ?? string.Empty;
              if (flag != RegEx_IsPublic.IsMatch(currentDescription))
              {
                var frags = Utility.Explode(currentDescription, " ").Except(new string[] { "IsPublic" }, StringComparer.OrdinalIgnoreCase).Distinct().ToList();
                if (flag)
                  frags.Add("IsPublic");
                roleRecord.Description = Utility.Implode(frags, " ", null, true, true).NullIfEmpty();
              }
            }
            var chg = DB.GetChangeSet();
            IsTainted |= chg.Updates.Any();
            DB.SubmitChanges();
          }
          if (IsTainted)
          {
            Roles_IKGD.Provider.UpdateAreaStorage(DB);
          }
        }
        return true;
      }
      catch { }
      return false;
    }


    protected Regex RegEx_IsPublic = new Regex(@"IsPublic", RegexOptions.IgnoreCase | RegexOptions.Singleline);
    public virtual List<RoleExtended> GetAllRolesExtended(Ikon.Auth.ExtraDB.DataContext DC)
    {
      List<RoleExtended> rolesData = new List<RoleExtended>();
      Ikon.Auth.ExtraDB.DataContext DB = null;
      try
      {
        DB = DC ?? Ikon.Auth.ExtraDB.DataContext.Factory();
        rolesData = DB.aspnet_Roles.Where(r => r.aspnet_Application.LoweredApplicationName == ApplicationName.ToLower()).OrderBy(r => r.RoleName).Select(r => new RoleExtended() { Name = r.RoleName, RoleId = r.RoleId, IsPublic = RegEx_IsPublic.IsMatch(r.Description ?? string.Empty) }).Distinct((r1, r2) => r1.Name == r2.Name).ToList();
      }
      catch { }
      finally
      {
        if (DC == null && DB != null)
          DB.Dispose();
      }
      return rolesData;
    }


    public class RoleExtended
    {
      public string Name { get; set; }
      public Guid RoleId { get; set; }
      public bool IsPublic { get; set; }
      public bool IsHardCoded { get; set; }

      public override string ToString()
      {
        return string.Format("{0}{1}{2}", Name, IsPublic ? " [Public]" : null, IsHardCoded ? " [HardCoded]" : null);
      }
    }

  }

}
