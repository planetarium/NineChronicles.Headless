using System.Collections.Generic;
using System.Numerics;
using GraphQL.Types;
using Libplanet;
using Libplanet.Assets;
using Libplanet.Explorer.GraphTypes;

namespace NineChronicles.Headless.GraphTypes
{
    public class GarageAddressAndFungibleAssetValueInputType :
        InputObjectGraphType<(Address balanceAddr, GarageFungibleAssetValueInputType fav)>
    {
        public GarageAddressAndFungibleAssetValueInputType()
        {
            Name = "GarageAddressAndFungibleAssetValueInput";

            Field<AddressType>(
                name: "balanceAddr",
                description: "Balance Address."
            );

            Field<GarageFungibleAssetValueInputType>(
                name: "fungibleAssetValue",
                description: "Fungible asset value ticker and amount."
            );
        }

        public override object ParseDictionary(IDictionary<string, object?> value)
        {
            var addr = (Address)value["balanceAddr"]!;
            var fav = ((CurrencyEnum, BigInteger, BigInteger))value["fungibleAssetValue"]!;
            return (addr, fav);
        }
    }
}
