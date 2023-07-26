using GraphQL.Types;
using Libplanet.Types.Assets;
using Libplanet.Explorer.GraphTypes;

namespace NineChronicles.Headless.GraphTypes
{
    public class CurrencyType : ObjectGraphType<Currency>
    {
        public CurrencyType()
        {
            Field<NonNullGraphType<StringGraphType>>(
                nameof(Currency.Ticker),
                resolve: context => context.Source.Ticker
            );
            Field<NonNullGraphType<ByteGraphType>>(
                nameof(Currency.DecimalPlaces),
                resolve: context => context.Source.DecimalPlaces
            );
            Field<ListGraphType<AddressType>>(
                nameof(Currency.Minters),
                resolve: context => context.Source.Minters
            );
        }
    }
}
