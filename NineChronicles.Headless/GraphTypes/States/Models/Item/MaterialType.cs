using GraphQL.Types;
using Libplanet.Explorer.GraphTypes;
using Nekoyume.Model.Item;

namespace NineChronicles.Headless.GraphTypes.States.Models.Item
{
    public class MaterialType : ItemBaseType<Material>
    {
        public MaterialType()
        {
            Field<NonNullGraphType<ByteStringType>>(
                nameof(Material.ItemId),
                resolve: context => context.Source.itemBase.ItemId.ToByteArray());
        }
    }
}
