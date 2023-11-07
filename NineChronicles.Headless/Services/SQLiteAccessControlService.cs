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
        private const string GetTxQuotaSql =
            "SELECT quota FROM txquotalist WHERE address=@Address";

        protected readonly string _connectionString;

        public SQLiteAccessControlService(string connectionString)
        {
            _connectionString = connectionString;
            using var connection = new SqliteConnection(_connectionString);
            connection.Open();

            using var command = connection.CreateCommand();
            command.CommandText = CreateTableSql;
            command.ExecuteNonQuery();
        }

        public int? GetTxQuota(Address address)
        {
            using var connection = new SqliteConnection(_connectionString);
            connection.Open();

            using var command = connection.CreateCommand();
            command.CommandText = GetTxQuotaSql;
            command.Parameters.AddWithValue("@Address", address.ToString());

            var queryResult = command.ExecuteScalar();

            if (queryResult != null)
            {
                return Convert.ToInt32(queryResult);
            }

            return null;
        }
    }
}
