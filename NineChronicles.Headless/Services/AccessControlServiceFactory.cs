using System;
using Nekoyume.Blockchain;

namespace NineChronicles.Headless.Services
{
    public static class AccessControlServiceFactory
    {
        public enum StorageType
        {
            /// <summary>
            /// Use Redis
            /// </summary>
            Redis,

            /// <summary>
            /// Use SQLite
            /// </summary>
            SQLite,

            /// <summary>
            /// Use RestAPI
            /// </summary>
            RestAPI,

            /// <summary>
            /// Use local
            /// </summary>
            Local
        }

        public static IAccessControlService Create(
            StorageType storageType,
            string connectionString)
        {
            return storageType switch
            {
                StorageType.Redis => new RedisAccessControlService(connectionString),
                StorageType.SQLite => new SQLiteAccessControlService(connectionString),
                StorageType.RestAPI => new RestAPIAccessControlService(connectionString),
                StorageType.Local => new LocalAccessControlService(connectionString),
                _ => throw new ArgumentOutOfRangeException(nameof(storageType), storageType, null)
            };
        }
    }
}
