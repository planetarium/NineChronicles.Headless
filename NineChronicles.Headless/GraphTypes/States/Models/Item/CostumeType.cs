using Bencodex.Types;
using GraphQL.Types;
using Libplanet;
using Nekoyume;
using Nekoyume.Battle;
using Nekoyume.Model.Item;
using Nekoyume.TableData;

namespace NineChronicles.Headless.GraphTypes.States.Models.Item
{
    public class CostumeType : ItemBaseType<Costume>
    {
        public CostumeType()
        {
            Field<NonNullGraphType<GuidGraphType>>(
                nameof(Costume.ItemId),
                description: "Guid of costume.",
                resolve: context => context.Source.itemBase.ItemId
            );
            Field<NonNullGraphType<BooleanGraphType>>(
                nameof(Costume.Equipped),
                description: "Status of Avatar equipped.",
                resolve: context => context.Source.itemBase.equipped
            );
            Field<NonNullGraphType<IntGraphType>>(
                "CombatPoint",
                description: "Combat point of costume.",
                resolve: context =>
                {
                    Address sheetAddress = Addresses.GetSheetAddress<CostumeStatSheet>();
                    if (context.Source.accountStateGetter(sheetAddress) is Text text)
                    {
                        CostumeStatSheet costumeStatSheet = new CostumeStatSheet();
                        costumeStatSheet.Set(text.Value);
                        return CPHelper.GetCP(context.Source.itemBase, costumeStatSheet);
                    }

                    return 0;
                }
            );
        }
    }
}
