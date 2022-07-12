using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Bencodex.Types;
using Libplanet;
using Libplanet.Action;
using Libplanet.Assets;

namespace NineChronicles.Headless.GraphTypes.States
{
    public class StateContext : IAccountStateDelta
    {
        public StateContext(AccountStateGetter accountStateGetter, AccountBalanceGetter accountBalanceGetter, long blockIndex)
        {
            AccountStateGetter = accountStateGetter;
            AccountBalanceGetter = accountBalanceGetter;
            this.BlockIndex = blockIndex;
        }
        public static ChampionArenaInfo[] AddRank(
           ChampionArenaInfo[] tuples)
        {

            if (tuples.Length == 0)
            {
                return new ChampionArenaInfo[0];
            }

            var orderedTuples = tuples
                .OrderByDescending(tuple => tuple.Score)
                .ThenBy(tuple => tuple.AvatarAddress)
                .ToArray();

            var result = new List<ChampionArenaInfo>();
            var trunk = new List<ChampionArenaInfo>();
            int? currentScore = null;
            var currentRank = 1;
            for (var i = 0; i < orderedTuples.Length; i++)
            {
                var tuple = orderedTuples[i];
                if (!currentScore.HasValue)
                {
                    currentScore = tuple.Score;
                    trunk.Add(tuple);
                    continue;
                }

                if (currentScore.Value == tuple.Score)
                {
                    trunk.Add(tuple);
                    currentRank++;
                    if (i < orderedTuples.Length - 1)
                    {
                        continue;
                    }

                    foreach (var tupleInTrunk in trunk)
                    {
                        tupleInTrunk.Rank = currentRank;
                        result.Add(tupleInTrunk);
                    }

                    trunk.Clear();

                    continue;
                }

                foreach (var tupleInTrunk in trunk)
                {
                    tupleInTrunk.Rank = currentRank;
                    result.Add(tupleInTrunk);
                }

                trunk.Clear();
                if (i < orderedTuples.Length - 1)
                {
                    trunk.Add(tuple);
                    currentScore = tuple.Score;
                    currentRank++;
                    continue;
                }
                tuple.Rank = currentRank;
                result.Add(tuple);
            }

            return result.ToArray();
        }

        public AccountStateGetter AccountStateGetter { get; }
        public AccountBalanceGetter AccountBalanceGetter { get; }
        public long BlockIndex { get; }

        public IImmutableSet<Address> UpdatedAddresses => throw new System.NotImplementedException();

        public IImmutableSet<Address> StateUpdatedAddresses => throw new System.NotImplementedException();

        public IImmutableDictionary<Address, IImmutableSet<Currency>> UpdatedFungibleAssets => throw new System.NotImplementedException();

        public IValue? GetState(Address address) =>
            AccountStateGetter(new[] { address })[0];

        public IReadOnlyList<IValue?> GetStates(IReadOnlyList<Address> addresses) =>
            AccountStateGetter(addresses);

        public FungibleAssetValue GetBalance(Address address, Currency currency) =>
            AccountBalanceGetter(address, currency);

        public IAccountStateDelta SetState(Address address, IValue state)
        {
            throw new System.NotImplementedException();
        }

        public IAccountStateDelta MintAsset(Address recipient, FungibleAssetValue value)
        {
            throw new System.NotImplementedException();
        }

        public IAccountStateDelta TransferAsset(Address sender, Address recipient, FungibleAssetValue value, bool allowNegativeBalance = false)
        {
            throw new System.NotImplementedException();
        }

        public IAccountStateDelta BurnAsset(Address owner, FungibleAssetValue value)
        {
            throw new System.NotImplementedException();
        }

        
    }
}
