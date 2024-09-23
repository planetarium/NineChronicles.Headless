using System;
using System.Collections.Generic;
using System.Linq;
using Lib9c.Tests;
using Libplanet.Action.State;
using Libplanet.Crypto;
using Libplanet.Mocks;
using Nekoyume;
using Nekoyume.Action;
using Nekoyume.Model.Arena;
using Nekoyume.Model.EnumType;
using Nekoyume.Model.Item;
using Nekoyume.Model.State;
using Nekoyume.Module;
using Nekoyume.TableData;
using Xunit;
using Random = Libplanet.Extensions.ActionEvaluatorCommonComponents.Random;

namespace NineChronicles.Headless.Tests;

public class ArenaParticipantsWorkerTest
{
    private readonly IWorld _world;
    private readonly Dictionary<string, string> _sheets;

    public ArenaParticipantsWorkerTest()
    {
        _world = new World(MockWorldState.CreateModern());
        _sheets = TableSheetsImporter.ImportSheets();
    }

    [Fact]
    public void GetRoundData()
    {
        var sheetAddress = Addresses.GetSheetAddress<ArenaSheet>();
        var csv = _sheets[nameof(ArenaSheet)];
        var arenaSheet = new ArenaSheet();
        arenaSheet.Set(csv);
        var row = arenaSheet.OrderedList.First();
        var expectedRound = row.Round.First();
        var blockIndex = expectedRound.StartBlockIndex;
        var state = _world.SetLegacyState(sheetAddress, csv.Serialize());
        var current = ArenaParticipantsWorker.GetRoundData(state, blockIndex);
        Assert.Equal(expectedRound.Round, current.Round);
        Assert.Equal(expectedRound.ChampionshipId, current.ChampionshipId);
    }

    [Fact]
    public void GetArenaParticipantsState()
    {
        var sheetAddress = Addresses.GetSheetAddress<ArenaSheet>();
        var csv = _sheets[nameof(ArenaSheet)];
        var arenaSheet = new ArenaSheet();
        arenaSheet.Set(csv);
        var row = arenaSheet.OrderedList.First();
        var currentRoundData = row.Round.First();
        var state = _world.SetLegacyState(sheetAddress, csv.Serialize());
        Assert.Null(ArenaParticipantsWorker.GetArenaParticipantsState(state, currentRoundData));

        var participantsAddr = ArenaParticipants.DeriveAddress(
            currentRoundData.ChampionshipId,
            currentRoundData.Round);
        var expected = new ArenaParticipants(currentRoundData.ChampionshipId, currentRoundData.Round);
        var avatarAddress = new PrivateKey().Address;
        expected.AvatarAddresses.Add(avatarAddress);
        state = state.SetLegacyState(participantsAddr, expected.Serialize());
        var actual = ArenaParticipantsWorker.GetArenaParticipantsState(state, currentRoundData);
        Assert.NotNull(actual);
        Assert.Equal(expected.Serialize(), actual.Serialize());
    }

    [Fact]
    public void AvatarAddrAndScoresWithRank()
    {
        var sheetAddress = Addresses.GetSheetAddress<ArenaSheet>();
        var csv = _sheets[nameof(ArenaSheet)];
        var arenaSheet = new ArenaSheet();
        arenaSheet.Set(csv);
        var row = arenaSheet.OrderedList.First();
        var currentRoundData = row.Round.First();
        var championshipId = currentRoundData.ChampionshipId;
        var round = currentRoundData.Round;
        var participantsAddr = ArenaParticipants.DeriveAddress(championshipId, round);
        var participants = new ArenaParticipants(championshipId, round);
        var avatarAddress = new PrivateKey().Address;
        var avatar2Address = new PrivateKey().Address;
        participants.AvatarAddresses.Add(avatarAddress);
        participants.AvatarAddresses.Add(avatar2Address);
        var arenaScore = new ArenaScore(avatarAddress, championshipId, round);
        arenaScore.AddScore(10);
        var state = _world
            .SetLegacyState(sheetAddress, csv.Serialize())
            .SetLegacyState(participantsAddr, participants.Serialize())
            .SetLegacyState(arenaScore.Address, arenaScore.Serialize());
        var actual =
            ArenaParticipantsWorker.AvatarAddrAndScoresWithRank(participants.AvatarAddresses, currentRoundData, state);
        Assert.Equal(2, actual.Count);
        var first = actual.First();
        Assert.Equal(avatarAddress, first.avatarAddr);
        Assert.Equal(1010, first.score);
        Assert.Equal(1, first.rank);
        var second = actual.Last();
        Assert.Equal(avatar2Address, second.avatarAddr);
        Assert.Equal(1000, second.score);
        Assert.Equal(2, second.rank);
    }

    [Fact]
    public void GetArenaParticipants()
    {
        var tableSheets = new TableSheets(_sheets);
        var agentAddress = new PrivateKey().Address;
        var avatarAddress = Addresses.GetAvatarAddress(agentAddress, 0);
        var avatarState = AvatarState.Create(
            avatarAddress,
            agentAddress,
            0,
            tableSheets.GetAvatarSheets(),
            new Address(),
            "avatar_state"
        );
        var avatar2Address = Addresses.GetAvatarAddress(agentAddress, 1);
        var avatarState2 = AvatarState.Create(
            avatar2Address,
            agentAddress,
            0,
            tableSheets.GetAvatarSheets(),
            new Address(),
            "avatar_state2"
        );

        // equipment
        var equipmentSheet = tableSheets.EquipmentItemSheet;
        var random = new Random(0);
        var equipment =
            (Equipment)ItemFactory.CreateItem(equipmentSheet.Values.First(r => r.ItemSubType == ItemSubType.Armor),
                random);
        equipment.equipped = true;
        avatarState.inventory.AddItem(equipment);
        avatarState2.inventory.AddItem(equipment);
        var itemSlotState = new ItemSlotState(BattleType.Arena);
        var itemSlotAddress = ItemSlotState.DeriveAddress(avatarAddress, BattleType.Arena);
        itemSlotState.UpdateEquipment(new List<Guid>
        {
            equipment.ItemId,
        });

        // rune
        var runeListSheet = tableSheets.RuneListSheet;
        var runeId = runeListSheet.Values.First().Id;
        var runeSlotState = new RuneSlotState(BattleType.Arena);
        var runeSlotAddress = RuneSlotState.DeriveAddress(avatarAddress, BattleType.Arena);
        var runeSlotInfo = new RuneSlotInfo(0, runeId);
        runeSlotState.UpdateSlot(new List<RuneSlotInfo>
        {
            runeSlotInfo,
        }, runeListSheet);
        var runeStates = new AllRuneState(runeId);

        // collection
        var collectionSheet = tableSheets.CollectionSheet;
        var collectionState = new CollectionState();
        collectionState.Ids.Add(collectionSheet.Values.First().Id);
        var arenaSheet = tableSheets.ArenaSheet;
        var row = arenaSheet.OrderedList.First();
        var currentRoundData = row.Round.First();
        var championshipId = currentRoundData.ChampionshipId;
        var round = currentRoundData.Round;
        var participantsAddr = ArenaParticipants.DeriveAddress(championshipId, round);
        var participants = new ArenaParticipants(championshipId, round);
        participants.AvatarAddresses.Add(avatarAddress);
        participants.AvatarAddresses.Add(avatar2Address);
        var arenaScore = new ArenaScore(avatarAddress, championshipId, round);
        arenaScore.AddScore(10);
        var state = _world
            .SetAvatarState(avatarAddress, avatarState, true, true, true, true)
            .SetAvatarState(avatar2Address, avatarState2, true, true, true, true)
            .SetLegacyState(itemSlotAddress, itemSlotState.Serialize())
            .SetRuneState(avatarAddress, runeStates)
            .SetLegacyState(runeSlotAddress, runeSlotState.Serialize())
            .SetCollectionState(avatar2Address, collectionState)
            .SetLegacyState(participantsAddr, participants.Serialize())
            .SetLegacyState(arenaScore.Address, arenaScore.Serialize());
        foreach (var (key, s) in _sheets)
        {
            state = state.SetLegacyState(Addresses.GetSheetAddress(key), s.Serialize());
        }

        var avatarAddrAndScoresWithRank =
            ArenaParticipantsWorker.AvatarAddrAndScoresWithRank(participants.AvatarAddresses, currentRoundData, state);
        var actual =
            ArenaParticipantsWorker.GetArenaParticipants(state, participants.AvatarAddresses,
                avatarAddrAndScoresWithRank);
        Assert.Equal(2, actual.Count);
        var first = actual.First();
        Assert.Equal(avatarAddress, first.AvatarAddr);
        Assert.Equal(1010, first.Score);
        Assert.Equal(1, first.Rank);
        Assert.Equal(equipment.Id, first.PortraitId);
        var second = actual.Last();
        Assert.Equal(avatar2Address, second.AvatarAddr);
        Assert.Equal(1000, second.Score);
        Assert.Equal(2, second.Rank);
        Assert.Equal(GameConfig.DefaultAvatarArmorId, second.PortraitId);
        Assert.True(first.Cp < second.Cp);
    }
}
