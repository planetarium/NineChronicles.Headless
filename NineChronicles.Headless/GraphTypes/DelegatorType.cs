using System.Numerics;
using GraphQL.Types;
using Libplanet.Crypto;
using Libplanet.Types.Assets;
using Nekoyume.Model.Guild;
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

    public static DelegatorType From(GuildRepository guildRepository, GuildParticipant guildParticipant)
    {
        var guild = guildRepository.GetGuild(guildParticipant.GuildAddress);
        var delegatee = guildRepository.GetDelegatee(guild.ValidatorAddress);
        var bond = guildRepository.GetBond(delegatee, guildParticipant.Address);
        var totalFAV = delegatee.Metadata.TotalDelegatedFAV;
        var totalShare = delegatee.Metadata.TotalShares;
        var lastDistributeHeight = bond.LastDistributeHeight ?? -1;
        var share = bond.Share;
        var fav = (share * totalFAV).DivRem(totalShare).Quotient;

        return new DelegatorType
        {
            LastDistributeHeight = lastDistributeHeight,
            Share = share,
            Fav = fav,
        };
    }

    public static DelegatorType From(ValidatorRepository validatorRepository, Guild guild)
    {
        var delegatee = validatorRepository.GetDelegatee(guild.ValidatorAddress);
        var bond = validatorRepository.GetBond(delegatee, guild.Address);
        var totalFAV = delegatee.Metadata.TotalDelegatedFAV;
        var totalShare = delegatee.Metadata.TotalShares;
        var lastDistributeHeight = bond.LastDistributeHeight ?? -1;
        var share = bond.Share;
        var fav = (share * totalFAV).DivRem(totalShare).Quotient;

        return new DelegatorType
        {
            LastDistributeHeight = lastDistributeHeight,
            Share = share,
            Fav = fav,
        };
    }

    public static DelegatorType From(GuildRepository guildRepository, Address validatorAddress)
    {
        var delegatee = guildRepository.GetDelegatee(validatorAddress);
        var bond = guildRepository.GetBond(delegatee, validatorAddress);
        var totalFAV = delegatee.Metadata.TotalDelegatedFAV;
        var totalShare = delegatee.Metadata.TotalShares;
        var lastDistributeHeight = bond.LastDistributeHeight ?? -1;
        var share = bond.Share;
        var fav = (share * totalFAV).DivRem(totalShare).Quotient;

        return new DelegatorType
        {
            LastDistributeHeight = lastDistributeHeight,
            Share = share,
            Fav = fav,
        };
    }

    public static DelegatorType From(ValidatorRepository validatorRepository, Address validatorAddress)
    {
        var delegatee = validatorRepository.GetDelegatee(validatorAddress);
        var bond = validatorRepository.GetBond(delegatee, validatorAddress);
        var totalFAV = delegatee.Metadata.TotalDelegatedFAV;
        var totalShare = delegatee.Metadata.TotalShares;
        var lastDistributeHeight = bond.LastDistributeHeight ?? -1;
        var share = bond.Share;
        var fav = (share * totalFAV).DivRem(totalShare).Quotient;

        return new DelegatorType
        {
            LastDistributeHeight = lastDistributeHeight,
            Share = share,
            Fav = fav,
        };
    }
}
