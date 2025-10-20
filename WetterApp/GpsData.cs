namespace WetterApp
{
    public class GpsData
    {
        public double Latitude { get; }

        public double Longitude { get; }

        public GpsData(double latitude, double longitude)
        {
            Latitude = latitude;
            Longitude = longitude;
        }

    }
}
