using Libplanet.Action;

namespace Libplanet.Extensions.RemoteActionEvaluator;

internal class Random : System.Random, IRandom
{
    public Random(int seed)
        : base(seed)
    {
        Seed = seed;
    }

    public int Seed { get; private set; }
}
