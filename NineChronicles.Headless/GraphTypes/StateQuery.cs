using System;
using System.Collections.Generic;
using System.Linq;
using Bencodex.Types;
using GraphQL;
using GraphQL.Types;
using Libplanet;
using Libplanet.Explorer.GraphTypes;
using Nekoyume;
using Nekoyume.Action;
using Nekoyume.Arena;
using Nekoyume.Model.Arena;
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
            Field<AvatarStateType>(
                name: "avatar",
                description: "State for avatar.",
                arguments: new QueryArguments(new QueryArgument<NonNullGraphType<AddressType>>
                {
                    Name = "avatarAddress",
                    Description = "Address of avatar."
                }),
                resolve: context =>
                {
                    var address = context.GetArgument<Address>("avatarAddress");
                    try
                    {
                        return context.Source.AccountStateGetter.GetAvatarState(address);
                    }
                    catch (InvalidAddressException)
                    {
                        throw new InvalidOperationException($"The state {address} doesn't exists");
                    }
                });
            Field<RankingMapStateType>(
                name: "rankingMap",
                description: "State for avatar EXP record.",
                arguments: new QueryArguments(
                    new QueryArgument<NonNullGraphType<IntGraphType>>
                    {
                        Name = "index",
                        Description = "RankingMapState index. 0 ~ 99"
                    }),
                resolve: context =>
                {
                    var index = context.GetArgument<int>("index");
                    if (context.Source.GetState(RankingState.Derive(index)) is { } state)
                    {
                        return new RankingMapState((Dictionary)state);
                    }

                    return null;
                });
            Field<ShopStateType>(
                name: "shop",
                description: "State for shop.",
                deprecationReason: "Shop is migrated to ShardedShop and not using now. Use shardedShop() instead.",
                resolve: context => context.Source.GetState(Addresses.Shop) is { } state
                    ? new ShopState((Dictionary)state)
                    : null);
            Field<ShardedShopStateV2Type>(
                name: "shardedShop",
                description: "State for sharded shop.",
                arguments: new QueryArguments(
                    new QueryArgument<NonNullGraphType<ItemSubTypeEnumType>>
                    {
                        Name = "itemSubType",
                        Description = "ItemSubType for shard. see from https://github.com/planetarium/lib9c/blob/main/Lib9c/Model/Item/ItemType.cs#L13"
                    },
                    new QueryArgument<NonNullGraphType<IntGraphType>>
                    {
                        Name = "nonce",
                        Description = "Nonce for shard. It's not considered if itemSubtype is kind of costume or title. 0 ~ 15"
                    }),
                resolve: context =>
                {
                    var subType = context.GetArgument<ItemSubType>("itemSubType");
                    var nonce = context.GetArgument<int>("nonce").ToString("X").ToLower();

                    if (context.Source.GetState(ShardedShopStateV2.DeriveAddress(subType, nonce)) is { } state)
                    {
                        return new ShardedShopStateV2((Dictionary)state);
                    }

                    return null;
                });
            Field<ChampionshipArenaStateType>(
                name: "championshipArena",
                description: "State for championShip arena.",
                arguments: new QueryArguments(
                    new QueryArgument<NonNullGraphType<IntGraphType>>
                    {
                        Name = "championshipid",
                        Description = "Championship Id, increases each season"
                    },
                    new QueryArgument<NonNullGraphType<IntGraphType>>
                    {
                        Name = "round",
                        Description = "The round number"
                    }),
                resolve: context =>
                {
                    var championshipId = context.GetArgument<int>("championshipid");
                    var round = context.GetArgument<int>("round");
                    var sheets = context.Source.GetSheets(sheetTypes: new[]
                    {
                        typeof(ArenaSheet)
                    });
                    var arenaSheet = sheets.FirstOrDefault().Value.sheet as ArenaSheet;
                    if (arenaSheet == null || !arenaSheet.TryGetValue(championshipId, out var arenaRow))
                    {
                        throw new SheetRowNotFoundException(nameof(ArenaSheet),
                            $"championship Id : {championshipId}");
                    }
                    if (!arenaRow.TryGetRound(round, out var roundData))
                    {
                        throw new RoundNotFoundException(
                            $"[{nameof(BattleArena)}] ChampionshipId({arenaRow.ChampionshipId}) - round({round})");
                    }

                    var arenaParticipantsAdr =
                        ArenaParticipants.DeriveAddress(roundData.ChampionshipId, roundData.Round);
                    if (!context.Source.TryGetArenaParticipants(arenaParticipantsAdr, out var arenaParticipants))
                    {
                        throw new ArenaParticipantsNotFoundException(
                            $"[{nameof(BattleArena)}] ChampionshipId({roundData.ChampionshipId}) - round({roundData.Round})");
                    }
                    var championshipInfo = new ChampionshipArenaStateType();
                    List<ChampionArenaInfo> arenaInformations = new List<ChampionArenaInfo>();
                    var gameConfigState = context.Source.GetGameConfigState();
                    var interval = gameConfigState.DailyArenaInterval;
                    var currentTicketResetCount = ArenaHelper.GetCurrentTicketResetCount(
                                    context.Source.BlockIndex, roundData.StartBlockIndex, interval);
                    foreach (var participant in arenaParticipants.AvatarAddresses)
                    {
                        var arenaInformationAdr =
                            ArenaInformation.DeriveAddress(participant, roundData.ChampionshipId, roundData.Round);
                        if (!context.Source.TryGetArenaInformation(arenaInformationAdr, out var arenaInformation))
                        {
                            continue;
                        }
                        var arenaScoreAdr =
                                ArenaScore.DeriveAddress(participant, roundData.ChampionshipId, roundData.Round);
                        if (!context.Source.TryGetArenaScore(arenaScoreAdr, out var arenaScore))
                        {
                            continue;
                        }
                        var ticket = arenaInformation.Ticket;
                        if (ticket == 0 && arenaInformation.TicketResetCount < currentTicketResetCount)
                        {
                            ticket = 8;
                        }
                        var avatar = context.Source.GetAvatarStateV2(participant);
                        var arenaInfo = new ChampionArenaInfo();
                        arenaInfo.AvatarAddress = participant;
                        arenaInfo.AgentAddress = avatar.agentAddress;
                        arenaInfo.AvatarName = avatar.name;
                        arenaInfo.Win = arenaInformation.Win;
                        arenaInfo.Ticket = ticket;
                        arenaInfo.Lose = arenaInformation.Lose;
                        arenaInfo.Score = arenaScore.Score;
                        arenaInformations.Add(arenaInfo);
                    }

                    var ranks = StateContext.AddRank(arenaInformations.ToArray());
                    foreach (var rank in ranks)
                    {
                        arenaInformations.First(a => a.AvatarAddress == rank.AgentAddress).Rank = rank.Rank;
                    }

                    return arenaInformations.OrderBy(a => a.Rank);
                });
            Field<WeeklyArenaStateType>(
                name: "weeklyArena",
                description: "State for weekly arena.",
                arguments: new QueryArguments(
                    new QueryArgument<NonNullGraphType<IntGraphType>>
                    {
                        Name = "index",
                        Description = "Old WeeklyArenaState index. It increases every 56,000 blocks."
                    }),
                resolve: context =>
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
            Field<AgentStateType>(
                name: "agent",
                description: "State for agent.",
                arguments: new QueryArguments(new QueryArgument<NonNullGraphType<AddressType>>
                {
                    Name = "address",
                    Description = "Address of agent."
                }),
                resolve: context =>
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
                }
            );

            Field<StakeStateType>(
                name: nameof(StakeState),
                description: "State for staking.",
                arguments: new QueryArguments(new QueryArgument<NonNullGraphType<AddressType>>
                {
                    Name = "address",
                    Description = "Address of agent who staked."
                }),
                resolve: context =>
                {
                    var address = context.GetArgument<Address>("address");
                    if (context.Source.GetState(StakeState.DeriveAddress(address)) is Dictionary state)
                    {
                        return new StakeStateType.StakeStateContext(
                            new StakeState(state),
                            context.Source.AccountStateGetter,
                            context.Source.AccountBalanceGetter,
                            context.Source.BlockIndex
                        );
                    }

                    return null;
                }
            );

            Field<MonsterCollectionStateType>(
                nameof(MonsterCollectionState),
                description: "State for monster collection.",
                arguments: new QueryArguments(
                    new QueryArgument<NonNullGraphType<AddressType>>
                    {
                        Name = "agentAddress",
                        Description = "Address of agent."
                    }
                ),
                resolve: context =>
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
                }
            );

            Field<MonsterCollectionSheetType>(
                nameof(MonsterCollectionSheet),
                resolve: context =>
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
                }
            );

            Field<StakeRewardsType>(
                "stakeRewards",
                resolve: context =>
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
                }
            );

            Field<CrystalMonsterCollectionMultiplierSheetType>(
                name: nameof(CrystalMonsterCollectionMultiplierSheet),
                resolve: context =>
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

            Field<ListGraphType<IntGraphType>>(
                "unlockedRecipeIds",
                description: "List of unlocked equipment recipe sheet row ids.",
                arguments: new QueryArguments(new QueryArgument<NonNullGraphType<AddressType>>
                {
                    Name = "avatarAddress",
                    Description = "Address of avatar."
                }),
                resolve: context =>
                {
                    var avatarAddress = context.GetArgument<Address>("avatarAddress");
                    var address = avatarAddress.Derive("recipe_ids");
                    IReadOnlyList<IValue?> values = context.Source.AccountStateGetter(new[] { address });
                    if (values[0] is List rawRecipeIds)
                    {
                        return rawRecipeIds.ToList(StateExtensions.ToInteger);
                    }

                    return null;
                }
            );

            Field<ListGraphType<IntGraphType>>(
                "unlockedWorldIds",
                description: "List of unlocked world sheet row ids.",
                arguments: new QueryArguments(new QueryArgument<NonNullGraphType<AddressType>>
                {
                    Name = "avatarAddress",
                    Description = "Address of avatar."
                }),
                resolve: context =>
                {
                    var avatarAddress = context.GetArgument<Address>("avatarAddress");
                    var address = avatarAddress.Derive("world_ids");
                    IReadOnlyList<IValue?> values = context.Source.AccountStateGetter(new[] { address });
                    if (values[0] is List rawWorldIds)
                    {
                        return rawWorldIds.ToList(StateExtensions.ToInteger);
                    }

                    return null;
                }
            );
        }
    }
}
