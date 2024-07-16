using System;
using System.Collections.Concurrent;
using System.Reactive.Subjects;
using Libplanet.Action.State;
using Libplanet.Crypto;
using Libplanet.Blockchain;
using Libplanet.KeyStore;
using Libplanet.Net;
using Libplanet.Headless;
using Libplanet.Store;
using Libplanet.Types.Blocks;
using Nekoyume.Model.State;
using NineChronicles.Headless.GraphTypes;
using NineChronicles.Headless.Utils;

namespace NineChronicles.Headless
{
    public class StandaloneContext : IBlockChainContext, INodeContext
    {
        private BlockChain? _blockChain;
        private IKeyStore? _keyStore;
        private IStore? _store;
        private IStateStore? _stateStore;
        private Swarm? _swarm;

        public BlockChain BlockChain
        {
            get => _blockChain ??
                throw new InvalidOperationException($"{nameof(BlockChain)} property is not set yet.");
            set => _blockChain = value;
        }
        public IKeyStore KeyStore
        {
            get => _keyStore ??
                throw new InvalidOperationException($"{nameof(KeyStore)} property is not set yet.");
            set => _keyStore = value;
        }
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
                ReplaySubject<string>>
            AgentAddresses
        { get; } = new ConcurrentDictionary<Address, ReplaySubject<string>>();

        public NodeStatusType NodeStatus => new(this)
        {
            BootstrapEnded = BootstrapEnded,
            PreloadEnded = PreloadEnded,
            IsMining = IsMining,
        };

        public IStore Store
        {
            get => _store ??
                throw new InvalidOperationException($"{nameof(Store)} property is not set yet.");
            internal set => _store = value;
        }

        public IStateStore StateStore
        {
            get => _stateStore ??
                throw new InvalidOperationException($"{nameof(StateStore)} property is not set yet.");
            internal set => _stateStore = value;
        }

        public Swarm Swarm
        {
            get => _swarm ??
                throw new InvalidOperationException($"{nameof(Swarm)} property is not set yet.");
            internal set => _swarm = value;
        }

        public CurrencyFactory? CurrencyFactory { get; set; }

        public FungibleAssetValueFactory? FungibleAssetValueFactory { get; set; }

        internal TimeSpan DifferentAppProtocolVersionEncounterInterval { get; set; } = TimeSpan.FromSeconds(30);

        internal TimeSpan NotificationInterval { get; set; } = TimeSpan.FromSeconds(30);

        internal TimeSpan NodeExceptionInterval { get; set; } = TimeSpan.FromSeconds(30);

        internal TimeSpan MonsterCollectionStateInterval { get; set; } = TimeSpan.FromSeconds(30);

        internal TimeSpan MonsterCollectionStatusInterval { get; set; } = TimeSpan.FromSeconds(30);

        public IWorldState GetWorldState(BlockHash blockHash)
        {
            return BlockChain.GetWorldState(blockHash);
        }

        public IWorldState GetWorldState(long blockIndex)
        {
            return BlockChain.GetWorldState(BlockChain[blockIndex].Hash);
        }

        public IWorldState GetWorldState()
        {
            return BlockChain.GetWorldState();
        }

        public Block GetBlock(long blockIndex)
        {
            return BlockChain[blockIndex];
        }

        public Block GetBlock(BlockHash blockHash)
        {
            return BlockChain[blockHash];
        }

        public Block GetTip()
        {
            return BlockChain.Tip;
        }

        public long GetNextTxNonce(Address address)
        {
            return BlockChain.GetNextTxNonce(address);
        }

        public Address Address =>
            NineChroniclesNodeService?.MinerPrivateKey?.Address ?? throw new InvalidOperationException();
    }
}
