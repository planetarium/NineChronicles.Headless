using System.Collections.Generic;
using System.Linq;
using GraphQL;
using GraphQL.Types;
using Libplanet;
using Libplanet.Explorer.GraphTypes;
using Nekoyume.Action;

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
                ActionBase action = new RequestPledge
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
                ActionBase action = new ApprovePledge
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
                ActionBase action = new EndPledge
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
                List<(Address, Address)> addresses = agentAddresses.Select(a => (a, a.GetPledgeAddress())).ToList();
                ActionBase action = new CreatePledge
                {
                    PatronAddress = patronAddress,
                    AgentAddresses = addresses,
                    Mead = mead,
                };
                return Encode(context, action);
            }
        );
    }
}
