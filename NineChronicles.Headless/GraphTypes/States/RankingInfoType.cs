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
                description: "AvatarState total EXP.",
                resolve: context => context.Source.Exp);
            Field<NonNullGraphType<IntGraphType>>(
                nameof(RankingInfo.Level),
                description: "AvatarState Level.",
                resolve: context => context.Source.Level);
            Field<NonNullGraphType<IntGraphType>>(
                nameof(RankingInfo.ArmorId),
                description: "Equipped Armor id from EquipmentItemSheet.",
                resolve: context => context.Source.ArmorId);
            Field<NonNullGraphType<LongGraphType>>(
                nameof(RankingInfo.UpdatedAt),
                description: "RankingInfo updated block index.",
                resolve: context => context.Source.UpdatedAt);
            Field<NonNullGraphType<LongGraphType>>(
                nameof(RankingInfo.StageClearedBlockIndex),
                description: "Latest stage cleared block index.",
                resolve: context => context.Source.StageClearedBlockIndex);
            Field<NonNullGraphType<AddressType>>(
                nameof(RankingInfo.AgentAddress),
                description: "Address of AgentState.",
                resolve: context => context.Source.AgentAddress);
            Field<NonNullGraphType<AddressType>>(
                nameof(RankingInfo.AvatarAddress),
                description: "Address of AvatarState.",
                resolve: context => context.Source.AvatarAddress);
            Field<NonNullGraphType<StringGraphType>>(
                nameof(RankingInfo.AvatarName),
                description: "AvatarState name.",
                resolve: context => context.Source.AvatarName);
        }
    }
}
