using GraphQL;
using GraphQL.Types;
using Libplanet.Crypto;
using Libplanet.Explorer.GraphTypes;
using Nekoyume.Action;

namespace NineChronicles.Headless.GraphTypes;

public partial class ActionQuery
{
    private void RegisterSummon()
    {
        Field<NonNullGraphType<ByteStringType>>(
            "auraSummon",
            arguments: new QueryArguments(
                new QueryArgument<NonNullGraphType<AddressType>>
                {
                    Name = "avatarAddress",
                    Description = "Avatar address to get summoned items"
                },
                new QueryArgument<NonNullGraphType<IntGraphType>>
                {
                    Name = "groupId",
                    Description = "Summon group id"
                },
                new QueryArgument<NonNullGraphType<IntGraphType>>
                {
                    Name = "summonCount",
                    Description = "Count to summon. Must between 1 and 10."
                }
            ),
            resolve: context =>
            {
                var avatarAddr = context.GetArgument<Address>("avatarAddress");
                var groupId = context.GetArgument<int>("groupId");
                var summonCount = context.GetArgument<int>("summonCount");

                ActionBase action = new AuraSummon(avatarAddr, groupId, summonCount);
                return Encode(context, action);
            }
        );

        Field<NonNullGraphType<ByteStringType>>(
            "runeSummon",
            arguments: new QueryArguments(
                new QueryArgument<NonNullGraphType<AddressType>>
                {
                    Name = "avatarAddress",
                    Description = "Avatar address to get summoned items"
                },
                new QueryArgument<NonNullGraphType<IntGraphType>>
                {
                    Name = "groupId",
                    Description = "Summon group id"
                },
                new QueryArgument<NonNullGraphType<IntGraphType>>
                {
                    Name = "summonCount",
                    Description = "Count to summon. Must between 1 and 10."
                }
            ),
            resolve: context =>
            {
                var avatarAddr = context.GetArgument<Address>("avatarAddress");
                var groupId = context.GetArgument<int>("groupId");
                var summonCount = context.GetArgument<int>("summonCount");

                ActionBase action = new RuneSummon
                {
                    AvatarAddress = avatarAddr,
                    GroupId = groupId,
                    SummonCount = summonCount,
                };
                return Encode(context, action);
            }
        );
    }
}
