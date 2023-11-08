using System.Collections.Generic;
using System.Linq;
using Libplanet.Crypto;
using Nekoyume.Model.Item;
using Nekoyume.Model.State;

namespace NineChronicles.Headless.GraphTypes;

public class ArenaParticipant
{
    public readonly Address AvatarAddr;
    public readonly int Score;
    public readonly int Rank;
    public readonly (int win, int lose) ExpectDeltaScore;
    public readonly int Cp;
    public readonly int ArmorId;
    public readonly string NameWithHash;
    public readonly int Level;

    public ArenaParticipant(
        Address avatarAddr,
        int score,
        int rank,
        AvatarState avatarState,
        int armorId,
        (int win, int lose) expectDeltaScore,
        int cp)
    {
        AvatarAddr = avatarAddr;
        Score = score;
        Rank = rank;
        ExpectDeltaScore = expectDeltaScore;
        Cp = cp;
        ArmorId = armorId;
        NameWithHash = avatarState.NameWithHash;
        Level = avatarState.level;
    }
}
