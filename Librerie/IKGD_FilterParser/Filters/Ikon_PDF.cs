/*
 * 
 * Intranet Ikon
 * 
 * Copyright (C) 2009 Ikon Srl
 * Tutti i Diritti Riservati. All Rights Reserved
 * 
 */


using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Net.Configuration;
using System.Xml;
using System.Xml.Linq;
using System.Reflection;

using iTextSharp.text;
using iTextSharp.text.pdf;
using iTextSharp.text.pdf.parser;
//using ExpertPdf;


//
//funzionalità di supporto per la gestione dei PDF, si tratta della lettura di flag e settings dai PDF
//e dei filtri per l'estrazione del testo dai PDF
//ci appoggiamo a 2 librerie:
//  pdftotext.dll    per l'estrazione dei contenuti testuali
//  itextsharp.dll   per la manipolazione dei pdf
//in alternativa si può utilizzare pdfbox, ma si tratta di una libreria java che ha bisogno di IKVM
//

namespace Ikon.Filters
{
  public class IKGD_SupportPDF
  {
    public static Dictionary<string, string> GetInfoPDF(string fileName) { return GetInfoPDF(fileName, null); }
    public static Dictionary<string, string> GetInfoPDF(string fileName, string password)
    {
      try
      {
        using (FileStream fstream = new FileStream(fileName, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
          return GetInfoPDF(fstream, password);
      }
      catch { }
      return null;
    }
    public static Dictionary<string, string> GetInfoPDF(Stream stream, string password)
    {
      PdfReader pdfReader = null;
      try
      {
        pdfReader = new PdfReader(stream);
        return pdfReader.Info.OfType<DictionaryEntry>().ToDictionary(r => (string)r.Key, r => (string)r.Value);
      }
      catch { return null; }
      finally
      {
        if (pdfReader != null)
          pdfReader.Close();
      }
    }


    //
    // password: opzionale
    // pageStart, pageEnd: se specificati sono 1-based
    // permette anche di accedere ai files criptati
    //
    public static string ExtractTextV2(byte[] bytes, int? pageStart, int? pageEnd, string password)
    {
      StringBuilder sb = new StringBuilder();
      PdfReader reader = null;
      try
      {
        ITextExtractionStrategy its = new iTextSharp.text.pdf.parser.LocationTextExtractionStrategy();
        if (password != null)
        {
          reader = new PdfReader(bytes, Encoding.Default.GetBytes(password));
        }
        else
        {
          reader = new PdfReader(bytes);
        }
        int p1 = Math.Max(pageStart.GetValueOrDefault(1), 1);
        int p2 = Math.Max(pageStart.GetValueOrDefault(reader.NumberOfPages), reader.NumberOfPages);
        for (int p = p1; p <= p2; p++)
        {
          sb.AppendLine(PdfTextExtractor.GetTextFromPage(reader, p, its));
        }
      }
      catch { }
      finally
      {
        if (reader != null)
          reader.Close();
      }
      return sb.ToString();
    }

    public static string ExtractTextV2(Stream stream, int? pageStart, int? pageEnd, string password)
    {
      StringBuilder sb = new StringBuilder();
      PdfReader reader = null;
      try
      {
        ITextExtractionStrategy its = new iTextSharp.text.pdf.parser.LocationTextExtractionStrategy();
        reader = new PdfReader(stream, (password == null ? null : Encoding.Default.GetBytes(password)));
        int p1 = Math.Max(pageStart.GetValueOrDefault(1), 1);
        int p2 = Math.Max(pageStart.GetValueOrDefault(reader.NumberOfPages), reader.NumberOfPages);
        for (int p = p1; p <= p2; p++)
          sb.AppendLine(PdfTextExtractor.GetTextFromPage(reader, p, its));
      }
      catch { }
      finally
      {
        if (reader != null)
          reader.Close();
      }
      return sb.ToString();
    }

    public static string ExtractTextV2(string fileName) { return ExtractTextV2(fileName, null); }
    public static string ExtractTextV2(string fileName, string password) { return ExtractTextV2(fileName, null, null, password); }
    public static string ExtractTextV2(string fileName, int? pageStart, int? pageEnd, string password)
    {
      try
      {
        using (FileStream fstream = new FileStream(fileName, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
          return ExtractTextV2(fstream, pageStart, pageEnd, password);
      }
      catch { }
      return string.Empty;
    }


  }

}
