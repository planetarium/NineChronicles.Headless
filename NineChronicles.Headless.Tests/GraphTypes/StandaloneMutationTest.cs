using GraphQL;
using Libplanet;
using Libplanet.Action;
using Libplanet.Assets;
using Libplanet.Blockchain;
using Libplanet.Blocks;
using Libplanet.Crypto;
using Libplanet.KeyStore;
using Libplanet.Tx;
using Nekoyume;
using Nekoyume.Action;
using Nekoyume.Model;
using Nekoyume.Model.State;
using Nekoyume.TableData;
using NineChronicles.Headless.Tests.Common;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Bencodex.Types;
using Xunit;
using Xunit.Abstractions;

namespace NineChronicles.Headless.Tests.GraphTypes
{
    public class StandaloneMutationTest : GraphQLTestBase
    {
        private readonly Dictionary<string, string> _sheets = null;

        private readonly TableSheets _tableSheets = null;

        public StandaloneMutationTest(ITestOutputHelper output) : base(output)
        {
            var fixturePath = Path.Combine("..", "..", "..", "..", "Lib9c", ".Lib9c.Tests", "Data", "TableCSV");
            _sheets = TableSheetsImporter.ImportSheets(fixturePath);
            _tableSheets = new TableSheets(_sheets);
        }

        [Fact]
        public async Task CreatePrivateKey()
        {
            // FIXME: passphrase로 "passphrase" 대신 랜덤 문자열을 사용하면 좋을 것 같습니다.
            var result = await ExecuteQueryAsync(
                "mutation { keyStore { createPrivateKey(passphrase: \"passphrase\") { publicKey { address } } } }");
            var createdPrivateKeyAddress = result.Data.As<Dictionary<string, object>>()["keyStore"]
                .As<Dictionary<string, object>>()["createPrivateKey"]
                .As<Dictionary<string, object>>()["publicKey"]
                .As<Dictionary<string, object>>()["address"].As<string>();

            Assert.Contains(KeyStore.List(),
                t => t.Item2.Address.ToString() == createdPrivateKeyAddress);
        }

        [Fact]
        public async Task CreatePrivateKeyWithGivenPrivateKey()
        {
            // FIXME: passphrase로 "passphrase" 대신 랜덤 문자열을 사용하면 좋을 것 같습니다.
            var privateKey = new PrivateKey();
            var privateKeyHex = ByteUtil.Hex(privateKey.ByteArray);
            var result = await ExecuteQueryAsync(
                $"mutation {{ keyStore {{ createPrivateKey(passphrase: \"passphrase\", privateKey: \"{privateKeyHex}\") {{ hex publicKey {{ address }} }} }} }}");
            var privateKeyResult = result.Data.As<Dictionary<string, object>>()["keyStore"]
                .As<Dictionary<string, object>>()["createPrivateKey"]
                .As<Dictionary<string, object>>();
            var createdPrivateKeyHex = privateKeyResult
                .As<Dictionary<string, object>>()["hex"].As<string>();
            var createdPrivateKeyAddress = privateKeyResult
                .As<Dictionary<string, object>>()["publicKey"]
                .As<Dictionary<string, object>>()["address"].As<string>();

            Assert.Equal(privateKey, new PrivateKey(ByteUtil.ParseHex(createdPrivateKeyHex)));
            Assert.Contains(KeyStore.List(),
                t => t.Item2.Address.ToString() == createdPrivateKeyAddress);
        }

        [Fact]
        public async Task RevokePrivateKey()
        {
            var privateKey = new PrivateKey();
            var passphrase = "";

            var protectedPrivateKey = ProtectedPrivateKey.Protect(privateKey, passphrase);
            KeyStore.Add(protectedPrivateKey);

            var address = privateKey.ToAddress();

            var result = await ExecuteQueryAsync(
                $"mutation {{ keyStore {{ revokePrivateKey(address: \"{address.ToHex()}\") {{ address }} }} }}");
            var revokedPrivateKeyAddress = result.Data.As<Dictionary<string, object>>()["keyStore"]
                .As<Dictionary<string, object>>()["revokePrivateKey"]
                .As<Dictionary<string, object>>()["address"].As<string>();

            Assert.DoesNotContain(KeyStore.List(),
                t => t.Item2.Address.ToString() == revokedPrivateKeyAddress);
            Assert.Equal(address.ToString(), revokedPrivateKeyAddress);
        }

        [Fact]
        public async Task ActivateAccount()
        {
            var adminPrivateKey = new PrivateKey();
            var adminAddress = adminPrivateKey.ToAddress();
            var activateAccounts = new[] { adminAddress }.ToImmutableHashSet();

            Block<PolymorphicAction<ActionBase>> genesis =
                MakeGenesisBlock(adminAddress, new Currency("NCG", 2, minters: null), activateAccounts);
            NineChroniclesNodeService service = ServiceBuilder.CreateNineChroniclesNodeService(genesis);
            StandaloneContextFx.NineChroniclesNodeService = service;
            StandaloneContextFx.BlockChain = service.Swarm.BlockChain;

            var blockChain = StandaloneContextFx.BlockChain;

            var nonce = new byte[] { 0x00, 0x01, 0x02, 0x03 };
            var privateKey = new PrivateKey();
            (ActivationKey activationKey, PendingActivationState pendingActivation) =
                ActivationKey.Create(privateKey, nonce);
            PolymorphicAction<ActionBase> action = new CreatePendingActivation(pendingActivation);
            blockChain.MakeTransaction(adminPrivateKey, new[] { action });
            await blockChain.MineBlock(adminAddress);

            var encodedActivationKey = activationKey.Encode();
            var queryResult = await ExecuteQueryAsync(
                $"mutation {{ activationStatus {{ activateAccount(encodedActivationKey: \"{encodedActivationKey}\") }} }}");
            await blockChain.MineBlock(adminAddress);

            var result = (bool)queryResult.Data
                .As<Dictionary<string, object>>()["activationStatus"]
                .As<Dictionary<string, object>>()["activateAccount"];
            Assert.True(result);

            var state = (Bencodex.Types.Dictionary)blockChain.GetState(
                ActivatedAccountsState.Address);
            var activatedAccountsState = new ActivatedAccountsState(state);
            Address userAddress = service.PrivateKey.ToAddress();
            Assert.True(activatedAccountsState.Accounts.Contains(userAddress));
        }

        [Fact]
        public async Task TransferGold()
        {
            var goldCurrency = new Currency("NCG", 2, minter: null);
            Block<PolymorphicAction<ActionBase>> genesis =
                MakeGenesisBlock(
                    default,
                    goldCurrency,
                    ImmutableHashSet<Address>.Empty
                );
            NineChroniclesNodeService service = ServiceBuilder.CreateNineChroniclesNodeService(genesis);
            StandaloneContextFx.NineChroniclesNodeService = service;
            StandaloneContextFx.BlockChain = service.BlockChain;

            Address senderAddress = service.PrivateKey.ToAddress();

            var blockChain = StandaloneContextFx.BlockChain;
            var store = service.Store;
            await blockChain.MineBlock(senderAddress);
            await blockChain.MineBlock(senderAddress);

            // 10 + 10 (mining rewards)
            Assert.Equal(
                20 * goldCurrency,
                blockChain.GetBalance(senderAddress, goldCurrency)
            );

            Address recipient = new PrivateKey().ToAddress();
            var query = $"mutation {{ transferGold(recipient: \"{recipient}\", amount: \"17.5\") }}";
            ExecutionResult result = await ExecuteQueryAsync(query);

            var stagedTxIds = blockChain.GetStagedTransactionIds().ToImmutableList();
            Assert.Single(stagedTxIds);

            var expectedResult = new Dictionary<string, object>
            {
                ["transferGold"] = stagedTxIds.Single().ToString(),
            };
            Assert.Null(result.Errors);
            Assert.Equal(expectedResult, result.Data);

            await blockChain.MineBlock(recipient);

            // 10 + 10 - 17.5(transfer)
            Assert.Equal(
                FungibleAssetValue.Parse(goldCurrency, "2.5"),
                blockChain.GetBalance(senderAddress, goldCurrency)
            );

            // 0 + 17.5(transfer) + 10(mining reward)
            Assert.Equal(
                FungibleAssetValue.Parse(goldCurrency, "27.5"),
                blockChain.GetBalance(recipient, goldCurrency)
            );
        }

        [Theory]
        [MemberData(nameof(CreateAvatarMember))]
        public async Task CreateAvatar(string name, int index, int hair, int lens, int ear, int tail)
        {
            var playerPrivateKey = new PrivateKey();
            var ranking = new RankingState();
            for (var i = 0; i < RankingState.RankingMapCapacity; i++)
            {
                ranking.RankingMap[RankingState.Derive(i)] = new HashSet<Address>().ToImmutableHashSet();
            }
            var blockChain = GetContextFx(playerPrivateKey, ranking);
            var query = $@"mutation {{
                action {{
                    createAvatar(avatarName: ""{name}"", avatarIndex: {index}, hairIndex: {hair}, lensIndex: {lens}, earIndex: {ear}, tailIndex: {tail})
                    }}
                }}";
            ExecutionResult result = await ExecuteQueryAsync(query);
            Assert.Null(result.Errors);

            var txIds = blockChain.GetStagedTransactionIds();
            Assert.Single(txIds);
            var tx = blockChain.GetTransaction(txIds.First());
            Assert.Single(tx.Actions);
            var action = (CreateAvatar2) tx.Actions.First().InnerAction;
            Assert.Equal(name, action.name);
            Assert.Equal(index, action.index);
            Assert.Equal(hair, action.hair);
            Assert.Equal(lens, action.lens);
            Assert.Equal(ear, action.ear);
            Assert.Equal(tail, action.tail);
        }

        public static IEnumerable<object[]> CreateAvatarMember => new List<object[]>
        {
            new object[]
            {
                "createByMutation",
                1,
                2,
                3,
                4,
                5,
            },
            new object[]
            {
                "createByMutation2",
                2,
                3,
                4,
                5,
                6,
            },
            new object[]
            {
                "createByMutation3",
                0,
                0,
                0,
                0,
                0,
            },
        };

        [Theory]
        [MemberData(nameof(HackAndSlashMember))]
        public async Task HackAndSlash(Address avatarAddress, int worldId, int stageId, Address weeklyArenaAddress,
            Address rankingArenaAddress, List<Guid> costumeIds, List<Guid> equipmentIds, List<Guid> consumableIds)
        {
            var playerPrivateKey = new PrivateKey();
            var ranking = new RankingState();
            for (var i = 0; i < RankingState.RankingMapCapacity; i++)
            {
                ranking.RankingMap[RankingState.Derive(i)] = new HashSet<Address>().ToImmutableHashSet();
            }
            var blockChain = GetContextFx(playerPrivateKey, ranking);
            var queryArgs= $"avatarAddress: \"{avatarAddress}\", worldId: {worldId}, stageId: {stageId}, weeklyArenaAddress: \"{weeklyArenaAddress}\", rankingArenaAddress: \"{rankingArenaAddress}\"";
            if (costumeIds.Any())
            {
                queryArgs += $", costumeIds: [{string.Join(",", costumeIds.Select(r => string.Format($"\"{r}\"")))}]";
            }
            if (equipmentIds.Any())
            {
                queryArgs += $", equipmentIds: [{string.Join(",", equipmentIds.Select(r => string.Format($"\"{r}\"")))}]";
            }
            if (consumableIds.Any())
            {
                queryArgs += $", consumableIds: [{string.Join(",", consumableIds.Select(r => string.Format($"\"{r}\"")))}]";
            }
            var query = @$"mutation {{ action {{ hackAndSlash({queryArgs}) }} }}";
            ExecutionResult result = await ExecuteQueryAsync(query);
            Assert.Null(result.Errors);

            var txIds = blockChain.GetStagedTransactionIds();
            Assert.Single(txIds);
            var tx = blockChain.GetTransaction(txIds.First());
            Assert.Single(tx.Actions);
            var action = (HackAndSlash4) tx.Actions.First().InnerAction;
            Assert.Equal(avatarAddress, action.avatarAddress);
            Assert.Equal(worldId, action.worldId);
            Assert.Equal(stageId, action.stageId);
            Assert.Equal(weeklyArenaAddress, action.WeeklyArenaAddress);
            Assert.Equal(rankingArenaAddress, action.RankingMapAddress);
            Assert.Equal(costumeIds, action.costumes);
            Assert.Equal(equipmentIds, action.equipments);
            Assert.Equal(consumableIds, action.foods);
        }

        public static IEnumerable<object[]> HackAndSlashMember => new List<object[]>
        {
            new object[]
            {
                new Address(),
                1,
                2,
                new Address(),
                new Address(),
                new List<Guid>(),
                new List<Guid>(),
                new List<Guid>(),
            },
            new object[]
            {
                new Address(),
                2,
                3,
                new Address(),
                new Address(),
                new List<Guid>
                {
                    Guid.NewGuid(),
                },
                new List<Guid>
                {
                    Guid.NewGuid(),
                    Guid.NewGuid(),
                },
                new List<Guid>
                {
                    Guid.NewGuid(),
                    Guid.NewGuid(),
                    Guid.NewGuid(),
                },
            },
        };

        [Fact]
        public async Task DailyReward()
        {
            var playerPrivateKey = new PrivateKey();
            var ranking = new RankingState();
            for (var i = 0; i < RankingState.RankingMapCapacity; i++)
            {
                ranking.RankingMap[RankingState.Derive(i)] = new HashSet<Address>().ToImmutableHashSet();
            }
            var blockChain = GetContextFx(playerPrivateKey, ranking);
            var avatarAddress = new Address();
            var query = $@"mutation {{
                action {{
                    dailyReward(avatarAddress: ""{avatarAddress}"")
                    }}
                }}";
            ExecutionResult result = await ExecuteQueryAsync(query);
            Assert.Null(result.Errors);

            var txIds = blockChain.GetStagedTransactionIds();
            Assert.Single(txIds);
            var tx = blockChain.GetTransaction(txIds.First());
            Assert.Single(tx.Actions);
            var action = (DailyReward) tx.Actions.First().InnerAction;
            Assert.Equal(avatarAddress, action.avatarAddress);
        }

        [Fact]
        public async Task Buy()
        {
            var sellerAgentAddress = new Address();
            var sellerAvatarAddress = new Address();
            var productId = Guid.NewGuid();
            var playerPrivateKey = new PrivateKey();
            var blockChain = GetContextFx(playerPrivateKey, new RankingState());
            var query = $@"mutation {{
                action {{
                    buy(sellerAgentAddress: ""{sellerAgentAddress}"", sellerAvatarAddress: ""{sellerAvatarAddress}"", buyerAvatarIndex: 0, productId: ""{productId}"")
                }}
            }}";
            var result = await ExecuteQueryAsync(query);
            Assert.Null(result.Errors);

            var txIds = blockChain.GetStagedTransactionIds();
            Assert.Single(txIds);
            var tx = blockChain.GetTransaction(txIds.First());
            Assert.Single(tx.Actions);
            var action = (Buy4) tx.Actions.First().InnerAction;
            Assert.Equal(productId, action.productId);
            Assert.Equal(sellerAgentAddress, action.sellerAgentAddress);
            Assert.Equal(sellerAvatarAddress, action.sellerAvatarAddress);
            Assert.Equal(tx.Signer.Derive(string.Format(CreateAvatar2.DeriveFormat, 0)), action.buyerAvatarAddress);
        }

        [Theory]
        [MemberData(nameof(CombinationEquipmentMember))]
        public async Task CombinationEquipment(Address avatarAddress, int recipeId, int slotIndex, int? subRecipeId)
        {
            var playerPrivateKey = new PrivateKey();
            var blockChain = GetContextFx(playerPrivateKey, new RankingState());
            var queryArgs = $"avatarAddress: \"{avatarAddress}\", recipeId: {recipeId} slotIndex: {slotIndex}";
            if (!(subRecipeId is null))
            {
                queryArgs += $"subRecipeId: {subRecipeId}";
            }
            var query = @$"mutation {{ action {{ combinationEquipment({queryArgs}) }} }}";
            var result = await ExecuteQueryAsync(query);
            Assert.Null(result.Errors);

            var txIds = blockChain.GetStagedTransactionIds();
            Assert.Single(txIds);
            var tx = blockChain.GetTransaction(txIds.First());
            Assert.Single(tx.Actions);
            var action = (CombinationEquipment4) tx.Actions.First().InnerAction;
            Assert.Equal(avatarAddress, action.AvatarAddress);
            Assert.Equal(recipeId, action.RecipeId);
            Assert.Equal(slotIndex, action.SlotIndex);
            Assert.Equal(subRecipeId, action.SubRecipeId);
        }

        public static IEnumerable<object[]> CombinationEquipmentMember => new List<object[]>
        {
            new object[]
            {
                new Address(),
                1,
                0,
                0,
            },
            new object[]
            {
                new Address(),
                1,
                2,
                1,
            },
            new object[]
            {
                new Address(),
                2,
                3,
                null,
            },
        };

        [Theory]
        [MemberData(nameof(ItemEnhancementMember))]
        public async Task ItemEnhancement(Address avatarAddress, Guid itemId, Guid materialId, int slotIndex)
        {
            var playerPrivateKey = new PrivateKey();
            var blockChain = GetContextFx(playerPrivateKey, new RankingState());
            var query = $@"mutation {{
                action {{
                    itemEnhancement(avatarAddress: ""{avatarAddress}"", itemId: ""{itemId}"", materialId: ""{materialId}"", slotIndex: {slotIndex})
                }}
            }}";
            var result = await ExecuteQueryAsync(query);
            Assert.Null(result.Errors);

            var txIds = blockChain.GetStagedTransactionIds();
            Assert.Single(txIds);
            var tx = blockChain.GetTransaction(txIds.First());
            Assert.Single(tx.Actions);
            var action = (ItemEnhancement5) tx.Actions.First().InnerAction;
            Assert.Equal(avatarAddress, action.avatarAddress);
            Assert.Equal(itemId, action.itemId);
            Assert.Equal(materialId, action.materialId);
            Assert.Equal(slotIndex, action.slotIndex);
        }

        public static IEnumerable<object[]> ItemEnhancementMember => new List<object[]>
        {
            new object[]
            {
                new Address(),
                Guid.NewGuid(),
                Guid.NewGuid(),
                0,
            },
            new object[]
            {
                new Address(),
                Guid.NewGuid(),
                Guid.NewGuid(),
                3,
            },
        };

        [Theory]
        [MemberData(nameof(SellMember))]
        public async Task Sell(Address sellerAvatarAddress, Guid itemId, int price)
        {
            var playerPrivateKey = new PrivateKey();
            var blockChain = GetContextFx(playerPrivateKey, new RankingState());
            var query = $@"mutation {{
                action {{
                    sell(sellerAvatarAddress: ""{sellerAvatarAddress}"", itemId: ""{itemId}"", price: {price})
                }}
            }}";
            var result = await ExecuteQueryAsync(query);
            Assert.Null(result.Errors);

            var txIds = blockChain.GetStagedTransactionIds();
            Assert.Single(txIds);
            var tx = blockChain.GetTransaction(txIds.First());
            Assert.Single(tx.Actions);
            var action = (Sell3) tx.Actions.First().InnerAction;
            Assert.Equal(sellerAvatarAddress, action.sellerAvatarAddress);
            Assert.Equal(itemId, action.itemId);
            var currency = new GoldCurrencyState(
                (Dictionary)blockChain.GetState(GoldCurrencyState.Address)
            ).Currency;

            Assert.Equal(price * currency, action.price);
        }

        public static IEnumerable<object[]> SellMember => new List<object[]>
        {
            new object[]
            {
                new Address(),
                Guid.NewGuid(),
                0,
            },
            new object[]
            {
                new Address(),
                Guid.NewGuid(),
                100,
            },
        };

        [Theory]
        [MemberData(nameof(CombinationConsumableMember))]
        public async Task CombinationConsumable(Address avatarAddress, int recipeId, int slotIndex)
        {
            var playerPrivateKey = new PrivateKey();
            var blockChain = GetContextFx(playerPrivateKey, new RankingState());
            var query = $@"mutation {{
                action {{
                    combinationConsumable(avatarAddress: ""{avatarAddress}"", recipeId: {recipeId}, slotIndex: {slotIndex})
                }}
            }}";
            var result = await ExecuteQueryAsync(query);
            Assert.Null(result.Errors);

            var txIds = blockChain.GetStagedTransactionIds();
            Assert.Single(txIds);
            var tx = blockChain.GetTransaction(txIds.First());
            Assert.Single(tx.Actions);
            var action = (CombinationConsumable3) tx.Actions.First().InnerAction;
            Assert.Equal(avatarAddress, action.AvatarAddress);
            Assert.Equal(recipeId, action.recipeId);
            Assert.Equal(slotIndex, action.slotIndex);
        }

        public static IEnumerable<object[]> CombinationConsumableMember => new List<object[]>
        {
            new object[]
            {
                new Address(),
                1,
                0,
            },
            new object[]
            {
                new Address(),
                2,
                3,
            },
        };

        [Fact]
        public async Task Tx()
        {
            Block<PolymorphicAction<ActionBase>> genesis =
                MakeGenesisBlock(
                    default,
                    new Currency("NCG", 2, minters: null),
                    ImmutableHashSet<Address>.Empty
                );
            NineChroniclesNodeService service = ServiceBuilder.CreateNineChroniclesNodeService(genesis);

            StandaloneContextFx.NineChroniclesNodeService = service;
            StandaloneContextFx.BlockChain = service.Swarm.BlockChain;

            // Error: empty payload
            var query = $"mutation {{ stageTx(payload: \"\") }}";
            ExecutionResult result = await ExecuteQueryAsync(query);
            Assert.NotNull(result.Errors);
            Assert.Equal(
                new Dictionary<string, object>
                {
                    ["stageTx"] = false,
                },
                result.Data
            );

            Transaction<PolymorphicAction<ActionBase>> tx =
                Transaction<PolymorphicAction<ActionBase>>.Create(
                    0,
                    service.PrivateKey,
                    genesis.Hash,
                    new PolymorphicAction<ActionBase>[] { }
                );
            string hexEncoded = ByteUtil.Hex(tx.Serialize(true));
            query = $"mutation {{ stageTx(payload: \"{hexEncoded}\") }}";
            result = await ExecuteQueryAsync(query);
            Assert.Null(result.Errors);
            Assert.Equal(
                new Dictionary<string, object>
                {
                    ["stageTx"] = true,
                }, 
                result.Data
            );
            Block<PolymorphicAction<ActionBase>> mined =
                await StandaloneContextFx.BlockChain.MineBlock(service.PrivateKey.ToAddress());
            Assert.Contains(tx, mined.Transactions);
        }

        private Block<PolymorphicAction<ActionBase>> MakeGenesisBlock(
            Address adminAddress,
            Currency curreny,
            IImmutableSet<Address> activatedAccounts,
            RankingState rankingState = null
        ) => BlockChain<PolymorphicAction<ActionBase>>.MakeGenesisBlock(
            new PolymorphicAction<ActionBase>[]
            {
                new InitializeStates(
                    rankingState: rankingState ?? new RankingState(),
                    shopState: new ShopState(),
                    gameConfigState: new GameConfigState(_sheets[nameof(GameConfigSheet)]),
                    redeemCodeState: new RedeemCodeState(Bencodex.Types.Dictionary.Empty
                        .Add("address", RedeemCodeState.Address.Serialize())
                        .Add("map", Bencodex.Types.Dictionary.Empty)
                    ),
                    adminAddressState: new AdminState(adminAddress, 1500000),
                    activatedAccountsState: new ActivatedAccountsState(activatedAccounts),
                    goldCurrencyState: new GoldCurrencyState(curreny),
                    goldDistributions: new GoldDistribution[0],
                    tableSheets: _sheets,
                    pendingActivationStates: new PendingActivationState[]{ }
                ),
            }, blockAction: ServiceBuilder.BlockPolicy.BlockAction
        );

        private BlockChain<PolymorphicAction<ActionBase>> GetContextFx(PrivateKey playerPrivateKey, RankingState ranking)
        {
            var goldCurrency = new Currency("NCG", 2, minter: null);
            Block<PolymorphicAction<ActionBase>> genesis =
                MakeGenesisBlock(default, goldCurrency, ImmutableHashSet<Address>.Empty, ranking);
            var service = ServiceBuilder.CreateNineChroniclesNodeService(genesis, playerPrivateKey);
            StandaloneContextFx.NineChroniclesNodeService = service;
            StandaloneContextFx.BlockChain = service.Swarm.BlockChain;
            return StandaloneContextFx.BlockChain;
        }
    }
}
