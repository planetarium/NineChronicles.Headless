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
    public IReadOnlyList<IActionResult> Evaluate(IPreEvaluationBlock block)
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
                    new AccountStateDelta(),
                    new Random(0),
                    null,
                    false),
                new AccountStateDelta(),
                null)
        }.Select(x => new ActionResult(x)).ToArray();
    }
}

class PreActionEvaluator : IActionEvaluator
{
    public IActionLoader ActionLoader => throw new NotSupportedException();
    public IReadOnlyList<IActionResult> Evaluate(IPreEvaluationBlock block)
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
                    new AccountStateDelta(),
                    new Random(0),
                    null,
                    false),
                new AccountStateDelta(),
                null)
        }.Select(x => new ActionResult(x)).ToArray();
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
