using GraphQL;
using GraphQL.Types;
using Libplanet;
using Libplanet.Explorer.GraphTypes;
using Nekoyume.Action;
using NCAction = Libplanet.Action.PolymorphicAction<Nekoyume.Action.ActionBase>;

namespace NineChronicles.Headless.GraphTypes;

public partial class ActionQuery
{
    private void RegisterRapidCombination()
    {
        Field<NonNullGraphType<ByteStringType>>(
            "rapidCombination",
            arguments: new QueryArguments(
                new QueryArgument<NonNullGraphType<AddressType>>
                {
                    Name = "avatarAddress",
                    Description = "Avatar address to execute rapid combination"
                },
                new QueryArgument<NonNullGraphType<IntGraphType>>
                {
                    Name = "slotIndex",
                    Description = "Slot index to execute rapid"
                }
            ),
            resolve: context =>
            {
                var avatarAddress = context.GetArgument<Address>("avatarAddress");
                var slotIndex = context.GetArgument<int>("slotIndex");
                NCAction action = new RapidCombination
                {
                    avatarAddress = avatarAddress,
                    slotIndex = slotIndex
                };
                return Encode(context, action);
            });
    }
}
