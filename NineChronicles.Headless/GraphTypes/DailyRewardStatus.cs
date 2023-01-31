using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NineChronicles.Headless.GraphTypes
{
    internal class DailyRewardStatus
    {
        public List<long> lastRewardIndex { get; }
        public List<int> actionPoint { get; }

        public DailyRewardStatus(List<long> lastRwardIndex, List<int> actionPoint)
        {
            lastRewardIndex= lastRwardIndex;
            this.actionPoint= actionPoint;
        }
    }
}
