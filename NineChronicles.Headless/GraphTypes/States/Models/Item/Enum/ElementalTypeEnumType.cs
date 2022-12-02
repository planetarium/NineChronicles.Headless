using GraphQL;
using GraphQL.Types;
using Nekoyume.Model.Elemental;

namespace NineChronicles.Headless.GraphTypes.States.Models.Item.Enum
{
    public class ElementalTypeEnumType : EnumerationGraphType<ElementalType>
    {
        public ElementalTypeEnumType()
        {
            this.AddDeprecatedNames(StringExtensions.ToPascalCase);
        }
    }
}
