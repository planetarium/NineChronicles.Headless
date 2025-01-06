using System.Numerics;
using GraphQL.Types;
using Libplanet.Types.Assets;
using Nekoyume.ValidatorDelegation;

namespace NineChronicles.Headless.GraphTypes;

public class DelegatorType : ObjectGraphType<DelegatorType>
{
    public long LastDistributeHeight { get; set; }

    public BigInteger Share { get; set; }

    public FungibleAssetValue Fav { get; set; }

    public DelegatorType()
    {
        Field<NonNullGraphType<LongGraphType>>(
            nameof(LastDistributeHeight),
            description: "LastDistributeHeight of delegator",
            resolve: context => context.Source.LastDistributeHeight);
        Field<NonNullGraphType<StringGraphType>>(
            nameof(Share),
            description: "Share of delegator",
            resolve: context => context.Source.Share.ToString("N0"));
        Field<NonNullGraphType<FungibleAssetValueType>>(
            nameof(Fav),
            description: "Delegated FAV calculated based on Share value",
            resolve: context => context.Source.Fav);
    }
}
