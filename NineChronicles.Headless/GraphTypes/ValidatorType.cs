using System.Numerics;
using GraphQL.Types;
using Libplanet.Types.Assets;
using Nekoyume.ValidatorDelegation;

namespace NineChronicles.Headless.GraphTypes;

public class ValidatorType : ObjectGraphType<ValidatorType>
{
    public BigInteger Power { get; set; }

    public bool IsActive { get; set; }

    public BigInteger TotalShares { get; set; }

    public bool Jailed { get; set; }

    public long JailedUntil { get; set; }

    public bool Tombstoned { get; set; }

    public FungibleAssetValue TotalDelegated { get; set; }

    public BigInteger CommissionPercentage { get; set; }

    public ValidatorType()
    {
        Field<NonNullGraphType<StringGraphType>>(
            nameof(Power),
            description: "Power of validator",
            resolve: context => context.Source.Power.ToString("N0"));
        Field<NonNullGraphType<BooleanGraphType>>(
            nameof(IsActive),
            description: "Specifies whether the validator is active.",
            resolve: context => context.Source.IsActive);
        Field<NonNullGraphType<StringGraphType>>(
            nameof(TotalShares),
            description: "Total shares of validator",
            resolve: context => context.Source.TotalShares.ToString("N0"));
        Field<NonNullGraphType<BooleanGraphType>>(
            nameof(Jailed),
            description: "Specifies whether the validator is jailed.",
            resolve: context => context.Source.Jailed);
        Field<NonNullGraphType<LongGraphType>>(
            nameof(JailedUntil),
            description: "Block height until which the validator is jailed.",
            resolve: context => context.Source.JailedUntil);
        Field<NonNullGraphType<BooleanGraphType>>(
            nameof(Tombstoned),
            description: "Specifies whether the validator is tombstoned.",
            resolve: context => context.Source.Tombstoned);
        Field<NonNullGraphType<FungibleAssetValueType>>(
            nameof(TotalDelegated),
            description: "Total delegated amount of the validator.",
            resolve: context => context.Source.TotalDelegated);
        Field<NonNullGraphType<StringGraphType>>(
            nameof(CommissionPercentage),
            description: "Commission percentage of the validator.",
            resolve: context => context.Source.CommissionPercentage.ToString("N0"));
    }

    public static ValidatorType FromDelegatee(ValidatorDelegatee validatorDelegatee) =>
            new ValidatorType
            {
                Power = validatorDelegatee.Power,
                IsActive = validatorDelegatee.IsActive,
                TotalShares = validatorDelegatee.TotalShares,
                Jailed = validatorDelegatee.Jailed,
                JailedUntil = validatorDelegatee.JailedUntil,
                Tombstoned = validatorDelegatee.Tombstoned,
                TotalDelegated = validatorDelegatee.TotalDelegated,
                CommissionPercentage = validatorDelegatee.CommissionPercentage,
            };
}
