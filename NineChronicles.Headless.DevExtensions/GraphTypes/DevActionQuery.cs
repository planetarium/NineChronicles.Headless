using System.Collections.Generic;
using Bencodex;
using GraphQL;
using GraphQL.Types;
using Lib9c.DevExtensions.Action;
using Libplanet.Explorer.GraphTypes;
using Nekoyume.Action;
using TestNCAction = Libplanet.Action.PolymorphicAction<Lib9c.DevExtensions.Action.TestActionBase>;

namespace NineChronicles.Headless.DevExtensions.GraphTypes;

public class DevActionQuery : ObjectGraphType
{
    private static readonly Codec Codec = new Codec();

    internal StandaloneContext StandaloneContext { get; set; }

    public DevActionQuery(StandaloneContext standaloneContext)
    {
        StandaloneContext = standaloneContext;

        Field<NonNullGraphType<ByteStringType>>(
            "faucetCurrency",
            arguments: new QueryArguments(
                new QueryArgument<NonNullGraphType<AddressType>>
                {
                    Name = "agentAddress",
                    Description = "9c Address to use faucet"
                },
                new QueryArgument<IntGraphType>
                {
                    Name = "faucetNcg",
                    Description = "Amount of NCG to get.",
                },
                new QueryArgument<IntGraphType>
                {
                    Name = "faucetCrystal",
                    Description = "Amount of Crystal to get.",
                }
            ),
            resolve: context =>
            {
                var agentAddress = context.GetArgument<Libplanet.Address>("agentAddress");
                var faucetNcg = context.GetArgument<int>("faucetNcg");
                var faucetCrystal = context.GetArgument<int>("faucetCrystal");
                TestNCAction action = new FaucetCurrency
                {
                    AgentAddress = agentAddress,
                    FaucetNcg = faucetNcg,
                    FaucetCrystal = faucetCrystal
                };
                return Encode(context, action);
            }
        );

        Field<NonNullGraphType<ByteStringType>>(
            "faucetRune",
            arguments: new QueryArguments(
                new QueryArgument<NonNullGraphType<AddressType>>
                {
                    Name = "avatarAddress",
                    Description = "avatar Address to use faucet"
                },
                new QueryArgument<ListGraphType<NonNullGraphType<FaucetRuneInputType>>>
                {
                    Name = "faucetRuneInfos",
                    Description = "List of rune info to get: [{runeId: <int>, amount: <int>}].",
                    DefaultValue = new List<FaucetRuneInfo>()
                }
            ),
            resolve: context =>
            {
                var avatarAddress = context.GetArgument<Libplanet.Address>("avatarAddress");
                var faucetRuneInfos = context.GetArgument<List<FaucetRuneInfo>>("faucetRuneInfos");
                TestNCAction action = new FaucetRune
                {
                    AvatarAddress = avatarAddress,
                    FaucetRuneInfos = faucetRuneInfos
                };
                return Encode(context, action);
            }
        );
    }

    internal static byte[] Encode(IResolveFieldContext context, TestNCAction action)
    {
        return Codec.Encode(action.PlainValue);
    }
}
