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
    }

    public class CurrencyEnumType : EnumerationGraphType<CurrencyEnum>
    {
    }
}
