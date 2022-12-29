using System.Collections.Generic;
using Bencodex;
using GraphQL;
using GraphQL.Types;
using Libplanet.Explorer.GraphTypes;
using Nekoyume.Action.Factory;
using Nekoyume.Model.Faucet;
using NCAction = Libplanet.Action.PolymorphicAction<Nekoyume.Action.ActionBase>;

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
                NCAction action = FaucetFactory.CreateFaucetCurrency(agentAddress, faucetNcg, faucetCrystal);
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
                NCAction action = FaucetFactory.CreateFaucetRune(avatarAddress, faucetRuneInfos);
                return Encode(context, action);
            }
        );
    }

    internal static byte[] Encode(IResolveFieldContext context, NCAction action)
    {
        return Codec.Encode(action.PlainValue);
    }
}
