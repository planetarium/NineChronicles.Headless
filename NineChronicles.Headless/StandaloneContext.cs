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
        public ReplaySubject<NodeStatusType> NodeStatusSubject { get; } = new ReplaySubject<NodeStatusType>(1);
        public ReplaySubject<PreloadState> PreloadStateSubject { get; } = new ReplaySubject<PreloadState>(1);
        public ReplaySubject<DifferentAppProtocolVersionEncounter> DifferentAppProtocolVersionEncounterSubject { get; }
            = new ReplaySubject<DifferentAppProtocolVersionEncounter>(1);
        public ReplaySubject<Notification> NotificationSubject { get; } = new ReplaySubject<Notification>(1);
        public ReplaySubject<NodeException> NodeExceptionSubject { get; } = new ReplaySubject<NodeException>(1);
        public ReplaySubject<MonsterCollectionState> MonsterCollectionStateSubject { get; } = new ReplaySubject<MonsterCollectionState>();
        public ReplaySubject<MonsterCollectionStatus> MonsterCollectionStatusSubject { get; } = new ReplaySubject<MonsterCollectionStatus>();
        public NineChroniclesNodeService? NineChroniclesNodeService { get; set; }

        public ConcurrentDictionary<Address,
                (ReplaySubject<MonsterCollectionStatus> statusSubject, ReplaySubject<MonsterCollectionState> stateSubject, ReplaySubject<string> balanceSubject)>
            AgentAddresses { get; } = new ConcurrentDictionary<Address,
                (ReplaySubject<MonsterCollectionStatus>, ReplaySubject<MonsterCollectionState>, ReplaySubject<string>)>();

        public NodeStatusType NodeStatus => new NodeStatusType(this)
        {
            BootstrapEnded = BootstrapEnded,
            PreloadEnded = PreloadEnded,
            IsMining = IsMining,
        };

        public IStore? Store { get; internal set; }
    }
}
