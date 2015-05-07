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
using System.Text;
using System.Web;
using System.Web.UI;


namespace Ikon.Support
{

  public static class CMS_Manager
  {
    static CMS_Manager()
    {
    }

    public static string TrimQuery(string url)
    {
      if (!string.IsNullOrEmpty(url))
        return url.Split('?', '&')[0];
      return url;
    }

  }


  //
  // modulo per sparare delle MessageBox a piacimento nella pagina
  //
  // usage: WebMsgBox.Show("messaggio");
  //
  public class WebMsgBox
  {
    protected static Hashtable handlerPages = new Hashtable();

    private WebMsgBox() { }

    public static void Show(string Message)
    {
      if (!(handlerPages.Contains(HttpContext.Current.Handler)))
      {
        Page currentPage = (Page)HttpContext.Current.Handler;
        if (currentPage != null)
        {
          Queue messageQueue = new Queue();
          messageQueue.Enqueue(Message);
          handlerPages.Add(HttpContext.Current.Handler, messageQueue);
          // non posso usare Unload perche' se uso RegisterStartupScript ho gia' renderizzato la pagina...
          //currentPage.Unload += new EventHandler(CurrentPageUnload);
          currentPage.PreRenderComplete += new EventHandler(CurrentPageUnload);
        }
      }
      else
      {
        Queue queue = ((Queue)(handlerPages[HttpContext.Current.Handler]));
        queue.Enqueue(Message);
      }
    }


    //
    // handler utilizzato automaticamente dalla classe
    //
    private static void CurrentPageUnload(object sender, EventArgs e)
    {
      Queue queue = ((Queue)(handlerPages[HttpContext.Current.Handler]));

      if (queue != null)
      {
        StringBuilder builder = new StringBuilder();
        int iMsgCount = queue.Count;
        //builder.Append("<script language='javascript'>");
        string sMsg;
        while ((iMsgCount > 0))
        {
          iMsgCount = iMsgCount - 1;
          sMsg = System.Convert.ToString(queue.Dequeue());
          sMsg = sMsg.Replace("\\", @"\\");
          sMsg = sMsg.Replace("\r", "");
          sMsg = sMsg.Replace("\n", "\\n");
          sMsg = sMsg.Replace("\"", "'");
          builder.Append("alert(\"" + sMsg + "\");");
        }
        //builder.Append("</script>");
        handlerPages.Remove(HttpContext.Current.Handler);
        //
        // non posso usare Response.Write perche' altrimenti AJAX si incazza di brutto
        //HttpContext.Current.Response.Write(builder.ToString());
        //
        Page pg = (Page)HttpContext.Current.Handler;
        // usando ScriptManager funziona anche dentro gli UpdatePanel
        ScriptManager.RegisterClientScriptBlock(pg, pg.GetType(), Guid.NewGuid().GetHashCode().ToString("x"), builder.ToString(), true);
        //pg.ClientScript.RegisterStartupScript(pg.GetType(), Guid.NewGuid().GetHashCode().ToString("x"), builder.ToString(), true);
      }
    }
  }


}
