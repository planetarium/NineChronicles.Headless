using GraphQL.Types;
using Nekoyume.Model.Stat;
using NineChronicles.Headless.GraphTypes.States.Models.Item.Enum;

namespace NineChronicles.Headless.GraphTypes.States.Models.Item
{
    public class DecimalStatType : ObjectGraphType<DecimalStat>
    {
        public DecimalStatType()
        {
            Field<NonNullGraphType<StatTypeEnumType>>(
                nameof(DecimalStat.StatType),
                resolve: context => context.Source.StatType);
            Field<NonNullGraphType<DecimalGraphType>>(nameof(DecimalStat.BaseValue));
            Field<NonNullGraphType<DecimalGraphType>>(nameof(DecimalStat.AdditionalValue));
            Field<NonNullGraphType<DecimalGraphType>>(nameof(DecimalStat.TotalValue));
        }
    }
}
