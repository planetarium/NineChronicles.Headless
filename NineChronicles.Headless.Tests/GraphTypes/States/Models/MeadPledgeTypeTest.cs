using System.Collections.Generic;
using System.Threading.Tasks;
using Bencodex.Types;
using GraphQL.Execution;
using Libplanet;
using Libplanet.Crypto;
using Nekoyume.Model.State;
using NineChronicles.Headless.GraphTypes.States.Models;
using Xunit;
using static NineChronicles.Headless.Tests.GraphQLTestUtils;

namespace NineChronicles.Headless.Tests.GraphTypes.States.Models;

public class MeadPledgeTypeTest
{
    [Theory]
    [InlineData(false, false, 0)]
    [InlineData(true, true, 1)]
    [InlineData(true, false, 2)]
    public async Task Query(bool exist, bool approved, int mead)
    {
        const string query = @"
        {
            patronAddress
            approved
            mead
        }";

        Address? address = null;
        if (exist)
        {
            address = new PrivateKey().Address;
        }
        (Address?, bool, int) contract = (address, approved, mead);
        var queryResult = await ExecuteQueryAsync<MeadPledgeType>(query, source: contract);
        var data = (Dictionary<string, object?>)((ExecutionNode)queryResult.Data!).ToValue()!;

        Assert.Equal(
            new Dictionary<string, object?>
            {
                ["patronAddress"] = address is null ? null : address.ToString(),
                ["approved"] = approved,
                ["mead"] = mead,
            },
            data
        );
    }
}
