using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Bencodex.Types;
using GraphQL;
using GraphQL.Types;
using Lib9c.Model.Order;
using Libplanet.Crypto;
using Libplanet.Types.Assets;
using Libplanet.Explorer.GraphTypes;
using Nekoyume;
using Nekoyume.Action;
using Nekoyume.Arena;
using Nekoyume.Battle;
using Nekoyume.Extensions;
using Nekoyume.Model.Arena;
using Nekoyume.Model.EnumType;
using Nekoyume.Model.Item;
using Nekoyume.Model.Stake;
using Nekoyume.Model.State;
using Nekoyume.TableData;
using Nekoyume.TableData.Crystal;
using Nekoyume.TableData.Stake;
using NineChronicles.Headless.GraphTypes.Abstractions;
using NineChronicles.Headless.GraphTypes.States;
using NineChronicles.Headless.GraphTypes.States.Models;
using NineChronicles.Headless.GraphTypes.States.Models.Item;
using NineChronicles.Headless.GraphTypes.States.Models.Item.Enum;
using NineChronicles.Headless.GraphTypes.States.Models.Table;
using static Lib9c.SerializeKeys;

namespace NineChronicles.Headless.GraphTypes
{
    public partial class StateQuery : ObjectGraphType<StateContext>
    {
        public StateQuery()
        {
            Name = "StateQuery";

            AvatarStateType.AvatarStateContext? GetAvatarState(StateContext context, Address address)
            {
                try
                {
                    return new AvatarStateType.AvatarStateContext(
                        context.AccountState.GetAvatarState(address),
                        context.AccountState,
                        context.BlockIndex);
                }
                catch (InvalidAddressException)
                {
                    return null;
                }
            }

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
                    return GetAvatarState(context.Source, address)
                        ?? throw new InvalidOperationException($"The state {address} doesn't exists");
                });
            Field<NonNullGraphType<ListGraphType<AvatarStateType>>>(
                name: "avatars",
                description: "Avatar states having some order as addresses",
                arguments: new QueryArguments(
                    new QueryArgument<NonNullGraphType<ListGraphType<NonNullGraphType<AddressType>>>>
                    {
                        Name = "addresses",
                        Description = "Addresses of avatars to query."
                    }
                ),
                resolve: context =>
                {
                    return context.GetArgument<List<Address>>("addresses")
                        .AsParallel()
                        .AsOrdered()
                        .Select(address => GetAvatarState(context.Source, address));
                }
            );
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
            Field<WeeklyArenaStateType>(
                name: "weeklyArena",
                description: "State for weekly arena.",
                arguments: new QueryArguments(
                    new QueryArgument<NonNullGraphType<IntGraphType>>
                    {
                        Name = "index",
                        Description = "WeeklyArenaState index. It increases every 56,000 blocks."
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
                                arenastate.OrderedArenaInfos.AddRange(arenaInfos.OrderByDescending(a => a.Score)
                                    .ThenBy(a => a.CombatPoint));
#pragma warning restore CS0618 // Type or member is obsolete
                            }
                        }

                        return arenastate;
                    }

                    return null;
                });
            Field<NonNullGraphType<ListGraphType<NonNullGraphType<ArenaInformationType>>>>(
                name: "arenaInformation",
                description: "List of arena information of requested arena and avatar list",
                arguments: new QueryArguments(
                    new QueryArgument<NonNullGraphType<IntGraphType>>
                    {
                        Name = "championshipId",
                        Description = "Championship ID to get arena information"
                    },
                    new QueryArgument<NonNullGraphType<IntGraphType>>
                    {
                        Name = "round",
                        Description = "Round of championship to get arena information"
                    },
                    new QueryArgument<NonNullGraphType<ListGraphType<NonNullGraphType<AddressType>>>>
                    {
                        Name = "avatarAddresses",
                        Description = "List of avatar address to get arena information"
                    }
                ),
                resolve: context =>
                {
                    var championshipId = context.GetArgument<int>("championshipId");
                    var round = context.GetArgument<int>("round");
                    return context.GetArgument<List<Address>>("avatarAddresses").AsParallel().AsOrdered().Select(
                        address =>
                        {
                            var infoAddr = ArenaInformation.DeriveAddress(address, championshipId, round);
                            var scoreAddr = ArenaScore.DeriveAddress(address, championshipId, round);

                            return (
                                address,
                                new ArenaInformation((List)context.Source.GetState(infoAddr)!),
                                new ArenaScore((List)context.Source.GetState(scoreAddr)!)
                            );
                        }
                    );
                }
            );
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
                            context.Source.AccountState,
                            context.Source.BlockIndex
                        );
                    }

                    return null;
                }
            );

            StakeStateType.StakeStateContext? GetStakeState(StateContext ctx, Address agentAddress)
            {
                var stakeStateAddress = StakeState.DeriveAddress(agentAddress);
                if (ctx.AccountState.TryGetStakeStateV2(agentAddr: agentAddress, out StakeStateV2 stakeStateV2))
                {
                    return new StakeStateType.StakeStateContext(
                        stakeStateV2,
                        stakeStateAddress,
                        ctx.AccountState,
                        ctx.BlockIndex
                    );
                }

                return null;
            }

            Field<StakeStateType>(
                name: "stakeState",
                description: "State for staking.",
                arguments: new QueryArguments(new QueryArgument<NonNullGraphType<AddressType>>
                {
                    Name = "address",
                    Description = "Address of agent who staked."
                }),
                resolve: context =>
                {
                    var address = context.GetArgument<Address>("address");
                    return GetStakeState(context.Source, address);
                }
            );

            Field<NonNullGraphType<ListGraphType<StakeStateType>>>(
                name: "StakeStates",
                description: "Staking states having same order as addresses",
                arguments: new QueryArguments(
                    new QueryArgument<NonNullGraphType<ListGraphType<AddressType>>>
                    {
                        Name = "addresses",
                        Description = "Addresses of agent who staked."
                    }
                ),
                resolve: context =>
                {
                    return context.GetArgument<List<Address>>("addresses")
                        .AsParallel()
                        .AsOrdered()
                        .Select(address => GetStakeState(context.Source, address));
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
                "latestStakeRewards",
                description: "The latest stake rewards based on StakePolicySheet.",
                resolve: context =>
                {
                    var stakePolicySheetStateValue = context.Source.GetState(Addresses.GetSheetAddress<StakePolicySheet>());
                    var stakePolicySheet = new StakePolicySheet();
                    if (stakePolicySheetStateValue is not Text stakePolicySheetStateText)
                    {
                        return null;
                    }

                    stakePolicySheet.Set(stakePolicySheetStateText);

                    IReadOnlyList<IValue?> values = context.Source.GetStates(new[]
                    {
                        Addresses.GetSheetAddress(stakePolicySheet["StakeRegularFixedRewardSheet"].Value),
                        Addresses.GetSheetAddress(stakePolicySheet["StakeRegularRewardSheet"].Value),
                    });

                    if (!(values[0] is Text fsv && values[1] is Text sv))
                    {
                        return null;
                    }

                    var stakeRegularFixedRewardSheet = new StakeRegularFixedRewardSheet();
                    var stakeRegularRewardSheet = new StakeRegularRewardSheet();
                    stakeRegularFixedRewardSheet.Set(fsv);
                    stakeRegularRewardSheet.Set(sv);

                    return (stakeRegularRewardSheet, stakeRegularFixedRewardSheet);
                }
            );
            Field<StakeRewardsType>(
                "stakeRewards",
                deprecationReason: "Since stake3, claim_stake_reward9 actions, each stakers have their own contracts.",
                resolve: context =>
                {
                    StakeRegularRewardSheet stakeRegularRewardSheet;
                    StakeRegularFixedRewardSheet stakeRegularFixedRewardSheet;

                    if (context.Source.BlockIndex < StakeState.StakeRewardSheetV2Index)
                    {
                        stakeRegularRewardSheet = new StakeRegularRewardSheet();
                        stakeRegularRewardSheet.Set(ClaimStakeReward8.V1.StakeRegularRewardSheetCsv);
                        stakeRegularFixedRewardSheet = new StakeRegularFixedRewardSheet();
                        stakeRegularFixedRewardSheet.Set(ClaimStakeReward8.V1.StakeRegularFixedRewardSheetCsv);
                    }
                    else
                    {
                        IReadOnlyList<IValue?> values = context.Source.GetStates(new[]
                        {
                            Addresses.GetSheetAddress<StakeRegularRewardSheet>(),
                            Addresses.GetSheetAddress<StakeRegularFixedRewardSheet>()
                        });

                        if (!(values[0] is Text sv && values[1] is Text fsv))
                        {
                            return null;
                        }

                        stakeRegularRewardSheet = new StakeRegularRewardSheet();
                        stakeRegularRewardSheet.Set(sv);
                        stakeRegularFixedRewardSheet = new StakeRegularFixedRewardSheet();
                        stakeRegularFixedRewardSheet.Set(fsv);
                    }

                    return (stakeRegularRewardSheet, stakeRegularFixedRewardSheet);
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
                    IReadOnlyList<IValue?> values = context.Source.AccountState.GetStates(new[] { address });
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
                    IReadOnlyList<IValue?> values = context.Source.AccountState.GetStates(new[] { address });
                    if (values[0] is List rawWorldIds)
                    {
                        return rawWorldIds.ToList(StateExtensions.ToInteger);
                    }

                    return null;
                }
            );

            Field<RaiderStateType>(
                name: "raiderState",
                description: "world boss season user information.",
                arguments: new QueryArguments(
                    new QueryArgument<NonNullGraphType<AddressType>>
                    {
                        Name = "raiderAddress",
                        Description = "address of world boss season."
                    }
                ),
                resolve: context =>
                {
                    var raiderAddress = context.GetArgument<Address>("raiderAddress");
                    if (context.Source.GetState(raiderAddress) is List list)
                    {
                        return new RaiderState(list);
                    }

                    return null;
                }
            );

            Field<NonNullGraphType<IntGraphType>>(
                "raidId",
                description: "world boss season id by block index.",
                arguments: new QueryArguments(
                    new QueryArgument<NonNullGraphType<LongGraphType>>
                    {
                        Name = "blockIndex"
                    },
                    new QueryArgument<BooleanGraphType>
                    {
                        Name = "prev",
                        Description = "find previous raid id.",
                        DefaultValue = false
                    }
                ),
                resolve: context =>
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
                }
            );

            Field<WorldBossStateType>(
                "worldBossState",
                description: "world boss season boss information.",
                arguments: new QueryArguments(new QueryArgument<NonNullGraphType<AddressType>>
                {
                    Name = "bossAddress"
                }),
                resolve: context =>
                {
                    var bossAddress = context.GetArgument<Address>("bossAddress");
                    if (context.Source.GetState(bossAddress) is List list)
                    {
                        return new WorldBossState(list);
                    }

                    return null;
                }
            );

            Field<WorldBossKillRewardRecordType>(
                "worldBossKillRewardRecord",
                description: "user boss kill reward record by world boss season.",
                arguments: new QueryArguments(new QueryArgument<NonNullGraphType<AddressType>>
                {
                    Name = "worldBossKillRewardRecordAddress"
                }),
                resolve: context =>
                {
                    var address = context.GetArgument<Address>("worldBossKillRewardRecordAddress");
                    if (context.Source.GetState(address) is List list)
                    {
                        return new WorldBossKillRewardRecord(list);
                    }
                    return null;
                }
            );

            Field<NonNullGraphType<FungibleAssetValueWithCurrencyType>>("balance",
                description: "asset balance by currency.",
                arguments: new QueryArguments(
                    new QueryArgument<NonNullGraphType<AddressType>>
                    {
                        Name = "address"
                    },
                    new QueryArgument<NonNullGraphType<CurrencyInputType>>
                    {
                        Name = "currency"
                    }
                ),
                resolve: context =>
                {
                    var address = context.GetArgument<Address>("address");
                    var currency = context.GetArgument<Currency>("currency");
                    return context.Source.GetBalance(address, currency);
                }
            );

            Field<ListGraphType<NonNullGraphType<AddressType>>>(
                "raiderList",
                description: "raider address list by world boss season.",
                arguments: new QueryArguments(new QueryArgument<NonNullGraphType<AddressType>>
                {
                    Name = "raiderListAddress"
                }),
                resolve: context =>
                {
                    var address = context.GetArgument<Address>("raiderListAddress");
                    if (context.Source.GetState(address) is List list)
                    {
                        return list.ToList(StateExtensions.ToAddress);
                    }
                    return null;
                }
            );

            Field<OrderDigestListStateType>(
                "orderDigestList",
                arguments: new QueryArguments(new QueryArgument<NonNullGraphType<AddressType>>
                {
                    Name = "avatarAddress"
                }),
                resolve: context =>
                {
                    var avatarAddress = context.GetArgument<Address>("avatarAddress");
                    var orderDigestListAddress = OrderDigestListState.DeriveAddress(avatarAddress);
                    if (context.Source.GetState(orderDigestListAddress) is Dictionary d)
                    {
                        return new OrderDigestListState(d);
                    }

                    return null;
                });
            Field<NonNullGraphType<MeadPledgeType>>(
                "pledge",
                arguments: new QueryArguments(new QueryArgument<NonNullGraphType<AddressType>>
                {
                    Name = "agentAddress"
                }),
                resolve: context =>
                {
                    var agentAddress = context.GetArgument<Address>("agentAddress");
                    var pledgeAddress = agentAddress.GetPledgeAddress();
                    Address? address = null;
                    bool approved = false;
                    int mead = 0;
                    if (context.Source.GetState(pledgeAddress) is List l)
                    {
                        address = l[0].ToAddress();
                        approved = l[1].ToBoolean();
                        mead = l[2].ToInteger();
                    }

                    return (address, approved, mead);
                }
            );

            RegisterGarages();

            Field<NonNullGraphType<ArenaInfoResultType>>(
                "arenaInfo",
                arguments: new QueryArguments(
                    new QueryArgument<NonNullGraphType<AddressType>>
                    {
                        Name = "avatarAddress"
                    },
                    new QueryArgument<NonNullGraphType<BooleanGraphType>>
                    {
                        Name = "filter",
                        DefaultValue = true,
                    }
                ),
                resolve: context =>
                {
                    var blockIndex = context.Source.BlockIndex;
                    var currentAvatarAddr = context.GetArgument<Address>("avatarAddress");
                    var filter = context.GetArgument<bool>("filter");
                    var currentRoundData = context.Source.AccountState.GetSheet<ArenaSheet>().GetRoundByBlockIndex(blockIndex);
                    var participantsAddr = ArenaParticipants.DeriveAddress(
                        currentRoundData.ChampionshipId,
                        currentRoundData.Round);
                    var participants = context.Source.GetState(participantsAddr) is List participantsList
                            ? new ArenaParticipants(participantsList)
                            : null;
                    if (participants is null)
                    {
                        return Array.Empty<ArenaParticipant>();
                    }

                    var avatarAddrList = participants.AvatarAddresses;
                    var avatarAndScoreAddrList = avatarAddrList
                        .Select(avatarAddr => (
                            avatarAddr,
                            ArenaScore.DeriveAddress(
                                avatarAddr,
                                currentRoundData.ChampionshipId,
                                currentRoundData.Round)))
                        .ToArray();
                    // NOTE: If addresses is too large, and split and get separately.
                    int playerScore = ArenaScore.ArenaScoreDefault;
                    var scores = context.Source.GetStates(
                        avatarAndScoreAddrList.Select(tuple => tuple.Item2).ToList());
                    var avatarAddrAndScores = new List<(Address avatarAddr, int score)>();
                    for (int i = 0; i < avatarAddrList.Count; i++)
                    {
                        var tuple = avatarAndScoreAddrList[i];
                        var score = scores[i] is List scoreList ? (int)(Integer)scoreList[1] : ArenaScore.ArenaScoreDefault;
                        if (tuple.avatarAddr == currentAvatarAddr)
                        {
                            playerScore = score;
                        }
                        avatarAddrAndScores.Add((tuple.avatarAddr, score));
                    }
                    List<(Address avatarAddr, int score, int rank)> orderedTuples = avatarAddrAndScores
                        .OrderByDescending(tuple => tuple.score)
                        .ThenByDescending(tuple => tuple.avatarAddr == currentAvatarAddr)
                        .ThenBy(tuple => tuple.avatarAddr)
                        .Select(tuple => (tuple.avatarAddr, tuple.score, 0))
                        .ToList();
                    int? currentScore = null;
                    var currentRank = 1;
                    var avatarAddrAndScoresWithRank = new List<(Address avatarAddr, int score, int rank)>();
                    var trunk = new List<(Address avatarAddr, int score, int rank)>();
                    for (var i = 0; i < orderedTuples.Count; i++)
                    {
                        var tuple = orderedTuples[i];
                        if (!currentScore.HasValue)
                        {
                            currentScore = tuple.score;
                            trunk.Add(tuple);
                            continue;
                        }

                        if (currentScore.Value == tuple.score)
                        {
                            trunk.Add(tuple);
                            currentRank++;
                            if (i < orderedTuples.Count - 1)
                            {
                                continue;
                            }

                            foreach (var tupleInTrunk in trunk)
                            {
                                avatarAddrAndScoresWithRank.Add((
                                    tupleInTrunk.avatarAddr,
                                    tupleInTrunk.score,
                                    currentRank));
                            }

                            trunk.Clear();

                            continue;
                        }

                        foreach (var tupleInTrunk in trunk)
                        {
                            avatarAddrAndScoresWithRank.Add((
                                tupleInTrunk.avatarAddr,
                                tupleInTrunk.score,
                                currentRank));
                        }

                        trunk.Clear();
                        if (i < orderedTuples.Count - 1)
                        {
                            trunk.Add(tuple);
                            currentScore = tuple.score;
                            currentRank++;
                            continue;
                        }

                        avatarAddrAndScoresWithRank.Add((
                            tuple.avatarAddr,
                            tuple.score,
                            currentRank + 1));
                    }
                    
                    var playerArenaInfoAddr = ArenaInformation.DeriveAddress(
                        currentAvatarAddr,
                        currentRoundData.ChampionshipId,
                        currentRoundData.Round);
                    var purchasedCountAddress =
                        playerArenaInfoAddr.Derive(BattleArena.PurchasedCountKey);
                    var arenaAvatarAddress =
                        ArenaAvatarState.DeriveAddress(currentAvatarAddr);

                    var runeListSheet = context.Source.AccountState.GetSheet<RuneListSheet>();
                    var costumeSheet = context.Source.AccountState.GetSheet<CostumeStatSheet>();
                    var characterSheet = context.Source.AccountState.GetSheet<CharacterSheet>();
                    var runeOptionSheet = context.Source.AccountState.GetSheet<RuneOptionSheet>();
                    var runeIds = runeListSheet.Values.Select(x => x.Id).ToList();
                    var row = characterSheet[GameConfig.DefaultAvatarCharacterId];
                    var addrBulk = avatarAddrAndScoresWithRank
                        .SelectMany(tuple => new[]
                        {
                            tuple.avatarAddr,
                            tuple.avatarAddr.Derive(LegacyInventoryKey),
                            ItemSlotState.DeriveAddress(tuple.avatarAddr, BattleType.Arena),
                            RuneSlotState.DeriveAddress(tuple.avatarAddr, BattleType.Arena),
                        })
                        .ToList();

                    foreach (var tuple in avatarAddrAndScoresWithRank)
                    {
                        addrBulk.AddRange(runeIds.Select(x => RuneState.DeriveAddress(tuple.avatarAddr, x)));
                    }

                    addrBulk.Add(playerArenaInfoAddr);
                    addrBulk.Add(purchasedCountAddress);
                    addrBulk.Add(arenaAvatarAddress);
                    // NOTE: If the [`addrBulk`] is too large, and split and get separately.
                    var states = context.Source.GetStates(addrBulk);
                    var stateBulk = new Dictionary<Address, IValue>();
                    for (int i = 0; i < addrBulk.Count; i++)
                    {
                        var address = addrBulk[i];
                        var value = states[i];
                        stateBulk.TryAdd(address, value ?? Null.Value);
                    }
                    var runeStates = new List<RuneState>();
                    var result = avatarAddrAndScoresWithRank.Select(tuple =>
                    {
                        var (avatarAddr, score, rank) = tuple;
                        var avatar = new AvatarState((Dictionary) stateBulk[avatarAddr]);
                        if (stateBulk[avatarAddr.Derive(LegacyInventoryKey)] is List inventoryList)
                        {
                            var inventory = new Inventory(inventoryList);
                            avatar.inventory = inventory;
                        }

                        var itemSlotState =
                            stateBulk[ItemSlotState.DeriveAddress(avatarAddr, BattleType.Arena)] is
                                List itemSlotList
                                ? new ItemSlotState(itemSlotList)
                                : new ItemSlotState(BattleType.Arena);

                        var runeSlotState =
                            stateBulk[RuneSlotState.DeriveAddress(avatarAddr, BattleType.Arena)] is
                                List runeSlotList
                                ? new RuneSlotState(runeSlotList)
                                : new RuneSlotState(BattleType.Arena);

                        runeStates.Clear();
                        foreach (var id in runeIds)
                        {
                            var address = RuneState.DeriveAddress(avatarAddr, id);
                            if (stateBulk[address] is List runeStateList)
                            {
                                runeStates.Add(new RuneState(runeStateList));
                            }
                        }

                        var equippedRuneStates = new List<RuneState>();
                        foreach (var runeId in runeSlotState.GetRuneSlot().Select(slot => slot.RuneId))
                        {
                            if (!runeId.HasValue)
                            {
                                continue;
                            }

                            var runeState = runeStates.FirstOrDefault(x => x.RuneId == runeId);
                            if (runeState != null)
                            {
                                equippedRuneStates.Add(runeState);
                            }
                        }

                        var (win, lose, _) =
                            ArenaHelper.GetScores(playerScore, score);
                        var equipments = itemSlotState.Equipments
                            .Select(guid =>
                                avatar.inventory.Equipments.FirstOrDefault(x => x.ItemId == guid))
                            .Where(item => item != null).ToList();
                        var costumes = itemSlotState.Costumes
                            .Select(guid =>
                                avatar.inventory.Costumes.FirstOrDefault(x => x.ItemId == guid))
                            .Where(item => item != null).ToList();
                        var runeOptions = GetRuneOptions(equippedRuneStates, runeOptionSheet);
                        var cp = CPHelper.TotalCP(equipments, costumes, runeOptions, avatar.level, row, costumeSheet);
                        return new ArenaParticipant(
                            avatarAddr,
                            avatarAddr.Equals(currentAvatarAddr)
                                ? playerScore
                                : score,
                            rank,
                            avatar,
                            (win, lose),
                            cp
                        );
                    }).ToList();
                    
                    var purchasedCountDuringInterval = stateBulk[purchasedCountAddress] is Integer iValue
                        ? (int)iValue
                        : 0;
                    var arenaAvatarState = stateBulk[arenaAvatarAddress] is List iValue2
                        ? new ArenaAvatarState(iValue2)
                        : null;
                    long lastBattleBlockIndex = arenaAvatarState?.LastBattleBlockIndex ?? 0L;
                    if (filter)
                    {
                        result = GetBoundsWithPlayerScore(result, currentRoundData.ArenaType, playerScore);
                    }
                    return (result, purchasedCountDuringInterval, lastBattleBlockIndex);
                }
            );
        }
        
        public static List<RuneOptionSheet.Row.RuneOptionInfo> GetRuneOptions(
            List<RuneState> runeStates,
            RuneOptionSheet sheet)
        {
            var result = new List<RuneOptionSheet.Row.RuneOptionInfo>();
            foreach (var runeState in runeStates)
            {
                if (!sheet.TryGetValue(runeState.RuneId, out var row))
                {
                    continue;
                }

                if (!row.LevelOptionMap.TryGetValue(runeState.Level, out var statInfo))
                {
                    continue;
                }

                result.Add(statInfo);
            }

            return result;
        }
        
        private static List<ArenaParticipant> GetBoundsWithPlayerScore(
            List<ArenaParticipant> arenaInformation,
            ArenaType arenaType,
            int playerScore)
        {
            var bounds = ArenaHelper.ScoreLimits.ContainsKey(arenaType)
                ? ArenaHelper.ScoreLimits[arenaType]
                : ArenaHelper.ScoreLimits.First().Value;

            bounds = (bounds.upper + playerScore, bounds.lower + playerScore);
            return arenaInformation
                .Where(a => a.Score <= bounds.upper && a.Score >= bounds.lower)
                .ToList();
        }
    }
}
