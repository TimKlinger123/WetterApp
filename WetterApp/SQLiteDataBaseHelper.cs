using System;
using System.Collections.Generic;
using System.Data.SQLite;
using System.Windows;

namespace WetterApp
{
    internal static class SQLiteDataBaseHelper
    {
        private static readonly string dbFilePath = "Wetterdaten.db";
        private static readonly string connectionString = $"Data Source={dbFilePath};";

        internal static void CreateDatabaseIfNotExists()
        {
            try
            {
                using (var connection = new SQLiteConnection(connectionString))
                {
                    connection.Open();

                    string createMessungTable = @"
                        CREATE TABLE IF NOT EXISTS Messung (
                        MessungID INTEGER PRIMARY KEY AUTOINCREMENT,
                        Datum DATE NOT NULL,
                        Longitude REAL NOT NULL,
                        Latitude REAL NOT NULL);";

                    string createWetterwertTable = @"
                        CREATE TABLE IF NOT EXISTS Wetterwert (
                        WetterwertID INTEGER PRIMARY KEY AUTOINCREMENT,
                        MessungID INTEGER NOT NULL,
                        Uhrzeit DATETIME NOT NULL,
                        Temperatur REAL NOT NULL,
                        Windstaerke REAL NOT NULL,
                        FOREIGN KEY (MessungID) REFERENCES Messung(MessungID));";

                    string createCityTable = @"
                        CREATE TABLE IF NOT EXISTS City (
                        CityID INTEGER PRIMARY KEY AUTOINCREMENT,
                        Name TEXT NOT NULL,
                        Latitude REAL NOT NULL,
                        Longitude REAL NOT NULL);";


                    new SQLiteCommand(createMessungTable, connection).ExecuteNonQuery();
                    new SQLiteCommand(createWetterwertTable, connection).ExecuteNonQuery();
                    new SQLiteCommand(createCityTable, connection).ExecuteNonQuery();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Fehler beim Erstellen der Datenbank: {ex.Message}");
            }
        }

        internal static void InsertCity(City city)
        {
            try
            {
                using (var connection = new SQLiteConnection(connectionString))
                {
                    connection.Open();

                    string insertQuery = @"
                        INSERT INTO City (Name, Latitude, Longitude)
                        VALUES (@Name, @Latitude, @Longitude);";

                    using (var command = new SQLiteCommand(insertQuery, connection))
                    {
                        command.Parameters.AddWithValue("@Name", city.Name);
                        command.Parameters.AddWithValue("@Latitude", city.Gps.Latitude);
                        command.Parameters.AddWithValue("@Longitude", city.Gps.Longitude);

                        command.ExecuteNonQuery();
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Fehler beim Einfügen der Stadt: {ex.Message}");
            }
        }

        internal static void DeleteCity(City city)
        {
            try
            {
                using (var connection = new SQLiteConnection(connectionString))
                {
                    connection.Open();

                    string deleteQuery = @"
                        DELETE FROM City
                        WHERE Name = @Name AND Latitude = @Latitude AND Longitude = @Longitude;";

                    using (var command = new SQLiteCommand(deleteQuery, connection))
                    {
                        command.Parameters.AddWithValue("@Name", city.Name);
                        command.Parameters.AddWithValue("@Latitude", city.Gps.Latitude);
                        command.Parameters.AddWithValue("@Longitude", city.Gps.Longitude);

                        command.ExecuteNonQuery();
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Fehler beim Löschen der Stadt: {ex.Message}");
            }
        }


        internal static List<City> GetAllCities()
        {
            var cities = new List<City>();

            try
            {
                using (var connection = new SQLiteConnection(connectionString))
                {
                    connection.Open();

                    string selectQuery = "SELECT Name, Latitude, Longitude FROM City;";

                    using (var command = new SQLiteCommand(selectQuery, connection))
                    using (var reader = command.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            string name = reader.GetString(0);
                            double latitude = reader.GetDouble(1);
                            double longitude = reader.GetDouble(2);

                            cities.Add(new City(name, latitude, longitude));
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Fehler beim Laden der Städte: {ex.Message}");
            }

            return cities;
        }

        internal static void InsertWetherData(WeatherResponse response)
        {
            try
            {
                using (var connection = new SQLiteConnection(connectionString))
                {
                    connection.Open();

                    string insertMessung = @"
                        INSERT INTO Messung (Datum, Longitude, Latitude)
                        VALUES (@Datum, @Longitude, @Latitude);
                        SELECT last_insert_rowid();";

                    using (var messungCmd = new SQLiteCommand(insertMessung, connection))
                    {
                        messungCmd.Parameters.AddWithValue("@Datum", DateTime.Now);
                        messungCmd.Parameters.AddWithValue("@Longitude", response.Hourly.longitude);
                        messungCmd.Parameters.AddWithValue("@Latitude", response.Hourly.latitude);

                        long messungId = (long)messungCmd.ExecuteScalar();

                        string insertWetterwert = @"
                            INSERT INTO Wetterwert (MessungID, Uhrzeit, Temperatur, Windstaerke)
                            VALUES (@MessungID, @Uhrzeit, @Temperatur, @Windstaerke);";

                        using (var wetterCmd = new SQLiteCommand(insertWetterwert, connection))
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

        internal static List<Wetterwert> GetWetterwerteByMessungId(int messungId)
        {
            List<Wetterwert> werte = new List<Wetterwert>();

            try
            {
                using (var connection = new SQLiteConnection(connectionString))
                {
                    connection.Open();

                    string query = @"
                        SELECT Uhrzeit, Temperatur, Windstaerke
                        FROM Wetterwert
                        WHERE MessungID = @MessungID
                        ORDER BY Uhrzeit ASC;";

                    using (var command = new SQLiteCommand(query, connection))
                    {
                        command.Parameters.AddWithValue("@MessungID", messungId);

                        using (var reader = command.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                werte.Add(new Wetterwert
                                {
                                    Uhrzeit = reader.GetDateTime(reader.GetOrdinal("Uhrzeit")),
                                    Temperatur = reader.GetDouble(reader.GetOrdinal("Temperatur")),
                                    Windstaerke = reader.GetDouble(reader.GetOrdinal("Windstaerke"))
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

        internal static List<Messung> GetAllMessurements()
        {
            var messungen = new List<Messung>();

            try
            {
                using (var connection = new SQLiteConnection(connectionString))
                {
                    connection.Open();

                    string query = "SELECT MessungID, Datum, Longitude, Latitude FROM Messung ORDER BY Datum DESC;";
                    using (var command = new SQLiteCommand(query, connection))
                    using (var reader = command.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            messungen.Add(new Messung
                            {
                                MessungID = reader.GetInt32(reader.GetOrdinal("MessungID")),
                                Datum = reader.GetDateTime(reader.GetOrdinal("Datum")),
                                Longitude = reader.GetDouble(reader.GetOrdinal("Longitude")),
                                Latitude = reader.GetDouble(reader.GetOrdinal("Latitude"))
                            });
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

        internal static void DeleteMessurement(int selectedId)
        {
            try
            {
                using (var connection = new SQLiteConnection(connectionString))
                {
                    connection.Open();

                    using (var transaction = connection.BeginTransaction())
                    {
                        try
                        {
                            string deleteWetterwerte = @"DELETE FROM Wetterwert WHERE MessungID = @MessungID;";
                            using (var cmdWetter = new SQLiteCommand(deleteWetterwerte, connection, transaction))
                            {
                                cmdWetter.Parameters.AddWithValue("@MessungID", selectedId);
                                cmdWetter.ExecuteNonQuery();
                            }

                            string deleteMessung = @"DELETE FROM Messung WHERE MessungID = @MessungID;";
                            using (var cmdMessung = new SQLiteCommand(deleteMessung, connection, transaction))
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
