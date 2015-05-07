/*
 * 
 * Intranet Ikon
 * 
 * Copyright (C) 2008 Ikon Srl
 * Tutti i Diritti Riservati. All Rights Reserved
 * 
 */


using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Net;
using System.Drawing;


namespace IKGD_Utility.Fonts
{

  public static class Tools
  {

    //
    // Function to calculate (as best we can), as text string that may have line breaks.
    // This is to auto-size controls to accomodate text without using scrollbars.
    // extraPercentageFraction --> ratio to extend the final height (fractional not %)
    // fontExtraSize --> go up 1 point on the font size to handle word wrapping differences
    //
    public static int GetTextHeight(string text, Font inFont, int ActualWidth) { return GetTextHeight(text, inFont, ActualWidth, null, null); }
    public static int GetTextHeight(string text, Font inFont, int ActualWidth, double? extraPercentageFraction, double? fontExtraSize)
    {
      Graphics g = null;
      Bitmap myBitmap = null;
      try
      {
        extraPercentageFraction = extraPercentageFraction ?? 0.01;  // 1%
        fontExtraSize = fontExtraSize ?? 1.0;
        //
        myBitmap = new Bitmap(10, 10);
        g = Graphics.FromImage(myBitmap);
        // go up 1 point on the font size to handle word wrapping differences
        Font Tempfont = new Font(inFont.FontFamily, inFont.Size + (float)fontExtraSize.Value, inFont.Style);
        SizeF structSize = g.MeasureString(text + "\r\n", Tempfont, ActualWidth);
        return (int)(structSize.Height * (1.0 + extraPercentageFraction.Value));
      }
      finally
      {
        g.Dispose();
        g = null;
        myBitmap.Dispose();
        myBitmap = null;
      }
    }



  }



}
