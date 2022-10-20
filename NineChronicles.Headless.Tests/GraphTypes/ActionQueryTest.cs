using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Security.Cryptography;
using System.Threading.Tasks;
using Bencodex;
using Bencodex.Types;
using GraphQL;
using GraphQL.Execution;
using Libplanet;
using Libplanet.Action;
using Libplanet.Assets;
using Libplanet.Blockchain;
using Libplanet.Blockchain.Policies;
using Libplanet.Crypto;
using Libplanet.Store;
using Libplanet.Store.Trie;
using Nekoyume;
using Nekoyume.Action;
using Nekoyume.Helper;
using Nekoyume.Model.EnumType;
using Nekoyume.Model.State;
using Nekoyume.TableData;
using NineChronicles.Headless.GraphTypes;
using Xunit;
using static NineChronicles.Headless.Tests.GraphQLTestUtils;
using NCAction = Libplanet.Action.PolymorphicAction<Nekoyume.Action.ActionBase>;

namespace NineChronicles.Headless.Tests.GraphTypes
{
    public class ActionQueryTest
    {
        private readonly Codec _codec;
        private readonly StandaloneContext _standaloneContext;

        public ActionQueryTest()
        {
            _codec = new Codec();
            var store = new DefaultStore(null);
            var stateStore = new TrieStateStore(new DefaultKeyValueStore(null));
            var minerPrivateKey = new PrivateKey();
            var genesisBlock = BlockChain<NCAction>.MakeGenesisBlock(
                new PolymorphicAction<ActionBase>[]
                {
                    new InitializeStates(
                        rankingState: new RankingState0(),
                        shopState: new ShopState(),
                        gameConfigState: new GameConfigState(),
                        redeemCodeState: new RedeemCodeState(Bencodex.Types.Dictionary.Empty
                            .Add("address", RedeemCodeState.Address.Serialize())
                            .Add("map", Bencodex.Types.Dictionary.Empty)
                        ),
                        adminAddressState: new AdminState(new PrivateKey().ToAddress(), 1500000),
                        activatedAccountsState: new ActivatedAccountsState(),
#pragma warning disable CS0618
                        // Use of obsolete method Currency.Legacy(): https://github.com/planetarium/lib9c/discussions/1319
                        goldCurrencyState: new GoldCurrencyState(Currency.Legacy("NCG", 2, minerPrivateKey.ToAddress())),
#pragma warning restore CS0618
                        goldDistributions: Array.Empty<GoldDistribution>(),
                        tableSheets: new Dictionary<string, string>(),
                        pendingActivationStates: new PendingActivationState[]{ }
                    ),
                },
                privateKey: minerPrivateKey
            );
            var blockchain = new BlockChain<PolymorphicAction<ActionBase>>(
                new BlockPolicy<PolymorphicAction<ActionBase>>(),
                new VolatileStagePolicy<PolymorphicAction<ActionBase>>(),
                store,
                stateStore,
                genesisBlock);
            _standaloneContext = new StandaloneContext
            {
                BlockChain = blockchain,
                Store = store,
            };
        }

        [Theory]
        [ClassData(typeof(StakeFixture))]
        public async Task Stake(BigInteger amount)
        {
            string query = $@"
            {{
                stake(amount: {amount})
            }}";

            var queryResult = await ExecuteQueryAsync<ActionQuery>(query, standaloneContext: _standaloneContext);
            var data = (Dictionary<string, object>)((ExecutionNode)queryResult.Data!).ToValue()!;
            NCAction action = new Stake(amount);
            var expected = new Dictionary<string, object>()
            {
                ["stake"] = ByteUtil.Hex(_codec.Encode(action.PlainValue)),
            };
            var plainValue = _codec.Decode(ByteUtil.ParseHex((string)data["stake"]));
            var expectedPlainValue = _codec.Decode(ByteUtil.ParseHex((string)expected["stake"]));
            Assert.IsType<Dictionary>(plainValue);
            var dictionary = (Dictionary)plainValue;
            Assert.IsType<Stake>(DeserializeNCAction(dictionary).InnerAction);
            var actualAmount = ((Dictionary)dictionary["values"])["am"].ToBigInteger();
            var expectedAmount = ((Dictionary)((Dictionary)expectedPlainValue)["values"])["am"].ToBigInteger();
            Assert.Equal(expectedAmount, actualAmount);
        }

        [Fact]
        public async Task ClaimStakeReward()
        {
            var avatarAddress = new PrivateKey().ToAddress();
            string query = $@"
            {{
                claimStakeReward(avatarAddress: ""{avatarAddress.ToString()}"")
            }}";

            var queryResult = await ExecuteQueryAsync<ActionQuery>(query, standaloneContext: _standaloneContext);
            var data = (Dictionary<string, object>)((ExecutionNode)queryResult.Data!).ToValue()!;
            var plainValue = _codec.Decode(ByteUtil.ParseHex((string)data["claimStakeReward"]));
            Assert.IsType<Dictionary>(plainValue);
            var dictionary = (Dictionary)plainValue;
            Assert.IsType<ClaimStakeReward>(DeserializeNCAction(dictionary).InnerAction);
        }

        [Fact]
        public async Task MigrateMonsterCollection()
        {
            var avatarAddress = new PrivateKey().ToAddress();
            string query = $@"
            {{
                migrateMonsterCollection(avatarAddress: ""{avatarAddress.ToString()}"")
            }}";

            var queryResult = await ExecuteQueryAsync<ActionQuery>(query, standaloneContext: _standaloneContext);
            var data = (Dictionary<string, object>)((ExecutionNode)queryResult.Data!).ToValue()!;
            var plainValue = _codec.Decode(ByteUtil.ParseHex((string)data["migrateMonsterCollection"]));
            var dictionary = Assert.IsType<Dictionary>(plainValue);
            var action = Assert.IsType<MigrateMonsterCollection>(DeserializeNCAction(dictionary).InnerAction);
            Assert.Equal(avatarAddress, action.AvatarAddress);
        }

        private class StakeFixture : IEnumerable<object[]>
        {
            private readonly List<object[]> _data = new List<object[]>
            {
                new object[]
                {
                    new BigInteger(1),
                },
                new object[]
                {
                    new BigInteger(100),
                },
            };

            public IEnumerator<object[]> GetEnumerator() => _data.GetEnumerator();

            IEnumerator IEnumerable.GetEnumerator() => _data.GetEnumerator();
        }
        [Theory]
        [InlineData("false", false)]
        [InlineData("true", true)]
        [InlineData(null, false)]
        public async Task Grinding(string chargeApValue, bool chargeAp)
        {
            var avatarAddress = new PrivateKey().ToAddress();
            var equipmentId = Guid.NewGuid();
            string queryArgs = $"avatarAddress: \"{avatarAddress.ToString()}\", equipmentIds: [{string.Format($"\"{equipmentId}\"")}]";
            if (!string.IsNullOrEmpty(chargeApValue))
            {
                queryArgs += $", chargeAp: {chargeApValue}";
            }
            string query = $@"
            {{
                grinding({queryArgs})
            }}";

            var queryResult = await ExecuteQueryAsync<ActionQuery>(query, standaloneContext: _standaloneContext);
            var data = (Dictionary<string, object>)((ExecutionNode)queryResult.Data!).ToValue()!;
            var plainValue = _codec.Decode(ByteUtil.ParseHex((string)data["grinding"]));
            Assert.IsType<Dictionary>(plainValue);
            var polymorphicAction = DeserializeNCAction(plainValue);
            var action = Assert.IsType<Grinding>(polymorphicAction.InnerAction);

            Assert.Equal(avatarAddress, action.AvatarAddress);
            Assert.Single(action.EquipmentIds);
            Assert.Equal(equipmentId, action.EquipmentIds.First());
            Assert.Equal(chargeAp, action.ChargeAp);
        }

        [Fact]
        public async Task UnlockEquipmentRecipe()
        {
            var avatarAddress = new PrivateKey().ToAddress();
            string query = $@"
            {{
                unlockEquipmentRecipe(avatarAddress: ""{avatarAddress.ToString()}"", recipeIds: [2, 3])
            }}";

            var queryResult = await ExecuteQueryAsync<ActionQuery>(query, standaloneContext: _standaloneContext);
            var data = (Dictionary<string, object>)((ExecutionNode)queryResult.Data!).ToValue()!;
            var plainValue = _codec.Decode(ByteUtil.ParseHex((string)data["unlockEquipmentRecipe"]));
            Assert.IsType<Dictionary>(plainValue);
            var polymorphicAction = DeserializeNCAction(plainValue);
            var action = Assert.IsType<UnlockEquipmentRecipe>(polymorphicAction.InnerAction);

            Assert.Equal(avatarAddress, action.AvatarAddress);
            Assert.Equal(
                new List<int>
                {
                    2,
                    3,
                },
                action.RecipeIds
            );
        }

        [Fact]
        public async Task UnlockWorld()
        {
            var avatarAddress = new PrivateKey().ToAddress();
            string query = $@"
            {{
                unlockWorld(avatarAddress: ""{avatarAddress.ToString()}"", worldIds: [2, 3])
            }}";

            var queryResult = await ExecuteQueryAsync<ActionQuery>(query, standaloneContext: _standaloneContext);
            var data = (Dictionary<string, object>)((ExecutionNode)queryResult.Data!).ToValue()!;
            var plainValue = _codec.Decode(ByteUtil.ParseHex((string)data["unlockWorld"]));
            Assert.IsType<Dictionary>(plainValue);
            var polymorphicAction = DeserializeNCAction(plainValue);
            var action = Assert.IsType<UnlockWorld>(polymorphicAction.InnerAction);

            Assert.Equal(avatarAddress, action.AvatarAddress);
            Assert.Equal(
                new List<int>
                {
                    2,
                    3,
                },
                action.WorldIds
            );
        }

        [Theory]
        [InlineData("NCG", true)]
        [InlineData("NCG", false)]
        [InlineData("CRYSTAL", true)]
        [InlineData("CRYSTAL", false)]
        public async Task TransferAsset(string currencyType, bool memo)
        {
            var recipient = new PrivateKey().ToAddress();
            var sender = new PrivateKey().ToAddress();
            var args = $"recipient: \"{recipient}\", sender: \"{sender}\", amount: \"17.5\", currency: {currencyType}";
            if (memo)
            {
                args += ", memo: \"memo\"";
            }
            var query = $"{{ transferAsset({args}) }}";
            var queryResult = await ExecuteQueryAsync<ActionQuery>(query, standaloneContext: _standaloneContext);
            var data = (Dictionary<string, object>)((ExecutionNode)queryResult.Data!).ToValue()!;
            var plainValue = _codec.Decode(ByteUtil.ParseHex((string)data["transferAsset"]));
            Assert.IsType<Dictionary>(plainValue);
            var polymorphicAction = DeserializeNCAction(plainValue);
            var action = Assert.IsType<TransferAsset>(polymorphicAction.InnerAction);
            var rawState = _standaloneContext.BlockChain!.GetState(Addresses.GoldCurrency);
            var goldCurrencyState = new GoldCurrencyState((Dictionary)rawState);
            Currency currency = currencyType == "NCG" ? goldCurrencyState.Currency : CrystalCalculator.CRYSTAL;

            Assert.Equal(recipient, action.Recipient);
            Assert.Equal(sender, action.Sender);
            Assert.Equal(FungibleAssetValue.Parse(currency, "17.5"), action.Amount);
            if (memo)
            {
                Assert.Equal("memo", action.Memo);
            }
            else
            {
                Assert.Null(action.Memo);
            }
        }

        [Fact]
        public async Task PatchTableSheet()
        {
            var tableName = nameof(ArenaSheet);
            var csv = @"
            id,round,arena_type,start_block_index,end_block_index,required_medal_count,entrance_fee,ticket_price,additional_ticket_price
            1,1,OffSeason,1,2,0,0,5,2
            1,2,Season,3,4,0,100,50,20
            1,3,OffSeason,5,1005284,0,0,5,2
            1,4,Season,1005285,1019684,0,100,50,20
            1,5,OffSeason,1019685,1026884,0,0,5,2
            1,6,Season,1026885,1034084,0,10000,50,20
            1,7,OffSeason,1034085,1041284,0,0,5,2
            1,8,Championship,1041285,1062884,20,100000,50,20
            2,1,OffSeason,1062885,200000000,0,0,5,2
            2,2,Season,200000001,200000002,0,100,50,20
            2,3,OffSeason,200000003,200000004,0,0,5,2
            2,4,Season,200000005,200000006,0,100,50,20
            2,5,OffSeason,200000007,200000008,0,0,5,2
            2,6,Season,200000009,200000010,0,10000,50,20
            2,7,OffSeason,200000011,200000012,0,0,5,2
            2,8,Championship,200000013,200000014,20,100000,50,20
            ";
            var query = $"{{ patchTableSheet(tableName: \"{tableName}\", tableCsv: \"\"\"{csv}\"\"\") }}";
            var queryResult = await ExecuteQueryAsync<ActionQuery>(query, standaloneContext: _standaloneContext);
            var data = (Dictionary<string, object>)((ExecutionNode)queryResult.Data!).ToValue()!;
            var plainValue = _codec.Decode(ByteUtil.ParseHex((string)data["patchTableSheet"]));
            Assert.IsType<Dictionary>(plainValue);
            var polymorphicAction = DeserializeNCAction(plainValue);
            var action = Assert.IsType<PatchTableSheet>(polymorphicAction.InnerAction);

            Assert.Equal(tableName, action.TableName);

            // FIXME parameterize sheet type.
            var sheet = new ArenaSheet();
            sheet.Set(action.TableCsv);
            var row = sheet.First!;
            var round = row.Round.First();

            Assert.Equal(1, row.ChampionshipId);
            Assert.Equal(1, round.Round);
            Assert.Equal(ArenaType.OffSeason, round.ArenaType);
            Assert.Equal(1, round.StartBlockIndex);
            Assert.Equal(2, round.EndBlockIndex);
            Assert.Equal(0, round.RequiredMedalCount);
            Assert.Equal(0, round.EntranceFee);
            Assert.Equal(5, round.TicketPrice);
            Assert.Equal(2, round.AdditionalTicketPrice);
        }

        [Fact]
        public async Task PatchTableSheet_Invalid_TableName()
        {
            var tableName = "Sheet";
            var csv = "id";
            var query = $"{{ patchTableSheet(tableName: \"{tableName}\", tableCsv: \"\"\"{csv}\"\"\") }}";
            var queryResult = await ExecuteQueryAsync<ActionQuery>(query, standaloneContext: _standaloneContext);
            var error = queryResult.Errors!.Single();
            Assert.Contains("Invalid tableName.", error.Message);
        }
        private NCAction DeserializeNCAction(IValue value)
        {
#pragma warning disable CS0612
            NCAction action = new NCAction();
#pragma warning restore CS0612
            action.LoadPlainValue(value);
            return action;
        }

        [Theory]
        [InlineData(true, false, false, false)]
        [InlineData(false, true, false, false)]
        [InlineData(false, false, true, false)]
        [InlineData(false, false, false, true)]
        public async Task Raid(bool equipment, bool costume, bool food, bool payNcg)
        {
            var avatarAddress = new PrivateKey().ToAddress();
            var args = $"avatarAddress: \"{avatarAddress}\"";
            var guid = Guid.NewGuid();
            if (equipment)
            {
                args += $", equipmentIds: [\"{guid}\"]";
            }

            if (costume)
            {
                args += $", costumeIds: [\"{guid}\"]";
            }

            if (food)
            {
                args += $", foodIds: [\"{guid}\"]";
            }

            if (payNcg)
            {
                args += $", payNcg: true";
            }

            var query = $"{{ raid({args}) }}";
            var queryResult = await ExecuteQueryAsync<ActionQuery>(query, standaloneContext: _standaloneContext);
            var data = (Dictionary<string, object>)((ExecutionNode)queryResult.Data!).ToValue()!;
            var plainValue = _codec.Decode(ByteUtil.ParseHex((string)data["raid"]));
            Assert.IsType<Dictionary>(plainValue);
            var polymorphicAction = DeserializeNCAction(plainValue);
            var action = Assert.IsType<Raid>(polymorphicAction.InnerAction);

            Assert.Equal(avatarAddress, action.AvatarAddress);
            if (equipment)
            {
                var equipmentId = Assert.Single(action.EquipmentIds);
                Assert.Equal(guid, equipmentId);
            }
            else
            {
                Assert.Empty(action.EquipmentIds);
            }

            if (costume)
            {
                var costumeId = Assert.Single(action.CostumeIds);
                Assert.Equal(guid, costumeId);
            }
            else
            {
                Assert.Empty(action.CostumeIds);
            }

            if (food)
            {
                var foodId = Assert.Single(action.FoodIds);
                Assert.Equal(guid, foodId);
            }
            else
            {
                Assert.Empty(action.FoodIds);
            }

            Assert.Equal(payNcg, action.PayNcg);
        }

        [Fact]
        public async Task ClaimRaidReward()
        {
            var avatarAddress = new PrivateKey().ToAddress();
            var query = $"{{ claimRaidReward(avatarAddress: \"{avatarAddress}\") }}";
            var queryResult = await ExecuteQueryAsync<ActionQuery>(query, standaloneContext: _standaloneContext);
            var data = (Dictionary<string, object>)((ExecutionNode)queryResult.Data!).ToValue()!;
            var plainValue = _codec.Decode(ByteUtil.ParseHex((string)data["claimRaidReward"]));
            Assert.IsType<Dictionary>(plainValue);
            var polymorphicAction = DeserializeNCAction(plainValue);
            var action = Assert.IsType<ClaimRaidReward>(polymorphicAction.InnerAction);

            Assert.Equal(avatarAddress, action.AvatarAddress);
        }

        [Fact]
        public async Task ClaimWorldBossKillReward()
        {
            var avatarAddress = new PrivateKey().ToAddress();
            var query = $"{{ claimWorldBossKillReward(avatarAddress: \"{avatarAddress}\") }}";
            var queryResult = await ExecuteQueryAsync<ActionQuery>(query, standaloneContext: _standaloneContext);
            var data = (Dictionary<string, object>)((ExecutionNode)queryResult.Data!).ToValue()!;
            var plainValue = _codec.Decode(ByteUtil.ParseHex((string)data["claimWorldBossKillReward"]));
            Assert.IsType<Dictionary>(plainValue);
            var polymorphicAction = DeserializeNCAction(plainValue);
            var action = Assert.IsType<ClaimWordBossKillReward>(polymorphicAction.InnerAction);

            Assert.Equal(avatarAddress, action.AvatarAddress);
        }

        [Theory]
        [InlineData(true, 2)]
        [InlineData(false, 1)]
        public async Task PrepareRewardAssets(bool mintersExist, int expectedCount)
        {
            var rewardPoolAddress = new PrivateKey().ToAddress();
            var assets = "{quantity: 100, decimalPlaces: 0, ticker: \"CRYSTAL\"}";
            if (mintersExist)
            {
                assets += $", {{quantity: 100, decimalPlaces: 2, ticker: \"NCG\", minters: [\"{rewardPoolAddress}\"]}}";
            }
            var query = $"{{ prepareRewardAssets(rewardPoolAddress: \"{rewardPoolAddress}\", assets: [{assets}]) }}";
            var queryResult = await ExecuteQueryAsync<ActionQuery>(query, standaloneContext: _standaloneContext);
            var data = (Dictionary<string, object>)((ExecutionNode)queryResult.Data!).ToValue()!;
            var plainValue = _codec.Decode(ByteUtil.ParseHex((string)data["prepareRewardAssets"]));
            Assert.IsType<Dictionary>(plainValue);
            var polymorphicAction = DeserializeNCAction(plainValue);
            var action = Assert.IsType<PrepareRewardAssets>(polymorphicAction.InnerAction);

            Assert.Equal(rewardPoolAddress, action.RewardPoolAddress);
            Assert.Equal(expectedCount, action.Assets.Count);

            var crystal = action.Assets.First(r => r.Currency.Ticker == "CRYSTAL");
            Assert.Equal(100, crystal.MajorUnit);
            Assert.Equal(0, crystal.Currency.DecimalPlaces);
            Assert.Null(crystal.Currency.Minters);

            if (mintersExist)
            {
                var ncg = action.Assets.First(r => r.Currency.Ticker == "NCG");
                Assert.Equal(100, ncg.MajorUnit);
                Assert.Equal(2, ncg.Currency.DecimalPlaces);
                var minter = Assert.Single(ncg.Currency.Minters!);
                Assert.Equal(rewardPoolAddress, minter);
            }
        }
    }
}
