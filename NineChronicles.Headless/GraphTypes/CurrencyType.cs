using System.ComponentModel;
using GraphQL.Types;

namespace NineChronicles.Headless.GraphTypes
{
    [Description("The currency type.")]
    public enum CurrencyEnum
    {
        CRYSTAL,
        NCG,
    }

    public class CurrencyType: EnumerationGraphType<CurrencyEnum>
    {
    }
}
