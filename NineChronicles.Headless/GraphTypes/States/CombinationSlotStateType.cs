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
                "unlockBlockIndex",
                description: "Block index at the combination slot can be usable.",
                resolve: context => context.Source.WorkCompleteBlockIndex);
            Field<NonNullGraphType<LongGraphType>>(
                "startBlockIndex",
                description: "Block index at the combination started.",
                resolve: context => context.Source.WorkStartBlockIndex);
            Field<IntGraphType>(
                nameof(CombinationSlotState.PetId),
                description: "Pet id used in equipment",
                resolve: context => context.Source.PetId);
            Field<NonNullGraphType<IntGraphType>>(
                nameof(CombinationSlotState.Index),
                description: "Slot Index at the combination slot",
                resolve: context => context.Source.Index);
            Field<NonNullGraphType<BooleanGraphType>>(
                nameof(CombinationSlotState.IsUnlocked),
                description: "Is the combination slot unlocked",
                resolve: context => context.Source.IsUnlocked);
        }
    }
}
