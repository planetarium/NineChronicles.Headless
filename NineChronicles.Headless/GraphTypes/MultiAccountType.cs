using System.Collections.Generic;
using GraphQL.Types;

namespace NineChronicles.Headless.GraphTypes
{
    public class MultiAccountInfoGraphType : ObjectGraphType<MultiAccountInfoGraphType.MultiAccountInfo>
    {
        public class MultiAccountInfo
        {
            public string Key { get; set; } = null!;
            public List<string> Ips { get; set; } = null!;
            public List<string> Agents { get; set; } = null!;
            public int IpsCount { get; set; }
            public int AgentsCount { get; set; }
        }

        public MultiAccountInfoGraphType()
        {
            Field("key", x => x.Key);
            Field<ListGraphType<StringGraphType>>("ips", resolve: context => context.Source.Ips);
            Field<ListGraphType<StringGraphType>>("agents", resolve: context => context.Source.Agents);
            Field<IntGraphType>("ipsCount", resolve: context => context.Source.Ips.Count);
            Field<IntGraphType>("agentsCount", resolve: context => context.Source.Agents.Count);
        }
    }
}
