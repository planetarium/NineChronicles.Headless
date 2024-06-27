using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Bencodex.Types;
using Libplanet.Action.State;
using Libplanet.Crypto;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Hosting;
using Nekoyume;
using Nekoyume.Battle;
using Nekoyume.Helper;
using Nekoyume.Model.Arena;
using Nekoyume.Model.EnumType;
using Nekoyume.Model.Stat;
using Nekoyume.Model.State;
using Nekoyume.Module;
using Nekoyume.TableData;
using Nekoyume.TableData.Rune;
using NineChronicles.Headless.GraphTypes;
using Serilog;

namespace NineChronicles.Headless;

public class ArenaParticipantsWorker : BackgroundService
{
    private ILogger _logger;

    private StateMemoryCache _cache;

    private StandaloneContext _context;

    private int _interval;

    public ArenaParticipantsWorker(StateMemoryCache memoryCache, StandaloneContext context, int interval)
    {
        _cache = memoryCache;
        _context = context;
        _logger = Log.Logger.ForContext<ArenaParticipantsWorker>();
        _interval = interval;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                await Task.Delay(_interval, stoppingToken);
                PrepareArenaParticipants();
            }
        }
        catch (OperationCanceledException)
        {
            //pass
            _logger.Information("[ArenaParticipantsWorker]Cancel ArenaParticipantsWorker");
        }
        catch (Exception e)
        {
            _logger.Error(e, "[ArenaParticipantsWorker]Stopping ArenaParticipantsWorker");
            await StopAsync(stoppingToken);
        }
    }

    /// <summary>
    /// Retrieves the state of arena participants from the given world state and current round data.
    /// </summary>
    /// <param name="worldState">The world state.</param>
    /// <param name="currentRoundData">The current round data.</param>
    /// <returns>The arena participants state, or null if not found.</returns>
    public static ArenaParticipants? GetArenaParticipantsState(IWorldState worldState, ArenaSheet.RoundData currentRoundData)
    {
        var participantsAddr = ArenaParticipants.DeriveAddress(
            currentRoundData.ChampionshipId,
            currentRoundData.Round);
        var participants = worldState.GetLegacyState(participantsAddr) is List participantsList
            ? new ArenaParticipants(participantsList)
            : null;
        return participants;
    }

    /// <summary>
    /// Gets the round data from the specified world state and block index.
    /// </summary>
    /// <param name="worldState">The world state containing the arena sheet.</param>
    /// <param name="blockIndex">The block index for which to retrieve the round data.</param>
    /// <returns>The round data for the specified block index.</returns>
    public static ArenaSheet.RoundData GetRoundData(IWorldState worldState, long blockIndex)
    {
        return worldState.GetSheet<ArenaSheet>().GetRoundByBlockIndex(blockIndex);
    }

    /// <summary>
    /// Retrieves the avatar addresses and scores with ranks for a given list of avatar addresses, current round data, and world state.
    /// </summary>
    /// <param name="avatarAddrList">The list of avatar addresses.</param>
    /// <param name="currentRoundData">The current round data.</param>
    /// <param name="worldState">The world state.</param>
    /// <returns>The list of avatar addresses, scores, and ranks.</returns>
    public static List<(Address avatarAddr, int score, int rank)> AvatarAddrAndScoresWithRank(List<Address> avatarAddrList, ArenaSheet.RoundData currentRoundData, IWorldState worldState)
    {
        var avatarAndScoreAddrList = avatarAddrList
            .Select(avatarAddr => (
                avatarAddr,
                ArenaScore.DeriveAddress(
                    avatarAddr,
                    currentRoundData.ChampionshipId,
                    currentRoundData.Round)))
            .ToArray();
        // NOTE: If addresses is too large, and split and get separately.
        var scores = worldState.GetLegacyStates(
            avatarAndScoreAddrList.Select(tuple => tuple.Item2).ToList());
        var avatarAddrAndScores = new List<(Address avatarAddr, int score)>();
        for (int i = 0; i < avatarAddrList.Count; i++)
        {
            var tuple = avatarAndScoreAddrList[i];
            var score = scores[i] is List scoreList ? (int)(Integer)scoreList[1] : ArenaScore.ArenaScoreDefault;
            avatarAddrAndScores.Add((tuple.avatarAddr, score));
        }

        List<(Address avatarAddr, int score, int rank)> orderedTuples = avatarAddrAndScores
            .OrderByDescending(tuple => tuple.score)
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

        return avatarAddrAndScoresWithRank;
    }

    /// <summary>
    /// Retrieve a list of arena participants based on the provided world state, avatar address list, and avatar addresses with scores and ranks.
    /// </summary>
    /// <param name="worldState">The world state from which to retrieve the arena participants.</param>
    /// <param name="avatarAddrList">The list of avatar addresses to filter the matching participants.</param>
    /// <param name="avatarAddrAndScoresWithRank">The list of avatar addresses with their scores and ranks.</param>
    /// <returns>A list of arena participants.</returns>
    public static List<ArenaParticipant> GetArenaParticipants(IWorldState worldState, List<Address> avatarAddrList, List<(Address avatarAddr, int score, int rank)> avatarAddrAndScoresWithRank)
    {
        var runeListSheet = worldState.GetSheet<RuneListSheet>();
        var costumeSheet = worldState.GetSheet<CostumeStatSheet>();
        var characterSheet = worldState.GetSheet<CharacterSheet>();
        var runeOptionSheet = worldState.GetSheet<RuneOptionSheet>();
        var runeIds = runeListSheet.Values.Select(x => x.Id).ToList();
        var row = characterSheet[GameConfig.DefaultAvatarCharacterId];
        CollectionSheet collectionSheet = new CollectionSheet();
        var collectionStates = worldState.GetCollectionStates(avatarAddrList);
        bool collectionSheetExist = true;
        try
        {
            collectionSheet = worldState.GetSheet<CollectionSheet>();
        }
        catch (Exception)
        {
            collectionSheetExist = false;
        }

        var result = avatarAddrAndScoresWithRank.Select(tuple =>
        {
            var (avatarAddr, score, rank) = tuple;
            var runeStates = worldState.GetRuneState(avatarAddr, out _);
            var avatar = worldState.GetAvatarState(avatarAddr, getWorldInformation: false, getQuestList: false);
            var itemSlotState =
                worldState.GetLegacyState(ItemSlotState.DeriveAddress(avatarAddr, BattleType.Arena)) is
                    List itemSlotList
                    ? new ItemSlotState(itemSlotList)
                    : new ItemSlotState(BattleType.Arena);

            var runeSlotState =
                worldState.GetLegacyState(RuneSlotState.DeriveAddress(avatarAddr, BattleType.Arena)) is
                    List runeSlotList
                    ? new RuneSlotState(runeSlotList)
                    : new RuneSlotState(BattleType.Arena);

            var equippedRuneStates = new List<RuneState>();
            foreach (var runeId in runeSlotState.GetRuneSlot().Select(slot => slot.RuneId))
            {
                if (!runeId.HasValue)
                {
                    continue;
                }

                if (runeStates.TryGetRuneState(runeId.Value, out var runeState))
                {
                    equippedRuneStates.Add(runeState);
                }
            }

            var equipments = itemSlotState.Equipments
                .Select(guid =>
                    avatar.inventory.Equipments.FirstOrDefault(x => x.ItemId == guid))
                .Where(item => item != null).ToList();
            var costumes = itemSlotState.Costumes
                .Select(guid =>
                    avatar.inventory.Costumes.FirstOrDefault(x => x.ItemId == guid))
                .Where(item => item != null).ToList();
            var runeOptions = StateQuery.GetRuneOptions(equippedRuneStates, runeOptionSheet);
            var collectionExist = collectionStates.ContainsKey(avatarAddr);
            var collectionModifiers = new List<StatModifier>();
            if (collectionSheetExist && collectionExist)
            {
                var collectionState = collectionStates[avatarAddr];
                foreach (var collectionId in collectionState.Ids)
                {
                    collectionModifiers.AddRange(collectionSheet[collectionId].StatModifiers);
                }
            }

            var cp = CPHelper.TotalCP(equipments, costumes, runeOptions, avatar.level, row, costumeSheet, collectionModifiers,
                RuneHelper.CalculateRuneLevelBonus(runeStates, runeListSheet, worldState.GetSheet<RuneLevelBonusSheet>())
            );
            var portraitId = StateQuery.GetPortraitId(equipments, costumes);
            return new ArenaParticipant(
                avatarAddr,
                score,
                rank,
                avatar,
                portraitId,
                0,
                0,
                cp
            );
        }).ToList();
        return result;
    }

    /// <summary>
    /// Prepares the arena participants by syncing the arena cache.
    /// </summary>
    public void PrepareArenaParticipants()
    {
        _logger.Information("[ArenaParticipantsWorker]Start Sync Arena Cache");
        var sw = new Stopwatch();
        sw.Start();
        // Copy from NineChronicles RxProps.Arena
        // https://github.com/planetarium/NineChronicles/blob/80.0.1/nekoyume/Assets/_Scripts/State/RxProps.Arena.cs#L279
        var blockChain = _context.BlockChain;
        if (blockChain is null)
        {
            _logger.Warning("[ArenaParticipantsWorker]BlockChain is null");
            throw new Exception();
        }

        var tip = blockChain.Tip;
        var blockIndex = blockChain.Tip.Index;
        var worldState = blockChain.GetWorldState(tip.Hash);
        var currentRoundData = GetRoundData(worldState, blockIndex);
        var participants = GetArenaParticipantsState(worldState, currentRoundData);
        var cacheKey = $"{currentRoundData.ChampionshipId}_{currentRoundData.Round}";
        if (participants is null)
        {
            _cache.ArenaParticipantsCache.Set(cacheKey, new List<ArenaParticipant>());
            _logger.Information("[ArenaParticipantsWorker] participants({CacheKey}) is null. set empty list", cacheKey);
            return;
        }

        var avatarAddrList = participants.AvatarAddresses;
        var avatarAddrAndScoresWithRank = AvatarAddrAndScoresWithRank(avatarAddrList, currentRoundData, worldState);
        var result = GetArenaParticipants(worldState, avatarAddrList, avatarAddrAndScoresWithRank);
        _cache.ArenaParticipantsCache.Set(cacheKey, result, TimeSpan.FromHours(1));
        sw.Stop();
        _logger.Information("[ArenaParticipantsWorker]Set Arena Cache[{CacheKey}]: {Elapsed}", cacheKey, sw.Elapsed);
    }
}
