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
using Ikon.SSO;


namespace Ikon.Auth
{

  /*
    //
    // esempio di configurazione dei membership provider sul server SSO
    // server e client devono condividere la stessa machineKey (verificare)
    // sul server basta solo sostituire SqlMembershipProvider con SqlMembershipProvider_SSO e Roles_IKGD con Roles_IKGD_SSO
    // configurare anche le subnets cha avranno accesso al sistema di SSO ()
    // <add key="RemoteMembershipAllowedNETs" value="95.174.21.74/32,95.174.21.162/32,95.174.21.168/32,95.174.21.170/32,95.226.159.168/30,78.134.37.176/29,10.0.0.0/8" />
    //
    // <forms name="InfoMobilita_SSO" loginUrl="~/AuthSSO/Login" timeout="600" slidingExpiration="true" protection="All" path="/" enableCrossAppRedirects="true">
    //
    // SERVER
    //
    <membership defaultProvider="Membership_AuxMapUR">
      <providers>
        <clear />
        <!--<add name="Membership_IKGD" applicationName="intranet" type="Ikon.Auth.Membership_IKGD" fallBackProvider="Membership_AuxMapUR" />-->
        <add name="Membership_AuxMapUR" applicationName="map_user_role" type="System.Web.Security.SqlMembershipProvider_SSO" connectionStringName="GDCS" requiresQuestionAndAnswer="false" requiresUniqueEmail="false" enablePasswordReset="true" maxInvalidPasswordAttempts="1000" minRequiredPasswordLength="5" minRequiredNonalphanumericCharacters="0" passwordStrengthRegularExpression="" />
        <add name="Membership_AuxMapRA" applicationName="map_role_area" type="System.Web.Security.SqlMembershipProvider_SSO" connectionStringName="GDCS" requiresQuestionAndAnswer="false" requiresUniqueEmail="false" />
        <add name="Membership_AuxMapUA" applicationName="map_user_area" type="System.Web.Security.SqlMembershipProvider_SSO" connectionStringName="GDCS" requiresQuestionAndAnswer="false" requiresUniqueEmail="false" />
      </providers>
    </membership>
    <roleManager enabled="true" defaultProvider="Roles_IKGD" cacheRolesInCookie="true" cookieSlidingExpiration="true" maxCachedResults="100">
      <providers>
        <clear />
        <add name="Roles_IKGD" type="Ikon.Auth.Roles_IKGD_SSO" applicationName="intranet" mapperUserToRole="Roles_AuxMapUR" mapperRoleToArea="Roles_AuxMapRA" mapperUserToArea="Roles_AuxMapUA" connectionStringName="GDCS" />
        <add name="Roles_AuxMapUR" applicationName="map_user_role" connectionStringName="GDCS" type="Ikon.Auth.RolesMapper" />
        <add name="Roles_AuxMapRA" applicationName="map_role_area" connectionStringName="GDCS" type="Ikon.Auth.RolesMapper" />
        <add name="Roles_AuxMapUA" applicationName="map_user_area" connectionStringName="GDCS" type="Ikon.Auth.RolesMapper" />
      </providers>
    </roleManager>
    //
    //
    //
    // CLIENT
    //
    <membership defaultProvider="Membership_AuxMapUR">
      <providers>
        <clear />
        <add name="Membership_AuxMapUR" applicationName="map_user_role" type="System.Web.Security.SqlMembershipProvider_SSO" connectionStringName="GDCS" requiresQuestionAndAnswer="false" requiresUniqueEmail="false" enablePasswordReset="true" maxInvalidPasswordAttempts="1000" minRequiredPasswordLength="5" minRequiredNonalphanumericCharacters="0" passwordStrengthRegularExpression="" />
      </providers>
    </membership>
    <roleManager enabled="true" defaultProvider="Roles_IKGD" cacheRolesInCookie="true" cookieSlidingExpiration="true" maxCachedResults="100">
      <providers>
        <clear />
        <add name="Roles_IKGD" type="Ikon.Auth.Roles_IKGD_SSO" applicationName="intranet" connectionStringName_OFF="GDCS" SSO_ServiceBaseUrl="http://sso.ikon.local/" />
      </providers>
    </roleManager>
    //
    //
  */


  //
  // Manager dei ruoli per la Intranet Ikon
  // il sistema gestisce i ruoli con un doppio mapping
  //
  public class Roles_IKGD_SSO : Roles_IKGD
  {

    public Roles_IKGD_SSO()
      : base()
    {
    }


    public override void Initialize(string name, System.Collections.Specialized.NameValueCollection config)
    {
      base.Initialize(name, config);
    }


    public override bool AreaExists(string areaName)
    {
      try
      {
        if (IsExternalSSO_Enabled)
        {
          return (bool)SSO_Manager.ProxySSO("Roles", "AreaExists", areaName);
        }
        else
        {
          return base.AreaExists(areaName);
        }
      }
      catch { }
      return false;
    }


    public override bool RoleExists(string roleName)
    {
      try
      {
        if (IsExternalSSO_Enabled)
        {
          return (bool)SSO_Manager.ProxySSO("Roles", "RoleExists", roleName);
        }
        else
        {
          return base.RoleExists(roleName);
        }
      }
      catch { }
      return false;
    }


    public override List<Ikon.Auth.RolesMapper.RoleExtended> UpdateAreaStorage(System.Data.Linq.DataContext DC)
    {
      lock (_lock)
      {
        if (IsExternalSSO_Enabled)
        {
          if (DC == null)
          {
            return SSO_Manager.ProxySSO("Roles", "UpdateAreaStorage", DC) as List<Ikon.Auth.RolesMapper.RoleExtended>;
          }
          else
          {
            throw new ArgumentException("parameter DC must be null for external providers");
          }
        }
        else
        {
          return base.UpdateAreaStorage(DC);
        }
      }
    }


    public override string[] GetAllRoles()
    {
      if (IsExternalSSO_Enabled)
      {
        return SSO_Manager.ProxySSO("Roles", "GetAllRoles") as string[];
      }
      else
      {
        return base.GetAllRoles();
      }
    }


    public override string[] GetAreasForUser(string username, bool directMappedOnly)
    {
      if (IsExternalSSO_Enabled)
      {
        return SSO_Manager.ProxySSO("Roles", "GetAreasForUser", username, directMappedOnly) as string[];
      }
      else
      {
        return base.GetAreasForUser(username, directMappedOnly);
      }
    }


    public override string[] GetRolesForUser(string username)
    {
      if (IsExternalSSO_Enabled)
      {
        return SSO_Manager.ProxySSO("Roles", "GetRolesForUser", username) as string[];
      }
      else
      {
        return base.GetRolesForUser(username);
      }
    }



    public override string[] GetUsersInArea(string areaName)
    {
      if (IsExternalSSO_Enabled)
      {
        return SSO_Manager.ProxySSO("Roles", "GetUsersInArea", areaName) as string[];
      }
      else
      {
        return base.GetUsersInArea(areaName);
      }
    }


    public override string[] GetUsersInRole(string roleName)
    {
      if (IsExternalSSO_Enabled)
      {
        return SSO_Manager.ProxySSO("Roles", "GetUsersInRole", roleName) as string[];
      }
      else
      {
        return base.GetUsersInRole(roleName);
      }
    }


    //
    // metodi di management per ruoli e gruppi
    //

    public override bool DeleteArea(string areaName, bool throwOnPopulatedArea)
    {
      if (IsExternalSSO_Enabled)
      {
        return (bool)SSO_Manager.ProxySSO("Roles", "DeleteArea", areaName, throwOnPopulatedArea);
      }
      else
      {
        return base.DeleteArea(areaName, throwOnPopulatedArea);
      }
    }


    public override bool DeleteRole(string roleName, bool throwOnPopulatedRole)
    {
      if (IsExternalSSO_Enabled)
      {
        return (bool)SSO_Manager.ProxySSO("Roles", "DeleteRole", roleName, throwOnPopulatedRole);
      }
      else
      {
        return base.DeleteRole(roleName, throwOnPopulatedRole);
      }
    }


    public override void CreateArea(string areaName)
    {
      if (IsExternalSSO_Enabled)
      {
        SSO_Manager.ProxySSO("Roles", "CreateArea", areaName);
      }
      else
      {
        base.CreateArea(areaName);
      }
    }


    public override void CreateRole(string roleName)
    {
      if (IsExternalSSO_Enabled)
      {
        SSO_Manager.ProxySSO("Roles", "CreateRole", roleName);
      }
      else
      {
        base.CreateRole(roleName);
      }
    }


    public override void AddUsersToAreas(string[] usernames, string[] areaNames)
    {
      if (IsExternalSSO_Enabled)
      {
        SSO_Manager.ProxySSO("Roles", "AddUsersToAreas", usernames, areaNames);
      }
      else
      {
        base.AddUsersToAreas(usernames, areaNames);
      }
    }


    public override void AddUsersToRoles(string[] usernames, string[] roleNames)
    {
      if (IsExternalSSO_Enabled)
      {
        SSO_Manager.ProxySSO("Roles", "AddUsersToRoles", usernames, roleNames);
      }
      else
      {
        base.AddUsersToRoles(usernames, roleNames);
      }
    }


    public override void RemoveUsersFromAreas(string[] usernames, string[] areaNames)
    {
      if (IsExternalSSO_Enabled)
      {
        SSO_Manager.ProxySSO("Roles", "RemoveUsersFromAreas", usernames, areaNames);
      }
      else
      {
        base.RemoveUsersFromAreas(usernames, areaNames);
      }
    }


    public override void RemoveUsersFromRoles(string[] usernames, string[] roleNames)
    {
      if (IsExternalSSO_Enabled)
      {
        SSO_Manager.ProxySSO("Roles", "RemoveUsersFromRoles", usernames, roleNames);
      }
      else
      {
        base.RemoveUsersFromRoles(usernames, roleNames);
      }
    }


    //
    // metodi di supporto del duplice mapping
    //

    public override void AddAreasToRole(string roleName, string[] areaNames)
    {
      if (IsExternalSSO_Enabled)
      {
        SSO_Manager.ProxySSO("Roles", "AddAreasToRole", roleName, areaNames);
      }
      else
      {
        base.AddAreasToRole(roleName, areaNames);
      }
    }


    public override void RemoveAreasFromRole(string roleName, string[] areaNames)
    {
      if (IsExternalSSO_Enabled)
      {
        SSO_Manager.ProxySSO("Roles", "RemoveAreasFromRole", roleName, areaNames);
      }
      else
      {
        base.RemoveAreasFromRole(roleName, areaNames);
      }
    }


    public override string[] GetAreasForRole(string roleName)
    {
      if (IsExternalSSO_Enabled)
      {
        return SSO_Manager.ProxySSO("Roles", "GetAreasForRole", roleName) as string[];
      }
      else
      {
        return base.GetAreasForRole(roleName);
      }
    }


    public override string[] GetRolesForArea(string areaName)
    {
      if (IsExternalSSO_Enabled)
      {
        return SSO_Manager.ProxySSO("Roles", "GetRolesForArea", areaName) as string[];
      }
      else
      {
        return base.GetRolesForArea(areaName);
      }
    }


    public override bool SetAreaPublicFlag(string areaName, bool flagPublic)
    {
      if (IsExternalSSO_Enabled)
      {
        return (bool)SSO_Manager.ProxySSO("Roles", "SetAreaPublicFlag", areaName, flagPublic);
      }
      else
      {
        return base.SetAreaPublicFlag(areaName, flagPublic);
      }
    }


  }



}
