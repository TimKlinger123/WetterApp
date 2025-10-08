using MySqlConnector;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Net.Http;
using System.Security.Permissions;
using System.Windows;
using System.Windows.Controls;

namespace WetterApp
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private bool selectionChanged = false;

        public ObservableCollection<WeatherItem> temparaturesAndWindSpeed = 
            new ObservableCollection<WeatherItem>();

        public ObservableCollection<Messung> Messungen { get; set; } = 
            new ObservableCollection<Messung>();

        public ObservableCollection<Wetterwert> Wetterwerte { get; set; } = 
            new ObservableCollection<Wetterwert>();

        public ObservableCollection<City> Cities { get; } = 
            new ObservableCollection<City>();

        public WeatherResponse LastRun { get; private set; }

        public MainWindow()
        {
            InitializeComponent();
            this.DataGridTemperatures.ItemsSource = temparaturesAndWindSpeed;
            this.MessungGrid.ItemsSource = Messungen;
            this.WetterwertGrid.ItemsSource = Wetterwerte;
            this.CityComboBox.ItemsSource = Cities;
        }

        private async void Button_Click(object sender, RoutedEventArgs e)
        {
            if (!double.TryParse(this.TextBlock_Longitude.Text, out var longitude) ||
                !double.TryParse(this.TextBlock_Laditude.Text, out var latitude))
            {
                MessageBox.Show(this, "Longitude or latitude is not a number.");
                return;
            }

            string baseUrl = "https://api.open-meteo.com/v1/forecast";
            string url = $"{baseUrl}?latitude={latitude.ToString().Replace(',', '.')}&longitude={longitude.ToString().Replace(',', '.')}&hourly=temperature_2m,wind_speed_10m";

            using (HttpClient client = new HttpClient())
            {
                try
                {
                    HttpResponseMessage response = await client.GetAsync(url);
                    response.EnsureSuccessStatusCode();

                    string responseBody = await response.Content.ReadAsStringAsync();
                    WeatherResponse weatherList = JsonConvert.DeserializeObject<WeatherResponse>(responseBody);
                    this.LastRun = weatherList;
                    this.LastRun.Hourly.latitude = latitude;
                    this.LastRun.Hourly.longitude = longitude;

                    ShowWetherData();
                }
                catch (Exception ex)
                {
                    MessageBox.Show(this, $"An error occurred while recieving the weather data: {ex.Message}");
                }
            }
        }

        private void ShowWetherData()
        {
            this.temparaturesAndWindSpeed.Clear();
            double totalTemp = 0;
            double minTemp = 5000000;
            double maxTemp = 0;
            double averageTemp = 0;
            double maxWindSpeed = 0;

            for (int i = 0; i < this.LastRun.Hourly.Temperature_2m.Count; i++)
            {
                double temp = this.LastRun.Hourly.Temperature_2m[i];
                double windSpeed = this.LastRun.Hourly.Wind_speed_10m[i];

                temparaturesAndWindSpeed.Add(new WeatherItem
                {
                    Time = this.LastRun.Hourly.Time[i],
                    Temperatur = temp.ToString(),
                    WindSpeed = windSpeed.ToString(),
                });

                totalTemp += this.LastRun.Hourly.Temperature_2m[i];

                if (temp > maxTemp)
                    maxTemp = temp;
                if (temp < minTemp)
                    minTemp = temp;
                if (windSpeed > maxWindSpeed)
                    maxWindSpeed = windSpeed;
            }

            averageTemp = totalTemp / this.LastRun.Hourly.Temperature_2m.Count;
            this.TextBlock_MaxTemp.Text = maxTemp.ToString("F1") + "°C";
            this.TextBlock_MinTemp.Text = minTemp.ToString("F1") + "°C";
            this.TextBlock_AverageTemp.Text = averageTemp.ToString("F1") + "°C";
            this.TextBlock_MaxWindSpeed.Text = maxWindSpeed.ToString("F1") + "km/h";
        }

        private void TextBlock_Longitude_PreviewTextInput(object sender, System.Windows.Input.TextCompositionEventArgs e)
        {
            if (e.Text != "," && !char.IsDigit(e.Text[0]))
            {
                e.Handled = true;
            }
        }

        private void ButtonSaveData_Click(object sender, RoutedEventArgs e)
        {
            if (this.LastRun == null || this.temparaturesAndWindSpeed.Count == 0)
            {
                MessageBox.Show(this, "Keine Wetterdaten vorhanden.");
                return;
            }

            SQLiteDataBaseHelper.InsertWetherData(this.LastRun);

            MessageBox.Show(this, "Wetterdaten erfolgreich gespeichert.");
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            SQLiteDataBaseHelper.CreateDatabaseIfNotExists();
            
            foreach (City city in SQLiteDataBaseHelper.GetAllCities())
            {
                this.Cities.Add(city);
            }
        }

        private void MessungGrid_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            if (this.MessungGrid.SelectedItem is Messung selected)
            {
                this.Wetterwerte.Clear();

                foreach (Wetterwert wert in SQLiteDataBaseHelper.GetWeatherData(selected.MessungID))
                {
                    this.Wetterwerte.Add(wert);
                }

                selectionChanged = true;
            }
        }

        private void TabControl_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            if (selectionChanged)
            {
                selectionChanged = false;
                return;
            }

            this.Messungen.Clear();

            if (this.TabItemPreviousRuns.IsSelected)
            {
                foreach (Messung messung in SQLiteDataBaseHelper.GetAllMessurements())
                {
                    this.Messungen.Add(messung);
                }
            }
        }

        private void ButtonDelete_Click(object sender, RoutedEventArgs e)
        {
            if (this.MessungGrid.SelectedItem is Messung selected)
            {
                SQLiteDataBaseHelper.DeleteMessurement(selected.MessungID);

                selectionChanged = true;
                this.Wetterwerte.Clear();
                this.Messungen.Clear();

                foreach (Messung messung in SQLiteDataBaseHelper.GetAllMessurements())
                {
                    this.Messungen.Add(messung);
                }
            }
        }

        private void ButtonAddCity_Click(object sender, RoutedEventArgs e)
        {
            if (TextBlock_CityName.Text == null ||
                TextBlock_CityName.Text == string.Empty)
            {
                MessageBox.Show(this, "Es muss ein Cityname eingetragen werden um Breiten- " +
                    "und Längengrad speichern zu können.");
                return;
            }

            if (!double.TryParse(this.TextBlock_Longitude.Text, out var longitude) ||
                !double.TryParse(this.TextBlock_Laditude.Text, out var latitude))
            {
                MessageBox.Show(this, "Longitude or latitude is not a number.");
                return;
            }

            City city = new City(TextBlock_CityName.Text, latitude, longitude);
            
            if (this.Cities.Any(c => c.Name  == city.Name))
            {
                MessageBox.Show(this, "Diese Stadt existiert bereits in der Liste.");
                return;
            }

            SQLiteDataBaseHelper.InsertCity(city);
            this.Cities.Add(city);
            TextBlock_CityName.Text = string.Empty;
        }

        private void MenuItemCity_Löschen_Click(object sender, RoutedEventArgs e)
        {
            City selectedCity = (City)((MenuItem)sender).DataContext;
            SQLiteDataBaseHelper.DeleteCity(selectedCity);
            this.Cities.Remove(selectedCity);
        }

        private void CityComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            City selectedCity = (City)((ComboBox)sender).SelectedItem;
            TextBlock_CityName.Text = selectedCity.Name;
            TextBlock_Laditude.Text = selectedCity.Gps.Latitude.ToString();
            TextBlock_Longitude.Text = selectedCity.Gps.Longitude.ToString();
        }

        private void CityItem_MouseDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            City selectedCity = (City)((TextBlock)sender).DataContext;
            TextBlock_CityName.Text = selectedCity.Name;
            TextBlock_Laditude.Text = selectedCity.Gps.Latitude.ToString();
            TextBlock_Longitude.Text = selectedCity.Gps.Longitude.ToString();
        }
    }

    public class WeatherResponse
    {
        [JsonProperty("hourly")]
        public Hourly Hourly { get; set; }
    }

    public class Hourly
    {
        public double longitude;
        public double latitude;

        public List<string> Time { get; set; }

        public List<double> Temperature_2m { get; set; }

        public List<double> Wind_speed_10m { get; set; }
    }

    public class WeatherItem
    {
        public string Time { get; set; }

        public string Temperatur { get; set; }

        public string WindSpeed { get; set; } 
    }

}
