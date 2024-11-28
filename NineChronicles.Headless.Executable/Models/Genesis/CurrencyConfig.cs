using System;
using System.Collections.Generic;

namespace NineChronicles.Headless.Executable.Models.Genesis
{
    /// <summary>
    /// Currency related configurations.<br/>
    /// Set initial minter(Tx signer) and/or initial currency depositions.<br/>
    /// If not provided, default values will set.
    /// </summary>
    [Serializable]
    public struct CurrencyConfig
    {
        /// <summary>
        /// Gets or sets the private key of initial currency minter.
        /// If not provided, a new private key will be created and used.
        /// </summary>
        public string? InitialMinter { get; set; } // PrivateKey, not Address

        /// <summary>
        /// Gets or sets initial currency deposition list.
        /// If you leave it to empty list or even not provide,
        /// the <see cref="InitialMinter"/> will get 10000 currency.
        /// You can see newly created deposition info in <c>initial_deposit.csv</c> file.
        /// </summary>
        public List<Nekoyume.Action.GoldDistribution>? InitialCurrencyDeposit { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether to allow mint.
        /// </summary>
        public bool AllowMint { get; set; }
    }
}
