/*
 * 
 * Intranet Ikon
 * 
 * Copyright (C) 2009 Ikon Srl
 * Tutti i Diritti Riservati. All Rights Reserved
 * 
 */


using System;
using System.Data;
using System.Configuration;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.UI;
using System.Web.Security;
using System.Xml.Linq;
using System.Data.Linq;
using System.IO;
using System.Text;
using System.Linq.Expressions;
using System.Threading;
using System.Reflection;
using System.Data.Linq.Mapping;
using System.Diagnostics;
using System.Linq.Dynamic;
using System.Transactions;
using System.Web.Caching;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;  // assembly System.ServiceModel.Web
using LinqKit;

using Ikon;
using Ikon.GD;
using System.Globalization;


namespace Ikon.GD
{

  //
  // nelle API VFS e Path modificare la modalita' di utilizzo della lingua:
  // utilizzare sempre una stringa e mai un bool che comporta delle API incoerenti
  // "it" --> lingua forzata manualmente
  // null --> LanguageHelper.Language
  // "*" --> filtro per le lingue disabilitato (usare una costante di FS_Operations) (LanguageNoFilterCode)
  // devono essere rivisti i filtri automatici su VNODE e VDATA per fsOp (attenzione alle interazoni con Author.wgx)
  //
  // inoltre si dovra' estendere il ModelBuilder: analizzando i path della request se la lingua non corrispondesse a quella della sessione
  // si effettuera' un cambio temporaneo per la lingua del context (es rendering di una pagina en senza cambiare la lingua corrente del sito)
  //




  public static class IKGD_Language_Provider
  {
    public enum LanguageAutoconfigEnum { None, Browser, BrowserFallBack, Domain, ForceDefault, CountryByIP, Custom }

    public static IKGD_Language_Provider_Interface Provider { get; set; }

    static IKGD_Language_Provider()
    {
      Provider = new IKGD_Language_Provider_Base();
    }
  }


  public interface IKGD_Language_Provider_Interface
  {
    string LanguageNN { get; set; }
    string Language { get; set; }
    string LanguageSession { get; set; }
    string LanguageContext { get; set; }
    string LanguageVFS { get; set; }
    string LanguageAuthor { get; set; }
    string LanguageAuthorNoFilterCode { get; }
    string LanguageNoFilterCode { get; }
    string LanguageMeta { get; }
    //string LanguageModel { get; set; }
    //
    void Reset();
    bool ValidateLanguage(string language);
    IList<string> LanguagesAvailable();
    void LanguagesAvailableOverride(List<string> languagesAvailableForContext);
    void AutoConfig(IKGD_Language_Provider.LanguageAutoconfigEnum? mode, string defaultSiteLanguage);
    void AutoConfig(IKGD_Language_Provider.LanguageAutoconfigEnum? mode, string defaultSiteLanguage, bool? forceSetThreadCulture);
    void AutoConfig(IKGD_Language_Provider.LanguageAutoconfigEnum? mode, string defaultSiteLanguage, Action preProcessor, Action postProcessor);
    CultureInfo GetCultureFromLanguage(string language);
    string GetCultureStringFromLanguage(string language);
    string GetCultureStringFromLanguage2(string language);
    CultureInfo SetThreadCulture(string forcedLanguage);
    //
    string GetCountryFromClientIP();
    string GetCountryFromClientIP(string forcedIP);
    string GetLanguageFromClientIP();
    string GetLanguageFromClientIP(string forcedIP, bool? filterFromBrowserLanguages);
    //
  }



  public class IKGD_Language_Provider_Base : IKGD_Language_Provider_Interface
  {
    protected object _lock = new object();
    public readonly string RequestBaseName = "IKGD_VFS_LanguageHelper";
    public readonly string LanguagesListBaseName = "IKGD_VFS_LanguagesListOverride";
    public readonly string LanguageFallBack = IKGD_Config.AppSettings["LanguageFallBack"] ?? "it";


    public IKGD_Language_Provider_Base()
    {
      _isDistinct_us_en = new string[] { "en", "us" }.Intersect(LanguagesAvailable()).Count() == 2;
    }


    public virtual string LanguageNN
    {
      get { lock (_lock) { return Language ?? LanguageFallBack; } }
      set { lock (_lock) { Language = value; } }
    }


    // non utiliziamo la session ma direttamente una cookie
    public virtual string Language
    {
      get
      {
        lock (_lock)
        {
          return LanguageContext ?? FS_OperationsHelpers.LanguageSession;
        }
      }
      set
      {
        lock (_lock)
        {
          if (ValidateLanguage(value))
          {
            if (LanguageVFS != null)
              LanguageVFS = null;
            if (LanguageContext != null)
              LanguageContext = null;
            FS_OperationsHelpers.LanguageSession = value;
          }
        }
      }
    }


    public virtual string LanguageSession
    {
      get
      {
        lock (_lock)
        {
          return FS_OperationsHelpers.LanguageSession;
        }
      }
      set
      {
        lock (_lock)
        {
          if (ValidateLanguage(value))
            FS_OperationsHelpers.LanguageSession = value;
          if (value != null)
          {
            SetThreadCulture(value);
          }
        }
      }
    }


    public virtual string LanguageContext
    {
      get
      {
        lock (_lock)
        {
          return HttpContext.Current.Items[RequestBaseName] as string;
        }
      }
      set
      {
        lock (_lock)
        {
          if (ValidateLanguage(value))
            HttpContext.Current.Items[RequestBaseName] = value;
          if (value != null)
          {
            SetThreadCulture(value);
          }
        }
      }
    }


    // dovra' essere ridefinita solo nel provider che supporta IoC e per override locali all'IoC (non e' in relazione alla FS_Lib)
    public virtual string LanguageVFS { get { return null; } set { } }


    // e' solo un duplicato di Language nel caso servissero trattamenti diversificati
    public virtual string LanguageAuthor { get { return Language; } set { Language = value; } }
    public virtual string LanguageAuthorNoFilterCode { get { return Language.NullIfEmpty() ?? FS_Operations.LanguageNoFilterCode; } }


    public virtual string LanguageNoFilterCode { get { return Language.NullIfEmpty() ?? FS_Operations.LanguageNoFilterCode; } }


    public virtual string LanguageMeta { get { return GetCultureStringFromLanguage(LanguageNN); } }


    public virtual void Reset()
    {
      lock (_lock)
      {
        LanguageVFS = null;
        LanguageContext = null;
        LanguageSession = null;
      }
    }


    public virtual bool ValidateLanguage(string language)
    {
      try
      {
        if (language == null)
          return true;
        if (LanguagesAvailable().Contains(language))
          return true;
      }
      catch { }
      return false;
    }


    protected List<string> _LanguagesAvailable;
    public virtual IList<string> LanguagesAvailable()
    {
      try
      {
        var languagesAvailableForContext = HttpContext.Current.Items[LanguagesListBaseName] as List<string>;
        if (languagesAvailableForContext != null)
          return languagesAvailableForContext;
        return _LanguagesAvailable ?? (_LanguagesAvailable = IKGD_Config.GetElements<string>("Languages", "language").Where(l => !string.IsNullOrEmpty(l)).ToList());
      }
      catch { return new List<string>(); }
    }


    public virtual void LanguagesAvailableOverride(List<string> languagesAvailableForContext)
    {
      if (languagesAvailableForContext != null && languagesAvailableForContext.Any())
      {
        HttpContext.Current.Items[LanguagesListBaseName] = languagesAvailableForContext;
      }
      else
      {
        HttpContext.Current.Items[LanguagesListBaseName] = null;
      }
    }


    protected bool _isDistinct_us_en;


    public virtual string GetCultureStringFromLanguage(string language)
    {
      if (_isDistinct_us_en)
      {
        if (string.Equals(language, "us", StringComparison.OrdinalIgnoreCase))
          return "en-US";
        else if (string.Equals(language, "en", StringComparison.OrdinalIgnoreCase))
          return "en-GB";
      }
      //
      if (string.Equals(language, "us", StringComparison.OrdinalIgnoreCase))
        return "en";
      else if (string.Equals(language, "zh", StringComparison.OrdinalIgnoreCase))
        return "zh-CN";
      return language;
    }


    public virtual string GetCultureStringFromLanguage2(string language)
    {
      language = language ?? LanguageMeta;
      if (string.Equals(language, "us", StringComparison.OrdinalIgnoreCase))
        return "en-US";
      else if (string.Equals(language, "en", StringComparison.OrdinalIgnoreCase))
      {
        if (_isDistinct_us_en)
          return "en-GB";
        else
          return "en-US";
      }
      else if (string.Equals(language, "zh", StringComparison.OrdinalIgnoreCase))
        return "zh-CN";
      if (language.Contains("_"))
        language = language.Replace("_", "-");
      if (language.Contains("-"))
        return language;
      return string.Format("{0}-{1}", language.ToLower(), language.ToUpper());
    }


    public virtual CultureInfo GetCultureFromLanguage(string language)
    {
      CultureInfo ci = null;
      try
      {
        string lang_orig = language ?? Language;
        string lang = GetCultureStringFromLanguage(lang_orig);
        try
        {
          // usiamo la session per limitare le richieste di generazione di CultureInfo
          ci = HttpContext.Current.Session[RequestBaseName] as CultureInfo;
          if (ci != null)
          {
            if (string.Equals(ci.Name, lang, StringComparison.OrdinalIgnoreCase))
              return ci;
            else if (string.Equals(ci.TwoLetterISOLanguageName, lang, StringComparison.OrdinalIgnoreCase))
              return ci;
            else if (string.Equals(ci.TwoLetterISOLanguageName, lang_orig, StringComparison.OrdinalIgnoreCase) && !_isDistinct_us_en)
              return ci;
          }
          //if (ci != null && (string.Equals(ci.TwoLetterISOLanguageName, lang, StringComparison.OrdinalIgnoreCase) || string.Equals(ci.TwoLetterISOLanguageName, lang_orig, StringComparison.OrdinalIgnoreCase) || string.Equals(ci.Name, lang, StringComparison.OrdinalIgnoreCase)))
          //  return ci;
        }
        catch { }
        if (!string.IsNullOrEmpty(lang))
        {
          try
          {
            //ci = CultureInfo.CreateSpecificCulture((lang).ToLower());
            ci = CultureInfo.GetCultureInfo(lang);
          }
          catch { }
          if (ci == null)
          {
            try { ci = CultureInfo.GetCultures(CultureTypes.AllCultures).Where(r => string.Equals(r.TwoLetterISOLanguageName, lang, StringComparison.OrdinalIgnoreCase) || string.Equals(r.TwoLetterISOLanguageName, lang_orig, StringComparison.OrdinalIgnoreCase)).FirstOrDefault(); }
            catch { }
          }
          HttpContext.Current.Session[RequestBaseName] = ci ?? System.Threading.Thread.CurrentThread.CurrentCulture;
        }
      }
      catch { }
      return ci ?? System.Threading.Thread.CurrentThread.CurrentCulture;
    }



    //
    // utilizzare questa funzione in global.asax (Application_AcquireRequestState) dopo aver eseguito AutoConfig
    //
    public virtual CultureInfo SetThreadCulture(string forcedLanguage)
    {
      CultureInfo ci = null;
      try
      {
        ci = GetCultureFromLanguage(forcedLanguage);
        System.Threading.Thread.CurrentThread.CurrentUICulture = ci;
        //System.Threading.Thread.CurrentThread.CurrentCulture = ci;  // non funziona con "zh", "en", ...
        System.Threading.Thread.CurrentThread.CurrentCulture = System.Globalization.CultureInfo.CreateSpecificCulture(ci.Name);
      }
      catch { }
      return ci;
    }


    //
    private static string[] _FakeCountriesByIP = new string[] { "eu", "ap", "a1", "a2" };
    //
    public string GetCountryFromClientIP() { return GetCountryFromClientIP(null); }
    public string GetCountryFromClientIP(string forcedIP)
    {
      string country = null;
      try
      {
        country = FS_OperationsHelpers.CountrySession;
        if (country == null || forcedIP.IsNotEmpty())
        {
          string query = null;
          try { query = string.Join(".", (forcedIP.NullIfEmpty() ?? Utility.GetRequestAddressExt(null)).Split('.').Reverse().ToArray()); }
          catch { }
          if (query.IsNotEmpty())
          {
            try
            {
              string name = query + (IKGD_Config.AppSettings["DNSBL_GeoipResolverAddress"] ?? ".geoip.dnsbl.digitalwebland.com");
              Heijden.DNS.Resolver resolver = new Heijden.DNS.Resolver();
              resolver.UseCache = true; // importante
              resolver.TimeOut = Utility.TryParse<int>(IKGD_Config.AppSettings["DNSBL_ResolverTimeout"], 1); // 1s
              resolver.Retries = 1;
              resolver.TransportType = Heijden.DNS.TransportType.Udp;
              //r.DnsServer = "217.64.205.98";
              //r.DnsServer = "81.2.233.17";
              //r.DnsServer = "85.94.194.106";
              Heijden.DNS.Response resp = resolver.Query(name, Heijden.DNS.QType.TXT, Heijden.DNS.QClass.IN);
              if (resp != null)
              {
                string _country = resp.RecordsTXT.Select(r => r.TXT).FirstOrDefault(r => r.IsNotEmpty());
                if (_country.IsNotEmpty() && !_FakeCountriesByIP.Contains(_country, StringComparer.OrdinalIgnoreCase))
                {
                  country = _country.ToLower();
                }
              }
            }
            catch { }
          }
        }
        if (forcedIP.IsNullOrEmpty())
        {
          if (country.IsNullOrEmpty() && IKGD_Config.AppSettings["DNSBL_ResolverAddressFakeNets"] != null)
          {
            foreach (var block in IKGD_Config.AppSettings["DNSBL_ResolverAddressFakeNets"].Split(','))
            {
              string nets = block.Split(':').FirstOrDefault().Trim();
              string naz = block.Split(':').Skip(1).FirstOrDefault().TrimSafe();
              if (naz.IsNotEmpty() && Utility.CheckNetMaskIP(forcedIP.NullIfEmpty() ?? Utility.GetRequestAddressExt(null), nets))
              {
                country = naz;
                break;
              }
            }
          }
          FS_OperationsHelpers.CountrySession = country ?? string.Empty;
        }
      }
      catch { }
      return country ?? string.Empty;
    }


    public string GetLanguageFromClientIP() { return GetLanguageFromClientIP(null, null); }
    public string GetLanguageFromClientIP(string forcedIP, bool? filterFromBrowserLanguages)
    {
      string language = null;
      string country = GetCountryFromClientIP(forcedIP);
      List<string> languages = new List<string>();
      var languagesAvailable = LanguagesAvailable();
      if (country.IsNotEmpty())
      {
        try
        {
          var cultureInfos = CultureInfo.GetCultures(CultureTypes.AllCultures).Where(c => c.Name.EndsWith(country, StringComparison.InvariantCultureIgnoreCase)).ToList();
          foreach (var ci in cultureInfos)
          {
            string lang = null;
            var ri = new RegionInfo(ci.LCID);
            if (ri != null && languagesAvailable.Contains(ri.TwoLetterISORegionName.ToLowerInvariant()))
              lang = ri.TwoLetterISORegionName.ToLowerInvariant();
            else if (languagesAvailable.Contains(ci.TwoLetterISOLanguageName.ToLowerInvariant()))
              lang = ci.TwoLetterISOLanguageName.ToLowerInvariant();
            if (lang.IsNotEmpty() && languagesAvailable.Contains(lang, StringComparer.OrdinalIgnoreCase))
              languages.Add(lang.ToLower());
          }
        }
        catch { }
        language = languages.FirstOrDefault();
        if (filterFromBrowserLanguages.GetValueOrDefault(true))
        {
          try
          {
            if (languages.Any() && HttpContext.Current.Request.UserLanguages != null && HttpContext.Current.Request.UserLanguages.Any())
            {
              List<string> languagesFromBrowser = new List<string>();
              foreach (var lang in HttpContext.Current.Request.UserLanguages)
              {
                try
                {
                  var lang2 = lang.Split(';').FirstOrDefault().TrimSafe().ToLowerInvariant();
                  if (languages.Contains(lang2))
                  {
                    languagesFromBrowser.Add(lang2);
                  }
                  else
                  {
                    var ci = CultureInfo.CreateSpecificCulture(lang2);
                    var ri = new RegionInfo(ci.LCID);
                    if (languages.Contains(ri.TwoLetterISORegionName.ToLowerInvariant()))
                      languagesFromBrowser.Add(ri.TwoLetterISORegionName.ToLowerInvariant());
                    else if (languages.Contains(ci.TwoLetterISOLanguageName.ToLowerInvariant()))
                      languagesFromBrowser.Add(ci.TwoLetterISOLanguageName.ToLowerInvariant());
                  }
                }
                catch { }
              }
              language = languages.Intersect(languagesFromBrowser).FirstOrDefault() ?? language;
            }
          }
          catch { }
        }
      }
      //
      return language;
    }


    //
    // da utilizzare in global.asax
    // in: protected void Application_AcquireRequestState(object sender, EventArgs e)
    //
    public virtual void AutoConfig(IKGD_Language_Provider.LanguageAutoconfigEnum? mode, string defaultSiteLanguage) { IKGD_Language_Provider.Provider.AutoConfig(mode, defaultSiteLanguage, null); }
    public virtual void AutoConfig(IKGD_Language_Provider.LanguageAutoconfigEnum? mode, string defaultSiteLanguage, bool? forceSetThreadCulture)
    {
      IKGD_Language_Provider.Provider.AutoConfig(mode, defaultSiteLanguage, null, null);
      if (forceSetThreadCulture.GetValueOrDefault(true))
      {
        if (IKGD_Language_Provider.Provider.LanguageNN != System.Threading.Thread.CurrentThread.CurrentCulture.TwoLetterISOLanguageName)
        {
          IKGD_Language_Provider.Provider.SetThreadCulture(IKGD_Language_Provider.Provider.LanguageNN);
        }
      }
    }
    public virtual void AutoConfig(IKGD_Language_Provider.LanguageAutoconfigEnum? mode, string defaultSiteLanguage, Action preProcessor, Action postProcessor)
    {
      string forceLanguageContext = null;
      string forceLanguageSession = null;
      try
      {
        //
        // controllo della querystring per vedere se sono stati specificatii degli override per il language (session o context) e setup immediato
        // LangCMS=..  (settaggio temporaneo nel context)
        // LangSetCMS=.. (settaggio permanente in sessione)
        //
        if (!string.IsNullOrEmpty(HttpContext.Current.Request.QueryString["LangCMS"]))
          forceLanguageContext = HttpContext.Current.Request.QueryString["LangCMS"];
        if (!string.IsNullOrEmpty(HttpContext.Current.Request.QueryString["LangSetCMS"]))
          forceLanguageSession = HttpContext.Current.Request.QueryString["LangSetCMS"];
        //
        // se non e' stata ancora definita una lingua di sessione si provvede al setup
        // il codice e' strutturato per essere eseguito in global.asax
        //
        defaultSiteLanguage = defaultSiteLanguage ?? FS_Operations.LanguageNoFilterCode;
        //
        if (IKGD_Language_Provider.Provider.LanguageSession == null && forceLanguageSession == null && defaultSiteLanguage != FS_Operations.LanguageNoFilterCode)
        {
          //
          mode = mode ?? IKGD_Language_Provider.LanguageAutoconfigEnum.ForceDefault;
          //
          if (preProcessor != null)
          {
            try { preProcessor(); }
            catch { }
            forceLanguageSession = IKGD_Language_Provider.Provider.LanguageSession;
          }
          //
          var languagesAvailable = LanguagesAvailable();
          if (forceLanguageSession == null && mode == IKGD_Language_Provider.LanguageAutoconfigEnum.Browser)
          {
            try
            {
              if (HttpContext.Current.Request.UserLanguages != null && HttpContext.Current.Request.UserLanguages.Any())
              {
                foreach (var lang in HttpContext.Current.Request.UserLanguages)
                {
                  try
                  {
                    var lang2 = lang.Split(';').FirstOrDefault().TrimSafe().ToLowerInvariant();
                    if (languagesAvailable.Contains(lang2))
                    {
                      forceLanguageSession = lang2;
                    }
                    else
                    {
                      var ci = CultureInfo.CreateSpecificCulture(lang2);
                      var ri = new RegionInfo(ci.LCID);
                      if (languagesAvailable.Contains(ri.TwoLetterISORegionName.ToLowerInvariant()))
                        forceLanguageSession = ri.TwoLetterISORegionName.ToLowerInvariant();
                      else if (languagesAvailable.Contains(ci.TwoLetterISOLanguageName.ToLowerInvariant()))
                        forceLanguageSession = ci.TwoLetterISOLanguageName.ToLowerInvariant();
                    }
                  }
                  catch { }
                  if (forceLanguageSession != null)
                    break;
                }
              }
            }
            catch { }
          }
          //
          // se trova un languange tra i preferiti nell'header lo utilizza
          // se non trova nessun match tra quelli specificati nell'header allora utilizza quello passato in defaultSiteLanguage
          // se non ho specificato languages preferiti nell'header usa il primo language della lista configurata (es il caso dei BOT)
          // in questo modo se un utente non ha disponibile la lingua preferita viene mostrato in inglese
          //
          if (forceLanguageSession == null && mode == IKGD_Language_Provider.LanguageAutoconfigEnum.BrowserFallBack)
          {
            try
            {
              if (HttpContext.Current.Request.UserLanguages != null && HttpContext.Current.Request.UserLanguages.Any())
              {
                foreach (var lang in HttpContext.Current.Request.UserLanguages)
                {
                  try
                  {
                    var lang2 = lang.Split(';').FirstOrDefault().ToLowerInvariant().Trim();
                    if (languagesAvailable.Contains(lang2))
                    {
                      forceLanguageSession = lang2;
                    }
                    else
                    {
                      var ci = CultureInfo.CreateSpecificCulture(lang2);
                      var ri = new RegionInfo(ci.LCID);
                      if (languagesAvailable.Contains(ri.TwoLetterISORegionName.ToLowerInvariant()))
                        forceLanguageSession = ri.TwoLetterISORegionName.ToLowerInvariant();
                      else if (languagesAvailable.Contains(ci.TwoLetterISOLanguageName.ToLowerInvariant()))
                        forceLanguageSession = ci.TwoLetterISOLanguageName.ToLowerInvariant();
                    }
                  }
                  catch { }
                  if (forceLanguageSession != null)
                    break;
                }
              }
            }
            catch { }
            try
            {
              if (forceLanguageSession == null && HttpContext.Current.Request.UserLanguages != null && HttpContext.Current.Request.UserLanguages.Any())
              {
                forceLanguageSession = defaultSiteLanguage;
              }
              if (forceLanguageSession == null)
              {
                forceLanguageSession = languagesAvailable.FirstOrDefault() ?? defaultSiteLanguage;
              }
            }
            catch { }
          }
          //
          if (forceLanguageSession == null && mode == IKGD_Language_Provider.LanguageAutoconfigEnum.CountryByIP)
          {
            if (Utility.CheckIfBOT() == false)
            {
              forceLanguageSession = GetLanguageFromClientIP(null, true);
            }
          }
          //
          if (forceLanguageSession == null && mode == IKGD_Language_Provider.LanguageAutoconfigEnum.Domain)
          {
            var hostTLD = HttpContext.Current.Request.Url.Host.ToLower().Split('.').LastOrDefault();
            // corregge il codice lingua in base al dominio
            if (hostTLD == "si")    // dominio sloveno
              hostTLD = "sl";     // language code ISO

            // se il language da settare non e' valido (oppure si tratta di domini non nazionali allora si passa alla modalita' ForceDefault)
            if (ValidateLanguage(hostTLD))
              forceLanguageSession = hostTLD;
            else
              mode = IKGD_Language_Provider.LanguageAutoconfigEnum.ForceDefault;
          }
          //
          if (forceLanguageSession != null)
          {
            IKGD_Language_Provider.Provider.LanguageSession = forceLanguageSession;
            forceLanguageSession = null;
          }
          //
          // esecuzione del postprocess prima del forcedefault per poter inserire dei trattamenti custom per la gestione dei domini
          if (postProcessor != null)
          {
            try { postProcessor(); }
            catch { }
          }
          //
          if (IKGD_Language_Provider.Provider.LanguageSession == null && mode == IKGD_Language_Provider.LanguageAutoconfigEnum.ForceDefault && !string.IsNullOrEmpty(defaultSiteLanguage))
          {
            IKGD_Language_Provider.Provider.LanguageSession = defaultSiteLanguage;
          }
          //
          if (IKGD_Language_Provider.Provider.LanguageSession == null)
          {
            IKGD_Language_Provider.Provider.LanguageSession = languagesAvailable.FirstOrDefault();
          }
          //
        }
        //
      }
      catch { }
      finally
      {
        if (forceLanguageSession != null)
          IKGD_Language_Provider.Provider.LanguageSession = forceLanguageSession;
        if (forceLanguageContext != null)
          IKGD_Language_Provider.Provider.LanguageContext = forceLanguageContext;
      }
    }


  }

}
