using System;
using System.Collections.Concurrent;
using System.Numerics;
using System.Reactive.Subjects;
using Bencodex.Types;
using Lib9c;
using Libplanet;
using Libplanet.Assets;
using Libplanet.Blockchain;
using Libplanet.KeyStore;
using Libplanet.Net;
using Libplanet.Headless;
using Libplanet.Store;
using Nekoyume;
using Nekoyume.Model.State;
using NineChronicles.Headless.GraphTypes;

namespace NineChronicles.Headless
{
    public class StandaloneContext
    {
        public BlockChain? BlockChain { get; set; }
        public IKeyStore? KeyStore { get; set; }
        public bool BootstrapEnded { get; set; }
        public bool PreloadEnded { get; set; }
        public bool IsMining { get; set; }
        public ReplaySubject<NodeStatusType> NodeStatusSubject { get; } = new ReplaySubject<NodeStatusType>(1);
        public ReplaySubject<BlockSyncState> PreloadStateSubject { get; } = new ReplaySubject<BlockSyncState>(5);
        public Subject<DifferentAppProtocolVersionEncounter> DifferentAppProtocolVersionEncounterSubject { get; }
            = new Subject<DifferentAppProtocolVersionEncounter>();
        public Subject<Notification> NotificationSubject { get; } = new Subject<Notification>();
        public Subject<NodeException> NodeExceptionSubject { get; } = new Subject<NodeException>();
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

        public IStore? Store { get; internal set; }

        public Swarm? Swarm { get; internal set; }

        public Currency? NCG { get; internal set; }

        internal TimeSpan DifferentAppProtocolVersionEncounterInterval { get; set; } = TimeSpan.FromSeconds(30);

        internal TimeSpan NotificationInterval { get; set; } = TimeSpan.FromSeconds(30);

        internal TimeSpan NodeExceptionInterval { get; set; } = TimeSpan.FromSeconds(30);

        internal TimeSpan MonsterCollectionStateInterval { get; set; } = TimeSpan.FromSeconds(30);

        internal TimeSpan MonsterCollectionStatusInterval { get; set; } = TimeSpan.FromSeconds(30);

        public bool TryGetCurrency(CurrencyEnum currencyEnum, out Currency? currency)
        {
            return TryGetCurrency(currencyEnum.ToString(), out currency);
        }

        public bool TryGetCurrency(string ticker, out Currency? currency)
        {
            currency = ticker switch
            {
                "NCG" => GetNCG(),
                _ => Currencies.GetMinterlessCurrency(ticker),
            };
            return currency is not null;
        }

        private Currency? GetNCG()
        {
            if (NCG is not null)
            {
                return NCG;
            }

            if (BlockChain?.GetState(Addresses.GoldCurrency) is
                Dictionary goldCurrencyDict)
            {
                var goldCurrency = new GoldCurrencyState(goldCurrencyDict);
                NCG = goldCurrency.Currency;
            }

            return NCG;
        }

        public bool TryGetFungibleAssetValue(
            CurrencyEnum currencyEnum,
            BigInteger majorUnit,
            BigInteger minorUnit,
            out FungibleAssetValue? fungibleAssetValue)
        {
            return TryGetFungibleAssetValue(
                currencyEnum.ToString(),
                majorUnit,
                minorUnit,
                out fungibleAssetValue);
        }

        public bool TryGetFungibleAssetValue(
            string ticker,
            BigInteger majorUnit,
            BigInteger minorUnit,
            out FungibleAssetValue? fungibleAssetValue)
        {
            if (TryGetCurrency(ticker, out var currency))
            {
                fungibleAssetValue = new FungibleAssetValue(currency!.Value, majorUnit, minorUnit);
                return true;
            }

            fungibleAssetValue = null;
            return false;
        }

        public bool TryGetFungibleAssetValue(
            CurrencyEnum currencyEnum,
            string value,
            out FungibleAssetValue? fungibleAssetValue)
        {
            return TryGetFungibleAssetValue(
                currencyEnum.ToString(),
                value,
                out fungibleAssetValue);
        }

        public bool TryGetFungibleAssetValue(
            string ticker,
            string value,
            out FungibleAssetValue? fungibleAssetValue)
        {
            if (TryGetCurrency(ticker, out var currency))
            {
                fungibleAssetValue = FungibleAssetValue.Parse(currency!.Value, value);
            }

            fungibleAssetValue = null;
            return false;
        }
    }
}
