using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;
using System.Web;
using System.Web.Mvc;
using System.Web.Mvc.Ajax;
using System.IO;
using System.Web.Security;
using System.Net.Mail;
using System.Security.Cryptography;
using System.Text;
using System.Data;
using System.Data.Common;
using System.Transactions;
using Microsoft.Web.Mvc;
using Newtonsoft.Json;
using LinqKit;
using Autofac;

using Ikon;
using Ikon.Config;
using Ikon.GD;
using Ikon.IKCMS;
using Ikon.IKCMS.Library.Resources;
using Ikon.IKGD.Library.Resources;
using System.Web.Hosting;
using System.Net;


namespace Ikon.IKCMS
{


  public static class MinimizeIncludesHelper
  {

    public static List<string> ProcessMinifierXml(bool? verbose, bool? js, bool? css, bool? less)
    {
      List<string> messages = new List<string>();
      try
      {
        verbose = verbose ?? true;
        //XElement xFiles = Utility.FileReadXmlVirtual("~/Content/Minifier.xml");
        XElement xFiles = XElement.Parse(EmbeddedResourcesVPP_Helper.GetResourceOrFileAsString("~/Content/Minifier.xml"));
        foreach (var xFile in xFiles.Elements("File"))
        {
          string fileName = xFile.AttributeValueNN("filename");
          string type = xFile.AttributeValueNN("type");
          if (!fileName.StartsWith("~/"))
            throw new Exception(string.Format("Filename di destinazione non valido: {0}", fileName));
          //
          StringBuilder sb = new StringBuilder();
          StringBuilder sb_nc = new StringBuilder();
          foreach (XElement xF in xFile.Elements("Add").Where(x => !string.IsNullOrEmpty(x.AttributeValue("filename"))).Distinct((x1, x2) => x1.AttributeValue("filename") == x2.AttributeValue("filename")))
          {
            string fName = xF.AttributeValue("filename").TrimSafe();
            if (fName.EndsWith("/") && HostingEnvironment.VirtualPathProvider.DirectoryExists(fName.TrimEnd('/')))
            {
              foreach (VirtualFile file in HostingEnvironment.VirtualPathProvider.GetDirectory(fName.TrimEnd('/')).Files.OfType<VirtualFile>().OrderBy(r => r.Name.ToLowerInvariant()))
              {
                if (string.Equals(type, "javascript", StringComparison.OrdinalIgnoreCase) && !file.Name.EndsWith(".js", StringComparison.OrdinalIgnoreCase))
                  continue;
                if (string.Equals(type, "css", StringComparison.OrdinalIgnoreCase) && !(file.Name.EndsWith(".css", StringComparison.OrdinalIgnoreCase) || file.Name.EndsWith(".less", StringComparison.OrdinalIgnoreCase)))
                  continue;
                using (var stream = file.Open())
                {
                  using (StreamReader reader = new StreamReader(stream, Encoding.UTF8))
                  {
                    if (verbose.Value)
                      messages.Add("Read: {0} -> {1}".FormatString(file.VirtualPath, stream.Length));
                    sb.AppendLine(reader.ReadToEnd());
                    sb.AppendLine("/**/");
                    //messages.Add(string.Format(" -- {0} -> {1}\n", file.Name, stream.Length));
                  }
                }
              }
            }
            else
            {
              if (Utility.TryParse<bool>(xF.AttributeValue("Compress"), true))
              {
                string contents = EmbeddedResourcesVPP_Helper.GetResourceOrFileAsString(fName);
                if (verbose.Value)
                  messages.Add("Read: {0} -> {1}".FormatString(fName, contents.Length));
                //sb.AppendLine(Utility.FileReadVirtual(fName));
                sb.AppendLine(contents);
                sb.AppendLine("/**/");
              }
              else
              {
                string contents = EmbeddedResourcesVPP_Helper.GetResourceOrFileAsString(fName);
                if (verbose.Value)
                  messages.Add("Read: {0} -> {1}".FormatString(fName, contents.Length));
                //sb_nc.AppendLine(Utility.FileReadVirtual(fName));
                sb_nc.AppendLine(contents);
                sb_nc.AppendLine("/**/");
              }
            }
          }
          string source = sb.ToString();
          string minified = null;
          switch (type.ToLower())
          {
            case "css":
              if (css.GetValueOrDefault(true))
              {
                var compressor = new Yahoo.Yui.Compressor.CssCompressor() { CompressionType = Yahoo.Yui.Compressor.CompressionType.Standard, LineBreakPosition = 1023, RemoveComments = true };
                minified = compressor.Compress(source);
                //minified = Yahoo.Yui.Compressor.CssCompressor.Compress(source, 1023, Yahoo.Yui.Compressor.CompressionType.CssCompressionType.Hybrid);
              }
              break;
            case "js":
            case "javascript":
              if (js.GetValueOrDefault(true))
              {
                var compressor = new Yahoo.Yui.Compressor.JavaScriptCompressor()
                {
                  CompressionType = Yahoo.Yui.Compressor.CompressionType.Standard,
                  DisableOptimizations = false,
                  Encoding = Encoding.UTF8,
                  LineBreakPosition = 1023,
                  ObfuscateJavascript = false,
                  PreserveAllSemicolons = false,
                  IgnoreEval = false,
                  ThreadCulture = System.Globalization.CultureInfo.InvariantCulture
                };
                minified = compressor.Compress(source);
                //minified = Yahoo.Yui.Compressor.JavaScriptCompressor.Compress(source, false, true, false, false, 1023, Encoding.UTF8, System.Globalization.CultureInfo.InvariantCulture);
              }
              break;
          }
          if (Utility.TryParse<bool>(HttpContext.Current.Request.QueryString["compress"], true) == false)
          {
            minified = source;
          }
          System.IO.File.WriteAllText(Utility.vPathMap(fileName), Utility.Implode(new string[] { minified, sb_nc.ToString() }, "\n", null, true, true));
          messages.Add(string.Format("CMS minimization [{3}]: initial size={0} compressed size={1} ratio={2:f3}<br/>\n", source.Length, minified.Length, minified.Length / (double)source.Length, fileName));
        }
        //
        if (less.GetValueOrDefault(true))
        {
          foreach (var xFile in xFiles.Elements("DotLess"))
          {
            foreach (XElement xF in xFile.Elements("Compile").Where(x => !string.IsNullOrEmpty(x.AttributeValue("filename"))).Distinct((x1, x2) => x1.AttributeValue("filename") == x2.AttributeValue("filename")))
            {
              string fNameIn = xF.AttributeValue("filename").TrimSafe();
              fNameIn = Utility.ResolveUrl(fNameIn);
              int idx = fNameIn.IndexOf(".less", StringComparison.OrdinalIgnoreCase);
              if (idx <= 0)
                continue;
              string fNameOut = fNameIn.Substring(0, idx) + ".css";
              string source = EmbeddedResourcesVPP_Helper.GetResourceOrFileAsString(fNameIn);
              //
              try
              {
                using (WebClient client = new WebClient())
                {
                  client.Encoding = System.Text.Encoding.UTF8;
                  Uri url = new Uri(HttpContext.Current.Request.Url, fNameIn);
                  url = Utility.UriSetQuery(url, "LessHash", Guid.NewGuid().ToString());  // deve essere configurato assieme ai parametri nel web.config: sessionMode="QueryParam" sessionQueryParamName="LessHash"
                  string response = client.DownloadString(url);
                  File.WriteAllText(Utility.vPathMap(fNameOut), response);
                  messages.Add("LESS compiler: generated {0} from {1} size={2}".FormatString(fNameOut, fNameIn, response.Length));
                }
              }
              catch (Exception ex)
              {
                messages.Add("Exception: generation of file {0} from {1} failed with: {2}".FormatString(fNameOut, fNameIn, ex.Message));
              }
              //
              try
              {
                //dotless.Core.configuration.DotlessConfiguration cfg = new dotless.Core.configuration.WebConfigConfigurationLoader().GetConfiguration();
                //dotless.Core.AspNetContainerFactory containerFactory = new dotless.Core.AspNetContainerFactory();
                ////containerFactory.GetContainer(cfg).Container.GetInstance<dotless.Core.HandlerImpl>().Execute();
                ////
                //cfg.CacheEnabled = false;
                //cfg.Debug = true;
                //cfg.DisableUrlRewriting = false;
                //cfg.HandleWebCompression = false;
                //cfg.ImportAllFilesAsLess = true;
                //cfg.InlineCssFiles = false;
                //cfg.LessSource = typeof(dotless.Core.Input.VirtualFileReader);
                //cfg.MinifyOutput = false;
                ////cfg.Optimization = 0;
                ////cfg.SessionMode = dotless.Core.configuration.DotlessSessionStateMode.Disabled;
                //cfg.Web = true;
                ////
                //dotless.Core.EngineFactory engineFactory = new dotless.Core.EngineFactory(cfg);
                //dotless.Core.ILessEngine engine2 = engineFactory.GetEngine(containerFactory);
                //var tmp01b = engine2.GetImports().ToList();
                //var tmp02b = engine2.TransformToCss(source, fNameIn);
                ////importAllFilesAsLess
                //// .less format CSS as string in 'input'.
                //var dotLessEngine = new dotless.Core.EngineFactory().GetEngine(
                //  //(.engine.ExtensibleEngineImpl(input);
                //// Transformed CSS now in 'css'.
                //var css = dotLessEngine.Css;
                //// Now minify...
                //var dotlessMinifier = new dotless.Core.minifier.Processor(css);
                //// Output CSS suitable for sending to client in 'output'.
                //var output = new string(dotlessMinifier.Output);
              }
              catch { }

            }
          }
        }
        //
        messages.Add("Operazione completata con successo.");
      }
      catch (Exception ex)
      {
        messages.Add(ex.Message);
      }
      return messages;
    }


  }
}
