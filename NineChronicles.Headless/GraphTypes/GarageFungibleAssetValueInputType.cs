using System.Collections.Generic;
using System.Numerics;
using GraphQL.Types;

namespace NineChronicles.Headless.GraphTypes
{
    public class GarageFungibleAssetValueInputType :
        InputObjectGraphType<(CurrencyEnum currency, BigInteger majorUnit, BigInteger minorUnit)>
    {
        public GarageFungibleAssetValueInputType()
        {
            Name = "GarageFungibleAssetValueInput";

            Field<CurrencyEnumType>(
                name: "currency",
                description: "A currency type to be loaded.");

            Field<NonNullGraphType<BigIntGraphType>>(
                name: "majorUnit",
                description: "Major unit of currency quantity.");

            Field<NonNullGraphType<BigIntGraphType>>(
                name: "minorUnit",
                description: "Minor unit of currency quantity.");
        }

        public override object ParseDictionary(IDictionary<string, object?> value)
        {
            var currency = (CurrencyEnum)value["currency"]!;
            var majorUnit = (BigInteger)value["majorUnit"]!;
            var minorUnit = (BigInteger)value["minorUnit"]!;
            return (currency, majorUnit, minorUnit);
        }
    }
}
