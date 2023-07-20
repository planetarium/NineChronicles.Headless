using GraphQL;
using GraphQL.Types;
using Libplanet.Crypto;
using Libplanet.Explorer.GraphTypes;
using Nekoyume.Action;

namespace NineChronicles.Headless.GraphTypes;

public partial class ActionQuery
{
    private void RegisterCombinationConsumable()
    {
        Field<NonNullGraphType<ByteStringType>>(
            "CombinationConsumable",
            arguments: new QueryArguments(
                new QueryArgument<NonNullGraphType<AddressType>>
                {
                    Name = "avatarAddress",
                    Description = "Avatar address to combine consumable"
                },
                new QueryArgument<NonNullGraphType<IntGraphType>>
                {
                    Name = "slotIndex",
                    Description = "Slot index to combine"
                },
                new QueryArgument<NonNullGraphType<IntGraphType>>
                {
                    Name = "recipeId",
                    Description = "Recipe ID to combine consumable"
                }
            ),
            resolve: context =>
            {
                var avatarAddress = context.GetArgument<Address>("avatarAddress");
                var slotIndex = context.GetArgument<int>("slotIndex");
                var recipeId = context.GetArgument<int>("recipeId");
                ActionBase action = new CombinationConsumable
                {
                    avatarAddress = avatarAddress,
                    slotIndex = slotIndex,
                    recipeId = recipeId,
                };
                return Encode(context, action);
            }
        );
    }
}
