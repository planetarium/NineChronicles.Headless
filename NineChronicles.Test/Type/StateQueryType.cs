namespace NineChronicles.Test.Type;

public class StateQueryResponseType
{
    public StateQueryType StateQuery { get; set; }
}

public class StateQueryType
{
    public AgentType? Agent { get; set; }
}

public class AgentType
{
    public Libplanet.Address address { get; set; }
    public AvatarStateType[] AvatarStates { get; set; }
    public string Gold { get; set; }
    public string Crystal { get; set; }
    public long monsterCollectionRound { get; set; }
    public long monsterCollectionLevel { get; set; }
    public bool hasTradeItem { get; set; }
}

public class AvatarStateType
{
    public Libplanet.Address Address { get; set; }
    public int BlockIndex { get; set; }
    public int CharacterId { get; set; }
    public long DailyRewardReceivedIndex { get; set; }
    public Libplanet.Address AgentAddress { get; set; }
    public int Index { get; set; }
    public long UpdatedAt { get; set; }
    public string Name { get; set; }
    public int Exp { get; set; }
    public int Level { get; set; }
    public int ActionPoint { get; set; }
    public int Ear { get; set; }
    public int Hair { get; set; }
    public int Lens { get; set; }
    public int Tail { get; set; }
    // // Inventory
    public Libplanet.Address[] CombinationSlotAddresses { get; set; }
    // // ItemMap
    // // EventMap
    // // MonsterMap
    // // StageMap
    // // QuestList
    // // MailBox
    // // WorldInformation
}
