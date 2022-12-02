using GraphQL.Types;
using Libplanet.Explorer.GraphTypes;
using Nekoyume.Model.State;
using System;

namespace NineChronicles.Headless.GraphTypes.States
{
    public class MonsterCollectionStateType : ObjectGraphType<MonsterCollectionState>
    {
        public MonsterCollectionStateType()
        {
            Field<NonNullGraphType<AddressType>>(nameof(MonsterCollectionState.address))
                .Resolve(context => context.Source.address);
            Field<NonNullGraphType<LongGraphType>>(nameof(MonsterCollectionState.Level))
                .Resolve(context => context.Source.Level);
            Field<NonNullGraphType<LongGraphType>>(nameof(MonsterCollectionState.ExpiredBlockIndex))
                .Resolve(context => context.Source.ExpiredBlockIndex);
            Field<NonNullGraphType<LongGraphType>>(nameof(MonsterCollectionState.StartedBlockIndex))
                .Resolve(context => context.Source.StartedBlockIndex);
            Field<NonNullGraphType<LongGraphType>>(nameof(MonsterCollectionState.ReceivedBlockIndex))
                .Resolve(context => context.Source.ReceivedBlockIndex);
            Field<NonNullGraphType<LongGraphType>>(nameof(MonsterCollectionState.RewardLevel))
                .Resolve(context => context.Source.RewardLevel);
            Field<NonNullGraphType<LongGraphType>>("claimableBlockIndex")
                .Resolve(context =>
                    Math.Max(context.Source.ReceivedBlockIndex, context.Source.StartedBlockIndex) +
                        MonsterCollectionState.RewardInterval);
        }
    }
}
