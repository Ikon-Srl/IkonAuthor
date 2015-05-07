/*
 * 
 * Intranet Ikon
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
using System.Linq;
using System.Linq.Expressions;
using System.Web;
using System.Web.Security;
using System.Web.Configuration;
using System.Configuration;
using System.Configuration.Provider;
using System.Web.Caching;
using LinqKit;

using Ikon.GD;


namespace Ikon.Auth
{

  //
  // Manager dei ruoli per la Intranet Ikon
  // il sistema gestisce i ruoli con un doppio mapping
  //
  public class Roles_IKGD : RoleProvider
  {
    protected NameValueCollection savedConfig;
    //
    protected string mapperUserToRole { get; set; }
    protected string mapperRoleToArea { get; set; }
    protected string mapperUserToArea { get; set; }
    //
    public string connectionStringName { get; protected set; }
    public string SSO_ServiceBaseUrl { get; protected set; }
    public string SSO_ServiceSoapUrl
    {
      get
      {
        return (SSO_ServiceBaseUrl != null) ? SSO_ServiceBaseUrl + "SSO_Service.asmx" : null;
      }
    }
    //
    public static bool IsExternalSSO_Enabled { get { return Provider != null && Provider.connectionStringName.IsNullOrEmpty(); } }
    //


    //
    protected object _lock = new object();
    protected List<Ikon.Auth.RolesMapper.RoleExtended> _AreasAllStorage { get; set; }  // viene sempre tornato un duplicato per evitare problemi di locking
    public List<Ikon.Auth.RolesMapper.RoleExtended> AreasAll { get { lock (_lock) { return (_AreasAllStorage ?? UpdateAreaStorage(null)).ToList(); } } }
    public List<Ikon.Auth.RolesMapper.RoleExtended> AreasAllNN { get { lock (_lock) { return (_AreasAllStorage ?? UpdateAreaStorage(null)).Where(a => !string.IsNullOrEmpty(a.Name)).ToList(); } } }
    public List<Ikon.Auth.RolesMapper.RoleExtended> AreasPublic { get { lock (_lock) { return (_AreasAllStorage ?? UpdateAreaStorage(null)).Where(a => a.IsPublic).ToList(); } } }
    public List<Ikon.Auth.RolesMapper.RoleExtended> AreasNotPublic { get { lock (_lock) { return (_AreasAllStorage ?? UpdateAreaStorage(null)).Where(a => a.IsPublic).ToList(); } } }
    public List<Ikon.Auth.RolesMapper.RoleExtended> AreasHardCoded { get { lock (_lock) { return (_AreasAllStorage ?? UpdateAreaStorage(null)).Where(a => a.IsHardCoded).ToList(); } } }
    public List<Ikon.Auth.RolesMapper.RoleExtended> AreasProtected { get { lock (_lock) { return (_AreasAllStorage ?? UpdateAreaStorage(null)).Where(a => a.IsHardCoded == false && a.IsPublic == false).ToList(); } } }
    //

    public Roles_IKGD()
      : base()
    {
    }


    public override void Initialize(string name, System.Collections.Specialized.NameValueCollection config)
    {
      if (config == null)
        throw new ArgumentNullException("config");
      //
      base.Initialize(name, config);
      savedConfig = new NameValueCollection(config);
      //
      ApplicationName = GetConfigValue("ApplicationName", System.Web.Hosting.HostingEnvironment.ApplicationVirtualPath);
      mapperUserToRole = GetConfigValue("mapperUserToRole", "map_user_role");
      mapperRoleToArea = GetConfigValue("mapperRoleToArea", "map_role_area");
      mapperUserToArea = GetConfigValue("mapperUserToArea", "map_user_area");
      connectionStringName = GetConfigValue("connectionStringName", null);
      SSO_ServiceBaseUrl = GetConfigValue("SSO_ServiceBaseUrl", null);
      if (SSO_ServiceBaseUrl.IsNotNullOrWhiteSpace())
        SSO_ServiceBaseUrl = SSO_ServiceBaseUrl.TrimEnd('/', ' ', '\\') + "/";
      //
    }



    //
    // A helper function to retrieve config values from the configuration file.
    //
    protected string GetConfigValue(string itemName, string defaultValue)
    {
      string val = null;
      try { val = savedConfig[itemName] ?? savedConfig[savedConfig.AllKeys.FirstOrDefault(k => string.Equals(k, itemName, StringComparison.OrdinalIgnoreCase))]; }
      catch { }
      return val ?? defaultValue;
    }


    //
    protected MembershipProvider membershipProviderUserToRole { get { return Membership.Providers[mapperUserToRole] ?? Membership.Providers.OfType<MembershipProvider>().FirstOrDefault(r => r.ApplicationName == "map_user_role"); } }
    protected MembershipProvider membershipProviderRoleToArea { get { return Membership.Providers[mapperRoleToArea] ?? Membership.Providers.OfType<MembershipProvider>().FirstOrDefault(r => r.ApplicationName == "map_role_area"); ; } }
    protected MembershipProvider membershipProviderUserToArea { get { return Membership.Providers[mapperUserToArea] ?? Membership.Providers.OfType<MembershipProvider>().FirstOrDefault(r => r.ApplicationName == "map_user_area"); ; } }
    //
    public RoleProvider roleProviderUserToRole { get { return Roles.Providers[mapperUserToRole]; } }    //UseExternalSSO
    public RoleProvider roleProviderRoleToArea { get { return Roles.Providers[mapperRoleToArea]; } }
    public RoleProvider roleProviderUserToArea { get { return Roles.Providers[mapperUserToArea]; } }    //UseExternalSSO
    //

    public override string ApplicationName { get; set; }

    //
    // seleziona il primo provider di tipo Roles_IKGD
    //
    //public static Roles_IKGD Provider { get { return Roles.Provider as Roles_IKGD; } }
    protected static Roles_IKGD _Provider_Cached = null;
    public static Roles_IKGD Provider
    {
      get
      {
        if (_Provider_Cached == null)
          _Provider_Cached = Roles.Providers.OfType<Roles_IKGD>().FirstOrDefault();
        return _Provider_Cached;
      }
    }
    //

    //
    // fornisce una cache dependancy per invalidare le cache delle aree dopo ogni modifica al DB [aspnet_UsersInRoles]
    //
    public static AggregateCacheDependency GetCacheDependency
    {
      get
      {
        AggregateCacheDependency sqlDeps = new AggregateCacheDependency();
        if (!IsExternalSSO_Enabled)
        {
          sqlDeps.Add(new SqlCacheDependency("GDCS", "aspnet_UsersInRoles"));
        }
        return sqlDeps;
      }
    }

    public static AggregateCacheDependency GetCacheDependencyAreas
    {
      get
      {
        AggregateCacheDependency sqlDeps = new AggregateCacheDependency();
        if (!IsExternalSSO_Enabled)
        {
          sqlDeps.Add(new SqlCacheDependency("GDCS", "aspnet_Roles"));
        }
        return sqlDeps;
      }
    }

    //
    // metodi non implemementati nella libreria attuale perche' non compatibili col doppio mapping dei ruoli
    //
    public override string[] FindUsersInRole(string roleName, string usernameToMatch)
    {
      throw new NotImplementedException();
    }


    //
    // lista delle aree comunque associate a ciascun utente
    //
    public virtual List<string> AreasBaseSet { get { try { return Utility.Explode(IKGD_Config.AppSettings["AreasBaseSet"] ?? string.Empty, ",", " ", true); } catch { return new List<string>(); } } }


    //
    // implementazione dei restanti metodi della classe abstract
    //


    public virtual bool AreaExists(string areaName)
    {
      try { return roleProviderRoleToArea.RoleExists(areaName) || roleProviderUserToArea.RoleExists(areaName); }
      catch { }
      return false;
    }


    public override bool RoleExists(string roleName)
    {
      try { return roleProviderUserToRole.RoleExists(roleName); }
      catch { }
      return false;
    }


    public virtual List<Ikon.Auth.RolesMapper.RoleExtended> UpdateAreaStorage(System.Data.Linq.DataContext DC)
    {
      lock (_lock)
      {
        List<Ikon.Auth.RolesMapper.RoleExtended> areas = new List<RolesMapper.RoleExtended>();
        Ikon.Auth.ExtraDB.DataContext DB = null;
        try
        {
          //
          DB = (DC != null) ? Ikon.Auth.ExtraDB.DataContext.Factory(DC.Connection) : Ikon.Auth.ExtraDB.DataContext.Factory();
          DB.ObjectTrackingEnabled = false;
          //
          try { areas.AddRange((roleProviderRoleToArea as RolesMapper).GetAllRolesExtended(DB)); }
          catch { }
          try { areas.AddRange((roleProviderUserToArea as RolesMapper).GetAllRolesExtended(DB)); }
          catch { }
          try { areas.AddRange(AreasBaseSet.Select(r => new Ikon.Auth.RolesMapper.RoleExtended() { Name = r, IsPublic = true, IsHardCoded = true })); }
          catch { }
          if (!areas.Any(a => a.Name == string.Empty && a.IsPublic))
            areas.Add(new Ikon.Auth.RolesMapper.RoleExtended() { Name = string.Empty, IsPublic = true, IsHardCoded = true });
          areas = areas.GroupBy(a => a.Name).OrderBy(g => g.Key).Select(g => g.FirstOrDefault(r => r.IsPublic) ?? g.FirstOrDefault()).ToList();
        }
        catch { }
        finally
        {
          if (DB != null)
          {
            DB.Dispose();
          }
        }
        _AreasAllStorage = areas;
        //
        // invalidatore per la cache delle aree con supporto per evitare di spararsi da solo sui piedi
        try
        {
          string cacheKey = "INVALIDATOR_AreasAllStorage";
          HttpRuntime.Cache.Insert(cacheKey, _AreasAllStorage, Ikon.Auth.Roles_IKGD.GetCacheDependencyAreas, DateTime.Now.AddSeconds(Utility.TryParse<int>(IKGD_Config.AppSettings["CachingACLs"], 3600)), Cache.NoSlidingExpiration, CacheItemPriority.High, (key, value, reason) =>
          {
            // verifica della cache key, nel caso di una rimozione manuale (e quindi non di scadenza o dipendenze non riazzeriamo la variabile)
            if (key == cacheKey && reason != CacheItemRemovedReason.Removed)
              _AreasAllStorage = null;
          });
        }
        catch { }
        //
        return areas;
      }
    }


    public string[] GetAllAreasNotPublic()
    {
      return AreasAll.Where(a => !a.IsPublic).Select(a => a.Name).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(r => r.ToLowerInvariant()).ToArray();
    }


    public string[] GetAllAreas()
    {
      //
      //List<string> lista = new List<string>();
      //try { lista.AddRange(roleProviderRoleToArea.GetAllRoles()); }
      //catch { }
      //try { lista.AddRange(roleProviderUserToArea.GetAllRoles()); }
      //catch { }
      //try { lista.AddRange(AreasBaseSet); }
      //catch { }
      //return lista.Distinct().OrderBy(r => r.ToLowerInvariant()).ToArray();
      //
      return AreasAll.Select(a => a.Name).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(r => r.ToLowerInvariant()).ToArray();
    }


    public string[] GetAllAssignableAreas()
    {
      return AreasAll.Where(a => !a.IsHardCoded).Select(a => a.Name).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(r => r.ToLowerInvariant()).ToArray();
    }


    public override string[] GetAllRoles()
    {
      List<string> lista = new List<string>();
      try { lista.AddRange(roleProviderUserToRole.GetAllRoles()); }
      catch { }
      return lista.Distinct().ToArray();
    }


    public bool IsUserInArea(string username, string areaName)
    {
      try { return GetAreasForUser(username).Contains(areaName); }
      catch { }
      return false;
    }
    //
    public override bool IsUserInRole(string username, string roleName)
    {
      try { return GetRolesForUser(username).Contains(roleName); }
      catch { }
      return false;
    }


    public virtual string[] GetAreasForUser(string username) { return GetAreasForUser(username, false); }
    public virtual string[] GetAreasForUser(string username, bool directMappedOnly)
    {
      if (directMappedOnly)
      {
        return roleProviderUserToArea.GetRolesForUser(username);
      }
      if (username == "root")
        return GetAllAreas();
      List<string> aree = AreasPublic.Select(a => a.Name).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
      try { aree.AddRange(roleProviderUserToArea.GetRolesForUser(username).Except(aree, StringComparer.OrdinalIgnoreCase)); }
      catch { }
      try
      {
        foreach (string role in roleProviderUserToRole.GetRolesForUser(username))
        {
          try { aree.AddRange(roleProviderRoleToArea.GetRolesForUser(role).Except(aree, StringComparer.OrdinalIgnoreCase)); }
          catch { }
        }
      }
      catch { }
      return aree.ToArray();
    }


    public override string[] GetRolesForUser(string username)
    {
      if (username == "root")
        return GetAllRoles();
      List<string> lista = new List<string>();
      try { lista.AddRange(roleProviderUserToRole.GetRolesForUser(username)); }
      catch { }
      return lista.Distinct().ToArray();
    }



    public virtual string[] GetUsersInArea(string areaName)
    {
      List<string> lista = new List<string>();
      if (AreasBaseSet.Contains(areaName))
        return Membership.GetAllUsers().OfType<MembershipUser>().Select(u => u.UserName).Distinct().ToArray();
      try { lista.AddRange(roleProviderUserToArea.GetUsersInRole(areaName)); }
      catch { }
      try
      {
        var roles = roleProviderRoleToArea.GetUsersInRole(areaName);
        foreach (string role in roles)
        {
          try { lista.AddRange(roleProviderUserToRole.GetUsersInRole(role)); }
          catch { }
        }
      }
      catch { }
      return lista.Distinct().ToArray();
    }


    public override string[] GetUsersInRole(string roleName)
    {
      List<string> lista = new List<string>();
      try { lista.AddRange(roleProviderUserToRole.GetUsersInRole(roleName)); }
      catch { }
      return lista.Distinct().ToArray();
    }


    //
    // metodi di management per ruoli e gruppi
    //

    public virtual bool DeleteArea(string areaName, bool throwOnPopulatedArea)
    {
      int usersCount = 0;
      try { usersCount = GetUsersInRole(areaName).Length; }
      catch { }
      if (throwOnPopulatedArea && usersCount > 0)
        throw new ProviderException("Area is not empty");
      bool res1 = false;
      bool res2 = false;
      try { res1 = roleProviderRoleToArea.DeleteRole(areaName, false); }
      catch { }
      try { res2 = roleProviderUserToArea.DeleteRole(areaName, false); }
      catch { }
      UpdateAreaStorage(null);
      return res1 && res2;
    }


    public override bool DeleteRole(string roleName, bool throwOnPopulatedRole)
    {
      int usersCount = 0;
      try { usersCount = GetUsersInRole(roleName).Length; }
      catch { }
      if (throwOnPopulatedRole && usersCount > 0)
        throw new ProviderException("Role is not empty");
      try { return roleProviderUserToRole.DeleteRole(roleName, false); }
      catch { }
      return false;
    }


    public virtual void CreateArea(string areaName)
    {
      try { roleProviderUserToArea.CreateRole(areaName); }
      catch { }
      try { roleProviderRoleToArea.CreateRole(areaName); }
      catch { }
      UpdateAreaStorage(null);
    }


    public override void CreateRole(string roleName)
    {
      try { roleProviderUserToRole.CreateRole(roleName); }
      catch { }
    }


    public virtual void AddUsersToAreas(string[] usernames, string[] areaNames)
    {
      try { roleProviderUserToArea.AddUsersToRoles(usernames, areaNames); }
      catch { }
    }


    public override void AddUsersToRoles(string[] usernames, string[] roleNames)
    {
      try { roleProviderUserToRole.AddUsersToRoles(usernames, roleNames); }
      catch { }
    }


    public virtual void RemoveUsersFromAreas(string[] usernames, string[] areaNames)
    {
      try { roleProviderUserToArea.RemoveUsersFromRoles(usernames, areaNames); }
      catch { }
    }


    public override void RemoveUsersFromRoles(string[] usernames, string[] roleNames)
    {
      try { roleProviderUserToRole.RemoveUsersFromRoles(usernames, roleNames); }
      catch { }
    }


    //
    // metodi di supporto del duplice mapping
    //

    public virtual void AddAreasToRole(string roleName, string[] areaNames)
    {
      try { roleProviderRoleToArea.AddUsersToRoles(new string[] { roleName }, areaNames); }
      catch { }
    }


    public virtual void RemoveAreasFromRole(string roleName, string[] areaNames)
    {
      try { roleProviderRoleToArea.RemoveUsersFromRoles(new string[] { roleName }, areaNames); }
      catch { }
    }


    public virtual string[] GetAreasForRole(string roleName)
    {
      List<string> lista = new List<string>();
      try { lista.AddRange(roleProviderRoleToArea.GetRolesForUser(roleName)); }
      catch { }
      try { lista.AddRange(AreasBaseSet); }
      catch { }
      return lista.Distinct().ToArray();
    }


    public virtual string[] GetRolesForArea(string areaName)
    {
      List<string> lista = new List<string>();
      if (AreasBaseSet.Contains(areaName))
        return roleProviderUserToRole.GetAllRoles();
      try { lista.AddRange(roleProviderRoleToArea.GetUsersInRole(areaName)); }
      catch { }
      return lista.Distinct().ToArray();
    }


    public virtual bool SetAreaPublicFlag(string areaName, bool flagPublic)
    {
      bool res = false;
      try { res |= (roleProviderUserToRole as RolesMapper).SetIsPublicFlag(null, areaName, flagPublic); }
      catch { }
      try { res |= (roleProviderUserToArea as RolesMapper).SetIsPublicFlag(null, areaName, flagPublic); }
      catch { }
      return res;
    }


  }



}
