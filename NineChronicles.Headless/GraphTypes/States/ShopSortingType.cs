using GraphQL.Types;

namespace NineChronicles.Headless.GraphTypes.States
{
    public enum ShopSortingEnum
    {
        Asc,
        Desc,
    }

    public class ShopSortingEnumType : EnumerationGraphType<ShopSortingEnum>
    {
    }
}
