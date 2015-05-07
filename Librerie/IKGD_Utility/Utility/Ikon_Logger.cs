/*
 * 
 * Intranet Ikon
 * 
 * Copyright (C) 2008 Ikon Srl
 * Tutti i Diritti Riservati. All Rights Reserved
 * 
 */


using System;
using System.IO;
using System.Text;
using System.Web;
using log4net;
using log4net.Config;


namespace Ikon.Log
{
  public static class Logger
  {
    private static object _lock = new object();
    private static ILog _log = null;

    static Logger()
    {
    }

    public static ILog Log
    {
      get
      {
        lock (_lock)
        {
          if (_log == null)
            _log = LogManager.GetLogger("MV_CMS");
          if (!_log.Logger.Repository.Configured)
            XmlConfigurator.Configure();
        }
        return _log;
      }
    }

    public static void Trace(string format, params object[] args)
    {
      HttpContext.Current.Trace.Write("Log", string.Format(format, args));
    }
  }

  public class TimedLog : IDisposable
  {
    private string _Message;
    private long _StartTicks;

    public TimedLog(string message) : this(HttpContext.Current.User.Identity.Name, message) { }
    public TimedLog(string userName, string message)
    {
      this._Message = userName + '\t' + message;
      this._StartTicks = DateTime.Now.Ticks;
    }

    void IDisposable.Dispose()
    {
      string msg = this._Message + '\t' + TimeSpan.FromTicks(DateTime.Now.Ticks - this._StartTicks).TotalSeconds.ToString();
      //EntLibHelper.PerformanceLog(msg);
      //System.Diagnostics.Debug.WriteLine(msg);
      HttpContext.Current.Trace.Write("TimedLog", msg);
    }
  }


  public class LINQ_Logger : TextWriter
  {

    private Encoding _encoding;
    public override Encoding Encoding
    {
      get
      {
        if (_encoding == null)
        {
          _encoding = new UnicodeEncoding(false, false);
        }
        return _encoding;
      }
    }

    public override void Write(string value)
    {
      HttpContext.Current.Trace.Write(value);
      //Console.WriteLine("LINQ:\n{0}".FormatString(value));
      //base.Write(value);
    }

    public override void Write(char[] buffer, int index, int count)
    {
      Write(new string(buffer, index, count));
    }

  }

}
