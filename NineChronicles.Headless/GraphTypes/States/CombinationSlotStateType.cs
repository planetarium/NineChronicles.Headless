using GraphQL.Types;
using Libplanet.Explorer.GraphTypes;
using Nekoyume.Model.State;

namespace NineChronicles.Headless.GraphTypes.States
{
    public class CombinationSlotStateType : ObjectGraphType<CombinationSlotState>
    {
        public CombinationSlotStateType()
        {
            Field<NonNullGraphType<AddressType>>(
                nameof(CombinationSlotState.address),
                description: "Address of combination slot.",
                resolve: context => context.Source.address);
            Field<NonNullGraphType<LongGraphType>>(
                nameof(CombinationSlotState.UnlockBlockIndex),
                description: "Block index at the combination slot can be usable.",
                resolve: context => context.Source.UnlockBlockIndex);
#pragma warning disable CS0618
            Field<NonNullGraphType<IntGraphType>>(
                nameof(
                    CombinationSlotState.UnlockStage),
                description: "Stage id at the combination slot unlock.",
                resolve: context => context.Source.UnlockStage);
#pragma warning restore CS0618
            Field<NonNullGraphType<LongGraphType>>(
                nameof(CombinationSlotState.StartBlockIndex),
                description: "Block index at the combination started.",
                resolve: context => context.Source.StartBlockIndex);
            Field<IntGraphType>(
                nameof(CombinationSlotState.PetId),
                description: "Pet id used in equipment",
                resolve: context => context.Source.PetId);
        }
    }
}
