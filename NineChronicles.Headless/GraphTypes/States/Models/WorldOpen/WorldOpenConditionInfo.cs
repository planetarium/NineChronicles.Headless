namespace NineChronicles.Headless.GraphTypes.States.Models.WorldOpen
{
    public class WorldOpenConditionInfo
    {
        public int CounterId { get; set; }
        // 카운터 시트에 행이 없으면 아래 네 값은 null.
        public string? Type { get; set; }
        public int? TargetBegin { get; set; }
        public int? TargetEnd { get; set; }
        public int? MinAvatarLevel { get; set; }
        public long Current { get; set; }
        public long Threshold { get; set; }
    }
}
