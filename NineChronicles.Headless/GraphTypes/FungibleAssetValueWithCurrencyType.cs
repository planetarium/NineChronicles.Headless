using GraphQL;
using GraphQL.Types;
using Libplanet.Assets;

namespace NineChronicles.Headless.GraphTypes
{
    public class FungibleAssetValueWithCurrencyType : ObjectGraphType<FungibleAssetValue>
    {
        public FungibleAssetValueWithCurrencyType()
        {
            Field<NonNullGraphType<CurrencyType>>(nameof(FungibleAssetValue.Currency))
                .Resolve(context => context.Source.Currency);
            Field<NonNullGraphType<StringGraphType>>("quantity")
                // sic; not minorUnit.  This was a typo, but fixing it would be a breaking change:
                .Argument<bool?>("minerUnit", true, arg => arg.DefaultValue = false)
                .Resolve(context =>
                {
                    var minorUnit = context.GetArgument<bool>("minerUnit");
                    return context.Source.GetQuantityString(minorUnit);
                });
        }
    }
}
