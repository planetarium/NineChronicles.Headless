using System;
using System.Collections.Generic;
using System.Linq;
using GraphQL;
using GraphQL.Types;
using Libplanet.Crypto;
using Libplanet.Explorer.GraphTypes;
using Log = Serilog.Log;

namespace NineChronicles.Headless.GraphTypes
{
    public class RpcInformationQuery : ObjectGraphType
    {
        public RpcInformationQuery(ActionEvaluationPublisher publisher)
        {
            Field<NonNullGraphType<IntGraphType>>(
                name: "totalCount",
                description: "total count by connected to this node.",
                resolve: context => publisher.GetClients().Count);
            Field<NonNullGraphType<ListGraphType<AddressType>>>(
                name: "clients",
                description: "List of address connected to this node.",
                resolve: context => publisher.GetClients());
            Field<NonNullGraphType<IntGraphType>>(
                name: "totalCountByDevice",
                arguments: new QueryArguments(
                    new QueryArgument<NonNullGraphType<StringGraphType>>
                    {
                        Name = "device"
                    }
                ),
                description: "total count by connected to this node.",
                resolve: context =>
                {
                    string device = context.GetArgument<string>("device");
                    return publisher.GetClientsCountByDevice(device);
                });
            Field<NonNullGraphType<ListGraphType<NonNullGraphType<AddressType>>>>(
                name: "clientsByDevice",
                arguments: new QueryArguments(
                    new QueryArgument<NonNullGraphType<StringGraphType>>
                    {
                        Name = "device"
                    }
                ),
                description: "clients connected to this node by device.",
                resolve: context =>
                {
                    string device = context.GetArgument<string>("device");
                    return publisher.GetClientsByDevice(device);
                });
            Field<NonNullGraphType<ListGraphType<StringGraphType>>>(
                name: "clientsByIp",
                arguments: new QueryArguments(
                    new QueryArgument<NonNullGraphType<IntGraphType>>
                    {
                        Name = "minimum"
                    }
                ),
                description: "clients connected to this node by Ip address.",
                resolve: context =>
                {
                    int minimum = context.GetArgument<int>("minimum");
                    return publisher.GetClientsByIp(minimum);
                });
            Field<NonNullGraphType<ListGraphType<StringGraphType>>>(
                name: "ipsByClient",
                description: "Ip addresses associate to each client.",
                resolve: context => publisher.GetIpsByClient());
        }
    }
}
