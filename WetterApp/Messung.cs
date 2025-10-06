using System;

namespace WetterApp
{
    public class Messung
    {
        public int MessungID { get; set; }

        public DateTime Datum { get; set; }

        public double Longitude { get; set; }

        public double Latitude { get; set; }
    }
}
