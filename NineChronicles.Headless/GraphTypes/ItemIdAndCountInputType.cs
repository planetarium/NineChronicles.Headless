using System.Collections.Generic;
using GraphQL.Types;

namespace NineChronicles.Headless.GraphTypes.Input
{
    public class ItemIdAndCountInputType : InputObjectGraphType<(int itemId, int count)>
    {
        public ItemIdAndCountInputType()
        {
            Name = "ItemIdAndCountInput";

            Field<NonNullGraphType<IntGraphType>>(
                name: "itemId",
                description: "item ID");

            Field<NonNullGraphType<IntGraphType>>(
                name: "count",
                description: "Count");
        }

        public override object ParseDictionary(IDictionary<string, object?> value)
        {
            var itemId = (int)value["itemId"]!;
            var count = (int)value["count"]!;
            return (itemId, count);
        }
    }
}
