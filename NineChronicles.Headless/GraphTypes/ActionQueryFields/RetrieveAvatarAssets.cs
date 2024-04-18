using GraphQL;
using GraphQL.Types;
using Libplanet.Crypto;
using Libplanet.Explorer.GraphTypes;
using Nekoyume.Action;

namespace NineChronicles.Headless.GraphTypes;

public partial class ActionQuery
{
    private void RegisterRetrieveAvatarAssets()
    {
        Field<NonNullGraphType<ByteStringType>>(
            name: "retrieveAvatarAssets",
            arguments: new QueryArguments(
                new QueryArgument<NonNullGraphType<AddressType>>
                {
                    Name = "avatarAddress",
                    Description = "Avatar address to retrieve assets"
                }
            ),
            resolve: context =>
            {
                var avatarAddress = context.GetArgument<Address>("avatarAddress");
                ActionBase action = new RetrieveAvatarAssets()
                {
                    AvatarAddress = avatarAddress
                };
                return Encode(context, action);
            }
        );
    }
}
