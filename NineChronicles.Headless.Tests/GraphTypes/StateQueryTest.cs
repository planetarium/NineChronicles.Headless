using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Bencodex;
using Bencodex.Types;
using GraphQL.Execution;
using Lib9c;
using Libplanet.Common;
using Libplanet.Crypto;
using Libplanet.Types.Assets;
using Nekoyume;
using Nekoyume.Model.Elemental;
using Nekoyume.Model.Garages;
using Nekoyume.Model.Item;
using Nekoyume.Model.State;
using NineChronicles.Headless.GraphTypes;
using NineChronicles.Headless.GraphTypes.States;
using NineChronicles.Headless.Tests.Common;
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
            IEnumerable<string> fungibleItemIds,
            IEnumerable<bool>? setToNullForFungibleItemGarages)
        {
            var sb = new StringBuilder("{ garages(");
            sb.Append($"agentAddr: \"{agentAddr.ToString()}\"");
            if (currencyEnums is not null)
            {
                sb.Append(", currencyEnums: [");
                sb.Append(string.Join(", ", currencyEnums));
                sb.Append("]");
            }

            if (currencyTickers is not null)
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
            sb.Append(" fungibleItemId addr item { fungibleItemId } count");
            sb.Append(" }");
            sb.Append("}}");
            var addrToFungibleItemIdDict = fungibleItemIds is null
                ? new Dictionary<Address, string>()
                : fungibleItemIds.ToDictionary(
                    fungibleItemId => Addresses.GetGarageAddress(
                        agentAddr,
                        HashDigest<SHA256>.FromString(fungibleItemId)),
                    fungibleItemId => fungibleItemId);

#pragma warning disable CS0618
            var goldCurrency = Currency.Legacy(
                "NCG",
                2,
                new Address("0x47D082a115c63E7b58B1532d20E631538eaFADde"));
#pragma warning restore CS0618
            MockState mockState = MockState.Empty;

            // NCG
            mockState = mockState
                .SetState(
                    Addresses.GoldCurrency,
                    new GoldCurrencyState(goldCurrency).Serialize());

            // Garage
            // Assume fungibleItemIds and setToNullForFungibleItemGarages are
            // of the same length if both not null
            if (fungibleItemIds is { } fids)
            {
                foreach ((var fid, var i) in fids.Select((item, index) => (item, index)))
                {
                    if (setToNullForFungibleItemGarages is { } flags &&
                        flags.ElementAt(i) == true)
                    {
                        continue;
                    }
                    else
                    {
                        var material = new Material(Dictionary.Empty
                            .SetItem("id", 400_000.Serialize())
                            .SetItem("grade", 1.Serialize())
                            .SetItem("item_type", ItemType.Material.Serialize())
                            .SetItem("item_sub_type", ItemSubType.Hourglass.Serialize())
                            .SetItem("elemental_type", ElementalType.Normal.Serialize())
                            .SetItem("item_id", HashDigest<SHA256>.FromString(fid).Serialize()));

                        mockState = mockState
                            .SetState(
                                Addresses.GetGarageAddress(
                                    agentAddr,
                                    HashDigest<SHA256>.FromString(fid)),
                                new FungibleItemGarage(material, 10).Serialize());
                    }
                }
            }

            // FAVs
            // FIXME: This might need fixing and additional testing;
            // testing without setting up any balance passes the tests;
            // also this is different from the original test setup as there is no way
            // to allow state to have "infinite" FAVs with all possible addresses having FAVs
            mockState = mockState
                .SetBalance(agentAddr, new FungibleAssetValue(goldCurrency, 99, 99))
                .SetBalance(agentAddr, new FungibleAssetValue(Currencies.Crystal, 99, 123456789012345678))
                .SetBalance(agentAddr, new FungibleAssetValue(Currencies.Garage, 99, 123456789012345678))
                .SetBalance(agentAddr, new FungibleAssetValue(Currencies.Mead, 99, 0));

            var queryResult = await ExecuteQueryAsync<StateQuery>(
                sb.ToString(),
                source: new StateContext(
                    mockState,
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
                if (setToNullForFungibleItemGarages is null)
                {
                    foreach (var (fungibleItemId, fungibleItemGarage) in fungibleItemIds
                                 .Zip(fungibleItemGarages))
                    {
                        var actualFungibleItemId = fungibleItemGarage["fungibleItemId"];
                        Assert.Equal(fungibleItemId, actualFungibleItemId);
                        var actualAddr = fungibleItemGarage["addr"];
                        Assert.Equal(
                            Addresses.GetGarageAddress(
                                agentAddr,
                                HashDigest<SHA256>.FromString(fungibleItemId)).ToString(),
                            actualAddr);
                        var actual = ((Dictionary<string, object>)fungibleItemGarage["item"])["fungibleItemId"];
                        Assert.Equal(fungibleItemId, actual);
                    }
                }
                else
                {
                    foreach (var ((fungibleItemId, setToNull), fungibleItemGarage) in fungibleItemIds
                                 .Zip(setToNullForFungibleItemGarages)
                                 .Zip(fungibleItemGarages))
                    {
                        var actualFungibleItemId = fungibleItemGarage["fungibleItemId"];
                        Assert.Equal(fungibleItemId, actualFungibleItemId);
                        var actualAddr = fungibleItemGarage["addr"];
                        Assert.Equal(
                            Addresses.GetGarageAddress(
                                agentAddr,
                                HashDigest<SHA256>.FromString(fungibleItemId)).ToString(),
                            actualAddr);
                        if (setToNull)
                        {
                            Assert.Null(fungibleItemGarage["item"]);
                        }
                        else
                        {
                            var actual = ((Dictionary<string, object>)fungibleItemGarage["item"])["fungibleItemId"];
                            Assert.Equal(fungibleItemId, actual);
                        }
                    }
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
                null,
            };
            yield return new object[]
            {
                agentAddr,
                new[] { CurrencyEnum.NCG, CurrencyEnum.CRYSTAL, CurrencyEnum.GARAGE },
                null,
                null,
                null,
            };
            yield return new object[]
            {
                agentAddr,
                null,
                new[] { "NCG", "CRYSTAL", "GARAGE" },
                null,
                null,
            };
            yield return new object[]
            {
                agentAddr,
                null,
                null,
                new[] { new HashDigest<SHA256>().ToString() },
                null,
            };
            yield return new object[]
            {
                agentAddr,
                null,
                null,
                new[] { new HashDigest<SHA256>().ToString() },
                new[] { true },
            };
            yield return new object[]
            {
                agentAddr,
                new[] { CurrencyEnum.NCG, CurrencyEnum.CRYSTAL, CurrencyEnum.GARAGE },
                null,
                new[] { new HashDigest<SHA256>().ToString() },
                null,
            };
            yield return new object[]
            {
                agentAddr,
                null,
                new[] { "NCG", "CRYSTAL", "GARAGE" },
                new[] { new HashDigest<SHA256>().ToString() },
                null,
            };
        }
    }
}
