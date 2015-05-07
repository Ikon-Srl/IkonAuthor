/*
 * 
 * Intranet Ikon
 * 
 * Copyright (C) 2008 Ikon Srl
 * Tutti i Diritti Riservati. All Rights Reserved
 * 
 */


using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Net;
using System.Net.Security;
using System.Security.Cryptography.X509Certificates;


namespace IKGD_Utility.Security
{

  public static class Security
  {

    public static void SSL_AcceptAllCertificates()
    {
      ServicePointManager.ServerCertificateValidationCallback = TrustAllCertificateCallback;
    }

    //
    // per le comunicazioni con SSL: ignoriamo i vari errori...
    //
    public static bool TrustAllCertificateCallback(object sender, X509Certificate certificate, X509Chain chain, SslPolicyErrors sslPolicyErrors)
    {
      if (sslPolicyErrors == SslPolicyErrors.None)
        return true;
      return true;
    }

  }



}
