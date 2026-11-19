using System.Collections.Generic;

namespace NineChronicles.Headless.GraphTypes.States.Models.WorldOpen
{
    public class WorldOpenResult
    {
        public long EvaluatedBlockIndex { get; set; }
        public List<string> Errors { get; set; } = new List<string>();
        public List<WorldOpenWorldInfo> Worlds { get; set; } = new List<WorldOpenWorldInfo>();
        public List<WorldOpenEntryInfo> Opens { get; set; } = new List<WorldOpenEntryInfo>();
    }
}
