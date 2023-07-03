using System.Collections.Generic;
using GraphQL;
using GraphQL.Types;

namespace NineChronicles.Headless.GraphTypes;

public class SimplifyCurrencyInputType : InputObjectGraphType<string>
{
    public static void SetFields<T>(ComplexGraphType<T> graphType)
    {
        graphType.Field<CurrencyEnumType>(
            name: "currencyEnum",
            description: "A currency type to be loaded.");

        graphType.Field<StringGraphType>(
            name: "currencyTicker",
            description: "A currency ticker to be loaded.");
    }

    public static string Parse(IDictionary<string, object?> value)
    {
        if (value.TryGetValue("currencyEnum", out var currencyEnum))
        {
            if (value.ContainsKey("currencyTicker"))
            {
                throw new ExecutionError("currencyEnum and currencyTicker cannot be specified at the same time.");
            }

            return ((CurrencyEnum)currencyEnum!).ToString();
        }

        if (value.TryGetValue("currencyTicker", out var currencyTicker))
        {
            return (string)currencyTicker!;
        }

        throw new ExecutionError("currencyEnum or currencyTicker must be specified.");
    }

    public SimplifyCurrencyInputType()
    {
        Name = "SimplifyCurrencyInput";
        Description = "A currency ticker to be loaded. Use either currencyEnum or currencyTicker.";
        SetFields(this);
    }

    public override object ParseDictionary(IDictionary<string, object?> value)
    {
        return Parse(value);
    }
}
