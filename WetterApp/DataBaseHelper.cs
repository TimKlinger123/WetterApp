using MySqlConnector;
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Windows;

namespace WetterApp
{
    internal static class DataBaseHelper
    {
        private static readonly string server = "localhost";
        private static readonly string user = "Tim";
        private static readonly string password = "rootPassword";
        private static readonly string connectionString = 
            $"Server={server};Port=3306;User ID={user};Password={password};";

        internal static void CreateDatabaseIfNotExists()
        {
            try
            {
                using (MySqlConnection connection = new MySqlConnection(connectionString))
                {
                    connection.Open();

                    var createDbCmd = new MySqlCommand("CREATE DATABASE IF NOT EXISTS Wetterdaten;", connection);
                    createDbCmd.ExecuteNonQuery();

                    connection.ChangeDatabase("Wetterdaten");

                    //new MySqlCommand("DROP TABLE IF EXISTS Wetterwert;", connection).ExecuteNonQuery();
                    //new MySqlCommand("DROP TABLE IF EXISTS Messung;", connection).ExecuteNonQuery();

                    string createMessungTable = @"
                        CREATE TABLE IF NOT EXISTS Messung (
                        MessungID INT AUTO_INCREMENT PRIMARY KEY,
                        Datum DATE NOT NULL,
                        Longitude DOUBLE NOT NULL,
                        Latitude DOUBLE NOT NULL);";

                    string createWetterwertTable = @"
                        CREATE TABLE IF NOT EXISTS Wetterwert (
                        WetterwertID INT AUTO_INCREMENT PRIMARY KEY,
                        MessungID INT NOT NULL,
                        Uhrzeit DATETIME NOT NULL,
                        Temperatur DOUBLE NOT NULL,
                        Windstaerke DOUBLE NOT NULL,
                        FOREIGN KEY (MessungID) REFERENCES Messung(MessungID));";

                    new MySqlCommand(createMessungTable, connection).ExecuteNonQuery();
                    new MySqlCommand(createWetterwertTable, connection).ExecuteNonQuery();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Fehler beim Erstellen der Datenbank: {ex.Message}");
            }
        }

        internal static void SaveWeatherData(WeatherResponse response)
        {
            try
            {
                using (MySqlConnection connection = new MySqlConnection(connectionString))
                {
                    connection.Open();
                    connection.ChangeDatabase("Wetterdaten");

                    string insertMessung = @"
                         INSERT INTO Messung (Datum, Longitude, Latitude)
                         VALUES (@Datum, @Longitude, @Latitude);
                         SELECT LAST_INSERT_ID();";

                    using (MySqlCommand messungCmd = new MySqlCommand(insertMessung, connection))
                    {
                        messungCmd.Parameters.AddWithValue("@Datum", DateTime.Now);
                        messungCmd.Parameters.AddWithValue("@Longitude", response.Hourly.longitude);
                        messungCmd.Parameters.AddWithValue("@Latitude", response.Hourly.latitude);

                        int messungId = Convert.ToInt32(messungCmd.ExecuteScalar());

                        string insertWetterwert = @"
                            INSERT INTO Wetterwert (MessungID, Uhrzeit, Temperatur, Windstaerke)
                            VALUES (@MessungID, @Uhrzeit, @Temperatur, @Windstaerke);";
                        using (MySqlCommand wetterCmd = new MySqlCommand(insertWetterwert, connection))
                        {
                            for (int i = 0; i < response.Hourly.Time.Count; i++)
                            {
                                wetterCmd.Parameters.Clear();
                                wetterCmd.Parameters.AddWithValue("@MessungID", messungId);
                                wetterCmd.Parameters.AddWithValue("@Uhrzeit", DateTime.Parse(response.Hourly.Time[i]));
                                wetterCmd.Parameters.AddWithValue("@Temperatur", response.Hourly.Temperature_2m[i]);
                                wetterCmd.Parameters.AddWithValue("@Windstaerke", response.Hourly.Wind_speed_10m[i]);

                                wetterCmd.ExecuteNonQuery();
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Fehler beim Speichern der Wetterdaten: {ex.Message}");
            }
        }

        internal static List<Messung> GetAllMessungen()
        {
            var messungen = new List<Messung>();

            try
            {
                using (MySqlConnection connection = new MySqlConnection(connectionString))
                {
                    connection.Open();
                    connection.ChangeDatabase("Wetterdaten");

                    string query = "SELECT MessungID, Datum, Longitude, Latitude FROM Messung ORDER BY Datum DESC;";
                    using (MySqlCommand command = new MySqlCommand(query, connection))
                    {
                        using (MySqlDataReader reader = command.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                messungen.Add(new Messung
                                {
                                    MessungID = reader.GetInt32("MessungID"),
                                    Datum = reader.GetDateTime("Datum"),
                                    Longitude = reader.GetDouble("Longitude"),
                                    Latitude = reader.GetDouble("Latitude")
                                });
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Fehler beim Abrufen der Messungen: {ex.Message}");
            }

            return messungen;
        }

        internal static List<Wetterwert> GetWetterwerteByMessungId(int messungId)
        {
            List<Wetterwert> werte = new List<Wetterwert>();

            try
            {
                using (MySqlConnection connection = new MySqlConnection(connectionString))
                {
                    connection.Open();
                    connection.ChangeDatabase("Wetterdaten");

                    string query = @"
                        SELECT Uhrzeit, Temperatur, Windstaerke
                        FROM Wetterwert
                        WHERE MessungID = @MessungID
                        ORDER BY Uhrzeit ASC;";

                    using (MySqlCommand command = new MySqlCommand(query, connection))
                    {
                        command.Parameters.AddWithValue("@MessungID", messungId);

                        using (MySqlDataReader reader = command.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                werte.Add(new Wetterwert
                                {
                                    Uhrzeit = reader.GetDateTime("Uhrzeit"),
                                    Temperatur = reader.GetDouble("Temperatur"),
                                    Windstaerke = reader.GetDouble("Windstaerke")
                                });
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Fehler beim Abrufen der Wetterwerte: {ex.Message}");
            }

            return werte;
        }

        internal static void DeleteMessung(int selectedId)
        {
            try
            {
                using (MySqlConnection connection = new MySqlConnection(connectionString))
                {
                    connection.Open();
                    connection.ChangeDatabase("Wetterdaten");

                    using (MySqlTransaction transaction = connection.BeginTransaction())
                    {
                        try
                        {
                            string deleteWetterwerte = @"DELETE FROM Wetterwert WHERE MessungID = @MessungID;";
                            using (MySqlCommand cmdWetter = new MySqlCommand(deleteWetterwerte, connection, transaction))
                            {
                                cmdWetter.Parameters.AddWithValue("@MessungID", selectedId);
                                cmdWetter.ExecuteNonQuery();
                            }

                            string deleteMessung = @"DELETE FROM Messung WHERE MessungID = @MessungID;";
                            using (MySqlCommand cmdMessung = new MySqlCommand(deleteMessung, connection, transaction))
                            {
                                cmdMessung.Parameters.AddWithValue("@MessungID", selectedId);
                                cmdMessung.ExecuteNonQuery();
                            }

                            transaction.Commit();
                            MessageBox.Show("Messung und zugehörige Wetterwerte wurden erfolgreich gelöscht.");
                        }
                        catch (Exception innerEx)
                        {
                            transaction.Rollback();
                            MessageBox.Show($"Fehler beim Löschen: {innerEx.Message}");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Verbindungsfehler: {ex.Message}");
            }

        }
    }
}
