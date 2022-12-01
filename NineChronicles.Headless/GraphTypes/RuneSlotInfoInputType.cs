using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using GraphQL.Types;
using Libplanet;
using Libplanet.Assets;
using Nekoyume.Action;

namespace NineChronicles.Headless.GraphTypes
{
    public class RuneSlotInfoInputType : InputObjectGraphType<RuneSlotInfo>
    {
        public RuneSlotInfoInputType()
        {
            Field<NonNullGraphType<IntGraphType>>("slotIndex");
            Field<NonNullGraphType<IntGraphType>>("runeId");
            Field<NonNullGraphType<IntGraphType>>("level");
        }

        public override object ParseDictionary(IDictionary<string, object?> value)
        {
            int slotIndex = (int)value["slotIndex"]!;
            int runeId = (int)value["runeId"]!;
            int level = (int)value["level"]!;
            return new RuneSlotInfo(slotIndex: slotIndex, runeId: runeId);
        }
    }
}
