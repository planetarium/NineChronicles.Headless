using System.Collections.Generic;
using GraphQL.Types;
using Libplanet;
using Libplanet.Assets;
using Libplanet.Explorer.GraphTypes;

namespace NineChronicles.Headless.GraphTypes
{
    public class BalanceAddressAndSimplifyFungibleAssetValueTupleInputType :
        InputObjectGraphType<(Address balanceAddr, FungibleAssetValue value)>
    {
        public BalanceAddressAndSimplifyFungibleAssetValueTupleInputType()
        {
            Name = "BalanceAddressAndFungibleAssetValueInput";

            Field<AddressType>(
                name: "balanceAddr",
                description: "Balance address");

            Field<SimplifyFungibleAssetValueInputType>(
                name: "value",
                description: "Simplify fungible asset value");
        }

        public override object ParseDictionary(IDictionary<string, object?> value)
        {
            var balanceAddr = (Address)value["balanceAddr"]!;
            var assetValue = (FungibleAssetValue)value["value"]!;
            return (balanceAddr, assetValue);
        }
    }
}
