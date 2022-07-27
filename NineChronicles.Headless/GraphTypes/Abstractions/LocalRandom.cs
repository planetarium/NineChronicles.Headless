using Libplanet.Action;
using System;

namespace NineChronicles.Headless.GraphTypes
{
    internal class LocalRandom : System.Random, IRandom
    {
        public int Seed
        {
            get
            {
                throw new NotImplementedException();
            }
        }

        public LocalRandom(int seed)
            : base(seed)
        {
        }
    }
}
