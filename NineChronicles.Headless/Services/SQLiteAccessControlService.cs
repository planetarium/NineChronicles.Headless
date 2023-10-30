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

            var queryResult = command.ExecuteScalar();

            var result = queryResult is not null && (long)queryResult == 1;

            if (result)
            {
                Log.ForContext("Source", nameof(IAccessControlService))
                    .Debug("\"{Address}\" is access denied", address);
            }

            return result;
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
