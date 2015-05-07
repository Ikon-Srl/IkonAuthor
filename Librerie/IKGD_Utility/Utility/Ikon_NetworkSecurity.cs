/*
 * 
 * Intranet Ikon
 * 
 * Copyright (C) 2008 Ikon Srl
 * Tutti i Diritti Riservati. All Rights Reserved
 * 
 */


using System;
using System.Runtime.InteropServices;
using System.Security.Principal;
using System.Security.Permissions;


namespace Ikon.Auth
{
  public enum LogonType : int
  {
    LOGON32_LOGON_INTERACTIVE = 2,
    LOGON32_LOGON_NETWORK = 3,
    LOGON32_LOGON_BATCH = 4,
    LOGON32_LOGON_SERVICE = 5,
    LOGON32_LOGON_UNLOCK = 7,
    LOGON32_LOGON_NETWORK_CLEARTEXT = 8,	// Only for Win2K or higher
    LOGON32_LOGON_NEW_CREDENTIALS = 9		// Only for Win2K or higher
  };


  public enum LogonProvider : int
  {
    LOGON32_PROVIDER_DEFAULT = 0,
    LOGON32_PROVIDER_WINNT35 = 1,
    LOGON32_PROVIDER_WINNT40 = 2,
    LOGON32_PROVIDER_WINNT50 = 3
  };


  class SecuUtil32
  {
    [DllImport("advapi32.dll", SetLastError = true)]
    public static extern bool LogonUser(String lpszUsername, String lpszDomain, String lpszPassword, int dwLogonType, int dwLogonProvider, ref IntPtr TokenHandle);

    [DllImport("kernel32.dll", CharSet = CharSet.Auto)]
    public extern static bool CloseHandle(IntPtr handle);

    [DllImport("advapi32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    public extern static bool DuplicateToken(IntPtr ExistingTokenHandle, int SECURITY_IMPERSONATION_LEVEL, ref IntPtr DuplicateTokenHandle);
  }


  /// <summary>
  /// Summary description for NetworkSecurity.
  /// </summary>
  public class NetworkSecurity
  {
    public static object _lock = new object();


    public NetworkSecurity()
    {
    }

    //
    // eg. ImpersonateUser("10.0.1.1", "marco", "xyz", LogonType.LOGON32_LOGON_NEW_CREDENTIALS, LogonProvider.LOGON32_PROVIDER_DEFAULT);
    //
    public static WindowsImpersonationContext ImpersonateUser(string strDomain, string strLogin, string strPwd, LogonType logonType, LogonProvider logonProvider)
    {
      IntPtr tokenHandle = new IntPtr(0);
      IntPtr dupeTokenHandle = new IntPtr(0);
      try
      {
        const int SecurityImpersonation = 2;
        //
        tokenHandle = IntPtr.Zero;
        dupeTokenHandle = IntPtr.Zero;
        //
        // Call LogonUser to obtain a handle to an access token.
        bool returnValue = SecuUtil32.LogonUser(strLogin, strDomain, strPwd, (int)logonType, (int)logonProvider, ref tokenHandle);
        if (returnValue == false)
        {
          int ret = Marshal.GetLastWin32Error();
          string strErr = String.Format("LogonUser failed with error code : {0}", ret);
          throw new ApplicationException(strErr, null);
        }
        //
        bool retVal = SecuUtil32.DuplicateToken(tokenHandle, SecurityImpersonation, ref dupeTokenHandle);
        if (retVal == false)
        {
          SecuUtil32.CloseHandle(tokenHandle);
          throw new ApplicationException("Failed to duplicate token", null);
        }
        //
        // The token that is passed to the following constructor must 
        // be a primary token in order to use it for impersonation.
        //WindowsIdentity newId = new WindowsIdentity(dupeTokenHandle);
        using (WindowsIdentity newId = new WindowsIdentity(dupeTokenHandle))
        {
          WindowsImpersonationContext impersonatedUser = newId.Impersonate();
          return impersonatedUser;
        }
        //
      }
      catch (Exception ex)
      {
        throw new ApplicationException(ex.Message, ex);
      }
      finally
      {
        try { SecuUtil32.CloseHandle(dupeTokenHandle); }
        catch { }
        try { SecuUtil32.CloseHandle(tokenHandle); }
        catch { }
      }
    }


    public static WindowsImpersonationContext ImpersonateUserCached(string strDomain, string strLogin, string strPwd, LogonType logonType, LogonProvider logonProvider)
    {
      WindowsImpersonationContext impersonatedUser = null;
      WindowsIdentity newIdentity = null;
      string identityKey = string.Format("ImpersonateUserCached_{0}_{1}_{2}_{3}", strDomain, strLogin, (int)logonType, (int)logonProvider);
      newIdentity = System.Web.HttpContext.Current.Application[identityKey] as WindowsIdentity;
      if (newIdentity != null)
      {
        impersonatedUser = newIdentity.Impersonate();
        return impersonatedUser;
      }
      else
      {
        //System.Threading.Monitor.Enter(_lock);
        //try
        //{
        //}
        //catch { }
        //finally
        //{
        //  System.Threading.Monitor.Exit(_lock);
        //}
        lock (_lock)
        {
          try
          {
            newIdentity = System.Web.HttpContext.Current.Application[identityKey] as WindowsIdentity;
            if (newIdentity == null)
            {
              IntPtr tokenHandle = new IntPtr(0);
              IntPtr dupeTokenHandle = new IntPtr(0);
              try
              {
                const int SecurityImpersonation = 2;
                //
                tokenHandle = IntPtr.Zero;
                dupeTokenHandle = IntPtr.Zero;
                //
                // Call LogonUser to obtain a handle to an access token.
                bool returnValue = SecuUtil32.LogonUser(strLogin, strDomain, strPwd, (int)logonType, (int)logonProvider, ref tokenHandle);
                if (returnValue == false)
                {
                  int ret = Marshal.GetLastWin32Error();
                  string strErr = String.Format("LogonUser failed with error code : {0}", ret);
                  throw new ApplicationException(strErr, null);
                }
                //
                bool retVal = SecuUtil32.DuplicateToken(tokenHandle, SecurityImpersonation, ref dupeTokenHandle);
                if (retVal == false)
                {
                  SecuUtil32.CloseHandle(tokenHandle);
                  throw new ApplicationException("Failed to duplicate token", null);
                }
                //
                // The token that is passed to the following constructor must 
                // be a primary token in order to use it for impersonation.
                System.Web.HttpContext.Current.Application[identityKey] = newIdentity = new WindowsIdentity(dupeTokenHandle);
                //
              }
              catch (Exception ex)
              {
                throw new ApplicationException(ex.Message, ex);
              }
              finally
              {
                //try { SecuUtil32.CloseHandle(dupeTokenHandle); }
                //catch { }
                //try { SecuUtil32.CloseHandle(tokenHandle); }
                //catch { }
              }
            }
            //
            if (newIdentity != null)
            {
              impersonatedUser = newIdentity.Impersonate();
              return impersonatedUser;
            }
            //
          }
          catch { }
        }
      }
      //
      return impersonatedUser;
    }


  }
}
