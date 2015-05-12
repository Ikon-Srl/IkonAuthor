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
using Newtonsoft.Json;
using Autofac;
using LinqKit;

using Ikon;
using Ikon.Auth.Login;
using Ikon.IKCMS;
using Ikon.GD;
using Custom.Controllers;
using SampleSiteWeb;


namespace IkonWeb.Controllers
{

  public class AuthController : AuthControllerBase
  {

      [AcceptVerbs(HttpVerbs.Post)]
      [ValidateInput(false)]
      public override ActionResult RegisterAjax(string userName, FormCollection collection)
      {
          //
          bool createUserAsApproved = true;
          string successUrl = null;
          List<string> messages = new List<string>();
          //
          try
          {
              MembershipUser userOrig = null;
              try { userOrig = Membership.GetUser(userName); }
              catch { }
              //
              bool HasACL = Ikon.GD.MembershipHelper.HasMembershipACL();
              bool IsSameUser = !string.IsNullOrEmpty(userName) && (userName == MembershipHelper.UserName) && !MembershipHelper.IsAnonymous;
              //
              successUrl = collection["successUrl"];
              //
              // prima di tutto verifica del captcha
              //
              if (Utility.TryParse<bool>(collection["captchaEnabled"], false) && collection["captcha"] != (string)HttpContext.Session["CAPTCHA"])
              {
                  messages.Add("Verifica che sei un umano e trascina l'icona nel cerchio secondo le istruzioni.");
              }
              //
              if (!Regex.IsMatch(collection["userEmail"].TrimSafe(), @"^[_a-zA-Z0-9-]+(\.[_a-zA-Z0-9-]+)*@[a-zA-Z0-9-]+(\.[a-zA-Z0-9-]+)*\.(([0-9]{1,3})|([a-zA-Z]{2,3})|(aero|coop|info|museum|name))$", RegexOptions.IgnoreCase))
                  messages.Add("L'E-mail inserita non è un indirizzo di E-mail valido.");
              if (string.IsNullOrEmpty(userName) && !string.IsNullOrEmpty(collection["userEmail"]))
              {
                  if (Membership.GetUser(collection["userEmail"].TrimSafe()) != null)
                      messages.Add("Lo username corrispondente all'indirizzo E-mail inserito è già utilizzato nel sistema.");
                  if (string.IsNullOrEmpty(collection["PasswordNew"]))
                      messages.Add("Per la creazione di un nuovo utente è necessario specificare una password.");
              }
              //
              // check delle password
              if (!string.IsNullOrEmpty(collection["PasswordCurrent"]) && !string.IsNullOrEmpty(collection["PasswordNew"]) && !string.IsNullOrEmpty(userName))
              {
                  if (!Membership.ValidateUser(userName, collection["PasswordCurrent"]))
                      messages.Add("Le password corrente inserita non é valida.");
              }
              if (collection["PasswordNew"] != collection["PasswordVerify"])
                  messages.Add("Le password inserite non coincidono.");
              else if (!string.IsNullOrEmpty(collection["PasswordNew"]) && collection["PasswordNew"].Length < 6)
                  messages.Add("La password deve essere di almeno 6 caratteri.");
              //
              if (collection["Input_Nome"].TrimSafe().Length == 0)
                  messages.Add("E' necessario compilare il campo Nome.");
              if (collection["Input_Cognome"].TrimSafe().Length == 0)
                  messages.Add("E' necessario compilare il campo Cognome.");

              //if (collection["Input_Indirizzo"].TrimSafe().Length == 0)
              //    messages.Add("E' necessario compilare il campo Indirizzo.");
              //if (collection["Input_Telefono"].TrimSafe().Length == 0 && collection["Input_Fax"].TrimSafe().Length == 0)
              //    messages.Add("E' necessario compilare almeno uno dei campi Telefono o Fax.");
              //if (collection["Input_Ente"].TrimSafe().Length == 0)
              //    messages.Add("E' necessario compilare il campo Ente.");
              //if (collection["Input_Posizione"].TrimSafe().Length == 0)
              //    messages.Add("E' necessario compilare il campo Posizione.");

              bool flag_privacy = Utility.TryParse<bool>(collection["privacy"]);
              if (!flag_privacy)
                  messages.Add("E' necessario fornire il consenso al trattamento dei dati personali per procedere con la registrazione.");
              //
              if (messages.Count > 0)
                  return Json(new { hasError = true, message = "<ul>" + string.Join("\n", messages.Select(m => string.Format("<li>{0}</li>", HttpUtility.HtmlEncode(m))).ToArray()) + "</ul>" });
              //
              // processing delle modifiche all'account
              //

              //
              // acquisizione dell'account o creazione di uno nuovo
              //
              bool sendWelcomMessage = false;
              string userNameToLoad = userName.DefaultIfEmpty(collection["userEmail"]).TrimSafe();
              MembershipUser user = Membership.GetUser(userNameToLoad);
              if (user == null && !string.IsNullOrEmpty(userName))
                  throw new Exception("L'utente selezionato non è definito nel sistema.");
              if (user == null)
              {
                  MembershipCreateStatus status;
                  user = Membership.CreateUser(userNameToLoad, collection["PasswordNew"], collection["userEmail"].TrimSafe(), null, null, createUserAsApproved, out status);
                  if (user == null || status != MembershipCreateStatus.Success)
                      throw new Exception("Errore nella creazione dell'account: " + status.ToString());
                  sendWelcomMessage = true;
              }
              else
              {
                  if (!string.IsNullOrEmpty(collection["PasswordNew"]))
                  {
                      string passNew = (HasACL && string.IsNullOrEmpty(collection["PasswordCurrent"])) ? user.ResetPassword() : collection["PasswordCurrent"];
                      user.ChangePassword(passNew, collection["PasswordNew"]);
                  }
                  if (user.Email != collection["userEmail"])
                      user.Email = collection["userEmail"].TrimSafe();
              }
              MembershipUserKVT userKVT = new MembershipUserKVT(user);
              user.IsApproved = true;
              //
              userKVT.FullName = Utility.Implode(new string[] { collection["Input_Nome"], collection["Input_Cognome"] }, " ", null, true, true);
              userKVT["Privacy"].Value = flag_privacy;
              //
              collection.AllKeys.Where(k => k.StartsWith("Input_", StringComparison.OrdinalIgnoreCase)).ForEach(k => userKVT[k.Substring("Input_".Length)].Value = collection[k].TrimSafe());
              userKVT.UpdateKVT(null);
              Membership.UpdateUser(user);
              //

              //
              // forzatura del login con migrazione dei dati anonimi gia' acquisiti
              // e salvataggio dei dati anagrafici nelle tabelle del LazyLogin
              //
              // devo forzare un login perche' cambia lo username nella cookie permanente!
              Ikon.GD.MembershipHelper.MembershipSessionReset();
              //Ikon.GD.MembershipHelper.MembershipLogin(user, null, true);
              bool logged = LoginManager.LoginUser(user, true, null, true);
              //
              ILazyLoginMapper llMapper = HelperExtensionCustom.DC.GetLazyLoginMapper((Guid)user.ProviderUserKey, true);
              var llAnagrafica = HelperExtensionCustom.DC.GetLazyLoginMapperChild<Custom.DB.LazyLogin_AnagraficaMain>(llMapper, true);
              //
              llAnagrafica.Nome = collection["Input_Nome"].TrimSafe();
              llAnagrafica.Cognome = collection["Input_Cognome"].TrimSafe();
              //llAnagrafica.EMail = user.Email;
              //llAnagrafica.Telefono = collection["Input_Telefono"].TrimSafe();
              llAnagrafica.Comune = collection["Input_Citta"].TrimSafe();
              llAnagrafica.Provincia = collection["Input_Provincia"].TrimSafe();
              llAnagrafica.Indirizzo = collection["Input_Indirizzo"].TrimSafe();
              llAnagrafica.flag_Privacy = Utility.TryParse<bool>(collection["privacy"], false);
              //
              var chg = HelperExtensionCustom.DC.GetChangeSet();
              HelperExtensionCustom.DC.SubmitChanges();
              //
              if (sendWelcomMessage)
              {
                  MailSendWelcomeNewUser(user, null);
              }
              //
              string msg = "Operazione completata con successo.";
              return Json(new { hasError = false, successUrl = successUrl, message = msg });
              //
          }
          catch (Exception ex)
          {
              return Json(new { hasError = true, message = ex.Message });
          }
      }



      //
      // per la gestione dei records di anagrafica degli utenti autentificati via facebook
      //
      public override bool AutoRegisterFB_PostProcessor(string FB_token, IDictionary<string, object> dataFB, MembershipUser user)
      {
          bool result = base.AutoRegisterFB_PostProcessor(FB_token, dataFB, user);
          if (result)
          {
              try
              {
                  string email = user.Email;
                  string fullname = user.Comment.NullIfEmpty() ?? (dataFB.ContainsKey("name") ? dataFB["name"] as string : null);
                  string first_name = dataFB.ContainsKey("first_name") ? dataFB["first_name"] as string : null;
                  string last_name = dataFB.ContainsKey("last_name") ? dataFB["last_name"] as string : null;
                  string gender = dataFB.ContainsKey("gender") ? dataFB["gender"] as string : null;
                  string birthday = dataFB.ContainsKey("birthday") ? dataFB["birthday"] as string : null;
                  //
                  ILazyLoginMapper llMapper = HelperExtensionCustom.DC.GetLazyLoginMapper((Guid)user.ProviderUserKey, true);
                  var llAnagrafica = HelperExtensionCustom.DC.GetLazyLoginMapperChild<Custom.DB.LazyLogin_AnagraficaMain>(llMapper, false);
                  //
                  // solo per i record nuovi e validi
                  //if (llAnagrafica != null && llAnagrafica.IdLL == 0)
                  if (llAnagrafica != null && string.IsNullOrEmpty(llAnagrafica.Nome))
                  {
                      llAnagrafica.Nome = first_name.TrimSafe();
                      llAnagrafica.Cognome = last_name.TrimSafe();
                      llAnagrafica.EMail = user.Email;
                      llAnagrafica.DataNascita = Utility.TryParse<DateTime?>(birthday, null);
                      llAnagrafica.Sesso = (gender ?? string.Empty) == "female" ? 'F' : 'M';
                      llAnagrafica.flag_Privacy = true;
                      //llAnagrafica.flag_PrivacyAnalytics = true;
                      llAnagrafica.flag_PrivacyCommerciale = true;
                      llAnagrafica.FacebookUserName = dataFB.ContainsKey("username") ? dataFB["username"] as string : null;
                      //
                      var chg = HelperExtensionCustom.DC.GetChangeSet();
                      HelperExtensionCustom.DC.SubmitChanges();
                  }
                  //
              }
              catch
              {
                  result = false;
              }
          }
          return result;
      }



  }

}
