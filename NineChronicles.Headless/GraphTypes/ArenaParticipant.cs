using System.Collections.Generic;
using Libplanet.Crypto;
using Nekoyume.Model.State;

namespace NineChronicles.Headless.GraphTypes;

public class ArenaParticipant
{
    public readonly Address AvatarAddr;
    public readonly int Score;
    public readonly int Rank;
    public readonly AvatarState? AvatarState;
    public readonly ItemSlotState ItemSlotState;
    public readonly RuneSlotState RuneSlotState;
    #pragma warning disable S3887
    public readonly List<RuneState> RuneStates;
    #pragma warning restore
    public readonly (int win, int lose) ExpectDeltaScore;

    public ArenaParticipant(
        Address avatarAddr,
        int score,
        int rank,
        AvatarState? avatarState,
        ItemSlotState itemSlotState,
        RuneSlotState runeSlotState,
        List<RuneState> runeStates,
        (int win, int lose) expectDeltaScore)
    {
        AvatarAddr = avatarAddr;
        Score = score;
        Rank = rank;
        AvatarState = avatarState;
        ItemSlotState = itemSlotState;
        RuneSlotState = runeSlotState;
        RuneStates = runeStates;
        ExpectDeltaScore = expectDeltaScore;
    }
}
