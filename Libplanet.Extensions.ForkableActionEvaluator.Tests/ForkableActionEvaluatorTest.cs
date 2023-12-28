using System.Security.Cryptography;
using Bencodex.Types;
using Libplanet.Action;
using Libplanet.Action.Loader;
using Libplanet.Action.State;
using Libplanet.Types.Blocks;
using Libplanet.Common;
using Libplanet.Crypto;
using Libplanet.Types.Tx;
using ArgumentOutOfRangeException = System.ArgumentOutOfRangeException;

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
        }, new SingleActionLoader(typeof(MockAction)));

        Assert.Equal((Text)"PRE", Assert.Single(evaluator.Evaluate(new MockBlock(0), null, out _)).Action);
        Assert.Equal((Text)"PRE", Assert.Single(evaluator.Evaluate(new MockBlock(99), null, out _)).Action);
        Assert.Equal((Text)"PRE", Assert.Single(evaluator.Evaluate(new MockBlock(100), null, out _)).Action);
        Assert.Equal((Text)"POST", Assert.Single(evaluator.Evaluate(new MockBlock(101), null, out _)).Action);
        Assert.Equal((Text)"POST", Assert.Single(evaluator.Evaluate(new MockBlock(long.MaxValue), null, out _)).Action);
    }

    [Fact]
    public void CheckPairs()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new ForkableActionEvaluator(
            new ((long, long), IActionEvaluator)[]
            {
                ((0L, 100L), new PreActionEvaluator()),
                ((99L, long.MaxValue), new PostActionEvaluator()),
            }, new SingleActionLoader(typeof(MockAction))));
        Assert.Throws<ArgumentOutOfRangeException>(() => new ForkableActionEvaluator(
            new ((long, long), IActionEvaluator)[]
            {
                ((0L, 100L), new PreActionEvaluator()),
                ((100L, long.MaxValue), new PostActionEvaluator()),
            }, new SingleActionLoader(typeof(MockAction))));
        Assert.Throws<ArgumentOutOfRangeException>(() => new ForkableActionEvaluator(
            new ((long, long), IActionEvaluator)[]
            {
                ((50L, 100L), new PreActionEvaluator()),
                ((101L, long.MaxValue), new PostActionEvaluator()),
            }, new SingleActionLoader(typeof(MockAction))));
        Assert.Throws<ArgumentOutOfRangeException>(() => new ForkableActionEvaluator(
            new ((long, long), IActionEvaluator)[]
            {
                ((0L, 100L), new PreActionEvaluator()),
                ((101L, long.MaxValue - 1), new PostActionEvaluator()),
            }, new SingleActionLoader(typeof(MockAction))));
    }
}

class PostActionEvaluator : IActionEvaluator
{
    public IActionLoader ActionLoader => throw new NotSupportedException();
    public IReadOnlyList<ICommittedActionEvaluation> Evaluate(
        IPreEvaluationBlock block, HashDigest<SHA256>? baseStateRootHash, out HashDigest<SHA256> stateRootHash)
    {
        stateRootHash = new HashDigest<SHA256>();
        return new ICommittedActionEvaluation[]
        {
            new CommittedActionEvaluation(
                (Text)"POST",
                new CommittedActionContext(
                    default,
                    null,
                    default,
                    0,
                    0,
                    default,
                    0,
                    false),
                default)
        };
    }
}

class PreActionEvaluator : IActionEvaluator
{
    public IActionLoader ActionLoader => throw new NotSupportedException();
    public IReadOnlyList<ICommittedActionEvaluation> Evaluate(
        IPreEvaluationBlock block, HashDigest<SHA256>? baseStateRootHash, out HashDigest<SHA256> stateRootHash)
    {
        stateRootHash = new HashDigest<SHA256>();
        return new ICommittedActionEvaluation[]
        {
            new CommittedActionEvaluation(
                (Text)"PRE",
                new CommittedActionContext(
                    default,
                    null,
                    default,
                    0,
                    0,
                    default,
                    0,
                    false),
                default)
        };
    }
}

class MockAction : IAction
{
    public IValue PlainValue => default(Null);

    public void LoadPlainValue(IValue plainValue)
    {
    }

    public IWorld Execute(IActionContext context) => context.PreviousState;
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
