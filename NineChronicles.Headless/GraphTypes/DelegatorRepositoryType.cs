using GraphQL.Types;
using Libplanet.Crypto;
using Nekoyume.Model.Guild;
using Nekoyume.ValidatorDelegation;

namespace NineChronicles.Headless.GraphTypes;

public class DelegatorRepositoryType : ObjectGraphType<DelegatorRepositoryType>
{
    public DelegatorType? GuildDelegator { get; set; }

    public DelegatorType? ValidatorDelegator { get; set; }

    public DelegatorRepositoryType()
    {
        Field<DelegatorType>(
            nameof(GuildDelegator),
            description: "Delegator of the guild repository",
            resolve: context => context.Source.GuildDelegator);
        Field<DelegatorType>(
            nameof(ValidatorDelegator),
            description: "Delegator of the validator repository",
            resolve: context => context.Source.ValidatorDelegator);
    }

    public static DelegatorRepositoryType From(GuildRepository guildRepository, GuildParticipant guildParticipant)
    {
        var validatorRepository = new ValidatorRepository(
            guildRepository.World, guildRepository.ActionContext);
        var guild = guildRepository.GetGuild(guildParticipant.GuildAddress);

        return new DelegatorRepositoryType
        {
            GuildDelegator = DelegatorType.From(guildRepository, guildParticipant),
            ValidatorDelegator = DelegatorType.From(validatorRepository, guild),
        };
    }

    public static DelegatorRepositoryType From(ValidatorRepository validatorRepository, Address validatorAddress)
    {
        var guildRepository = new GuildRepository(
            validatorRepository.World, validatorRepository.ActionContext);

        return new DelegatorRepositoryType
        {
            GuildDelegator = DelegatorType.From(guildRepository, validatorAddress),
            ValidatorDelegator = DelegatorType.From(validatorRepository, validatorAddress),
        };
    }
}
