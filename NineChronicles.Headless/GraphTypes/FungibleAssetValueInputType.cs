using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Numerics;
using GraphQL.Language.AST;
using GraphQL.Types;
using Libplanet.Crypto;
using Libplanet.Types.Assets;
using Libplanet.Explorer.GraphTypes;

namespace NineChronicles.Headless.GraphTypes
{
    public class FungibleAssetValueInputType : InputObjectGraphType<FungibleAssetValue>
    {
        public FungibleAssetValueInputType()
        {
            Field<NonNullGraphType<BigIntGraphType>>("quantity");
            Field<NonNullGraphType<StringGraphType>>("ticker");
            Field<NonNullGraphType<ByteGraphType>>("decimalPlaces");
            Field<ListGraphType<NonNullGraphType<AddressType>>>("minters");
        }

        public override object ParseDictionary(IDictionary<string, object?> value)
        {
            IImmutableSet<Address>? minters = null;
            if (value.ContainsKey("minters"))
            {
                var rawMinters = (object[])value["minters"]!;
                if (rawMinters.Any())
                {
                    minters = ImmutableHashSet<Address>.Empty;
                    foreach (var rawMinter in rawMinters)
                    {
                        minters = minters.Add((Address)rawMinter);
                    }
                }
            }
#pragma warning disable CS0618
            // Use of obsolete method Currency.Legacy(): https://github.com/planetarium/lib9c/discussions/1319
            var currency = Currency.Legacy((string)value["ticker"]!, (byte)value["decimalPlaces"]!, minters: minters);
            return currency * (BigInteger)value["quantity"]!;
#pragma warning restore CS0618
        }

        public override IValue? ToAST(object value)
        {
            if (value is FungibleAssetValue fav)
            {
                IValue mintersValue = new NullValue();
                if (fav.Currency.Minters is not null)
                {
                    var minters = new List<StringValue>();
                    minters.AddRange(fav.Currency.Minters.Select(minter => new StringValue(minter.ToString())));
                    mintersValue = new ListValue(minters);
                }
                return new ObjectValue(new List<ObjectField>
                {
                    new("quantity", new BigIntValue(fav.RawValue)),
                    new("ticker", new StringValue(fav.Currency.Ticker)),
                    new("decimalPlaces", new IntValue(fav.Currency.DecimalPlaces)),
                    new("minters", mintersValue),
                });
            }

            return null;
        }
    }
}
