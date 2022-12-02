using GraphQL;
using GraphQL.Types;
using Nekoyume.Model.Item;

namespace NineChronicles.Headless.GraphTypes.States.Models.Item.Enum
{
    public class ItemSubTypeEnumType : EnumerationGraphType<ItemSubType>
    {
        public ItemSubTypeEnumType()
        {
            this.AddDeprecatedNames(StringExtensions.ToPascalCase);
        }
    }
}
