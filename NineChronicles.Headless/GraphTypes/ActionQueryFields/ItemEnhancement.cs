using System;
using System.Collections.Generic;
using GraphQL;
using GraphQL.Types;
using Libplanet.Crypto;
using Libplanet.Explorer.GraphTypes;
using Nekoyume.Action;

namespace NineChronicles.Headless.GraphTypes;

public partial class ActionQuery
{
    private void RegisterItemEnhancement()
    {
        Field<NonNullGraphType<ByteStringType>>(
            "itemEnhancement",
            arguments: new QueryArguments(
                new QueryArgument<NonNullGraphType<AddressType>>
                {
                    Name = "avatarAddress",
                    Description = "Avatar address to enhance item"
                },
                new QueryArgument<NonNullGraphType<IntGraphType>>
                {
                    Name = "slotIndex",
                    Description = "Slot index to enhance item"
                },
                new QueryArgument<NonNullGraphType<GuidGraphType>>
                {
                    Name = "itemId",
                    Description = "Target item ID to enhance"
                },
                new QueryArgument<NonNullGraphType<ListGraphType<NonNullGraphType<GuidGraphType>>>>
                {
                    Name = "materialIds",
                    Description = "Material ID list to enhance"
                }
            ),
            resolve: context =>
            {
                var avatarAddress = context.GetArgument<Address>("avatarAddress");
                var slotIndex = context.GetArgument<int>("slotIndex");
                var itemId = context.GetArgument<Guid>("itemId");
                var materialIds = context.GetArgument<List<Guid>>("materialIds");
                ActionBase action = new ItemEnhancement
                {
                    avatarAddress = avatarAddress,
                    slotIndex = slotIndex,
                    itemId = itemId,
                    materialIds = materialIds
                };
                return Encode(context, action);
            }
        );
    }
}
