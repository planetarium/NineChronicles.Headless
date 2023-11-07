using System;
using System.Collections.Generic;
using Microsoft.Data.Sqlite;
using Libplanet.Crypto;
using NineChronicles.Headless.Services;

namespace NineChronicles.Headless.AccessControlCenter.AccessControlService
{
    public class MutableSqliteAccessControlService : SQLiteAccessControlService, IMutableAccessControlService
    {
        private const string AddTxQuotaSql =
            "INSERT OR IGNORE INTO txquotalist (address, quota) VALUES (@Address, @Quota)";
        private const string RemoveTxQuotaSql = "DELETE FROM txquotalist WHERE address=@Address";

        public MutableSqliteAccessControlService(string connectionString) : base(connectionString)
        {
        }

        public void AddTxQuota(Address address, int quota)
        {
            using var connection = new SqliteConnection(_connectionString);
            connection.Open();

            using var command = connection.CreateCommand();
            command.CommandText = AddTxQuotaSql;
            command.Parameters.AddWithValue("@Address", address.ToString());
            command.Parameters.AddWithValue("@Quota", quota);
            command.ExecuteNonQuery();
        }

        public void RemoveTxQuota(Address address)
        {
            using var connection = new SqliteConnection(_connectionString);
            connection.Open();

            using var command = connection.CreateCommand();
            command.CommandText = RemoveTxQuotaSql;
            command.Parameters.AddWithValue("@Address", address.ToString());
            command.ExecuteNonQuery();
        }

        public List<Address> ListTxQuotaAddresses(int offset, int limit)
        {
            var txQuotaAddresses = new List<Address>();

            using var connection = new SqliteConnection(_connectionString);
            connection.Open();

            using var command = connection.CreateCommand();
            command.CommandText = $"SELECT address FROM txquotalist LIMIT @Limit OFFSET @Offset";
            command.Parameters.AddWithValue("@Limit", limit);
            command.Parameters.AddWithValue("@Offset", offset);

            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                txQuotaAddresses.Add(new Address(reader.GetString(0)));
            }

            return txQuotaAddresses;
        }
    }
}
