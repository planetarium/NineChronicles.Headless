using System.Reactive.Subjects;
using Libplanet.Blockchain;
using Libplanet.KeyStore;
using Libplanet.Net;
using Libplanet.Headless;
using Libplanet.Store;
using NineChronicles.Headless.GraphTypes;
using NineChroniclesActionType = Libplanet.Action.PolymorphicAction<Nekoyume.Action.ActionBase>;
using NCBlock = Libplanet.Blocks.Block<Libplanet.Action.PolymorphicAction<Nekoyume.Action.ActionBase>>;

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
        public ISubject<(NCBlock OldTip, NCBlock NewTip)>? BlockSubject { get; set; }
        public ReplaySubject<DifferentAppProtocolVersionEncounter> DifferentAppProtocolVersionEncounterSubject { get; }
            = new ReplaySubject<DifferentAppProtocolVersionEncounter>();
        public ReplaySubject<Notification> NotificationSubject { get; } = new ReplaySubject<Notification>(1);
        public ReplaySubject<NodeException> NodeExceptionSubject { get; } = new ReplaySubject<NodeException>();
        public NineChroniclesNodeService? NineChroniclesNodeService { get; set; }
        public NodeStatusType NodeStatus => new NodeStatusType()
        {
            BootstrapEnded = BootstrapEnded,
            PreloadEnded = PreloadEnded,
            IsMining = IsMining,
        };

        public IStore? Store { get; internal set; }
    }
}
