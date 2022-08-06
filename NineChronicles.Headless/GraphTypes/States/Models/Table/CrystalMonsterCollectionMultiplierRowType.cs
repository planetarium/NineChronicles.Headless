using GraphQL.Types;
using Nekoyume.TableData.Crystal;

namespace NineChronicles.Headless.GraphTypes.States.Models.Table
{
    public class CrystalMonsterCollectionMultiplierRowType : ObjectGraphType<CrystalMonsterCollectionMultiplierSheet.Row>
    {
        public CrystalMonsterCollectionMultiplierRowType()
        {
            Field<NonNullGraphType<IntGraphType>>(
                nameof(CrystalMonsterCollectionMultiplierSheet.Row.Level),
                resolve: context => context.Source.Level
            );

            Field<NonNullGraphType<IntGraphType>>(
                nameof(CrystalMonsterCollectionMultiplierSheet.Row.Multiplier),
                resolve: context => context.Source.Multiplier
            );
        }
    }
}
