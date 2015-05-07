/*
 * 
 * Intranet Ikon
 * 
 * Copyright (C) 2008 Ikon Srl
 * Tutti i Diritti Riservati. All Rights Reserved
 * 
 */


using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Configuration;
using System.ComponentModel;
using System.Data;
using System.Data.Common;
using System.Data.SqlClient;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Security;
using System.Security.Principal;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Web;
using System.Web.Configuration;
using System.Web.Hosting;
using System.Web.Script.Serialization;
using System.Web.Security;
using System.Web.UI;
using System.Web.UI.HtmlControls;
using System.Web.UI.WebControls;
using System.Xml;
using System.Xml.Linq;
using System.Xml.Serialization;
using System.Xml.XPath;
using System.Xml.Xsl;
using System.Net;
using System.IO.Compression;
using System.Data.Linq.Mapping;
using System.Runtime.InteropServices;
using System.Runtime.Serialization;
using LinqKit;

using Ikon.Log;
using Ikon.Support;
using System.Diagnostics;


namespace Ikon
{
  public static class Utility
  {
    public static readonly string PersistantConnectionBaseName = "PersistantConnectionBaseName_";
    //
    public static readonly DateTime DateTimeMinValueDB;
    public static readonly DateTime DateTimeMaxValueDB;
    private static MethodInfo TryParseMI;
    private static MethodInfo TryParseMI2;
    //

    static Utility()
    {
      DateTimeMinValueDB = DateTime.Parse("1753-01-01");
      DateTimeMaxValueDB = DateTime.Parse("9999-12-31");
      //
      //ConnectionStringSettings connString = (ConnectionStringSettings)ConfigurationManager.GetSection("connectionStrings/connection_profile");
      //String testo= Profile.subsection2.key_02;
      //String config_001_value= ConfigurationManager.AppSettings.Get("config_001");
      //String config_002_value= IKGD_Config.AppSettings["config_002"];
      //
      TryParseMI = typeof(Utility).GetMethod("TryParse", new Type[] { typeof(object) });
      TryParseMI2 = typeof(Utility).GetMethod("TryParse", new Type[] { typeof(object), typeof(object) });
    }


    public static bool IsNullOrEmpty(this string str) { return string.IsNullOrEmpty(str); }
    public static bool IsNullOrWhiteSpace(this string str) { return string.IsNullOrEmpty(str) || str.ToCharArray().All(c => char.IsWhiteSpace(c)); }
    public static bool IsNotEmpty(this string str) { return !string.IsNullOrEmpty(str); }
    public static bool IsNotNullOrWhiteSpace(this string str) { return !string.IsNullOrEmpty(str) && str.ToCharArray().Any(c => !char.IsWhiteSpace(c)); }


    //
    // test se una stringa e' composta solo da blanks html
    //
    private static Regex _MatchBlanksHtml = new Regex(@"<(br|p).*?>|</p>|\s|&nbsp;|&#160;", RegexOptions.IgnoreCase | RegexOptions.Singleline | RegexOptions.Compiled);
    public static bool IsNotNullOrBlank(this string str) { return !IsNullOrBlank(str); }
    public static bool IsNullOrBlank(this string str)
    {
      if (IsNullOrWhiteSpace(str))
        return true;
      return _MatchBlanksHtml.Replace(str, string.Empty).IsNullOrEmpty();
    }


    //
    // trimming dei blanks html da una stringa
    //
    private static Regex _TrimBlanksHtmlStart = new Regex(@"^(<br\s*/*>|\s|&nbsp;|&#160;)+", RegexOptions.IgnoreCase | RegexOptions.Singleline | RegexOptions.Compiled);
    private static Regex _TrimBlanksHtmlEnd = new Regex(@"(<br\s*/*>|\s|&nbsp;|&#160;)+$", RegexOptions.IgnoreCase | RegexOptions.Singleline | RegexOptions.Compiled);
    public static string TrimBlanks(this string str)
    {
      if (str.IsNullOrEmpty())
        return str;
      return _TrimBlanksHtmlEnd.Replace(_TrimBlanksHtmlStart.Replace(str, string.Empty), string.Empty);
    }
    public static string TrimBlanksStart(this string str)
    {
      if (str.IsNullOrEmpty())
        return str;
      return _TrimBlanksHtmlStart.Replace(str, string.Empty);
    }
    public static string TrimBlanksEnd(this string str)
    {
      if (str.IsNullOrEmpty())
        return str;
      return _TrimBlanksHtmlEnd.Replace(str, string.Empty);
    }


    //
    // da commentare se si usa LinqKit
    //
    //public static void ForEach<T>(this IEnumerable<T> source, Action<T> action)
    //{
    //  foreach (T element in source)
    //    action(element);
    //}


    public static IEnumerable<ListItem> LINQ(this ListItemCollection elements) { return elements.Cast<ListItem>(); }
    public static IEnumerable<T> EnumeratorLocked<T>(this IEnumerable<T> ie, object @lock) { return new SafeEnumerable<T>(ie, @lock); }
    public static SmartEnumerable<T> AsSmartEnumerable<T>(this IEnumerable<T> source) { return new SmartEnumerable<T>(source); }


    //
    // IEnumerable<T> ricorsivo per tree
    // childrenFunc eg.: n=>n.Nodes
    //
    public static IEnumerable<T> EnumerableOnTree<T>(this T head) where T : IEnumerable<T>
    {
      yield return head;
      foreach (var node in head)
      {
        foreach (var child in EnumerableOnTree(node))
        {
          yield return child;
        }
      }
    }
    //
    public static IEnumerable<T> EnumerableOnTree<T>(this T head, Func<T, IEnumerable<T>> childrenFunc)
    {
      yield return head;
      foreach (var node in childrenFunc(head))
      {
        foreach (var child in EnumerableOnTree(node, childrenFunc))
        {
          yield return child;
        }
      }
    }
    //
    public static IEnumerable<T> EnumerableOnTreeChildLast<T>(this T head, Func<T, IEnumerable<T>> childrenFunc)
    {
      yield return head;
      var last = head;
      foreach (var node in EnumerableOnTreeChildLast(head, childrenFunc))
      {
        foreach (var child in childrenFunc(node))
        {
          yield return child;
          last = child;
        }
        if (last.Equals(node))
          yield break;
      }
    }


    // versione di IndexOf utilizzabile per i sort
    public static int IndexOfSortable<T>(this IList<T> list, T item) { return IndexOfSortable<T>(list, item, null); }
    public static int IndexOfSortable<T>(this IList<T> list, T item, int? notFoundValue)
    {
      int idx = -1;
      if (list != null)
        idx = list.IndexOf(item);
      if (idx < 0)
        return notFoundValue.GetValueOrDefault(list != null ? list.Count : 0);
      return idx;
    }


    public static IEnumerable<T> SkipLast<T>(this IEnumerable<T> sequence, int? itemsToSkip)
    {
      if (itemsToSkip.GetValueOrDefault(0) <= 0)
        return sequence;
      try
      {
        int num = Math.Max(sequence.Count() - itemsToSkip.GetValueOrDefault(0), 0);
        return sequence.Take(num);
      }
      catch { }
      return sequence;
    }


    //
    // Slices a sequence into a sub-sequences each containing maxItemsPerSlice, except for the last
    // which will contain any items left over
    //
    public static IEnumerable<List<T>> Slice<T>(this IEnumerable<T> sequence, int maxItemsPerSlice)
    {
      if (maxItemsPerSlice <= 0)
        throw new ArgumentOutOfRangeException("maxItemsPerSlice", "maxItemsPerSlice must be greater than 0");
      List<T> slice = new List<T>(maxItemsPerSlice);
      foreach (var item in sequence)
      {
        slice.Add(item);
        if (slice.Count == maxItemsPerSlice)
        {
          yield return slice.ToList();
          slice.Clear();
        }
      }
      if (slice.Count > 0)
        yield return slice.ToList();
    }

    //
    // extension method per il supporto di IEqualityComparer generici con LINQ
    //
    public static IEnumerable<TSource> Except<TSource>(this IEnumerable<TSource> first, IEnumerable<TSource> second, Func<TSource, TSource, bool> comparer) { return first.Except(second, new LambdaComparer<TSource>(comparer)); }
    public static IEnumerable<TSource> Intersect<TSource>(this IEnumerable<TSource> first, IEnumerable<TSource> second, Func<TSource, TSource, bool> comparer) { return first.Intersect(second, new LambdaComparer<TSource>(comparer)); }

    //
    // eg: lista_di_liste.Distinct((x, y) => x.SequenceEqual(y))  --> fornisce la lista delle liste distinte
    //
    public static IEnumerable<TSource> Distinct<TSource>(this IEnumerable<TSource> source, Func<TSource, TSource, bool> comparer) { return source.Distinct(new LambdaComparer<TSource>(comparer)); }
    // NON SCOMMENTARE: non funziona con IQueryable perche' non riesce mai a compilare la lambda expression e presenta un sacco di problemi di compatibilita' con il codice esistente
    //public static IQueryable<TSource> Distinct<TSource>(this IQueryable<TSource> source, Func<TSource, TSource, bool> comparer) { return source.Distinct(new LambdaComparer<TSource>(comparer)); }


    public static IEnumerable<TResult> JoinLeftOuter<TOuter, TInner, TKey, TResult>(this IEnumerable<TOuter> outer, IEnumerable<TInner> inner, Func<TOuter, TKey> outerKeySelector, Func<TInner, TKey> innerKeySelector, Func<TOuter, TInner, TResult> resultSelector)
    {
      return outer.GroupJoin(inner, outerKeySelector, innerKeySelector, (outerElem, inners) => resultSelector(outerElem, inners.DefaultIfEmpty().FirstOrDefault()));
    }


    //
    // metodo per caricare automaticamente in una query i riferimenti esterni
    // deve essere l'ultima operazione nella costruzione di una query (ritorna iEnumerable)
    // e opera unicamente per mappings 1:1
    // 
    // var oneInclude = db.Posts.Where(p => p.Published).Include2(p => p.Blog));
    // var multipleIncludes = db.Posts.Where(p => p.Published).Include2(p => new { p.Blog, p.Template, p.Blog.Author }));
    // http://damieng.com/blog/2010/05/21/include-for-linq-to-sql-and-maybe-other-providers
    //
    public static IEnumerable<T> Include<T, TInclude>(this IQueryable<T> query, Expression<Func<T, TInclude>> path)
      where T : class
      where TInclude : class
    {
      ParameterExpression pathParameter = path.Parameters.Single();
      Type tupleType = typeof(Tuple<T, TInclude>);
      Expression<Func<T, Tuple<T, TInclude>>> pathSelector = Expression.Lambda<Func<T, Tuple<T, TInclude>>>(
        Expression.New(tupleType.GetConstructor(new Type[] { typeof(T), typeof(TInclude) }),
          new Expression[] { pathParameter, path.Body },
          tupleType.GetProperty("Item1"), tupleType.GetProperty("Item2")), pathParameter);
      return query.Select(pathSelector).Select(t => new { Item1 = t.Item1, Item2 = t.Item2 }).AsEnumerable().Select(t => t.Item1);
    }


    public static HashSet<T> ToHashSet<T>(this IEnumerable<T> source)
    {
      HashSet<T> hashSet = new HashSet<T>();
      if (source != null)
      {
        foreach (var item in source)
          hashSet.Add(item);
      }
      return hashSet;
    }


    public static void Swap<T>(T object1, T object2)
    {
      T obj = object1;
      object1 = object2;
      object2 = obj;
    }


    public static void ClearCachingFriendly<T>(this IList<T> list)
    {
      if (list != null)
      {
        for (int idx = list.Count - 1; idx >= 0; idx--)
          list[idx] = default(T);
        list.Clear();
      }
    }


    public static void ClearCachingFriendly<T>(this IList<T> list, Action<T> processor)
    {
      if (list != null)
      {
        if (processor == null)
        {
          ClearCachingFriendly(list);
          return;
        }
        for (int idx = list.Count - 1; idx >= 0; idx--)
        {
          processor(list[idx]);
          list[idx] = default(T);
        }
        list.Clear();
      }
    }


    public static void ClearEnumerableCachingFriendly<T>(this IEnumerable<T> list, Action<T> processor)
    {
      if (list != null && processor != null)
      {
        foreach (var item in list)
          processor(item);
      }
    }


    public static void ClearCachingFriendly<K, V>(this IDictionary<K, V> dict)
    {
      if (dict != null)
      {
        foreach (var key in dict.Keys)
          dict[key] = default(V);
        dict.Clear();
      }
    }


    public static T WeakReferenceGet<T>(this WeakReference weakRef) where T : class
    {
      if (weakRef != null)
      {
        T value = weakRef.Target as T;
        return value;
      }
      return default(T);
    }


    public static T WeakReferenceSet<T>(ref WeakReference weakRef, T value) where T : class
    {
      T valueStored = null;
      if (weakRef != null)
      {
        valueStored = weakRef.Target as T;
      }
      if (valueStored == null)
      {
        weakRef = null;
        weakRef = new WeakReference(value);
      }
      return value;
    }


    public static bool WeakReferenceIsAlive<T>(this WeakReference weakRef)
    {
      if (weakRef != null)
        return weakRef.IsAlive;
      return false;
    }


    public static String HtmlSpecialChars(String str)
    {
      return HttpUtility.HtmlEncode(str);
    }

    public static String NormalizeNewline(String str)
    {
      String out_s = str;
      out_s = Regex.Replace(out_s, "\r\n", "\n");  // MS-DOS line feed
      out_s = Regex.Replace(out_s, "\n\r", "\n");  // wrong line feed
      out_s = Regex.Replace(out_s, "\r", "\n");    // MAC line feed
      return out_s;
    }

    public static string StripTags(string strReplace)
    {
      string str = Regex.Replace(strReplace, @"<(.|\n)*?>", string.Empty);
      str = Regex.Replace(str, @"\n", "<br />");

      return str;
    }

    public static string StripTagsAndTrim(string strReplace, int length)
    {
      string str = StripTags(strReplace);

      if (length <= 0)
        return string.Empty;

      int pos = str.IndexOf(' ', Math.Min(length, str.Length) - 1);
      if (pos != -1)
      {
        str = str.Substring(0, pos);
        str += "..";
      }

      return str;
    }

    public static String nl2br(String str)
    {
      return Regex.Replace(NormalizeNewline(str), "\n", "<br />\n");
    }


    private static readonly char[][] SafeListCodes = InitializeSafeList();
    //[System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Maintainability", "CA1502:AvoidExcessiveComplexity", Justification = "This is necessary complexity.")]
    private static char[][] InitializeSafeList()
    {
      char[][] allCharacters = new char[65536][];
      for (int i = 0; i < allCharacters.Length; i++)
      {
        if (
            (i >= 97 && i <= 122) ||        // a-z
            (i >= 65 && i <= 90) ||         // A-Z
            (i >= 48 && i <= 57) ||         // 0-9
            i == 32 ||                      // space
            i == 46 ||                      // .
            i == 44 ||                      // ,
            i == 45 ||                      // -
            i == 95 ||                      // _
            (i >= 256 && i <= 591) ||       // Latin,Extended-A,Latin Extended-B        
            (i >= 880 && i <= 2047) ||      // Greek and Coptic,Cyrillic,Cyrillic Supplement,Armenian,Hebrew,Arabic,Syriac,Arabic,Supplement,Thaana,NKo
            (i >= 2304 && i <= 6319) ||     // Devanagari,Bengali,Gurmukhi,Gujarati,Oriya,Tamil,Telugu,Kannada,Malayalam,Sinhala,Thai,Lao,Tibetan,Myanmar,eorgian,Hangul Jamo,Ethiopic,Ethiopic Supplement,Cherokee,Unified Canadian Aboriginal Syllabics,Ogham,Runic,Tagalog,Hanunoo,Buhid,Tagbanwa,Khmer,Mongolian   
            (i >= 6400 && i <= 6687) ||     // Limbu, Tai Le, New Tai Lue, Khmer, Symbols, Buginese
            (i >= 6912 && i <= 7039) ||     // Balinese         
            (i >= 7680 && i <= 8191) ||     // Latin Extended Additional, Greek Extended        
            (i >= 11264 && i <= 11743) ||   // Glagolitic, Latin Extended-C, Coptic, Georgian Supplement, Tifinagh, Ethiopic Extended    
            (i >= 12352 && i <= 12591) ||   // Hiragana, Katakana, Bopomofo       
            (i >= 12688 && i <= 12735) ||   // Kanbun, Bopomofo Extended        
            (i >= 12784 && i <= 12799) ||   // Katakana, Phonetic Extensions         
            (i >= 19968 && i <= 40899) ||   // Mixed japanese/chinese/korean
            (i >= 40960 && i <= 42191) ||   // Yi Syllables, Yi Radicals        
            (i >= 42784 && i <= 43055) ||   // Latin Extended-D, Syloti, Nagri        
            (i >= 43072 && i <= 43135) ||   // Phags-pa         
            (i >= 44032 && i <= 55215) /* Hangul Syllables */)
        {
          allCharacters[i] = null;
        }
        else
        {
          string integerStringValue = i.ToString(CultureInfo.InvariantCulture);
          int integerStringLength = integerStringValue.Length;
          char[] thisChar = new char[integerStringLength];
          for (int j = 0; j < integerStringLength; j++)
          {
            thisChar[j] = integerStringValue[j];
          }

          allCharacters[i] = thisChar;
        }
      }
      return allCharacters;
    }

    // .NET4 da sostituire con Encoder.JavaScriptEncode
    public static string JavaScriptEncode(string input, bool emitQuotes)
    {
      // Input validation: empty or null string condition
      if (string.IsNullOrEmpty(input))
      {
        return emitQuotes ? "''" : string.Empty;
      }

      // Use a new char array.
      int outputLength = 0;
      int inputLength = input.Length;
      char[] returnMe = new char[inputLength * 8]; // worst case length scenario

      // First step is to start the encoding with an apostrophe if flag is true.
      if (emitQuotes)
      {
        returnMe[outputLength++] = '\'';
      }

      for (int i = 0; i < inputLength; i++)
      {
        int currentCharacterAsInteger = input[i];
        char currentCharacter = input[i];
        if (SafeListCodes[currentCharacterAsInteger] != null || currentCharacterAsInteger == 92 || (currentCharacterAsInteger >= 123 && currentCharacterAsInteger <= 127))
        {
          // character needs to be encoded
          if (currentCharacterAsInteger >= 127)
          {
            returnMe[outputLength++] = '\\';
            returnMe[outputLength++] = 'u';
            string hex = ((int)currentCharacter).ToString("x", CultureInfo.InvariantCulture).PadLeft(4, '0');
            returnMe[outputLength++] = hex[0];
            returnMe[outputLength++] = hex[1];
            returnMe[outputLength++] = hex[2];
            returnMe[outputLength++] = hex[3];
          }
          else
          {
            returnMe[outputLength++] = '\\';
            returnMe[outputLength++] = 'x';
            string hex = ((int)currentCharacter).ToString("x", CultureInfo.InvariantCulture).PadLeft(2, '0');
            returnMe[outputLength++] = hex[0];
            returnMe[outputLength++] = hex[1];
          }
        }
        else
        {
          // character does not need encoding
          returnMe[outputLength++] = input[i];
        }
      }

      // Last step is to end the encoding with an apostrophe if flag is true.
      if (emitQuotes)
      {
        returnMe[outputLength++] = '\'';
      }

      return new string(returnMe, 0, outputLength);
    }


    private static Regex _PathSanitizeFileNameRegEx = new Regex(String.Format("[{0}]+", Regex.Escape(new string(Path.GetInvalidFileNameChars()))), RegexOptions.Singleline | RegexOptions.Compiled | RegexOptions.CultureInvariant);
    public static string PathSanitizeFileName(string fileName) { return PathSanitizeFileName(fileName, string.Empty); }
    public static string PathSanitizeFileName(string fileName, string substChar)
    {
      try { return _PathSanitizeFileNameRegEx.Replace(fileName, substChar ?? string.Empty); }
      catch { return null; }
    }


    private static Regex _PathSanitizeRegEx = new Regex(String.Format("[{0}]+", Regex.Escape(new string(Path.GetInvalidPathChars()))), RegexOptions.Singleline | RegexOptions.Compiled | RegexOptions.CultureInvariant);
    public static string PathSanitize(string path) { return PathSanitize(path, string.Empty); }
    public static string PathSanitize(string path, string substChar)
    {
      try { return _PathSanitizeRegEx.Replace(path, substChar ?? string.Empty); }
      catch { return null; }
    }


    public static string PathGetFileNameSanitized(string path) { return PathGetFileNameSanitized(path, string.Empty); }
    public static string PathGetFileNameSanitized(string path, string substChar)
    {
      try { return Path.GetFileName(PathSanitizeFileName(path, substChar)); }
      catch { return null; }
    }


    public static string PathGetExtensionSanitized(string path)
    {
      try { return Path.GetExtension(PathSanitizeFileName(path, string.Empty)); }
      catch { return null; }
    }


    public static string vPathMap(string vPath)
    {
      //return HttpContext.Current.Server.MapPath(vPath);
      //return HostingEnvironment.MapPath(vPath);

      string fsPath = null;
      try
      {
        fsPath = HostingEnvironment.MapPath(vPath);
      }
      catch { }
      if (!string.IsNullOrEmpty(fsPath))
        return fsPath;
      // nel caso non sia possibile il mapping (es. in global.asax o cache expiry) provo con metodi alternativi
      try
      {
        string vPath2 = VirtualPathUtility.ToAppRelative(vPath, HttpRuntime.AppDomainAppVirtualPath);
        vPath2 = vPath2.TrimStart('~').Replace('/', Path.DirectorySeparatorChar).TrimStart(Path.DirectorySeparatorChar);
        fsPath = Path.Combine(HttpRuntime.AppDomainAppPath, vPath2);
      }
      catch { }
      return fsPath;
    }

    public static String vToAppRelative(String url)
    {
      if (string.IsNullOrEmpty(url))
        return string.Empty;
      string query = string.Empty;
      int idx1 = url.IndexOf('?');
      if (idx1 != -1)
      {
        query = url.Substring(idx1);
        url = url.Substring(0, idx1);
      }
      if (url.IndexOf(':') == -1)
        if (VirtualPathUtility.IsAbsolute(url))
          url = VirtualPathUtility.ToAppRelative(url);
      return url + query;
    }

    public static String vToAbsolute(String url)
    {
      if (string.IsNullOrEmpty(url))
        return string.Empty;
      string query = string.Empty;
      int idx1 = url.IndexOf('?');
      if (idx1 != -1)
      {
        query = url.Substring(idx1);
        url = url.Substring(0, idx1);
      }
      if (url.IndexOf(':') == -1)
        if (VirtualPathUtility.IsAppRelative(url))
          url = VirtualPathUtility.ToAbsolute(url);
      return url + query;
    }

    public static String ToAppRelative(String url)
    {
      if (string.IsNullOrEmpty(url))
        return string.Empty;
      string query = string.Empty;
      try
      {
        int idx1 = url.IndexOf('?');
        if (idx1 != -1)
        {
          query = url.Substring(idx1);
          url = url.Substring(0, idx1);
        }
        if (url.IndexOf(':') == -1)
          if (VirtualPathUtility.IsAbsolute(url))
            url = VirtualPathUtility.ToAppRelative(url);
        return url + query;
      }
      catch { }
      return url;
    }

    public static bool FileExistsVirtual(string fname)
    {
      return File.Exists(vPathMap(fname));
    }

    public static String FileReadVirtual(String fname)
    {
      return FileRead(vPathMap(fname));
    }

    public static String FileReadVirtual(String fname, Encoding enc)
    {
      return FileRead(vPathMap(fname), enc);
    }

    public static String FileRead(String fname)
    {
      //questo sistema ha problemi di locking con files aperti da altre applicazioni
      //using (StreamReader fstream = new StreamReader(fname))
      //  return fstream.ReadToEnd();
      using (FileStream fstream = new FileStream(fname, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
      {
        using (StreamReader sr = new StreamReader(fstream))
          return sr.ReadToEnd();
      }
    }

    public static byte[] FileReadBytes(String fname, long? maxLength)
    {
      using (FileStream fstream = new FileStream(fname, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
      {
        int len = (int)Math.Min(fstream.Length, maxLength.GetValueOrDefault(long.MaxValue));
        byte[] data = new byte[len];
        fstream.Read(data, 0, len);
        return data;
      }
    }

    public static String FileRead(String fname, Encoding enc)
    {
      using (FileStream fstream = new FileStream(fname, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
      {
        using (StreamReader sr = new StreamReader(fstream, enc))
          return sr.ReadToEnd();
      }
    }

    public static XElement FileReadXml(String fname)
    {
      XElement xml = null;
      using (FileStream fstream = new FileStream(fname, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
      {
        using (XmlReader xr = XmlReader.Create(fstream))
        {
          if (xr != null)
            xml = XElement.Load(xr);
          xr.Close();
        }
      }
      return xml;
    }

    public static XElement FileReadXmlVirtual(String vfname)
    {
      return FileReadXml(vPathMap(vfname));
    }


    public static bool DeleteFileManaged(string filename)
    {
      FileInfo fi = new FileInfo(filename);
      return fi.DeleteManaged();
    }


    public static bool DeleteManaged(this FileInfo fi)
    {
      if (fi == null || !fi.Exists)
      {
        // il file non esiste o l'oggetto e' nullo
        return false;
      }
      try
      {
        fi.Delete();
        Logger.Log.Info(string.Format("DeleteManaged: [regular] OK path={0}", fi.FullName));
        return true;
      }
      catch (Exception ex)
      {
        Logger.Log.Info(string.Format("DeleteManaged: [regular] path={0} exception={1}", fi.FullName, ex.Message));
      }
      try
      {
        string forcedel_fname = @"C:\windows\ForceDel.exe";
        if (File.Exists(forcedel_fname))
        {
          System.Diagnostics.Process proc = new System.Diagnostics.Process();
          proc.EnableRaisingEvents = false;
          proc.StartInfo.FileName = forcedel_fname;
          proc.StartInfo.Arguments = string.Format("\"{0}\"", fi.FullName);
          //proc.StartInfo.WorkingDirectory = fi.DirectoryName;
          //proc.StartInfo.UseShellExecute = false;
          proc.StartInfo.CreateNoWindow = true;
          proc.Start();
          proc.WaitForExit(2000);
          Logger.Log.Info(string.Format("DeleteManaged: [forced] OK path={0}", fi.FullName));
          return true;
        }
      }
      catch (Exception ex)
      {
        Logger.Log.Info(string.Format("DeleteManaged: [forced] path={0} exception={1}", fi.FullName, ex.Message));
      }
      return false;
    }


    public static bool ValidateEMail(string email)
    {
      System.Net.Mail.MailAddress addr = null;
      if (email.IsNotEmpty())
      {
        //return Regex.IsMatch(email.Trim(), @"^[_a-zA-Z0-9-]+(\.[_a-zA-Z0-9-]+)*@[a-zA-Z0-9-]+(\.[a-zA-Z0-9-]+)*\.(([0-9]{1,3})|([a-zA-Z]{2,3})|(aero|coop|info|museum|name))$", RegexOptions.IgnoreCase);
        try { addr = new System.Net.Mail.MailAddress(email); }
        catch { }
      }
      return addr != null;
    }


    //
    // fornisce un mime dal filename o derivandolo dai contenuti
    //
    public static string GetMimeType(string Filename)
    {
      // longest mime in registry is 73 characters long
      string mime = "application/octet-stream";
      try
      {
        string ext = PathGetExtensionSanitized(Filename).ToLower();
        Microsoft.Win32.RegistryKey rk = Microsoft.Win32.Registry.ClassesRoot.OpenSubKey(ext);
        if (rk != null && rk.GetValue("Content Type") != null)
          mime = rk.GetValue("Content Type").ToString();
      }
      catch { }
      return mime;
    }
    //
    public static string GetMimeType(string Filename, byte[] data)
    {
      try
      {
        System.UInt32 mimetype;
        FindMimeFromData(0, Filename, data, (uint)Math.Min(data.Length, 1024), null, 0, out mimetype, 0);
        System.IntPtr mimeTypePtr = new IntPtr(mimetype);
        string mime = Marshal.PtrToStringUni(mimeTypePtr);
        Marshal.FreeCoTaskMem(mimeTypePtr);
        if (mime == "application/octet-stream")
        {
          mime = GetMimeType(Filename);
        }
        return mime;
      }
      catch { return "application/octet-stream"; }
    }
    //
    [DllImport(@"urlmon.dll", CharSet = CharSet.Auto)]
    private extern static System.UInt32 FindMimeFromData(
        System.UInt32 pBC,
        [MarshalAs(UnmanagedType.LPStr)] System.String pwzUrl,
        [MarshalAs(UnmanagedType.LPArray)] byte[] pBuffer,
        System.UInt32 cbSize,
        [MarshalAs(UnmanagedType.LPStr)] System.String pwzMimeProposed,
        System.UInt32 dwMimeFlags,
        out System.UInt32 ppwzMimeOut,
        System.UInt32 dwReserverd
    );


    public static string WebRequestWrapper(string url, Action<WebRequest> preProcessor)
    {
      HttpWebRequest webreq = (HttpWebRequest)HttpWebRequest.Create(url);
      if (preProcessor != null)
      {
        // es. webreq.Timeout = 3600 * 1000;
        preProcessor(webreq);
      }
      using (var webresp = webreq.GetResponse())
      {
        using (Stream stream2 = webresp.GetResponseStream())
        {
          using (StreamReader reader2 = new StreamReader(stream2))
          {
            return reader2.ReadToEnd();
          }
        }
      }
    }


    public static byte[] WebRequestWrapperBinary(string url, Action<WebRequest> preProcessor)
    {
      HttpWebRequest webreq = (HttpWebRequest)HttpWebRequest.Create(url);
      if (preProcessor != null)
      {
        // es. webreq.Timeout = 3600 * 1000;
        preProcessor(webreq);
      }
      using (var webresp = webreq.GetResponse())
      {
        using (Stream stream2 = webresp.GetResponseStream())
        {
          byte[] data = new byte[stream2.Length];
          int len = stream2.Read(data, 0, (int)stream2.Length);
          return data;
        }
      }
    }


    public static string UrlFetch_UTF8(string url)
    {
      string response = string.Empty;
      try
      {
        using (WebClient client = new WebClient())
        {
          client.Encoding = System.Text.Encoding.UTF8;
          response = client.DownloadString(url);
        }
      }
      catch { }
      return response;
    }


    public static string UrlFetch(string url)
    {
      string response = string.Empty;
      try
      {
        using (WebClient client = new WebClient())
        {
          //client.Encoding = System.Text.Encoding.UTF8;
          response = client.DownloadString(url);
        }
      }
      catch { }
      return response;
    }


    //
    // funzioni di supporto per l'upload di files (anche multipli sullo stesso controllo) tramite AJAX
    // il primo argomento del lookup e' il nome del controllo di upload
    // e' adatta ad essere utilizzata con il plugin jQuery MultiFile
    //
    public static ILookup<string, HttpPostedFile> GetAjaxPostedFilesAndFields()
    {
      try
      {
        if (HttpContext.Current.Request.Files != null)
        {
          List<string> fields = HttpContext.Current.Request.Files.OfType<string>().ToList();
          ILookup<string, HttpPostedFile> files = Enumerable.Range(0, HttpContext.Current.Request.Files.Count).Where(i => HttpContext.Current.Request.Files[i].ContentLength > 0).ToLookup(i => fields[i], i => HttpContext.Current.Request.Files[i]);
          return files;
        }
      }
      catch { }
      return null;
    }


    //
    // come quella precedente GetAjaxPostedFilesAndFields ma senza grouping per nome del controllo di upload
    //
    public static List<HttpPostedFile> GetAjaxPostedFiles()
    {
      try
      {
        if (HttpContext.Current.Request.Files != null)
          return Enumerable.Range(0, HttpContext.Current.Request.Files.Count).Where(i => HttpContext.Current.Request.Files[i].ContentLength > 0).Select(i => HttpContext.Current.Request.Files[i]).ToList();
      }
      catch { }
      return null;
    }


    public static string AjaxResponseWrapperForFileUpload(string jsonString)
    {
      bool hasFiles = false;
      try { hasFiles = HttpContext.Current.Request.Files != null && HttpContext.Current.Request.Files.Count > 0; }
      catch { }
      return hasFiles ? "<textarea>" + (jsonString ?? string.Empty) + "</textarea>" : jsonString;
    }


    public static string StringTruncate(this string str, int length) { return StringTruncate(str, length, string.Empty); }
    public static string StringTruncate(this string str, int length, string ellissi)
    {
      if (str != null && str.Length > length)
        return string.IsNullOrEmpty(ellissi) ? str.Substring(0, length) : str.Substring(0, Math.Max(length - ellissi.Length, 0)) + ellissi;
      else
        return str;
    }

    public static string StringReplaceFirst(string str, string frag_old, string frag_new)
    {
      if (!string.IsNullOrEmpty(str))
      {
        int idx = str.IndexOf(frag_old);
        if (idx != -1)
          str = str.Remove(idx, frag_old.Length).Insert(idx, frag_new);
      }
      return str;
    }


    public static string SubStringCutFromEnd(this string str, int count)
    {
      try
      {
        int l = str.Length;
        return str.Substring(0, Math.Max(l - count, 0));
      }
      catch { }
      return str;
    }


    //
    // trasformazione dei caratteri accentati in caratteri ASCII normali
    //
    public static string ReplaceAccents(this string str)
    {
      try
      {
        string stFormD = str.Normalize(NormalizationForm.FormD);
        StringBuilder sb = new StringBuilder();
        for (int ich = 0; ich < stFormD.Length; ich++)
        {
          UnicodeCategory uc = CharUnicodeInfo.GetUnicodeCategory(stFormD[ich]);
          if (uc != UnicodeCategory.NonSpacingMark)
            sb.Append(stFormD[ich]);
        }
        return (sb.ToString().Normalize(NormalizationForm.FormC));
      }
      catch { return str; }
    }


    //
    // funzioni di supporto per il trattamento delle stringhe null/empty
    //
    public static string NullIfEmpty(this string str, string emptyValue) { return (str == null || str == emptyValue) ? null : str; }
    public static string NullIfEmpty(this string str) { return string.IsNullOrEmpty(str) ? null : str; }
    public static string DefaultIfEmpty(this string str, string defaultValue) { return string.IsNullOrEmpty(str) ? defaultValue : str; }
    public static string DefaultIfEmptyTrim(this string str, string defaultValue)
    {
      return str.IsNullOrWhiteSpace() ? defaultValue : str.Trim();
    }


    public static string TrimSafe(this string str, params char[] trimChars) { return (str ?? string.Empty).Trim(trimChars); }
    public static string TrimStartSafe(this string str, params char[] trimChars) { return (str ?? string.Empty).TrimStart(trimChars); }
    public static string TrimEndSafe(this string str, params char[] trimChars) { return (str ?? string.Empty).TrimEnd(trimChars); }


    public static string Quoting4JS(this string str)
    {
      if (str.IsNullOrEmpty())
        return str;
      char? last = null;
      StringBuilder sb = new StringBuilder(str.Length);
      str.ToCharArray().ForEach(c => { if (last != '\\' && (c == '\'' || c == '"')) sb.Append('\\'); sb.Append(c); last = c; });
      return sb.ToString();
    }

    public static string QuotingSingle4JS(this string str)
    {
      if (str.IsNullOrEmpty())
        return str;
      char? last = null;
      StringBuilder sb = new StringBuilder(str.Length);
      str.ToCharArray().ForEach(c => { if (last != '\\' && c == '\'') sb.Append('\\'); sb.Append(c); last = c; });
      return sb.ToString();
    }

    public static string HtmlAttributeEncode(this string str)
    {
      if (str.IsNullOrEmpty())
        return string.Empty;
      return HttpUtility.HtmlAttributeEncode(str).Replace("'", "&apos;");
    }


    public static IEnumerable<T> ReverseT<T>(this IEnumerable<T> data) { return data.Reverse(); }


    public static string ImplodeNN<T>(IEnumerable<T> arr, string separator) { return Implode(arr, separator, string.Empty, false, true); }
    public static string Implode<T>(IEnumerable<T> arr, string separator) { return Implode(arr, separator, string.Empty, false, false); }
    public static string Implode<T>(IEnumerable<T> arr, string separator, string quotes, bool trim) { return Implode(arr, separator, quotes, trim, false); }
    public static string Implode<T>(IEnumerable<T> arr, string separator, string quotes, bool trim, bool discard_empty)
    {
      quotes = quotes ?? string.Empty;
      StringBuilder sb = null;
      try
      {
        foreach (T s in arr)
        {
          string ss = null;
          if (s != null)
            ss = trim ? s.ToString().Trim() : s.ToString();
          if (discard_empty && string.IsNullOrEmpty(ss))
            continue;
          if (sb != null)
            sb.Append(separator);
          else
            sb = new StringBuilder();
          sb.Append(quotes + ss + quotes);
        }
      }
      catch { }
      if (sb != null)
        return sb.ToString();
      else
        return string.Empty;
    }

    public static List<string> Explode2(string src, string separatorString) { return Explode2(src, separatorString, null, false); }
    public static List<string> Explode2(string src, string separatorString, string trim_chars, bool discard_empty)
    {
      List<string> res = new List<string>();
      for (int idx = 0; idx != -1; )
      {
        int lastIdx = idx;
        idx = src.IndexOf(separatorString, lastIdx);
        if (idx == -1)
        {
          res.Add(src.Substring(lastIdx));
          break;
        }
        else
        {
          res.Add(src.Substring(lastIdx, idx - lastIdx));
          idx += separatorString.Length;
        }
      }
      if (trim_chars != null)
        res = res.ConvertAll<string>(delegate(string s) { return s.Trim(trim_chars.ToCharArray()); });
      if (discard_empty)
        return res.FindAll(delegate(string s) { return !string.IsNullOrEmpty(s); });
      return res;
    }

    public static List<string> Explode(string src, string separators) { return Explode(src, separators, null, false); }
    public static List<string> Explode(string src, string separators, string trim_chars, bool discard_empty)
    {
      IEnumerable<string> res = (src ?? string.Empty).Split(separators.ToCharArray());
      if (trim_chars != null)
      {
        var trimChars = trim_chars.ToCharArray();
        res = res.Select(s => s != null ? s.Trim(trimChars) : s);
      }
      if (discard_empty)
        res = res.Where(s => !string.IsNullOrEmpty(s));
      return res.ToList();

      //List<string> res = new List<string>((src ?? string.Empty).Split(separator.ToCharArray()));
      //if (trim_chars != null)
      //  res = res.ConvertAll<string>(delegate(string s) { return s.Trim(trim_chars.ToCharArray()); });
      //if (discard_empty)
      //  return res.FindAll(delegate(string s) { return !string.IsNullOrEmpty(s); });
      //return res;
    }

    public static List<T> ExplodeT<T>(string src, string separators) { return ExplodeT<T>(src, separators, null, false); }
    public static List<T> ExplodeT<T>(string src, string separators, string trim_chars, bool discard_empty)
    {
      IEnumerable<string> res = (src ?? string.Empty).Split(separators.ToCharArray());
      if (trim_chars != null)
      {
        var trimChars = trim_chars.ToCharArray();
        res = res.Select(s => s != null ? s.Trim(trimChars) : s);
      }
      if (discard_empty)
        res = res.Where(s => !string.IsNullOrEmpty(s));
      return res.Select(s => Utility.TryParse<T>(s)).ToList();

      //List<string> res = new List<string>((src ?? string.Empty).Split(separator.ToCharArray()));
      //if (trim_chars != null)
      //  res = res.ConvertAll<string>(delegate(string s) { return s.Trim(trim_chars.ToCharArray()); });
      //if (discard_empty)
      //  res = res.FindAll(delegate(string s) { return !string.IsNullOrEmpty(s); });
      //return res.ConvertAll<T>(delegate(string s) { return Utility.TryParse<T>(s); });
      ////return res.ConvertAll<T>(delegate(string s) { return (T)Convert.ChangeType(s, typeof(T)); });
    }

    // spazio farlocco che ci ritroviamo spesso nelle stringhe
    public static readonly string CharNbsp = "\xA0";


    public static List<T> ListToUnique<T>(ICollection<T> arr)
    {
      List<T> items = new List<T>();
      foreach (T item in arr)
        if (!items.Contains(item))
          items.Add(item);
      return items;
    }


    public static string CreateCSVrecord(IEnumerable<object> data, string separator, string quotes)
    {
      if (data != null)
      {
        if (quotes != null)
        {
          return Implode(data.Select(r => (r ?? string.Empty).ToString().Replace(quotes, quotes + quotes)), separator ?? ",", quotes, false, false);
          //return Implode(data.Select(r => (r ?? string.Empty).ToString().Replace(quotes, "\\" + quotes)), separator ?? ",", quotes, false, false);
        }
        else
        {
          return Implode(data, separator ?? ",", quotes, false, false);
        }
      }
      return string.Empty;
    }


    public static List<string> ParseCSVrecord(string record, string separator, string quote)
    {
      List<string> CSV = new List<string>();
      if (string.IsNullOrEmpty(quote))
        quote = string.Empty;

      record = record.TrimEnd("\r\n".ToCharArray());
      while (record.Length > 0)
      {
        string field = null;
        string sep = separator;
        int offset = 0;
        if (quote.Length > 0 && record.StartsWith(quote))
        {
          sep = quote + separator;
          offset = quote.Length;
        }
        int pos = record.IndexOf(sep, offset);
        if (pos != -1)
        {
          field = record.Substring(offset, pos - offset);
          record = record.Substring(pos + sep.Length);
        }
        else
        {
          field = record;
          record = string.Empty;
        }
        if (quote.Length > 0)
          field = field.Replace(quote + quote, quote);
        CSV.Add(field);
      }
      return CSV;
    }

    public static List<List<string>> ParseCSVdata(string data, string separator, string quote)
    {
      List<List<string>> CSV = new List<List<string>>();
      using (StringReader sr = new StringReader(data))
      {
        string record;
        while ((record = sr.ReadLine()) != null)
          CSV.Add(ParseCSVrecord(record, separator, quote));
      }
      return CSV;
    }

    public static string EscapeSql(string str) { return str.Replace("'", "''"); }

    public static SqlDataAdapter GetDataAdapterWrapper(string connection_string_name, string selectCmd)
    {
      ConnectionStringSettings db_cs = ConfigurationManager.ConnectionStrings[connection_string_name];
      DbProviderFactory db_fact = DbProviderFactories.GetFactory(db_cs.ProviderName);
      SqlDataAdapter db_da = (SqlDataAdapter)db_fact.CreateDataAdapter();
      db_da.SelectCommand = (SqlCommand)db_fact.CreateCommand();
      db_da.SelectCommand.Connection = (SqlConnection)db_fact.CreateConnection();
      db_da.SelectCommand.Connection.ConnectionString = db_cs.ConnectionString;
      db_da.SelectCommand.Connection.Open();
      db_da.SelectCommand.CommandText = selectCmd;

      //SqlCommandBuilder db_cb = (SqlCommandBuilder)db_fact.CreateCommandBuilder();
      SqlCommandBuilder db_cb = new SqlCommandBuilder(db_da);
      //db_cb.DataAdapter = db_da;
      return db_da;
    }

    public static DbDataAdapter GetDataAdapterAuto(string connection_string_name) { return GetDataAdapterAuto(connection_string_name, false); }
    public static DbDataAdapter GetDataAdapterAuto(string connection_string_name, bool? open)
    {
      DbProviderFactory db_fact = (DbProviderFactory)HttpContext.Current.Items[PersistantConnectionBaseName + "db_fact_" + connection_string_name];
      DbDataAdapter db_da = (DbDataAdapter)HttpContext.Current.Items[PersistantConnectionBaseName + "db_da_" + connection_string_name];
      if (open == null)
      {
        HttpContext.Current.Items.Remove(PersistantConnectionBaseName + "db_fact_" + connection_string_name);
        HttpContext.Current.Items.Remove(PersistantConnectionBaseName + "db_da_" + connection_string_name);
        return null;
      }
      if (db_da != null && db_fact != null)
        return db_da;

      ConnectionStringSettings db_cs = ConfigurationManager.ConnectionStrings[connection_string_name];
      db_fact = DbProviderFactories.GetFactory(db_cs.ProviderName);
      db_da = db_fact.CreateDataAdapter();
      db_da.SelectCommand = db_fact.CreateCommand();
      db_da.SelectCommand.Connection = db_fact.CreateConnection();
      db_da.SelectCommand.Connection.ConnectionString = db_cs.ConnectionString;
      if (open == true)
        if (db_da.SelectCommand.Connection.State == ConnectionState.Closed || db_da.SelectCommand.Connection.State == ConnectionState.Broken)
          db_da.SelectCommand.Connection.Open();
      //db_da.SelectCommand.CommandText = query;
      //db_da.Fill(results);  //chiude automaticamente la connessione
      HttpContext.Current.Items[PersistantConnectionBaseName + "db_fact_" + connection_string_name] = db_fact;
      HttpContext.Current.Items[PersistantConnectionBaseName + "db_da_" + connection_string_name] = db_da;
      return db_da;
    }

    public static DbProviderFactory GetProviderFactoryAuto(string connection_string_name)
    {
      GetDataAdapterAuto(connection_string_name);
      return (DbProviderFactory)HttpContext.Current.Items[PersistantConnectionBaseName + "db_fact_" + connection_string_name];
    }

    public static DbCommandBuilder GetCommandBuilderAuto(string connection_string_name)
    {
      DbDataAdapter db_da = GetDataAdapterAuto(connection_string_name);
      DbProviderFactory db_fact = (DbProviderFactory)HttpContext.Current.Items[PersistantConnectionBaseName + "db_fact_" + connection_string_name];
      DbCommandBuilder db_cb = db_fact.CreateCommandBuilder();
      db_cb.DataAdapter = db_da;
      return db_cb;

      //if (HttpContext.Current.Items[PersistantConnectionBaseName + "db_cb_" + connection_string_name] == null)
      //{
      //  DbProviderFactory db_fact = (DbProviderFactory)HttpContext.Current.Items[PersistantConnectionBaseName + "db_fact_" + connection_string_name];
      //  DbCommandBuilder db_cb = db_fact.CreateCommandBuilder();
      //  db_cb.DataAdapter = db_da;
      //  HttpContext.Current.Items[PersistantConnectionBaseName + "db_cb_" + connection_string_name] = db_cb;
      //}
      //return (DbCommandBuilder)HttpContext.Current.Items[PersistantConnectionBaseName + "db_cb_" + connection_string_name];
    }

    //
    // per semplifiacre l'utilizzo di parametri con le query (ci sono grossissimi problemi nel passare le date con le query in stringa...)
    // posso passare i parametri come argomenti opzionali (nello stesso ordine)
    // oppure posso assegnarli in seguito con dbCmd.Parameters.AddWithValue("@data","xyz");
    //
    public static DbCommand GetSqlCommand(string connection_string_name, string query, params object[] values)
    {
      ConnectionStringSettings db_cs = ConfigurationManager.ConnectionStrings[connection_string_name];
      DbProviderFactory db_fact = GetProviderFactoryAuto(connection_string_name);
      DbCommand dbCmd = db_fact.CreateCommand();
      dbCmd.Connection = db_fact.CreateConnection();
      dbCmd.Connection.ConnectionString = db_cs.ConnectionString;
      dbCmd.CommandText = query;
      MatchCollection mcoll = Regex.Matches(query, @"@\w+");
      for (int i = 0; i < mcoll.Count; i++)
      {
        DbParameter p = db_fact.CreateParameter();
        p.ParameterName = mcoll[i].Value;
        p.Value = (i < values.Length) ? values[i] : string.Empty;
        dbCmd.Parameters.Add(p);
      }
      return dbCmd;
    }

    //
    // per chiudere le connessioni lasciate aperte passare null come query
    //
    public static DataTable GetTableAuto(string connection_string_name, object cmdOrQuery) { return GetTableAuto(connection_string_name, cmdOrQuery, true); }
    public static DataTable GetTableAuto(string connection_string_name, object cmdOrQuery, bool keep_open)
    {
      DbDataAdapter db_da = (cmdOrQuery != null) ? GetDataAdapterAuto(connection_string_name) : GetDataAdapterAuto(connection_string_name, null);
      if (db_da == null)
        return null;
      DataTable results = new DataTable();
      if (cmdOrQuery is DbCommand)
        db_da.SelectCommand = (DbCommand)cmdOrQuery;
      else
        db_da.SelectCommand.CommandText = cmdOrQuery.ToString();

      db_da.Fill(results);
      return results;
    }

    public static DataTable GetTableAutoSimple(string connection_string_name, string query)
    {
      DataTable results = new DataTable();
      SqlDataAdapter db_da = new SqlDataAdapter(query, ConfigurationManager.ConnectionStrings[connection_string_name].ConnectionString);
      db_da.Fill(results);
      return results;
    }

    public static string GetSingleValueFromDB(string connection_string_name, object cmdOrQuery) { return GetSingleValueFromDB(connection_string_name, cmdOrQuery, 0); }
    public static string GetSingleValueFromDB(string connection_string_name, object cmdOrQuery, int timeOut)
    {
      DbDataAdapter db_da = (cmdOrQuery != null) ? GetDataAdapterAuto(connection_string_name) : GetDataAdapterAuto(connection_string_name, null);
      if (db_da == null)
        return null;
      DbCommand db_cmd = db_da.SelectCommand;
      if (cmdOrQuery is DbCommand)
        db_cmd = (DbCommand)cmdOrQuery;
      else
        db_cmd.CommandText = cmdOrQuery.ToString();
      if (timeOut > 0)
        db_cmd.CommandTimeout = timeOut;
      if (db_cmd.Connection.State == ConnectionState.Closed || db_cmd.Connection.State == ConnectionState.Broken)
        db_cmd.Connection.Open();
      object res = db_cmd.ExecuteScalar();
      return (res == null) ? "" : res.ToString();
    }

    public static int GetNoValueFromDB(string connection_string_name, object cmdOrQuery) { return GetNoValueFromDB(connection_string_name, cmdOrQuery, 0); }
    public static int GetNoValueFromDB(string connection_string_name, object cmdOrQuery, int timeOut)
    {
      DbDataAdapter db_da = GetDataAdapterAuto(connection_string_name);
      DbProviderFactory db_fact = GetProviderFactoryAuto(connection_string_name);
      if (db_fact == null || db_da == null)
        return -1;
      DbCommand db_cmd = db_fact.CreateCommand();
      if (cmdOrQuery is DbCommand)
        db_cmd = (DbCommand)cmdOrQuery;
      else
        db_cmd.CommandText = cmdOrQuery.ToString();
      if (timeOut > 0)
        db_cmd.CommandTimeout = timeOut;
      db_cmd.Connection = db_da.SelectCommand.Connection;
      if (db_cmd.Connection.State == ConnectionState.Closed || db_cmd.Connection.State == ConnectionState.Broken)
        db_cmd.Connection.Open();
      return db_cmd.ExecuteNonQuery();
    }

    //
    // toglie gli xmlns="http://schemas.microsoft.com/AspNet/SiteMap-File-1.0"
    // che creano casini con le query xpath 1.0 nei files xml
    //
    public static XmlDocument XmlDocumentStrip_xmlns(XmlDocument xml_doc)
    {
      if (xml_doc != null && xml_doc.DocumentElement != null && xml_doc.DocumentElement.NamespaceURI.Length > 0)
      {
        xml_doc.DocumentElement.SetAttribute("xmlns", null);
        xml_doc.LoadXml(xml_doc.OuterXml);
      }
      return xml_doc;
    }

    //
    // per convertire i numeri in virgola mobile indipendentemente dalla lingua
    //
    //public static double ToDouble(object o) { return Convert.ToDouble(o, CultureInfo.InvariantCulture); }
    public static double ToDouble(object o) { return TryParse<double>(o); }
    public static string ToString(double value) { return value.ToString(CultureInfo.InvariantCulture); }
    public static string ToString(string format, double value) { return value.ToString(format, CultureInfo.InvariantCulture); }

    public static string ToString<T>(this Nullable<T> obj, string format) where T : struct { return obj == null ? string.Empty : string.Format("{0:" + format + "}", obj.Value); }

    //
    // caricamento dinamico dei CSS con distinzione tra modalita' debug e release
    //
    // usage Utility.CssManager(this,
    //          new string[] { "~/CSS/AllAdapters.css", "~/App_Themes/data/css/all_MP.css.ashx" },
    //          new string[] { "~/CSS/AllAdapters.css", "~/App_Themes/data/css/generale.css.ashx", "~/App_Themes/data/css/Menu_oriz.css.ashx", "~/App_Themes/data/css/Menu_vert.css.ashx", "~/App_Themes/data/css/Menu_statico.css.ashx" });
    //
    public static void CssManager(Control ctrl, IEnumerable<string> releaseCSS, IEnumerable<string> debugCSS) { CssManager(ctrl, releaseCSS, debugCSS, null); }
    public static void CssManager(Control ctrl, IEnumerable<string> releaseCSS, IEnumerable<string> debugCSS, bool? dataBindHeader) { CssManager(ctrl, releaseCSS, debugCSS, dataBindHeader, null); }
    public static void CssManager(Control ctrl, IEnumerable<string> releaseCSS, IEnumerable<string> debugCSS, bool? dataBindHeader, bool? googleAnalytics)
    {
      IEnumerable<string> listCSS = HttpContext.Current.IsDebuggingEnabled ? debugCSS : releaseCSS;
      try
      {
        //
        if (dataBindHeader == true)
          ctrl.Page.Header.DataBind();
        //
        listCSS = listCSS ?? debugCSS ?? releaseCSS;
        //
        // se e' presente nell'header un PlaceHolder/ContentPlaceHolder con il nome che termina in CSS verra' utilizzato per inserire i CSS dinamici
        // altrimenti utilizzera' lo script manager
        //
        Control PlaceholderHeaderCSS = null;
        PlaceholderHeaderCSS = PlaceholderHeaderCSS ?? Utility.FindAllControlsLINQ<ContentPlaceHolder>(ctrl.Page.Header).FirstOrDefault(c => c.ID != null && c.ID.EndsWith("CSS"));
        PlaceholderHeaderCSS = PlaceholderHeaderCSS ?? Utility.FindAllControlsLINQ<PlaceHolder>(ctrl.Page.Header).FirstOrDefault(c => c.ID != null && c.ID.EndsWith("CSS"));
        //
        try
        {
          var listCSSreordered = (PlaceholderHeaderCSS != null) ? listCSS.Reverse() : listCSS;
          foreach (string url in listCSSreordered)
          {
            try
            {
              string aurl = ctrl.ResolveUrl(url);
              string block = string.Format("<link href='{0}' rel='stylesheet' type='text/css' />", aurl);
              if (PlaceholderHeaderCSS != null)
                PlaceholderHeaderCSS.Controls.AddAt(0, new LiteralControl(block));
              else
                ScriptManager.RegisterClientScriptBlock(ctrl, ctrl.GetType(), url, block, false);
            }
            catch { }
          }
        }
        catch { }
        //
        if (googleAnalytics != false)
        {
          string gaUrl = ((HttpContext.Current.Request.Url.Scheme == "https") ? "https://ssl." : "http://www.") + "google-analytics.com/ga.js";
          ScriptManager.RegisterClientScriptInclude(ctrl, ctrl.GetType(), "GA", gaUrl);
        }
      }
      catch { }
    }


    //
    // aggiunge dinamicamente un CSS all'header della pagina
    //
    public static void AddCssLinkToHeader(Page page, string cssPath)
    {
      if (page == null || page.Header == null)
        return;
      cssPath = page.ResolveUrl(cssPath);
      HtmlLink myHtmlLink = new HtmlLink();
      myHtmlLink.Href = cssPath;
      myHtmlLink.Attributes.Add("rel", "stylesheet");
      myHtmlLink.Attributes.Add("type", "text/css");
      page.Header.Controls.Add(myHtmlLink);
    }

    public static string GetAuthenticatedUserAuthString()
    {
      try
      {
        FormsAuthenticationTicket tk = ((FormsIdentity)HttpContext.Current.User.Identity).Ticket;
        string authString = FormsAuthentication.Encrypt(tk);
        return authString;
      }
      catch { }
      return string.Empty;
    }

    public static string GetAuthenticatedUser(string authString)
    {
      try
      {
        FormsAuthenticationTicket tk = FormsAuthentication.Decrypt(authString);
        if (!tk.Expired)
          return tk.Name;
      }
      catch { }
      return null;
    }


    //
    // controlla se un path e' accessibile per l'utente corrente
    //
    public static bool IsUserOrAnonAllowedToPath(string virtualPath)
    {
      if (HttpContext.Current == null)
        return false;
      if (string.IsNullOrEmpty(virtualPath))
        return true;
      try
      {
        return UrlAuthorizationModule.CheckUrlAccessForPrincipal(CMS_Manager.TrimQuery(virtualPath), HttpContext.Current.User, string.Empty);
      }
      catch { }
      return false;
    }
    public static bool IsUserAllowedToPath(string virtualPath) { return IsUserAllowedToPath(virtualPath, false); }
    public static bool IsUserAllowedToPath(string virtualPath, bool anonymousAllowed)
    {
      if (HttpContext.Current == null)
        return false;
      if (string.IsNullOrEmpty(virtualPath))
        return true;
      if (!anonymousAllowed && !HttpContext.Current.User.Identity.IsAuthenticated)
        return false;
      try
      {
        return UrlAuthorizationModule.CheckUrlAccessForPrincipal(CMS_Manager.TrimQuery(virtualPath), HttpContext.Current.User, string.Empty);
      }
      catch { }
      return false;
    }
    public static bool IsUserAllowedToPath(string virtualPath, string userName)
    {
      //MembershipUser user = Membership.GetUser(userName);
      GenericIdentity gi = new GenericIdentity(userName);
      IPrincipal rp = null;
      try
      {
        // funziona solo se sono attivi i ruoli
        rp = new RolePrincipal(gi);
      }
      catch { }
      if (rp == null)
      {
        try
        {
          rp = new GenericPrincipal(gi, new string[0]);
        }
        catch
        {
          return false;
        }
      }
      return UrlAuthorizationModule.CheckUrlAccessForPrincipal(CMS_Manager.TrimQuery(virtualPath), rp, string.Empty);
    }


    public static T FindControlRecurseT<T>(Control ctrlBase, string id) where T : class
    {
      return FindControlRecurse(ctrlBase, id) as T;
    }

    //
    // non ritorna mai un null
    //
    public static T FindControlRecurseTnn<T>(Control ctrlBase, string id) where T : class, new()
    {
      T ctrl = FindControlRecurseT<T>(ctrlBase, id);
      if (ctrl == null)
        ctrl = new T();
      return ctrl;
    }

    public static Control FindControlRecurse(Control ctrlBase, string id)
    {
      Control ctrl = ctrlBase.FindControl(id);
      if (ctrl == null)
      {
        foreach (Control c in ctrlBase.Controls)
        {
          ctrl = FindControlRecurse(c, id);
          if (ctrl != null)
            return ctrl;
        }
      }
      return ctrl;
    }

    //
    // prova a trovare il controllo cha ha generato il postback
    // uso Control invece di Page per applicarlo anche agli usercontrol e similari
    //
    public static Control FindPostBackSender(Control page) { return FindPostBackSender(page, null, null); }
    public static Control FindPostBackSender(Control page, string regExID) { return FindPostBackSender(page, null, regExID); }
    public static Control FindPostBackSender(Control page, Type tp) { return FindPostBackSender(page, tp, null); }
    public static Control FindPostBackSender(Control page, Type tp, string regExID)
    {
      List<string> ctrls = new List<string>();
      if (!string.IsNullOrEmpty(HttpContext.Current.Request.Form["__EVENTTARGET"]))
        ctrls.Add(HttpContext.Current.Request.Form["__EVENTTARGET"]);
      foreach (string k in HttpContext.Current.Request.Form.AllKeys)
      {
        if (string.IsNullOrEmpty(k) || k.StartsWith("__"))
          continue;
        string ctrl_name = k;
        if (ctrl_name.EndsWith(".x") || ctrl_name.EndsWith(".y"))
          ctrl_name = ctrl_name.Substring(0, ctrl_name.Length - 2);
        ctrls.Add(ctrl_name);
      }
      ctrls = ctrls.Distinct().ToList();
      Control ctrl = null;
      foreach (string ctrl_name in ctrls)
      {
        Control c = FindPostBackSenderWorker(page, ctrl_name, ctrl_name.Replace(':', '$'));
        if (c == null)
          continue;
        Type tpc = c.GetType();
        if (tp != null && tpc != tp)
          continue;
        if (!string.IsNullOrEmpty(regExID) && !Regex.IsMatch(c.ID, regExID))
          continue;
        ctrl = c;
        break;
      }
      return ctrl;
    }

    public static Control FindPostBackSenderWorker(Control container, string ctrl_name1, string ctrl_name2)
    {
      if (container == null || (string.IsNullOrEmpty(ctrl_name1) && string.IsNullOrEmpty(ctrl_name2)))
        return null;
      if (container.ID == ctrl_name1 || container.ID == ctrl_name2)
        return container;
      if (container.UniqueID == ctrl_name1 || container.UniqueID == ctrl_name2)
        return container;
      //if (!container.HasControls())
      //  return null;
      foreach (Control ctrl in container.Controls)
      {
        Control ctrl_found = FindPostBackSenderWorker(ctrl, ctrl_name1, ctrl_name2);
        if (ctrl_found != null)
          return ctrl_found;
      }
      return null;
    }

    public static WebControl AddCssClass(this WebControl ctrl, string className)
    {
      List<string> classes = Utility.Explode(ctrl.CssClass, " ", " ", true);
      if (!classes.Contains(className))
      {
        classes.Add(className);
        ctrl.CssClass = Utility.Implode(classes, " ");
      }
      return ctrl;
    }
    public static WebControl RemoveCssClass(this WebControl ctrl, string className)
    {
      List<string> classes = Utility.Explode(ctrl.CssClass, " ", " ", true);
      if (classes.Contains(className))
      {
        classes.Remove(className);
        ctrl.CssClass = Utility.Implode(classes, " ");
      }
      return ctrl;
    }
    public static WebControl ToggleCssClass(this WebControl ctrl, string className) { return ctrl.ToggleCssClass(className, null); }
    public static WebControl ToggleCssClass(this WebControl ctrl, string className, bool? flag)
    {
      List<string> classes = Utility.Explode(ctrl.CssClass, " ", " ", true);
      if (classes.Contains(className))
      {
        if (flag != true)
        {
          classes.Remove(className);
          ctrl.CssClass = Utility.Implode(classes, " ");
        }
      }
      else if (flag != false)
      {
        classes.Add(className);
        ctrl.CssClass = Utility.Implode(classes, " ");
      }
      return ctrl;
    }


    public static XElement AddAttributeFragment(this XElement xElem, string attributeName, string attributeFrag)
    {
      List<string> frags = Utility.Explode(xElem.AttributeValue(attributeName), " ", " ", true);
      if (!frags.Contains(attributeFrag))
      {
        frags.Add(attributeFrag);
        xElem.SetAttributeValue(attributeName, Utility.Implode(frags, " "));
      }
      return xElem;
    }
    public static XElement RemoveAttributeFragment(this XElement xElem, string attributeName, string attributeFrag)
    {
      List<string> frags = Utility.Explode(xElem.AttributeValue(attributeName), " ", " ", true);
      if (frags.Contains(attributeFrag))
      {
        frags.Remove(attributeFrag);
        xElem.SetAttributeValue(attributeName, Utility.Implode(frags, " "));
      }
      return xElem;
    }
    public static XElement ToggleCssClass(this XElement xElem, string attributeName, string attributeFrag) { return xElem.ToggleCssClass(attributeName, attributeFrag, null); }
    public static XElement ToggleCssClass(this XElement xElem, string attributeName, string attributeFrag, bool? flag)
    {
      List<string> frags = Utility.Explode(xElem.AttributeValue(attributeName), " ", " ", true);
      if (frags.Contains(attributeFrag))
      {
        if (flag != true)
        {
          frags.Remove(attributeFrag);
          xElem.SetAttributeValue(attributeName, Utility.Implode(frags, " "));
        }
      }
      else if (flag != false)
      {
        frags.Add(attributeFrag);
        xElem.SetAttributeValue(attributeName, Utility.Implode(frags, " "));
      }
      return xElem;
    }


    //
    // ricerca ricorsiva di tutti i controlli con un certo tipo e ID
    // regExID e controlType possono essere nulli
    // ctrl puo' essere anche la pagina stessa
    //
    public static List<Control> FindAllControlsMatching(Control ctrl, Regex regExID, Type controlType)
    {
      List<Control> controls = new List<Control>();
      FindAllControlsMatchingRecursive(controls, ctrl, regExID, controlType);
      return controls;
    }
    public static void FindAllControlsMatchingRecursive(List<Control> controls, Control ctrl, Regex regExID, Type controlType)
    {
      if (controls == null || ctrl == null)
        return;
      if (ctrl.HasControls())
        foreach (Control c in ctrl.Controls)
          FindAllControlsMatchingRecursive(controls, c, regExID, controlType);
      string id = ctrl.ID ?? string.Empty;  // boh? in alcuni casi ritorna un null...
      if (regExID != null)
        if (!regExID.IsMatch(id))
          return;
      // verifica del tipo
      if (controlType != null && !ctrl.GetType().IsAssignableFrom(controlType))
        return;
      controls.Add(ctrl);
    }

    //
    // ricerca ricorsiva di tutti i controlli mediante lambda expressions
    // attensione che spesso nella ricorsione troviamo dei Literal con ID null
    // eg:  FindAllControlsWhere(Page, c => (c is TextBox && c.ID.StartsWith("input_")))
    //
    public static List<Control> FindAllControlsWhere(Control ctrl, Func<Control, bool> predicate)
    {
      List<Control> controls = new List<Control>();
      FindAllControlsWhereRecursive(controls, ctrl, predicate);
      return controls;
    }
    public static void FindAllControlsWhereRecursive(List<Control> controls, Control ctrl, Func<Control, bool> predicate)
    {
      if (controls == null || ctrl == null || predicate == null)
        return;
      if (ctrl.HasControls())
        foreach (Control c in ctrl.Controls)
          FindAllControlsWhereRecursive(controls, c, predicate);
      if (predicate.Invoke(ctrl))
        controls.Add(ctrl);
    }

    //
    // versione con supporto completo dei generics
    //
    public static List<T> FindAllControlsWhere<T>(Control ctrl, Func<T, bool> predicate) where T : class
    {
      List<T> controls = new List<T>();
      FindAllControlsWhereRecursive<T>(controls, ctrl, predicate);
      return controls;
    }
    public static void FindAllControlsWhereRecursive<T>(List<T> controls, Control ctrl, Func<T, bool> predicate) where T : class
    {
      if (controls == null || ctrl == null || predicate == null)
        return;
      if (ctrl.HasControls())
        foreach (Control c in ctrl.Controls)
          FindAllControlsWhereRecursive<T>(controls, c, predicate);
      if (!(ctrl is T))
        return;
      if (predicate.Invoke(ctrl as T))
        controls.Add(ctrl as T);
    }

    //
    // versione generica con iteratore ricorsivo per LINQ
    //
    public static IEnumerable<T> FindAllControlsLINQ<T>(Control ctrl) where T : class
    {
      if (ctrl == null)
        yield break;
      if (ctrl is T)
        yield return ctrl as T;
      if (ctrl.HasControls())
      {
        foreach (Control c in ctrl.Controls)
        {
          // eseguo un minimo di ottimizzazione per limitare un po' le ricorsioni
          if (c == null)
            continue;
          if (c.HasControls())
            foreach (T r in FindAllControlsLINQ<T>(c))
              yield return r as T;
          else if (c is T)
            yield return c as T;
        }
      }
    }
    public static IEnumerable<T> FindAllParentsLINQ<T>(Control ctrl) where T : class
    {
      if (ctrl == null)
        yield break;
      if (ctrl is T)
        yield return ctrl as T;
      Control c = ctrl;
      while ((c = c.Parent) != null)
      {
        if (c == null)
          yield break;
        else if (c is T)
          yield return c as T;
      }
    }


    //
    // disabilita ricorsivamente il viewstate per una gerarchia di controlli
    //
    public static void DisableViewStateRecursive(Control ctrl, bool selfDisable)
    {
      if (selfDisable)
        ctrl.EnableViewState = false;
      if (ctrl.HasControls())
        foreach (Control c in ctrl.Controls)
          DisableViewStateRecursive(c, true);
    }


    public static PropertyInfo HasPropertyExt<T>(object source, string propertyName)
    {
      PropertyInfo pi = null;
      try
      {
        Type ty = source.GetType();
        try { pi = ty.GetProperty(propertyName, typeof(T)) ?? ty.GetProperty(propertyName); }
        catch { pi = ty.GetProperties().FirstOrDefault(p => p.Name == propertyName); }
      }
      catch { }
      return pi;
    }


    public static bool HasProperty<T>(object source, string propertyName)
    {
      try
      {
        PropertyInfo pi = null;
        Type ty = source.GetType();
        try { pi = ty.GetProperty(propertyName, typeof(T)) ?? ty.GetProperty(propertyName); }
        catch { pi = ty.GetProperties().FirstOrDefault(p => p.Name == propertyName); }
        return (pi != null);
      }
      catch { }
      return false;
    }

    //
    // legge una proprieta' senza incazzarsi
    //
    public static T FindPropertySafe<T>(object source, string propertyName)
    {
      try
      {
        PropertyInfo pi = null;
        Type ty = source.GetType();
        try { pi = ty.GetProperty(propertyName, typeof(T)) ?? ty.GetProperty(propertyName); }
        catch { pi = ty.GetProperties().FirstOrDefault(p => p.Name == propertyName); }
        if (pi != null)
        {
          //return (T)pi.GetValue(source, index);
          return (T)pi.GetValue(source, null);
        }
      }
      catch { }
      return default(T);
    }

    public static bool SetPropertySafe<T>(object dest, string propertyName, T value)
    {
      try
      {
        PropertyInfo pi = null;
        Type ty = dest.GetType();
        try { pi = ty.GetProperty(propertyName, typeof(T)) ?? ty.GetProperty(propertyName); }
        catch { pi = ty.GetProperties().FirstOrDefault(p => p.Name == propertyName); }
        if (pi != null && pi.CanWrite)
        {
          pi.SetValue(dest, value, null);
          return true;
        }
      }
      catch { }
      return false;
    }

    public static bool SetPropertyAuto(object dest, string propertyName, object value)
    {
      try
      {
        PropertyInfo pi = null;
        Type ty = dest.GetType();
        try { pi = ty.GetProperty(propertyName); }
        catch { pi = ty.GetProperties().FirstOrDefault(p => p.Name == propertyName); }
        if (pi != null && pi.CanWrite)
        {
          object valueNew = Convert.ChangeType(value, pi.PropertyType);
          pi.SetValue(dest, valueNew, null);
          return true;
        }
      }
      catch { }
      return false;
    }


    public static T FindPropertyStatic<T>(Type ty, string propertyName)
    {
      try
      {
        PropertyInfo pi = ty.GetProperty(propertyName);
        if (pi != null)
          return (T)pi.GetValue(null, null);
      }
      catch { }
      return default(T);
    }

    public static object InvokeVoidMethod(object obj, string methodName)
    {
      try
      {
        MethodInfo mi = obj.GetType().GetMethod(methodName);
        if (mi != null)
          return mi.Invoke(obj, null);
      }
      catch { }
      return null;
    }

    public static string GetClassDescription(Type ty)
    {
      //DescriptionAttribute desc = ty.GetCustomAttributes(typeof(DescriptionAttribute), true).FirstOrDefault() as DescriptionAttribute;
      DescriptionAttribute desc = GetAttributesFromType<DescriptionAttribute>(ty).FirstOrDefault();
      return (desc != null) ? desc.Description : string.Empty;
    }


    public static IEnumerable<T> GetAttributesFromObject<T>(object obj) where T : _Attribute { return GetAttributesFromType<T>(obj.GetType()); }
    public static IEnumerable<T> GetAttributesFromType<T>(Type type) where T : _Attribute
    {
      return type.GetCustomAttributes(typeof(T), true).OfType<T>();
    }


    public static object TryParseType(Type type, object data)
    {
      try { return TryParseMI.MakeGenericMethod(type).Invoke(null, new object[] { data }); }
      catch { return null; }
    }
    public static object TryParseType(Type type, object data, object defaultValue)
    {
      try { return TryParseMI2.MakeGenericMethod(type).Invoke(null, new object[] { data, defaultValue }); }
      catch { return defaultValue; }
    }


    //
    // prova a fare il parsing di un valore con una API piu' comoda del tryparse dei tipi e gestione dei nullable
    //
    public static T TryParse<T>(object data) { return TryParse<T>(data, default(T)); }
    public static T TryParse<T>(object data, object defaultValue)
    {
      if (data == null)
        return (T)defaultValue;
      Type type = typeof(T);
      if (data.GetType() == type)
        return (T)data;
      if (type.IsAssignableFrom(data.GetType()))
        return (T)data;
      if (IsNullableType(type))
        type = Nullable.GetUnderlyingType(type);
      if (data is string && (type == typeof(double) || type == typeof(float) || type == typeof(decimal)))
      {
        string num = (string)data;
        // elimino i separatori se questi sono multipli
        if (num.IndexOf(NumberFormatInfo.InvariantInfo.NumberGroupSeparator) != num.LastIndexOf(NumberFormatInfo.InvariantInfo.NumberGroupSeparator))
          num = num.Replace(NumberFormatInfo.InvariantInfo.NumberGroupSeparator, "");
        if (num.IndexOf(NumberFormatInfo.InvariantInfo.NumberDecimalSeparator) != num.LastIndexOf(NumberFormatInfo.InvariantInfo.NumberDecimalSeparator))
          num = num.Replace(NumberFormatInfo.InvariantInfo.NumberDecimalSeparator, "");
        int idx1 = num.IndexOf(NumberFormatInfo.InvariantInfo.NumberDecimalSeparator);
        int idx2 = num.IndexOf(NumberFormatInfo.InvariantInfo.NumberGroupSeparator);
        if (Math.Min(idx1, idx2) != -1)
        {
          if (idx1 < idx2)
            num = num.Replace(NumberFormatInfo.InvariantInfo.NumberDecimalSeparator, "");
          else
            num = num.Replace(NumberFormatInfo.InvariantInfo.NumberGroupSeparator, "");
        }
        if (num.IndexOf(NumberFormatInfo.InvariantInfo.NumberGroupSeparator) != -1)
          num = num.Replace(NumberFormatInfo.InvariantInfo.NumberGroupSeparator, NumberFormatInfo.InvariantInfo.NumberDecimalSeparator);
        object[] parameters = { num, NumberStyles.Any, NumberFormatInfo.InvariantInfo, defaultValue };
        if ((bool)type.InvokeMember("TryParse", BindingFlags.InvokeMethod, null, null, parameters, CultureInfo.InvariantCulture))
          return (T)parameters[3];
      }
      else if (data is string && type == typeof(DateTime))
      {
        try
        {
          DateTime dateOut;
          if (DateTime.TryParse((string)data, System.Globalization.DateTimeFormatInfo.CurrentInfo, System.Globalization.DateTimeStyles.None, out dateOut))
          {
            return (T)((object)dateOut);
          }
          else if (DateTime.TryParse((string)data, System.Globalization.DateTimeFormatInfo.InvariantInfo, System.Globalization.DateTimeStyles.None, out dateOut))
          {
            return (T)((object)dateOut);
          }
          return (T)((object)defaultValue);
        }
        catch { }
      }
      else
      {
        try
        {
          object[] parameters = { data, defaultValue };
          if ((bool)type.InvokeMember("TryParse", BindingFlags.InvokeMethod, null, null, parameters, CultureInfo.InvariantCulture))
            return (T)parameters[1];
        }
        catch { }
        // per i formati numerici
        try
        {
          object[] parameters = { data, NumberStyles.Any, NumberFormatInfo.InvariantInfo, defaultValue };
          if ((bool)type.InvokeMember("TryParse", BindingFlags.InvokeMethod, null, null, parameters, CultureInfo.InvariantCulture))
            return (T)parameters[3];
        }
        catch { }
      }
      return (T)defaultValue;
    }


    public static bool IsNullableType(Type type)
    {
      return (type.IsGenericType && type.GetGenericTypeDefinition().Equals(typeof(Nullable<>)));
    }


    public static string DateTimeStringSafe(string dateStr) { return DateTimeStringSafe(dateStr, null); }
    public static string DateTimeStringSafe(string dateStr, string format)
    {
      DateTime? dateParsed = Utility.TryParse<DateTime?>(dateStr);
      if (dateParsed == null)
        return null;
      if (format.IsNotEmpty())
        return dateParsed.Value.ToString(format);
      return dateParsed.Value.ToShortDateString();
    }


    //
    // estrazione dei valori da controllo UC_FramedDateTimeRange
    public static void GetDatesFromDateTimeRange(string range, out DateTime? fromDate, out DateTime? toDate)
    {
      fromDate = null;
      toDate = null;
      if (string.IsNullOrEmpty(range))
        return;

      string[] dates = range.Split('|');
      fromDate = Utility.TryParse<DateTime?>(dates[0], null);
      toDate = Utility.TryParse<DateTime?>(dates[1], null);
    }

    public static void GetDatesFromDateTimeRange(string range, out string fromDate, out string toDate)
    {
      DateTime? fromDT;
      DateTime? toDT;
      Utility.GetDatesFromDateTimeRange(range, out fromDT, out toDT);

      fromDate = null;
      toDate = null;
      fromDate = (fromDT.HasValue) ? fromDT.Value.ToShortDateString() : string.Empty;
      toDate = (toDT.HasValue) ? toDT.Value.ToShortDateString() : string.Empty;
    }

    //
    // normalizzazione delle date negli update tramite LINQ con date non ottimizzate
    //
    public static void NormalizeDatesOnChangeSetDB(System.Data.Linq.DataContext DB, bool inserts, bool updates)
    {
      if (DB != null)
      {
        var CS = DB.GetChangeSet();
        if (inserts)
          foreach (object entity in CS.Inserts)
            NormalizeDatesOnEntityDB(entity);
        if (updates)
          foreach (object entity in CS.Updates)
            NormalizeDatesOnEntityDB(entity);
      }
    }
    public static void NormalizeDatesOnEntitiesDB(IEnumerable entities)
    {
      foreach (object entity in entities)
        NormalizeDatesOnEntityDB(entity);
    }
    public static void NormalizeDatesOnEntityDB(object entity)
    {
      IEnumerable<PropertyInfo> props = entity.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance);
      foreach (PropertyInfo pi in props.Where(p => (Nullable.GetUnderlyingType(p.PropertyType) ?? p.PropertyType) == typeof(DateTime)))
      {
        DateTime? dt0 = (DateTime?)pi.GetValue(entity, null);
        if (dt0 != null)
        {
          DateTime dt1 = dt0.Value >= DateTimeMinValueDB ? dt0.Value : DateTimeMinValueDB;
          DateTime dt2 = dt1 <= DateTimeMaxValueDB ? dt1 : DateTimeMaxValueDB;
          if (dt0.Value != dt2)
            pi.SetValue(entity, dt2, null);
        }
      }
    }

    public static DateTime? GetNullDateIfInvalidDateDB(DateTime? date)
    {
      DateTime? result = date;
      if (date != null)
      {
        if (date <= DateTimeMinValueDB)
          result = null;
        else if (date >= DateTimeMaxValueDB)
          result = null;
      }
      return result;
    }


    public static DateTime DateTimeFromUnix(long unixTimeStamp)
    {
      System.DateTime dtDateTime = new DateTime(1970, 1, 1, 0, 0, 0, 0);
      dtDateTime = dtDateTime.AddSeconds(unixTimeStamp).ToLocalTime();
      return dtDateTime;
    }


    public static long DateTimeFromUnix(DateTime datetime)
    {
      System.DateTime dtDateTime = new DateTime(1970, 1, 1, 0, 0, 0, 0);
      long unixTimeStamp = (long)((datetime - dtDateTime).TotalSeconds);
      return unixTimeStamp;
    }


    public static DateTime DateTimeFromJava(long javaTimeStamp)
    {
      System.DateTime dtDateTime = new DateTime(1970, 1, 1, 0, 0, 0, 0);
      dtDateTime = dtDateTime.AddSeconds(Math.Round(javaTimeStamp / 1000.0)).ToLocalTime();
      return dtDateTime;
    }


    public static long DateTimeFromJava(DateTime datetime)
    {
      System.DateTime dtDateTime = new DateTime(1970, 1, 1, 0, 0, 0, 0);
      long javaTimeStamp = (long)((datetime - dtDateTime).TotalMilliseconds);
      return javaTimeStamp;
    }


    //
    // Max e Min generici
    //
    public static T Max<T>(T first, T second) where T : IComparable
    {
      var comparer = Comparer<T>.Default;
      return comparer.Compare(first, second) < 0 ? second : first;
    }

    public static T Min<T>(T first, T second) where T : IComparable
    {
      var comparer = Comparer<T>.Default;
      return comparer.Compare(first, second) > 0 ? second : first;
    }


    public static T MaxAll<T>(params T[] items) where T : IComparable
    {
      var comparer = Comparer<T>.Default;
      T result = items.FirstOrDefault();
      items.Skip(1).ForEach(r => result = comparer.Compare(r, result) < 0 ? result : r);
      return result;
    }

    public static T MinAll<T>(params T[] items) where T : IComparable
    {
      var comparer = Comparer<T>.Default;
      T result = items.FirstOrDefault();
      items.Skip(1).ForEach(r => result = comparer.Compare(r, result) > 0 ? result : r);
      return result;
    }


    //
    // Deep Clone di un oggetto generico
    //
    public static T DeepClone<T>(this T entity)
    {
      using (MemoryStream stream = new MemoryStream())
      {
        System.Runtime.Serialization.Formatters.Binary.BinaryFormatter formatter = new System.Runtime.Serialization.Formatters.Binary.BinaryFormatter();
        formatter.Serialize(stream, entity);
        stream.Position = 0;
        return (T)formatter.Deserialize(stream);
      }
    }


    //
    // clonazione di un record LINQ
    //
    public static T CloneEntity<T>(T entity) where T : class, new() { return CloneEntity(entity, false, true); }
    public static T CloneEntity<T>(T entity, bool copyAutoGenerated, bool copyForeignKeys) where T : class, new()
    {
      T clonedEntity = new T();
      if (entity != null)
      {
        IEnumerable<PropertyInfo> props = entity.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance);
        if (copyForeignKeys == false)
        {
          List<string> forbiddenProps = new List<string>();
          foreach (PropertyInfo prop in props)
            foreach (AssociationAttribute aa in prop.GetCustomAttributes(typeof(AssociationAttribute), false))
              if (aa.IsForeignKey && !string.IsNullOrEmpty(aa.ThisKey))
                forbiddenProps.Add(aa.ThisKey);
          props = props.Where(p => !forbiddenProps.Contains(p.Name));
        }
        if (copyAutoGenerated == false)
          props = props.Where(prop => prop.GetCustomAttributes(typeof(ColumnAttribute), false).OfType<ColumnAttribute>().Count(ca => ca.IsDbGenerated) == 0);
        props = props.Where(prop => prop.GetCustomAttributes(typeof(ColumnAttribute), false).Count() > 0);
        foreach (PropertyInfo prop in props)
          prop.SetValue(clonedEntity, prop.GetValue(entity, null), null);
      }
      else
      {
        // filtraggio sulle date che non vengono gestite bene da SQL
        DateTime dMin = DateTimeMinValueDB;
        DateTime dMax = DateTimeMaxValueDB;
        IEnumerable<PropertyInfo> props = clonedEntity.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance);
        foreach (PropertyInfo pi in props.Where(p => (Nullable.GetUnderlyingType(p.PropertyType) ?? p.PropertyType) == typeof(DateTime)))
        {
          DateTime? dt0 = (DateTime?)pi.GetValue(clonedEntity, null);
          if (dt0 != null)
          {
            DateTime dt1 = dt0.Value >= dMin ? dt0.Value : dMin;
            DateTime dt2 = dt1 <= dMax ? dt1 : dMax;
            if (dt0.Value != dt2)
              pi.SetValue(clonedEntity, dt2, null);
          }
        }
      }
      return clonedEntity;
    }


    public static void ClearCachedData(string prefix)
    {
      if (prefix.IsNotEmpty())
      {
        HttpRuntime.Cache.OfType<DictionaryEntry>().Where(c => c.Key is string && (c.Key as string).StartsWith(prefix)).Select(c => c.Key as string).ForEach(k => HttpRuntime.Cache.Remove(k));
      }
    }


    public static bool IsAssignableTo(this Type self, Type type)
    {
      return type.IsAssignableFrom(self);
    }


    private static List<Assembly> _cachedReferencedAssemblies = null;
    public static void ClearCachedApplicationReferencedAssemblies() { _cachedReferencedAssemblies = null; }
    public static IEnumerable<Assembly> GetApplicationReferencedAssemblies(bool includeGAC) { return GetApplicationReferencedAssemblies(includeGAC, false); }
    public static IEnumerable<Assembly> GetApplicationReferencedAssemblies(bool includeGAC, bool reset)
    {
      lock (typeof(Utility))
      {
        if (reset)
          _cachedReferencedAssemblies = null;
        if (_cachedReferencedAssemblies == null)
        {
          // questa chiamata benche' apparentemente inutile serve per fare in modo che il sistema possa vedere correttamente tutti gli assembly
          // registrati, altrimenti accade che dopo una ricompilazione se si killa il webserver e questo riparte (tipo riciclo)
          // non vede una parte degli assembly privati (tipo quelli dipendenti da gizmox*) e non trova tutti i tipi relativi
          // per cui provvediamo a chiamarla per poi semplicemente dimenticarcene e procedere con il processing regolare
          List<string> builtAssemblies = System.Web.Compilation.BuildManager.GetReferencedAssemblies().OfType<Assembly>().Select(a => a.FullName).OrderBy(a => a).ToList();
          //
          List<string> refs = AppDomain.CurrentDomain.GetAssemblies().Where(a => !a.GlobalAssemblyCache).SelectMany(a => a.GetReferencedAssemblies().Select(n => n.FullName)).Distinct().OrderBy(a => a).ToList();
          List<string> missingAsms = refs.Except(AppDomain.CurrentDomain.GetAssemblies().Select(a => a.FullName)).OrderBy(a => a).ToList();
          foreach (string asmName in missingAsms)
          {
            if (!AppDomain.CurrentDomain.GetAssemblies().Any(a => a.FullName == asmName))
            {
              try { Assembly.Load(asmName); }
              catch { }
            }
          }
          _cachedReferencedAssemblies = AppDomain.CurrentDomain.GetAssemblies().OrderBy(a => a.FullName).ToList();
        }
        if (includeGAC)
          return _cachedReferencedAssemblies;
        else
          return _cachedReferencedAssemblies.Where(a => !a.GlobalAssemblyCache);
      }
    }


    //
    // cerca un metodo dalla stringa del namespace [, assembly] (utile per le callback in libreria)
    //
    // piuttosto utilizzare System.Web.Compilation.BuildManager.GetType(typeName, throwExceptions);
    // che pero' non rileva gli assembly caricati dinamicamente
    //
    public static Type FindType(string firma, bool throwExceptions)
    {
      //return System.Web.Compilation.BuildManager.GetType(firma, throwExceptions);
      //
      Type ty = null;
      try
      {
        if (string.IsNullOrEmpty(firma))
          return ty;
        Assembly asm = null;
        int idx = firma.LastIndexOf(',');
        if (idx != -1)
        {
          asm = Assembly.Load(firma.Substring(idx + 1).Trim());
          firma = firma.Substring(0, idx);
          if (asm == null)
            throw new Exception("FindMethod: assembly not specified.");
        }
        firma = firma.Trim();
        List<Assembly> asms = GetApplicationReferencedAssemblies(true).ToList();
        //List<Assembly> asms = System.Web.Compilation.BuildManager.GetReferencedAssemblies().OfType<Assembly>().ToList();
        if (asm != null && !asms.Contains(asm))
          asms.Insert(0, asm);
        idx = firma.LastIndexOf('.');
        if (idx == -1)
          throw new Exception("FindMethod: no type given.");
        //Regex rx = new Regex("(^mscorlib$|^System.)", RegexOptions.IgnoreCase);
        foreach (Assembly a in asms)
        {
          //if (rx.IsMatch(a.GetName().Name))
          //  continue;
          if ((ty = a.GetType(firma, false, true)) != null)
            break;
        }
        if (ty == null)
          throw new Exception("FindType: Type not found.");
      }
      catch
      {
        if (throwExceptions)
          throw;
      }
      return ty;
    }


    //
    // versione di FindType con supporto per generics
    // poi usare Activator.CreateInstance(ty)
    //
    public static Type FindTypeGeneric(string typeName, params Type[] genericsParams)
    {
      Type ty = null;
      try
      {
        if (string.IsNullOrEmpty(typeName))
          return ty;
        try { ty = Type.GetType(typeName, false, false); }
        catch { }
        if (ty == null)
        {
          typeName = typeName.Split(',').FirstOrDefault().Trim();
          //
          ty = FindTypeCached(typeName, true);
          //
          // il vecchio approccio tendeva ad incasinarsi nel caso qualche assembly avesse dipendenze mancanti
          //List<Assembly> asms = GetApplicationReferencedAssemblies(false).ToList();
          //try { ty = ty ?? asms.SelectMany(a => a.GetTypes()).FirstOrDefault(t => t.FullName == typeName); }
          //catch (ReflectionTypeLoadException ex) { }
          //catch { }
          //try { ty = ty ?? asms.SelectMany(a => a.GetTypes()).FirstOrDefault(t => t.Name == typeName); }
          //catch (ReflectionTypeLoadException ex) { }
          //catch { }
        }
        if (ty != null && ty.ContainsGenericParameters && genericsParams.Length > 0)
        {
          ty = ty.MakeGenericType(genericsParams);
        }
      }
      catch { }
      return ty;
    }


    //
    // cerca un metodo dalla stringa del namespace [, assembly] (utile per le callback in libreria)
    //
    public static MethodInfo FindMethod(string firma, bool staticMethod, bool throwExceptions)
    {
      MethodInfo mi = null;
      try
      {
        if (string.IsNullOrEmpty(firma))
          return mi;
        Assembly asm = null;
        int idx = firma.LastIndexOf(',');
        if (idx != -1)
        {
          asm = Assembly.Load(firma.Substring(idx + 1).Trim());
          firma = firma.Substring(0, idx);
          if (asm == null)
            throw new Exception("FindMethod: assembly not specified.");
        }
        firma = firma.Trim();
        List<Assembly> asms = GetApplicationReferencedAssemblies(false).ToList();
        //List<Assembly> asms = System.Web.Compilation.BuildManager.GetReferencedAssemblies().OfType<Assembly>().ToList();
        if (asm != null && !asms.Contains(asm))
          asms.Insert(0, asm);
        idx = firma.LastIndexOf('.');
        if (idx == -1)
          throw new Exception("FindMethod: no type given.");
        string cb_ty = firma.Substring(0, idx);
        string cb_mt = firma.Substring(idx + 1);
        //Regex rx = new Regex("(^mscorlib$|^System.)", RegexOptions.IgnoreCase);
        Type tp = null;
        foreach (Assembly a in asms)
        {
          //if (rx.IsMatch(a.GetName().Name))
          //  continue;
          if ((tp = a.GetType(cb_ty, false, true)) != null)
            break;
        }
        if (tp == null)
          throw new Exception("FindMethod: Type not found.");
        BindingFlags bf = BindingFlags.Public;
        if (staticMethod)
          bf |= BindingFlags.Static;
        mi = tp.GetMethod(cb_mt, bf);
        if (mi == null)
          throw new Exception("FindMethod: method not found.");
      }
      catch
      {
        if (throwExceptions)
          throw;
      }
      return mi;
    }


    //
    // ottiene una lista di tipi (tra gli assembly registrati) che implementano tutte le interfacce specificate come argomento
    //
    public static IEnumerable<Type> FindTypesWithInterfaces(params Type[] interfaceTypes) { return FindTypesWithInterfaces(false, interfaceTypes); }
    public static IEnumerable<Type> FindTypesWithInterfaces(bool? includeGAC, params Type[] interfaceTypes)
    {
      if (interfaceTypes == null || interfaceTypes.Length == 0)
        throw new ArgumentException("interfaceTypes null or void");
      int inCount = interfaceTypes.Length;
      foreach (Assembly asm in GetApplicationReferencedAssemblies(includeGAC ?? false))
      {
        if (includeGAC != null && asm.GlobalAssemblyCache != includeGAC.Value)
          continue;
        IEnumerable<Type> types = null;
        try { types = asm.GetTypes(); }
        catch { }
        if (types != null)
        {
          foreach (Type ty in types.Where(t => t.IsClass))
          {
            if (interfaceTypes.Intersect(ty.GetInterfaces()).Count() != inCount)
              continue;
            //TODO: verificare yield con inizializzazione pesante
            yield return ty;
          }
        }
      }
    }

    //
    // cerca un tipo (senza namespace) con la possibilita' di specificare una serie di interfacce per il filtraggio
    //
    public static Type FindType(string TypeName, params Type[] interfaces)
    {
      int inCount = interfaces.Count();
      //foreach (Assembly asm in System.Web.Compilation.BuildManager.GetReferencedAssemblies())
      foreach (Assembly asm in GetApplicationReferencedAssemblies(false))
      {
        IEnumerable<Type> types = null;
        try { types = asm.GetTypes(); }
        catch { }
        if (types != null)
        {
          foreach (Type ty in types.Where(t => t.IsClass && t.Name == TypeName))
            if (interfaces.Length == 0 || ty.GetInterfaces().Intersect(interfaces).Count() == inCount)
              return ty;
        }
      }
      return null;
    }


    public static IEnumerable<Type> FindTypes(string TypeName, params Type[] interfaces)
    {
      int inCount = interfaces.Count();
      //foreach (Assembly asm in System.Web.Compilation.BuildManager.GetReferencedAssemblies())
      foreach (Assembly asm in GetApplicationReferencedAssemblies(false))
      {
        IEnumerable<Type> types = null;
        try { types = asm.GetTypes(); }
        catch { }
        if (types != null)
        {
          foreach (Type ty in types.Where(t => t.IsClass && t.Name == TypeName))
            if (interfaces.Length == 0 || ty.GetInterfaces().Intersect(interfaces).Count() == inCount)
              yield return ty;
        }
      }
    }


    public static Type FindInterface(string TypeName)
    {
      foreach (Assembly asm in GetApplicationReferencedAssemblies(false))
      {
        IEnumerable<Type> types = null;
        try { types = asm.GetTypes(); }
        catch { }
        if (types != null)
        {
          Type type = types.FirstOrDefault(t => t.IsInterface && t.Name == TypeName);
          if (type != null)
            return type;
        }
      }
      return null;
    }


    private static Dictionary<string, Type> _TypeCacheDictionary = new Dictionary<string, Type>();
    public static Type FindTypeCached(string TypeName) { return FindTypeCached(TypeName, false); }
    public static Type FindTypeCached(string TypeName, bool includeGAC)
    {
      try
      {
        if (!_TypeCacheDictionary.ContainsKey(TypeName))
        {
          foreach (Assembly asm in GetApplicationReferencedAssemblies(includeGAC))
          {
            IEnumerable<Type> types = null;
            try { types = asm.GetTypes(); }
            catch { }
            if (types != null && types.Any(t => t.IsClass && t.Name == TypeName))
            {
              _TypeCacheDictionary[TypeName] = types.Where(t => t.IsClass && t.Name == TypeName).FirstOrDefault();
              break;
            }
          }
        }
        return _TypeCacheDictionary[TypeName];
      }
      catch { return null; }
    }


    public static IEnumerable<Type> FindTypesDerivedFrom(bool? includeGAC, string baseTypeName) { return FindTypesDerivedFrom(includeGAC, FindTypeCachedExt(baseTypeName, includeGAC.GetValueOrDefault(false))); }
    public static IEnumerable<Type> FindTypesDerivedFrom(bool? includeGAC, Type baseType)
    {
      if (baseType == null)
        yield break;
      if (baseType.IsClass)
        yield return baseType;
      foreach (Assembly asm in GetApplicationReferencedAssemblies(includeGAC ?? false))
      {
        if (includeGAC != null && asm.GlobalAssemblyCache != includeGAC.Value)
          continue;
        IEnumerable<Type> types = null;
        try { types = asm.GetTypes(); }
        catch { }
        if (types != null)
        {
          //TODO: verificare yield con inizializzazione pesante
          if (baseType.IsInterface)
          {
            foreach (Type ty in types.Where(t => t.IsClass && t.GetInterfaces().Contains(baseType)))
              yield return ty;
          }
          else
          {
            foreach (Type ty in types.Where(t => t.IsClass && baseType.IsSubclassOf(t)))
              yield return ty;
          }
        }
      }
    }


    //
    // questa versione e' piu' lenta ma accetta anche classi nested (da usare con il + invece di . come nesting operator)
    //
    public static Type FindTypeCachedExt(string TypeName) { return FindTypeCachedExt(TypeName, false); }
    public static Type FindTypeCachedExt(string TypeName, bool includeGAC)
    {
      try
      {
        if (!_TypeCacheDictionary.ContainsKey(TypeName))
        {
          string declaringTypeName = TypeName.Split('+').FirstOrDefault();
          string nestedTypeName = TypeName.Split('+').Skip(1).FirstOrDefault();
          foreach (Assembly asm in GetApplicationReferencedAssemblies(includeGAC))
          {
            IEnumerable<Type> types = null;
            try { types = asm.GetTypes(); }
            catch { }
            if (types == null)
              continue;
            Type ty = null;
            var typesMatching = types.Where(t => t.FullName == declaringTypeName || t.FullName.EndsWith("." + declaringTypeName));
            if (nestedTypeName != null)
            {
              ty = typesMatching.Select(t => t.GetNestedType(nestedTypeName)).Where(t => t != null).FirstOrDefault();
            }
            else
            {
              ty = typesMatching.FirstOrDefault();
            }
            if (ty != null)
            {
              _TypeCacheDictionary[TypeName] = ty;
              break;
            }
          }
        }
        return _TypeCacheDictionary[TypeName];
      }
      catch { return null; }
    }


    //
    // cerca un type (senza namespace) con la possibilita' di specificare una serie di interfacce per il filtraggio
    //
    public static bool HasInterface(object obj, Type interfaceType) { return HasInterface(obj.GetType(), interfaceType); }
    public static bool HasInterface(Type type, Type interfaceType)
    {
      return type.GetInterface(interfaceType.Name) != null;
    }

    //
    // funzione ausiliaria per una specie di copia per riferimento (C++) con la reflection
    //
    // eg. nei costruttori di copia per riferimento (che in C# non ci sono...)
    // object _this = this; // NB. this e' readonly
    // Utility.ReferenceCopyContructorWorker(ref _this, rhs);
    //
    public static void ReferenceCopyContructorWorker<T>(ref T dst, object rhs)
    {
      Type dst_type = rhs.GetType();
      while (dst_type != typeof(object))
      {
        foreach (FieldInfo fi in dst_type.GetFields(BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance))
          fi.SetValue(dst, fi.GetValue(rhs));
        dst_type = dst_type.BaseType;
      }
    }

    //
    // clonazione delle proprieta' di un WebControl per duplicare anche il ViewState
    //
    public static void WebControlCloneProperties<T>(ref T dst, object rhs) { WebControlCloneProperties(ref dst, rhs, string.Empty); }
    public static void WebControlCloneProperties<T>(ref T dst, object rhs, string PropsToSkip)
    {
      List<string> PropsToSkipList = Explode(PropsToSkip, ",", null, true);
      Type dst_type = rhs.GetType();
      while (dst_type != typeof(Control))
      {
        foreach (PropertyInfo pi in dst_type.GetProperties())
        {
          switch (pi.Name)
          {
            // proprieta' uniche
            case "ID":
            case "ClientID":
            case "UniqueID":
              continue;
            // proprieta' che devono essere settate prima dell'aggiunta Controls
            case "EnableTheming":
            case "SkinID":
              continue;
          }
          if (PropsToSkipList.Contains(pi.Name))
            continue;
          if (pi.CanRead && pi.CanWrite)
            pi.SetValue(dst, pi.GetValue(rhs, null), null);
        }
        dst_type = dst_type.BaseType;
      }
    }

    //
    // clonazione delle proprieta' di una specifica classe tra un oggetto ed un altro
    //
    public static void WebControlCloneProperties<T>(ref T dst, object rhs, Type ref_class) { WebControlCloneProperties(ref dst, rhs, ref_class, string.Empty); }
    public static void WebControlCloneProperties<T>(ref T dst, object rhs, Type ref_class, string PropsToSkip)
    {
      List<string> PropsToSkipList = Explode(PropsToSkip, ",", null, true);
      Type type_src = rhs.GetType();
      Type type_dst = dst.GetType();
      foreach (PropertyInfo pi in ref_class.GetProperties())
      {
        try
        {
          switch (pi.Name)
          {
            // proprieta' uniche
            case "ID":
            case "ClientID":
            case "UniqueID":
              continue;
            // proprieta' che devono essere settate prima dell'aggiunta Controls
            case "EnableTheming":
            case "SkinID":
              continue;
          }
          if (PropsToSkipList.Contains(pi.Name))
            continue;
          PropertyInfo pi_s = type_src.GetProperty(pi.Name);
          PropertyInfo pi_d = type_dst.GetProperty(pi.Name);
          if (pi_s == null || pi_d == null)
            continue;
          if (pi.CanRead && pi.CanWrite)
            pi.SetValue(dst, pi.GetValue(rhs, null), null);
        }
        catch { }
      }
    }

    //
    // aggiunge un trucco per evitare il doppio click su un pulsante
    //
    public static void NoDoubleClick(WebControl btn, string newText)
    {
      ButtonExtender(btn, newText, null, false);
      //if (btn == null)
      //  return;
      //string jsPB = btn.Page.ClientScript.GetPostBackEventReference(btn, null) + ";";
      //string jsTX = string.IsNullOrEmpty(newText) ? "" : string.Format(" this.value = '{0}'; ", newText);
      //string js = " this.disabled = true; " + jsTX + jsPB;
      //btn.Attributes.Add("onclick", js);
    }

    public static void ButtonExtender(WebControl btn, string disabledButtonText, string popupMessage, bool isConfirm)
    {
      if (btn == null)
        return;
      bool autoDisable = !string.IsNullOrEmpty(disabledButtonText);
      disabledButtonText = disabledButtonText ?? string.Empty;
      disabledButtonText = disabledButtonText.Replace(@"'", @"\'");
      if (!string.IsNullOrEmpty(popupMessage))
      {
        if (isConfirm)
          popupMessage = string.Format("if (confirm('{0}')!=true) return false; ", popupMessage.Replace(@"'", @"\'"));  // quoting per JS
        else
          popupMessage = string.Format("alert('{0}'); ", popupMessage.Replace(@"'", @"\'"));  // quoting per JS
      }
      //string jsCode = btn.Page.ClientScript.GetPostBackEventReference(btn, null) + ";";
      List<string> jsCodes = new List<string>();
      if (!string.IsNullOrEmpty(disabledButtonText))
        jsCodes.Insert(0, string.Format("this.value='{0}'", disabledButtonText));
      if (autoDisable)
        jsCodes.Insert(0, "this.disabled=true");
      if (!string.IsNullOrEmpty(popupMessage))
        jsCodes.Insert(0, popupMessage);
      // commentato altrimenti viene inserito doppio nel caso il pulsante abbia settato UseSubmitBehavior="false"
      if (btn is Button && ((Button)btn).UseSubmitBehavior == false)
      {
        //niente...
      }
      else
      {
        jsCodes.Add(btn.Page.ClientScript.GetPostBackEventReference(btn, null));
      }
      string jsCode = Utility.Implode(jsCodes, "; ");
      btn.Attributes.Add("onclick", jsCode);
    }


    private static Regex _rxCheckBotDefault = new Regex(@"(bot|spider|crawl|slurp)", RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.Singleline);
    private static Regex _rxCheckMobileBotDefault = new Regex(@"(mobile)", RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.Singleline);
    public static bool CheckIfBOT() { return CheckIfBOT(_rxCheckBotDefault); }
    public static bool CheckIfBOT(Regex rxCheckBot)
    {
      bool isBot = false;
      try
      {
        string userAgent = null;
        try { userAgent = HttpContext.Current.Request.UserAgent; }
        catch { }
        userAgent = userAgent ?? string.Empty;
        if (rxCheckBot != null)
        {
          isBot = rxCheckBot.IsMatch(userAgent);
        }
        else
        {
          isBot = userAgent.IndexOf("bot", StringComparison.OrdinalIgnoreCase) >= 0;
        }
      }
      catch { }
      return isBot;
    }


    public static bool CheckIfMobileBOT()
    {
      bool isBot = CheckIfBOT(_rxCheckBotDefault);
      if (isBot)
      {
        string userAgent = null;
        try { userAgent = HttpContext.Current.Request.UserAgent; }
        catch { }
        userAgent = userAgent ?? string.Empty;
        isBot &= _rxCheckMobileBotDefault.IsMatch(userAgent);
      }
      return isBot;
    }


    //
    // key == null --> non utilizza le subkey
    // value == null --> cancella la cookie
    //
    //public static void CookieUpdateKeyValueExt(string cookieName, string key, string value)
    //{
    //  if (!string.IsNullOrEmpty(cookieName))
    //    return;
    //  var currentCookies = HttpContext.Current.Request.Cookies.LINQ().Where(c => c.Name == cookieName).ToList();
    //}


    //
    // registra una key in una cookie esistente (mantenendo inalterate le altre key)
    // nel caso non esista crea una nuova cookie temporanea con path / e dominio esteso a tutto il dominio di II livello
    //
    public static void CookieUpdateKeyValue(string cookieName, string key, string value, bool register)
    {
      //HttpCookie newCookie = HttpContext.Current.Request.Cookies[cookieName];
      HttpCookie newCookie = null;
      try
      {
        newCookie = HttpContext.Current.Response.Cookies.LINQ().LastOrDefault(r => r.Name == cookieName);
        if (newCookie == null)
        {
          newCookie = HttpContext.Current.Request.Cookies.LINQ().LastOrDefault(r => r.Name == cookieName);
        }
      }
      catch { }
      if (newCookie == null)
      {
        newCookie = new HttpCookie(cookieName);
        newCookie.Path = "/";
        newCookie.HttpOnly = true;
      }
      if (value == null)
      {
        newCookie.Values.Remove(key);
      }
      newCookie[key] = value;
      CookieSetDomainAuto(newCookie, null);  // deve essere settato sempre non viene settato nei dati della cookie importata
      //
      if (register)
      {
        try
        {
          HttpContext.Current.Request.Cookies.Set(newCookie);  // per l'uso nella request corrente
          HttpContext.Current.Response.Cookies.Set(newCookie);  // per la registrazione
        }
        catch { }
      }
    }


    // estrae il dominio principale sul quale attivare la cookie
    // non viene attivato il constrain sul dominio se e' stata specificata una porta nella uri (development)
    public static void CookieSetDomainAuto(HttpCookie cookie, int? fragsToKeep)
    {
      try
      {
        if (HttpContext.Current.Request.Url.HostNameType == UriHostNameType.Dns && HttpContext.Current.Request.Url.IsDefaultPort)
        {
          if (fragsToKeep == null)
            fragsToKeep = Utility.TryParse<int>(WebConfigurationManager.AppSettings["CookieDomainFragmentsCount"], 999);
          if (fragsToKeep.Value < 999)
          {
            fragsToKeep = Math.Max(fragsToKeep.Value, 2);
            List<string> frags = HttpContext.Current.Request.Url.Host.Split('.').ToList();
            string domain = Regex.Replace(string.Join(".", frags.Select((f, i) => (i < frags.Count - fragsToKeep.Value) ? string.Empty : f).ToArray()), @"\.{2,}", ".");
            if (!string.Equals(HttpContext.Current.Request.Url.Host, domain, StringComparison.OrdinalIgnoreCase))
              cookie.Domain = domain;
          }
          /*
          List<string> frags = HttpContext.Current.Request.Url.Host.Split('.').ToList();
          fragsToKeep = Math.Max(fragsToKeep.GetValueOrDefault(0), Utility.TryParse<int>(WebConfigurationManager.AppSettings["CookieDomainFragmentsCount"], 100));
          // uso >= per attivare il filtro per i domini di II livello
          // sembra ci sia un problema con le cookie che non possono accettare domini di primo livello (x tutti i browser)???
          if (frags.Count >= fragsToKeep.Value)
          {
            string domain = string.Join(".", frags.SkipWhile((s, i) => i < frags.Count - fragsToKeep.Value).ToArray()).NullIfEmpty();
            if (domain != null)
              domain = domain + ".";
            cookie.Domain = domain;
          }
          */
        }
      }
      catch { }
    }


    public static string Value(this HttpCookieCollection cookies, string cookieName) { try { return cookies.LINQ().LastOrDefault(c => c.Name == cookieName).Value; } catch { return null; } }
    public static string Value(this HttpCookieCollection cookies, string cookieName, string key) { try { return cookies.LINQ().LastOrDefault(c => c.Name == cookieName)[key]; } catch { return null; } }
    public static IEnumerable<HttpCookie> LINQ(this HttpCookieCollection cookies)
    {
      if (cookies == null)
        yield break;
      for (int idx = 0; idx < cookies.Count; idx++)
        yield return cookies[idx];
    }


    public static void CookieRemoveFromCurrentRequest(string cookieName)
    {
      //while (HttpContext.Current.Request.Cookies[cookieName] != null)
      //  HttpContext.Current.Request.Cookies.Remove(cookieName);
    }


    public static void CookieRemove(string cookieName)
    {
      try
      {
        HttpCookie aux_ck = HttpContext.Current.Request.Cookies[cookieName] ?? HttpContext.Current.Response.Cookies[cookieName];
        if (aux_ck != null)
        {
          aux_ck.Expires = DateTime.Now.AddYears(-1);
          CookieSetDomainAuto(aux_ck, null);
          HttpContext.Current.Response.Cookies.Add(aux_ck);
        }
      }
      catch { }
    }


    //
    // random strings and hashing
    //
    // ritorna una stringa casuale di lunghezza fissata
    public static string RandomString(int length)
    {
      string tempString = string.Empty;
      while (tempString.Length < length)
        tempString += Guid.NewGuid().ToString().ToLower().Replace("-", "");
      tempString = tempString.Substring(0, length);
      return tempString;
    }

    //
    // conversione di una stringa da/a UTF8(default)
    //
    public static string stringEncodeToUTF8(string encodingStr, string data)
    {
      Encoding encoding = Encoding.GetEncoding(encodingStr);
      string res = Encoding.UTF8.GetString(Encoding.Convert(encoding, Encoding.UTF8, encoding.GetBytes(data)));
      return res;
    }

    public static string stringEncodeFromUTF8(string encodingStr, string data)
    {
      Encoding encoding = Encoding.GetEncoding(encodingStr);
      string res = encoding.GetString(Encoding.Convert(Encoding.UTF8, encoding, Encoding.UTF8.GetBytes(data)));
      return res;
    }


    public static string EncodeJavascriptString(string s)
    {
      StringBuilder sb = new StringBuilder();
      //sb.Append("\"");
      foreach (char c in s)
      {
        switch (c)
        {
          case '\"':
            sb.Append("\\\"");
            break;
          case '\\':
            sb.Append("\\\\");
            break;
          case '\b':
            sb.Append("\\b");
            break;
          case '\f':
            sb.Append("\\f");
            break;
          case '\n':
            sb.Append("\\n");
            break;
          case '\r':
            sb.Append("\\r");
            break;
          case '\t':
            sb.Append("\\t");
            break;
          default:
            int i = (int)c;
            if (i < 32 || i > 127)
            {
              sb.AppendFormat("\\u{0:X04}", i);
            }
            else
            {
              sb.Append(c);
            }
            break;
        }
      }
      //sb.Append("\"");
      //
      return sb.ToString();
    }


    //
    // Gets the MD5 or SHA1 hash for a string
    //
    public static string HashMD5(string text) { using (var hasher = new MD5CryptoServiceProvider()) return HashWorker(hasher, text); }
    public static string HashSHA1(string text) { using (var hasher = new SHA1CryptoServiceProvider()) return HashWorker(hasher, text); }
    public static string HashWorker(HashAlgorithm HA, string text)
    {
      byte[] hash = HA.ComputeHash(Encoding.Default.GetBytes(text));
      StringBuilder sb = new StringBuilder();
      foreach (byte b in hash)
        sb.Append(b.ToString("x2"));
      return sb.ToString();
    }

    public static string Encrypt(string text)
    {
      if (string.IsNullOrEmpty(text))
        return string.Empty;
      //
      byte[] key = { 145, 12, 32, 245, 98, 132, 98, 214, 6, 77, 131, 44, 221, 3, 9, 50 };
      byte[] iv = { 15, 122, 132, 5, 93, 198, 44, 31, 9, 39, 241, 49, 250, 188, 80, 7 };
      //
      using (SymmetricAlgorithm algorithm = Rijndael.Create())
      {
        using (ICryptoTransform encryptor = algorithm.CreateEncryptor(key, iv))
        {
          using (MemoryStream ms = new MemoryStream())
          {
            using (CryptoStream cs = new CryptoStream(ms, encryptor, CryptoStreamMode.Write))
            {
              using (StreamWriter sw = new StreamWriter(cs))
              {
                sw.Write(text);
                sw.Flush();
                cs.FlushFinalBlock();
              }
            }
            return Convert.ToBase64String(ms.ToArray());
          }
        }
      }
    }

    public static string Decrypt(string cryptedText)
    {
      if (string.IsNullOrEmpty(cryptedText))
        return string.Empty;
      //
      byte[] key = { 145, 12, 32, 245, 98, 132, 98, 214, 6, 77, 131, 44, 221, 3, 9, 50 };
      byte[] iv = { 15, 122, 132, 5, 93, 198, 44, 31, 9, 39, 241, 49, 250, 188, 80, 7 };
      //
      using (SymmetricAlgorithm algorithm = Rijndael.Create())
      {
        using (ICryptoTransform decryptor = algorithm.CreateDecryptor(key, iv))
        {
          using (MemoryStream ms = new MemoryStream(Convert.FromBase64String(cryptedText)))
          {
            using (CryptoStream cs = new CryptoStream(ms, decryptor, CryptoStreamMode.Read))
            {
              using (StreamReader sr = new StreamReader(cs))
              {
                return sr.ReadToEnd();
              }
            }
          }
        }
      }
    }


    public static byte[] Compress(byte[] data)
    {
      using (MemoryStream ms = new MemoryStream())
      {
        using (DeflateStream ds = new DeflateStream(ms, CompressionMode.Compress))
        {
          ds.Write(data, 0, data.Length);
          ds.Flush();
        }
        return ms.ToArray();
      }
    }

    public static byte[] DeCompress(byte[] data)
    {
      using (MemoryStream ms = new MemoryStream(data))
      {
        using (DeflateStream ds = new DeflateStream(ms, CompressionMode.Decompress))
        {
          using (MemoryStream msOut = new MemoryStream())
          {
            byte[] buff = new byte[1024];
            int len = 0;
            while ((len = ds.Read(buff, 0, buff.Length)) != 0)
              msOut.Write(buff, 0, len);
            msOut.Flush();
            return msOut.ToArray();
          }
        }
      }
    }


    public static byte[] GZip(byte[] data)
    {
      using (MemoryStream ms = new MemoryStream())
      {
        using (GZipStream gzs = new GZipStream(ms, CompressionMode.Compress))
        {
          gzs.Write(data, 0, data.Length);
          gzs.Flush();
        }
        return ms.ToArray();
      }
    }

    public static byte[] GUnZip(byte[] data)
    {
      using (MemoryStream ms = new MemoryStream(data))
      {
        using (GZipStream gzs = new GZipStream(ms, CompressionMode.Decompress))
        {
          using (MemoryStream msOut = new MemoryStream())
          {
            byte[] buff = new byte[1024];
            int len = 0;
            while ((len = gzs.Read(buff, 0, buff.Length)) != 0)
              msOut.Write(buff, 0, len);
            msOut.Flush();
            return msOut.ToArray();
          }
        }
      }
    }


    public static string GZip2Base64(string data)
    {
      return Convert.ToBase64String(GZip(Encoding.UTF8.GetBytes(data)));
    }

    public static string GUnZipFromBase64(string base64string)
    {
      return Encoding.UTF8.GetString(GUnZip(Convert.FromBase64String(base64string)));
    }


    //public static string ToJSON(object obj)
    //{
    //  JavaScriptSerializer serializer = new JavaScriptSerializer();
    //  string serialized = serializer.Serialize(obj);
    //  return serialized;
    //}

    //public static object FromJSON(string serialized)
    //{
    //  JavaScriptSerializer serializer = new JavaScriptSerializer();
    //  object deserialized = serializer.DeserializeObject(serialized);
    //  return deserialized;
    //}


    //
    // serializzazione/deserializzazione con un metodo compatto adatto ad essere usato nelle QueryString
    //
    public static string SerializeToQueryString<T>(T obj)
    {
      System.ServiceModel.Dispatcher.JsonQueryStringConverter jsc = new System.ServiceModel.Dispatcher.JsonQueryStringConverter();
      return Convert.ToBase64String(Encoding.UTF8.GetBytes(jsc.ConvertValueToString(obj, typeof(T))));
      //return Encoding.UTF8.GetString(Convert.FromBase64String(string_base64));
    }
    public static T DeSerializeFromQueryString<T>(string dataStr)
    {
      System.ServiceModel.Dispatcher.JsonQueryStringConverter jsc = new System.ServiceModel.Dispatcher.JsonQueryStringConverter();
      return (T)jsc.ConvertStringToValue(Encoding.UTF8.GetString(Convert.FromBase64String(dataStr)), typeof(T));
    }


    //
    // serialization with the viewstate serializer
    // e' quasi identica al binary serializer con scrittura di una stringa base64
    //
    public static string SerializeLOS(object obj)
    {
      using (StringWriter sw = new StringWriter())
      {
        LosFormatter los = new LosFormatter();
        los.Serialize(sw, obj);
        return sw.ToString();
      }
      //if (compressed)
      //{
      //  using (MemoryStream ms = new MemoryStream())
      //  {
      //    using (GZipStream gs = new GZipStream(ms, CompressionMode.Compress))
      //    {
      //      using (MemoryStream sw = new MemoryStream())
      //      {
      //        LosFormatter los = new LosFormatter();
      //        los.Serialize(sw, obj);
      //        byte[] buffer = sw.GetBuffer();
      //        gs.Write(buffer, 0, buffer.Length);
      //        int len1 = buffer.Length;
      //      }
      //      gs.Flush();
      //      gs.Close();
      //    }
      //    int len2 = ms.GetBuffer().Length;
      //    return Convert.ToBase64String(ms.GetBuffer());
      //  }
      //}
    }
    //
    public static T DeSerializeLOS<T>(string dataStr)
    {
      try
      {
        LosFormatter los = new LosFormatter();
        return (T)los.Deserialize(dataStr);
      }
      catch { return default(T); }
    }

    //
    // serialization wrapper
    //
    public static string SerializeWrapper(object obj)
    {
      Type ty = obj.GetType();
      string cacheKey = "Ikon.Utility." + ty.Name;
      XmlSerializer serializer = HttpRuntime.Cache[cacheKey] as XmlSerializer;
      if (serializer == null)
        HttpRuntime.Cache.Insert(cacheKey, serializer = new XmlSerializer(ty));
      using (StringWriter sw = new StringWriter())
      {
        serializer.Serialize(sw, obj);
        string res = sw.ToString();
        sw.Close();
        return res;
      }
    }

    //
    // deserialization wrapper
    //
    // usage: CBS_API.t_Response res= (CBS_API.t_Response)MVLIB_utility.Deserialize_Wrapper(typeof(CBS_API.t_Response), xml_code);
    public static object DeserializeWrapper(Type ty, string src)
    {
      string cacheKey = "Ikon.Utility." + ty.Name;
      XmlSerializer serializer = HttpRuntime.Cache[cacheKey] as XmlSerializer;
      if (serializer == null)
        HttpRuntime.Cache.Insert(cacheKey, serializer = new XmlSerializer(ty));
      using (StringReader sr = new StringReader(src))
      {
        object obj = serializer.Deserialize(sr);
        sr.Close();
        return obj;
      }
    }

    public static object DeserializeWrapper(Type ty, string src, string xpath_expr)
    {
      XmlDocument xml_doc = new XmlDocument();
      xml_doc.LoadXml(src);
      string xml_out = xml_doc.SelectSingleNode(xpath_expr).OuterXml;
      return DeserializeWrapper(ty, xml_out);
    }

    public static T DeserializeWrapper<T>(string src)
    {
      string cacheKey = "Ikon.Utility." + typeof(T).Name;
      XmlSerializer serializer = HttpRuntime.Cache[cacheKey] as XmlSerializer;
      if (serializer == null)
        HttpRuntime.Cache.Insert(cacheKey, serializer = new XmlSerializer(typeof(T)));
      using (StringReader sr = new StringReader(src))
      {
        T obj = (T)serializer.Deserialize(sr);
        sr.Close();
        return obj;
      }
    }

    public static string TransformXml(string input, string xslt_file)
    {
      XmlDocument xml_doc = new XmlDocument();
      xml_doc.LoadXml(input);
      StringBuilder sb_out = new StringBuilder();

      XmlWriterSettings wrs = new XmlWriterSettings();
      wrs.Indent = true;
      wrs.IndentChars = "  ";
      wrs.NewLineHandling = NewLineHandling.Replace;
      wrs.Encoding = Encoding.UTF8;

      using (XmlWriter wr = XmlWriter.Create(sb_out, wrs))
      {
        XslCompiledTransform xslt_trasf = new XslCompiledTransform();
        xslt_trasf.Load(xslt_file);
        xslt_trasf.Transform(xml_doc, null, wr);
      }

      return sb_out.ToString();
    }


    //
    // Tuples generic classes
    // TODO: add equal method
    //
    public class Pair<T1, T2>
    {
      public T1 first { get; set; }
      public T2 second { get; set; }
      public Pair(T1 first, T2 second)
      {
        this.first = first;
        this.second = second;
      }
    }
    public class Triple<T1, T2, T3>
    {
      public T1 first { get; set; }
      public T2 second { get; set; }
      public T3 third { get; set; }
      public Triple(T1 first, T2 second, T3 third)
      {
        this.first = first;
        this.second = second;
        this.third = third;
      }
    }
    public class Quad<T1, T2, T3, T4>
    {
      public T1 first { get; set; }
      public T2 second { get; set; }
      public T3 third { get; set; }
      public T4 fourth { get; set; }
      public Quad(T1 first, T2 second, T3 third, T4 fourth)
      {
        this.first = first;
        this.second = second;
        this.third = third;
        this.fourth = fourth;
      }
    }


    //
    // remove tuples for .NET >= 4
    //
    class Tuple<T1, T2>
    {
      public T1 Item1 { get; private set; }
      public T2 Item2 { get; private set; }

      public Tuple(T1 item1, T2 item2)
      {
        this.Item1 = item1;
        this.Item2 = item2;
      }
    }

    class Tuple<T1, T2, T3>
    {
      public T1 Item1 { get; private set; }
      public T2 Item2 { get; private set; }
      public T3 Item3 { get; private set; }

      public Tuple(T1 item1, T2 item2, T3 item3)
      {
        this.Item1 = item1;
        this.Item2 = item2;
        this.Item3 = item3;
      }
    }

    class Tuple<T1, T2, T3, T4>
    {
      public T1 Item1 { get; private set; }
      public T2 Item2 { get; private set; }
      public T3 Item3 { get; private set; }
      public T4 Item4 { get; private set; }

      public Tuple(T1 item1, T2 item2, T3 item3, T4 item4)
      {
        this.Item1 = item1;
        this.Item2 = item2;
        this.Item3 = item3;
        this.Item4 = item4;
      }
    }


    public static DataTable Dictionary2DataTable(Dictionary<string, string> dict)
    {
      DataTable dt = new DataTable();
      dt.Columns.Add(new DataColumn("value"));
      dt.Columns.Add(new DataColumn("text"));
      foreach (KeyValuePair<string, string> kv in dict)
        dt.Rows.Add(kv.Key, kv.Value);
      return dt;
    }

    public static List<string> DataTable2Array(DataTable dt)
    {
      List<string> lst = new List<string>();
      foreach (DataRow r in dt.Rows)
        lst.Add(r[0].ToString());
      return lst;
    }

    public static Dictionary<string, string> DataTable2KeyValue(DataTable dt)
    {
      Dictionary<string, string> dict = new Dictionary<string, string>();
      foreach (DataRow r in dt.Rows)
        dict.Add(r[0].ToString(), r[1].ToString());
      return dict;
    }

    // non usa .Add per poter inserire chiavi doppie
    public static Dictionary<string, string> DataTable2CodeValue(DataTable dt)
    {
      Dictionary<string, string> dict = new Dictionary<string, string>();
      foreach (DataRow r in dt.Rows)
        dict[r[0].ToString()] = r[1].ToString();
      return dict;
    }

    public static List<Dictionary<string, string>> DataTable2List(DataTable dt)
    {
      List<Dictionary<string, string>> lst = new List<Dictionary<string, string>>();
      foreach (DataRow r in dt.Rows)
      {
        Dictionary<string, string> d = new Dictionary<string, string>();
        foreach (DataColumn c in dt.Columns)
          d.Add(c.ColumnName, r[c.ColumnName].ToString());
        lst.Add(d);
      }
      return lst;
    }

    public static Dictionary<string, Dictionary<string, string>> DataTable2Dictionary(DataTable dt, string field_key)
    {
      Dictionary<string, Dictionary<string, string>> lst = new Dictionary<string, Dictionary<string, string>>();
      foreach (DataRow r in dt.Rows)
      {
        Dictionary<string, string> d = new Dictionary<string, string>();
        foreach (DataColumn c in dt.Columns)
          d.Add(c.ColumnName, r[c.ColumnName].ToString());
        lst.Add(d[field_key], d);
      }
      return lst;
    }

    public static string Convert_file2base64(string filename)
    {
      string fname_in = vPathMap(filename);
      string fname_out = fname_in + ".txt";
      string data_in = File.ReadAllText(fname_in);
      string data_out = Convert.ToBase64String(Encoding.UTF8.GetBytes(data_in));
      File.WriteAllText(fname_out, data_out);
      return data_out;
    }

    public static string StringBase64ToString(string string_base64)
    {
      try { return Encoding.UTF8.GetString(Convert.FromBase64String(string_base64)); }
      catch { return null; }
    }
    public static string StringBase64ToStringUrlEncoded(string string_base64)
    {
      try { return Encoding.UTF8.GetString(Convert.FromBase64String(HttpUtility.UrlDecode(string_base64))); }
      catch { return null; }
    }

    public static string StringToBase64(string string_src)
    {
      try { return Convert.ToBase64String(Encoding.UTF8.GetBytes(string_src)); }
      catch { return null; }
    }
    public static string StringToBase64UrlEncoded(string string_src)
    {
      try { return HttpUtility.UrlEncode(Convert.ToBase64String(Encoding.UTF8.GetBytes(string_src))); }
      catch { return null; }
    }


    public static byte[] StringHexToByteArray(string hexString)
    {
      byte[] HexAsBytes = new byte[hexString.Length / 2];
      for (int index = 0; index < HexAsBytes.Length; index++)
      {
        string byteValue = hexString.Substring(index * 2, 2);
        HexAsBytes[index] = byte.Parse(byteValue, NumberStyles.HexNumber, CultureInfo.InvariantCulture);
      }
      return HexAsBytes;
    }


    public static string FormatString(this string format, params object[] args)
    {
      try { return string.Format(format, args); }
      catch { return format; }
    }

    //
    // string.Format con property names nella format string
    //
    // MembershipUser user = Membership.GetUser();
    // Status.Text = "{UserName} last logged in at {LastLoginDate}".FormatWith(user);
    //
    public static string FormatWith(this string format, object source)
    {
      return FormatWith(format, null, source);
    }

    private static Regex _FormatWithRx = new Regex(@"(?<start>\{)+(?<property>[\w\.\[\]]+)(?<format>:[^}]+)?(?<end>\})+", RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);
    public static string FormatWith(this string format, IFormatProvider provider, object source)
    {
      if (format == null)
        throw new ArgumentNullException("format");
      List<object> values = new List<object>();
      string rewrittenFormat = _FormatWithRx.Replace(format, delegate(Match m)
      {
        Group startGroup = m.Groups["start"];
        Group propertyGroup = m.Groups["property"];
        Group formatGroup = m.Groups["format"];
        Group endGroup = m.Groups["end"];
        values.Add((propertyGroup.Value == "0") ? source : DataBinder.Eval(source, propertyGroup.Value));
        return new string('{', startGroup.Captures.Count) + (values.Count - 1) + formatGroup.Value + new string('}', endGroup.Captures.Count);
      });
      return string.Format(provider, rewrittenFormat, values.ToArray());
    }


    //
    // funzioni per l'accesso a dati Linq.Binary come stringa
    // con una conversione compatibile con nvarchar(MAX) su DB (per evntuali CAST)
    //
    public static string GetStringDB(this System.Data.Linq.Binary binary)
    {
      return LinqBinaryGetStringDB(binary);
    }


    public static string LinqBinaryGetStringDB(byte[] binary)
    {
      try { return Encoding.Unicode.GetString(binary); }
      catch { return null; }
    }


    public static string LinqBinaryGetStringDB(System.Data.Linq.Binary binary)
    {
      try { return Encoding.Unicode.GetString(binary.ToArray()); }
      catch { return null; }
    }


    public static System.Data.Linq.Binary LinqBinarySetStringDB(string value)
    {
      try { return new System.Data.Linq.Binary(Encoding.Unicode.GetBytes(value)); }
      catch { return null; }
    }


    public static V TryGetValueMV<K, V>(this IDictionary<K, V> dict, K key)
    {
      return (dict != null && dict.ContainsKey(key)) ? dict[key] : default(V);
    }


    //
    // gestione della descrizione testuale per gli Enum che devono essere marcati con l'attributo
    // [DescriptionAttribute("abc")]
    //
    public static string EnumStringValue(this Enum value)
    {
      try
      {
        FieldInfo fi = value.GetType().GetField(value.ToString());
        return (fi.GetCustomAttributes(typeof(DescriptionAttribute), false).FirstOrDefault() as DescriptionAttribute).Description;
      }
      catch { return value.ToString(); }
    }

    public static T EnumStringValueParse<T>(string value)
    {
      try
      {
        Type enumType = typeof(T);
        foreach (string name in Enum.GetNames(enumType))
        {
          object en = Enum.Parse(enumType, name);
          if (((Enum)en).EnumStringValue() == value)
            return (T)Enum.Parse(enumType, name);
        }
      }
      catch { }
      return default(T);
    }


    public static Dictionary<T, string> EnumGetDictionary<T>()
    {
      return Enum.GetValues(typeof(T)).OfType<T>().ToDictionary(e => e, e => (e as Enum).EnumStringValue());
    }


    //
    // IIS e ASP.NET in genere hanno dei problemi a processare alcuni caratteri nelle url/path che diventa un problema serio con MVC
    // queste due funzioni consentono di effettuare l'escape delle url in modo da poter passare quasi di tutto a IIS senza
    // modificare i file di registro (operazione che comunque non risolve tutti i problemi ma solo & e *)
    // i caratteri processati sono: []&:<>*|?#
    // e gli spazi all'inizio e alla fine del path che altrimenti vengono mangiati dal browser
    // IIS si incasina anche con url contenenti ..
    // Il routing engine ha anche dei problemi per le url che contengono un . nell'ultimo frammento perche' tenta di mapparle ad un'estensione
    // che di solito non esiste
    //
    private static Regex UrlEncodePath_IIS_RegEx = new Regex(@"[\s\p{P}\p{S}-[%+\-@_./]]|\.{2,}|^\s+|\s+$|\.$", RegexOptions.Compiled);
    private static Regex UrlEncodePath_IIS_RegExFrags = new Regex(@"[\s\p{P}\p{S}-[%+\-@_.]]|\.{2,}|^\s+|\s+$|\.$", RegexOptions.Compiled);
    //private static Regex UrlEncodePath_IIS_RegEx = new Regex(@"[\[,\],&,:,<,>,\*,\|,\?,\#,""]|\.{2,}|^\s+|\s+$|\.$", RegexOptions.Compiled);
    //private static Regex UrlEncodePath_IIS_RegExFrags = new Regex(@"[\[,\],&,:,<,>,\*,\|,\?,\#,"",/]|\.{2,}|^\s+|\s+$|\.$", RegexOptions.Compiled);
    //
    private static Regex UrlEncodePath_IIS_RegEx_ValidExts = new Regex(@"^\.[a-z]{3,4}$", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static Regex UrlDecodePath_IIS_RegEx = new Regex(@"\[[0-9,a-f]{4}\]", RegexOptions.Compiled);
    //
    private static MatchEvaluator UrlDecodePath_IIS_RegExMEV = new MatchEvaluator(m => string.Join("", m.Value.ToCharArray().Select(c => string.Format("[{0:x4}]", (int)c)).ToArray()));
    public static string UrlEncodePathFragment_IIS(string fragment)
    {
      try
      {
        string fragmentOut = UrlEncodePath_IIS_RegExFrags.Replace(fragment, UrlDecodePath_IIS_RegExMEV);
        // encoding delle estensioni non riconosciute dal routing per sistemare ulteriori bug nell'extensionless routing
        // lo effettuiamo solamente per i frammenti in quanto questa e' la funzione utilizzata nel build del menu
        fragmentOut = (fragmentOut.IndexOf('.') >= 0) ? (!UrlEncodePath_IIS_RegEx_ValidExts.IsMatch(fragmentOut.Substring(fragmentOut.IndexOf('.'))) ? Regex.Replace(fragmentOut, @"\.", UrlDecodePath_IIS_RegExMEV) : fragmentOut) : fragmentOut;
        return fragmentOut;
      }
      catch { }
      return fragment;
    }
    //
    public static IEnumerable<string> UrlEncodePathFragments_IIS(IEnumerable<string> fragments)
    {
      try { return fragments.Select(f => UrlEncodePathFragment_IIS(f)); }
      catch { return fragments; }
    }
    //
    public static string UrlEncodePath_IIS(string url) { return UrlEncodePath_IIS(url, false); }
    public static string UrlEncodePath_IIS(string url, bool stopAtQueryString)
    {
      if (string.IsNullOrEmpty(url))
        return url;
      try
      {
        if (stopAtQueryString && url.IndexOf('?') >= 0)
          return UrlEncodePath_IIS(url.Substring(0, url.IndexOf('?')), false) + url.Substring(url.IndexOf('?'));
        //TODO: encoding delle estensioni non riconosciute dal routing per sistemare ulteriori bug nell'extensionless routing
        // per l'utilizzo attuale di questa funzione forse possiamo non implementarlo
        return UrlEncodePath_IIS_RegEx.Replace(url, new MatchEvaluator(m => string.Join("", m.Value.ToCharArray().Select(c => string.Format("[{0:x4}]", (int)c)).ToArray())));
        //return UrlEncodePath_IIS_RegEx.Replace(url, new MatchEvaluator(m => string.Format("[{0:x4}]", (int)m.Value[0])));
      }
      catch { }
      return url;
    }
    //
    public static string UrlDecodePath_IIS(string url)
    {
      try
      {
        if (string.IsNullOrEmpty(url))
          return url;
        if (url.IndexOf('?') >= 0)
          return UrlDecodePath_IIS(url.Substring(0, url.IndexOf('?'))) + url.Substring(url.IndexOf('?'));
        return UrlDecodePath_IIS_RegEx.Replace(url, new MatchEvaluator(m => ((char)int.Parse(m.Value.Substring(1, 4), System.Globalization.NumberStyles.AllowHexSpecifier)).ToString()));
      }
      catch { }
      return url;
    }
    //
    public static IEnumerable<string> UrlDecodePathTofrags_IIS(string url)
    {
      if (string.IsNullOrEmpty(url))
        yield break;
      if (url.IndexOf('?') >= 0)
        url = url.Substring(0, url.IndexOf('?'));
      foreach (string frag in url.Split('/'))
        yield return UrlDecodePath_IIS_RegEx.Replace(frag, new MatchEvaluator(m => ((char)int.Parse(m.Value.Substring(1, 4), System.Globalization.NumberStyles.AllowHexSpecifier)).ToString()));
    }

    // provare ad utilizzare @"[\s\p{P}\p{S}]"
    // provare ad utilizzare @"[\s\p{P}\p{S}-[/]]"
    //private static Regex UrlEncodePath_IIS_RegExSEO1 = new Regex(@"\.", RegexOptions.Compiled);  // sostituzione di . con stringa vuota
    //private static Regex UrlEncodePath_IIS_RegExSEO2 = new Regex(@"[\[,\],&,:,<,>,\*,\|,\?,\#,"",',/]|\s", RegexOptions.Compiled);
    //private static Regex UrlEncodePath_IIS_RegExSEO2s = new Regex(@"[\[,\],&,:,<,>,\*,\|,\?,\#,"",']|\s", RegexOptions.Compiled);
    private static Regex UrlEncodePath_IIS_RegExSEO3 = new Regex(@"-{2,}|/{2,}", RegexOptions.Compiled);  // condensazione dei - e / multipli
    private static Regex UrlEncodePath_IIS_RegExSEO4 = new Regex(@"^[0-9]+$", RegexOptions.Compiled);  // non possiamo usare frag esclusivamente numerici
    private static Regex UrlEncodePath_IIS_RegExSEO5 = new Regex(@"[\s\p{P}\p{S}-[%+@_]]", RegexOptions.Compiled);  // niente spazi, simboli, puntuation eccetto %+@_/
    private static Regex UrlEncodePath_IIS_RegExSEO5s = new Regex(@"[\s\p{P}\p{S}-[%+@_/]]", RegexOptions.Compiled);  // niente spazi, simboli, puntuation eccetto %+@_/
    public static string UrlEncodeIndexPathForSEO(string indexPath)
    {
      if (!string.IsNullOrEmpty(indexPath))
      {
        //try { return UrlEncodePath_IIS_RegExSEO3.Replace(UrlEncodePath_IIS_RegExSEO2.Replace(UrlEncodePath_IIS_RegExSEO1.Replace(indexPath.ReplaceAccents(), "-"), "-"), "-").Trim('-'); }
        //catch { }
        try { return UrlEncodePath_IIS_RegExSEO3.Replace(UrlEncodePath_IIS_RegExSEO5.Replace(indexPath.ReplaceAccents(), "-"), "-").Trim('-'); }
        catch { }
      }
      return indexPath;
    }
    public static string UrlEncodeIndexPathWithSlashesForSEO(string indexPath)
    {
      if (!string.IsNullOrEmpty(indexPath))
      {
        try
        {
          //string url_tmp = UrlEncodePath_IIS_RegExSEO3.Replace(UrlEncodePath_IIS_RegExSEO2s.Replace(UrlEncodePath_IIS_RegExSEO1.Replace(indexPath.ReplaceAccents(), "-"), "-"), "-").Trim('-');
          string url_tmp = UrlEncodePath_IIS_RegExSEO3.Replace(UrlEncodePath_IIS_RegExSEO5s.Replace(indexPath.ReplaceAccents(), "-"), "-").Trim('-');
          if (UrlEncodePath_IIS_RegExSEO4.IsMatch(url_tmp))
            url_tmp = "-" + url_tmp + "-";
          return url_tmp;
        }
        catch { }
        //try { return UrlEncodePath_IIS_RegExSEO3.Replace(UrlEncodePath_IIS_RegExSEO2s.Replace(UrlEncodePath_IIS_RegExSEO1.Replace(indexPath.ReplaceAccents(), string.Empty), "-"), "-").Trim('-'); }
        //catch { }
      }
      return indexPath;
    }
    public static string UrlEncodeIndexPathForDownload(string indexPath)
    {
      if (!string.IsNullOrEmpty(indexPath))
      {
        try
        {
          //string url_tmp = UrlEncodePath_IIS_RegExSEO3.Replace(UrlEncodePath_IIS_RegExSEO2s.Replace(indexPath.ReplaceAccents(), "-"), "-").Trim('-');
          string url_tmp = UrlEncodePath_IIS_RegExSEO3.Replace(UrlEncodePath_IIS_RegExSEO5s.Replace(indexPath.ReplaceAccents(), "-"), "-").Trim('-');
          if (UrlEncodePath_IIS_RegExSEO4.IsMatch(url_tmp))
            url_tmp = "-" + url_tmp + "-";
          return url_tmp;
        }
        catch { }
        //try { return UrlEncodePath_IIS_RegExSEO3.Replace(UrlEncodePath_IIS_RegExSEO2s.Replace(UrlEncodePath_IIS_RegExSEO1.Replace(indexPath.ReplaceAccents(), string.Empty), "-"), "-").Trim('-'); }
        //catch { }
      }
      return indexPath;
    }


    public static bool IsLocalToHostUrl(string url)
    {
      bool isLocal = false;
      try
      {
        url = url ?? HttpContext.Current.Request.Url.ToString();
        if (url.IsNullOrEmpty())
        {
          return isLocal;
        }
        if (url.IndexOf("#") >= 0)
        {
          url = url.Substring(0, url.IndexOf("#"));
        }
        Uri absoluteUri;
        if (Uri.TryCreate(url, UriKind.Absolute, out absoluteUri))
        {
          isLocal = String.Equals(HttpContext.Current.Request.Url.Host, absoluteUri.Host, StringComparison.OrdinalIgnoreCase);
        }
        else
        {
          isLocal = !url.StartsWith("http:", StringComparison.OrdinalIgnoreCase) && !url.StartsWith("https:", StringComparison.OrdinalIgnoreCase) && Uri.IsWellFormedUriString(url, UriKind.Relative);
        }
      }
      catch { }
      return isLocal;
    }


    public static string ResolveUrlFull(string relativeUrl) { return ResolveUrlFull(relativeUrl, null); }
    public static string ResolveUrlFull(string relativeUrl, string baseReferenceUrl)
    {
      try
      {
        int idx = relativeUrl.Trim().IndexOf("://", StringComparison.OrdinalIgnoreCase);
        if (idx >= 4 && idx <= 5)
        {
          return relativeUrl;
        }
        if (baseReferenceUrl.IsNullOrWhiteSpace())
        {
          return new Uri(HttpContext.Current.Request.Url, ResolveUrl(relativeUrl)).ToString();
        }
        else
        {
          return new Uri(new Uri(baseReferenceUrl), ResolveUrl(relativeUrl)).ToString();
        }
      }
      catch { }
      return relativeUrl;
    }


    public static string ResolveUrl(string relativeUrl) { return ResolveUrl(relativeUrl, null); }
    public static string ResolveUrl(string relativeUrl, string forcedAppVirtualPath)
    {
      if (relativeUrl.IsNullOrEmpty())
        return relativeUrl;
      if ((relativeUrl[0] == '/' || relativeUrl[0] == '\\') && forcedAppVirtualPath.IsNullOrEmpty())
        return relativeUrl;
      int idxOfScheme = relativeUrl.IndexOf(@"://", StringComparison.Ordinal);
      if (idxOfScheme != -1)
      {
        int idxOfQM = relativeUrl.IndexOf('?');
        if (idxOfQM == -1 || idxOfQM > idxOfScheme)
          return relativeUrl;
      }
      StringBuilder sbUrl = new StringBuilder();
      sbUrl.Append(forcedAppVirtualPath ?? HttpRuntime.AppDomainAppVirtualPath);
      if (sbUrl.Length == 0 || sbUrl[sbUrl.Length - 1] != '/')
        sbUrl.Append('/');
      // found question mark already? query string, do not touch!
      bool foundQM = false;
      bool foundSlash; // the latest char was a slash?
      if (relativeUrl.Length > 1 && relativeUrl[0] == '~' && (relativeUrl[1] == '/' || relativeUrl[1] == '\\'))
      {
        relativeUrl = relativeUrl.Substring(2);
        foundSlash = true;
      }
      else
      {
        if (forcedAppVirtualPath.IsNotEmpty())
        {
          if (HttpRuntime.AppDomainAppVirtualPath.Length > 1 && relativeUrl.StartsWith(HttpRuntime.AppDomainAppVirtualPath + "/"))
            relativeUrl = relativeUrl.Substring(HttpRuntime.AppDomainAppVirtualPath.Length + 1);
        }
        foundSlash = (relativeUrl.Length > 1 && relativeUrl[0] == '/');
      }
      foreach (char c in relativeUrl)
      {
        if (!foundQM)
        {
          if (c == '?')
            foundQM = true;
          else
          {
            if (c == '/' || c == '\\')
            {
              if (foundSlash)
                continue;
              else
              {
                sbUrl.Append('/');
                foundSlash = true;
                continue;
              }
            }
            else if (foundSlash)
              foundSlash = false;
          }
        }
        sbUrl.Append(c);
      }
      return sbUrl.ToString();
    }


    public static string BuildFullUrl(string urlFragment, IDictionary<string, string> parameters, bool? clearFragQueryString)
    {
      clearFragQueryString = clearFragQueryString ?? false;
      if (clearFragQueryString.Value)
        urlFragment = urlFragment.Split('?').FirstOrDefault();
      urlFragment = Utility.ResolveUrl(urlFragment ?? string.Empty);
      Uri uri = new Uri(HttpContext.Current.Request.Url, urlFragment);
      string url = uri.ToString();
      if (parameters != null)
      {
        foreach (KeyValuePair<string, string> kv in parameters)
          url += (url.IndexOf('?') == -1 ? "?" : "&") + string.Format("{0}={1}", HttpUtility.UrlEncode(kv.Key), HttpUtility.UrlEncode(kv.Value));
      }
      return url;
    }


    //
    // supporto per CloudFlare
    //
    public static string GetRequestAddressExt(HttpRequest request)
    {
      string str_IP = null;
      try
      {
        if (request == null)
          request = HttpContext.Current.Request;
        // per attivare il supporto cloudflare
        try { str_IP = str_IP ?? request.Headers["CF-Connecting-IP"]; }
        catch { }
        try { str_IP = str_IP ?? request.ServerVariables["HTTP_CF_CONNECTING_IP"]; }
        catch { }
        try { str_IP = str_IP ?? request.ServerVariables["HTTP_X_FORWARDED_FOR"]; }
        catch { }
        //try { str_IP = str_IP ?? request.Headers["X-Forwarded-For"]; }
        //catch { }
        //
        // dobbiamo verificare che non si tratti di una rete interna, nel qual caso si tratta di un trasparent proxy e dobbiamo ignorare il valore
        if (str_IP.IsNotEmpty())
        {
          try
          {
            uint addr02 = BitConverter.ToUInt32(System.Net.IPAddress.Parse(str_IP).GetAddressBytes().Reverse().ToArray(), 0);
            if ((addr02 & (uint)0XFFFF0000) == (uint)0xC0A80000)
            {
              // 192.168.0.0/16
              str_IP = null;
            }
            else if ((addr02 & (uint)0XFFF00000) == (uint)0xAC100000)
            {
              // 172.16.0.0-172.31.0.0/16 -->172.16.0.0/12
              str_IP = null;
            }
            else if ((addr02 & (uint)0XFF000000) == (uint)0x0A000000)
            {
              // 10.0.0.0/8
              str_IP = null;
            }
            else if ((addr02 & (uint)0XFF000000) == (uint)0x7F000000)
            {
              // 127.0.0.0/8
              str_IP = null;
            }
          }
          catch
          {
            str_IP = null;
          }
        }
        //
        str_IP = str_IP ?? request.UserHostAddress;
      }
      catch { }
      return str_IP;
    }


    public static int GetRequestAddressExtNum(HttpRequest request)
    {
      int myIP = 0;
      try
      {
        string str_IP = GetRequestAddressExt(request);
        var IP = Utility.TryParse<System.Net.IPAddress>(str_IP);
        if (IP != null)
        {
          //var bytes = IP.GetAddressBytes();
          //myIP = (int)bytes[3] << 24 | (int)bytes[2] << 16 | (int)bytes[1] << 8 | (int)bytes[0];
          myIP = IP.GetHashCode();
        }
      }
      catch { }
      return myIP;
    }


    public static string UriSetQuery(string url, string key, string val)
    {
      try
      {
        int idx = url.IndexOf('?');
        string query = (idx < 0) ? string.Empty : url.Substring(idx + 1);
        NameValueCollection vars = HttpUtility.ParseQueryString(query);
        StringBuilder q = new StringBuilder("?");
        //
        if (val == null)
          vars.Remove(key);
        else
          vars[key] = val;
        foreach (string k in vars.AllKeys)
        {
          foreach (string v in vars.GetValues(k))
          {
            if (string.IsNullOrEmpty(k))
              q.AppendFormat("{0}&", HttpUtility.UrlEncode(v));
            else
              q.AppendFormat("{0}={1}&", HttpUtility.UrlEncode(k), HttpUtility.UrlEncode(v));
          }
        }
        //
        query = (q.Length > 1) ? q.Remove(q.Length - 1, 1).ToString() : string.Empty;
        url = (idx < 0) ? url + query : url.Substring(0, idx) + query;
      }
      catch { }
      return url;
    }

    public static string UriAddQuery(string url, string key, string val)
    {
      try
      {
        int idx = url.IndexOf('?');
        string query = (idx < 0) ? string.Empty : url.Substring(idx + 1);
        NameValueCollection vars = HttpUtility.ParseQueryString(query);
        StringBuilder q = new StringBuilder("?");
        //
        if (val == null)
          vars.Remove(key);
        else
          vars.Add(key, val);
        foreach (string k in vars.AllKeys)
        {
          foreach (string v in vars.GetValues(k))
          {
            if (string.IsNullOrEmpty(k))
              q.AppendFormat("{0}&", HttpUtility.UrlEncode(v));
            else
              q.AppendFormat("{0}={1}&", HttpUtility.UrlEncode(k), HttpUtility.UrlEncode(v));
          }
        }
        //
        query = (q.Length > 1) ? q.Remove(q.Length - 1, 1).ToString() : string.Empty;
        url = (idx < 0) ? url + query : url.Substring(0, idx) + query;
      }
      catch { }
      return url;
    }

    public static string UriDelQueryVar(string url, string key, string val)
    {
      try
      {
        int idx = url.IndexOf('?');
        string query = (idx < 0) ? string.Empty : url.Substring(idx + 1);
        NameValueCollection vars = HttpUtility.ParseQueryString(query);
        StringBuilder q = new StringBuilder("?");
        foreach (string k in vars.AllKeys)
        {
          foreach (string v in vars.GetValues(k))
          {
            if (k == key && v == val)
              continue;
            if (string.IsNullOrEmpty(k))
              q.AppendFormat("{0}&", HttpUtility.UrlEncode(v));
            else
              q.AppendFormat("{0}={1}&", HttpUtility.UrlEncode(k), HttpUtility.UrlEncode(v));
          }
        }
        //
        query = (q.Length > 1) ? q.Remove(q.Length - 1, 1).ToString() : string.Empty;
        url = (idx < 0) ? url + query : url.Substring(0, idx) + query;
      }
      catch { }
      return url;
    }

    public static string UriDelQueryVars(string url, params string[] keys)
    {
      try
      {
        int idx = url.IndexOf('?');
        string query = (idx < 0) ? string.Empty : url.Substring(idx + 1);
        NameValueCollection vars = HttpUtility.ParseQueryString(query);
        StringBuilder q = new StringBuilder("?");
        //
        if (keys != null)
          foreach (string key in keys)
            vars.Remove(key);
        foreach (string k in vars.AllKeys)
        {
          foreach (string v in vars.GetValues(k))
          {
            if (string.IsNullOrEmpty(k))
              q.AppendFormat("{0}&", HttpUtility.UrlEncode(v));
            else
              q.AppendFormat("{0}={1}&", HttpUtility.UrlEncode(k), HttpUtility.UrlEncode(v));
          }
        }
        //
        query = (q.Length > 1) ? q.Remove(q.Length - 1, 1).ToString() : string.Empty;
        url = (idx < 0) ? url + query : url.Substring(0, idx) + query;
      }
      catch { }
      return url;
    }


    public static string UriMigrateQueryString(string urlSrc, string urlDst, bool clearDst)
    {
      string url = urlDst;
      try
      {
        var qsSrc = ParseQueryString(urlSrc);
        var qsDst = clearDst ? ParseQueryString(urlDst).Where(q => !qsSrc.Any(r => r.Key == q.Key)).ToList() : ParseQueryString(urlDst);
        url = SaveQueryString(urlDst, qsDst.Concat(qsSrc));
        return url;
      }
      catch { }
      return url;
    }


    public static List<KeyValuePair<string, string>> ParseQueryString(string url) { return ParseQueryString(url, false); }
    public static List<KeyValuePair<string, string>> ParseQueryString(string url, bool parseFrags)
    {
      List<KeyValuePair<string, string>> data = new List<KeyValuePair<string, string>>();
      try
      {
        int idx = url.IndexOfAny((parseFrags ? "?#" : "?").ToCharArray());
        string qs = (idx < 0) ? string.Empty : url.Substring(idx + 1);
        int num = (qs != null) ? qs.Length : 0;
        for (int i = 0; i < num; i++)
        {
          int startIndex = i;
          int num4 = -1;
          while (i < num)
          {
            char ch = qs[i];
            if (ch == '=')
            {
              if (num4 < 0)
                num4 = i;
            }
            else if (ch == '&' || ch == '#')
            {
              break;
            }
            i++;
          }
          string str = null;
          string str2 = null;
          if (num4 >= 0)
          {
            str = qs.Substring(startIndex, num4 - startIndex);
            str2 = qs.Substring(num4 + 1, (i - num4) - 1);
          }
          else
          {
            str2 = qs.Substring(startIndex, i - startIndex);
          }
          data.Add(new KeyValuePair<string, string>(HttpUtility.UrlDecode(str), HttpUtility.UrlDecode(str2)));
          if (qs[i] == '#' && parseFrags == false)
            break;
        }
      }
      catch { }
      return data;
    }


    public static string SaveQueryString(string url, IEnumerable<KeyValuePair<string, string>> qs)
    {
      string urlFrag = url.Split('?').FirstOrDefault();
      string fragQS = Utility.Implode(qs.Select(r => HttpUtility.UrlEncode(r.Key) + "=" + HttpUtility.UrlEncode(r.Value)), "&");
      return Utility.Implode(new string[] { urlFrag, fragQS }, "?", null, false, true);
    }

    public static Uri UriSetQuery(Uri uri, string key, string val)
    {
      return new Uri(UriSetQuery(uri.ToString(), key, val));
      //UriBuilder ub = new UriBuilder(uri);
      //NameValueCollection vars = HttpUtility.ParseQueryString(ub.Query);
      //StringBuilder q = new StringBuilder();
      //if (val == null)
      //  vars.Remove(key);
      //else
      //  vars[key] = val;
      //foreach (string k in vars.AllKeys)
      //{
      //  foreach (string v in vars.GetValues(k))
      //  {
      //    if (string.IsNullOrEmpty(k))
      //      q.AppendFormat("{0}&", HttpUtility.UrlEncode(v));
      //    else
      //      q.AppendFormat("{0}={1}&", HttpUtility.UrlEncode(k), HttpUtility.UrlEncode(v));
      //  }
      //}
      //ub.Query = q.ToString().TrimEnd("&".ToCharArray());
      //return ub.Uri;
    }
    public static Uri UriAddQuery(Uri uri, string key, string val)
    {
      return new Uri(UriAddQuery(uri.ToString(), key, val));
      //UriBuilder ub = new UriBuilder(uri);
      //NameValueCollection vars = HttpUtility.ParseQueryString(ub.Query);
      //StringBuilder q = new StringBuilder();
      //if (val == null)
      //  vars.Remove(key);
      //else
      //  vars.Add(key, val);
      //foreach (string k in vars.AllKeys)
      //{
      //  foreach (string v in vars.GetValues(k))
      //  {
      //    if (string.IsNullOrEmpty(k))
      //      q.AppendFormat("{0}&", HttpUtility.UrlEncode(v));
      //    else
      //      q.AppendFormat("{0}={1}&", HttpUtility.UrlEncode(k), HttpUtility.UrlEncode(v));
      //  }
      //}
      //ub.Query = q.ToString().TrimEnd("&".ToCharArray());
      //return ub.Uri;
    }
    public static Uri UriDelQueryVars(Uri uri, params string[] keys)
    {
      return new Uri(UriDelQueryVars(uri.ToString(), keys));
      //UriBuilder ub = new UriBuilder(uri);
      //NameValueCollection vars = HttpUtility.ParseQueryString(ub.Query);
      //StringBuilder q = new StringBuilder();
      //if (keys != null)
      //  foreach (string key in keys)
      //    vars.Remove(key);
      //foreach (string k in vars.AllKeys)
      //{
      //  foreach (string v in vars.GetValues(k))
      //  {
      //    if (string.IsNullOrEmpty(k))
      //      q.AppendFormat("{0}&", HttpUtility.UrlEncode(v));
      //    else
      //      q.AppendFormat("{0}={1}&", HttpUtility.UrlEncode(k), HttpUtility.UrlEncode(v));
      //  }
      //}
      //ub.Query = q.ToString().TrimEnd("&".ToCharArray());
      //return ub.Uri;
    }


    public static bool QueryStringCheck(string name, string value) { return NameValueCollectionCheck(HttpContext.Current.Request.QueryString, name, value); }
    public static bool RequestParamsCheck(string name, string value) { return NameValueCollectionCheck(HttpContext.Current.Request.Params, name, value); }
    public static bool NameValueCollectionCheck(NameValueCollection ArgsSet, string name, string value)
    {
      if (ArgsSet != null && ArgsSet.Count > 0)
      {
        // attenzione che NameValueCollection e' implementata in modo indecente per renderla quasi del tutto inutile ma e' quello che passa il convento...
        // non e' possibile accedere mai ad un valore singolo, se ci sono key degeneri mi ritorna una stringa separata da virgole @#!!!
        return ArgsSet.AllKeys.Contains(name) && ArgsSet.GetValues(name).Contains(value);
      }
      return false;
    }


    public static bool EnumCheckMask(this object enum_obj, object mask)
    {
      return ((int)enum_obj & (int)mask) == (int)mask;
    }


    //
    // verifica se un indirizzo corrisponde ad una netmask
    // netMask deve essere espresso come 10.0.0.0/24
    //
    public static bool CheckNetMaskIP(string addrIP, string netMaskList)
    {
      try
      {
        System.Net.IPAddress testIP = Utility.TryParse<System.Net.IPAddress>(addrIP);
        uint addr02 = BitConverter.ToUInt32(testIP.GetAddressBytes().Reverse().ToArray(), 0);
        foreach (string netMask in Utility.Explode(netMaskList, ";, ", " ", true))
        {
          string netMaskAux = netMask + "/32";
          string network = netMaskAux.Split('/')[0];
          int bits = 32 - Math.Min(Math.Max(Utility.TryParse<int>(netMaskAux.Split('/')[1], 32), 0), 32);
          System.Net.IPAddress networkIP = Utility.TryParse<System.Net.IPAddress>(netMaskAux.Split('/')[0]);
          uint addr01 = BitConverter.ToUInt32(networkIP.GetAddressBytes().Reverse().ToArray(), 0);
          bool match = ((addr01 >> bits) == (addr02 >> bits)) || bits > 31;
          if (match)
            return true;
        }
      }
      catch { }
      return false;
    }


    //
    // per settare il formato numerico internazionale
    //
    public static void InvariantFormatSetup() { InvariantFormatSetup(true, true); }
    public static void InvariantFormatSetup(bool invariantNumeric, bool invariantDate) { InvariantFormatSetup(null, invariantNumeric, invariantDate); }
    public static void InvariantFormatSetup(string forcedCulture, bool invariantNumeric, bool invariantDate)
    {
      CultureInfo cc = Thread.CurrentThread.CurrentCulture;
      CultureInfo nci = new CultureInfo(string.IsNullOrEmpty(forcedCulture) ? cc.Name : forcedCulture);
      if (invariantNumeric)
        nci.NumberFormat = CultureInfo.InvariantCulture.NumberFormat;
      if (invariantDate)
        nci.DateTimeFormat = CultureInfo.InvariantCulture.DateTimeFormat;
      Thread.CurrentThread.CurrentCulture = nci;
    }

    //
    // Setting Default Values on Automatic Properties
    // da usare nel costruttore della classe
    // basta decorare le properties con: (funziona anche con gli enum)
    // non usarla su Page perche' ha un sacco di DefaultValueAttribute definiti in giro
    //
    // using System.ComponentModel;
    // [DefaultValue("DefaultString!")]
    // public string DefaultString { get; set; }
    //
    public static void InitPropertyDefaults(this object obj)
    {
      foreach (PropertyDescriptor property in TypeDescriptor.GetProperties(obj))
        property.ResetValue(obj);
      //bool recurse
      //foreach (PropertyInfo prop in obj.GetType().GetProperties(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static))
      //  foreach (DefaultValueAttribute dva in prop.GetCustomAttributes(typeof(DefaultValueAttribute), recurse))
      //    if (prop.PropertyType.IsAssignableFrom(dva.Value.GetType()))
      //      prop.SetValue(obj, dva.Value, null);
    }

    //
    // funzione per il databinding di blocchi di xml in un controllo databound
    // eg.: <ikon:CMS_Image ID="CMS_foto1" runat="server" XML_Field='<%# MV_CMS.Utility.XmlXPathBinder(Container.DataItem, "./data/foto1") %>' />
    //
    public static string XmlXPathBinder(object xData, string xPathExpr)
    {
      if (xData is IXPathNavigable)
      {
        try
        {
          IXPathNavigable xmlObject = (IXPathNavigable)xData;
          XPathNavigator xml = xmlObject.CreateNavigator().SelectSingleNode(xPathExpr);
          return xml.OuterXml;
        }
        catch { }
      }
      return string.Empty;
    }


    //
    // extension methods per agevolare la conversione di XML tra XElement e XmlNode
    //
    public static XElement ToXElement(this XmlNode node)
    {
      XDocument xDoc = new XDocument();
      using (XmlWriter xmlWriter = xDoc.CreateWriter())
        node.WriteTo(xmlWriter);
      return xDoc.Root;
    }
    //
    public static XmlNode ToXmlNode(this XElement xElement)
    {
      using (XmlReader xmlReader = xElement.CreateReader())
      {
        xmlReader.Settings.IgnoreWhitespace = true;
        XmlDocument xmlDoc = new XmlDocument();
        xmlDoc.Load(xmlReader);
        return xmlDoc.DocumentElement;
      }
    }


    //
    // extension methods per creare delle funzioni piu' comode per l'accesso agli attributi e gestire
    // valori di default e attributi non presenti senza troppi test per il chiamante
    // queste extensions funzionano anche se applicate su un null!
    //
    public static void SetAttributeValueWithEmptyAsNull(this XElement xElem, XName name, string value)
    {
      xElem.SetAttributeValue(name, string.IsNullOrEmpty(value) ? null : value);
    }

    public static string AttributeValueNN(this XElement xElem, XName name) { return xElem.AttributeValue(name, string.Empty); }
    public static string AttributeValue(this XElement xElem, XName name) { return xElem.AttributeValue(name, null); }
    public static string AttributeValue(this XElement xElem, XName name, string defValue)
    {
      if (xElem != null)
      {
        XAttribute xA = xElem.Attribute(name);
        if (xA != null && xA.Value != null)
          return xA.Value;
      }
      return defValue;
    }

    // come sopra, ma per gli xElement
    public static string ElementValueNN(this XElement xParent, XName name) { return xParent.ElementValue(name, string.Empty); }
    public static string ElementValue(this XElement xParent, XName name) { return xParent.ElementValue(name, null); }
    public static string ElementValue(this XElement xParent, XName name, string defValue)
    {
      if (xParent != null)
      {
        XElement xE = xParent.Element(name);
        if (xE != null && xE.Value != null)
          return xE.Value;
      }
      return defValue;
    }

    // come sopra, ma per il primo descendant
    public static string DescendantValueNN(this XElement xParent, XName name) { return xParent.DescendantValue(name, string.Empty); }
    public static string DescendantValue(this XElement xParent, XName name) { return xParent.DescendantValue(name, null); }
    public static string DescendantValue(this XElement xParent, XName name, string defValue)
    {
      if (xParent != null)
      {
        XElement xE = xParent.Descendants(name).FirstOrDefault();
        if (xE != null && xE.Value != null)
          return xE.Value;
      }
      return defValue;
    }

    //
    // funzioni per la manipolazione dei siblings
    //
    public static IEnumerable<XElement> Siblings(this XElement xElement)
    {
      if (xElement == null || xElement.Parent == null)
        return null;
      return xElement.Parent.Elements();
    }
    public static XElement PreviousSibling(this XElement xElement)
    {
      if (xElement == null)
        return null;
      for (XNode xN = xElement.PreviousNode; xN != null; xN = xN.PreviousNode)
        if (xN is XElement)
          return (XElement)xN;
      return null;
    }
    public static XElement NextSibling(this XElement xElement)
    {
      if (xElement == null)
        return null;
      for (XNode xN = xElement.NextNode; xN != null; xN = xN.NextNode)
        if (xN is XElement)
          return (XElement)xN;
      return null;
    }

    //
    // wrapper un po' piu' decenti per la gestione dell'XPath
    //
    public static string XPathEvaluateString(this XElement xElement, string xPathExpr) { return xElement.XPathEvaluateT<string>(xPathExpr) ?? string.Empty; }
    public static T XPathEvaluateT<T>(this XElement xElement, string xPathExpr)
    {
      try
      {
        var res = xElement.XPathEvaluate(xPathExpr);
        if (res is bool || res is double || res is string)
          return Utility.TryParse<T>(res);
        foreach (var obj in (IEnumerable)res)
        {
          string val = Utility.FindPropertySafe<string>(obj, "Value");
          return Utility.TryParse<T>(val);
        }
      }
      catch { }
      return default(T);
    }


    public static XElement XElementXPath(XElement xObj, string xpathExpr)
    {
      if (xObj == null)
        return null;
      int counter = 0;
      string last = null;
      foreach (string elem in Explode(xpathExpr, "/"))
      {
        switch (elem)
        {
          case "": // root or inside //
            break;
          case ".":
            break;
          case "..":
            xObj = xObj.Parent;
            break;
          default:
            if (last == string.Empty)
            {
              if (counter != 1)
              {
                foreach (XElement xe in xObj.Descendants(elem))
                {
                  xObj = xe;
                  break;
                }
              }
            }
            else
              xObj = xObj.Element(elem);
            if (xObj == null || xObj.Name.LocalName != elem)
              return null;
            break;
        }
        counter++;
        last = elem;
        if (xObj == null)
          return null;
      }
      return xObj;
    }

    public static string XElementXPathValue(XElement xObj, string xpathExpr)
    {
      if (xObj == null)
        return string.Empty;
      int counter = 0;
      string last = null;
      foreach (string elem in Explode(xpathExpr, "/"))
      {
        switch (elem)
        {
          case "": // root or inside //
            break;
          case ".":
            break;
          case "..":
            xObj = xObj.Parent;
            break;
          default:
            if (elem.StartsWith("@"))
            {
              XAttribute xa = xObj.Attribute(elem.Substring(1));
              if (xa != null)
                return xa.Value;
              return string.Empty;
            }

            if (last == string.Empty)
            {
              if (counter != 1)
              {
                foreach (XElement xe in xObj.Descendants(elem))
                {
                  xObj = xe;
                  break;
                }
              }
            }
            else
              xObj = xObj.Element(elem);
            if (xObj == null || xObj.Name.LocalName != elem)
              return string.Empty;
            break;
        }
        counter++;
        last = elem;
        if (xObj == null)
          return string.Empty;
      }
      return xObj.Value;
    }


    public static string WriteoutXML(XElement xe) { return WriteoutXML(xe.ToString(), null); }
    public static string WriteoutXML(String xml_str) { return WriteoutXML(xml_str, null); }
    public static string WriteoutXML(String xml_str, string filename)
    {
      String out_str = xml_str;

      //
      // save raw XML
      //
      bool save_raw_XML = (filename != null);
      if (save_raw_XML)
      {
        using (StreamWriter sw = new StreamWriter(vPathMap(filename)))
          sw.Write(out_str);
      }

      //
      // processing xslt
      //
      bool xslt_postprocess = true;
      if (xslt_postprocess)
      {
        string xslt_src = @"<?xml version='1.0'?>
<xsl:stylesheet version='1.0' xmlns:xsl='http://www.w3.org/1999/XSL/Transform'>
<xsl:output method='xml' indent='yes' encoding='utf-8' omit-xml-declaration='yes'/>
<xsl:template match='*'>
   <xsl:copy>
      <xsl:copy-of select='@*' />
      <xsl:apply-templates />
   </xsl:copy>
</xsl:template>
<xsl:template match='comment()|processing-instruction()'>
   <xsl:copy />
</xsl:template>
<!-- WARNING: pericoloso: da utilizzare con attenzione! -->
<xsl:template match='text()[normalize-space(.)=""""]'/>
</xsl:stylesheet>
";
        //TextReader strReader = new StringReader(out_str);
        TextWriter xslt_out = new StringWriter();
        XslCompiledTransform xslt = new XslCompiledTransform();
        xslt.Load(XmlReader.Create(new StringReader(xslt_src)));
        xslt.Transform(XmlReader.Create(new StringReader(out_str)), null, xslt_out);
        out_str = xslt_out.ToString();
      }

      //
      // encoding XML
      //
      bool xml2html_encoding = true;
      if (xml2html_encoding)
      {
        out_str = NormalizeNewline(out_str);
        // prima rinormaliziamo a fondo i contenuti limitando i loops...
        for (int i = 0; i < 10; i++)
        {
          string str_norm = HttpUtility.HtmlDecode(out_str);
          if (str_norm == out_str)
            break;
          out_str = str_norm;
        }
        out_str = HttpUtility.HtmlEncode(out_str);
        out_str = Regex.Replace(out_str, " ", "&nbsp;");
        out_str = nl2br(out_str);
      }

      return out_str;
    }


    //
    // Dictionary<TKey, TValue> che non ritorna un'eccezione nel caso manchi la Key cercata
    // ritorna un null per gli object oppure il valore di default per i value type
    //
    public class DictionaryMV<TKey, TValue> : Dictionary<TKey, TValue>
    {
      public DictionaryMV() : base() { }
      public DictionaryMV(IDictionary<TKey, TValue> dictionary) : base(dictionary) { }
      public DictionaryMV(IEqualityComparer<TKey> comparer) : base(comparer) { }
      public DictionaryMV(int capacity) : base(capacity) { }
      public DictionaryMV(IDictionary<TKey, TValue> dictionary, IEqualityComparer<TKey> comparer) : base(dictionary, comparer) { }
      public DictionaryMV(int capacity, IEqualityComparer<TKey> comparer) : base(capacity, comparer) { }

      public new TValue this[TKey key]
      {
        get
        {
          TValue val = default(TValue);
          TryGetValue(key, out val);
          return val;
        }
        set { base[key] = value; }
      }
    }




    //
    // thread safe locked enumerator
    //
    public class SafeEnumerator<T> : IEnumerator<T>
    {
      // this is the (thread-unsafe)
      // enumerator of the underlying collection
      private readonly IEnumerator<T> m_Inner;
      // this is the object we shall lock on. 
      private readonly object m_Lock;

      public SafeEnumerator(IEnumerator<T> inner, object @lock)
      {
        m_Inner = inner;
        m_Lock = @lock;
        // entering lock in constructor
        Monitor.Enter(m_Lock);
      }

      public void Dispose()
      {
        // .. and exiting lock on Dispose()
        // This will be called when foreach loop finishes
        Monitor.Exit(m_Lock);
      }

      // we just delegate actual implementation
      // to the inner enumerator, that actually iterates
      // over some collection

      public bool MoveNext()
      {
        return m_Inner.MoveNext();
      }

      public void Reset()
      {
        m_Inner.Reset();
      }

      public T Current
      {
        get { return m_Inner.Current; }
      }

      object IEnumerator.Current
      {
        get { return Current; }
      }

    }


    public class SafeEnumerable<T> : IEnumerable<T>
    {
      private readonly IEnumerable<T> m_Inner;
      private readonly object m_Lock;

      public SafeEnumerable(IEnumerable<T> inner, object @lock)
      {
        m_Lock = @lock;
        m_Inner = inner;
      }

      public IEnumerator<T> GetEnumerator()
      {
        return new SafeEnumerator<T>(m_Inner.GetEnumerator(), m_Lock);
      }

      IEnumerator IEnumerable.GetEnumerator()
      {
        return GetEnumerator();
      }

    }

    //
    // versione piu' sofisticata per il check della similarita' tra stringhe
    // con il check anche della trasposizione di blocchi (v. http://www.codeproject.com/KB/recipes/improvestringsimilarity.aspx)
    // return 1.0 identical, 0.0 no similarity
    //
    private static Regex StringSimilarity_RegEx = new Regex(@"([ \n\r\t{}():;/\\])", RegexOptions.Compiled);
    public static double StringSimilarity(string string1, string string2) { return StringSimilarity(string1, string2, null, null, null); }
    public static double StringSimilarity(string string1, string string2, bool? caseSensitive) { return StringSimilarity(string1, string2, caseSensitive, null, null); }
    public static double StringSimilarity(string string1, string string2, bool? caseSensitive, bool? stripAccents) { return StringSimilarity(string1, string2, caseSensitive, stripAccents, null); }
    public static double StringSimilarity(string string1, string string2, bool? caseSensitive, bool? stripAccents, Regex tokenizerRx)
    {
      string[] _leftTokens, _rightTokens;
      int leftLen, rightLen;
      double[,] cost;
      StringSimilarity getSimilarity;

      string1 = string1 ?? string.Empty;
      string2 = string2 ?? string.Empty;
      if (caseSensitive.GetValueOrDefault(true) == false)
      {
        string1 = string1.ToLowerInvariant();
        string2 = string2.ToLowerInvariant();
      }
      if (stripAccents.GetValueOrDefault(false))
      {
        string1 = string1.ReplaceAccents();
        string2 = string2.ReplaceAccents();
      }
      Leven editdistance = new Leven();
      getSimilarity = new StringSimilarity(editdistance.GetSimilarity);
      Tokeniser tokeniser = new Tokeniser() { tokenizerRx = tokenizerRx ?? StringSimilarity_RegEx };
      _leftTokens = tokeniser.Partition(string1);
      _rightTokens = tokeniser.Partition(string2);
      if (_leftTokens.Length > _rightTokens.Length)
      {
        string[] tmp = _leftTokens;
        _leftTokens = _rightTokens;
        _rightTokens = tmp;
        string s = string1; string1 = string2; string2 = s;
      }
      leftLen = _leftTokens.Length - 1;
      rightLen = _rightTokens.Length - 1;
      cost = new double[leftLen + 1, rightLen + 1];
      for (int i = 0; i <= leftLen; i++)
        for (int j = 0; j <= rightLen; j++)
          cost[i, j] = getSimilarity(_leftTokens[i], _rightTokens[j]);
      BipartiteMatcher match = new BipartiteMatcher(_leftTokens, _rightTokens, cost);
      return match.Score;
    }


    // Computes the Levenshtein Edit Distance between two strings
    public static int StringDistance(this string string1, string string2) { return EditDistance(string1, string2); }
    public static double StringDistanceF(this string string1, string string2) { return (double)EditDistance(string1 ?? string.Empty, string2 ?? string.Empty) / ((double)Math.Max(Math.Max((string1 ?? string.Empty).Length, (string2 ?? string.Empty).Length), 1)); }

    /// <SUMMARY>Computes the Levenshtein Edit Distance between two enumerables.</SUMMARY>
    /// <TYPEPARAM name="T">The type of the items in the enumerables.</TYPEPARAM>
    /// <PARAM name="x">The first enumerable.</PARAM>
    /// <PARAM name="y">The second enumerable.</PARAM>
    /// <RETURNS>The edit distance.</RETURNS>
    public static int EditDistance<T>(IEnumerable<T> x, IEnumerable<T> y)
        where T : IEquatable<T>
    {
      // Validate parameters
      if (x == null) throw new ArgumentNullException("x");
      if (y == null) throw new ArgumentNullException("y");
      //
      // Convert the parameters into IList instances
      // in order to obtain indexing capabilities
      IList<T> first = x as IList<T> ?? new List<T>(x);
      IList<T> second = y as IList<T> ?? new List<T>(y);
      //
      // Get the length of both.  If either is 0, return
      // the length of the other, since that number of insertions
      // would be required.
      int n = first.Count, m = second.Count;
      if (n == 0) return m;
      if (m == 0) return n;
      //
      // Rather than maintain an entire matrix (which would require O(n*m) space),
      // just store the current row and the next row, each of which has a length m+1,
      // so just O(m) space. Initialize the current row.
      int curRow = 0, nextRow = 1;
      int[][] rows = new int[][] { new int[m + 1], new int[m + 1] };
      for (int j = 0; j <= m; ++j) rows[curRow][j] = j;
      // For each virtual row (since we only have physical storage for two)
      for (int i = 1; i <= n; ++i)
      {
        // Fill in the values in the row
        rows[nextRow][0] = i;
        for (int j = 1; j <= m; ++j)
        {
          int dist1 = rows[curRow][j] + 1;
          int dist2 = rows[nextRow][j - 1] + 1;
          int dist3 = rows[curRow][j - 1] + (first[i - 1].Equals(second[j - 1]) ? 0 : 1);
          rows[nextRow][j] = Math.Min(dist1, Math.Min(dist2, dist3));
        }
        // Swap the current and next rows
        if (curRow == 0)
        {
          curRow = 1;
          nextRow = 0;
        }
        else
        {
          curRow = 0;
          nextRow = 1;
        }
      }
      // Return the computed edit distance
      return rows[curRow][m];
    }


    /// <summary>
    /// Returns the properties of the given object as XElements.
    /// Properties with null values are still returned, but as empty
    /// elements. Underscores in property names are replaces with hyphens.
    /// </summary>
    public static IEnumerable<XElement> AsXElements(this object source)
    {
      foreach (PropertyInfo prop in source.GetType().GetProperties())
      {
        object value = prop.GetValue(source, null);
        yield return new XElement(prop.Name.Replace("_", "-"), value);
      }
    }

    /// <summary>
    /// Returns the properties of the given object as XElements.
    /// Properties with null values are returned as empty attributes.
    /// Underscores in property names are replaces with hyphens.
    /// </summary>
    public static IEnumerable<XAttribute> AsXAttributes(this object source)
    {
      foreach (PropertyInfo prop in source.GetType().GetProperties())
      {
        object value = prop.GetValue(source, null);
        yield return new XAttribute(prop.Name.Replace("_", "-"), value ?? "");
      }
    }


    //
    // detect se e' attivato il codice di produzione
    //
    private static bool? _IsDebugBuild = null;
    public static bool IsDebugBuild()
    {
      if (_IsDebugBuild != null)
        return _IsDebugBuild.Value;
      try
      {
        object[] attributes = Assembly.GetExecutingAssembly().GetCustomAttributes(typeof(DebuggableAttribute), false);
        if (attributes != null && attributes.Length == 0)
        {
          _IsDebugBuild = false;
        }
        else
        {
          _IsDebugBuild = attributes.OfType<DebuggableAttribute>().Any(a => a.IsJITOptimizerDisabled);
        }
      }
      catch { }
      return _IsDebugBuild.GetValueOrDefault(true);
    }


  }  // utility




  //
  // Monads extensions
  // http://www.codeproject.com/KB/cs/maybemonads.aspx
  // da estendere anche con:
  // http://www.codeproject.com/Articles/739772/Dynamically-Check-Nested-Values-for-IsNull-Values
  //
  public static class Monads
  {

    public static TResult Return<TInput, TResult>(this TInput o, Func<TInput, TResult> evaluator)
      where TInput : class
    {
      if (o == null) return default(TResult);
      return evaluator(o);
    }


    public static TResult Return<TInput, TResult>(this TInput o, Func<TInput, TResult> evaluator, TResult failureValue)
      where TInput : class
    {
      if (o == null) return failureValue;
      return evaluator(o);
    }

    public static TResult ReturnTry<TInput, TResult>(this TInput o, Func<TInput, TResult> evaluator)
      where TInput : class
    {
      try
      {
        if (o == null) return default(TResult);
        return evaluator(o);
      }
      catch { return default(TResult); }
    }


    public static TResult ReturnTry<TInput, TResult>(this TInput o, Func<TInput, TResult> evaluator, TResult failureValue)
      where TInput : class
    {
      try
      {
        if (o == null) return failureValue;
        return evaluator(o);
      }
      catch { return failureValue; }
    }


    public static TInput If<TInput>(this TInput o, Func<TInput, bool> evaluator)
      where TInput : class
    {
      if (o == null) return null;
      return evaluator(o) ? o : null;
    }


    public static TInput ElseIf<TInput>(this TInput o, Func<TInput, bool> evaluator)
      where TInput : class
    {
      if (o == null) return null;
      return evaluator(o) ? null : o;
    }


    public static TInput With<TInput>(this TInput o, Action<TInput> action)
      where TInput : class
    {
      if (o == null) return null;
      action(o);
      return o;
    }

  }



  /// <summary>
  /// Provides strong-typed reflection of the <typeparamref name="TTarget"/> 
  /// type.
  /// </summary>
  /// <typeparam name="TTarget">Type to reflect.</typeparam>
  public static class Reflect<TTarget>
  {
    /// <summary>
    /// Gets the method represented by the lambda expression.
    /// </summary>
    /// <exception cref="ArgumentNullException">The <paramref name="method"/> is null.</exception>
    /// <exception cref="ArgumentException">The <paramref name="method"/> is not a lambda expression or it does not represent a method invocation.</exception>
    public static MethodInfo GetMethod(Expression<Action<TTarget>> method)
    {
      return GetMethodInfo(method);
    }

    /// <summary>
    /// Gets the method represented by the lambda expression.
    /// </summary>
    /// <exception cref="ArgumentNullException">The <paramref name="method"/> is null.</exception>
    /// <exception cref="ArgumentException">The <paramref name="method"/> is not a lambda expression or it does not represent a method invocation.</exception>
    public static MethodInfo GetMethod<T1>(Expression<Action<TTarget, T1>> method)
    {
      return GetMethodInfo(method);
    }

    /// <summary>
    /// Gets the method represented by the lambda expression.
    /// </summary>
    /// <exception cref="ArgumentNullException">The <paramref name="method"/> is null.</exception>
    /// <exception cref="ArgumentException">The <paramref name="method"/> is not a lambda expression or it does not represent a method invocation.</exception>
    public static MethodInfo GetMethod<T1, T2>(Expression<Action<TTarget, T1, T2>> method)
    {
      return GetMethodInfo(method);
    }

    /// <summary>
    /// Gets the method represented by the lambda expression.
    /// </summary>
    /// <exception cref="ArgumentNullException">The <paramref name="method"/> is null.</exception>
    /// <exception cref="ArgumentException">The <paramref name="method"/> is not a lambda expression or it does not represent a method invocation.</exception>
    public static MethodInfo GetMethod<T1, T2, T3>(Expression<Action<TTarget, T1, T2, T3>> method)
    {
      return GetMethodInfo(method);
    }

    private static MethodInfo GetMethodInfo(Expression method)
    {
      if (method == null) throw new ArgumentNullException("method");

      LambdaExpression lambda = method as LambdaExpression;
      if (lambda == null) throw new ArgumentException("Not a lambda expression", "method");
      if (lambda.Body.NodeType != ExpressionType.Call) throw new ArgumentException("Not a method call", "method");

      return ((MethodCallExpression)lambda.Body).Method;
    }

    /// <summary>
    /// Gets the property represented by the lambda expression.
    /// </summary>
    /// <exception cref="ArgumentNullException">The <paramref name="method"/> is null.</exception>
    /// <exception cref="ArgumentException">The <paramref name="method"/> is not a lambda expression or it does not represent a property access.</exception>
    public static PropertyInfo GetProperty(Expression<Func<TTarget, object>> property)
    {
      PropertyInfo info = GetMemberInfo(property) as PropertyInfo;
      if (info == null) throw new ArgumentException("Member is not a property");

      return info;
    }

    /// <summary>
    /// Gets the field represented by the lambda expression.
    /// </summary>
    /// <exception cref="ArgumentNullException">The <paramref name="method"/> is null.</exception>
    /// <exception cref="ArgumentException">The <paramref name="method"/> is not a lambda expression or it does not represent a field access.</exception>
    public static FieldInfo GetField(Expression<Func<TTarget, object>> field)
    {
      FieldInfo info = GetMemberInfo(field) as FieldInfo;
      if (info == null) throw new ArgumentException("Member is not a field");

      return info;
    }

    private static MemberInfo GetMemberInfo(Expression member)
    {
      if (member == null) throw new ArgumentNullException("member");

      LambdaExpression lambda = member as LambdaExpression;
      if (lambda == null) throw new ArgumentException("Not a lambda expression", "member");

      MemberExpression memberExpr = null;

      // The Func<TTarget, object> we use returns an object, so first statement can be either 
      // a cast (if the field/property does not return an object) or the direct member access.
      if (lambda.Body.NodeType == ExpressionType.Convert)
      {
        // The cast is an unary expression, where the operand is the 
        // actual member access expression.
        memberExpr = ((UnaryExpression)lambda.Body).Operand as MemberExpression;
      }
      else if (lambda.Body.NodeType == ExpressionType.MemberAccess)
      {
        memberExpr = lambda.Body as MemberExpression;
      }

      if (memberExpr == null) throw new ArgumentException("Not a member access", "member");

      return memberExpr.Member;
    }
  }


  public static class PropertyCopy<TTarget> where TTarget : class, new()
  {
    /// <summary>
    /// Copies all readable properties from the source to a new instance
    /// of TTarget.
    /// </summary>
    public static TTarget CopyFrom<TSource>(TSource source) where TSource : class
    {
      return PropertyCopier<TSource>.Copy(source);
    }

    /// <summary>
    /// Static class to efficiently store the compiled delegate which can
    /// do the copying. We need a bit of work to ensure that exceptions are
    /// appropriately propagated, as the exception is generated at type initialization
    /// time, but we wish it to be thrown as an ArgumentException.
    /// </summary>
    private static class PropertyCopier<TSource> where TSource : class
    {
      private static readonly Func<TSource, TTarget> copier;
      private static readonly Exception initializationException;

      internal static TTarget Copy(TSource source)
      {
        if (initializationException != null)
        {
          throw initializationException;
        }
        if (source == null)
        {
          throw new ArgumentNullException("source");
        }
        return copier(source);
      }

      static PropertyCopier()
      {
        try
        {
          copier = BuildCopier();
          initializationException = null;
        }
        catch (Exception e)
        {
          copier = null;
          initializationException = e;
        }
      }

      private static Func<TSource, TTarget> BuildCopier()
      {
        ParameterExpression sourceParameter = Expression.Parameter(typeof(TSource), "source");
        var bindings = new List<MemberBinding>();
        foreach (PropertyInfo sourceProperty in typeof(TSource).GetProperties())
        {
          if (!sourceProperty.CanRead)
          {
            continue;
          }
          PropertyInfo targetProperty = typeof(TTarget).GetProperty(sourceProperty.Name);
          if (targetProperty == null)
          {
            throw new ArgumentException("Property " + sourceProperty.Name + " is not present and accessible in " + typeof(TTarget).FullName);
          }
          if (!targetProperty.CanWrite)
          {
            throw new ArgumentException("Property " + sourceProperty.Name + " is not writable in " + typeof(TTarget).FullName);
          }
          if (!targetProperty.PropertyType.IsAssignableFrom(sourceProperty.PropertyType))
          {
            throw new ArgumentException("Property " + sourceProperty.Name + " has an incompatible type in " + typeof(TTarget).FullName);
          }
          bindings.Add(Expression.Bind(targetProperty, Expression.Property(sourceParameter, sourceProperty)));
        }
        Expression initializer = Expression.MemberInit(Expression.New(typeof(TTarget)), bindings);
        return Expression.Lambda<Func<TSource, TTarget>>(initializer, sourceParameter).Compile();
      }
    }
  }



  //
  // IEqualityComparer for Lambda Expressions
  //
  public class LambdaComparer<T> : IEqualityComparer<T>
  {
    private readonly Func<T, T, bool> _lambdaComparer;
    private readonly Func<T, int> _lambdaHash;


    public LambdaComparer(Func<T, T, bool> lambdaComparer) :
      this(lambdaComparer, o => 0)
    {
    }


    public LambdaComparer(Func<T, T, bool> lambdaComparer, Func<T, int> lambdaHash)
    {
      if (lambdaComparer == null)
        throw new ArgumentNullException("lambdaComparer");
      if (lambdaHash == null)
        throw new ArgumentNullException("lambdaHash");

      _lambdaComparer = lambdaComparer;
      _lambdaHash = lambdaHash;
    }


    public bool Equals(T x, T y)
    {
      return _lambdaComparer(x, y);
    }


    public int GetHashCode(T obj)
    {
      return _lambdaHash(obj);
    }

  }



  //
  // classi per wrappare un oggetto (o un blocco di codice)
  // in un using implementando l'interface IDisposable
  //

  // used for symetric Disposable actions like login, logout
  public class AutoDisposable : IDisposable
  {
    private Action Cleanup { get; set; }
    public AutoDisposable(Action init, Action cleanup)
    {
      Cleanup = cleanup;
      if (init != null) init();
    }
    void IDisposable.Dispose() { if (Cleanup != null) Cleanup(); }
  }


  // used for symmetric Disposable actions that must pass
  // over an object from init to cleanup and that
  // need to provide the item to the "using" body
  public class AutoDisposable<T> : IDisposable
  {
    private Action<T> Cleanup { get; set; }
    public T Item { get; private set; }
    public AutoDisposable(Func<T> init, Action<T> cleanup)
    {
      Cleanup = cleanup;
      Item = (init != null) ? init() : default(T);
    }
    void IDisposable.Dispose() { if (Cleanup != null) Cleanup(Item); }
  }



  /// <summary>
  /// Type chaining an IEnumerable&lt;T&gt; to allow the iterating code
  /// to detect the first and last entries simply.
  /// </summary>
  /// <typeparam name="T">Type to iterate over</typeparam>
  public class SmartEnumerable<T> : IEnumerable<SmartEnumerable<T>.Entry>
  {
    /// <summary>
    /// Enumerable we proxy to
    /// </summary>
    readonly IEnumerable<T> enumerable;

    /// <summary>
    /// Constructor.
    /// </summary>
    /// <param name="enumerable">Collection to enumerate. Must not be null.</param>
    public SmartEnumerable(IEnumerable<T> enumerable)
    {
      if (enumerable == null)
      {
        throw new ArgumentNullException("enumerable");
      }
      this.enumerable = enumerable;
    }

    /// <summary>
    /// Returns an enumeration of Entry objects, each of which knows
    /// whether it is the first/last of the enumeration, as well as the
    /// current value.
    /// </summary>
    public IEnumerator<Entry> GetEnumerator()
    {
      using (IEnumerator<T> enumerator = enumerable.GetEnumerator())
      {
        if (!enumerator.MoveNext())
        {
          yield break;
        }
        bool isFirst = true;
        bool isLast = false;
        int index = 0;
        while (!isLast)
        {
          T current = enumerator.Current;
          isLast = !enumerator.MoveNext();
          yield return new Entry(isFirst, isLast, current, index++);
          isFirst = false;
        }
      }
    }

    /// <summary>
    /// Non-generic form of GetEnumerator.
    /// </summary>
    IEnumerator IEnumerable.GetEnumerator()
    {
      return GetEnumerator();
    }

    /// <summary>
    /// Represents each entry returned within a collection,
    /// containing the value and whether it is the first and/or
    /// the last entry in the collection's. enumeration
    /// </summary>
    public class Entry
    {
      readonly bool isFirst;
      readonly bool isLast;
      readonly T value;
      readonly int index;

      internal Entry(bool isFirst, bool isLast, T value, int index)
      {
        this.isFirst = isFirst;
        this.isLast = isLast;
        this.value = value;
        this.index = index;
      }

      /// <summary>
      /// The value of the entry.
      /// </summary>
      public T Value { get { return value; } }

      /// <summary>
      /// Whether or not this entry is first in the collection's enumeration.
      /// </summary>
      public bool IsFirst { get { return isFirst; } }

      /// <summary>
      /// Whether or not this entry is last in the collection's enumeration.
      /// </summary>
      public bool IsLast { get { return isLast; } }

      /// <summary>
      /// The 0-based index of this entry (i.e. how many entries have been returned before this one)
      /// </summary>
      public int Index { get { return index; } }
    }
  }



  public struct TimedLock : IDisposable
  {

    public static TimedLock Lock(object o)
    {
      return Lock(o, TimeSpan.FromSeconds(10));
    }


    public static TimedLock Lock(object o, TimeSpan timeout)
    {
      TimedLock tl = new TimedLock(o);
      if (!Monitor.TryEnter(o, timeout))
      {
#if DEBUG
        System.GC.SuppressFinalize(tl.leakDetector);
#endif
        throw new LockTimeoutException();
      }

      return tl;
    }


    private TimedLock(object o)
    {
      target = o;
#if DEBUG
      leakDetector = new Sentinel();
#endif
    }
    private object target;


    public void Dispose()
    {
      Monitor.Exit(target);

      // It's a bad error if someone forgets to call Dispose,
      // so in Debug builds, we put a finalizer in to detect
      // the error. If Dispose is called, we suppress the
      // finalizer.
#if DEBUG
      GC.SuppressFinalize(leakDetector);
#endif
    }

#if DEBUG
    // (In Debug mode, we make it a class so that we can add a finalizer
    // in order to detect when the object is not freed.)
    private class Sentinel
    {
      ~Sentinel()
      {
        // If this finalizer runs, someone somewhere failed to
        // call Dispose, which means we've failed to leave
        // a monitor!
        System.Diagnostics.Debug.Fail("Undisposed lock");
      }
    }
    private Sentinel leakDetector;
#endif
  }


  public class LockTimeoutException : ApplicationException
  {
    public LockTimeoutException()
      : base("Timeout waiting for lock")
    {
    }
  }



  public static class StaticRandom
  {
    static Random random = new Random();
    static object myLock = new object();

    /// <summary>
    /// Returns a nonnegative random number. 
    /// </summary>		
    /// <returns>A 32-bit signed integer greater than or equal to zero and less than Int32.MaxValue.</returns>
    public static int Next()
    {
      lock (myLock)
      {
        return random.Next();
      }
    }

    /// <summary>
    /// Returns a nonnegative random number less than the specified maximum. 
    /// </summary>
    /// <returns>
    /// A 32-bit signed integer greater than or equal to zero, and less than maxValue; 
    /// that is, the range of return values includes zero but not maxValue.
    /// </returns>
    /// <exception cref="ArgumentOutOfRangeException">maxValue is less than zero.</exception>
    public static int Next(int max)
    {
      lock (myLock)
      {
        return random.Next(max);
      }
    }

    /// <summary>
    /// Returns a random number within a specified range. 
    /// </summary>
    /// <param name="min">The inclusive lower bound of the random number returned. </param>
    /// <param name="max">
    /// The exclusive upper bound of the random number returned. 
    /// maxValue must be greater than or equal to minValue.
    /// </param>
    /// <returns>
    /// A 32-bit signed integer greater than or equal to minValue and less than maxValue;
    /// that is, the range of return values includes minValue but not maxValue.
    /// If minValue equals maxValue, minValue is returned.
    /// </returns>
    /// <exception cref="ArgumentOutOfRangeException">minValue is greater than maxValue.</exception>
    public static int Next(int min, int max)
    {
      lock (myLock)
      {
        return random.Next(min, max);
      }
    }

    /// <summary>
    /// Returns a random number between 0.0 and 1.0.
    /// </summary>
    /// <returns>A double-precision floating point number greater than or equal to 0.0, and less than 1.0.</returns>
    public static double NextDouble()
    {
      lock (myLock)
      {
        return random.NextDouble();
      }
    }

    /// <summary>
    /// Fills the elements of a specified array of bytes with random numbers.
    /// </summary>
    /// <param name="buffer">An array of bytes to contain random numbers.</param>
    /// <exception cref="ArgumentNullException">buffer is a null reference (Nothing in Visual Basic).</exception>
    public static void NextBytes(byte[] buffer)
    {
      lock (myLock)
      {
        random.NextBytes(buffer);
      }
    }
  }



}  // namespace Ikon
