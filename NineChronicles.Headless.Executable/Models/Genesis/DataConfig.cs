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
        /// <summary>
        /// Gets or sets a path of game data table directory.
        /// </summary>
        public string TablePath { get; set; }
    }
}
