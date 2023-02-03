using Libplanet;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NineChronicles.Headless.GraphTypes
{
    internal class DailyRewardStatus
    {
        public long lastRewardIndex { get; }
        public int actionPoint { get; }
        public Address avatarAddress { get; }

        public DailyRewardStatus(long lastRewardIndex, int actionPoint, Address avatarAddress)
        {
            this.lastRewardIndex= lastRewardIndex;
            this.actionPoint= actionPoint;
            this.avatarAddress = avatarAddress;
        }
    }
}
