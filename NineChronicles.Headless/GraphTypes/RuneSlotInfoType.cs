using GraphQL.Types;
using Nekoyume.Model;

namespace NineChronicles.Headless.GraphTypes
{
    public class RuneSlotInfoType : ObjectGraphType<RuneSlotInfo>
    {
        public RuneSlotInfoType()
        {
            Field<NonNullGraphType<IntGraphType>>(
                nameof(RuneSlotInfo.SlotIndex),
                resolve: context => context.Source.SlotIndex);
            Field<NonNullGraphType<IntGraphType>>(
                nameof(RuneSlotInfo.RuneId),
                resolve: context => context.Source.RuneId);
        }
    }
}
