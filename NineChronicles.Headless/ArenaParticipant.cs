using Lib9c.Formatters;
using Libplanet.Crypto;
using MessagePack;
using Nekoyume.Model.State;

namespace NineChronicles.Headless;

[MessagePackObject]
public class ArenaParticipant
{
    [Key(0)]
    [MessagePackFormatter(typeof(AddressFormatter))]
    public readonly Address AvatarAddr;
    [Key(1)]
    public readonly int Score;
    [Key(2)]
    public readonly int Rank;
    [Key(3)]
    public readonly string NameWithHash;
    [Key(4)]
    public readonly int Level;
    [Key(5)]
    public readonly int PortraitId;
    [Key(6)]
    public int WinScore;
    [Key(7)]
    public int LoseScore;
    [Key(8)]
    public readonly int Cp;

    [SerializationConstructor]
    public ArenaParticipant(
        Address avatarAddr,
        int score,
        int rank,
        string nameWithHash,
        int level,
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
        NameWithHash = nameWithHash;
        Level = level;
    }
}
