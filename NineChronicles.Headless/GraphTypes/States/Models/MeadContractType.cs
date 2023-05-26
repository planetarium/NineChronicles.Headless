using GraphQL.Types;
using Libplanet;
using Libplanet.Explorer.GraphTypes;

namespace NineChronicles.Headless.GraphTypes.States.Models;

public class MeadContractType : ObjectGraphType<(Address?, bool)>
{
    public MeadContractType()
    {
        Field<AddressType>(name: "patronAddress", resolve: context => context.Source.Item1);
        Field<NonNullGraphType<BooleanGraphType>>(name: "contracted", resolve: context => context.Source.Item2);
    }
}
