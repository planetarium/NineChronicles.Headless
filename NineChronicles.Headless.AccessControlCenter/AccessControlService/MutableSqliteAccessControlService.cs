using System;
using System.Collections.Generic;
using Microsoft.Data.Sqlite;
using Libplanet.Crypto;
using NineChronicles.Headless.Services;

namespace NineChronicles.Headless.AccessControlCenter.AccessControlService
{
    public class MutableSqliteAccessControlService : SQLiteAccessControlService, IMutableAccessControlService
    {
        private const string DenyAccessSql =
            "INSERT OR IGNORE INTO blocklist (address) VALUES (@Address)";
        private const string AllowAccessSql = "DELETE FROM blocklist WHERE address=@Address";

        public MutableSqliteAccessControlService(string connectionString) : base(connectionString)
        {
        }

        public void DenyAccess(Address address)
        {
            using var connection = new SqliteConnection(_connectionString);
            connection.Open();

            using var command = connection.CreateCommand();
            command.CommandText = DenyAccessSql;
            command.Parameters.AddWithValue("@Address", address.ToString());
            command.ExecuteNonQuery();
        }

        public void AllowAccess(Address address)
        {
            using var connection = new SqliteConnection(_connectionString);
            connection.Open();

            using var command = connection.CreateCommand();
            command.CommandText = AllowAccessSql;
            command.Parameters.AddWithValue("@Address", address.ToString());
            command.ExecuteNonQuery();
        }

        public List<Address> ListBlockedAddresses(int offset, int limit)
        {
            if (limit > 30)
            {
                throw new ArgumentException("Limit cannot exceed 30.", nameof(limit));
            }

            var blockedAddresses = new List<Address>();

            using var connection = new SqliteConnection(_connectionString);
            connection.Open();

            using var command = connection.CreateCommand();
            command.CommandText = $"SELECT address FROM blocklist LIMIT @Limit OFFSET @Offset";
            command.Parameters.AddWithValue("@Limit", limit);
            command.Parameters.AddWithValue("@Offset", offset);

            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                blockedAddresses.Add(new Address(reader.GetString(0)));
            }

            return blockedAddresses;
        }
    }
}
