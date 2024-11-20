using System;

namespace NineChronicles.Headless.Executable.Models.Genesis
{
    /// <summary>
    /// Admin related configurations.
    /// If not provided, no admin will be set.
    /// </summary>
    [Serializable]
    public struct AdminConfig
    {
        /// <summary>
        /// Gets or sets a value indicating whether active admin address or not.
        /// </summary>
        public bool Activate { get; set; }

        /// <summary>
        /// Gets or sets address to give admin privilege.<br/>
        /// If <see cref="Activate"/> is <c>true</c> and no <see cref="Address"/> provided, the <see cref="CurrencyConfig.InitialMinter"/> will get admin privilege.
        /// </summary>
        public string Address { get; set; }

        /// <summary>
        /// Gets or sets the block index to persist admin privilege.
        /// After <see cref="ValidUntil"/> block index, admin will no longer be admin.
        /// </summary>
        public long ValidUntil { get; set; }
    }
}
