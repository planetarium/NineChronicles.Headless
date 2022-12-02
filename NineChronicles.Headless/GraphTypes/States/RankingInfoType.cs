using GraphQL.Types;
using Libplanet.Explorer.GraphTypes;
using Nekoyume.Model.State;

namespace NineChronicles.Headless.GraphTypes.States
{
    public class RankingInfoType : ObjectGraphType<RankingInfo>
    {
        public RankingInfoType()
        {
            Field<NonNullGraphType<LongGraphType>>(nameof(RankingInfo.Exp))
                .Description("Avatar total EXP.")
                .Resolve(context => context.Source.Exp);
            Field<NonNullGraphType<IntGraphType>>(nameof(RankingInfo.Level))
                .Description("Avatar Level.")
                .Resolve(context => context.Source.Level);
            Field<NonNullGraphType<IntGraphType>>(nameof(RankingInfo.ArmorId))
                .Description("Equipped Armor ID from EquipmentItemSheet.")
                .Resolve(context => context.Source.ArmorId);
            Field<NonNullGraphType<LongGraphType>>(nameof(RankingInfo.UpdatedAt))
                .Description("Block index at RankingInfo update.")
                .Resolve(context => context.Source.UpdatedAt);
            Field<NonNullGraphType<LongGraphType>>(nameof(RankingInfo.StageClearedBlockIndex))
                .Description("Block index at Latest stage cleared.")
                .Resolve(context => context.Source.StageClearedBlockIndex);
            Field<NonNullGraphType<AddressType>>(nameof(RankingInfo.AgentAddress))
                .Description("Address of agent.")
                .Resolve(context => context.Source.AgentAddress);
            Field<NonNullGraphType<AddressType>>(nameof(RankingInfo.AvatarAddress))
                .Description("Address of avatar.")
                .Resolve(context => context.Source.AvatarAddress);
            Field<NonNullGraphType<StringGraphType>>(nameof(RankingInfo.AvatarName))
                .Description("Avatar name.")
                .Resolve(context => context.Source.AvatarName);
        }
    }
}
