using System;
using Microsoft.Data.Sqlite;
using Libplanet.Crypto;
using Nekoyume.Blockchain;
using Serilog;

namespace NineChronicles.Headless.Services
{
    public class SQLiteAccessControlService : IAccessControlService
    {
        private const string CreateTableSql =
            "CREATE TABLE IF NOT EXISTS txquotalist (address VARCHAR(42), quota INT)";
        private const string CreateIndexSql = "CREATE INDEX IF NOT EXISTS idx_address ON txquotalist (address);";
        private const string GetTxQuotaSql =
            "SELECT quota FROM txquotalist WHERE address=@Address";

        protected readonly string _connectionString;

        public SQLiteAccessControlService(string connectionString)
        {
            _connectionString = connectionString;
            try
            {
                using var connection = new SqliteConnection(_connectionString);
                connection.Open();

                ExecuteNonQuery(connection, CreateTableSql);
                ExecuteNonQuery(connection, CreateIndexSql);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "An error occurred while initializing the database.");
                throw;
            }
        }

        public int? GetTxQuota(Address address)
        {
            try
            {
                using var connection = new SqliteConnection(_connectionString);
                connection.Open();

                using var command = connection.CreateCommand();
                command.CommandText = GetTxQuotaSql;
                command.Parameters.AddWithValue("@Address", address.ToString());

                var queryResult = command.ExecuteScalar();

                if (queryResult != null && queryResult != DBNull.Value)
                {
                    return Convert.ToInt32(queryResult);
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "An error occurred while getting transaction quota.");
            }

            return null;
        }

        private void ExecuteNonQuery(SqliteConnection connection, string commandText)
        {
            using var command = connection.CreateCommand();
            command.CommandText = commandText;
            command.ExecuteNonQuery();
        }
    }
}
