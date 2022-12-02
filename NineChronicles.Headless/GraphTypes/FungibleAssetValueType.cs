using GraphQL.Types;
using Libplanet.Assets;

namespace NineChronicles.Headless.GraphTypes
{
    public class FungibleAssetValueType : ObjectGraphType<FungibleAssetValue>
    {
        public FungibleAssetValueType()
        {
            Field<NonNullGraphType<StringGraphType>>(nameof(FungibleAssetValue.Currency))
                .Resolve(context => context.Source.Currency.Ticker);
            Field<NonNullGraphType<StringGraphType>>("quantity")
                .Resolve(context => context.Source.GetQuantityString(true));
        }
    }
}
