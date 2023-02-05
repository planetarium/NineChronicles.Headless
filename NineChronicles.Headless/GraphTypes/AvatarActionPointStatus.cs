using Libplanet;
using Nekoyume.Model.Item;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NineChronicles.Headless.GraphTypes
{
    internal class AvatarActionPointStatus
    {
        public long blockIndex { get; }
        public int actionPoint { get; }
        public long experience { get; }
        public int level { get; }
        public Address avatarAddress { get; }
        public Inventory inventory { get; }

        public AvatarActionPointStatus(long blockIndex, int actionPoint, Address avatarAddress, long exp, int level, Inventory inventory)
        {
            this.blockIndex = blockIndex;
            this.actionPoint = actionPoint;
            this.avatarAddress = avatarAddress;
            experience = exp;
            this.level = level;
            this.inventory = inventory;
        }
    }
}
