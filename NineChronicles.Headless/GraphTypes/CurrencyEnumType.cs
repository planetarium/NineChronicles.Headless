using System.ComponentModel;
using GraphQL.Types;

namespace NineChronicles.Headless.GraphTypes
{
    [Description("The currency type.")]
    public enum CurrencyEnum
    {
        CRYSTAL,
        NCG,
        GARAGE,
        MEAD,
        RUNE_GOLDENLEAF,
        RUNE_ADVENTURER,
        RUNESTONE_FREYA_LIBERATION,
        RUNESTONE_FREYA_BLESSING,
        RUNESTONE_ODIN_WEAKNESS,
        RUNESTONE_ODIN_WISDOM
    }

    public class CurrencyEnumType : EnumerationGraphType<CurrencyEnum>
    {
    }
}
