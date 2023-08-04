using GraphQL.Types;
using Libplanet.Crypto;
using Libplanet.Explorer.GraphTypes;

namespace NineChronicles.Headless.GraphTypes.States.Models;

public class MeadPledgeType : ObjectGraphType<(Address? Address, bool Approved, int Mead)>
{
    public MeadPledgeType()
    {
        Field<AddressType>(name: "patronAddress", resolve: context => context.Source.Address);
        Field<NonNullGraphType<BooleanGraphType>>(name: "approved", resolve: context => context.Source.Approved);
        Field<NonNullGraphType<IntGraphType>>(name: "mead", resolve: context => context.Source.Mead);
    }
}
