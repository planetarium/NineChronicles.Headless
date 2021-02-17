using System.Security.Cryptography;
using Bencodex.Types;
using GraphQL;
using GraphQL.Types;
using Libplanet;
using Libplanet.Action;
using Libplanet.Blockchain;
using Nekoyume;
using Nekoyume.Model.State;
using NineChronicles.Headless.GraphTypes.States;

namespace NineChronicles.Headless.GraphTypes
{
    public class StateQuery<T> : ObjectGraphType<BlockChain<T>>
        where T : IAction, new()
    {
        public StateQuery()
        {
            Name = "StateQuery";
            Field<AvatarStateType>(
                name: "avatar",
                arguments: new QueryArguments(
                    new QueryArgument<AddressType>
                    {
                        Name = "address",
                    },
                    new QueryArgument<ByteStringType>
                    {
                        Name = "hash",
                        Description = "Offset block hash for query."
                    }),
                resolve: context =>
                {
                    var address = context.GetArgument<Address>("address");
                    byte[] blockHashByteArray = context.GetArgument<byte[]>("hash");
                    if (blockHashByteArray is null)
                    {
                        return new AvatarState((Dictionary)context.Source.GetState(address));
                    }
                    else
                    {
                        HashDigest<SHA256> blockHash = new HashDigest<SHA256>(blockHashByteArray);
                        return new AvatarState((Dictionary)context.Source.GetState(address, blockHash));
                    }
                });
            Field<RankingMapStateType>(
                name: "rankingMap",
                arguments: new QueryArguments(
                    new QueryArgument<NonNullGraphType<IntGraphType>>
                    {
                        Name = "index",
                    },
                    new QueryArgument<ByteStringType>
                    {
                        Name = "hash",
                        Description = "Offset block hash for query."
                    }),
                resolve: context =>
                {
                    var index = context.GetArgument<int>("index");
                    byte[] blockHashByteArray = context.GetArgument<byte[]>("hash");
                    if (blockHashByteArray is null)
                    {
                        return new RankingMapState(
                            (Dictionary)context.Source.GetState(RankingState.Derive(index)));
                    }
                    else
                    {
                        HashDigest<SHA256> blockHash = new HashDigest<SHA256>(blockHashByteArray);
                        return new RankingMapState(
                            (Dictionary)context.Source.GetState(RankingState.Derive(index), blockHash));
                    }
                });
            Field<ShopStateType>(
                name: "shop",
                arguments: new QueryArguments(
                    new QueryArgument<ByteStringType>
                    {
                        Name = "hash",
                        Description = "Offset block hash for query."
                    }),
                resolve: context =>
                {
                    byte[] blockHashByteArray = context.GetArgument<byte[]>("hash");
                    if (blockHashByteArray is null)
                    {
                        return new ShopState((Dictionary) context.Source.GetState(Addresses.Shop));
                    }
                    else
                    {
                        HashDigest<SHA256> blockHash = new HashDigest<SHA256>(blockHashByteArray);
                        return new ShopState((Dictionary) context.Source.GetState(Addresses.Shop, blockHash));
                    }
                });
            Field<WeeklyArenaStateType>(
                name: "weeklyArena",
                arguments: new QueryArguments(
                    new QueryArgument<NonNullGraphType<IntGraphType>>
                    {
                        Name = "index",
                    },
                    new QueryArgument<ByteStringType>
                    {
                        Name = "hash",
                        Description = "Offset block hash for query."
                    }),
                resolve: context =>
                {
                    var index = context.GetArgument<int>("index");
                    byte[] blockHashByteArray = context.GetArgument<byte[]>("hash");
                    if (blockHashByteArray is null)
                    {
                        return new WeeklyArenaState(
                            (Dictionary) context.Source.GetState(WeeklyArenaState.DeriveAddress(index)));
                    }
                    else
                    {
                        HashDigest<SHA256> blockHash = new HashDigest<SHA256>(blockHashByteArray);
                        return new WeeklyArenaState(
                            (Dictionary) context.Source.GetState(WeeklyArenaState.DeriveAddress(index), blockHash));
                    }
                });
        }
    }
}
