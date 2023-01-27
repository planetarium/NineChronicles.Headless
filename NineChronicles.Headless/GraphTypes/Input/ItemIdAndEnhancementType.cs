using System.Collections.Generic;
using GraphQL.Types;

namespace NineChronicles.Headless.GraphTypes.Input
{
    public class ItemIdAndEnhancementType :
        InputObjectGraphType<(int equipmentId, int enhancement)>
    {
        public ItemIdAndEnhancementType()
        {
            Field<NonNullGraphType<IntGraphType>>(
                name: "itemId",
                description: "Item ID.");
            Field<IntGraphType>(
                name: "enhancement",
                description: "The enhancement level of the equipment. If not specified, it will be 0.");
        }

        public override object ParseDictionary(IDictionary<string, object?> value)
        {
            return (
                (int)value["itemId"]!,
                value.ContainsKey("enhancement")
                    ? (int)value["enhancement"]!
                    : 0);
        }
    }
}
