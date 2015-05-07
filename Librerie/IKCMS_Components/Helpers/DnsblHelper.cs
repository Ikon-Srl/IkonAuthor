using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Text.RegularExpressions;
using System.Web;
using System.Xml.Linq;
using System.Web.Mvc;
using System.Web.Mvc.Html;
using System.Reflection;
using System.Runtime.Serialization;
using LinqKit;
using Autofac;

using Ikon;
using Ikon.GD;
using Ikon.IKGD.Library;


namespace Ikon.IKCMS
{

  public static class DnsblHelper
  {

    static DnsblHelper()
    {
    }


    public static bool IsRequestFromBotNet() { return IsRequestFromBotNet(null, null, null); }
    public static bool IsRequestFromBotNet(int? intIP, string stringIP, IEnumerable<string> resolvers)
    {
      bool result = false;
      try
      {
        string query = null;
        if (query.IsNullOrEmpty() && stringIP.IsNotEmpty())
        {
          try { query = string.Join(".", stringIP.Split('.').Reverse().ToArray()); }
          catch { }
        }
        if (query.IsNullOrEmpty() && intIP.GetValueOrDefault(0) != 0)
        {
          int myIP = intIP.GetValueOrDefault(0);
          query = string.Format("{3}.{2}.{1}.{0}", myIP & 0xff, (myIP >> 8) & 0xff, (myIP >> 16) & 0xff, (myIP >> 24) & 0xff);
        }
        if (query.IsNullOrEmpty())
        {
          try { query = string.Join(".", Utility.GetRequestAddressExt(null).Split('.').Reverse().ToArray()); }
          catch { }
        }
        if (resolvers == null || !resolvers.Any())
        {
          resolvers = Utility.Explode(IKGD_Config.AppSettings["DNSBL_BotResolverAddressess"] ?? ".tor.dan.me.uk", ", ", ", ", true);
        }
        foreach (string resolverAddr in resolvers)
        {
          string name = query + resolverAddr;
          Heijden.DNS.Resolver resolver = new Heijden.DNS.Resolver();
          resolver.UseCache = true; // importante
          resolver.TimeOut = Utility.TryParse<int>(IKGD_Config.AppSettings["DNSBL_BotResolverTimeout"], 1); // 1s
          resolver.Retries = 1;
          resolver.TransportType = Heijden.DNS.TransportType.Udp;
          Heijden.DNS.Response resp = resolver.Query(name, Heijden.DNS.QType.A, Heijden.DNS.QClass.IN);
          if (resp != null && resp.RecordsA.Any())
          {
            try
            {
              var addrBytes = resp.RecordsA.FirstOrDefault().Address.GetAddressBytes();
              var firstByte = addrBytes.FirstOrDefault();
              if (firstByte == 127)
              {
                try { System.Web.HttpContext.Current.Response.AppendToLog("dnsbl-" + resolverAddr); }
                catch { }
                return true;
              }
            }
            catch { }
          }
        }
      }
      catch { }
      return result;
    }


  }
}
