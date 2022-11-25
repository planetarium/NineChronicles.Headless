using System;
using System.Text.Json;
using System.Threading;
using GraphQL;
using GraphQL.Types;
using Libplanet.Blockchain;
using Libplanet.Crypto;
using Libplanet.Explorer.GraphTypes;
using Serilog;
using NCAction = Libplanet.Action.PolymorphicAction<Nekoyume.Action.ActionBase>;

namespace NineChronicles.Headless.GraphTypes
{
    public class ValidationQuery : ObjectGraphType
    {
        public ValidationQuery(StandaloneContext standaloneContext, BlockChain<NCAction> blockChain)
        {
            Field<NonNullGraphType<BooleanGraphType>>(
                name: "metadata",
                arguments: new QueryArguments(
                    new QueryArgument<NonNullGraphType<StringGraphType>>
                    {
                        Name = "raw",
                        Description = "The raw value of json metadata."
                    }),
                resolve: context =>
                {
                    var raw = context.GetArgument<string>("raw");
                    try
                    {
                        Log.Debug($"Validating received raw: {raw}");
                        // FIXME: Thread.Sleep is temporary. Should be removed.
                        var timeSpent = 0;
                        Log.Debug("Time until blockchain online: {time}ms", timeSpent);

                        var remoteIndex = JsonDocument.Parse(raw).RootElement.GetProperty("Index").GetInt32();
                        Log.Debug("Remote: {index1}, Local: {index2}",
                            remoteIndex, blockChain.Tip.Index);
                        var ret = remoteIndex > blockChain.Tip.Index;
                        return ret;
                    }
                    catch (JsonException je)
                    {
                        Log.Warning(je, "Given metadata is invalid. (raw: {raw})", raw);
                        return false;
                    }
                    catch (Exception e)
                    {
                        var msg = $"Exception occurred while validating metadata. (raw: {raw})";
                        Log.Warning(e, msg + " {e}", e);
                        throw new ExecutionError(msg, e);
                    }
                }
            );

            Field<NonNullGraphType<BooleanGraphType>>(
                name: "privateKey",
                arguments: new QueryArguments(
                    new QueryArgument<NonNullGraphType<ByteStringType>>
                    {
                        Name = "hex",
                        Description = "The raw value of private-key, presented as hexadecimal."
                    }),
                resolve: context =>
                {
                    try
                    {
                        var rawPrivateKey = context.GetArgument<byte[]>("hex");
                        var _ = new PrivateKey(rawPrivateKey);
                        return true;
                    }
                    catch (ArgumentException)
                    {
                        return false;
                    }
                }
            );

            Field<NonNullGraphType<BooleanGraphType>>(
                name: "publicKey",
                arguments: new QueryArguments(
                    new QueryArgument<NonNullGraphType<ByteStringType>>
                    {
                        Name = "hex",
                        Description = "The raw value of public-key, presented as hexadecimal."
                    }),
                resolve: context =>
                {
                    try
                    {
                        var rawPublicKey = context.GetArgument<byte[]>("hex");
                        var _ = new PublicKey(rawPublicKey);
                        return true;
                    }
                    catch (ArgumentException)
                    {
                        return false;
                    }
                    catch (FormatException)
                    {
                        return false;
                    }
                }
            );
        }
    }
}
