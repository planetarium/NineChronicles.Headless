using System.Collections.Generic;
using GraphQL.Types;
using Libplanet.Common;
using System.Security.Cryptography;

namespace NineChronicles.Headless.GraphTypes.Input
{
    public class FungibleIdAndCountInputType :
        InputObjectGraphType<(HashDigest<SHA256> fungibleId, int count)>
    {
        public FungibleIdAndCountInputType()
        {
            Name = "FungibleIdAndCountInput";

            Field<NonNullGraphType<StringGraphType>>(
                name: "fungibleId",
                description: "Fungible ID");

            Field<NonNullGraphType<IntGraphType>>(
                name: "count",
                description: "Count");
        }

        public override object ParseDictionary(IDictionary<string, object?> value)
        {
            var hexDigest = (string)value["fungibleId"]!;
            var fungibleId = HashDigest<SHA256>.FromString(hexDigest);
            var count = (int)value["count"]!;
            return (fungibleId, count);
        }
    }
}
