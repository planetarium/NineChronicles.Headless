using System.Collections.Generic;

namespace NineChronicles.Headless.GraphTypes.States.Models.WorldOpen
{
    public class WorldOpenConditionGroupInfo
    {
        public bool Satisfied { get; set; }
        public List<WorldOpenConditionInfo> Conditions { get; set; } =
            new List<WorldOpenConditionInfo>();
    }
}
