using System;

namespace NineChronicles.Headless.Executable.Models.Genesis
{
    /// <summary>
    /// Game data to set into genesis block.
    /// </summary>
    /// <seealso cref="GenesisConfig"/>
    [Serializable]
    public struct DataConfig
    {
        /// <value>A path of game data table directory.</value>
        public string TablePath { get; set; }
    }
}
