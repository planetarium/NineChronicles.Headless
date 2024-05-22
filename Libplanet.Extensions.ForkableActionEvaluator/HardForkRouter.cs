using System.Collections.Immutable;
using Libplanet.Action;

namespace Libplanet.Extensions.ForkableActionEvaluator;

internal class HardForkRouter
{
    private readonly ImmutableArray<((long StartIndex, long EndIndex) Range, IActionEvaluator ActionEvaluator)>
        _pairs;

    public HardForkRouter(
        IEnumerable<((long StartIndex, long EndIndex) Range, IActionEvaluator ActionEvaluator)>
            rangeAndEvaluatorPairs
    )
    {
        _pairs = rangeAndEvaluatorPairs.OrderBy(x => x.Range.StartIndex).ToImmutableArray();

        CheckCollision(_pairs);
    }

    public IActionEvaluator GetEvaluator(long blockIndex)
    {
        foreach (var ((startIndex, endIndex), actionEvaluator) in _pairs)
        {
            if (startIndex <= blockIndex && blockIndex <= endIndex)
            {
                return actionEvaluator;
            }
        }

        throw new InvalidOperationException(
            $"Unexpected situation occurred. {nameof(_pairs)} must cover " +
            $"all range over blockchain if {nameof(CheckCollision)}() works well.");
    }

    private static void CheckCollision(
        ImmutableArray<((long StartIndex, long EndIndex) Range, IActionEvaluator ActionEvaluator)> pairs)
    {
        if (pairs.Length == 0)
        {
            throw new ArgumentOutOfRangeException(nameof(pairs), "It must have one more paris at least.");
        }

        if (pairs[0].Range.StartIndex != 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(pairs),
                "The pairs must cover all range of blockchain. Its first element's start index wasn't 0.");
        }

        if (pairs.Last().Range.EndIndex != long.MaxValue)
        {
            throw new ArgumentOutOfRangeException(
                nameof(pairs),
                $"The pairs must cover all range over blockchain. Its last element's end index wasn't long.MaxValue({long.MaxValue}).");
        }

        if (pairs.Length == 1)
        {
            return;
        }

        for (int i = 1; i < pairs.Length; ++i)
        {
            long previousPairEndIndex = pairs[i - 1].Range.EndIndex;
            long startIndex = pairs[i].Range.StartIndex;

            if (previousPairEndIndex + 1 != startIndex)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(pairs),
                    "The pairs must cover all range over blockchain. " +
                    "So Nth pair's end index + 1 must be same as N+1th pair's start index. " +
                    $"But Nth pair's end index is {previousPairEndIndex} and N+1th pair's start index is {startIndex}");
            }
        }
    }
}
