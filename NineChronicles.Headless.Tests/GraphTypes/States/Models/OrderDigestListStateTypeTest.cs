using System.Collections.Generic;
using System.Threading.Tasks;
using GraphQL.Execution;
using Lib9c.Model.Order;
using Libplanet;
using Nekoyume.Model.State;
using NineChronicles.Headless.GraphTypes.States;
using NineChronicles.Headless.GraphTypes.States.Models.Item;
using Xunit;
using static NineChronicles.Headless.Tests.GraphQLTestUtils;

namespace NineChronicles.Headless.Tests.GraphTypes.States.Models;

public class OrderDigestListStateTypeTest
{
    [Fact]
    public async Task Query()
    {
        const string query = @"
            {
                address
                orderDigestList {
                    sellerAgentAddress
                    itemId
                    price
                    combatPoint
                    expiredBlockIndex
                    tradableId
                }
            }";

        var orderDigestList = new OrderDigestListState(OrderDigestListState.DeriveAddress(default));
        var queryResult = await ExecuteQueryAsync<OrderDigestListStateType>(query, source: orderDigestList);
        var data = (Dictionary<string, object>)((ExecutionNode)queryResult.Data!).ToValue()!;

        Assert.Equal(
            new Dictionary<string, object>
            {
                ["address"] = "0x0BCfBEA1c9B6a25F4C04Ecd3030bFEEa77c78D9a",
                ["orderDigestList"] = new Dictionary<string, object>(),
            },
            data
        );
    }
}
