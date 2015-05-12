using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using Ikon;
using Ikon.IKCMS;
using Ikon.GD;
using IkonWeb.Controllers;
using System.Web.Security;
using Ikon.Auth.Login;
using SampleSiteWeb;

namespace SampleSite_Web.Controllers
{
    public class AreaPersonaleController : AutoStaticCMS_Controller
    {
        public ActionResult Index()
        {
            return View();
        }

        [LazyLoginFilterNoAnonymous]
        public ActionResult DatiPersonali()
        {
            ILazyLoginMapper llMapper = HelperExtensionCustom.DC.GetLazyLoginMapper();
            var llAnagrafica = HelperExtensionCustom.DC.GetLazyLoginMapperChild<Custom.DB.LazyLogin_AnagraficaMain>(llMapper, true);
            ViewData["anagraficaMain"] = llAnagrafica;
            return View();
        }

        [LazyLoginFilterNoAnonymous]
        [AcceptVerbs(HttpVerbs.Post)]
        [ValidateInput(false)]
        public ActionResult DatiPersonali(FormCollection collection)
        {
            string successUrl = null;
            List<string> messages = new List<string>();
            //
            try
            {
                successUrl = collection["successUrl"].NullIfEmpty("javascript:;").NullIfEmpty();

                Custom.DB.LazyLoginMapper llMapper = HelperExtensionCustom.DC.GetLazyLoginMapper() as Custom.DB.LazyLoginMapper;
                var anagraficaMain = HelperExtensionCustom.DC.GetLazyLoginMapperChild<Custom.DB.LazyLogin_AnagraficaMain>(llMapper, true);
                //
                if (!Utility.ValidateEMail(collection["input_EMail"]))
                    messages.Add("L'E-mail inserita non è un indirizzo di E-mail valido.");
                //
                if (collection["input_Nome"].Trim().Length == 0)
                    messages.Add("E' necessario compilare il campo Nome.");
                if (collection["input_Cognome"].Trim().Length == 0)
                    messages.Add("E' necessario compilare il campo Cognome.");
                /*if (collection["input_Indirizzo"].Trim().Length == 0)
                    messages.Add("E' necessario compilare il campo Indirizzo.");
                if (collection["input_CAP"].Trim().Length == 0)
                    messages.Add("E' necessario compilare il campo CAP.");
                if (collection["input_Comune"].Trim().Length == 0)
                    messages.Add("E' necessario compilare il campo Località.");
                if (collection["input_Provincia"].Trim().Length == 0)
                    messages.Add("E' necessario compilare il campo Provincia.");*/
                //
                if (messages.Count > 0)
                    return Json(new { hasError = true, message = "<ul>" + string.Join("\n", messages.Select(m => string.Format("<li>{0}</li>", HttpUtility.HtmlEncode(m))).ToArray()) + "</ul>" });
                //
                string Email = (collection["input_EMail"] ?? string.Empty).Trim();
                string fullName = Utility.Implode(new string[] { collection["input_Nome"], collection["input_Cognome"] }, " ", null, true, true);
                //
                // update dei membership data
                //
                if (!string.Equals(llMapper.aspnet_Membership.Email, Email))
                    llMapper.aspnet_Membership.Email = Email;
                if (!string.Equals(llMapper.aspnet_Membership.Comment, fullName))
                    llMapper.aspnet_Membership.Comment = fullName;
                //
                // update dei dati anagrafici
                //
                anagraficaMain.Nome = collection["input_Nome"].Trim();
                anagraficaMain.Cognome = collection["input_Cognome"].Trim();
                anagraficaMain.EMail = collection["input_EMail"].Trim();
                anagraficaMain.Indirizzo = collection["input_Indirizzo"].Trim();
                anagraficaMain.CAP = collection["input_CAP"].Trim();
                anagraficaMain.Comune = collection["input_Comune"].Trim();
                anagraficaMain.Provincia = collection["input_Provincia"].Trim();
                anagraficaMain.Telefono = collection["input_Telefono"].Trim();
                //
                var chg = HelperExtensionCustom.DC.GetChangeSet();
                HelperExtensionCustom.DC.SubmitChanges();
                //
                string message = "I tuoi dati sono stati aggiornati.";
                //
                return Json(new { hasError = false, successUrl = successUrl, message = message });
            }
            catch (Exception ex)
            {
                return Json(new { hasError = true, message = ex.Message });
            }
        }


        /// <summary>
        /// Registrazione al sito
        /// </summary>
        /// <returns></returns>
        public ActionResult Register()
        {
            return View();
        }

        [AcceptVerbs(HttpVerbs.Post)]
        [ValidateInput(false)]
        [Recaptcha.RecaptchaControlMvc.CaptchaValidatorAttribute]
        public ActionResult Register(FormCollection collection, bool captchaValid)
        {
            string successUrl = null;
            List<string> messages = new List<string>();
            //
            try
            {
                successUrl = IKCMS_RouteUrlManager.GetMvcActionUrl<SampleSite_Web.Controllers.AreaPersonaleController>(c => c.Index());
                //
                if (!Utility.ValidateEMail(collection["input_EMail"]))
                    messages.Add("L'E-mail inserita non è un indirizzo di E-mail valido.");
                if (!string.IsNullOrEmpty(collection["input_EMail"]))
                {
                    if (Membership.GetUser((collection["input_EMail"] ?? string.Empty).Trim()) != null)
                        messages.Add("Lo username corrispondente all'indirizzo E-mail inserito è già utilizzato nel sistema.");
                }
                //
                // check delle password
                if (string.IsNullOrEmpty(collection["extra_PasswordNew"]))
                    messages.Add("Per la creazione di un nuovo utente è necessario specificare una password.");
                if (collection["extra_PasswordNew"] != collection["extra_PasswordVerify"])
                    messages.Add("Le password inserite non coincidono.");
                else if (!string.IsNullOrEmpty(collection["extra_PasswordNew"]) && collection["extra_PasswordNew"].Length < 8)
                    messages.Add("La password deve essere di almeno 8 caratteri.");
                //
                if (collection["input_Nome"].Trim().Length == 0)
                    messages.Add("E' necessario compilare il campo Nome.");
                if (collection["input_Cognome"].Trim().Length == 0)
                    messages.Add("E' necessario compilare il campo Cognome.");
                /*if (collection["input_Indirizzo"].Trim().Length == 0)
                    messages.Add("E' necessario compilare il campo Indirizzo.");
                if (collection["input_CAP"].Trim().Length == 0)
                    messages.Add("E' necessario compilare il campo CAP.");
                if (collection["input_Comune"].Trim().Length == 0)
                    messages.Add("E' necessario compilare il campo Località.");
                if (collection["input_Provincia"].Trim().Length == 0)
                    messages.Add("E' necessario compilare il campo Provincia.");*/
                // Privacy
                string hh = collection["input_flag_Privacy"];
                if (Utility.TryParse<bool?>(collection["input_flag_Privacy"], null) == null)
                    messages.Add("E' necessario specificare il proprio consenso al trattamento dei dati personali.");
                //
                // Verifica reCAPCHA
                if (!captchaValid && collection["recaptcha_challenge_field"].IsNotEmpty())
                {
                    messages.Add("E' necessario inserire correttamente i testi di verifica reCAPTCHA.");
                }
                //
                if (messages.Count > 0)
                    return Json(new { hasError = true, message = "<ul>" + string.Join("\n", messages.Select(m => string.Format("<li>{0}</li>", HttpUtility.HtmlEncode(m))).ToArray()) + "</ul>" });

                //
                // creazione dell'account
                //
                bool sendWelcomMessage = false;
                MembershipCreateStatus status;
                MembershipUser user = Membership.CreateUser((collection["input_EMail"] ?? string.Empty).Trim(), collection["extra_PasswordNew"], (collection["input_EMail"] ?? string.Empty).Trim(), null, null, true, out status);
                if (user == null || status != MembershipCreateStatus.Success)
                    throw new Exception("Errore nella creazione dell'account: " + status.ToString());

                sendWelcomMessage = true;
                user.Comment = collection["input_Nome"].Trim();
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
                llAnagrafica.Nome = collection["input_Nome"].Trim();
                llAnagrafica.Cognome = collection["input_Cognome"].Trim();
                llAnagrafica.EMail = collection["input_Email"].Trim();
                llAnagrafica.Indirizzo = collection["input_Indirizzo"].Trim();
                llAnagrafica.CAP = collection["input_CAP"].Trim().StringTruncate(5);
                llAnagrafica.Comune = collection["input_Comune"].Trim();
                llAnagrafica.Provincia = collection["input_Provincia"];
                llAnagrafica.Telefono = collection["input_Telefono"].NullIfEmpty();
                llAnagrafica.flag_Privacy = true;

                var chg = HelperExtensionCustom.DC.GetChangeSet();
                HelperExtensionCustom.DC.SubmitChanges();
                //
                if (sendWelcomMessage)
                {
                    IkonWeb.Controllers.AuthController.MailSendWelcomeNewUser(user, (msg) =>
                    {
                        msg.Body = msg.Body.Replace("{$thisAction}", IKCMS_RouteUrlManager.GetMvcActionUrl<SampleSite_Web.Controllers.AreaPersonaleController>(null, c => c.DatiPersonali(), true));
                    }, this.ControllerContext);
                }
                string message = "Grazie per esserti iscritto al sito {0}.".FormatString(IKGD_Config.AppSettingsWeb["IKGD_Application"] ?? string.Empty);
                string trackPageview = collection["trackPageview"] ?? "/registrazione-effettuata/";
                //
                return Json(new { hasError = false, successUrl = successUrl, message = message, trackPageview = trackPageview });
            }
            catch (Exception ex)
            {
                return Json(new { hasError = true, message = ex.Message });
            }
        }
    }
}
