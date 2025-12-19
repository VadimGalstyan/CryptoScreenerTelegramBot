using Microsoft.Data.SqlClient;
using ScreenerTest.Models;
using System;

namespace ScreenerTest.Services
{
    public class DatabaseService
    {
        private readonly string _connectionString = "";

        public DatabaseService(string connectionString)
        {
            _connectionString = connectionString;
        }

        public void SaveAlert(Alert alert)
        {
            using var conn = new SqlConnection(_connectionString);
            conn.Open();

            var cmd = new SqlCommand(@"
                INSERT INTO Alerts (Exchange, Pair, ChangePercent, AlertTime, Timeframe)
                VALUES (@Exchange, @Pair, @ChangePercent, @AlertTime, @Timeframe)
            ", conn);


            cmd.Parameters.AddWithValue("@Exchange", alert.Exchange);
            cmd.Parameters.AddWithValue("@Pair", alert.Pair);
            cmd.Parameters.AddWithValue("@ChangePercent", alert.ChangePercent);
            cmd.Parameters.AddWithValue("@AlertTime", alert.Time);
            cmd.Parameters.AddWithValue("@Timeframe", alert.Timeframe);

            cmd.ExecuteNonQuery();
        }

        public async Task ClearOldAlerts()
        {

            using var conn = new SqlConnection(_connectionString);
            await conn.OpenAsync();
            var cmd = new SqlCommand("DELETE FROM Alerts WHERE DATEDIFF(MINUTE, AlertTime, SYSUTCDATETIME()) > Timeframe;", conn);
            await cmd.ExecuteNonQueryAsync();

        }

        public async Task<bool> ContainingAlert(string pair)
        {
            using var conn = new SqlConnection(_connectionString);
            await conn.OpenAsync();
            var cmd = new SqlCommand("SELECT 1 FROM Alerts WHERE Pair = @pair", conn);
            cmd.Parameters.AddWithValue("@pair", pair);
            var result = await cmd.ExecuteScalarAsync();

            return result != null;
        }

    }
}
