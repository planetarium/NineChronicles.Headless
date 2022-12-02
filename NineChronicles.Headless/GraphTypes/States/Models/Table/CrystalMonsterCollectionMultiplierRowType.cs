using GraphQL.Types;
using Nekoyume.TableData.Crystal;

namespace NineChronicles.Headless.GraphTypes.States.Models.Table
{
    public class CrystalMonsterCollectionMultiplierRowType : ObjectGraphType<CrystalMonsterCollectionMultiplierSheet.Row>
    {
        public CrystalMonsterCollectionMultiplierRowType()
        {
            Field<NonNullGraphType<IntGraphType>>(
                nameof(CrystalMonsterCollectionMultiplierSheet.Row.Level))
                .Resolve(context => context.Source.Level);

            Field<NonNullGraphType<IntGraphType>>(
                nameof(CrystalMonsterCollectionMultiplierSheet.Row.Multiplier))
                .Resolve(context => context.Source.Multiplier);
        }
    }
}
