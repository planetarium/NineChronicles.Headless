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
                description: "Avatar total EXP.",
                resolve: context => context.Source.Exp);
            Field<NonNullGraphType<IntGraphType>>(
                nameof(RankingInfo.Level),
                description: "Avatar Level.",
                resolve: context => context.Source.Level);
            Field<NonNullGraphType<IntGraphType>>(
                nameof(RankingInfo.ArmorId),
                description: "Equipped Armor ID from EquipmentItemSheet.",
                resolve: context => context.Source.ArmorId);
            Field<NonNullGraphType<LongGraphType>>(
                nameof(RankingInfo.UpdatedAt),
                description: "Block index at RankingInfo update.",
                resolve: context => context.Source.UpdatedAt);
            Field<NonNullGraphType<LongGraphType>>(
                nameof(RankingInfo.StageClearedBlockIndex),
                description: "Block index at Latest stage cleared.",
                resolve: context => context.Source.StageClearedBlockIndex);
            Field<NonNullGraphType<AddressType>>(
                nameof(RankingInfo.AgentAddress),
                description: "Address of agent.",
                resolve: context => context.Source.AgentAddress);
            Field<NonNullGraphType<AddressType>>(
                nameof(RankingInfo.AvatarAddress),
                description: "Address of avatar.",
                resolve: context => context.Source.AvatarAddress);
            Field<NonNullGraphType<StringGraphType>>(
                nameof(RankingInfo.AvatarName),
                description: "Avatar name.",
                resolve: context => context.Source.AvatarName);
        }
    }
}
