using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Numerics;
using GraphQL.Types;
using Libplanet;
using Libplanet.Assets;
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
                        minters = minters.Add((Address) rawMinter);
                    }
                }
            }
            var currency = new Currency((string)value["ticker"]!, (byte)value["decimalPlaces"]!, minters: minters);
            return currency * (BigInteger)value["quantity"]!;
        }
    }
}
