using System;
using System.Collections.Generic;
using System.Linq;
using GraphQL.Types;
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
        }
    }
}
