using System;

namespace NineChronicles.Headless.AccessControlCenter.AccessControlService
{
    public static class MutableAccessControlServiceFactory
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
            SQLite
        }

        public static IMutableAccessControlService Create(
            StorageType storageType,
            string connectionString
        )
        {
            return storageType switch
            {
                StorageType.Redis => new MutableRedisAccessControlService(connectionString),
                StorageType.SQLite => new MutableSqliteAccessControlService(connectionString),
                _ => throw new ArgumentOutOfRangeException(nameof(storageType), storageType, null)
            };
        }
    }
}
