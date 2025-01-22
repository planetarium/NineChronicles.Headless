using GraphQL.Types;

namespace NineChronicles.Headless.GraphTypes;

public class DelegateeRepositoryType : ObjectGraphType<DelegateeRepositoryType>
{
    public DelegateeType? GuildDelegatee { get; set; }

    public DelegateeType? ValidatorDelegatee { get; set; }

    public DelegateeRepositoryType()
    {
        Field<NonNullGraphType<DelegateeType>>(
            nameof(GuildDelegatee),
            description: "Delegatee of the guild repository",
            resolve: context => context.Source.GuildDelegatee);
        Field<NonNullGraphType<DelegateeType>>(
            nameof(ValidatorDelegatee),
            description: "Delegatee of the validator repository",
            resolve: context => context.Source.ValidatorDelegatee);
    }
}
