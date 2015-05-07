/*
 * 
 * Intranet Ikon
 * 
 * Copyright (C) 2008 Ikon Srl
 * Tutti i Diritti Riservati. All Rights Reserved
 * 
 */


using System;
using System.Diagnostics;
using System.Threading;
using System.Web;
using System.Net;
using System.IO;
using System.IO.Compression;
using System.Web.Caching;



namespace Ikon.Handlers
{

  //
  // HttpHandler class for a streaming async proxy handler for downloading external urls
  // define a ProxyAjax.axd file on the webSite referencing this class, then call the url:
  // http://website/ProxyAjax.axd?url={externalUrl}&type=mimeType&cache=number
  // type and cache are optional parameters, cache expiry interval is expressed in seconds
  //
  public class ProxyStreaming : IHttpAsyncHandler
  {
    const int BUFFER_SIZE = 8 * 1024;
    private PipeStream _PipeStream;
    private Stream _ResponseStream;


    public bool IsReusable { get { return false; } }


    private void DownloadData(HttpWebRequest request, HttpWebResponse response, HttpContext context, int cacheDuration)
    {
      MemoryStream responseBuffer = new MemoryStream();
      context.Response.Buffer = false;
      try
      {
        if (response.StatusCode != HttpStatusCode.OK)
        {
          context.Response.StatusCode = (int)response.StatusCode;
          return;
        }
        using (Stream readStream = response.GetResponseStream())
        {
          if (context.Response.IsClientConnected)
          {
            string contentLength = string.Empty;
            string contentEncoding = string.Empty;
            ProduceResponseHeader(response, context, cacheDuration, out contentLength, out contentEncoding);
            int totalBytesWritten = TransmitDataAsyncOptimized(context, readStream, responseBuffer);
            if (cacheDuration > 0)
            {
              // Cache the content on server for specific duration
              CachedContent cache = new CachedContent();
              cache.Content = responseBuffer;
              cache.ContentEncoding = contentEncoding;
              cache.ContentLength = contentLength;
              cache.ContentType = response.ContentType;
              cache.CacheExpiry = DateTime.Now.AddSeconds(cacheDuration);
              string cacheKey = "ProxyStreaming:" + request.RequestUri.ToString();
              context.Cache.Remove(cacheKey);
              context.Cache.Insert(cacheKey, cache, null, cache.CacheExpiry, Cache.NoSlidingExpiration, CacheItemPriority.Low, null);
            }
          }
          context.Response.Flush();
        }
      }
      catch
      {
        request.Abort();
      }
    }


    private int TransmitDataAsyncOptimized(HttpContext context, Stream readStream, MemoryStream responseBuffer)
    {
      this._ResponseStream = readStream;
      _PipeStream = new PipeStreamBlock(10000);
      //_PipeStream = new PipeStream(10000);
      byte[] buffer = new byte[BUFFER_SIZE];
      // Asynchronously read content form response stream
      Thread readerThread = new Thread(new ThreadStart(this.ReadData));
      readerThread.Start();
      //ThreadPool.QueueUserWorkItem(new WaitCallback(this.ReadData));
      // Write to response 
      int totalBytesWritten = 0;
      int dataReceived;
      byte[] outputBuffer = new byte[BUFFER_SIZE];
      int responseBufferPos = 0;
      while ((dataReceived = this._PipeStream.Read(buffer, 0, BUFFER_SIZE)) > 0)
      {
        // if about to overflow, transmit the response buffer and restart
        int bufferSpaceLeft = BUFFER_SIZE - responseBufferPos;
        if (bufferSpaceLeft < dataReceived)
        {
          Buffer.BlockCopy(buffer, 0, outputBuffer, responseBufferPos, bufferSpaceLeft);
          context.Response.OutputStream.Write(outputBuffer, 0, BUFFER_SIZE);
          responseBuffer.Write(outputBuffer, 0, BUFFER_SIZE);
          totalBytesWritten += BUFFER_SIZE;
          // Initialize response buffer and copy the bytes that were not sent
          responseBufferPos = 0;
          int bytesLeftOver = dataReceived - bufferSpaceLeft;
          Buffer.BlockCopy(buffer, bufferSpaceLeft, outputBuffer, 0, bytesLeftOver);
          responseBufferPos = bytesLeftOver;
        }
        else
        {
          Buffer.BlockCopy(buffer, 0, outputBuffer, responseBufferPos, dataReceived);
          responseBufferPos += dataReceived;
        }
      }
      // If some data left in the response buffer, send it
      if (responseBufferPos > 0)
      {
        context.Response.OutputStream.Write(outputBuffer, 0, responseBufferPos);
        responseBuffer.Write(outputBuffer, 0, responseBufferPos);
        totalBytesWritten += responseBufferPos;
      }
      _PipeStream.Dispose();
      return totalBytesWritten;
    }


    private void ProduceResponseHeader(HttpWebResponse response, HttpContext context, int cacheDuration, out string contentLength, out string contentEncoding)
    {
      // produce cache headers for response caching
      if (cacheDuration > 0)
        HttpHelper.CacheResponse(context, cacheDuration);
      else
        HttpHelper.DoNotCacheResponse(context);
      // If content length is not specified, this the response will be sent as Transfer-Encoding: chunked
      contentLength = response.GetResponseHeader("Content-Length");
      if (!string.IsNullOrEmpty(contentLength))
        context.Response.AppendHeader("Content-Length", contentLength);
      // If downloaded data is compressed, Content-Encoding will have either gzip or deflate
      contentEncoding = response.GetResponseHeader("Content-Encoding");
      if (!string.IsNullOrEmpty(contentEncoding))
        context.Response.AppendHeader("Content-Encoding", contentEncoding);
      context.Response.ContentType = response.ContentType;
    }


    private void ReadData()
    {
      byte[] buffer = new byte[BUFFER_SIZE];
      int dataReceived;
      int totalBytesFromSocket = 0;
      try
      {
        while ((dataReceived = this._ResponseStream.Read(buffer, 0, BUFFER_SIZE)) > 0)
        {
          this._PipeStream.Write(buffer, 0, dataReceived);
          totalBytesFromSocket += dataReceived;
        }
      }
      catch { }
      finally
      {
        this._ResponseStream.Dispose();
        this._PipeStream.Flush();
      }
    }


    public void ProcessRequest(HttpContext context)
    {
      string url = context.Request["url"];
      string contentType = context.Request["type"];
      int cacheDuration = Utility.TryParse<int>(context.Request["cache"], 0);
      //
      if (cacheDuration > 0)
      {
        CachedContent content = context.Cache["ProxyStreaming:" + url] as CachedContent;
        if (content != null)
        {
          if (!string.IsNullOrEmpty(content.ContentEncoding))
            context.Response.AppendHeader("Content-Encoding", content.ContentEncoding);
          if (!string.IsNullOrEmpty(content.ContentLength))
            context.Response.AppendHeader("Content-Length", content.ContentLength);
          context.Response.ContentType = content.ContentType;
          content.Content.Position = 0;
          content.Content.WriteTo(context.Response.OutputStream);
        }
      }
      //
      HttpWebRequest request = HttpHelper.CreateScalableHttpWebRequest(url);
      // As we will stream the response, don't want to automatically decompress the content when source sends compressed content
      request.AutomaticDecompression = DecompressionMethods.None;
      if (!string.IsNullOrEmpty(contentType))
        request.ContentType = contentType;
      using (HttpWebResponse response = request.GetResponse() as HttpWebResponse)
      {
        this.DownloadData(request, response, context, cacheDuration);
      }
    }


    public IAsyncResult BeginProcessRequest(HttpContext context, AsyncCallback cb, object extraData)
    {
      string url = context.Request["url"];
      string contentType = context.Request["type"];
      int cacheDuration = Utility.TryParse<int>(context.Request["cache"], 0);
      if (cacheDuration > 0)
      {
        CachedContent content = context.Cache["ProxyStreaming:" + url] as CachedContent;
        if (content != null)
        {
          SyncResult result = new SyncResult();
          result.Context = context;
          result.Content = content;
          return result;
        }
      }
      HttpWebRequest request = HttpHelper.CreateScalableHttpWebRequest(url);
      // As we will stream the response, don't want to automatically decompress the content when source sends compressed content
      try
      {
        request.AutomaticDecompression = DecompressionMethods.None;
        if (!string.IsNullOrEmpty(contentType))
          request.ContentType = contentType;
        AsyncState state = new AsyncState();
        state.Context = context;
        state.Url = url;
        state.CacheDuration = cacheDuration;
        state.Request = request;
        return request.BeginGetResponse(cb, state);
      }
      catch { }
      return null;
    }


    public void EndProcessRequest(IAsyncResult result)
    {
      try
      {
        if (result.CompletedSynchronously)
        {
          // Content is already available in the cache and can be delivered from cache
          SyncResult syncResult = result as SyncResult;
          syncResult.Context.Response.ContentType = syncResult.Content.ContentType;
          syncResult.Context.Response.AppendHeader("Content-Encoding", syncResult.Content.ContentEncoding);
          syncResult.Context.Response.AppendHeader("Content-Length", syncResult.Content.ContentLength);
          //
          if (syncResult.Content.CacheExpiry > DateTime.Now)
            HttpHelper.CacheResponse(syncResult.Context, syncResult.Content.CacheExpiry);
          else
            HttpHelper.DoNotCacheResponse(syncResult.Context);
          //
          syncResult.Content.Content.Seek(0, SeekOrigin.Begin);
          syncResult.Content.Content.WriteTo(syncResult.Context.Response.OutputStream);
        }
        else
        {
          // Content is not available in cache and needs to be downloaded from external source
          AsyncState state = result.AsyncState as AsyncState;
          state.Context.Response.Buffer = false;
          HttpWebRequest request = state.Request;
          using (HttpWebResponse response = request.EndGetResponse(result) as HttpWebResponse)
          {
            this.DownloadData(request, response, state.Context, state.CacheDuration);
          }
        }
      }
      catch { }
    }
  }

}
