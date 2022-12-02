using System;
using System.Linq;
using GraphQL;
using GraphQL.Types;
using Libplanet;
using Libplanet.Crypto;
using Libplanet.Explorer.GraphTypes;
using Libplanet.KeyStore;

namespace NineChronicles.Headless.GraphTypes
{
    public class KeyStoreMutation : ObjectGraphType<IKeyStore>
    {
        public KeyStoreMutation()
        {
            DeprecationReason = "Use `planet key` command instead.  https://www.npmjs.com/package/@planetarium/cli";

            Field<NonNullGraphType<PrivateKeyType>>("createPrivateKey")
                .Argument<string>("passphrase", false)
                .Argument<ByteStringType>("privateKey")
                .Resolve(context =>
                {
                    var keyStore = context.Source;
                    var passphrase = context.GetArgument<string>("passphrase");
                    var privateKeyBytes = context.GetArgument<byte[]>("privateKey");

                    var privateKey = privateKeyBytes is null ? new PrivateKey() : new PrivateKey(privateKeyBytes);
                    var protectedPrivateKey = ProtectedPrivateKey.Protect(privateKey, passphrase);

                    keyStore.Add(protectedPrivateKey);
                    return privateKey;
                });

            Field<NonNullGraphType<ProtectedPrivateKeyType>>("revokePrivateKey")
                .Argument<Address>("address", false)
                .Resolve(context =>
                {
                    var keyStore = context.Source;
                    var address = context.GetArgument<Address>("address");

                    keyStore.List()
                        .First(guidAndKey => guidAndKey.Item2.Address.Equals(address))
                        .Deconstruct(out Guid guid, out ProtectedPrivateKey protectedPrivateKey);

                    keyStore.Remove(guid);
                    return protectedPrivateKey;
                });
        }
    }
}
