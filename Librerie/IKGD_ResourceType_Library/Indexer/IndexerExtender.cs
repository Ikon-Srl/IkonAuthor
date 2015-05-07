using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Configuration;
using System.Web;

using Ikon.GD;


namespace Ikon.Indexer
{
  using IKGD_ResourceType_Library.IKGD_Indexer;


  //
  // viene utilizzato solamente dai widget della intranet
  // che utilizzano il webservice, non dovrebbe essere problematico
  // effettuare la migrazione
  //
  public class IndexerWrapper : IndexerSoapClient, IDisposable
  {
    public IndexerWrapper() : base()
    {
      try
      {
        string configAppKey = "IndexerUrl";
        if (string.IsNullOrEmpty(IKGD_Config.AppSettings[configAppKey]))
          return;
        string url = IKGD_Config.AppSettings[configAppKey];
        if (url.StartsWith("~/"))
          url = VirtualPathUtility.ToAbsolute(url);
        if (url.StartsWith("/"))
          url = new Uri(HttpContext.Current.Request.Url, url).ToString();
        this.Endpoint.Address = new System.ServiceModel.EndpointAddress(url);
      }
      catch { }
    }


    ~IndexerWrapper()
    {
      Dispose();
    }


    // non ho bisogno dell'implementazione sofisticata, tanto il Close puo' venire chiamato piu' volte
    public void Dispose()
    {
      //this.Close();
    }
    
  }

}
