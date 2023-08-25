using System.Collections.Generic;
using GraphQL.Types;
using Nekoyume.Model;

namespace NineChronicles.Headless.GraphTypes
{
    public class RuneSlotInfoInputType : InputObjectGraphType<RuneSlotInfo>
    {
        public RuneSlotInfoInputType()
        {
            Field<NonNullGraphType<IntGraphType>>("slotIndex");
            Field<NonNullGraphType<IntGraphType>>("runeId");
        }

        public override object ParseDictionary(IDictionary<string, object?> value)
        {
            int slotIndex = (int)value["slotIndex"]!;
            int runeId = (int)value["runeId"]!;
            return new RuneSlotInfo(slotIndex: slotIndex, runeId: runeId);
        }
    }
}
