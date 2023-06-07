using System.Collections.Generic;
using GraphQL.Types;
using Libplanet;
using System.Security.Cryptography;

namespace NineChronicles.Headless.GraphTypes.Input
{
    public class FungibleIdAndCountInputType :
        InputObjectGraphType<(HashDigest<SHA256> fungibleId, uint count)>
    {
        public FungibleIdAndCountInputType()
        {
            Name = "FungibleIdAndCountInput";

            Field<HashDigestInputType<SHA256>>(
                name: "fungibleId",
                description: "Fungible ID");

            Field<UIntGraphType>(
                name: "count",
                description: "Count");
        }

        public override object ParseDictionary(IDictionary<string, object?> value)
        {
            var fungibleId = (HashDigest<SHA256>)value["fungibleId"]!;
            var count = (uint)value["count"]!;
            return (fungibleId, count);
        }
    }
}
