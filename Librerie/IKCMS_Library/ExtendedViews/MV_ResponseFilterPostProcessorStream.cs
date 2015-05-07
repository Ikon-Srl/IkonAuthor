/*
 * 
 * Copyright (C) 2012 Ikon Srl
 * Tutti i Diritti Riservati. All Rights Reserved
 * 
 */


using System;
using System.Data;
using System.Configuration;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Web;
using System.Web.UI;
using System.Web.Security;
using System.Xml.Linq;
using System.Data.Linq;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
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
using Autofac;
using HtmlAgilityPack;

using Ikon;
using Ikon.GD;
using Ikon.Log;


namespace Ikon.IKCMS
{

  //
  // TODO: aggiungere il processing degli attributi vuoti nell'html
  // nell'html aggiungere il processing automatico dei tags per aggiungere la query string di versioning del file senza intervenire sul codice
  // <link + href="~/
  // <script + src="~/
  //
  public class MV_ResponseFilterPostProcessorStream : ResponseFilterStream
  {
    // sostituzione dei pattern tipo ...='~/path o ...="~/path o url(~/path
    // TODO: provare a vedere di indagare delle regex piu' performanti
    private static Regex rx_virtualpath = new Regex("(?<pre>(=\\s*('|\")|url\\s*\\(\\s*))~/", RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.Multiline);
    private static string path_subst = "${pre}" + HttpContext.Current.Request.ApplicationPath.TrimEnd('/') + "/";  // integrazione del prematch del vpath con il path espanso
    protected HttpResponseBase httpResponse { get; set; }

    public MV_ResponseFilterPostProcessorStream(HttpResponseBase httpResponse)
      : base(httpResponse.Filter)
    {
      this.httpResponse = httpResponse;
      this.TransformStream += filter_TransformStream;
      this.TransformStream += filter_TransformStreamHtml;
    }


    MemoryStream filter_TransformStream(MemoryStream ms)
    {
      if (!string.Equals(httpResponse.ContentType, "text/html", StringComparison.OrdinalIgnoreCase))
        return ms;
      Encoding encoding = httpResponse.ContentEncoding;
      MemoryStream ms_out = new MemoryStream(encoding.GetBytes(FixPaths(encoding.GetString(ms.ToArray()))));
      ms.Dispose();
      return ms_out;  // non usare using altrimenti non puo' continuare il postprocessing dopo il Dispose
    }


    MemoryStream filter_TransformStreamHtml(MemoryStream ms)
    {
      if (!string.Equals(httpResponse.ContentType, "text/html", StringComparison.OrdinalIgnoreCase))
        return ms;
      //
      Encoding encoding = httpResponse.ContentEncoding;
      var str01 = encoding.GetString(ms.ToArray());
      //
      HtmlDocument htmlDoc = new HtmlDocument();
      //
      htmlDoc.OptionAutoCloseOnEnd = true;
      htmlDoc.OptionFixNestedTags = true;
      htmlDoc.OptionOutputAsXml = true;
      htmlDoc.OptionOutputOriginalCase = false;
      //
      htmlDoc.LoadHtml(str01);
      //htmlDoc.Load(ms);  // non funziona verificare la causa (posizione seek, encoding, ...)
      //
      if (htmlDoc.ParseErrors != null && htmlDoc.ParseErrors.Any())
      {
        // Handle any parse errors as required
      }
      if (htmlDoc.DocumentNode != null)
      {
        var tmp01 = htmlDoc.DocumentNode.SelectNodes("//link[@href]");
        var tmp02 = htmlDoc.DocumentNode.SelectNodes("//script[@src]");
        var tmp03 = htmlDoc.DocumentNode.SelectNodes("//a[@href]");
      }

      //
      // http://www.4guysfromrolla.com/articles/011211-1.aspx
      // http://www.fairnet.com/post/2010/08/28/Html-screen-scraping-with-HtmlAgilityPack-Library.aspx
      // http://stackoverflow.com/questions/846994/how-to-use-html-agility-pack
      //

      //doc.Load("file.htm");
      //foreach(HtmlNode link in doc.DocumentElement.SelectNodes("//a[@href"]))
      //{
      //   HtmlAttribute att = link["href"];
      //   //att.Value = FixLink(att);
      //}
      //doc.Save("file.htm");

      return ms;
    }

    private string FixPaths(string output)
    {
      return rx_virtualpath.Replace(output, path_subst);
      //Stopwatch sw = Stopwatch.StartNew();
      //output = rx_virtualpath.Replace(output, path_subst);
      //sw.Stop();
      //var dt1 = sw.ElapsedTicks;
      //var dt2 = sw.Elapsed.TotalMilliseconds;
      //return output;
    }

  }




  public class MV_ResponseFilterPostProcessorStream_02 : MemoryStream
  {
    private readonly Stream _response;
    
    public MV_ResponseFilterPostProcessorStream_02(Stream response)
    {
      _response = response;
    }

    public override void Write(byte[] buffer, int offset, int count)
    {
      var html = Encoding.UTF8.GetString(buffer);
      html = ReplaceTags(html);
      buffer = Encoding.UTF8.GetBytes(html);
      _response.Write(buffer, offset, buffer.Length);
    }

    private string ReplaceTags(string html)
    {
      // TODO: go ahead and implement the filtering logic
      throw new NotImplementedException();
    }
  }




}

