/*
 * 
 * Intranet Ikon
 * 
 * Copyright (C) 2012 Ikon Srl
 * Tutti i Diritti Riservati. All Rights Reserved
 * 
 */


using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Web;
using System.Web.Configuration;
using System.Web.Security;
using System.Web.Services;
using System.Configuration;
using System.Reflection;
using System.Runtime.Serialization;

using Ikon;
using Ikon.GD;


namespace Ikon.SSO
{


  //
  // questo metodo non e' piu' utilizzato da Ikon Author che utilizza normali chiamate http con un controller di appoggio
  // lo manteniamo solo per riferimento come webservice installato in libreria
  //
  [WebService(Namespace = "http://sso.ikon.it/")]
  [WebServiceBinding(ConformsTo = WsiProfiles.BasicProfile1_1)]
  [System.ComponentModel.ToolboxItem(false)]
  // To allow this Web Service to be called from script, using ASP.NET AJAX, uncomment the following line. 
  // [System.Web.Script.Services.ScriptService]
  public class SSO_Service : System.Web.Services.WebService
  {

    [WebMethod]
    public bool VerifyToken(string token)
    {
      return SSO_Manager.VerifyToken(token);
    }


    [WebMethod]
    public string AuthenticateUser(string UserName, string Password)
    {
      return SSO_Manager.AuthenticateUser(UserName, Password);
    }


    [WebMethod]
    public string GetUserName(string token)
    {
      return SSO_Manager.GetUserName(token);
    }


    [WebMethod]
    public SSO_UserInfo GetUserInfo(string token)
    {
      return SSO_Manager.GetUserInfo(token, null, null, null);
    }


    [WebMethod]
    public SSO_UserInfo GetUserInfoExt(string token, bool? getRoles, bool? getAreas, bool? getVariables)
    {
      return SSO_Manager.GetUserInfo(token, getRoles, getAreas, getVariables);
    }


    [WebMethod]
    public List<string> GetUserRoles(string token)
    {
      return SSO_Manager.GetUserRoles(token);
    }


    [WebMethod]
    public List<string> GetUserAreas(string token)
    {
      return SSO_Manager.GetUserAreas(token);
    }


    [WebMethod]
    public List<SSO_UserVariable> GetUserVariables(string token)
    {
      return SSO_Manager.GetUserVariables(token);
    }


    [WebMethod]
    public List<SSO_UserVariable> GetUserVariablesPublic(string token)
    {
      return SSO_Manager.GetUserVariablesPublic(token);
    }


    //
    // methods to support remote membership functionality
    //

    private static Ikon.Auth.Roles_IKGD _ProviderRoles = null;
    private static MembershipProvider _ProviderMembership = null;
    private static List<MethodInfo> _ProviderRolesMethods = null;
    private static List<MethodInfo> _ProviderMembershipMethods = null;


    protected void EnsureProvidersDataInitialized()
    {
      //
      if (_ProviderRoles == null)
      {
        try
        {
          _ProviderRoles = Ikon.Auth.Roles_IKGD.Provider;
          _ProviderRolesMethods = _ProviderRoles.GetType().GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.InvokeMethod).ToList();
        }
        catch { }
      }
      //
      if (_ProviderMembership == null)
      {
        try
        {
          _ProviderMembership = Membership.Provider;
          _ProviderMembershipMethods = _ProviderMembership.GetType().GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.InvokeMethod).ToList();
        }
        catch { }
      }
      //
    }

  }
}



namespace IKGD_Auth_Modules.SSO_ServiceClient
{
  //
  // questo metodo non e' piu' utilizzato da Ikon Author che utilizza normali chiamate http con un controller di appoggio
  // lo manteniamo solo per riferimento come webservice installato in libreria
  //
  public partial class SSO_ServiceSoapClient
  {
    public static IKGD_Auth_Modules.SSO_ServiceClient.SSO_ServiceSoapClient Factory()
    {
      IKGD_Auth_Modules.SSO_ServiceClient.SSO_ServiceSoapClient service = null;
      try
      {
        //
        // cerchiamo nella configurazione del sito se e' presente un endpoint configurato "SSO_ServiceSoap" per il service richiesto
        // nel caso questo mancasse si provvede a generare un endpoint+binding di default
        // nel caso la configurazione dell'endpoint fosse assente bisognerebbe ottenere la url del webservice dal membership provider customizzato
        //
        System.ServiceModel.Configuration.ChannelEndpointElement SSO_ServiceSoap_endpoint = null;
        try { SSO_ServiceSoap_endpoint = (System.Web.Configuration.WebConfigurationManager.GetSection("system.serviceModel/client") as System.ServiceModel.Configuration.ClientSection).Endpoints.OfType<System.ServiceModel.Configuration.ChannelEndpointElement>().FirstOrDefault(r => r.Name == "SSO_ServiceSoap"); }
        catch { }
        //
        string SSO_ServiceSoap_endpoint_defaultUrl = null;
        try { SSO_ServiceSoap_endpoint_defaultUrl = Ikon.Auth.Roles_IKGD.Provider.SSO_ServiceSoapUrl; }
        catch { }
        if (SSO_ServiceSoap_endpoint_defaultUrl.IsNullOrWhiteSpace())
        {
          SSO_ServiceSoap_endpoint_defaultUrl = IKGD_Config.AppSettings["SSO_ServiceSoapUrl"] ?? "http://localhost/SSO/SSO_Service.asmx";
        }
        //
        if (SSO_ServiceSoap_endpoint != null)
        {
          service = new IKGD_Auth_Modules.SSO_ServiceClient.SSO_ServiceSoapClient(SSO_ServiceSoap_endpoint.Name);
        }
        else
        {
          var binding = new System.ServiceModel.BasicHttpBinding();
          var endpointAddress = new System.ServiceModel.EndpointAddress(SSO_ServiceSoap_endpoint_defaultUrl);
          service = new IKGD_Auth_Modules.SSO_ServiceClient.SSO_ServiceSoapClient(binding, endpointAddress);
        }
      }
      catch { }
      return service;
    }
  }
}
