using GraphQL.Types;
using Libplanet;
using Libplanet.Explorer.GraphTypes;

namespace NineChronicles.Headless.GraphTypes;

public sealed class WithAddressType<TGraphType, TSourceType> :
    ObjectGraphType<(TSourceType source, Address addr)>
    where TGraphType : IComplexGraphType, new()
{
    public WithAddressType()
    {
        var t = new TGraphType();
        foreach (var field in t.Fields)
        {
            AddField(field);
        }

        Field<AddressType>(
            "address",
            resolve: context => context.Source.addr);
    }
}
