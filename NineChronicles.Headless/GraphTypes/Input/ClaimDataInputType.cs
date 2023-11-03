using System.Collections.Generic;
using System.Linq;
using GraphQL.Types;
using Libplanet.Crypto;
using Libplanet.Explorer.GraphTypes;
using Libplanet.Types.Assets;

namespace NineChronicles.Headless.GraphTypes.Input;

public class ClaimDataInputType : InputObjectGraphType<(Address avatarAddress, IReadOnlyList<FungibleAssetValue> favList)>
{
    public ClaimDataInputType()
    {
        Field<NonNullGraphType<AddressType>>("avatarAddress");
        Field<NonNullGraphType<ListGraphType<NonNullGraphType<FungibleAssetValueInputType>>>>("fungibleAssetValues");
    }

    public override object ParseDictionary(IDictionary<string, object?> value)
    {
        return (
            (Address)value["avatarAddress"]!,
            (IReadOnlyList<FungibleAssetValue>)((object[])value["fungibleAssetValues"]!).Cast<FungibleAssetValue>().ToList()
        );
    }
}
