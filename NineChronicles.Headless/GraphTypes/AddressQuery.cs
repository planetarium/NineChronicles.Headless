using System.Runtime.CompilerServices;
using Bencodex.Types;
using GraphQL;
using GraphQL.Types;
using Libplanet;
using Libplanet.Explorer.GraphTypes;
using Nekoyume;
using Nekoyume.Helper;
using Nekoyume.Model.State;

namespace NineChronicles.Headless.GraphTypes
{
    public class AddressQuery : ObjectGraphType
    {
        public AddressQuery(StandaloneContext standaloneContext)
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
                resolve: context => Addresses.GetRaiderAddress(
                    context.GetArgument<Address>("avatarAddress"),
                    context.GetArgument<int>("raidId")));

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
                resolve: context => Addresses.GetWorldBossAddress(
                    context.GetArgument<int>("raidId")));

            Field<NonNullGraphType<AddressType>>(
                name: "worldBossKillRewardRecordAddress",
                description: "user boss kill reward record address by world boss season.",
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
                resolve: context => Addresses.GetWorldBossKillRewardRecordAddress(
                    context.GetArgument<Address>("avatarAddress"),
                    context.GetArgument<int>("raidId")));

            Field<NonNullGraphType<AddressType>>(
                name: "raiderListAddress",
                description: "raider list address by world boss season.",
                arguments: new QueryArguments(
                    new QueryArgument<NonNullGraphType<IntGraphType>>
                    {
                        Name = "raidId",
                        Description = "world boss season id."
                    }
                ),
                resolve: context => Addresses.GetRaiderListAddress(
                    context.GetArgument<int>("raidId")));

            Field<ListGraphType<NonNullGraphType<AddressType>>>(
                name: "currencyMintersAddress",
                description: "currency minters address.",
                arguments: new QueryArguments(
                    new QueryArgument<NonNullGraphType<CurrencyEnumType>>
                    {
                        Name = "currency",
                        Description = "A currency type. " +
                                      "see also: https://github.com/planetarium/NineChronicles.Headless/blob/main/NineChronicles.Headless/GraphTypes/CurrencyEnumType.cs",
                    }),
                resolve: context =>
                {
                    var currencyEnum = context.GetArgument<CurrencyEnum>("currency");
                    if (!standaloneContext.TryGetCurrency(currencyEnum, out var currency))
                    {
                        throw new ExecutionError($"Currency {currencyEnum} is not found.");
                    }

                    return currency!.Value.Minters;
                });
        }
    }
}
