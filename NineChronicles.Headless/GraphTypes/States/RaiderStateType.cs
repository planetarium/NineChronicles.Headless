using GraphQL.Types;
using Libplanet.Explorer.GraphTypes;
using Nekoyume.Model.State;

namespace NineChronicles.Headless.GraphTypes.States
{
    public class RaiderStateType : ObjectGraphType<RaiderState>
    {
        public RaiderStateType()
        {
            Field<IntGraphType>(
                nameof(RaiderState.TotalScore),
                description: "season total score.",
                resolve: context => context.Source.TotalScore
            );
            Field<IntGraphType>(
                nameof(RaiderState.HighScore),
                description: "season high score.",
                resolve: context => context.Source.HighScore
            );
            Field<IntGraphType>(
                nameof(RaiderState.TotalChallengeCount),
                description: "season total challenge count.",
                resolve: context => context.Source.TotalChallengeCount
            );
            Field<IntGraphType>(
                nameof(RaiderState.RemainChallengeCount),
                description: "remain challenge count before refill.",
                resolve: context => context.Source.RemainChallengeCount
            );
            Field<IntGraphType>(
                nameof(RaiderState.LatestRewardRank),
                description: "latest reward claimed season rank.",
                resolve: context => context.Source.LatestRewardRank
            );
            Field<IntGraphType>(
                nameof(RaiderState.PurchaseCount),
                description: "challenge ticket purchase count.",
                resolve: context => context.Source.PurchaseCount
            );
            Field<IntGraphType>(
                nameof(RaiderState.Cp),
                description: "combat point of avatar state.",
                resolve: context => context.Source.Cp
            );
            Field<IntGraphType>(
                nameof(RaiderState.Level),
                description: "level of avatar state.",
                resolve: context => context.Source.Level
            );
            Field<IntGraphType>(
                nameof(RaiderState.IconId),
                description: "icon id for ranking portrait.",
                resolve: context => context.Source.IconId
            );
            Field<IntGraphType>(
                nameof(RaiderState.LatestBossLevel),
                description: "latest challenge boss level.",
                resolve: context => context.Source.LatestBossLevel
            );
            Field<LongGraphType>(
                nameof(RaiderState.ClaimedBlockIndex),
                description: "rank reward claimed block index.",
                resolve: context => context.Source.ClaimedBlockIndex
            );
            Field<LongGraphType>(
                nameof(RaiderState.RefillBlockIndex),
                description: "ticket refilled block index.",
                resolve: context => context.Source.RefillBlockIndex
            );
            Field<AddressType>(
                nameof(RaiderState.AvatarAddress),
                description: "address of avatar state.",
                resolve: context => context.Source.AvatarAddress
            );
            Field<StringGraphType>(
                nameof(RaiderState.AvatarNameWithHash),
                description: "name of avatar state.",
                resolve: context => context.Source.AvatarNameWithHash
            );
        }
    }
}
