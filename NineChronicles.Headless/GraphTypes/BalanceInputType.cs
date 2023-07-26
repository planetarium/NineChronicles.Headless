using System.Collections.Generic;
using GraphQL.Types;
using Libplanet.Crypto;
using Libplanet.Explorer.GraphTypes;

namespace NineChronicles.Headless.GraphTypes
{
    public class BalanceInputType : InputObjectGraphType<(
        Address balanceAddr,
        SimplifyFungibleAssetValueInputType valueTuple)>
    {
        public BalanceInputType()
        {
            Name = "BalanceInput";

            Field<AddressType>(
                name: "balanceAddr",
                description: "Balance Address."
            );

            Field<SimplifyFungibleAssetValueInputType>(
                name: "value",
                description: "Fungible asset value ticker and amount."
            );
        }

        public override object ParseDictionary(IDictionary<string, object?> value)
        {
            var addr = (Address)value["balanceAddr"]!;
            var favTuple = ((string currencyTicker, string value))value["value"]!;
            return (addr, favTuple);
        }
    }
}
