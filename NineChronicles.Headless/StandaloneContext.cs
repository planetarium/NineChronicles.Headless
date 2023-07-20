using System;
using System.Collections.Concurrent;
using System.Reactive.Subjects;
using Libplanet.Crypto;
using Libplanet.Blockchain;
using Libplanet.KeyStore;
using Libplanet.Net;
using Libplanet.Headless;
using Libplanet.Store;
using Nekoyume.Model.State;
using NineChronicles.Headless.GraphTypes;
using NineChronicles.Headless.Utils;

namespace NineChronicles.Headless
{
    public class StandaloneContext
    {
        public BlockChain? BlockChain { get; set; }
        public IKeyStore? KeyStore { get; set; }
        public bool BootstrapEnded { get; set; }
        public bool PreloadEnded { get; set; }
        public bool IsMining { get; set; }
        public ReplaySubject<NodeStatusType> NodeStatusSubject { get; } = new(1);
        public ReplaySubject<BlockSyncState> PreloadStateSubject { get; } = new(5);

        public Subject<DifferentAppProtocolVersionEncounter> DifferentAppProtocolVersionEncounterSubject { get; } =
            new();

        public Subject<Notification> NotificationSubject { get; } = new();
        public Subject<NodeException> NodeExceptionSubject { get; } = new();
        public NineChroniclesNodeService? NineChroniclesNodeService { get; set; }

        public ConcurrentDictionary<Address,
                (ReplaySubject<MonsterCollectionStatus> statusSubject, ReplaySubject<MonsterCollectionState> stateSubject, ReplaySubject<string> balanceSubject)>
            AgentAddresses
        { get; } = new ConcurrentDictionary<Address,
            (ReplaySubject<MonsterCollectionStatus>, ReplaySubject<MonsterCollectionState>, ReplaySubject<string>)>();

        public NodeStatusType NodeStatus => new(this)
        {
            BootstrapEnded = BootstrapEnded,
            PreloadEnded = PreloadEnded,
            IsMining = IsMining,
        };

        public IStore? Store { get; internal set; }

        public Swarm? Swarm { get; internal set; }

        public CurrencyFactory? CurrencyFactory { get; set; }

        public FungibleAssetValueFactory? FungibleAssetValueFactory { get; set; }

        internal TimeSpan DifferentAppProtocolVersionEncounterInterval { get; set; } = TimeSpan.FromSeconds(30);

        internal TimeSpan NotificationInterval { get; set; } = TimeSpan.FromSeconds(30);

        internal TimeSpan NodeExceptionInterval { get; set; } = TimeSpan.FromSeconds(30);

        internal TimeSpan MonsterCollectionStateInterval { get; set; } = TimeSpan.FromSeconds(30);

        internal TimeSpan MonsterCollectionStatusInterval { get; set; } = TimeSpan.FromSeconds(30);
    }
}
