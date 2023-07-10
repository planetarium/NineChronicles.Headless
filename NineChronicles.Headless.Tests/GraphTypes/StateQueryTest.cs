using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Bencodex;
using Bencodex.Types;
using Google.Protobuf.WellKnownTypes;
using GraphQL;
using GraphQL.Execution;
using Lib9c;
using Libplanet;
using Libplanet.Assets;
using Libplanet.Crypto;
using Nekoyume;
using Nekoyume.Action.Garages;
using Nekoyume.Model.Elemental;
using Nekoyume.Model.Garages;
using Nekoyume.Model.Item;
using Nekoyume.Model.State;
using NineChronicles.Headless.GraphTypes;
using NineChronicles.Headless.GraphTypes.States;
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
        [MemberData(nameof(GetMemberDataOfGarages))]
        public async Task Garage(
            Address agentAddr,
            IEnumerable<CurrencyEnum>? currencyEnums,
            IEnumerable<string>? currencyTickers,
            IEnumerable<string>? fungibleItemIds)
        {
            var sb = new StringBuilder("{ garages(");
            sb.Append($"agentAddr: \"{agentAddr.ToString()}\"");
            if (currencyEnums is not null)
            {
                if (currencyTickers is not null)
                {
                    throw new ExecutionError(
                        "Use either `currencyEnums` or `currencyTickers` to get balances.");
                }

                sb.Append(", currencyEnums: [");
                sb.Append(string.Join(", ", currencyEnums));
                sb.Append("]");
            }
            else if (currencyTickers is not null)
            {
                sb.Append(", currencyTickers: [");
                sb.Append(string.Join(", ", currencyTickers.Select(ticker => $"\"{ticker}\"")));
                sb.Append("]");
            }

            if (fungibleItemIds is not null)
            {
                sb.Append(", fungibleItemIds: [");
                sb.Append(string.Join(", ", fungibleItemIds.Select(id => $"\"{id}\"")));
                sb.Append("]");
            }

            sb.Append(") {");
            sb.Append("agentAddr");
            sb.Append(" garageBalancesAddr");
            sb.Append(" garageBalances {");
            sb.Append(" currency { ticker } sign majorUnit minorUnit quantity string");
            sb.Append(" }");
            sb.Append(" fungibleItemGarages {");
            sb.Append(" item { fungibleItemId } count");
            sb.Append(" }");
            sb.Append("}}");
            var addrToFungibleItemIdDict = fungibleItemIds is null
                ? new Dictionary<Address, string>()
                : fungibleItemIds.ToDictionary(
                    fungibleItemId => Addresses.GetGarageAddress(
                        agentAddr,
                        HashDigest<SHA256>.FromString(fungibleItemId)),
                    fungibleItemId => fungibleItemId);
            var queryResult = await ExecuteQueryAsync<StateQuery>(
                sb.ToString(),
                source: new StateContext(
                    stateAddresses =>
                    {
                        var arr = new IValue?[stateAddresses.Count];
                        for (var i = 0; i < stateAddresses.Count; i++)
                        {
                            var stateAddr = stateAddresses[i];
                            if (stateAddr.Equals(Addresses.GoldCurrency))
                            {
                                var currency = Currency.Legacy(
                                    "NCG",
                                    2,
                                    new Address("0x47D082a115c63E7b58B1532d20E631538eaFADde"));
                                arr[i] = new GoldCurrencyState(currency).Serialize();
                                continue;
                            }

                            var fungibleItemId = addrToFungibleItemIdDict[stateAddr];
                            var material = new Material(Dictionary.Empty
                                .SetItem("id", 400_000.Serialize())
                                .SetItem("grade", 1.Serialize())
                                .SetItem("item_type", ItemType.Material.Serialize())
                                .SetItem("item_sub_type", ItemSubType.Hourglass.Serialize())
                                .SetItem("elemental_type", ElementalType.Normal.Serialize())
                                .SetItem("item_id", HashDigest<SHA256>.FromString(fungibleItemId).Serialize()));
                            var fig = new FungibleItemGarage(material, 10);
                            arr[i] = fig.Serialize();
                        }

                        return arr;
                    },
                    (_, currency) => currency.Ticker switch
                    {
                        "NCG" => new FungibleAssetValue(currency, 99, 99),
                        "CRYSTAL" or "GARAGE" => new FungibleAssetValue(
                            currency,
                            99,
                            123456789012345678),
                        _ => new FungibleAssetValue(currency, 99, 0),
                    },
                    0L));
            Assert.Null(queryResult.Errors);
            var data = (Dictionary<string, object>)((ExecutionNode)queryResult.Data!).ToValue()!;
            var garages = (Dictionary<string, object>)data["garages"];
            Assert.Equal(agentAddr.ToString(), garages["agentAddr"]);
            Assert.Equal(Addresses.GetGarageBalanceAddress(agentAddr).ToString(), garages["garageBalancesAddr"]);
            if (currencyEnums is not null)
            {
                var garageBalances = ((object[])garages["garageBalances"]).OfType<Dictionary<string, object>>();
                Assert.Equal(currencyEnums.Count(), garageBalances.Count());
                foreach (var (currencyEnum, garageBalance) in currencyEnums.Zip(garageBalances))
                {
                    Assert.Equal(
                        currencyEnum.ToString(),
                        ((Dictionary<string, object>)garageBalance["currency"])["ticker"]);
                }
            }
            else if (currencyTickers is not null)
            {
                var garageBalances = ((object[])garages["garageBalances"]).OfType<Dictionary<string, object>>();
                Assert.Equal(currencyTickers.Count(), garageBalances.Count());
                foreach (var (currencyTicker, garageBalance) in currencyTickers.Zip(garageBalances))
                {
                    Assert.Equal(
                        currencyTicker,
                        ((Dictionary<string, object>)garageBalance["currency"])["ticker"]);
                }
            }

            if (fungibleItemIds is not null)
            {
                var fungibleItemGarages =
                    ((object[])garages["fungibleItemGarages"]).OfType<Dictionary<string, object>>();
                Assert.Equal(fungibleItemIds.Count(), fungibleItemGarages.Count());
                foreach (var (fungibleItemId, fungibleItemGarage) in fungibleItemIds.Zip(fungibleItemGarages))
                {
                    var actual = ((Dictionary<string, object>)fungibleItemGarage["item"])["fungibleItemId"];
                    Assert.Equal(fungibleItemId, actual);
                }
            }
        }

        private static IEnumerable<object[]> GetMemberDataOfGarages()
        {
            var agentAddr = new PrivateKey().ToAddress();
            yield return new object[]
            {
                agentAddr,
                null,
                null,
                null,
            };
            yield return new object[]
            {
                agentAddr,
                new[] { CurrencyEnum.NCG, CurrencyEnum.CRYSTAL, CurrencyEnum.GARAGE },
                null,
                null,
            };
            yield return new object[]
            {
                agentAddr,
                null,
                new[] { "NCG", "CRYSTAL", "GARAGE" },
                null,
            };
            yield return new object[]
            {
                agentAddr,
                null,
                null,
                new[] { new HashDigest<SHA256>().ToString() },
            };
            yield return new object[]
            {
                agentAddr,
                new[] { CurrencyEnum.NCG, CurrencyEnum.CRYSTAL, CurrencyEnum.GARAGE },
                null,
                new[] { new HashDigest<SHA256>().ToString() },
            };
            yield return new object[]
            {
                agentAddr,
                null,
                new[] { "NCG", "CRYSTAL", "GARAGE" },
                new[] { new HashDigest<SHA256>().ToString() },
            };
        }
    }
}
