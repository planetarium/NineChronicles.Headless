using System;

namespace NineChronicles.Headless.Executable.Models.Genesis
{
    [Serializable]
    public struct Validator
    {
        public string PublicKey { get; set; }

        public long Power { get; set; }
    }
}
