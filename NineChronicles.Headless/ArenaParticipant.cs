using Libplanet.Crypto;
using Nekoyume.Model.State;

namespace NineChronicles.Headless;

public class ArenaParticipant
{
    public Address AvatarAddr { get; set; }
    public int Score { get; set; }
    public int Rank { get; set; }
    public int WinScore { get; set; }
    public int LoseScore { get; set; }
    public int Cp { get; set; }
    public int PortraitId { get; set; }
    public string NameWithHash { get; set; } = "";
    public int Level { get; set; }

    public ArenaParticipant()
    {
    }

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
