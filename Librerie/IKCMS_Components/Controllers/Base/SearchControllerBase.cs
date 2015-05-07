using System;
using System.Collections;
using System.Collections.Specialized;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Linq;
using System.Web;
using System.Web.Security;
using System.Web.Configuration;
using System.Web.UI;
using System.Web.UI.HtmlControls;
using System.Web.UI.WebControls;
using System.Web.UI.WebControls.WebParts;
using System.Web.Caching;
using System.Xml.Linq;
using System.Linq.Expressions;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Reflection;
using System.Web.Mvc;
using System.Web.Mvc.Ajax;
//using Microsoft.Web.Mvc;
using LinqKit;

using Ikon;
using Ikon.Support;
using Ikon.GD;
using Ikon.IKCMS;
using Ikon.IKGD.Library.Resources;
using Ikon.IKCMS.Library.Resources;
using Ikon.IKGD.Library.Collectors;


namespace Ikon.IKCMS
{



  //[CompressFilter()]
  [CacheFilter(86400)]
  [Microsoft.Web.Mvc.ControllerSessionState(Microsoft.Web.Mvc.ControllerSessionState.ReadOnly)]
  public abstract class SearchControllerBase : AutoStaticCMS_Controller
  {

    //
    // handler per la gestione del search engine da estendere nelle customizzazioni di ciascun sito
    //
    [RobotsDeny()]
    public virtual ActionResult SearchCMS(string searchCMS)
    {
      try
      {
        //
        // viene settato in OnActionExecuted se non inizializzato prima
        //ViewData.Model = ViewData.Model ?? GetDefaultModel("/Search");
        //
        int matchesCount = 0;
        List<Ikon.Indexer.IKGD_LuceneDocCollection> items = null;
        if (Utility.TryParse<bool>(IKGD_Config.AppSettingsWeb["CachingIKCMS_LuceneEnabled"], true))
        {
          string cacheKey = "LuceneCache_" + FS_OperationsHelpers.ContextHashNN(searchCMS);
          var results = FS_OperationsHelpers.CachedEntityWrapper(cacheKey, () =>
          {
            using (Ikon.Indexer.LuceneIndexer indexer = new Ikon.Indexer.LuceneIndexer(false))
            {
              return new { items = indexer.IKGD_SearchLuceneCMS(searchCMS, null).ToList(), matchesCount = indexer.LastMatchesCount };
            }
          }, Utility.TryParse<int>(IKGD_Config.AppSettings["CachingIKCMS_Lucene"], 120), FS_OperationsHelpers.Const_CacheDependencyIKGD_vNode_vData_iNode);
          items = results.items;
          matchesCount = results.matchesCount;
        }
        else
        {
          using (Ikon.Indexer.LuceneIndexer indexer = new Ikon.Indexer.LuceneIndexer(false))
          {
            items = indexer.IKGD_SearchLuceneCMS(searchCMS, null).ToList();
            matchesCount = indexer.LastMatchesCount;
          }
        }
        //
        ViewData["query"] = searchCMS;
        ViewData["Items"] = items;
        ViewData["matchesCount"] = matchesCount;
        if (items != null)
        {
          ViewData["ItemsCount"] = (items != null) ? items.Count : 0;
        }
        //
      }
      catch { }
      //
      ViewData["ViewTemplate"] = ViewData["ViewTemplate"] ?? "~/Views/Search/Search";
      //
      return View(ViewData["ViewTemplate"] as string);
    }


    //
    // disponibile alla url:
    // ~/SitemapCMS
    //
    [AcceptVerbs(HttpVerbs.Get)]
    public ActionResult SiteMapXml(string language, string siteMode)
    {
      SiteMapHelper.SiteMapXml(Response, language, siteMode ?? IKGD_SiteMode.SiteMode);
      return null;
    }


  }
}
