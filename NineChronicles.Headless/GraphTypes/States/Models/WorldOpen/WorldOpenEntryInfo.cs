using System.Collections.Generic;

namespace NineChronicles.Headless.GraphTypes.States.Models.WorldOpen
{
    public class WorldOpenEntryInfo
    {
        public int Id { get; set; }
        public int WorldId { get; set; }
        public int StageEnd { get; set; }
        public int? PrerequisiteOpenId { get; set; }
        public long MinBlockIndex { get; set; }
        public long FallbackBlockIndex { get; set; }
        public long EffectiveDelayBlock { get; set; }
        public long? ConfirmedBlockIndex { get; set; }
        public long? AppliedBlockIndex { get; set; }
        public bool Applied { get; set; }
        public bool Pending { get; set; }
        public List<WorldOpenConditionGroupInfo> ConditionGroups { get; set; } =
            new List<WorldOpenConditionGroupInfo>();
    }
}
