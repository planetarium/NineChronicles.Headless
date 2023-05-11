using System.Collections.Immutable;
using Bencodex.Types;
using Libplanet.Action;
using Libplanet.Crypto;

namespace Libplanet.Extensions.ActionEvaluatorCommonComponents.Tests;

public class ActionEvaluationSerializerTest
{
    [Fact]
    public void Serialization()
    {
        var addresses = Enumerable.Repeat(0, 4).Select(_ => new PrivateKey().ToAddress()).ToImmutableList();
        AccountStateDelta outputStates = (AccountStateDelta)new AccountStateDelta()
            .SetState(addresses[0], Null.Value)
            .SetState(addresses[1], (Text)"foo")
            .SetState(addresses[2], new List((Text)"bar"));
        var actionEvaluation = new ActionEvaluation(
            Null.Value,
            new ActionContext(null,
                addresses[0],
                null,
                addresses[1],
                0,
                false,
                new AccountStateDelta(),
                new Random(123),
                null,
                true),
            outputStates,
            new Libplanet.Action.UnexpectedlyTerminatedActionException("", null, null, null, null, new NullAction(), null),
            new List<string> { "one", "two" });
        var serialized = ActionEvaluationMarshaller.Serialize(actionEvaluation);
        var deserialized = ActionEvaluationMarshaller.Deserialize(serialized);

        Assert.Equal(Null.Value, deserialized.Action);
        Assert.Equal(123, deserialized.InputContext.Random.Seed);
        Assert.Equal(0, deserialized.InputContext.BlockIndex);
        Assert.Equal(new[] { "one", "two" }, deserialized.Logs);
        Assert.Equal(addresses[0], deserialized.InputContext.Signer);
        Assert.Equal(addresses[1], deserialized.InputContext.Miner);
        Assert.Equal(Null.Value, deserialized.OutputStates.GetState(addresses[0]));
        Assert.Equal((Text)"foo", deserialized.OutputStates.GetState(addresses[1]));
        Assert.Equal(new List((Text)"bar"), deserialized.OutputStates.GetState(addresses[2]));
    }
}
