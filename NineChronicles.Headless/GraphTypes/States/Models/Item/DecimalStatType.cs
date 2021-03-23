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
                nameof(DecimalStat.Type),
                resolve: context => context.Source.Type);
            Field<NonNullGraphType<DecimalGraphType>>(nameof(DecimalStat.Value));
        }
    }
}
