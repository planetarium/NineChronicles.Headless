using System.Collections.Generic;
using GraphQL;
using GraphQL.Types;
using Libplanet.Crypto;
using Libplanet.Explorer.GraphTypes;
using Libplanet.Types.Assets;
using Nekoyume.Action;
using NineChronicles.Headless.GraphTypes.Input;

namespace NineChronicles.Headless.GraphTypes
{
    public partial class ActionQuery
    {
        private void RegisterIssueToken()
        {
            Field<NonNullGraphType<ByteStringType>>(
                "issueToken",
                arguments: new QueryArguments(
                    new QueryArgument<NonNullGraphType<ListGraphType<NonNullGraphType<FungibleAssetValueInputType>>>>
                    {
                        Name = "fungibleAssetValues",
                        Description = "List of FungibleAssetValues for wrapping token"
                    },
                    new QueryArgument<NonNullGraphType<ListGraphType<NonNullGraphType<IssueTokenItemsInputType>>>>
                    {
                        Name = "items",
                        Description = "List of pair of item id, count for wrapping token"
                    },
                    new QueryArgument<NonNullGraphType<AddressType>>
                    {
                        Name = "avatarAddress",
                        Description = "Avatar address"
                    }
                ),
                resolve: context =>
                {
                    var fungibleAssetValues = context.GetArgument<List<FungibleAssetValue>>("fungibleAssetValues");
                    var items = context.GetArgument<List<(int itemId, int count, bool tradable)>>("items");
                    var avatarAddress = context.GetArgument<Address>("avatarAddress");
                    ActionBase action = new IssueToken
                    {
                        AvatarAddress = avatarAddress,
                        FungibleAssetValues = fungibleAssetValues,
                        Items = items,
                    };
                    return Encode(context, action);
                }
            );
        }
    }
}
