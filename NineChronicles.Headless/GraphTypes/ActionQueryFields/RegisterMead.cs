using GraphQL;
using GraphQL.Types;
using Libplanet;
using Libplanet.Explorer.GraphTypes;
using Nekoyume;
using Nekoyume.Action;
using NCAction = Libplanet.Action.PolymorphicAction<Nekoyume.Action.ActionBase>;

namespace NineChronicles.Headless.GraphTypes;

public partial class ActionQuery
{
    private void RegisterMead()
    {
        Field<NonNullGraphType<ByteStringType>>(
            "bringEinheri",
            arguments: new QueryArguments
            {
                new QueryArgument<NonNullGraphType<AddressType>>
                {
                    Name = "agentAddress",
                }
            },
            resolve: context =>
            {
                var agentAddress = context.GetArgument<Address>("agentAddress");
                NCAction action = new BringEinheri
                {
                    EinheriAddress = agentAddress,
                };
                return Encode(context, action);
            }
        );
        Field<NonNullGraphType<ByteStringType>>(
            "takeSides",
            arguments: new QueryArguments
            {
                new QueryArgument<NonNullGraphType<AddressType>>
                {
                    Name = "valkyrieAddress",
                }
            },
            resolve: context =>
            {
                var valkyrieAddress = context.GetArgument<Address>("valkyrieAddress");
                NCAction action = new TakeSides
                {
                    ValkyrieAddress = valkyrieAddress,
                };
                return Encode(context, action);
            }
        );
        Field<NonNullGraphType<ByteStringType>>(
            "releaseEinheri",
            arguments: new QueryArguments
            {
                new QueryArgument<NonNullGraphType<AddressType>>
                {
                    Name = "einheriAddress",
                }
            },
            resolve: context =>
            {
                var eienhriAddress = context.GetArgument<Address>("einheriAddress");
                NCAction action = new ReleaseEinheri
                {
                    EinheriAddress = eienhriAddress,
                };
                return Encode(context, action);
            }
        );
    }
}
