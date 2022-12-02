using System;
using System.Collections.Generic;
using System.Linq;
using Bencodex.Types;
using GraphQL;
using GraphQL.Types;
using Libplanet;
using Libplanet.Assets;
using Libplanet.Explorer.GraphTypes;
using Nekoyume;
using Nekoyume.Action;
using Nekoyume.Extensions;
using Nekoyume.Model.Item;
using Nekoyume.Model.State;
using Nekoyume.TableData;
using Nekoyume.TableData.Crystal;
using NineChronicles.Headless.GraphTypes.Abstractions;
using NineChronicles.Headless.GraphTypes.States;
using NineChronicles.Headless.GraphTypes.States.Models.Item.Enum;
using NineChronicles.Headless.GraphTypes.States.Models.Table;

namespace NineChronicles.Headless.GraphTypes
{
    public class StateQuery : ObjectGraphType<StateContext>
    {
        public StateQuery()
        {
            Name = "StateQuery";
            Field<AvatarStateType>("avatar")
                .Description("State for avatar.")
                .Argument<Address>("avatarAddress", false, "Address of avatar.")
                .Resolve(context =>
                {
                    var address = context.GetArgument<Address>("avatarAddress");
                    try
                    {
                        return new AvatarStateType.AvatarStateContext(
                            context.Source.AccountStateGetter.GetAvatarState(address),
                            context.Source.AccountStateGetter,
                            context.Source.AccountBalanceGetter,
                            context.Source.BlockIndex);
                    }
                    catch (InvalidAddressException)
                    {
                        throw new InvalidOperationException($"The state {address} doesn't exists");
                    }
                });
            Field<RankingMapStateType>("rankingMap")
                .Description("State for avatar EXP record.")
                .Argument<int>("index", false, "RankingMapState index. 0 ~ 99")
                .Resolve(context =>
                {
                    var index = context.GetArgument<int>("index");
                    if (context.Source.GetState(RankingState.Derive(index)) is { } state)
                    {
                        return new RankingMapState((Dictionary)state);
                    }

                    return null;
                });
            Field<ShopStateType>("shop")
                .Description("State for shop.")
                .DeprecationReason(
                    "Shop is migrated to ShardedShop and not using now.  " +
                        "Use shardedShop() instead.")
                .Resolve(context => context.Source.GetState(Addresses.Shop) is { } state
                    ? new ShopState((Dictionary)state)
                    : null);
            Field<ShardedShopStateV2Type>("shardedShop")
                .Description("State for sharded shop.")
                .Argument<NonNullGraphType<ItemSubTypeEnumType>>(
                    "itemSubType",
                    "ItemSubType for shard. see from https://github.com/planetarium/lib9c/blob/main/Lib9c/Model/Item/ItemType.cs#L13")
                .Argument<int>(
                    "nonce",
                    false,
                    "Nonce for shard. It's not considered if itemSubtype is kind of costume or title. 0 ~ 15")
                .Resolve(context =>
                {
                    var subType = context.GetArgument<ItemSubType>("itemSubType");
                    var nonce = context.GetArgument<int>("nonce").ToString("X").ToLower();

                    if (context.Source.GetState(ShardedShopStateV2.DeriveAddress(subType, nonce)) is { } state)
                    {
                        return new ShardedShopStateV2((Dictionary)state);
                    }

                    return null;
                });
            Field<WeeklyArenaStateType>("weeklyArena")
                .Description("State for weekly arena.")
                .Argument<int>(
                    "index",
                    false,
                    "WeeklyArenaState index. It increases every 56,000 blocks.")
                .Resolve(context =>
                {
                    var index = context.GetArgument<int>("index");
                    var arenaAddress = WeeklyArenaState.DeriveAddress(index);
                    if (context.Source.GetState(arenaAddress) is { } state)
                    {
                        var arenastate = new WeeklyArenaState((Dictionary)state);
                        if (arenastate.OrderedArenaInfos.Count == 0)
                        {
                            var listAddress = arenaAddress.Derive("address_list");
                            if (context.Source.GetState(listAddress) is List rawList)
                            {
                                var addressList = rawList.ToList(StateExtensions.ToAddress);
                                var arenaInfos = new List<ArenaInfo>();
                                foreach (var address in addressList)
                                {
                                    var infoAddress = arenaAddress.Derive(address.ToByteArray());
                                    if (context.Source.GetState(infoAddress) is Dictionary rawInfo)
                                    {
                                        var info = new ArenaInfo(rawInfo);
                                        arenaInfos.Add(info);
                                    }
                                }
#pragma warning disable CS0618 // Type or member is obsolete
                                arenastate.OrderedArenaInfos.AddRange(arenaInfos.OrderByDescending(a => a.Score).ThenBy(a => a.CombatPoint));
#pragma warning restore CS0618 // Type or member is obsolete
                            }
                        }
                        return arenastate;
                    }

                    return null;
                });
            Field<AgentStateType>("agent")
                .Description("State for agent.")
                .Argument<Address>("address", false, "Address of agent.")
                .Resolve(context =>
                {
                    var address = context.GetArgument<Address>("address");
                    if (context.Source.GetState(address) is Dictionary state)
                    {
                        return new AgentStateType.AgentStateContext(
                            new AgentState(state),
                            context.Source.AccountStateGetter,
                            context.Source.AccountBalanceGetter,
                            context.Source.BlockIndex
                        );
                    }

                    return null;
                });

            StakeStateType.StakeStateContext? GetStakeState(StateContext ctx, Address agentAddress)
            {
                if (ctx.GetState(StakeState.DeriveAddress(agentAddress)) is Dictionary state)
                {
                    return new StakeStateType.StakeStateContext(
                        new StakeState(state),
                        ctx.AccountStateGetter,
                        ctx.AccountBalanceGetter,
                        ctx.BlockIndex
                    );
                }

                return null;
            }

            Field<StakeStateType>(nameof(StakeState))
                .Description("State for staking.")
                .Argument<Address>("address", false, "Address of agent who staked.")
                .Resolve(context =>
                {
                    var address = context.GetArgument<Address>("address");
                    return GetStakeState(context.Source, address);
                });

            Field<NonNullGraphType<ListGraphType<StakeStateType>>>("StakeStates")
                .Description("Staking states having same order as addresses")
                .Argument<NonNullGraphType<ListGraphType<AddressType>>>(
                    "addresses",
                    "Addresses of agents who staked.")
                .Resolve(context =>
                {
                    return context.GetArgument<List<Address>>("addresses")
                        .AsParallel()
                        .AsOrdered()
                        .Select(address => GetStakeState(context.Source, address));
                });

            Field<MonsterCollectionStateType>(nameof(MonsterCollectionState))
                .Description("State for monster collection.")
                .Argument<Address>("agentAddress", false, "Address of agent.")
                .Resolve(context =>
                {
                    var agentAddress = context.GetArgument<Address>("agentAddress");
                    if (!(context.Source.GetState(agentAddress) is Dictionary value))
                    {
                        return null;
                    }
                    var agentState = new AgentState(value);
                    var deriveAddress = MonsterCollectionState.DeriveAddress(agentAddress, agentState.MonsterCollectionRound);
                    if (context.Source.GetState(deriveAddress) is Dictionary state)
                    {
                        return new MonsterCollectionState(state);
                    }

                    return null;
                });

            Field<MonsterCollectionSheetType>(nameof(MonsterCollectionSheet))
                .Resolve(context =>
                {
                    var sheetAddress = Addresses.GetSheetAddress<MonsterCollectionSheet>();
                    var rewardSheetAddress = Addresses.GetSheetAddress<MonsterCollectionRewardSheet>();
                    IReadOnlyList<IValue?> values = context.Source.GetStates(new[]
                    {
                        sheetAddress,
                        rewardSheetAddress,
                    });
                    if (values[0] is Text ss &&
                        values[1] is Text srs)
                    {
                        var monsterCollectionSheet = new MonsterCollectionSheet();
                        monsterCollectionSheet.Set(ss);
                        var monsterCollectionRewardSheet = new MonsterCollectionRewardSheet();
                        monsterCollectionRewardSheet.Set(srs);
                        return (monsterCollectionSheet, monsterCollectionRewardSheet);
                    }

                    return null;
                });

            Field<StakeRewardsType>("stakeRewards")
                .Resolve(context =>
                {
                    var sheetAddress = Addresses.GetSheetAddress<StakeRegularRewardSheet>();
                    var fixedSheetAddress = Addresses.GetSheetAddress<StakeRegularFixedRewardSheet>();
                    IValue? sheetValue = context.Source.GetState(sheetAddress);
                    IValue? fixedSheetValue = context.Source.GetState(fixedSheetAddress);
                    if (sheetValue is Text sv && fixedSheetValue is Text fsv)
                    {
                        var stakeRegularRewardSheet = new StakeRegularRewardSheet();
                        stakeRegularRewardSheet.Set(sv);
                        var stakeRegularFixedRewardSheet = new StakeRegularFixedRewardSheet();
                        stakeRegularFixedRewardSheet.Set(fsv);

                        return (stakeRegularRewardSheet, stakeRegularFixedRewardSheet);
                    }

                    return null;
                });

            Field<CrystalMonsterCollectionMultiplierSheetType>(
                nameof(CrystalMonsterCollectionMultiplierSheet))
                .Resolve(context =>
                {
                    var sheetAddress = Addresses.GetSheetAddress<CrystalMonsterCollectionMultiplierSheet>();
                    IValue? sheetValue = context.Source.GetState(sheetAddress);
                    if (sheetValue is Text sv)
                    {
                        var crystalMonsterCollectionMultiplierSheet = new CrystalMonsterCollectionMultiplierSheet();
                        crystalMonsterCollectionMultiplierSheet.Set(sv);
                        return crystalMonsterCollectionMultiplierSheet;
                    }

                    return null;
                });

            Field<ListGraphType<IntGraphType>>("unlockedRecipeIds")
                .Description("List of unlocked equipment recipe sheet row ids.")
                .Argument<Address>("avatarAddress", false, "Address of avatar.")
                .Resolve(context =>
                {
                    var avatarAddress = context.GetArgument<Address>("avatarAddress");
                    var address = avatarAddress.Derive("recipe_ids");
                    IReadOnlyList<IValue?> values = context.Source.AccountStateGetter(new[] { address });
                    if (values[0] is List rawRecipeIds)
                    {
                        return rawRecipeIds.ToList(StateExtensions.ToInteger);
                    }

                    return null;
                });

            Field<ListGraphType<IntGraphType>>("unlockedWorldIds")
                .Description("List of unlocked world sheet row ids.")
                .Argument<Address>("avatarAddress", false, "Address of avatar.")
                .Resolve(context =>
                {
                    var avatarAddress = context.GetArgument<Address>("avatarAddress");
                    var address = avatarAddress.Derive("world_ids");
                    IReadOnlyList<IValue?> values = context.Source.AccountStateGetter(new[] { address });
                    if (values[0] is List rawWorldIds)
                    {
                        return rawWorldIds.ToList(StateExtensions.ToInteger);
                    }

                    return null;
                });

            Field<RaiderStateType>("raiderState")
                .Description("world boss season user information.")
                .Argument<Address>("raiderAddress", false, "address of world boss season.")
                .Resolve(context =>
                {
                    var raiderAddress = context.GetArgument<Address>("raiderAddress");
                    if (context.Source.GetState(raiderAddress) is List list)
                    {
                        return new RaiderState(list);
                    }

                    return null;
                });

            Field<NonNullGraphType<IntGraphType>>("raidId")
                .Description("world boss season id by block index.")
                .Argument<long>("blockIndex", false)
                .Argument<bool?>(
                    "prev",
                    true,
                    "find previous raid id.",
                    arg => arg.DefaultValue = false)
                .Resolve(context =>
                {
                    var blockIndex = context.GetArgument<long>("blockIndex");
                    var prev = context.GetArgument<bool>("prev");
                    var sheet = new WorldBossListSheet();
                    var address = Addresses.GetSheetAddress<WorldBossListSheet>();
                    if (context.Source.GetState(address) is Text text)
                    {
                        sheet.Set(text);
                    }

                    return prev
                        ? sheet.FindPreviousRaidIdByBlockIndex(blockIndex)
                        : sheet.FindRaidIdByBlockIndex(blockIndex);
                });

            Field<WorldBossStateType>("worldBossState")
                .Description("world boss season boss information.")
                .Argument<Address>("bossAddress", false)
                .Resolve(context =>
                {
                    var bossAddress = context.GetArgument<Address>("bossAddress");
                    if (context.Source.GetState(bossAddress) is List list)
                    {
                        return new WorldBossState(list);
                    }

                    return null;
                });

            Field<WorldBossKillRewardRecordType>("worldBossKillRewardRecord")
                .Description("user boss kill reward record by world boss season.")
                .Argument<Address>("worldBossKillRewardRecordAddress", false)
                .Resolve(context =>
                {
                    var address = context.GetArgument<Address>("worldBossKillRewardRecordAddress");
                    if (context.Source.GetState(address) is List list)
                    {
                        return new WorldBossKillRewardRecord(list);
                    }
                    return null;
                });

            Field<NonNullGraphType<FungibleAssetValueWithCurrencyType>>("balance")
                .Description("asset balance by currency.")
                .Argument<Address>("address", false)
                .Argument<NonNullGraphType<CurrencyInputType>>("currency")
                .Resolve(context =>
                {
                    var address = context.GetArgument<Address>("address");
                    var currency = context.GetArgument<Currency>("currency");
                    return context.Source.GetBalance(address, currency);
                });

            Field<ListGraphType<NonNullGraphType<AddressType>>>("raiderList")
                .Description("raider address list by world boss season.")
                .Argument<Address>("raiderListAddress", false)
                .Resolve(context =>
                {
                    var address = context.GetArgument<Address>("raiderListAddress");
                    if (context.Source.GetState(address) is List list)
                    {
                        return list.ToList(StateExtensions.ToAddress);
                    }
                    return null;
                });
        }
    }
}
