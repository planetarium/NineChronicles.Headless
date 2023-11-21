using Libplanet.Crypto;
using Nekoyume.Model.State;

namespace NineChronicles.Headless;

public class ArenaParticipant
{
    public readonly Address AvatarAddr;
    public readonly int Score;
    public readonly int Rank;
    public int WinScore;
    public int LoseScore;
    public readonly int Cp;
    public readonly int PortraitId;
    public readonly string NameWithHash;
    public readonly int Level;

    public ArenaParticipant(
        Address avatarAddr,
        int score,
        int rank,
        AvatarState avatarState,
        int portraitId,
        int winScore,
        int loseScore,
        int cp)
    {
        AvatarAddr = avatarAddr;
        Score = score;
        Rank = rank;
        WinScore = winScore;
        LoseScore = loseScore;
        Cp = cp;
        PortraitId = portraitId;
        NameWithHash = avatarState.NameWithHash;
        Level = avatarState.level;
    }
}
