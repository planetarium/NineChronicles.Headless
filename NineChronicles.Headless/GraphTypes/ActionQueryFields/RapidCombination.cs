using System.Collections.Generic;
using GraphQL;
using GraphQL.Types;
using Libplanet.Crypto;
using Libplanet.Explorer.GraphTypes;
using Nekoyume.Action;

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
                new QueryArgument<NonNullGraphType<ListGraphType<NonNullGraphType<IntGraphType>>>>
                {
                    Name = "slotIndexList",
                    Description = "Slot index list to execute rapid"
                }
            ),
            resolve: context =>
            {
                var avatarAddress = context.GetArgument<Address>("avatarAddress");
                var slotIndexList = context.GetArgument<List<int>>("slotIndexList");
                ActionBase action = new RapidCombination
                {
                    avatarAddress = avatarAddress,
                    slotIndexList = slotIndexList
                };
                return Encode(context, action);
            });
    }
}
