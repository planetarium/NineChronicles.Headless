using GraphQL.Types;

namespace NineChronicles.Headless.GraphTypes.States
{
    public enum ShopSortingEnum
    {
        asc,
        desc,
    }

    public class ShopSortingEnumType : EnumerationGraphType<ShopSortingEnum>
    {
    }
}
