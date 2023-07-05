using Libplanet;
using System;
using System.Collections.Generic;

namespace NineChronicles.Headless.GraphTypes.States
{
    public class ArenaSimulationState
    {
        public long? blockIndex { get; set; }
        public List<ArenaSimulationResult>? result { get; set; }
        public decimal winPercentage { get; set; }
    }
}
