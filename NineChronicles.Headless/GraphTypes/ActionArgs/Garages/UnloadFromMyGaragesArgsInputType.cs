using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using GraphQL.Types;
using Libplanet;
using Libplanet.Assets;
using Libplanet.Explorer.GraphTypes;
using NineChronicles.Headless.GraphTypes.Input;

namespace NineChronicles.Headless.GraphTypes.ActionArgs.Garages
{
    public class UnloadFromMyGaragesArgsInputType : InputObjectGraphType<(
        IEnumerable<(Address balanceAddr, FungibleAssetValue value)>? fungibleAssetValues,
        Address? inventoryAddr,
        IEnumerable<(HashDigest<SHA256> fungibleId, int count)>? fungibleIdAndCounts)>
    {
        public UnloadFromMyGaragesArgsInputType()
        {
            Name = "UnloadFromMyGaragesArgsInput";

            Field<ListGraphType<NonNullGraphType<BalanceAddressAndSimplifyFungibleAssetValueTupleInputType>>>(
                name: "fungibleAssetValues",
                description: "Array of balance address and currency ticker and quantity.");

            Field<AddressType>(
                name: "inventoryAddr",
                description: "Inventory address");

            Field<ListGraphType<NonNullGraphType<FungibleIdAndCountInputType>>>(
                name: "fungibleIdAndCounts",
                description: "Array of fungible ID and count");
        }

        public override object ParseDictionary(IDictionary<string, object?> value)
        {
            IEnumerable<(Address balanceAddr, FungibleAssetValue value)>? fungibleAssetValues = null;
            if (value["fungibleAssetValues"] is object[] fungibleAssetValueObjects)
            {
                fungibleAssetValues = fungibleAssetValueObjects
                    .OfType<(Address, FungibleAssetValue)>()
                    .Select(tuple => (tuple.Item1, tuple.Item2));
            }

            var inventoryAddr = (Address?)value["inventoryAddr"];
            IEnumerable<(HashDigest<SHA256> fungibleId, int count)>? fungibleIdAndCounts = null;
            if (value["fungibleIdAndCounts"] is object[] fungibleIdAndCountObjects)
            {
                fungibleIdAndCounts = fungibleIdAndCountObjects
                    .OfType<(HashDigest<SHA256>, uint)>()
                    .Select(tuple => (tuple.Item1, (int)tuple.Item2));
            }

            return (fungibleAssetValues, inventoryAddr, fungibleIdAndCounts);
        }
    }
}
