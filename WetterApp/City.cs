using System.Net;

namespace WetterApp
{
    public class City
    {
        public string Name { get; }

        public GpsData Gps { get; }

        public City(string name, double latitude, double longitude)
        {
            this.Name = name;
            this.Gps = new GpsData(latitude, longitude);
        }
    }
}
