using MySqlConnector;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Net.Http;
using System.Security.Permissions;
using System.Windows;

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

        public WeatherResponse LastRun { get; private set; }

        public MainWindow()
        {
            InitializeComponent();
            this.DataGridTemperatures.ItemsSource = temparaturesAndWindSpeed;
            this.MessungGrid.ItemsSource = Messungen;
            this.WetterwertGrid.ItemsSource = Wetterwerte;
        }

        private async void Button_Click(object sender, RoutedEventArgs e)
        {
            if (!double.TryParse(this.TextBlock_Longitude.Text, out var longitude) ||
                !double.TryParse(this.TextBlock_Laditude.Text, out var latitude))
            {
                MessageBox.Show("Longitude or latitude is not a number.");
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

                    this.temparaturesAndWindSpeed.Clear();

                    for (int i = 0; i < this.LastRun.Hourly.Temperature_2m.Count; i++)
                    {
                        temparaturesAndWindSpeed.Add(new WeatherItem
                        {
                            Time = this.LastRun.Hourly.Time[i],
                            Temperatur = this.LastRun.Hourly.Temperature_2m[i].ToString(),
                            WindSpeed = this.LastRun.Hourly.Wind_speed_10m[i].ToString(),
                        });
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"An error occurred while recieving the weather data: {ex.Message}");
                }
            }
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
                MessageBox.Show("Keine Wetterdaten vorhanden.");
                return;
            }

            DataBaseHelper.SaveWeatherData(this.LastRun);

            MessageBox.Show("Wetterdaten erfolgreich gespeichert.");
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            DataBaseHelper.CreateDatabaseIfNotExists();
        }

        private void MessungGrid_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            if (this.MessungGrid.SelectedItem is Messung selected)
            {
                this.Wetterwerte.Clear();

                foreach (Wetterwert wert in DataBaseHelper.GetWetterwerteByMessungId(selected.MessungID))
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
                foreach (Messung messung in DataBaseHelper.GetAllMessungen())
                {
                    this.Messungen.Add(messung);
                }
            }
        }

        private void ButtonDelete_Click(object sender, RoutedEventArgs e)
        {
            if (this.MessungGrid.SelectedItem is Messung selected)
            {
                DataBaseHelper.DeleteMessung(selected.MessungID);

                this.Messungen.Clear();
                this.Wetterwerte.Clear();

                foreach (Messung messung in DataBaseHelper.GetAllMessungen())
                {
                    this.Messungen.Add(messung);
                }
            }
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
