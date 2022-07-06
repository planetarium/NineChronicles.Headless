using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using Bencodex;
using Bencodex.Types;
using Libplanet;
using Nekoyume.Action;
using Nekoyume.TableData;

namespace NineChronicles.Headless.GraphTypes.States
{
    public class ChampionshipArenaState
    {
        public Address Address;
        public long StartIndex;
        public long EndIndex;
        public List<ChampionArenaInfo> OrderedArenaInfos { get; set; }

        public ChampionshipArenaState()
        {
            OrderedArenaInfos = new List<ChampionArenaInfo>();
        }

    }
}
