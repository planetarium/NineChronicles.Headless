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
            Field<NonNullGraphType<IntGraphType>>("totalCount")
                .Description("total count by connected to this node.")
                .Resolve(context => publisher.GetClients().Count);
            Field<NonNullGraphType<ListGraphType<AddressType>>>("clients")
                .Description("List of address connected to this node.")
                .Resolve(context => publisher.GetClients());
        }
    }
}
