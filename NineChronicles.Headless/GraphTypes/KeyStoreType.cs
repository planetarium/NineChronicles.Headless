using System;
using System.Linq;
using GraphQL;
using GraphQL.Types;
using Libplanet;
using Libplanet.Crypto;
using Libplanet.Explorer.GraphTypes;
using Libplanet.KeyStore;
using Org.BouncyCastle.Security;

namespace NineChronicles.Headless.GraphTypes
{
    public class KeyStoreType : ObjectGraphType<IKeyStore>
    {
        public KeyStoreType()
        {
            DeprecationReason = "Use `planet key` command instead.  https://www.npmjs.com/package/@planetarium/cli";

            Field<NonNullGraphType<ListGraphType<NonNullGraphType<ProtectedPrivateKeyType>>>>(
                name: "protectedPrivateKeys",
                resolve: context => context.Source.List().Select(t => t.Item2));

            // TODO: description을 적어야 합니다.
            Field<NonNullGraphType<ByteStringType>>(
                name: "decryptedPrivateKey",
                arguments: new QueryArguments(
                    new QueryArgument<NonNullGraphType<AddressType>> { Name = "address" },
                    new QueryArgument<NonNullGraphType<StringGraphType>> { Name = "passphrase" }),
                resolve: context =>
                {
                    var keyStore = context.Source;

                    var address = context.GetArgument<Address>("address");
                    var passphrase = context.GetArgument<string>("passphrase");

                    var protectedPrivateKeys = keyStore.List().Select(t => t.Item2);

                    try
                    {
                        var protectedPrivateKey = protectedPrivateKeys.Where(key => key.Address.Equals(address)).First();
                        var privateKey = protectedPrivateKey.Unprotect(passphrase);
                        return privateKey.ToByteArray();
                    }
                    catch (InvalidOperationException)
                    {
                        return null;
                    }
                }
            );

            Field<NonNullGraphType<PrivateKeyType>>(
                name: "privateKey",
                description: "An API to provide conversion to public-key, address.",
                arguments: new QueryArguments(
                    new QueryArgument<NonNullGraphType<ByteStringType>>
                    {
                        Name = "hex",
                        Description = "A representation of public-key with hexadecimal format."
                    }),
                resolve: context =>
                {
                    var privateKeyBytes = context.GetArgument<byte[]>("hex");
                    var privateKey = new PrivateKey(privateKeyBytes);
                    return privateKey;
                });
        }
    }
}
