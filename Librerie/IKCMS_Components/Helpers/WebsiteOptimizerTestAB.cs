using System;
using System.Collections.Generic;
using System.Text;
using System.Linq;
using System.Web;
using System.Xml.Linq;
using System.Web.Mvc;
using System.Web.Mvc.Html;
using System.Reflection;

using Ikon;
using Ikon.GD;
using Ikon.IKCMS;
using Ikon.IKCMS.Library.Resources;


namespace Ikon.IKCMS
{
  public static class WebsiteOptimizerTestAB_Extensions
  {

    public static string WebsiteOptimizerTestAB_TOP(this HtmlHelper helper)
    {
      try
      {
        IKCMS_ModelCMS_VFS_LanguageKVT_Interface mdl = helper.ViewData.Model as IKCMS_ModelCMS_VFS_LanguageKVT_Interface;
        if (mdl != null && !string.IsNullOrEmpty(mdl.VFS_ResourceLanguageKVT("SEO_Optimizer_TestAB_Code").ValueString) && !string.IsNullOrEmpty(mdl.VFS_ResourceLanguageKVT("SEO_Optimizer_TestAB_UA").ValueString) && mdl.VFS_ResourceLanguageKVT("SEO_Optimizer_TestAB_Orig").ValueT<bool>() == true)
        {
          string frag = "********";
          string codeJSbase = @"PCEtLSBHb29nbGUgV2Vic2l0ZSBPcHRpbWl6ZXIgQ29udHJvbCBTY3JpcHQgLS0+DQo8c2NyaXB0
Pg0KZnVuY3Rpb24gdXRteF9zZWN0aW9uKCl7fWZ1bmN0aW9uIHV0bXgoKXt9DQooZnVuY3Rpb24o
KXt2YXIgaz0nKioqKioqKionLGQ9ZG9jdW1lbnQsbD1kLmxvY2F0aW9uLGM9ZC5jb29raWU7ZnVu
Y3Rpb24gZihuKXsNCmlmKGMpe3ZhciBpPWMuaW5kZXhPZihuKyc9Jyk7aWYoaT4tMSl7dmFyIGo9
Yy5pbmRleE9mKCc7JyxpKTtyZXR1cm4gYy5zdWJzdHJpbmcoaStuLg0KbGVuZ3RoKzEsajwwP2Mu
bGVuZ3RoOmopfX19dmFyIHg9ZignX191dG14JykseHg9ZignX191dG14eCcpLGg9bC5oYXNoOw0K
ZC53cml0ZSgnPHNjJysncmlwdCBzcmM9IicrDQonaHR0cCcrKGwucHJvdG9jb2w9PSdodHRwczon
PydzOi8vc3NsJzonOi8vd3d3JykrJy5nb29nbGUtYW5hbHl0aWNzLmNvbScNCisnL3NpdGVvcHQu
anM/dj0xJnV0bXhrZXk9JytrKycmdXRteD0nKyh4P3g6JycpKycmdXRteHg9JysoeHg/eHg6Jycp
KycmdXRteHRpbWU9Jw0KK25ldyBEYXRlKCkudmFsdWVPZigpKyhoPycmdXRteGhhc2g9Jytlc2Nh
cGUoaC5zdWJzdHIoMSkpOicnKSsNCiciIHR5cGU9InRleHQvamF2YXNjcmlwdCIgY2hhcnNldD0i
dXRmLTgiPjwvc2MnKydyaXB0PicpfSkoKTsNCjwvc2NyaXB0PjxzY3JpcHQ+dXRteCgidXJsIiwn
QS9CJyk7PC9zY3JpcHQ+DQo8IS0tIEVuZCBvZiBHb29nbGUgV2Vic2l0ZSBPcHRpbWl6ZXIgQ29u
dHJvbCBTY3JpcHQgLS0+";
          string codeJS = Utility.StringBase64ToString(codeJSbase).Replace(frag, mdl.VFS_ResourceLanguageKVT("SEO_Optimizer_TestAB_Code").ValueString.Trim());
          return codeJS;
        }
      }
      catch { }
      return string.Empty;
    }


    public static string WebsiteOptimizerTestAB_BOTTOM(this HtmlHelper helper)
    {
      try
      {
        IKCMS_ModelCMS_VFS_LanguageKVT_Interface mdl = helper.ViewData.Model as IKCMS_ModelCMS_VFS_LanguageKVT_Interface;
        if (mdl == null || string.IsNullOrEmpty(mdl.VFS_ResourceLanguageKVT("SEO_Optimizer_TestAB_Code").ValueString) || string.IsNullOrEmpty(mdl.VFS_ResourceLanguageKVT("SEO_Optimizer_TestAB_UA").ValueString))
          return string.Empty;
        string testCodeIn = "********";
        string userCodeIn = "XYZ";
        string testCodeOut = mdl.VFS_ResourceLanguageKVT("SEO_Optimizer_TestAB_Code").ValueString.Trim();
        string userCodeOut = mdl.VFS_ResourceLanguageKVT("SEO_Optimizer_TestAB_UA").ValueString.Trim();

        if (mdl.VFS_ResourceLanguageKVT("SEO_Optimizer_TestAB_Orig").ValueT<bool>() == true)
        {
          //Original Page
          string codeJSbase = @"PCEtLSBHb29nbGUgV2Vic2l0ZSBPcHRpbWl6ZXIgVHJhY2tpbmcgU2NyaXB0IC0tPg0KPHNjcmlw
dCB0eXBlPSJ0ZXh0L2phdmFzY3JpcHQiPg0KaWYodHlwZW9mKF9nYXQpIT0nb2JqZWN0Jylkb2N1
bWVudC53cml0ZSgnPHNjJysncmlwdCBzcmM9Imh0dHAnKw0KKGRvY3VtZW50LmxvY2F0aW9uLnBy
b3RvY29sPT0naHR0cHM6Jz8nczovL3NzbCc6JzovL3d3dycpKw0KJy5nb29nbGUtYW5hbHl0aWNz
LmNvbS9nYS5qcyI+PC9zYycrJ3JpcHQ+Jyk8L3NjcmlwdD4NCjxzY3JpcHQgdHlwZT0idGV4dC9q
YXZhc2NyaXB0Ij4NCnRyeSB7DQp2YXIgZ3dvVHJhY2tlcj1fZ2F0Ll9nZXRUcmFja2VyKCJYWFhY
WFhYWCIpOw0KZ3dvVHJhY2tlci5fdHJhY2tQYWdldmlldygiLyoqKioqKioqL3Rlc3QiKTsNCn1j
YXRjaChlcnIpe308L3NjcmlwdD4NCjwhLS0gRW5kIG9mIEdvb2dsZSBXZWJzaXRlIE9wdGltaXpl
ciBUcmFja2luZyBTY3JpcHQgLS0+";
          string codeJS = Utility.StringBase64ToString(codeJSbase).Replace(userCodeIn, userCodeOut).Replace(testCodeIn, testCodeOut);
          return codeJS;
        }
        else if (mdl.VFS_ResourceLanguageKVT("SEO_Optimizer_TestAB_Target").ValueT<bool>() == true)
        {
          //Goal/Target
          string codeJSbase = @"PCEtLSBHb29nbGUgV2Vic2l0ZSBPcHRpbWl6ZXIgQ29udmVyc2lvbiBTY3JpcHQgLS0+DQo8c2Ny
aXB0IHR5cGU9InRleHQvamF2YXNjcmlwdCI+DQppZih0eXBlb2YoX2dhdCkhPSdvYmplY3QnKWRv
Y3VtZW50LndyaXRlKCc8c2MnKydyaXB0IHNyYz0iaHR0cCcrDQooZG9jdW1lbnQubG9jYXRpb24u
cHJvdG9jb2w9PSdodHRwczonPydzOi8vc3NsJzonOi8vd3d3JykrDQonLmdvb2dsZS1hbmFseXRp
Y3MuY29tL2dhLmpzIj48L3NjJysncmlwdD4nKTwvc2NyaXB0Pg0KPHNjcmlwdCB0eXBlPSJ0ZXh0
L2phdmFzY3JpcHQiPg0KdHJ5IHsNCnZhciBnd29UcmFja2VyPV9nYXQuX2dldFRyYWNrZXIoIlhY
WFhYWFhYIik7DQpnd29UcmFja2VyLl90cmFja1BhZ2V2aWV3KCIvKioqKioqKiovZ29hbCIpOw0K
fWNhdGNoKGVycil7fTwvc2NyaXB0Pg0KPCEtLSBFbmQgb2YgR29vZ2xlIFdlYnNpdGUgT3B0aW1p
emVyIENvbnZlcnNpb24gU2NyaXB0IC0tPg==";
          string codeJS = Utility.StringBase64ToString(codeJSbase).Replace(userCodeIn, userCodeOut).Replace(testCodeIn, testCodeOut);
          return codeJS;
        }
        else
        {
          //Variants
          string codeJSbase = @"PCEtLSBHb29nbGUgV2Vic2l0ZSBPcHRpbWl6ZXIgVHJhY2tpbmcgU2NyaXB0IC0tPg0KPHNjcmlw
dCB0eXBlPSJ0ZXh0L2phdmFzY3JpcHQiPg0KaWYodHlwZW9mKF9nYXQpIT0nb2JqZWN0Jylkb2N1
bWVudC53cml0ZSgnPHNjJysncmlwdCBzcmM9Imh0dHAnKw0KKGRvY3VtZW50LmxvY2F0aW9uLnBy
b3RvY29sPT0naHR0cHM6Jz8nczovL3NzbCc6JzovL3d3dycpKw0KJy5nb29nbGUtYW5hbHl0aWNz
LmNvbS9nYS5qcyI+PC9zYycrJ3JpcHQ+Jyk8L3NjcmlwdD4NCjxzY3JpcHQgdHlwZT0idGV4dC9q
YXZhc2NyaXB0Ij4NCnRyeSB7DQp2YXIgZ3dvVHJhY2tlcj1fZ2F0Ll9nZXRUcmFja2VyKCJYWFhY
WFhYWCIpOw0KZ3dvVHJhY2tlci5fdHJhY2tQYWdldmlldygiLyoqKioqKioqL3Rlc3QiKTsNCn1j
YXRjaChlcnIpe308L3NjcmlwdD4NCjwhLS0gRW5kIG9mIEdvb2dsZSBXZWJzaXRlIE9wdGltaXpl
ciBUcmFja2luZyBTY3JpcHQgLS0+";
          string codeJS = Utility.StringBase64ToString(codeJSbase).Replace(userCodeIn, userCodeOut).Replace(testCodeIn, testCodeOut);
          return codeJS;
        }
      }
      catch { }
      return string.Empty;
    }




  }
}
