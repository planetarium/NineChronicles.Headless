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
        private void RegisterClaimItems()
        {
            Field<NonNullGraphType<ByteStringType>>(
                "claimItems",
                arguments: new QueryArguments(
                    new QueryArgument<NonNullGraphType<ListGraphType<NonNullGraphType<ClaimDataInputType>>>>
                    {
                        Name = "claimData",
                        Description = "List of pair of avatar address, List<FAV> to claim."
                    },
                    new QueryArgument<StringGraphType>
                    {
                        Name = "memo",
                        Description = "Memo to attach to this action."
                    }
                ),
                resolve: context =>
                {
                    var claimData =
                        context.GetArgument<
                            List<(Address avataAddress, IReadOnlyList<FungibleAssetValue> fungibleAssetValues)>
                        >("claimData").AsReadOnly();
                    var memo = context.GetArgument<string?>("memo");
                    ActionBase action = new ClaimItems(claimData, memo);
                    return Encode(context, action);
                }
            );
        }
    }
}
