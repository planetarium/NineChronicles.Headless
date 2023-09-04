using System.Collections.Generic;
using GraphQL.Types;

namespace NineChronicles.Headless.GraphTypes
{
    public class MultiAccountGraphType : ObjectGraphType<MultiAccountGraphType.MultiAccountInfo>
    {
        public class MultiAccountInfo
        {
            public string Key { get; set; } = null!;
            public List<string> Values { get; set; } = null!;
            public int Count { get; set; }
        }

        public MultiAccountGraphType()
        {
            Field(x => x.Key);
            Field<ListGraphType<StringGraphType>>("values", resolve: context => context.Source.Values);
            Field<IntGraphType>("count", resolve: context => context.Source.Values.Count);
        }
    }
}

