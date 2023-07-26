using System.Collections.Generic;
using GraphQL.Types;
using Libplanet.Crypto;
using Libplanet.Types.Assets;
using Libplanet.Explorer.GraphTypes;

namespace NineChronicles.Headless.GraphTypes
{
    public class RecipientsInputType : InputObjectGraphType<(Address recipient, FungibleAssetValue amount)>
    {
        public RecipientsInputType()
        {
            Field<NonNullGraphType<AddressType>>("recipient");
            Field<NonNullGraphType<FungibleAssetValueInputType>>("amount");
        }

        public override object ParseDictionary(IDictionary<string, object?> value)
        {
            return ((Address)value["recipient"]!, (FungibleAssetValue)value["amount"]!);
        }
    }
}
