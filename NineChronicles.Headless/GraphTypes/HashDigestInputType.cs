using System.Collections.Generic;
using System.Security.Cryptography;
using GraphQL.Types;
using Libplanet;

namespace NineChronicles.Headless.GraphTypes
{
    // FIXME: To StringGraphType
    public class HashDigestInputType<T> : InputObjectGraphType<HashDigest<T>>
        where T : HashAlgorithm
    {
        public HashDigestInputType()
        {
            Name = $"HashDigestInput_{typeof(T).Name}";
            Field<StringGraphType>(
                name: "value",
                description: "Hash digest hex string");
        }

        public override object ParseDictionary(IDictionary<string, object?> value)
        {
            var hashValue = (string)value["value"]!;
            return HashDigest<T>.FromString(hashValue);
        }
    }
}
