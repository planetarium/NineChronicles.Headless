using System.Collections.Immutable;
using Bencodex;
using Bencodex.Types;
using Libplanet.Action;
using Libplanet.Assets;

namespace Libplanet.Extensions.RemoteActionEvaluator;

public static class AccountStateDeltaMarshaller
{
    private static readonly Codec Codec = new Codec();

    public static byte[] Serialize(this IAccountStateDelta value)
    {
        return Codec.Encode(Marshal(value));
    }
    
    public static IEnumerable<Dictionary> Marshal(IEnumerable<IAccountStateDelta> deltas)
    {
        IImmutableDictionary<Address, IValue> updatedStates = ImmutableDictionary<Address, IValue>.Empty;
        IImmutableDictionary<Address, IImmutableSet<Currency>> updatedFungibleAssets = ImmutableDictionary<Address, IImmutableSet<Currency>>.Empty;
        IImmutableSet<Currency> totalSupplyUpdatedCurrencies = ImmutableHashSet<Currency>.Empty;
        foreach (var value in deltas)
        {
            updatedStates = updatedStates.SetItems(value.StateUpdatedAddresses.Select(addr =>
                new KeyValuePair<Address, IValue>(addr, value.GetState(addr))));
            updatedFungibleAssets = updatedFungibleAssets.SetItems(value.UpdatedFungibleAssets);
            totalSupplyUpdatedCurrencies = totalSupplyUpdatedCurrencies.Union(value.TotalSupplyUpdatedCurrencies);
            var state = new Dictionary(
                updatedStates.Select(pair => new KeyValuePair<IKey, IValue>((Binary)pair.Key.ByteArray, pair.Value))
            );
            var balance = new Bencodex.Types.List(
#pragma warning disable LAA1002
                updatedFungibleAssets.SelectMany(ua =>
#pragma warning restore LAA1002
                    ua.Value.Select(c =>
                        {
                            FungibleAssetValue b = value.GetBalance(ua.Key, c);
                            return new Bencodex.Types.Dictionary(new[]
                            {
                                new KeyValuePair<IKey, IValue>((Text) "address", (Binary) ua.Key.ByteArray),
                                new KeyValuePair<IKey, IValue>((Text) "currency", c.Serialize()),
                                new KeyValuePair<IKey, IValue>((Text) "amount", (Integer) b.RawValue),
                            });
                        }
                    )
                ).Cast<IValue>()
            );
            var totalSupply = new Dictionary(
                totalSupplyUpdatedCurrencies.Select(currency =>
                    new KeyValuePair<IKey, IValue>(
                        (Binary)Codec.Encode(currency.Serialize()),
                        (Integer)value.GetTotalSupply(currency).RawValue)));

            var bdict = new Dictionary(new[]
            {
                new KeyValuePair<IKey, IValue>((Text) "states", state),
                new KeyValuePair<IKey, IValue>((Text) "balances", balance),
                new KeyValuePair<IKey, IValue>((Text) "totalSupplies", totalSupply),
                new KeyValuePair<IKey, IValue>((Text) "validatorSet", value.GetValidatorSet().Bencoded),
            });

            yield return bdict;
        }
    }

    public static Dictionary Marshal(IAccountStateDelta value)
    {
        var state = new Dictionary(
            value.UpdatedAddresses.Where(addr => value.GetState(addr) is not null).Select(addr => new KeyValuePair<IKey, IValue>(
                (Binary)addr.ToByteArray(),
                value.GetState(addr)
            ))
        );
        var balance = new Bencodex.Types.List(
#pragma warning disable LAA1002
            value.UpdatedFungibleAssets.SelectMany(ua =>
#pragma warning restore LAA1002
                ua.Value.Select(c =>
                    {
                        FungibleAssetValue b = value.GetBalance(ua.Key, c);
                        return new Bencodex.Types.Dictionary(new[]
                        {
                            new KeyValuePair<IKey, IValue>((Text) "address", (Binary) ua.Key.ByteArray),
                            new KeyValuePair<IKey, IValue>((Text) "currency", c.Serialize()),
                            new KeyValuePair<IKey, IValue>((Text) "amount", (Integer) b.RawValue),
                        });
                    }
                )
            ).Cast<IValue>()
        );
        var totalSupply = new Dictionary(
            value.TotalSupplyUpdatedCurrencies.Select(currency =>
                new KeyValuePair<IKey, IValue>(
                    (Binary)new Codec().Encode(currency.Serialize()),
                    (Integer)value.GetTotalSupply(currency).RawValue)));

        var bdict = new Dictionary(new[]
        {
            new KeyValuePair<IKey, IValue>((Text) "states", state),
            new KeyValuePair<IKey, IValue>((Text) "balances", balance),
            new KeyValuePair<IKey, IValue>((Text) "totalSupplies", totalSupply),
            new KeyValuePair<IKey, IValue>((Text) "validatorSet", value.GetValidatorSet().Bencoded),
        });

        return bdict;
    }

    public static AccountStateDelta Unmarshal(IValue marshalled)
    {
        return new AccountStateDelta(marshalled);
    }

    public static AccountStateDelta Deserialize(byte[] serialized)
    {
        var decoded = Codec.Decode(serialized);
        return Unmarshal(decoded);
    }
}
