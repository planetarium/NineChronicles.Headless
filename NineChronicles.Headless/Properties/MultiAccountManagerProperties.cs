namespace NineChronicles.Headless.Properties
{
    public class MultiAccountManagerProperties
    {
        public bool EnableManaging { get; set; }

        public int ManagementTimeMinutes { get; set; } = 10;

        public int TxIntervalMinutes { get; set; } = 10;

        public int ThresholdCount { get; set; } = 50;
    }
}
