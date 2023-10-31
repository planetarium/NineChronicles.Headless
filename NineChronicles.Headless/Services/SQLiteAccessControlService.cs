using System;
using Microsoft.Data.Sqlite;
using Libplanet.Crypto;
using Nekoyume.Blockchain;

namespace NineChronicles.Headless.Services
{
    public class SQLiteAccessControlService : IAccessControlService
    {
        private const string CreateTableSql =
            "CREATE TABLE IF NOT EXISTS blocklist (address VARCHAR(42), quota INT)";
        private const string CheckAccessSql =
            "SELECT EXISTS(SELECT 1 FROM blocklist WHERE address=@Address)";
        private const string GetTxQuotaSql =
            "SELECT quota FROM blocklist WHERE address=@Address";

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

        public bool IsAccessDenied(Address address)
        {
            using var connection = new SqliteConnection(_connectionString);
            connection.Open();

            using var command = connection.CreateCommand();
            command.CommandText = CheckAccessSql;
            command.Parameters.AddWithValue("@Address", address.ToString());

            var result = command.ExecuteScalar();

            return result is not null && (long)result == 1;
        }

        public int? GetTxQuota(Address address)
        {
            using var connection = new SqliteConnection(_connectionString);
            connection.Open();

            using var command = connection.CreateCommand();
            command.CommandText = GetTxQuotaSql;
            command.Parameters.AddWithValue("@Address", address.ToString());

            var queryResult = command.ExecuteScalar();

            return Convert.ToInt32(queryResult);
        }
    }
}
