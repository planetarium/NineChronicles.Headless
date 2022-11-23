using System;
using System.Collections.Concurrent;
using System.Reactive.Subjects;
using Libplanet;
using Libplanet.Assets;
using Libplanet.Blockchain;
using Libplanet.KeyStore;
using Libplanet.Net;
using Libplanet.Headless;
using Libplanet.Store;
using Nekoyume.Model.State;
using NineChronicles.Headless.GraphTypes;
using NineChroniclesActionType = Libplanet.Action.PolymorphicAction<Nekoyume.Action.ActionBase>;

namespace NineChronicles.Headless
{
    public class StandaloneContext
    {
        public BlockChain<NineChroniclesActionType>? BlockChain { get; set; }
        public IKeyStore? KeyStore { get; set; }
        public bool BootstrapEnded { get; set; }
        public bool PreloadEnded { get; set; }
        public bool IsMining { get; set; }
        public ReplaySubject<NodeStatusType> NodeStatusSubject { get; } = new ReplaySubject<NodeStatusType>();
        public ReplaySubject<PreloadState> PreloadStateSubject { get; } = new ReplaySubject<PreloadState>();
        public Subject<DifferentAppProtocolVersionEncounter> DifferentAppProtocolVersionEncounterSubject { get; }
            = new Subject<DifferentAppProtocolVersionEncounter>();
        public Subject<Notification> NotificationSubject { get; } = new Subject<Notification>();
        public Subject<NodeException> NodeExceptionSubject { get; } = new Subject<NodeException>();
        public Subject<MonsterCollectionState> MonsterCollectionStateSubject { get; } = new Subject<MonsterCollectionState>();
        public Subject<MonsterCollectionStatus> MonsterCollectionStatusSubject { get; } = new Subject<MonsterCollectionStatus>();
        public NineChroniclesNodeService? NineChroniclesNodeService { get; set; }

        public ConcurrentDictionary<Address,
                (ReplaySubject<MonsterCollectionStatus> statusSubject, ReplaySubject<MonsterCollectionState> stateSubject, ReplaySubject<string> balanceSubject)>
            AgentAddresses
        { get; } = new ConcurrentDictionary<Address,
                (ReplaySubject<MonsterCollectionStatus>, ReplaySubject<MonsterCollectionState>, ReplaySubject<string>)>();

        public NodeStatusType NodeStatus => new NodeStatusType(this)
        {
            BootstrapEnded = BootstrapEnded,
            PreloadEnded = PreloadEnded,
            IsMining = IsMining,
        };

        internal TimeSpan DifferentAppProtocolVersionEncounterInterval { get; set; } = TimeSpan.FromSeconds(30);

        internal TimeSpan NotificationInterval { get; set; } = TimeSpan.FromSeconds(30);

        internal TimeSpan NodeExceptionInterval { get; set; } = TimeSpan.FromSeconds(30);

        internal TimeSpan MonsterCollectionStateInterval { get; set; } = TimeSpan.FromSeconds(30);

        internal TimeSpan MonsterCollectionStatusInterval { get; set; } = TimeSpan.FromSeconds(30);
    }
}
