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
using System.Web;
using System.Web.Mvc;

using Ikon.SSO;


namespace Ikon.IKCMS
{

  [RobotsDeny()]
  [Microsoft.Web.Mvc.ControllerSessionState(Microsoft.Web.Mvc.ControllerSessionState.ReadOnly)]
  public class QuerySSOController : Controller
  {

    public ActionResult Index()
    {
      return null;
    }


    /* consente di verificare la validità di un token*/
    public JsonResult VerifyToken(string token)
    {
      return Json(SSO_Manager.VerifyToken(token), JsonRequestBehavior.AllowGet);
    }


    /*consente l'autenticazione dell'utente e ritorna il token, analogamente alle funzionalità offerte dalla pagina di login. 
     *Può essere utilizzata per verificare le credenziali di un utente da javascript o per applicazioni non Web.*/
    public JsonResult AuthenticateUser(string UserName, string Password)
    {
      return Json(SSO_Manager.AuthenticateUser(UserName, Password), JsonRequestBehavior.AllowGet);
    }

    /* fornisce lo username dell'utente */
    public JsonResult GetUserName(string token)
    {
        return Json(SSO_Manager.GetUserName(token));
    }

    /* fornisce le informazioni principali riguardanti l'utente corrente: UserName, UserId, Email, Nome Completo, LazyLoginId*/
    public JsonResult GetUserInfo(string token, bool? getRoles, bool? getAreas, bool? getVariables)
    {
      return Json(SSO_Manager.GetUserInfo(token, getRoles, getAreas, getVariables), JsonRequestBehavior.AllowGet);
    }


    /* fornisce un'array con la lista dei ruoli associati all'utente corrente*/
    public JsonResult GetUserRoles(string token)
    {
      return Json(SSO_Manager.GetUserRoles(token), JsonRequestBehavior.AllowGet);
    }


    /*fornisce un'array con la lista delle aree associate all'utente corrente*/
    public JsonResult GetUserAreas(string token)
    {
      return Json(SSO_Manager.GetUserAreas(token), JsonRequestBehavior.AllowGet);
    }


    /* fornisce un oggetto con la lista delle user variables selezionate. */
    public JsonResult GetUserVariables(string token)
    {
      return Json(SSO_Manager.GetUserVariables(token), JsonRequestBehavior.AllowGet);
    }


    public JsonResult GetUserVariablesPublic(string token)
    {
      return Json(SSO_Manager.GetUserVariablesPublic(token), JsonRequestBehavior.AllowGet);
    }

  }


}
