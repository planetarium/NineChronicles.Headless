using System.Collections.Generic;
using GraphQL.Types;

namespace NineChronicles.Headless.GraphTypes
{
    public class RewardSetItemPairType : ObjectGraphType<KeyValuePair<int, uint>>
    {
        public RewardSetItemPairType()
        {
            Field<NonNullGraphType<GuidGraphType>>(
                "Id",
                resolve: context => context.Source.Key
            );
            Field<NonNullGraphType<ListGraphType>>(
                "Count",
                resolve: context => context.Source.Value
            );
        }
    }
}
