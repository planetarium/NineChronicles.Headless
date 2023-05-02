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

public class MeadContractTypeTest
{
    [Theory]
    [InlineData(false, false)]
    [InlineData(true, true)]
    [InlineData(true, false)]
    public async Task Query(bool exist, bool contracted)
    {
        const string query = @"
        {
            valkyrieAddress
            contracted
        }";

        Address? address = null;
        if (exist)
        {
            address = new PrivateKey().ToAddress();
        }
        (Address?, bool) contract = (address, contracted);
        var queryResult = await ExecuteQueryAsync<MeadContractType>(query, source: contract);
        var data = (Dictionary<string, object?>)((ExecutionNode)queryResult.Data!).ToValue()!;

        Assert.Equal(
            new Dictionary<string, object?>
            {
                ["valkyrieAddress"] = address is null ? null : address.ToString(),
                ["contracted"] = contracted,
            },
            data
        );
    }
}
