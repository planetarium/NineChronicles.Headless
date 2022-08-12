using GraphQL;
using GraphQL.Types;
using Libplanet;
using Libplanet.Explorer.GraphTypes;
using Microsoft.AspNetCore.DataProtection.AuthenticatedEncryption.ConfigurationModel;
using Nekoyume;
using NineChronicles.Headless.GraphTypes.States;

namespace NineChronicles.Headless.GraphTypes
{
    public class AddressQuery : ObjectGraphType
    {
        public AddressQuery()
        {
            Field<NonNullGraphType<AddressType>>(
                name: "raiderAddress",
                description: "user information address by world boss season.",
                arguments: new QueryArguments(
                    new QueryArgument<NonNullGraphType<AddressType>>
                    {
                        Name = "avatarAddress",
                        Description = "address of avatar state."
                    },
                    new QueryArgument<NonNullGraphType<IntGraphType>>
                    {
                        Name = "raidId",
                        Description = "world boss season id."
                    }
                ),
                resolve: context =>
                {
                    var avatarAddress = context.GetArgument<Address>("avtarAddress");
                    var raidId = context.GetArgument<int>("raidId");
                    return Addresses.GetRaiderAddress(avatarAddress, raidId);
                }
            );

            Field<NonNullGraphType<AddressType>>(
                name: "worldBossAddress",
                description: "boss information address by world boss season.",
                arguments: new QueryArguments(
                    new QueryArgument<NonNullGraphType<IntGraphType>>
                    {
                        Name = "raidId",
                        Description = "world boss season id."
                    }
                ),
                resolve: context =>
                {
                    var raidId = context.GetArgument<int>("raidId");
                    return Addresses.GetWorldBossAddress(raidId);
                }
            );
        }
    }
}
