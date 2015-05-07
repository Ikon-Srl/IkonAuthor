using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using System.Net.Mail;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Transactions;
using System.Web;
using System.Web.Mvc;
using System.Web.Mvc.Ajax;
using System.Web.Security;
using System.Reflection;
using Newtonsoft.Json;
using Autofac;

using Ikon;
using Ikon.Auth.Login;
using Ikon.GD;
using Ikon.IKCMS;
using Ikon.IKGD.Library.Resources;
using Ikon.IKCMS.Library.Resources;



namespace Ikon.IKCMS
{


  //[RobotsDeny()]
  public abstract class LanguageControllerBase : VFS_Access_Controller
  {

    public virtual ActionResult Index()
    {
      return Content(IKGD_Language_Provider.Provider.LanguageNN);
    }


    public virtual ActionResult Get()
    {
      return Content(IKGD_Language_Provider.Provider.LanguageNN);
    }


    public virtual ActionResult Set(string id)
    {
      TrySetLanguage(id);
      return Content(IKGD_Language_Provider.Provider.LanguageNN);
    }


    public virtual bool TrySetLanguage(string language)
    {
      try
      {
        if (string.IsNullOrEmpty(language))
          return false;
        if (IKGD_Language_Provider.Provider.Language == language)
          return false;
        IKGD_Language_Provider.Provider.Language = language;
        if (IKGD_Language_Provider.Provider.Language == language)
          return true;
      }
      catch { }
      return false;
    }


    public virtual ActionResult SetLanguage(string language, int? rNode, int? sNode) { return SetLanguageExt(language, rNode, sNode, null); }
    public virtual ActionResult SetLanguageExt(string language, int? rNode, int? sNode, string ReturnUrl)
    {
      string urlNew = ReturnUrl.NullIfEmpty();
      string urlFallBack = "~/";
      List<FS_Operations.FS_TreeNode<TreeNodeInfoVFS>> nodes = null;
      try
      {
        //
        try { nodes = HelperMenuCommon.GetNodesFromUrl(Request.UrlReferrer); }
        catch { }
        //
        if (TrySetLanguage(language))
        {
          //
          // salvataggio della nuova lingua nei settings del LazyLogin e/o custom
          //
          urlNew = SetLanguageCustomWorker(rNode, sNode);
        }
        else
          throw new Exception("Lingua non modificata.");
        //
      }
      catch
      {
        // in caso di eccezioni oppure lingua del sito non modificata si ritorna alla pagina originale
        // dalla quale e' stato cliccata la action, altrimenti rimandiamo alla home
        if (urlNew != null)
          return Redirect(urlNew);
        if (Request.UrlReferrer == null)
          return Redirect(urlFallBack);
        return Redirect(Request.UrlReferrer.ToString());
      }
      urlNew = urlNew ?? ReturnUrl.NullIfEmpty();
      //
      // in caso di effettiva modifica della lingua per ora rimandiamo alla home
      // in futuro dovremmo cercare la pagina equivalente
      // scan del referrer dal menu, se trovato rimando alla stessa url
      // analisi della url con le regex delle url standard cms e in caso ricera del nuovo sNode
      // fallback --> Home
      //

      try
      {
        if (urlNew == null)
        {
          List<IKGD_Path> paths = null;
          nodes = nodes ?? HelperMenuCommon.GetNodesFromUrl(Request.UrlReferrer);
          if (nodes != null && nodes.Any())
            paths = fsOp.PathsFromNodesExt(null, nodes.Select(n => n.Data.rNode), false, true, false).ToList();
          if ((paths == null || !paths.Any()) && rNode != null)
            paths = fsOp.PathsFromNodesExt(null, new int[] { rNode.Value }, false, true, false).ToList();
          if ((paths == null || !paths.Any()) && sNode != null)
            paths = fsOp.PathsFromNodesExt(new int[] { sNode.Value }, null, false, true, false).ToList();
          if (paths != null && paths.Any())
          {
            var pathsFiltered = paths.FilterCustom(IKGD_Path_Helper.FilterByLanguage, IKGD_Path_Helper.FilterByActive, IKGD_Path_Helper.FilterByAreas, IKGD_Path_Helper.FilterByRootCMS).ToList();
            if (pathsFiltered.Any())
            {
              // nel caso di path ancora degeneri provo a filtrarli con un map su tutti gli rnode dei fragments
              if (pathsFiltered.Count > 1)
              {
                var pathSrc = paths.FirstOrDefault(p => p.sNode == sNode);
                if (pathSrc != null)
                {
                  var pathFiltered2 = pathsFiltered.Where(p => p.Fragments.All(f => pathSrc.Fragments.Any(fs => fs.rNode == f.rNode))).ToList();
                  if (pathFiltered2.Any())
                    pathsFiltered = pathFiltered2;
                }
              }
              //TODO: servirebbe un formatter standard per le url da utilizzare nel menu', breadcrumbs, ecc.
              var path = pathsFiltered.FirstOrDefault();
              urlNew = IKCMS_ModelCMS.GetUrlCanonical(path.rNode, path.sNode, null, path.LastFragment.Name, language, true, null);
              //
              //urlNew = IKCMS_ModelCMS.GetUrlCanonical(path.rNode, path.sNode, null, path.LastFragment.Name);
              //urlNew = IKCMS_RouteUrlManager.GetMvcUrlGeneral(paths2.FirstOrDefault().sNode);
              //urlNew = IKGD_SEO_Manager.MapOutcomingUrl(paths2.FirstOrDefault().sNode);
              //try { urlNew = IKGD_SEO_Manager.MapOutcomingUrl(urlNew, true); }
              //catch { }
            }
          }
        }
      }
      catch { }
      //
      if (!string.IsNullOrEmpty(urlNew))
      {
        return Redirect(urlNew);
      }
      //
      return Redirect("~/");
      //
    }


    //
    // postprocessing customizzabile per il cambio lingua
    // ritorna (opzionale) la url alla quale effettuare un redirect al termine dei processing
    //
    public virtual string SetLanguageCustomWorker(int? rNode, int? sNode)
    {
      return null;
    }



    public static string GetLanguageLabel(string language)
    {
      switch (language)
      {
        case "en":
          return "English";
        case "fr":
          return "Français";
        case "es":
          return "Español";
        case "de":
          return "Deutsch";
        case "sl":
        case "si":
          return "Slovensko";
        case "hr":
          return "Hrvatski";
        case "jp":
          return "日本語";
        case "zh":
          return "简体字";
        case "it":
        default:
          return "Italiano";
      }
    }


    public static string GetCountryLabel(string language)
    {
      switch (language)
      {
        case "en":
          return "United Kingdom";
        case "fr":
          return "France";
        case "es":
          return "España";
        case "de":
          return "Deutschland";
        case "sl":
        case "si":
          return "Slovenija";
        case "hr":
          return "Hrvatska";
        case "jp":
          return "日本語";
        case "zh":
          return "中国";
        case "it":
        default:
          return "Italia";
      }
    }


  }
}
