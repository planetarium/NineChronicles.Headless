using System.Collections.Generic;
using GraphQL.Types;
using Nekoyume.Model.Faucet;

namespace NineChronicles.Headless.DevExtensions.GraphTypes;

public class FaucetRuneInputType : InputObjectGraphType<FaucetRuneInfo>
{
    public FaucetRuneInputType()
    {
        Field<NonNullGraphType<IntGraphType>>("runeId");
        Field<NonNullGraphType<IntGraphType>>("amount");
    }

    public override object ParseDictionary(IDictionary<string, object?> value)
    {
        int runeId = (int)value["runeId"]!;
        int amount = (int)value["amount"]!;
        return new FaucetRuneInfo(runeId, amount);
    }
}
