using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Net;
using System.Web;
using System.Xml;
using System.Xml.XPath;
using System.Linq;
using System.Xml.Linq;
using System.Data;
using System.Configuration;
using System.Runtime.Serialization;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Bson;
using LinqKit;

using Ikon;
using Ikon.GD;



namespace Ikon.IKCMS.Google
{

  public class GoogleHelper
  {
    public const string ServiceUrl = "http://maps.googleapis.com/maps/api/geocode/json?sensor=false&address={0}";
    public const string ServiceUrlReverse = "http://maps.googleapis.com/maps/api/geocode/json?sensor=false&latlng={0}";


    public static string GMap_AutoSetKey()
    {
      //
      // selezione dinamica della key per adattarsi al contesto di lavoro
      //
      string host = HttpContext.Current.Request.Url.Authority;
      //
      // prova comunque su web.config come primo tentativo
      //
      if (!string.IsNullOrEmpty(IKGD_Config.AppSettings["GoogleMapsApiKey_" + host.ToLower()]))
        return IKGD_Config.AppSettings["GoogleMapsApiKey_" + host.ToLower()];
      //
      if (!string.IsNullOrEmpty(IKGD_Config.AppSettings["GoogleMapsApiKey"]))
        return IKGD_Config.AppSettings["GoogleMapsApiKey"];
      //
      return "ABQIAAAA8NqfEhRUq0xThgsLLsF8kRTwM0brOpm-All5BF6PoaKBxRWWERTsTMbe8OVrzRTDz-pVKMZJ-X07eQ";
    }




    public static GeoResponse GeoCode(string street, string city, string state, string postalCode, string country) { return GeoCode(String.Format("{0} {1}, {2} {3}, {4}", street, city, state, postalCode, country), null, null); }
    public static GeoResponse GeoCode(string address) { return GeoCode(address, null, null); }
    public static GeoResponse GeoCode(double latY, double lngX) { return GeoCode(null, latY, lngX); }
    public static GeoResponse GeoCode(string address, double? latY, double? lngX)
    {
      //
      GeoResponse result = null;
      string url = null;
      //
      if (!string.IsNullOrEmpty(address))
      {
        url = String.Format(ServiceUrl, HttpUtility.UrlEncode(address));
      }
      else if (latY != null && lngX != null)
      {
        string ll = String.Format(System.Globalization.CultureInfo.InvariantCulture.NumberFormat, "{0},{1}", latY, lngX);
        url = String.Format(ServiceUrlReverse, ll);
      }
      else
      {
        throw new ArgumentNullException("address");
      }
      //
      using (var webClient = new System.Net.WebClient())
      {
        string jsonString = webClient.DownloadString(url);
        result = JsonConvert.DeserializeObject<GeoResponse>(jsonString);
      }
      //
      return result;
    }




  }






  [DataContract]
  public class GeoResponse
  {
    [DataMember(Name = "status")]
    public string Status { get; set; }
    [DataMember(Name = "results")]
    public List<GResult> Results { get; set; }

    public GResult Result { get { return Results.FirstOrDefault(); } }

    public bool valid { get { return string.Equals(Status, "OK", StringComparison.OrdinalIgnoreCase); } }


    [DataContract]
    public class GResult
    {
      [DataMember(Name = "formatted_address")]
      public string AddressFormatted { get; set; }
      [DataMember(Name = "types")]
      public List<string> AddressComponentTypes { get; set; }
      [DataMember(Name = "address_components")]
      public List<GAddressComponent> AddressComponents { get; set; }
      [DataMember(Name = "geometry")]
      public GGeometry Geometry { get; set; }

      public string AddressType { get { return Utility.Implode(AddressComponentTypes, ",", null, true, true); } }


      [DataContract]
      public class GAddressComponent
      {
        [DataMember(Name = "long_name")]
        public string NameLong { get; set; }
        [DataMember(Name = "short_name")]
        public string NameShort { get; set; }
        [DataMember(Name = "types")]
        public List<string> AddressComponentTypes { get; set; }
      }


      [DataContract]
      public class GGeometry
      {
        [DataMember(Name = "location")]
        public GLocation Location { get; set; }
        [DataMember(Name = "location_type")]
        public string LocationType { get; set; }
        [DataMember(Name = "viewport")]
        public GBBox Viewport { get; set; }
        [DataMember(Name = "bounds")]
        public GBBox Bounds { get; set; }


        [DataContract]
        public class GBBox
        {
          [DataMember(Name = "southwest")]
          public GLocation SW { get; set; }
          [DataMember(Name = "northeast")]
          public GLocation NE { get; set; }
          //
          public double LatMin { get { return SW.Lat; } }
          public double LatMax { get { return NE.Lat; } }
          public double LngMin { get { return SW.Lng; } }
          public double LngMax { get { return NE.Lng; } }
          //
        }

        [DataContract]
        public class GLocation
        {
          [DataMember(Name = "lat")]
          public double Lat { get; set; }

          [DataMember(Name = "lng")]
          public double Lng { get; set; }

          public GLocation()
          { }

          public GLocation(double Lat, double Lng)
            : this()
          {
            this.Lat = Lat;
            this.Lng = Lng;
          }

        }
      }
    }
  }


  // per compatibilita' con Subgurim.Controles.GLatLng
  public class GLatLng
  {
    public double lat { get; set; }

    public double lng { get; set; }

    public GLatLng()
    { }

    public GLatLng(double lat, double lng)
      : this()
    {
      this.lat = lat;
      this.lng = lng;
    }


    private static double Radians(double x) { return x * Math.PI / 180.0; }
    public static double Distance(GLatLng a, GLatLng b) { return Distance(a.lng, a.lat, b.lng, b.lat); }
    public static double Distance(double lon1, double lat1, double lon2, double lat2)
    {
      double RADIUS = 6378160.0;
      double dlon = Radians(lon2 - lon1);
      double dlat = Radians(lat2 - lat1);
      double a = (Math.Sin(dlat / 2) * Math.Sin(dlat / 2)) + Math.Cos(Radians(lat1)) * Math.Cos(Radians(lat2)) * (Math.Sin(dlon / 2) * Math.Sin(dlon / 2));
      double angle = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
      return angle * RADIUS;
    }

    public double distanceFrom(GLatLng b)
    {
      return Distance(this, b);
    }

  }

}
