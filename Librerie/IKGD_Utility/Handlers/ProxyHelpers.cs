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
using System.Web;
using System.Web.Security;
using System.Web.UI;
using System.Web.UI.WebControls;
using System.Web.UI.WebControls.WebParts;
using System.Web.UI.HtmlControls;
using System.Net;
using System.Threading;
using System.IO;
using System.Collections.Generic;



namespace Ikon.Handlers
{


  public static class HttpHelper
  {

    public static HttpWebRequest CreateScalableHttpWebRequest(string url)
    {
      HttpWebRequest request = null;
      try
      {
        request = WebRequest.Create(url) as HttpWebRequest;
        request.Headers.Add("Accept-Encoding", "gzip");
        request.AutomaticDecompression = DecompressionMethods.GZip;
        request.MaximumAutomaticRedirections = 2;
        request.ReadWriteTimeout = 5000;
        request.Timeout = 3000;
        request.Accept = "*/*";
        request.Headers.Add("Accept-Language", "en-US");
        request.UserAgent = "Mozilla/5.0 (Windows; U; Windows NT 6.0; en-US; rv:1.8.1.6) Gecko/20070725 Firefox/2.0.0.6";
      }
      catch { }
      return request;
    }


    public static void CacheResponse(HttpContext context, DateTime CacheExpiryDate)
    {
      context.Response.Cache.SetCacheability(HttpCacheability.Public);
      context.Response.Cache.SetValidUntilExpires(true);
      context.Response.Cache.SetExpires(CacheExpiryDate);
      context.Response.Cache.AppendCacheExtension("must-revalidate, proxy-revalidate");
    }


    public static void CacheResponse(HttpContext context, int durationInSeconds) { CacheResponse(context, durationInSeconds, null, null); }
    public static void CacheResponse(HttpContext context, int durationInSeconds, string ETag, DateTime? LastModified)
    {
      TimeSpan cacheDuration = TimeSpan.FromSeconds(durationInSeconds);
      context.Response.Cache.SetCacheability(HttpCacheability.Public);
      context.Response.Cache.SetValidUntilExpires(true);
      if (LastModified != null)
      {
        context.Response.Cache.SetLastModified(LastModified.Value);
      }
      context.Response.Cache.SetExpires(DateTime.Now.Add(cacheDuration));
      context.Response.Cache.SetMaxAge(cacheDuration);   // se specificato esegue l'override di SetExpires?
      context.Response.Cache.AppendCacheExtension("must-revalidate, proxy-revalidate");
      if (ETag.IsNotEmpty())
      {
        context.Response.Cache.SetETag(ETag);
      }
    }


    public static void DoNotCacheResponse(HttpContext context) { DoNotCacheResponse(context, null); }
    public static void DoNotCacheResponse(HttpContext context, string ETag)
    {
      context.Response.Cache.SetNoServerCaching();
      context.Response.Cache.SetNoStore();
      context.Response.Cache.SetMaxAge(TimeSpan.Zero);
      context.Response.Cache.AppendCacheExtension("must-revalidate, proxy-revalidate");
      context.Response.Cache.SetExpires(DateTime.Now.AddYears(-1));
      if (ETag.IsNotEmpty())
      {
        context.Response.Cache.SetETag(ETag);
      }
    }


    /// <summary>
    /// setup caching headers for browser
    /// use null for no action and a negative value to disable any caching
    /// </summary>
    /// <param name="durationInSeconds"></param>
    public static void CacheResponse(int? durationInSeconds)
    {
      if (durationInSeconds > 0)
        CacheResponse(HttpContext.Current, durationInSeconds.Value);
      else if (durationInSeconds < 0)
        DoNotCacheResponse(HttpContext.Current);
    }

  }


  public class CachedContent
  {
    public string ContentType { get; set; }
    public string ContentEncoding { get; set; }
    public string ContentLength { get; set; }
    public MemoryStream Content { get; set; }
    public DateTime CacheExpiry { get; set; }
  }


  public class AsyncState
  {
    public HttpContext Context { get; set; }
    public string Url { get; set; }
    public int CacheDuration { get; set; }
    public HttpWebRequest Request { get; set; }
  }


  public class SyncResult : IAsyncResult
  {
    public CachedContent Content;
    public HttpContext Context;

    object IAsyncResult.AsyncState { get { return new object(); } }
    WaitHandle IAsyncResult.AsyncWaitHandle { get { return new ManualResetEvent(true); } }
    bool IAsyncResult.CompletedSynchronously { get { return true; } }
    bool IAsyncResult.IsCompleted { get { return true; } }
  }


  public class PipeStreamBlock : PipeStream
  {
    private int _Length = 0;
    private Queue<byte[]> _Buffer = new Queue<byte[]>(1000);


    public PipeStreamBlock(int readWriteTimeout)
      : base(readWriteTimeout)
    {
    }


    protected override void WriteToBuffer(byte[] buffer, int offset, int count)
    {
      byte[] bufferCopy = new byte[count];
      Buffer.BlockCopy(buffer, offset, bufferCopy, 0, count);
      this._Buffer.Enqueue(bufferCopy);
      this._Length += count;
    }


    protected override int ReadToBuffer(byte[] buffer, int offset, int count)
    {
      if (0 == this._Buffer.Count) return 0;
      byte[] chunk = this._Buffer.Dequeue();
      // It's possible the chunk has smaller number of bytes than buffer capacity
      Buffer.BlockCopy(chunk, 0, buffer, offset, chunk.Length);
      this._Length -= chunk.Length;
      return chunk.Length;
    }


    public override long Length { get { return this._Length; } }


    protected override void Dispose(bool disposing)
    {
      base.Dispose(disposing);
      this._Length = 0;
      _Buffer.Clear();
    }

  }


  public class PipeStream : Stream
  {
    /// <summary>
    /// Queue of bytes provides the datastructure for transmitting from an
    /// input stream to an output stream.
    /// </summary>
    /// <remarks>Possible more effecient ways to accomplish this.</remarks>
    private Queue<byte> mBuffer = new Queue<byte>();

    /// <summary>
    /// Event occurs after data is written.
    /// </summary>
    private ManualResetEvent mWriteEvent;

    /// <summary>
    /// Event occurs after data is read.
    /// </summary>
    private ManualResetEvent mReadEvent;

    /// <summary>
    /// Indicates that the input stream has been flushed and that
    /// all remaining data should be written to the output stream.
    /// </summary>
    private bool mFlushed = false;

    /// <summary>
    /// Maximum number of bytes to store in the buffer.
    /// </summary>
    private long mMaxBufferLength = 1 * MB;

    /// <summary>
    /// Setting this to true will cause Read() to block if it appears
    /// that it will run out of data.
    /// </summary>
    private bool mBlockLastRead = false;

    private int _ReadWriteTimeout;

    private long _TotalWrite = 0;


    /// <summary>
    /// Number of bytes in a kilobyte
    /// </summary>
    public const long KB = 1024;

    /// <summary>
    /// Number of bytes in a megabyte
    /// </summary>
    public const long MB = KB * 1024;


    /// <summary>
    /// Gets or sets the maximum number of bytes to store in the buffer.
    /// </summary>
    /// <value>The length of the max buffer.</value>
    public long MaxBufferLength
    {
      get { return mMaxBufferLength; }
      set { mMaxBufferLength = value; }
    }

    /// <summary>
    /// Gets or sets a value indicating whether to block last read method before the buffer is empty.
    /// When true, Read() will block until it can fill the passed in buffer and count.
    /// When false, Read() will not block, returning all the available buffer data.
    /// </summary>
    /// <remarks>
    /// Setting to true will remove the possibility of ending a stream reader prematurely.
    /// </remarks>
    /// <value>
    /// 	<c>true</c> if block last read method before the buffer is empty; otherwise, <c>false</c>.
    /// </value>
    public bool BlockLastReadBuffer
    {
      get { return mBlockLastRead; }
      set
      {
        mBlockLastRead = value;
        // when turning off the block last read, signal Read() that it may now read the rest of the buffer.
        if (!mBlockLastRead)
          mWriteEvent.Set();
      }
    }


    /// <summary>
    /// Initializes a new instance of the <see cref="PipeStream"/> class.
    /// </summary>
    public PipeStream(int readWriteTimeout)
    {
      this._ReadWriteTimeout = readWriteTimeout;
      mWriteEvent = new ManualResetEvent(false);
      mReadEvent = new ManualResetEvent(false);
    }


    ///<summary>
    ///When overridden in a derived class, clears all buffers for this stream and causes any buffered data to be written to the underlying device.
    ///</summary>
    ///
    ///<exception cref="T:System.IO.IOException">An I/O error occurs. </exception><filterpriority>2</filterpriority>
    public override void Flush()
    {
      mFlushed = true;
      mWriteEvent.Set(); // signal any waiting read events.
    }

    ///<summary>
    ///When overridden in a derived class, sets the position within the current stream.
    ///</summary>
    ///
    ///<returns>
    ///The new position within the current stream.
    ///</returns>
    ///
    ///<param name="offset">A byte offset relative to the origin parameter. </param>
    ///<param name="origin">A value of type <see cref="T:System.IO.SeekOrigin"></see> indicating the reference point used to obtain the new position. </param>
    ///<exception cref="T:System.IO.IOException">An I/O error occurs. </exception>
    ///<exception cref="T:System.NotSupportedException">The stream does not support seeking, such as if the stream is constructed from a pipe or console output. </exception>
    ///<exception cref="T:System.ObjectDisposedException">Methods were called after the stream was closed. </exception><filterpriority>1</filterpriority>
    public override long Seek(long offset, SeekOrigin origin)
    {
      throw new NotImplementedException();
    }

    ///<summary>
    ///When overridden in a derived class, sets the length of the current stream.
    ///</summary>
    ///
    ///<param name="value">The desired length of the current stream in bytes. </param>
    ///<exception cref="T:System.NotSupportedException">The stream does not support both writing and seeking, such as if the stream is constructed from a pipe or console output. </exception>
    ///<exception cref="T:System.IO.IOException">An I/O error occurs. </exception>
    ///<exception cref="T:System.ObjectDisposedException">Methods were called after the stream was closed. </exception><filterpriority>2</filterpriority>
    public override void SetLength(long value)
    {
      throw new NotImplementedException();
    }


    ///<summary>
    ///When overridden in a derived class, reads a sequence of bytes from the current stream and advances the position within the stream by the number of bytes read.
    ///</summary>
    ///
    ///<returns>
    ///The total number of bytes read into the buffer. This can be less than the number of bytes requested if that many bytes are not currently available, or zero (0) if the end of the stream has been reached.
    ///</returns>
    ///
    ///<param name="offset">The zero-based byte offset in buffer at which to begin storing the data read from the current stream. </param>
    ///<param name="count">The maximum number of bytes to be read from the current stream. </param>
    ///<param name="buffer">An array of bytes. When this method returns, the buffer contains the specified byte array with the values between offset and (offset + count - 1) replaced by the bytes read from the current source. </param>
    ///<exception cref="T:System.ArgumentException">The sum of offset and count is larger than the buffer length. </exception>
    ///<exception cref="T:System.ObjectDisposedException">Methods were called after the stream was closed. </exception>
    ///<exception cref="T:System.NotSupportedException">The stream does not support reading. </exception>
    ///<exception cref="T:System.ArgumentNullException">buffer is null. </exception>
    ///<exception cref="T:System.IO.IOException">An I/O error occurs. </exception>
    ///<exception cref="T:System.ArgumentOutOfRangeException">offset or count is negative. </exception><filterpriority>1</filterpriority>
    public override int Read(byte[] buffer, int offset, int count)
    {
      //using (new ProxyHelpers.TimedLog("PipeStrem\tRead"))
      //{
      if (offset != 0)
        throw new NotImplementedException("Offsets with value of non-zero are not supported");
      if (buffer == null)
        throw new ArgumentException("Buffer is null");
      if (offset + count > buffer.Length)
        throw new ArgumentException("The sum of offset and count is greater than the buffer length. ");
      if (offset < 0 || count < 0)
        throw new ArgumentOutOfRangeException("offset or count is negative.");
      if (BlockLastReadBuffer && count >= mMaxBufferLength)
        throw new ArgumentException("count > mMaxBufferLength");

      if (count == 0)
        return 0;

      int readLength;

      while ((Length < count && !mFlushed) || (Length < (count + 1) && BlockLastReadBuffer))
      {
        mWriteEvent.Reset(); // turn off an existing write signal
        mReadEvent.Set(); // signal any waiting reads, preventing deadlock
        mWriteEvent.WaitOne(this._ReadWriteTimeout, false); // wait until a write occurs
      }
      lock (mBuffer)
      {
        // fill the read buffer
        readLength = ReadToBuffer(buffer, offset, count);
        mReadEvent.Set();
      }
      return readLength;
      //}
    }

    protected virtual int ReadToBuffer(byte[] buffer, int offset, int count)
    {
      int readLength = 0;
      for (; readLength < count && Length > 0; readLength++)
      {
        buffer[readLength] = mBuffer.Dequeue();
      }
      return readLength;
    }

    ///<summary>
    ///When overridden in a derived class, writes a sequence of bytes to the current stream and advances the current position within this stream by the number of bytes written.
    ///</summary>
    ///
    ///<param name="offset">The zero-based byte offset in buffer at which to begin copying bytes to the current stream. </param>
    ///<param name="count">The number of bytes to be written to the current stream. </param>
    ///<param name="buffer">An array of bytes. This method copies count bytes from buffer to the current stream. </param>
    ///<exception cref="T:System.IO.IOException">An I/O error occurs. </exception>
    ///<exception cref="T:System.NotSupportedException">The stream does not support writing. </exception>
    ///<exception cref="T:System.ObjectDisposedException">Methods were called after the stream was closed. </exception>
    ///<exception cref="T:System.ArgumentNullException">buffer is null. </exception>
    ///<exception cref="T:System.ArgumentException">The sum of offset and count is greater than the buffer length. </exception>
    ///<exception cref="T:System.ArgumentOutOfRangeException">offset or count is negative. </exception><filterpriority>1</filterpriority>
    public override void Write(byte[] buffer, int offset, int count)
    {
      if (buffer == null)
        throw new ArgumentException("Buffer is null");
      if (offset + count > buffer.Length)
        throw new ArgumentException("The sum of offset and count is greater than the buffer length. ");
      if (offset < 0 || count < 0)
        throw new ArgumentOutOfRangeException("offset or count is negative.");
      if (count == 0)
        return;
      while (Length >= mMaxBufferLength)
      {
        mReadEvent.Reset();
        mWriteEvent.Set(); // release any blocked read events
        mReadEvent.WaitOne(this._ReadWriteTimeout, false);
      }
      lock (mBuffer)
      {
        mFlushed = false; // if it were flushed before, it soon will not be.
        WriteToBuffer(buffer, offset, count);
        this._TotalWrite += count;
        mWriteEvent.Set(); // signal that write has occured
      }
    }


    protected virtual void WriteToBuffer(byte[] buffer, int offset, int count)
    {
      // queue up the buffer data
      for (int i = offset; i < count; i++)
      {
        mBuffer.Enqueue(buffer[i]);
      }
    }

    ///<summary>
    ///When overridden in a derived class, gets a value indicating whether the current stream supports reading.
    ///</summary>
    ///
    ///<returns>
    ///true if the stream supports reading; otherwise, false.
    ///</returns>
    ///<filterpriority>1</filterpriority>
    public override bool CanRead { get { return true; } }

    ///<summary>
    ///When overridden in a derived class, gets a value indicating whether the current stream supports seeking.
    ///</summary>
    ///
    ///<returns>
    ///true if the stream supports seeking; otherwise, false.
    ///</returns>
    ///<filterpriority>1</filterpriority>
    public override bool CanSeek { get { return false; } }

    ///<summary>
    ///When overridden in a derived class, gets a value indicating whether the current stream supports writing.
    ///</summary>
    ///
    ///<returns>
    ///true if the stream supports writing; otherwise, false.
    ///</returns>
    ///<filterpriority>1</filterpriority>
    public override bool CanWrite { get { return true; } }

    ///<summary>
    ///When overridden in a derived class, gets the length in bytes of the stream.
    ///</summary>
    ///
    ///<returns>
    ///A long value representing the length of the stream in bytes.
    ///</returns>
    ///
    ///<exception cref="T:System.NotSupportedException">A class derived from Stream does not support seeking. </exception>
    ///<exception cref="T:System.ObjectDisposedException">Methods were called after the stream was closed. </exception><filterpriority>1</filterpriority>
    public override long Length { get { return mBuffer.Count; } }

    ///<summary>
    ///When overridden in a derived class, gets or sets the position within the current stream.
    ///</summary>
    ///
    ///<returns>
    ///The current position within the stream.
    ///</returns>
    ///
    ///<exception cref="T:System.IO.IOException">An I/O error occurs. </exception>
    ///<exception cref="T:System.NotSupportedException">The stream does not support seeking. </exception>
    ///<exception cref="T:System.ObjectDisposedException">Methods were called after the stream was closed. </exception><filterpriority>1</filterpriority>
    public override long Position
    {
      get { return 0; }
      set { throw new NotImplementedException(); }
    }


    protected override void Dispose(bool disposing)
    {
      base.Dispose(disposing);

      mBuffer.Clear();
      TotalWrite = 0;
      (mWriteEvent as IDisposable).Dispose();
      (mReadEvent as IDisposable).Dispose();
    }


    public long TotalWrite { get { return _TotalWrite; } set { _TotalWrite = value; } }
  }


}
