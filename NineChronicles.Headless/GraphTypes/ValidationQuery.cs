using System;
using System.Text.Json;
using System.Threading;
using GraphQL;
using GraphQL.Types;
using Libplanet.Crypto;
using Serilog;

namespace NineChronicles.Headless.GraphTypes
{
    public class ValidationQuery : ObjectGraphType
    {
        public ValidationQuery(StandaloneContext standaloneContext)
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
                        const int retryInterval = 1000;
                        const int grace = 100 * 1000;
                        while (standaloneContext.BlockChain is null)
                        {
                            Log.Debug(
                                "Blockchain instance is null. Sleep {interval}ms...",
                                retryInterval);
                            Thread.Sleep(retryInterval);
                            timeSpent += retryInterval;

                            if (timeSpent < grace)
                            {
                                continue;
                            }

                            var msg = $"Blockchain instance is not initialized until {grace}ms.";
                            Log.Debug(msg);
                            throw new NullReferenceException(msg);
                        }
                        
                        Log.Debug("Time until blockchain online: {time}ms", timeSpent);
                        
                        var remoteIndex = JsonDocument.Parse(raw).RootElement.GetProperty("Index").GetInt32();
                        Log.Debug("Remote: {index1}, Local: {index2}",
                            remoteIndex, standaloneContext.BlockChain.Tip?.Index ?? -1);
                        var ret = remoteIndex > (standaloneContext.BlockChain.Tip?.Index ?? -1);
                        return ret;
                    }
                    catch (JsonException je)
                    {
                        Log.Warning(je, "Given metadata is invalid. (raw: {raw})", raw);
                        return false;
                    }
                    catch (Exception e)
                    {
                        Log.Warning(
                            e,
                            "Unexpected exception occurred. (raw: {raw}) {e}",
                            raw, e);
                        throw;
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
