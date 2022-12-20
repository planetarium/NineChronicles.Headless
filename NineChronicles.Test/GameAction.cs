using GraphQL;
using Libplanet;
using Libplanet.Crypto;
using NineChronicles.Test.Type;

namespace NineChronicles.Test;

public class GameAction
{
    public static async Task<bool> GetStatus(PrivateKey pk)
    {
        var query = $@"query {{
            stateQuery {{
                agent(address: ""{pk.ToAddress()}"") {{
                    address
                    gold
                    crystal
                    avatarStates {{
                        index
                        address
                        name
                        level
                        exp
                        actionPoint
                    }}
                }}
            }}
        }}";
        (bool success, StateQueryResponseType data, GraphQLError[]? errors) = await Graphql.Query<StateQueryResponseType>(query);
        if (!success)
        {
            Console.WriteLine("Get Status action failed.");
            return false;
        }
        Console.WriteLine(data.StateQuery.Agent);
        return true;
    }

    public static async Task<bool> RuneEnhancement(PrivateKey pk)
    {
        var query = $@"";
        (bool success, ActionTxQueryResponseType data, GraphQLError[]? errors) = await Graphql.Action(pk, query);
        if (!success)
        {
            Console.WriteLine("Rune enhancement action failed.");
            return false;
        }

        var tx = ByteUtil.ParseHex(data.ActionTxQuery.RuneEnhancement);
        var signature = pk.Sign(tx);
        (bool result, string txId) = await Graphql.Stage(tx, signature);
        if (!result)
        {
            Console.WriteLine("Rune enhancement action stage failed");
            return false;
        }

        var txResult = await Graphql.WaitTxMining(txId);
        if (txResult == "SUCCESS")
        {
            Console.WriteLine("Rune Enhancement Done.");
            return true;
        }

        Console.WriteLine($"Rune Enhancement Failed: {txResult}");
        return false;
    }
}
