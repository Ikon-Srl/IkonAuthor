/*
 * 
 * IkonPortal
 * 
 * Copyright (C) 2010 Ikon Srl
 * Tutti i Diritti Riservati. All Rights Reserved
 * 
 */


using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Data;
using System.Configuration;
using System.Web;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security;
using System.Security.Principal;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;
using System.ComponentModel;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;  // assembly System.ServiceModel.Web
using System.Linq.Expressions;
using System.Web.Caching;
using System.Drawing;
using ICSharpCode.SharpZipLib.Zip.Compression.Streams;
using LinqKit;

using Ikon;
using Ikon.Log;
using Ikon.Support;
using Ikon.GD;


namespace Ikon.IKGD.Library.Imaging
{

  //
  // classe statica per analizzare l'header dei files swf (anche compressi) e determinarne le dimensioni
  //
  public static class SwfParser
  {

    public static Rectangle GetDimensions(String filePath)
    {
      using (FileStream stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
      {
        return GetDimensions(stream);
      }
    }


    public static Rectangle GetDimensions(byte[] data)
    {
      using (MemoryStream ms = new MemoryStream(data))
      {
        return GetDimensions(ms);
      }
    }


    public static Rectangle GetDimensions(Stream stream)
    {
      Stream inputStream = null;
      byte[] signature = new byte[8];
      byte[] rect = new byte[8];
      stream.Read(signature, 0, 8);
      if ("CWS" == System.Text.Encoding.ASCII.GetString(signature, 0, 3))
      {
        inputStream = new InflaterInputStream(stream);
      }
      else
      {
        inputStream = stream;
      }
      inputStream.Read(rect, 0, 8);
      int nbits = rect[0] >> 3;
      rect[0] = (byte)(rect[0] & 0x07);
      String bits = ByteArrayToBitString(rect);
      bits = bits.Remove(0, 5);
      int[] dims = new int[4];
      for (int i = 0; i < 4; i++)
      {
        char[] dest = new char[nbits];
        bits.CopyTo(0, dest, 0, bits.Length > nbits ? nbits : bits.Length);
        bits = bits.Remove(0, bits.Length > nbits ? nbits : bits.Length);
        dims[i] = BitStringToInteger(new String(dest)) / 20;
      }
      return new Rectangle(0, 0, dims[1] - dims[0], dims[3] - dims[2]);
    }


    private static int BitStringToInteger(String bits)
    {
      int converted = 0;
      for (int i = 0; i < bits.Length; i++)
      {
        converted = (converted << 1) + (bits[i] == '1' ? 1 : 0);
      }
      return converted;
    }


    private static String ByteArrayToBitString(byte[] byteArray)
    {
      byte[] newByteArray = new byte[byteArray.Length];
      Array.Copy(byteArray, newByteArray, byteArray.Length);
      String converted = "";
      for (int i = 0; i < newByteArray.Length; i++)
      {
        for (int j = 0; j < 8; j++)
        {
          converted += (newByteArray[i] & 0x80) > 0 ? "1" : "0";
          newByteArray[i] <<= 1;
        }
      }
      return converted;
    }

  }


}

