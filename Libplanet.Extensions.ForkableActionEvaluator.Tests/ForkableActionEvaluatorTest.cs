using System.Security.Cryptography;
using Bencodex.Types;
using Libplanet.Action;
using Libplanet.Action.Loader;
using Libplanet.Types.Blocks;
using Libplanet.Common;
using Libplanet.Crypto;
using Libplanet.Extensions.ActionEvaluatorCommonComponents;
using Libplanet.Types.Tx;
using ActionEvaluation = Libplanet.Extensions.ActionEvaluatorCommonComponents.ActionEvaluation;
using ArgumentOutOfRangeException = System.ArgumentOutOfRangeException;
using Random = Libplanet.Extensions.ActionEvaluatorCommonComponents.Random;

namespace Libplanet.Extensions.ForkableActionEvaluator.Tests;

public class ForkableActionEvaluatorTest
{
    [Fact]
    public void ForkEvaluation()
    {
        var evaluator = new ForkableActionEvaluator(new ((long, long), IActionEvaluator)[]
        {
            ((0L, 100L), new PreActionEvaluator()),
            ((101L, long.MaxValue), new PostActionEvaluator()),
        });

        Assert.Equal((Text)"PRE", Assert.Single(evaluator.Evaluate(new MockBlock(0))).Action);
        Assert.Equal((Text)"PRE", Assert.Single(evaluator.Evaluate(new MockBlock(99))).Action);
        Assert.Equal((Text)"PRE", Assert.Single(evaluator.Evaluate(new MockBlock(100))).Action);
        Assert.Equal((Text)"POST", Assert.Single(evaluator.Evaluate(new MockBlock(101))).Action);
        Assert.Equal((Text)"POST", Assert.Single(evaluator.Evaluate(new MockBlock(long.MaxValue))).Action);
    }

    [Fact]
    public void CheckPairs()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new ForkableActionEvaluator(
            new ((long, long), IActionEvaluator)[]
            {
                ((0L, 100L), new PreActionEvaluator()),
                ((99L, long.MaxValue), new PostActionEvaluator()),
            }));
        Assert.Throws<ArgumentOutOfRangeException>(() => new ForkableActionEvaluator(
            new ((long, long), IActionEvaluator)[]
            {
                ((0L, 100L), new PreActionEvaluator()),
                ((100L, long.MaxValue), new PostActionEvaluator()),
            }));
        Assert.Throws<ArgumentOutOfRangeException>(() => new ForkableActionEvaluator(
            new ((long, long), IActionEvaluator)[]
            {
                ((50L, 100L), new PreActionEvaluator()),
                ((101L, long.MaxValue), new PostActionEvaluator()),
            }));
        Assert.Throws<ArgumentOutOfRangeException>(() => new ForkableActionEvaluator(
            new ((long, long), IActionEvaluator)[]
            {
                ((0L, 100L), new PreActionEvaluator()),
                ((101L, long.MaxValue - 1), new PostActionEvaluator()),
            }));
    }
}

class PostActionEvaluator : IActionEvaluator
{
    public IActionLoader ActionLoader => throw new NotSupportedException();
    public IReadOnlyList<IActionEvaluation> Evaluate(IPreEvaluationBlock block)
    {
        return new IActionEvaluation[]
        {
            new ActionEvaluation(
                (Text)"POST",
                new ActionContext(
                    null,
                    default,
                    null,
                    default,
                    0,
                    0,
                    false,
                    new World(),
                    new Random(0),
                    null,
                    false),
                new World(),
                null)
        };
    }
}

class PreActionEvaluator : IActionEvaluator
{
    public IActionLoader ActionLoader => throw new NotSupportedException();
    public IReadOnlyList<IActionEvaluation> Evaluate(IPreEvaluationBlock block)
    {
        return new IActionEvaluation[]
        {
            new ActionEvaluation(
                (Text)"PRE",
                new ActionContext(
                    null,
                    default,
                    null,
                    default,
                    0,
                    0,
                    false,
                    new World(),
                    new Random(0),
                    null,
                    false),
                new World(),
                null)
        };
    }
}

class MockBlock : IPreEvaluationBlock
{
    public MockBlock(long blockIndex)
    {
        Index = blockIndex;
    }

    public int ProtocolVersion { get; }
    public long Index { get; }
    public DateTimeOffset Timestamp { get; }
    public Address Miner { get; }
    public PublicKey? PublicKey { get; }
    public BlockHash? PreviousHash { get; }
    public HashDigest<SHA256>? TxHash { get; }
    public BlockCommit? LastCommit { get; }
    public IReadOnlyList<ITransaction> Transactions { get; }
    public HashDigest<SHA256> PreEvaluationHash { get; }
}
