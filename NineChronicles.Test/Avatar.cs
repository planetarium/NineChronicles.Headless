using GraphQL;
using Libplanet;
using Libplanet.Crypto;
using NineChronicles.Test.Type;

namespace NineChronicles.Test;

public class Avatar
{
    public static async Task<AvatarStateType[]> GetAvatarStates(Libplanet.Address address)
    {
        var query = $@"query {{
                    stateQuery {{
                        agent(address: ""{address}"") {{
                            address
                            avatarStates {{
                                index
                                address
                                name 
                            }}
                        }}
                    }}
                }}";
        (bool success, StateQueryResponseType data, GraphQLError[]? errors) =
            await Graphql.Query<StateQueryResponseType>(query);
        if (!success || data.StateQuery.Agent is null)
        {
            Console.WriteLine("Error!");
            foreach (GraphQLError e in errors ?? new GraphQLError[] { })
            {
                Console.WriteLine(e.Message);
            }

            return new AvatarStateType[] { };
        }

        return data.StateQuery.Agent.AvatarStates;
    }

    public static async Task<Libplanet.Address?> CreateAvatar(PrivateKey pk, int index = 1)
    {
        var query = $@"createAvatar (
            index: {index},
            name: ""avatar{index}""
        )";
        (bool success, ActionTxQueryResponseType data, GraphQLError[]? erros) = await Graphql.Action(pk, query);
        if (!success)
        {
            Console.WriteLine($"createAvatar Action failed. Try again later. :: {erros}");
        }

        var tx = ByteUtil.ParseHex(data.ActionTxQuery.CreateAvatar);
        var signature = pk.Sign(tx);
        (bool result, string txId) = await Graphql.Stage(tx, signature);
        if (!result)
        {
            Console.WriteLine("Create avatar action stage failed");
            return null;
        }

        string txResult = await Graphql.WaitTxMining(txId);
        if (txResult == "SUCCESS")
        {
            Console.WriteLine("Avatar created");
            var avatarStates = await GetAvatarStates(pk.ToAddress());
            return avatarStates[0].Address;
        }

        Console.WriteLine("Avatar create failed. Please try later.");
        return null;
    }

    public static async Task<Libplanet.Address?> SelectAvatar(PrivateKey pk)
    {
        var avatarStates = await GetAvatarStates(pk.ToAddress());
        if (avatarStates.Length == 0)
        {
            Console.WriteLine("No avatar created. Creating one...");
            return await CreateAvatar(pk);
        }

        Console.WriteLine($"You have {avatarStates.Length} avatars in your address. Select one to use");
        if (avatarStates.Length < 3)
        {
            Console.WriteLine("0. Create new avatar");
        }

        foreach (var item in avatarStates.Select((value, index) => (value, index)))
        {
            Console.WriteLine($"{item.index + 1}: {item.value.Address}");
        }

        var index = Util.Select(avatarStates);
        Libplanet.Address? avatarAddress;
        if (index == 0)
        {
            avatarAddress = await CreateAvatar(pk);
        }
        else
        {
            avatarAddress = avatarStates[index - 1].Address;
        }

        return avatarAddress;
    }
}
