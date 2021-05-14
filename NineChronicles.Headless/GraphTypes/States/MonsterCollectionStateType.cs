using System;
using System.Collections.Generic;
using System.Linq;
using GraphQL;
using GraphQL.Types;
using Libplanet.Explorer.GraphTypes;
using Nekoyume.Model.State;
using Nekoyume.TableData;
using NineChronicles.Headless.GraphTypes.States.Models.Table;

namespace NineChronicles.Headless.GraphTypes.States
{
    public class MonsterCollectionStateType : ObjectGraphType<MonsterCollectionState>
    {
        public MonsterCollectionStateType()
        {
            Field<NonNullGraphType<AddressType>>(
                nameof(MonsterCollectionState.address),
                resolve: context => context.Source.address);
            Field<NonNullGraphType<LongGraphType>>(
                nameof(MonsterCollectionState.Level),
                resolve: context => context.Source.Level);
            Field<NonNullGraphType<LongGraphType>>(
                nameof(MonsterCollectionState.ExpiredBlockIndex),
                resolve: context => context.Source.ExpiredBlockIndex);
            Field<NonNullGraphType<LongGraphType>>(
                nameof(MonsterCollectionState.StartedBlockIndex),
                resolve: context => context.Source.StartedBlockIndex);
            Field<NonNullGraphType<LongGraphType>>(
                nameof(MonsterCollectionState.ReceivedBlockIndex),
                resolve: context => context.Source.ReceivedBlockIndex);
            Field<NonNullGraphType<LongGraphType>>(
                nameof(MonsterCollectionState.RewardLevel),
                resolve: context => context.Source.RewardLevel);
            Field<NonNullGraphType<BooleanGraphType>>(
                nameof(MonsterCollectionState.End),
                resolve: context => context.Source.End);
            Field<NonNullGraphType<ListGraphType<MonsterCollectionResultType>>>(
                nameof(MonsterCollectionState.RewardMap),
                resolve: context => context.Source.RewardMap.Select(kv => kv.Value));
            Field<ListGraphType<ListGraphType<MonsterCollectionRewardInfoType>>>(
                nameof(MonsterCollectionState.RewardLevelMap),
                resolve: context =>
                {
                    return context.Source.RewardLevelMap.Select(kv => kv.Value).ToList();
                });
            Field<ListGraphType<MonsterCollectionRewardInfoType>>(
                "totalRewards",
                arguments: new QueryArguments(new QueryArgument<LongGraphType> {
                    Name = "rewardLevel",
                    Description = "The level used to calculate total rewards, including lower level rewards."
                }),
                resolve: context =>
                {
                    long rewardLevel = context.GetArgument<long>("rewardLevel", context.Source.RewardLevel);
                    var list = context.Source.RewardLevelMap
                        .Where(kv => kv.Key <= rewardLevel)
                        .Select(kv => kv.Value).ToList();
                    var map = new Dictionary<int, int>();
                    foreach (var ri in list.SelectMany(l => l))
                    {
                        if (map.ContainsKey(ri.ItemId))
                        {
                            map[ri.ItemId] += ri.Quantity;
                        }
                        else
                        {
                            map[ri.ItemId] = ri.Quantity;
                        }
                    }

                    var result = map
                        .Select(
                            kv =>
                                new MonsterCollectionRewardSheet.RewardInfo(kv.Key.ToString(), kv.Value.ToString())
                        )
                        .ToList();
                    return result;
                });
            Field<NonNullGraphType<LongGraphType>>(
                "claimableBlockIndex",
                resolve: context => Math.Max(context.Source.ReceivedBlockIndex, context.Source.StartedBlockIndex) +
                                    MonsterCollectionState.RewardInterval);
        }
    }
}
