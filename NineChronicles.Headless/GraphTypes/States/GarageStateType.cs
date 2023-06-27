using System.Collections.Generic;
using System.Security.Cryptography;
using Bencodex.Types;
using GraphQL.Types;
using Lib9c;
using Libplanet;
using Libplanet.Assets;
using Libplanet.State;
using Nekoyume.Model.Garages;
using NineChronicles.Headless.GraphTypes.States.Models.Garage;

namespace NineChronicles.Headless.GraphTypes.States;

public class GarageStateType : ObjectGraphType<(FungibleAssetValue balance, IReadOnlyList<IValue> fungibleItemList)>
{
    public GarageStateType()
    {
        Field<FungibleAssetValueType>(name: "balance", resolve: context => context.Source.balance);
        Field<ListGraphType<FungibleItemGarageType>>(name: "fungibleItemList",
            resolve: context => context.Source.fungibleItemList);
    }
}
