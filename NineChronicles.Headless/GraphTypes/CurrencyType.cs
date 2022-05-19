using System.ComponentModel;
using GraphQL.Types;
using Libplanet.Assets;
using Nekoyume.Helper;

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
        public static readonly Currency NCG = new Currency("NCG", 2, minters: null);
        public static Currency CRYSTAL => CrystalCalculator.CRYSTAL;
    }
}
