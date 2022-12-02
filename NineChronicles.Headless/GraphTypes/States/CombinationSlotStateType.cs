using GraphQL.Types;
using Libplanet.Explorer.GraphTypes;
using Nekoyume.Model.State;

namespace NineChronicles.Headless.GraphTypes.States
{
    public class CombinationSlotStateType : ObjectGraphType<CombinationSlotState>
    {
        public CombinationSlotStateType()
        {
            Field<NonNullGraphType<AddressType>>(nameof(CombinationSlotState.address))
                .Description("Address of combination slot.")
                .Resolve(context => context.Source.address);
            Field<NonNullGraphType<IntGraphType>>(nameof(CombinationSlotState.UnlockBlockIndex))
                .Description("Block index at the combination slot can be usable.")
                .Resolve(context => context.Source.UnlockBlockIndex);
            Field<NonNullGraphType<IntGraphType>>(nameof(CombinationSlotState.UnlockStage))
                .Description("Stage id at the combination slot unlock.")
                .Resolve(context => context.Source.UnlockStage);
            Field<NonNullGraphType<IntGraphType>>(nameof(CombinationSlotState.StartBlockIndex))
                .Description("Block index at the combination started.")
                .Resolve(context => context.Source.StartBlockIndex);
        }
    }
}
