using Libplanet;

namespace NineChronicles.Headless.GraphTypes.States
{
    public class ChampionArenaInfo
    {
        public Address AvatarAddress;
        public Address AgentAddress;
        public string? AvatarName;
        public int Win { get; set; }
        public int Lose { get; set; }
        public int Ticket { get; set; }
        public int TicketResetCount { get; set; }
        public int PurchasedTicketCount { get; set; }
        public int Score { get; set; }
        public bool Active { get; set; }
        public int Rank { get; set; }
    }
}
