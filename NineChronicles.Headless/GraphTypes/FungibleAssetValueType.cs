using GraphQL.Types;
using Libplanet.Types.Assets;

namespace NineChronicles.Headless.GraphTypes
{
    public class FungibleAssetValueType : ObjectGraphType<FungibleAssetValue>
    {
        public FungibleAssetValueType()
        {
            Field<NonNullGraphType<StringGraphType>>(
                nameof(FungibleAssetValue.Currency),
                resolve: context => context.Source.Currency.Ticker);
            Field<NonNullGraphType<StringGraphType>>(
                name: "quantity",
                resolve: context => context.Source.GetQuantityString(true));
        }
    }
}
