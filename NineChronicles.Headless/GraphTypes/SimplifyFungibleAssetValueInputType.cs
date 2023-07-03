using System.Collections.Generic;
using GraphQL;
using GraphQL.Types;

namespace NineChronicles.Headless.GraphTypes;

public class SimplifyFungibleAssetValueInputType :
    InputObjectGraphType<(string currencyTicker, string value)>
{
    public SimplifyFungibleAssetValueInputType()
    {
        Name = "SimplifyFungibleAssetValueInput";
        Description = "A fungible asset value ticker and amount." +
                      "You can specify either currencyEnum or currencyTicker.";

        SimplifyCurrencyInputType.SetFields(this);
        Field<NonNullGraphType<StringGraphType>>(
            name: "value",
            description: "A numeric string to parse.  Can consist of digits, " +
                         "plus (+), minus (-), and decimal separator (.)." +
                         " <see cref=\"FungibleAssetValue.Parse(Currency, string)\" />");
    }

    public override object ParseDictionary(IDictionary<string, object?> value)
    {
        var value2 = (string)value["value"]!;
        var currencyTicker = SimplifyCurrencyInputType.Parse(value);
        return (currencyTicker, value: value2);
    }
}
