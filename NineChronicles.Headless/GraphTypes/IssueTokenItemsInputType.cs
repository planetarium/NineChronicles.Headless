using System.Collections.Generic;
using GraphQL.Types;

namespace NineChronicles.Headless.GraphTypes.Input
{
    public class IssueTokenItemsInputType : InputObjectGraphType<(int itemId, int count, bool tradable)>
    {
        public IssueTokenItemsInputType()
        {
            Name = "IssueTokenItemsInputType";

            Field<NonNullGraphType<IntGraphType>>(
                name: "itemId",
                description: "item ID");

            Field<NonNullGraphType<IntGraphType>>(
                name: "count",
                description: "Count");

            Field<NonNullGraphType<BooleanGraphType>>(
                name: "tradable",
                description: "item can be tradable");
        }

        public override object ParseDictionary(IDictionary<string, object?> value)
        {
            var itemId = (int)value["itemId"]!;
            var count = (int)value["count"]!;
            var tradable = (bool)value["tradable"]!;
            return (itemId, count, tradable);
        }
    }
}
