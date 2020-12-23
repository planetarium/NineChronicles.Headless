using GraphQL.Types;
using Nekoyume.Model.Item;

namespace NineChronicles.Headless.GraphTypes.States.Models.Item
{
    public class CostumeType : ItemBaseType<Costume>
    {
        public CostumeType()
        {
            Field<NonNullGraphType<GuidGraphType>>(nameof(Costume.ItemId));
            Field<NonNullGraphType<BooleanGraphType>>(nameof(Costume.Equipped));
        }
    }
}
