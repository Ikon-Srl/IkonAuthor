/*
 * 
 * Intranet Ikon
 * 
 * Copyright (C) 2008 Ikon Srl
 * Tutti i Diritti Riservati. All Rights Reserved
 * 
 */


using System;
using System.Data;
using System.Configuration;
using System.Collections;
using System.Web;

using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Net.Configuration;
using System.Collections.Generic;
using System.Xml;
using System.Xml.Linq;
using System.Linq;
using System.Linq.Expressions;
using LinqKit;



namespace Ikon.Filters
{
  using TidyNet;


  //
  // classe per il filtraggio e sanitizzazione dell'html basata su HtmlTidy.NET e LINQ2XML
  //
  public class IKGD_HtmlCleaner
  {
    public Tidy tidy;
    public TidyMessageCollection messages;  // log di parsing
    public string HtmlParsedFull { get; set; }   // html completo di headers come fornito da Tidy
    public string HtmlParsed { get; set; }   // estrazione dell'html originale sanitizzato estratto dal body di HtmlParsed
    public XElement xHtml { get; set; }   // xElement con il <body> sanitizzato estratto da HtmlParsedFull
    //
    public bool optionReturnInputOnError { get; set; }   // flag ritornare l'html originale in caso di errore di parsing
    //
    public object _lock = new object();
    //


    public IKGD_HtmlCleaner()
    {
      Reset();
      optionReturnInputOnError = false;
    }

    public IKGD_HtmlCleaner(string rawHtml)
      : this()
    {
      HtmlParsed = Parse(rawHtml);
    }


    public static IKGD_HtmlCleaner Factory()
    {
      return new IKGD_HtmlCleaner();
    }


    public static string SanitizerXSS(string input)
    {
      //string output = Microsoft.Security.Application.Sanitizer.GetSafeHtml(input);
      string output = Microsoft.Security.Application.Sanitizer.GetSafeHtmlFragment(input);
      return output;
    }


    public void Reset()
    {
      lock (_lock)
      {
        try
        {
          //
          tidy = new Tidy();
          messages = new TidyMessageCollection();
          //
          tidy.Options.DocType = DocType.Omit;  // per evitare i namespaces
          //tidy.Options.DropFontTags = true;
          //tidy.Options.LogicalEmphasis = true;
          tidy.Options.Xhtml = true;
          tidy.Options.XmlOut = true;
          tidy.Options.MakeClean = true;
          tidy.Options.TidyMark = false;
          tidy.Options.NumEntities = true;
          tidy.Options.QuoteNbsp = false;
          tidy.Options.XmlPi = true;
          tidy.Options.AltText = "_";
          tidy.Options.BreakBeforeBR = false;
          tidy.Options.CharEncoding = CharEncoding.UTF8;
          tidy.Options.FixComments = true;
          tidy.Options.LiteralAttribs = true;
          tidy.Options.SmartIndent = true;
          tidy.Options.Spaces = 0;
          tidy.Options.TabSize = 8;
          tidy.Options.WrapLen = 10240;
          tidy.Options.WrapSection = false;
          tidy.Options.DropEmptyParas = false;
          //
          tidy.Options.Word2000 = true;
          //
          // NB:
          // attenzione che il TidyNet ha delle difficolta' nel processare gli iframe senza contenuto di default
          // bisogna per forza utilizzare una forma del tipo:
          // <iframe src="http://www.ikon.it/">noframe</iframe>
          // con un testo, qualsiasi tentativo di usare spazi o &nbsp; o la versione <iframe ... />
          // poi lo trasforma in <iframe></iframe> e  al salvataggio successivo lo elimina
          //
        }
        catch { }
      }
    }


    public string ParseAndTruncate(string rawHtml, int maxLength) { return ParseAndTruncate(rawHtml, maxLength, true, "..."); }
    public string ParseAndTruncate(string rawHtml, int maxLength, bool wholeWords, string ellipsis)
    {
      lock (_lock)
      {
        Parse(rawHtml);
        return Truncate(maxLength, wholeWords, ellipsis);
      }
    }


    public string Parse(string rawHtml) { return Parse(rawHtml, true, false); }
    public string Parse(string rawHtml, bool filterScriptTag, bool throwExceptions)
    {
      lock (_lock)
      {
        try
        {
          HtmlParsed = HtmlParsedFull = string.Empty;
          xHtml = new XElement("body");
          messages.Clear();
          try
          {
            using (MemoryStream input = new MemoryStream(Encoding.UTF8.GetBytes(rawHtml)))
            {
              using (MemoryStream output = new MemoryStream())
              {
                tidy.Parse(input, output, messages);
                HtmlParsedFull = Encoding.UTF8.GetString(output.ToArray());
              }
            }
            if (messages != null && messages.Errors > 0 && string.IsNullOrEmpty(HtmlParsedFull))
            {
              if (optionReturnInputOnError)
                HtmlParsedFull = rawHtml;
            }
          }
          catch
          {
            if (throwExceptions)
              throw;
          }
          XElement xDoc = XElement.Parse(HtmlParsedFull, LoadOptions.PreserveWhitespace);
          if (filterScriptTag)
            xDoc.Descendants("script").Remove();
          //
          xHtml = xDoc.Elements().FirstOrDefault(x => x.Name.LocalName.ToLower() == "body") ?? new XElement("body");
          //
          HtmlParsed = xHtmlToString(xHtml);
        }
        catch
        {
          if (throwExceptions)
            throw;
        }
        finally
        {
          if (messages != null && messages.Errors > 0)
            Reset();
        }
      }
      return HtmlParsed;
    }


    //
    // ritrasforma in stringa un tag <body> senza il root element
    //
    public string xHtmlToString(XElement xElem)
    {
      StringBuilder sb = new StringBuilder();
      try
      {
        foreach (XNode xN in xElem.Nodes())
          sb.Append(xN.ToString(SaveOptions.DisableFormatting));
      }
      catch { }
      return sb.ToString();
    }


    //
    // funzione per filtrare un html gia' sanitizzato, fornendo le liste dei tag ammessi o vietati
    // se una lista e' null non viene applicato il relativo filtro
    //
    public string Filter(IEnumerable<string> allowedTags, IEnumerable<string> deniedTags)
    {
      Expression<Func<XElement, bool>> xFilterDel = PredicateBuilder.True<XElement>();
      if (allowedTags != null)
        xFilterDel = xFilterDel.And(x => !allowedTags.Contains(x.Name.LocalName));
      if (deniedTags != null)
        xFilterDel = xFilterDel.And(x => deniedTags.Contains(x.Name.LocalName));
      xHtml.Descendants().Where(xFilterDel.Compile()).Remove();
      //
      return xHtmlToString(xHtml);
    }

    public string Filter(IEnumerable<string> allowedTags, IEnumerable<string> deniedTags, IEnumerable<string> allowedAttrs, IEnumerable<string> deniedAttrs)
    {
      Expression<Func<XElement, bool>> xFilterDel = PredicateBuilder.True<XElement>();
      if (allowedTags != null)
        xFilterDel = xFilterDel.And(x => !allowedTags.Contains(x.Name.LocalName));
      if (deniedTags != null)
        xFilterDel = xFilterDel.And(x => deniedTags.Contains(x.Name.LocalName));
      xHtml.Descendants().Where(xFilterDel.Compile()).Remove();
      //
      if (allowedAttrs != null || deniedAttrs != null)
      {
        foreach (XElement xE in xHtml.Descendants().Where(x => x.HasAttributes))
        {
          if (deniedAttrs != null)
            xE.Attributes().Where(a => deniedAttrs.Contains(a.Name.LocalName, StringComparer.CurrentCultureIgnoreCase)).Remove();
          if (allowedAttrs != null)
            xE.Attributes().Where(a => !allowedAttrs.Contains(a.Name.LocalName, StringComparer.CurrentCultureIgnoreCase)).Remove();
        }
      }
      //
      return xHtmlToString(xHtml);
    }


    //
    // estrazione dei puri testi contenuti nell'html: utile per i search engine o per un filtraggio totale
    //
    public string Text() { return Text(null); }
    public string Text(string rawHtml)
    {
      lock (_lock)
      {
        if (rawHtml != null)
          Parse(rawHtml);
        StringBuilder sb = new StringBuilder();
        try
        {
          foreach (XNode node in xHtml.DescendantNodes().Where(n => n.NodeType == XmlNodeType.Text || n.NodeType == XmlNodeType.CDATA))
          {
            string text = (node.NodeType == XmlNodeType.Text) ? (node as XText).Value : (node as XCData).Value;
            sb.Append(text.Trim(' ', '\n', '\r', '\t'));
            sb.Append(" ");
          }
        }
        catch { }
        return sb.ToString();
      }
    }

    //
    // estrazione dei puri testi contenuti nell'html: rimuove il tag <br />
    //
    public string TextNoBR() { return Text(null); }
    public string TextNoBR(string rawHtml)
    {
      lock (_lock)
      {
        if (rawHtml != null)
          Parse(rawHtml);
        StringBuilder sb = new StringBuilder();
        try
        {
          foreach (XNode node in xHtml.DescendantNodes().Where(n => n.NodeType == XmlNodeType.Text || n.NodeType == XmlNodeType.CDATA))
          {
            string text = (node.NodeType == XmlNodeType.Text) ? (node as XText).Value : (node as XCData).Value;
            text = text.ToLower().Replace("<br />", "");
            sb.Append(text.Trim(' ', '\n', '\r', '\t'));
          }
        }
        catch { }
        return sb.ToString();
      }
    }


    //
    // troncamento dell'html con chiusura corretta dei tag non ancora chiusi al raggiungimento del limite di caratteri impostato
    //
    public string Truncate(int maxLength, bool wholeWords, string ellipsis)
    {
      int counter = 0;
      ellipsis = ellipsis ?? string.Empty;
      XElement xElem = new XElement(xHtml);
      foreach (XNode node in xElem.DescendantNodes().Where(n => n.NodeType == XmlNodeType.Text || n.NodeType == XmlNodeType.CDATA))
      {
        string text = (node.NodeType == XmlNodeType.Text) ? (node as XText).Value : (node as XCData).Value;
        if ((counter += text.Length) < maxLength)
          continue;
        int allowedLength = Math.Max(maxLength - (counter - text.Length), ellipsis.Length);
        text = TruncateSimple(text, allowedLength, wholeWords, ellipsis);
        //
        node.NodesAfterSelf().Remove();
        foreach (XNode xn in node.Ancestors())
          xn.NodesAfterSelf().Remove();
        //
        node.ReplaceWith(new XText(text));
        break;
      }
      return xHtmlToString(xElem);
    }


    public static string TruncateSimple(string str, int maxLength, bool wholeWords, string ellipsis) { return TruncateSimple(str, maxLength, wholeWords, ellipsis, Utility.CharNbsp + " \n\r\t;"); }
    public static string TruncateSimple(string str, int maxLength, bool wholeWords, string ellipsis, string seps)
    {
      if (maxLength < 0)
        return str;
      if (str.Length <= maxLength)
        return str;
      ellipsis = ellipsis ?? string.Empty;
      maxLength -= ellipsis.Length;
      if (!wholeWords)
        if (str.Length > maxLength)
          return str.Substring(0, maxLength) + ellipsis;
      if (str.Length > maxLength)
        str = str.Substring(0, maxLength + 1);
      int off = str.LastIndexOfAny(seps.ToCharArray());
      if (off == -1)
        return ellipsis;
      return str.Substring(0, off + 1) + ellipsis;
      //return str.Substring(0, off + 1).Trim() + strEnding;
    }


  }



  //
  // classe obsoleta sostituita da IKGD_HtmlCleaner
  //
  public class CMS_HtmlCleaner
  {
    private StringBuilder output_buffer;
    private HtmlWriter htmlWriter;
    public enum stripMode { None, NoHtml, HtmlMinimal, HtmlTables, HtmlAll };

    public CMS_HtmlCleaner() : this(stripMode.HtmlAll, string.Empty, string.Empty) { }
    public CMS_HtmlCleaner(stripMode mode) : this(mode, string.Empty, string.Empty) { }
    public CMS_HtmlCleaner(stripMode mode, string html_tags, string html_attrs)
    {
      List<string> tags = new List<string>();
      List<string> attrs = new List<string>();
      tags.Add(html_tags);
      attrs.Add(html_attrs);
      output_buffer = new StringBuilder();
      htmlWriter = new HtmlWriter(output_buffer);
      FilterOutput = true;
      switch (mode)
      {
        case stripMode.HtmlAll:
          FilterOutput = false;
          break;
        case stripMode.HtmlTables:
          tags.Add("b,i,u,em,div,img,span,pre,br,hr,table,tr,td,ul,ol,li,strong,a,dd,dt");
          attrs.Add("href,target,src");
          break;
        case stripMode.HtmlMinimal:
          tags.Add("b,i,u,br,hr,p,strong,a");
          attrs.Add("href,target,src");
          break;
        case stripMode.NoHtml:
          htmlWriter.NoHtml = true;
          break;
      }
      html_tags = Utility.Implode(tags, ",");
      html_attrs = Utility.Implode(attrs, ",");
      Tags = Utility.Explode(html_tags, ",", " ", true).ToArray();
      Attributes = Utility.Explode(html_attrs, ",", " ", true).ToArray();
      //
      // stripMode.None non filtra gli attributi a meno che non siano specificati in Tags e Attributes
      //
      if (mode == stripMode.None && Tags.Length == 0 && Attributes.Length == 0)
      {
        FilterOutput = false;
        htmlWriter.NoHtml = false;
      }
    }


    public string Parse(string html_input) { return Parse(html_input, 0); }
    public string Parse(string html_input, int max_len)
    {
      if (string.IsNullOrEmpty(html_input.Trim()))
        return string.Empty;

      HtmlReader htmlReader = new HtmlReader(html_input);
      output_buffer.Remove(0, output_buffer.Length);

      htmlReader.Read();
      while (!htmlReader.EOF)
        htmlWriter.WriteNode(htmlReader, true);
      htmlWriter.Flush();

      string result = output_buffer.ToString();
      result = result.Replace("<" + htmlWriter.ReplacementTag + ">", "");
      result = result.Replace("</" + htmlWriter.ReplacementTag + ">", "");
      result = result.Replace("<" + htmlWriter.ReplacementTag + " />", "");
      result = result.Replace("<" + htmlWriter.ReplacementTag + "/>", "");

      if (max_len > 0 && result.Length > max_len)
        result = result.Substring(0, max_len);

      return result;
    }

    public static string TruncateSimple(string str, int maxLength, bool wholeWords, string strEnding) { return TruncateSimple(str, maxLength, wholeWords, strEnding, Utility.CharNbsp + " \n\r\t;"); }
    public static string TruncateSimple(string str, int maxLength, bool wholeWords, string strEnding, string seps)
    {
      if (maxLength < 0)
        return str;
      if (str.Length <= maxLength)
        return str;
      strEnding = strEnding ?? string.Empty;
      maxLength -= strEnding.Length;
      if (!wholeWords)
        if (str.Length > maxLength)
          return str.Substring(0, maxLength) + strEnding;
      if (str.Length > maxLength)
        str = str.Substring(0, maxLength + 1);
      int off = str.LastIndexOfAny(seps.ToCharArray());
      if (off == -1)
        return strEnding;
      return str.Substring(0, off + 1) + strEnding;
      //return str.Substring(0, off + 1).Trim() + strEnding;
    }

    public string Truncate(string html_input, int maxLength, bool fullParse, bool wholeWords, string strEnding)
    {
      try
      {
        string html_parsed = Parse(html_input, 0);
        strEnding = strEnding ?? string.Empty;
        //maxLength -= strEnding.Length;
        if (maxLength <= 0)
          return html_parsed;
        if (!fullParse)
          return TruncateSimple(html_parsed, maxLength, wholeWords, strEnding);
        XElement html = XElement.Parse("<html>" + html_parsed + "</html>");
        int count_base = 0;
        XNode xLast = null;
        string value_last = null;
        foreach (XNode n in html.DescendantNodes())
        {
          string value = null;
          value = (n is XText) ? ((XText)n).Value : value;
          value = (n is XElement) ? ((XElement)n).Value : value;
          if (value == null)
            continue;
          string value_orig = value;
          value = value.Trim((Utility.CharNbsp + " \n\r\t").ToCharArray());
          if (string.IsNullOrEmpty(value))
            continue;
          int allowedLen = maxLength - count_base;
          count_base += value.Length;
          if (count_base < maxLength)
            continue;
          value_last = TruncateSimple(value_orig, allowedLen, wholeWords, strEnding);
          xLast = n;
          break;
        }
        if (xLast != null)
        {
          List<XNode> nodes_all = new List<XNode>(html.DescendantNodes());
          for (int i = nodes_all.Count - 1; i >= 0; i--)
            if (nodes_all[i] != null && nodes_all[i].IsAfter(xLast))
              nodes_all[i].Remove();
          if (xLast is XText)
            ((XText)xLast).Value = value_last;
          if (xLast is XElement)
            ((XElement)xLast).Value = value_last;
        }
        StringBuilder sb = new StringBuilder();
        foreach (XNode xn in html.Nodes())
          sb.Append(xn.ToString());
        return sb.ToString();
      }
      catch
      {
        return string.Empty;
      }
    }


    public bool FilterOutput
    {
      get { return htmlWriter.FilterOutput; }
      set { htmlWriter.FilterOutput = value; }
    }

    public string[] Tags
    {
      get { return htmlWriter.Tags; }
      set { htmlWriter.Tags = value; }
    }

    public string[] Attributes
    {
      get { return htmlWriter.Attributes; }
      set { htmlWriter.Attributes = value; }
    }


    //
    //
    // nested classes
    //
    //

    /// <summary>
    /// This class skips all nodes which has some kind of prefix. This trick does the job 
    /// to clean up MS Word/Outlook HTML markups.
    /// </summary>
    private class HtmlReader : Sgml.SgmlReader
    {

      public HtmlReader(TextReader reader)
        : base()
      {
        base.InputStream = reader;
        base.DocType = "HTML";
      }
      public HtmlReader(string content)
        : base()
      {
        base.InputStream = new StringReader(content);
        base.DocType = "HTML";
      }
      public override bool Read()
      {
        bool status = base.Read();
        if (status)
        {

          switch (base.NodeType)
          {
            // skippa i tags di word
            case XmlNodeType.Element:
              if (base.Name.IndexOf(':') > 0)
                base.Skip();
              break;
          }
        }
        return status;
      }
    }

    /// <summary>
    /// Extends XmlTextWriter to provide Html writing feature which is not as strict as Xml
    /// writing. For example, Xml Writer encodes content passed to WriteString which encodes special markups like
    /// &nbsp to &amp;bsp. So, WriteString is bypassed by calling WriteRaw.
    /// </summary>
    private class HtmlWriter : XmlTextWriter
    {
      /// <summary>
      /// If set to true, it will filter the output by using tag and attribute filtering,
      /// space reduce etc
      /// </summary>
      public bool FilterOutput = false;
      public bool NoHtml = false;

      /// <summary>
      /// If true, it will reduce consecutive &nbsp; with one instance
      /// </summary>
      public bool ReduceConsecutiveSpace = true;

      /// <summary>
      /// Set the tag names in lower case which are allowed to go to output
      /// </summary>


      /// <summary>
      /// If any tag found which is not allowed, it is replaced by this tag.
      /// Specify a tag which has least impact on output
      /// </summary>
      public string ReplacementTag = "dd";

      /// <summary>
      /// New lines \r\n are replaced with space which saves space and makes the
      /// output compact
      /// </summary>
      public bool RemoveNewlines = true;
      /// <summary>
      /// Specify which attributes are allowed. Any other attribute will be discarded
      /// </summary>
      /// 

      private string[] AllowedAttributes = new string[] { "class", "href", "target", 
			"border", "src", "align", "width", "height", "color", "size" };

      private string[] AllowedTags = new string[] { "p", "b", "i", "u", "em", "big", "small", 
			"div", "img", "span", "blockquote", "code", "pre", "br", "hr", "table", "tr", "td",
			"ul", "ol", "li", "del", "ins", "strong", "a", "font", "dd", "dt"};

      private Dictionary<string, string> SubstituteTags = new Dictionary<string, string>();



      public void AddReplaceTags(string key, string value)
      {
        SubstituteTags.Add(key, value);
      }

      public void DelReplaceTags(string key)
      {
        SubstituteTags.Remove(key);
      }

      public void ClearReplaceTags()
      {
        SubstituteTags.Clear();
      }

      public string[] Tags
      {
        get { return AllowedTags; }
        set { AllowedTags = value; }
      }

      public string[] Attributes
      {
        get { return AllowedAttributes; }
        set { AllowedAttributes = value; }
      }


      public HtmlWriter(TextWriter writer)
        : base(writer)
      {
      }
      public HtmlWriter(StringBuilder builder)
        : base(new StringWriter(builder))
      {
      }
      public HtmlWriter(Stream stream, Encoding enc)
        : base(stream, enc)
      {
      }

      public override void WriteString(string text)
      {
        if (NoHtml)
        {
          text += " ";
        }

        base.WriteString(text);
      }

      /*
      /// <summary>
      /// The reason why we are overriding this method is, we do not want the output to be
      /// encoded for texts inside attribute and inside node elements. For example, all the &nbsp;
      /// gets converted to &amp;nbsp in output. But this does not 
      /// apply to HTML. In HTML, we need to have &nbsp; as it is.
      /// </summary>
      /// <param name="text"></param>
      public override void WriteString(string text)
      {
        // Change all non-breaking space to normal space
        text = text.Replace(" ", "&nbsp;");
        /// When you are reading RSS feed and writing Html, this line helps remove
        /// those CDATA tags
        text = text.Replace("<![CDATA[", "");
        text = text.Replace("]]>", "");

        // Do some encoding of our own because we are going to use WriteRaw which won't
        // do any of the necessary encoding
         
        text = text.Replace("<", "&lt;");
        text = text.Replace(">", "&gt;");
        text = text.Replace("'", "&apos;");
        text = text.Replace("\"", "&quote;");
          

        if (this.FilterOutput)
        {
          text = text.Trim();

          // We want to replace consecutive spaces to one space in order to save horizontal
          // width
          if (this.ReduceConsecutiveSpace) text = text.Replace("&nbsp;&nbsp;&nbsp;", "&nbsp;");

          if (this.RemoveNewlines) text = text.Replace(Environment.NewLine, " ");

          base.WriteRaw(text);
        }
        else
        {
          base.WriteRaw(text);
        }
      }
      */

      public override void WriteWhitespace(string ws)
      {
        if (!this.FilterOutput) base.WriteWhitespace(ws);
      }

      public override void WriteComment(string text)
      {
        if (!this.FilterOutput) base.WriteComment(text);
      }

      /// <summary>
      /// This method is overriden to filter out tags which are not allowed
      /// </summary>
      public override void WriteStartElement(string prefix, string localName, string ns)
      {
        /*
         * MM_
         * prima procedo a sostituire il tags se presente nel dizionario delle sostituzioni
         * poi controllo se il filtro e' attivato ed eventualmente se il tag sostituito non
         * e' abilitato taglio il testo
         * 
         * ho preferito mantenere la logica di far prevalere il troncamento sul rimpiazzo
         */

        // se devo strippare l'html rimpiazzo tutto con il tag replacementtag che verra' poi rimosso
        if (this.NoHtml)
        {
          this.FilterOutput = true;
          localName = this.ReplacementTag;
        }
        else
        {
          foreach (KeyValuePair<string, string> kv in SubstituteTags)
          {
            if (localName.ToLower() == kv.Key.ToLower())
            {
              localName = kv.Value.ToLower();
              break;
            }
          }

          if (this.FilterOutput)
          {
            bool canWrite = false;
            string tagLocalName = localName.ToLower();
            foreach (string name in this.AllowedTags)
            {
              if (name == tagLocalName)
              {
                canWrite = true;
                break;
              }
            }

            if (!canWrite)
              localName = this.ReplacementTag;
          }
          else
          {
            if (localName.ToLower() == "html")
              localName = this.ReplacementTag;
          }
        }



        base.WriteStartElement(prefix, localName, ns);
      }

      private bool isAllowedElement(string element)
      {
        foreach (string ele in AllowedTags)
          if (string.Equals(ele, element, StringComparison.OrdinalIgnoreCase))
            return true;

        return false;
      }

      /// <summary>
      /// This method is overriden to filter out attributes which are not allowed
      /// </summary>
      public override void WriteAttributes(XmlReader reader, bool defattr)
      {
        if (this.FilterOutput)
        {
          // MM_ filtro attributi per tags non ammessi
          // in modo da cancellare il tags dopo con un replace
          // ed evitare cose del tipo <nop attr="">
          if (this.NoHtml || (reader.NodeType == XmlNodeType.Element &&
            isAllowedElement(reader.LocalName) == false))
          {
            reader.MoveToContent();
            return;
          }


          // The following code is copied from implementation of XmlWriter's
          // WriteAttributes method. 
          if (reader == null)
          {
            throw new ArgumentNullException("reader");
          }
          if ((reader.NodeType == XmlNodeType.Element) || (reader.NodeType == XmlNodeType.XmlDeclaration))
          {
            if (reader.MoveToFirstAttribute())
            {
              this.WriteAttributes(reader, defattr);
              reader.MoveToElement();
            }
          }
          else
          {
            if (reader.NodeType != XmlNodeType.Attribute)
            {
              throw new XmlException("Xml_InvalidPosition");
            }
            do
            {
              if (defattr || !reader.IsDefault)
              {
                // Check if the attribute is allowed 
                bool canWrite = false;
                string attributeLocalName = reader.LocalName.ToLower();
                foreach (string name in this.AllowedAttributes)
                {
                  if (name == attributeLocalName)
                  {
                    canWrite = true;
                    break;
                  }
                }

                // If allowed, write the attribute
                if (canWrite)
                  this.WriteStartAttribute(reader.Prefix, attributeLocalName,
                    reader.NamespaceURI);

                while (reader.ReadAttributeValue())
                {
                  if (reader.NodeType == XmlNodeType.EntityReference)
                  {
                    if (canWrite) this.WriteEntityRef(reader.Name);
                    continue;
                  }
                  if (canWrite) this.WriteString(reader.Value);
                }
                if (canWrite) this.WriteEndAttribute();
              }
            } while (reader.MoveToNextAttribute());
          }
        }
        else
        {
          base.WriteAttributes(reader, defattr);
        }
      }
    }

  }


}  // Ikon.Filters