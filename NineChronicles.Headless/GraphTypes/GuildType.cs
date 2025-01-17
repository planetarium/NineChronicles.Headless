using GraphQL.Types;
using Libplanet.Crypto;
using Libplanet.Explorer.GraphTypes;
using Nekoyume.Model.Guild;

namespace NineChronicles.Headless.GraphTypes;

public class GuildType : ObjectGraphType<GuildType>
{
    public Address Address { get; set; }

    public Address ValidatorAddress { get; set; }

    public Address GuildMasterAddress { get; set; }

    public GuildType()
    {
        Field<NonNullGraphType<AddressType>>(
            nameof(Address),
            description: "Address of the guild",
            resolve: context => context.Source.Address);
        Field<NonNullGraphType<AddressType>>(
            nameof(ValidatorAddress),
            description: "Validator address of the guild",
            resolve: context => context.Source.ValidatorAddress);
        Field<NonNullGraphType<AddressType>>(
            nameof(GuildMasterAddress),
            description: "Guild master address of the guild",
            resolve: context => context.Source.GuildMasterAddress);
    }

    public static GuildType FromDelegatee(Guild guild) => new GuildType
    {
        Address = guild.Address,
        ValidatorAddress = guild.ValidatorAddress,
        GuildMasterAddress = guild.GuildMasterAddress,
    };
}
