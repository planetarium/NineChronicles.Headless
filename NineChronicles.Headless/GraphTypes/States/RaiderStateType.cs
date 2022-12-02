using GraphQL.Types;
using Libplanet.Explorer.GraphTypes;
using Nekoyume.Model.State;

namespace NineChronicles.Headless.GraphTypes.States
{
    public class RaiderStateType : ObjectGraphType<RaiderState>
    {
        public RaiderStateType()
        {
            Field<NonNullGraphType<IntGraphType>>(nameof(RaiderState.TotalScore))
                .Description("season total score.")
                .Resolve(context => context.Source.TotalScore);
            Field<NonNullGraphType<IntGraphType>>(nameof(RaiderState.HighScore))
                .Description("season high score.")
                .Resolve(context => context.Source.HighScore);
            Field<NonNullGraphType<IntGraphType>>(nameof(RaiderState.TotalChallengeCount))
                .Description("season total challenge count.")
                .Resolve(context => context.Source.TotalChallengeCount);
            Field<NonNullGraphType<IntGraphType>>(nameof(RaiderState.RemainChallengeCount))
                .Description("remain challenge count before refill.")
                .Resolve(context => context.Source.RemainChallengeCount);
            Field<NonNullGraphType<IntGraphType>>(nameof(RaiderState.LatestRewardRank))
                .Description("latest reward claimed season rank.")
                .Resolve(context => context.Source.LatestRewardRank);
            Field<NonNullGraphType<IntGraphType>>(nameof(RaiderState.PurchaseCount))
                .Description("challenge ticket purchase count.")
                .Resolve(context => context.Source.PurchaseCount);
            Field<NonNullGraphType<IntGraphType>>(nameof(RaiderState.Cp))
                .Description("combat point of avatar state.")
                .Resolve(context => context.Source.Cp);
            Field<NonNullGraphType<IntGraphType>>(nameof(RaiderState.Level))
                .Description("level of avatar state.")
                .Resolve(context => context.Source.Level);
            Field<NonNullGraphType<IntGraphType>>(nameof(RaiderState.IconId))
                .Description("icon id for ranking portrait.")
                .Resolve(context => context.Source.IconId);
            Field<NonNullGraphType<IntGraphType>>(nameof(RaiderState.LatestBossLevel))
                .Description("latest challenge boss level.")
                .Resolve(context => context.Source.LatestBossLevel);
            Field<NonNullGraphType<LongGraphType>>(nameof(RaiderState.ClaimedBlockIndex))
                .Description("rank reward claimed block index.")
                .Resolve(context => context.Source.ClaimedBlockIndex);
            Field<NonNullGraphType<LongGraphType>>(nameof(RaiderState.RefillBlockIndex))
                .Description("ticket refilled block index.")
                .Resolve(context => context.Source.RefillBlockIndex);
            Field<NonNullGraphType<AddressType>>(nameof(RaiderState.AvatarAddress))
                .Description("address of avatar state.")
                .Resolve(context => context.Source.AvatarAddress);
            Field<NonNullGraphType<StringGraphType>>(nameof(RaiderState.AvatarName))
                .Description("name of avatar state.")
                .Resolve(context => context.Source.AvatarName);
        }
    }
}
