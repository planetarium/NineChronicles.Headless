using GraphQL;
using GraphQL.Types;
using Libplanet.Types.Assets;

namespace NineChronicles.Headless.GraphTypes
{
    public class FungibleAssetValueWithCurrencyType : ObjectGraphType<FungibleAssetValue>
    {
        public FungibleAssetValueWithCurrencyType()
        {
            Field<NonNullGraphType<CurrencyType>>(
                nameof(FungibleAssetValue.Currency),
                resolve: context => context.Source.Currency
            );
            Field<NonNullGraphType<StringGraphType>>(
                name: "quantity",
                arguments: new QueryArguments(
                    new QueryArgument<BooleanGraphType>
                    {
                        Name = "minerUnit",
                        DefaultValue = false
                    }
                ),
                resolve: context =>
                {
                    var minorUnit = context.GetArgument<bool>("minerUnit");
                    return context.Source.GetQuantityString(minorUnit);
                });
        }
    }
}
