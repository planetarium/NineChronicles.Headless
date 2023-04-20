using System;
using GraphQL.Types;
using Lib9c.Model.Order;
using Libplanet.Explorer.GraphTypes;

namespace NineChronicles.Headless.GraphTypes.States.Models.Item;

[Serializable]
public class OrderDigestListStateType : ObjectGraphType<OrderDigestListState>
{
    public OrderDigestListStateType()
    {
        Field<AddressType>(
            nameof(OrderDigestListState.Address),
            resolve: context => context.Source.Address
        );
        Field<NonNullGraphType<ListGraphType<OrderDigestType>>>(
            nameof(OrderDigestListState.OrderDigestList),
            resolve: context => context.Source.OrderDigestList
        );
    }
}
