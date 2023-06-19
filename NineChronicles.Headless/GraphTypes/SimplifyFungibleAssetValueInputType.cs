using System.Collections.Generic;
using GraphQL;
using GraphQL.Types;

namespace NineChronicles.Headless.GraphTypes
{
    public class SimplifyFungibleAssetValueInputType :
        InputObjectGraphType<(string currencyTicker, string value)>
    {
        public SimplifyFungibleAssetValueInputType()
        {
            Name = "SimplifyFungibleAssetValueInput";
            Description = "A fungible asset value ticker and amount." +
                          "You can specify either currencyEnum or currencyTicker.";

            Field<CurrencyEnumType>(
                name: "currencyEnum",
                description: "A currency type to be loaded.");

            Field<StringGraphType>(
                name: "currencyTicker",
                description: "A currency ticker to be loaded.");

            Field<NonNullGraphType<StringGraphType>>(
                name: "value",
                description: "A numeric string to parse.  Can consist of digits, " +
                             "plus (+), minus (-), and decimal separator (.)." +
                             " <see cref=\"FungibleAssetValue.Parse(Currency, string)\" />");
        }

        public override object ParseDictionary(IDictionary<string, object?> value)
        {
            var value2 = (string)value["value"]!;
            if (value.TryGetValue("currencyEnum", out var currencyEnum))
            {
                if (value.ContainsKey("currencyTicker"))
                {
                    throw new ExecutionError("currencyEnum and currencyTicker cannot be specified at the same time.");    
                }
                
                var currencyTicker = ((CurrencyEnum)currencyEnum!).ToString();
                return (currencyTicker, value: value2);
            }
            else if (value.TryGetValue("currencyTicker", out var currencyTicker))
            {
                return ((string)currencyTicker!, value: value2);
            }
            else
            {
                throw new ExecutionError("currencyEnum or currencyTicker must be specified.");
            }
        }
    }
}
