using System.Linq;
using GraphQL.Types;
using Libplanet.Explorer.GraphTypes;
using Nekoyume.Model.State;
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
        }
    }
}
