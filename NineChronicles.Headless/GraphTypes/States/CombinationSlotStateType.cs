using GraphQL.Types;
using Libplanet.Explorer.GraphTypes;
using Nekoyume.Model.State;

namespace NineChronicles.Headless.GraphTypes.States
{
    public class CombinationSlotStateType: ObjectGraphType<CombinationSlotState>
    {
        public CombinationSlotStateType()
        {
            Field<NonNullGraphType<AddressType>>(
                nameof(CombinationSlotState.address),
                description: "Address of combination slot.",
                resolve: context => context.Source.address);
            Field<NonNullGraphType<IntGraphType>>(
                nameof(CombinationSlotState.UnlockBlockIndex),
                description: "Block index at the combination slot can be usable.",
                resolve: context => context.Source.UnlockBlockIndex);
            Field<NonNullGraphType<IntGraphType>>(
                nameof(CombinationSlotState.UnlockStage),
                description: "Stage id at the combination slot unlock.",
                resolve: context => context.Source.UnlockStage);
            Field<NonNullGraphType<IntGraphType>>(
                nameof(CombinationSlotState.StartBlockIndex),
                description: "Block index at the combination started.",
                resolve: context => context.Source.StartBlockIndex);
        }
    }
}
