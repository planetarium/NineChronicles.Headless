using System.Collections.Generic;
using System.Security.Cryptography;
using GraphQL;
using GraphQL.Types;
using Libplanet;
using Libplanet.Assets;
using Libplanet.Explorer.GraphTypes;
using Nekoyume.Action;
using Nekoyume.Action.Garages;
using NineChronicles.Headless.GraphTypes.ActionArgs.Garages;

namespace NineChronicles.Headless.GraphTypes;

public partial class ActionQuery
{
    private void RegisterGarages()
    {
        Field<NonNullGraphType<ByteStringType>>(
            "LoadIntoMyGarages",
            arguments: new QueryArguments(
                new QueryArgument<LoadIntoMyGaragesArgsInputType>
                {
                    Name = "args",
                    Description = "The arguments of the \"LoadIntoMyGarages\" action constructor.",
                }
            ),
            resolve: context =>
            {
                var args = context.GetArgument<(
                    IEnumerable<(Address balanceAddr, FungibleAssetValue value)>? fungibleAssetValues,
                    Address? inventoryAddr,
                    IEnumerable<(HashDigest<SHA256> fungibleId, int count)>? fungibleIdAndCounts
                    )>("args");
                ActionBase action = new LoadIntoMyGarages(
                    args.fungibleAssetValues,
                    args.inventoryAddr,
                    args.fungibleIdAndCounts);
                return Encode(context, action);
            }
        );

        Field<NonNullGraphType<ByteStringType>>(
            "deliverToOthersGarages",
            arguments: new QueryArguments(
                new QueryArgument<DeliverToOthersGaragesArgsInputType>
                {
                    Name = "args",
                    Description = "The arguments of the \"DeliverToOthersGarages\" action constructor.",
                }
            ),
            resolve: context =>
            {
                var args = context.GetArgument<(
                    Address recipientAgentAddr,
                    IEnumerable<FungibleAssetValue>? fungibleAssetValues,
                    IEnumerable<(HashDigest<SHA256> fungibleId, int count)>? fungibleIdAndCounts
                    )>("args");
                ActionBase action = new DeliverToOthersGarages(
                    args.recipientAgentAddr,
                    args.fungibleAssetValues,
                    args.fungibleIdAndCounts);
                return Encode(context, action);
            }
        );
        
        Field<NonNullGraphType<ByteStringType>>(
            "unloadFromMyGarages",
            arguments: new QueryArguments(
                new QueryArgument<UnloadFromMyGaragesArgsInputType>
                {
                    Name = "args",
                    Description = "The arguments of the \"UnloadFromMyGarages\" action constructor.",
                }
            ),
            resolve: context =>
            {
                var args = context.GetArgument<(
                    IEnumerable<(Address balanceAddr, FungibleAssetValue value)>? fungibleAssetValues,
                    Address? inventoryAddr,
                    IEnumerable<(HashDigest<SHA256> fungibleId, int count)>? fungibleIdAndCounts
                    )>("args");
                ActionBase action = new UnloadFromMyGarages(
                    args.fungibleAssetValues,
                    args.inventoryAddr,
                    args.fungibleIdAndCounts);
                return Encode(context, action);
            }
        );
    }
}
