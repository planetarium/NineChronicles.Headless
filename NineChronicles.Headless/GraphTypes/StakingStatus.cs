namespace NineChronicles.Headless.GraphTypes
{
    public class StakingStatus
    {
        public bool CanReceive { get; }

        public StakingStatus(bool canReceive)
        {
            CanReceive = canReceive;
        }
    }
}
