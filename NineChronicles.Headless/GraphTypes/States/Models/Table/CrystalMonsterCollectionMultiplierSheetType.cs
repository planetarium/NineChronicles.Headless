using GraphQL.Types;
using Nekoyume.TableData.Crystal;

namespace NineChronicles.Headless.GraphTypes.States.Models.Table
{
    public class CrystalMonsterCollectionMultiplierSheetType : ObjectGraphType<CrystalMonsterCollectionMultiplierSheet>
    {
        public CrystalMonsterCollectionMultiplierSheetType()
        {
            Field<NonNullGraphType<ListGraphType<NonNullGraphType<CrystalMonsterCollectionMultiplierRowType>>>>(
                nameof(CrystalMonsterCollectionMultiplierSheet.OrderedList),
                resolve: context => context.Source.OrderedList
            );
        }
    }
}
