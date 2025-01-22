using System.Numerics;
using GraphQL.Types;
using Libplanet.Crypto;
using Libplanet.Types.Assets;
using Nekoyume.Model.Guild;
using Nekoyume.ValidatorDelegation;

namespace NineChronicles.Headless.GraphTypes;

public class DelegateeType : ObjectGraphType<DelegateeType>
{
    public BigInteger TotalShares { get; set; }

    public bool Jailed { get; set; }

    public long JailedUntil { get; set; }

    public bool Tombstoned { get; set; }

    public FungibleAssetValue TotalDelegated { get; set; }

    public BigInteger CommissionPercentage { get; set; }

    public DelegateeType()
    {
        Field<NonNullGraphType<StringGraphType>>(
            nameof(TotalShares),
            description: "Total shares of delegatee",
            resolve: context => context.Source.TotalShares.ToString("N0"));
        Field<NonNullGraphType<BooleanGraphType>>(
            nameof(Jailed),
            description: "Specifies whether the delegatee is jailed.",
            resolve: context => context.Source.Jailed);
        Field<NonNullGraphType<LongGraphType>>(
            nameof(JailedUntil),
            description: "Block height until which the delegatee is jailed.",
            resolve: context => context.Source.JailedUntil);
        Field<NonNullGraphType<BooleanGraphType>>(
            nameof(Tombstoned),
            description: "Specifies whether the delegatee is tombstoned.",
            resolve: context => context.Source.Tombstoned);
        Field<NonNullGraphType<FungibleAssetValueType>>(
            nameof(TotalDelegated),
            description: "Total delegated amount of the delegatee.",
            resolve: context => context.Source.TotalDelegated);
    }

    public static DelegateeType From(GuildRepository guildRepository, Address validatorAddress)
    {
        var delegatee = guildRepository.GetDelegatee(validatorAddress);

        return new DelegateeType
        {
            TotalShares = delegatee.TotalShares,
            Jailed = delegatee.Jailed,
            JailedUntil = delegatee.JailedUntil,
            Tombstoned = delegatee.Tombstoned,
            TotalDelegated = delegatee.TotalDelegated,
        };
    }

    public static DelegateeType From(ValidatorRepository validatorRepository, Address validatorAddress)
    {
        var delegatee = validatorRepository.GetDelegatee(validatorAddress);

        return new DelegateeType
        {
            TotalShares = delegatee.TotalShares,
            Jailed = delegatee.Jailed,
            JailedUntil = delegatee.JailedUntil,
            Tombstoned = delegatee.Tombstoned,
            TotalDelegated = delegatee.TotalDelegated,
        };
    }
}
