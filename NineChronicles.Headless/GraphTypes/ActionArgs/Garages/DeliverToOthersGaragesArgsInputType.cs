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
    public class DeliverToOthersGaragesArgsInputType : InputObjectGraphType<(
        Address recipientAgentAddr,
        IEnumerable<FungibleAssetValue>? fungibleAssetValues,
        IEnumerable<(HashDigest<SHA256> fungibleId, int count)>? fungibleIdAndCounts)>
    {
        public DeliverToOthersGaragesArgsInputType()
        {
            Name = "DeliverToOthersGaragesArgsInput";

            Field<NonNullGraphType<AddressType>>(
                name: "recipientAgentAddr",
                description: "Recipient agent address.");

            Field<ListGraphType<NonNullGraphType<GarageFungibleAssetValueInputType>>>(
                name: "fungibleAssetValues",
                description: "Array of currency ticker and quantity.");

            Field<ListGraphType<NonNullGraphType<FungibleIdAndCountInputType>>>(
                name: "fungibleIdAndCounts",
                description: "Array of fungible ID and count.");
        }

        public override object ParseDictionary(IDictionary<string, object?> value)
        {
            var recipientAgentAddr = (Address)value["recipientAgentAddr"]!;
            IEnumerable<FungibleAssetValue>? fungibleAssetValues = null;
            if (value["fungibleAssetValues"] is object[] fungibleAssetValueObjects)
            {
                fungibleAssetValues = fungibleAssetValueObjects
                    .OfType<FungibleAssetValue>();
            }

            IEnumerable<(HashDigest<SHA256> fungibleId, int count)>? fungibleIdAndCounts = null;
            if (value["fungibleIdAndCounts"] is object[] fungibleIdAndCountObjects)
            {
                fungibleIdAndCounts = fungibleIdAndCountObjects
                    .OfType<(HashDigest<SHA256>, uint)>()
                    .Select(tuple => (tuple.Item1, (int)tuple.Item2));
            }

            return (recipientAgentAddr, fungibleAssetValues, fungibleIdAndCounts);
        }
    }
}
