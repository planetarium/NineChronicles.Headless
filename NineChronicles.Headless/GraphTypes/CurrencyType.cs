using GraphQL.Types;
using Libplanet.Assets;
using Libplanet.Explorer.GraphTypes;

namespace NineChronicles.Headless.GraphTypes
{
    public class CurrencyType : ObjectGraphType<Currency>
    {
        public CurrencyType()
        {
            Field<NonNullGraphType<StringGraphType>>(nameof(Currency.Ticker))
                .Resolve(context => context.Source.Ticker);
            Field<NonNullGraphType<ByteGraphType>>(nameof(Currency.DecimalPlaces))
                .Resolve(context => context.Source.DecimalPlaces);
            Field<ListGraphType<AddressType>>(nameof(Currency.Minters))
                .Resolve(context => context.Source.Minters);
        }
    }
}
