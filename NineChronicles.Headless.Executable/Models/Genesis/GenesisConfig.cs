using System;
using System.Collections.Generic;

namespace NineChronicles.Headless.Executable.Models.Genesis
{
    /// <summary>
    /// Config to mine new genesis block.
    /// </summary>
    /// <list type="table">
    /// <listheader>
    /// <term>Config</term>
    /// <description>Description</description>
    /// </listheader>
    /// <item>
    /// <term><see cref="DataConfig">Data</see></term>
    /// <description>Required. Sets game data to genesis block.</description>
    /// </item>
    /// <item>
    /// <term><see cref="CurrencyConfig">Currency</see></term>
    /// <description>Optional. Sets initial currency mint/deposition data to genesis block.</description>
    /// </item>
    /// <item>
    /// <term><see cref="AdminConfig">Admin</see></term>
    /// <description>Optional. Sets game admin and lifespan to genesis block.</description>
    /// </item>
    /// <item>
    /// <term><see cref="InitialValidatorSet">Initial validator set</see></term>
    /// <description>Optional. Sets game admin and lifespan to genesis block.</description>
    /// </item>
    /// </list>
    [Serializable]
    public struct GenesisConfig
    {
        public DataConfig Data { get; set; } // Required
        public CurrencyConfig? Currency { get; set; }
        public AdminConfig? Admin { get; set; }
        public List<Validator>? InitialValidatorSet { get; set; }

        public List<MeadConfig>? InitialMeadConfigs { get; set; }

        public List<PledgeConfig>? InitialPledgeConfigs { get; set; }
    }
}
