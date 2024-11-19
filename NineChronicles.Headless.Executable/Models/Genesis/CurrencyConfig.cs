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
        /// <value>
        /// Private Key of initial currency minter.<br/>
        /// If not provided, a new private key will be created and used.<br/>
        /// </value>
        public string? InitialMinter { get; set; } // PrivateKey, not Address

        /// <value>
        /// Initial currency deposition list.<br/>
        /// If you leave it to empty list or even not provide, the `InitialMinter` will get 10000 currency.<br.>
        /// You can see newly created deposition info in <c>initial_deposit.csv</c> file.
        /// </value>
        public List<Nekoyume.Action.GoldDistribution>? InitialCurrencyDeposit { get; set; }

        public bool AllowMint { get; set; }
    }
}
