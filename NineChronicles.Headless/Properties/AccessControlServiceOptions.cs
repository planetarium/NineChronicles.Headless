using System;
using System.ComponentModel.DataAnnotations;
using NineChronicles.Headless.Services;

namespace NineChronicles.Headless.Properties
{
    public class AccessControlServiceOptions
    {
        [Required]
        public string AccessControlServiceType { get; set; } = null!;

        [Required]
        public string AccessControlServiceConnectionString { get; set; } = null!;

        public AccessControlServiceFactory.StorageType GetStorageType()
        {
            return Enum.Parse<AccessControlServiceFactory.StorageType>(AccessControlServiceType, true);
        }
    }
}
