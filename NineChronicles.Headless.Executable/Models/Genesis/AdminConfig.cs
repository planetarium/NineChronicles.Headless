using System;

namespace NineChronicles.Headless.Executable.Models.Genesis
{
    /// <summary>
    /// Admin related configurations.<br/>
    /// If not provided, no admin will be set.
    /// </summary>
    [Serializable]
    public struct AdminConfig
    {
        /// <value>Whether active admin address or not.</value>
        public bool Activate { get; set; }

        /// <value>
        /// Address to give admin privilege.<br/>
        /// If <c>Activate</c> is <c>true</c> and no <c>Address</c> provided, the <see cref="CurrencyConfig.InitialMinter"/> will get admin privilege.
        /// </value>
        public string Address { get; set; }

        /// <value>
        /// The block count to persist admin privilege.<br/>
        /// After this block, admin will no longer be admin.
        /// </value>
        public long ValidUntil { get; set; }
    }
}
