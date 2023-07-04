using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Bencodex;
using Bencodex.Types;
using GraphQL.Execution;
using Libplanet;
using Libplanet.Assets;
using Nekoyume.Action.Garages;
using NineChronicles.Headless.GraphTypes;
using Xunit;
using static NineChronicles.Headless.Tests.GraphQLTestUtils;

namespace NineChronicles.Headless.Tests.GraphTypes
{
    public class StateQueryTest
    {
        private readonly Codec _codec;

        public StateQueryTest()
        {
            _codec = new Codec();
        }

        [Theory]
        [MemberData(nameof(GetMemberDataOfLoadIntoMyGarages))]
        public async Task Garage(
            IEnumerable<(Address balanceAddr, FungibleAssetValue value)>? fungibleAssetValues,
            Address? inventoryAddr,
            IEnumerable<(HashDigest<SHA256> fungibleId, int count)>? fungibleIdAndCounts,
            string? memo)
        {
            var expectedAction = new LoadIntoMyGarages(
                fungibleAssetValues,
                inventoryAddr,
                fungibleIdAndCounts,
                memo);
            var sb = new StringBuilder("{ loadIntoMyGarages(");
            if (fungibleAssetValues is not null)
            {
                sb.Append("fungibleAssetValues: [");
                sb.Append(string.Join(",", fungibleAssetValues.Select(tuple =>
                    $"{{ balanceAddr: \"{tuple.balanceAddr.ToHex()}\", " +
                    $"value: {{ currencyTicker: \"{tuple.value.Currency.Ticker}\"," +
                    $"value: \"{tuple.value.GetQuantityString()}\" }} }}")));
                sb.Append("],");
            }

            if (inventoryAddr is not null)
            {
                sb.Append($"inventoryAddr: \"{inventoryAddr.Value.ToHex()}\",");
            }

            if (fungibleIdAndCounts is not null)
            {
                sb.Append("fungibleIdAndCounts: [");
                sb.Append(string.Join(",", fungibleIdAndCounts.Select(tuple =>
                    $"{{ fungibleId: \"{tuple.fungibleId.ToString()}\", " +
                    $"count: {tuple.count} }}")));
                sb.Append("],");
            }

            if (memo is not null)
            {
                sb.Append($"memo: \"{memo}\"");
            }

            // Remove last ',' if exists.
            if (sb[^1] == ',')
            {
                sb.Remove(sb.Length - 1, 1);
            }

            sb.Append(") }");
            var queryResult = await ExecuteQueryAsync<StateQuery>(sb.ToString());
            Assert.Null(queryResult.Errors);
            var data = (Dictionary<string, object>)((ExecutionNode)queryResult.Data!).ToValue()!;
            var plainValue = _codec.Decode(ByteUtil.ParseHex((string)data["loadIntoMyGarages"]));
            Assert.IsType<Dictionary>(plainValue);
            var actionBase = DeserializeNCAction(plainValue);
            var actualAction = Assert.IsType<LoadIntoMyGarages>(actionBase);
            Assert.True(expectedAction.FungibleAssetValues?.SequenceEqual(actualAction.FungibleAssetValues) ??
                        actualAction.FungibleAssetValues is null);
            Assert.Equal(expectedAction.InventoryAddr, actualAction.InventoryAddr);
            Assert.True(expectedAction.FungibleIdAndCounts?.SequenceEqual(actualAction.FungibleIdAndCounts) ??
                        actualAction.FungibleIdAndCounts is null);
            Assert.Equal(expectedAction.Memo, actualAction.Memo);
        }
    }
}
