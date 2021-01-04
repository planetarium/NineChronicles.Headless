using GraphQL.Types;
using Nekoyume.Model.State;

namespace NineChronicles.Headless.GraphTypes.States
{
    public class RankingInfoType : ObjectGraphType<RankingInfo>
    {
        public RankingInfoType()
        {
            Field<NonNullGraphType<LongGraphType>>(
                nameof(RankingInfo.Exp),
                resolve: context => context.Source.Exp);
            Field<NonNullGraphType<IntGraphType>>(
                nameof(RankingInfo.Level),
                resolve: context => context.Source.Level);
            Field<NonNullGraphType<IntGraphType>>(
                nameof(RankingInfo.ArmorId),
                resolve: context => context.Source.ArmorId);
            Field<NonNullGraphType<LongGraphType>>(
                nameof(RankingInfo.UpdatedAt),
                resolve: context => context.Source.UpdatedAt);
            Field<NonNullGraphType<LongGraphType>>(
                nameof(RankingInfo.StageClearedBlockIndex),
                resolve: context => context.Source.StageClearedBlockIndex);
            Field<NonNullGraphType<AddressType>>(
                nameof(RankingInfo.AgentAddress),
                resolve: context => context.Source.AgentAddress);
            Field<NonNullGraphType<AddressType>>(
                nameof(RankingInfo.AvatarAddress),
                resolve: context => context.Source.AvatarAddress);
            Field<NonNullGraphType<StringGraphType>>(
                nameof(RankingInfo.AvatarName),
                resolve: context => context.Source.AvatarName);
        }
    }
}
