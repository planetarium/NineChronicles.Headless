using System.Collections.Generic;
using GraphQL;
using GraphQL.Types;
using Lib9c;
using Libplanet;
using Libplanet.Assets;

namespace NineChronicles.Headless.GraphTypes
{
    // FIXME: To StringGraphType.
    public class SimplifyCurrencyInputType : InputObjectGraphType<Currency>
    {
        /// <summary>
        /// This NCG definition is for mainnet.
        /// </summary>
#pragma warning disable CS0618
        public static readonly Currency NCG = Currency.Legacy(
            "NCG",
            2,
            new Address("0x47D082a115c63E7b58B1532d20E631538eaFADde"));
#pragma warning restore CS0618

        public SimplifyCurrencyInputType()
        {
            Name = "SimplifyCurrencyInput";
            Description = "Generate currency from ticker. If ticker is NCG, it will return mainnet NCG.\n" +
                          " See also: Currency? Currencies.GetCurrency(string ticker) " +
                          "https://github.com/planetarium/lib9c/blob/main/Lib9c/Currencies.cs";
            Field<NonNullGraphType<StringGraphType>>(
                name: "ticker",
                description: "Ticker.");
        }

        public override object ParseDictionary(IDictionary<string, object?> value)
        {
            var ticker = (string)value["ticker"]!;
            var currency = Currencies.GetCurrency(ticker);
            if (currency is not null)
            {
                return currency;
            }

            if (ticker == "NCG")
            {
                return NCG;
            }

            throw new ExecutionError($"Invalid ticker: {ticker}");
        }
    }
}
