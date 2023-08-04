using GraphQL;
using GraphQL.Types;
using Libplanet.Crypto;
using Libplanet.Explorer.GraphTypes;
using Nekoyume.Action;

namespace NineChronicles.Headless.GraphTypes;

public partial class ActionQuery
{
    private void RegisterCombinationEquipment()
    {
        Field<NonNullGraphType<ByteStringType>>(
            "combinationEquipment",
            arguments: new QueryArguments(
                new QueryArgument<NonNullGraphType<AddressType>>
                {
                    Name = "avatarAddress",
                    Description = "Avatar address to combine equipment"
                },
                new QueryArgument<NonNullGraphType<IntGraphType>>
                {
                    Name = "slotIndex",
                    Description = "Slot index to combine equipment"
                },
                new QueryArgument<NonNullGraphType<IntGraphType>>
                {
                    Name = "recipeId",
                    Description = "Combination recipe ID"
                },
                new QueryArgument<IntGraphType>
                {
                    Name = "subRecipeId",
                    Description = "Sub-recipe ID of this combination"
                },
                new QueryArgument<BooleanGraphType>
                {
                    Name = "payByCrystal",
                    Description = "Pay crystal co combine equipment?"
                },
                new QueryArgument<BooleanGraphType>
                {
                    Name = "useHammerPoint",
                    Description = "Use hammer point to combine equipment?"
                }
            ),
            resolve: context =>
            {
                var avatarAddress = context.GetArgument<Address>("avatarAddress");
                var slotIndex = context.GetArgument<int>("slotIndex");
                var recipeId = context.GetArgument<int>("recipeId");
                var subRecipeId = context.GetArgument<int?>("subRecipeId");
                var payByCrystal = context.GetArgument<bool>("payByCrystal");
                var useHammerPoint = context.GetArgument<bool>("useHammerPoint");
                ActionBase action = new CombinationEquipment
                {
                    avatarAddress = avatarAddress,
                    slotIndex = slotIndex,
                    recipeId = recipeId,
                    subRecipeId = subRecipeId,
                    payByCrystal = payByCrystal,
                    useHammerPoint = useHammerPoint
                };
                return Encode(context, action);
            }
        );
    }
}
