using System;

namespace NineChronicles.Headless.Executable.Models.Genesis
{
    [Serializable]
    public struct PledgeConfig
    {
        public string AgentAddress { get; set; }

        public string PatronAddress { get; set; }

        public int Mead { get; set; }
    }
}
