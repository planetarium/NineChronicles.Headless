using System.Collections.Generic;
using System.Numerics;
using GraphQL.Types;
using Libplanet.Assets;

namespace NineChronicles.Headless.GraphTypes
{
    public class SimplifyFungibleAssetValueInputType : InputObjectGraphType<FungibleAssetValue>
    {
        public SimplifyFungibleAssetValueInputType()
        {
            Name = "SimplifyFungibleAssetValueInput";
            Field<NonNullGraphType<SimplifyCurrencyInputType>>(
                name: "currency");
            Field<NonNullGraphType<BigIntGraphType>>(
                name: "majorUnit",
                description: "Major unit of currency quantity.");
            Field<NonNullGraphType<BigIntGraphType>>(
                name: "minorUnit",
                description: "Minor unit of currency quantity.");
        }

        public override object ParseDictionary(IDictionary<string, object?> value)
        {
            var currency = (Currency)value["currency"]!;
            var majorUnit = (BigInteger)value["majorUnit"]!;
            var minorUnit = (BigInteger)value["minorUnit"]!;
            return new FungibleAssetValue(currency, majorUnit, minorUnit);
        }
    }
}
