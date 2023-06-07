using System.Collections.Generic;
using GraphQL;
using GraphQL.Types;
using Libplanet;
using Libplanet.Explorer.GraphTypes;
using Nekoyume.Action;
using NCAction = Libplanet.Action.PolymorphicAction<Nekoyume.Action.ActionBase>;

namespace NineChronicles.Headless.GraphTypes;

public partial class ActionQuery
{
    private void RegisterMead()
    {
        Field<NonNullGraphType<ByteStringType>>(
            "requestPledge",
            arguments: new QueryArguments
            {
                new QueryArgument<NonNullGraphType<AddressType>>
                {
                    Name = "agentAddress",
                },
                new QueryArgument<IntGraphType>
                {
                    Name = "mead",
                    DefaultValue = RequestPledge.RefillMead
                }
            },
            resolve: context =>
            {
                var agentAddress = context.GetArgument<Address>("agentAddress");
                int mead = context.GetArgument<int>("mead");
                NCAction action = new RequestPledge
                {
                    AgentAddress = agentAddress,
                    Mead = mead,
                };
                return Encode(context, action);
            }
        );
        Field<NonNullGraphType<ByteStringType>>(
            "approvePledge",
            arguments: new QueryArguments
            {
                new QueryArgument<NonNullGraphType<AddressType>>
                {
                    Name = "patronAddress",
                }
            },
            resolve: context =>
            {
                var patronAddress = context.GetArgument<Address>("patronAddress");
                NCAction action = new ApprovePledge
                {
                    PatronAddress = patronAddress
                };
                return Encode(context, action);
            }
        );
        Field<NonNullGraphType<ByteStringType>>(
            "endPledge",
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
                NCAction action = new EndPledge
                {
                    AgentAddress = agentAddress
                };
                return Encode(context, action);
            }
        );
        Field<NonNullGraphType<ByteStringType>>(
            "createPledge",
            arguments: new QueryArguments
            {
                new QueryArgument<NonNullGraphType<AddressType>>
                {
                    Name = "patronAddress"
                },
                new QueryArgument<NonNullGraphType<ListGraphType<NonNullGraphType<AddressType>>>>
                {
                    Name = "agentAddresses"
                },
                new QueryArgument<IntGraphType>
                {
                    Name = "mead",
                    DefaultValue = RequestPledge.RefillMead
                }
            },
            resolve: context =>
            {
                var patronAddress = context.GetArgument<Address>("patronAddress");
                var agentAddresses = context.GetArgument<List<Address>>("agentAddresses");
                var mead = context.GetArgument<int>("mead");
                NCAction action = new CreatePledge
                {
                    PatronAddress = patronAddress,
                    AgentAddresses = agentAddresses,
                    Mead = mead,
                };
                return Encode(context, action);
            }
        );
    }
}
