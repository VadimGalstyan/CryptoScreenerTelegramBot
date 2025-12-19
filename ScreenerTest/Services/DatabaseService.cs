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

        //public void SaveAlert(Alert alert)
        //{
        //    using var conn = new SqlConnection(_connectionString);
        //    conn.Open();

        //    var cmd = new SqlCommand(@"
        //        INSERT INTO Alerts (Exchange, Pair, ChangePercent, AlertTime, Timeframe)
        //        VALUES (@Exchange, @Pair, @ChangePercent, @AlertTime, @Timeframe)
        //    ", conn);


        //    cmd.Parameters.AddWithValue("@Exchange", alert.Exchange);
        //    cmd.Parameters.AddWithValue("@Pair", alert.Pair);
        //    cmd.Parameters.AddWithValue("@ChangePercent", alert.ChangePercent);
        //    cmd.Parameters.AddWithValue("@AlertTime", alert.Time);
        //    cmd.Parameters.AddWithValue("@Timeframe", alert.Timeframe);

        //    cmd.ExecuteNonQuery();
        //}

        public async Task CreateUserAlertsTable(long chatId)
        {
            string tableName = $"Alerts_{chatId}";

            string sql = $@"
                            IF NOT EXISTS (SELECT * FROM sysobjects WHERE name='{tableName}' AND xtype='U')
                            CREATE TABLE [{tableName}] (
                                Id INT IDENTITY PRIMARY KEY,
                                Exchange NVARCHAR(50),
                                Pair NVARCHAR(50),
                                ChangePercent DECIMAL(18, 4),
                                Time DATETIME,
                                Timeframe INT
                            )";

            using (var connection = new SqlConnection(_connectionString))
            {
                using (var command = new SqlCommand(sql, connection))
                {
                    await connection.OpenAsync();
                    await command.ExecuteNonQueryAsync();
                }
            }
        }

        public async Task SaveAlert(long chatId, Alert alert)
        {
            string tableName = $"Alerts_{chatId}";
            string sql = $@"
             INSERT INTO [{tableName}] (Exchange, Pair, ChangePercent, Time, Timeframe)
             VALUES (@Exchange, @Pair, @ChangePercent, @Time, @Timeframe)";

            using (var connection = new SqlConnection(_connectionString))
            using (var command = new SqlCommand(sql, connection))
            {
                command.Parameters.AddWithValue("@Exchange", alert.Exchange);
                command.Parameters.AddWithValue("@Pair", alert.Pair);
                command.Parameters.AddWithValue("@ChangePercent", alert.ChangePercent);
                command.Parameters.AddWithValue("@Time", alert.Time);
                command.Parameters.AddWithValue("@Timeframe", alert.Timeframe);

                await connection.OpenAsync();
                await command.ExecuteNonQueryAsync();
            }
        }

        public async Task ClearOldAlerts(long chatId)
        {
            string tableName = $"Alerts_{chatId}"; // таблица конкретного пользователя
            string sql = $@"
             DELETE FROM [{tableName}]
             WHERE DATEDIFF(MINUTE, Time, SYSUTCDATETIME()) > Timeframe;";

            using var conn = new SqlConnection(_connectionString);
            await conn.OpenAsync();
            using var cmd = new SqlCommand(sql, conn);
            await cmd.ExecuteNonQueryAsync();
        }

        public async Task<bool> ContainingAlert(long chatId, string pair)
        {
            string tableName = $"Alerts_{chatId}";
            string sql = $"SELECT 1 FROM [{tableName}] WHERE Pair = @pair";

            using var conn = new SqlConnection(_connectionString);
            await conn.OpenAsync();
            using var cmd = new SqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@pair", pair);
            var result = await cmd.ExecuteScalarAsync();

            return result != null;
        }



    }
}
