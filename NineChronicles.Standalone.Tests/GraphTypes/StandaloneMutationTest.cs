using GraphQL;
using Lib9c.Tests;
using Libplanet;
using Libplanet.Action;
using Libplanet.Assets;
using Libplanet.Blockchain;
using Libplanet.Blocks;
using Libplanet.Crypto;
using Libplanet.KeyStore;
using Libplanet.Net;
using Libplanet.Standalone.Hosting;
using Libplanet.Tx;
using Nekoyume;
using Nekoyume.Action;
using Nekoyume.Model;
using Nekoyume.Model.State;
using Nekoyume.TableData;
using NineChronicles.Standalone.Tests.Common;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;

namespace NineChronicles.Standalone.Tests.GraphTypes
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

            var stagedTxIds = store.IterateStagedTransactionIds().ToImmutableList();
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

        [Fact]
        public async Task CreateAvatar()
        {
            var playerPrivateKey = new PrivateKey();
            Address playerAddress = playerPrivateKey.ToAddress();
            var ranking = new RankingState();
            for (var i = 0; i < RankingState.RankingMapCapacity; i++)
            {
                ranking.RankingMap[RankingState.Derive(i)] = new HashSet<Address>().ToImmutableHashSet();
            }
            var blockChain = GetContextFx(playerPrivateKey, ranking);

            var query = $"mutation {{ action {{ createAvatar }} }}";
            ExecutionResult result = await ExecuteQueryAsync(query);

            var isCreated = (bool)result.Data
                .As<Dictionary<string, object>>()["action"]
                .As<Dictionary<string, object>>()["createAvatar"];

            Assert.True(isCreated);

            await blockChain.MineBlock(playerAddress);
            var playerState = (Bencodex.Types.Dictionary)blockChain.GetState(playerAddress);
            var agentState = new AgentState(playerState);

            Assert.Equal(playerAddress, agentState.address);
            Assert.True(agentState.avatarAddresses.ContainsKey(0));

            var avatar = new AvatarState((Bencodex.Types.Dictionary)blockChain.GetState(agentState.avatarAddresses[0]));

            Assert.Equal("createbymutation", avatar.name);
        }

        [Fact]
        public async Task HackAndSlash()
        {
            var playerPrivateKey = new PrivateKey();
            Address playerAddress = playerPrivateKey.ToAddress();
            var ranking = new RankingState();
            for (var i = 0; i < RankingState.RankingMapCapacity; i++)
            {
                ranking.RankingMap[RankingState.Derive(i)] = new HashSet<Address>().ToImmutableHashSet();
            }
            var blockChain = GetContextFx(playerPrivateKey, ranking);

            var createAvatarQuery = $"mutation {{ action {{ createAvatar }} }}";
            ExecutionResult createAvatarResult = await ExecuteQueryAsync(createAvatarQuery);
            await blockChain.MineBlock(playerAddress);

            var playerState = (Bencodex.Types.Dictionary)blockChain.GetState(playerAddress);
            var agentState = new AgentState(playerState);
            var weeklyArenaAddress = (new WeeklyArenaState(1)).address;
            var rankingMapAddress = RankingState.Derive(0);

            Assert.Equal(playerAddress, agentState.address);

            var hackAndSlashQuery = $"mutation {{ action {{ hackAndSlash(weeklyArenaAddress: \"{weeklyArenaAddress.ToHex()}\", rankingArenaAddress: \"{rankingMapAddress.ToHex()}\") }} }}";
            ExecutionResult result = await ExecuteQueryAsync(hackAndSlashQuery);

            var isPlayed = (bool)result.Data
                .As<Dictionary<string, object>>()["action"]
                .As<Dictionary<string, object>>()["hackAndSlash"];

            await blockChain.MineBlock(playerAddress);
            Assert.True(isPlayed);

            var avatar = new AvatarState((Bencodex.Types.Dictionary)blockChain.GetState(agentState.avatarAddresses[0]));
            Assert.True(avatar.exp > 0);
        }

        [Fact]
        public async Task DailyReward()
        {
            _sheets[nameof(GameConfigSheet)] = string.Join(
                Environment.NewLine,
                "key, value",
                "hourglass_per_block, 3",
                "action_point_max, 120",
                "daily_reward_interval, 4",
                "daily_arena_interval, 500",
                "weekly_arena_interval, 56000");

            var playerPrivateKey = new PrivateKey();
            Address playerAddress = playerPrivateKey.ToAddress();
            var ranking = new RankingState();
            for (var i = 0; i < RankingState.RankingMapCapacity; i++)
            {
                ranking.RankingMap[RankingState.Derive(i)] = new HashSet<Address>().ToImmutableHashSet();
            }
            var blockChain = GetContextFx(playerPrivateKey, ranking);

            var createAvatarQuery = $"mutation {{ action {{ createAvatar }} }}";
            await ExecuteQueryAsync(createAvatarQuery);
            await blockChain.MineBlock(playerAddress);

            var playerState = (Bencodex.Types.Dictionary)blockChain.GetState(playerAddress);
            var agentState = new AgentState(playerState);
            var weeklyArenaAddress = (new WeeklyArenaState(1)).address;
            var rankingMapAddress = RankingState.Derive(0);
            var gameConfigState = new GameConfigState((Bencodex.Types.Dictionary)blockChain.GetState(Addresses.GameConfig));
            var avatarState = new AvatarState((Bencodex.Types.Dictionary)blockChain.GetState(agentState.avatarAddresses[0]));

            Assert.Equal(playerAddress, agentState.address);

            var hackAndSlashQuery = $"mutation {{ action {{ hackAndSlash(weeklyArenaAddress: \"{weeklyArenaAddress.ToHex()}\", rankingArenaAddress: \"{rankingMapAddress.ToHex()}\") }} }}";
            await ExecuteQueryAsync(hackAndSlashQuery);
            await blockChain.MineBlock(playerAddress);

            avatarState = new AvatarState((Bencodex.Types.Dictionary)blockChain.GetState(agentState.avatarAddresses[0]));
            Assert.True(avatarState.actionPoint < gameConfigState.ActionPointMax);

            var dailyRewardQuery = $"mutation {{ action {{ dailyReward }} }}";
            ExecutionResult result = await ExecuteQueryAsync(dailyRewardQuery);

            var isPlayed = (bool)result.Data
                .As<Dictionary<string, object>>()["action"]
                .As<Dictionary<string, object>>()["dailyReward"];

            Assert.True(isPlayed);
            await blockChain.MineBlock(playerAddress);

            avatarState = new AvatarState((Bencodex.Types.Dictionary)blockChain.GetState(agentState.avatarAddresses[0]));
            // BlockIndex : 3, Interval : 4, so actionPoint won't be charged.
            Assert.NotEqual(avatarState.actionPoint, gameConfigState.ActionPointMax);

            result = await ExecuteQueryAsync(dailyRewardQuery);
            isPlayed = (bool)result.Data
                .As<Dictionary<string, object>>()["action"]
                .As<Dictionary<string, object>>()["dailyReward"];

            Assert.True(isPlayed);
            await blockChain.MineBlock(playerAddress);

            avatarState = new AvatarState((Bencodex.Types.Dictionary)blockChain.GetState(agentState.avatarAddresses[0]));
            // BlockIndex : 4, Interval : 4, so actionPoint will be charged.
            Assert.Equal(avatarState.actionPoint, gameConfigState.ActionPointMax);
        }

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
            IImmutableSet<Address> activatedAccounts
        ) => BlockChain<PolymorphicAction<ActionBase>>.MakeGenesisBlock(
                new PolymorphicAction<ActionBase>[]
                {
                    new InitializeStates(
                        rankingState: new RankingState(),
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
                        tableSheets: new Dictionary<string, string>(),
                        pendingActivationStates: new PendingActivationState[]{ }
                    ),
                }
            );

        private BlockChain<PolymorphicAction<ActionBase>> GetContextFx(PrivateKey playerPrivateKey, RankingState ranking)
        {
            var goldCurrency = new Currency("NCG", 2, minter: null);
            Block<PolymorphicAction<ActionBase>> genesis =
                BlockChain<PolymorphicAction<ActionBase>>.MakeGenesisBlock(
                    new PolymorphicAction<ActionBase>[]
                    {
                        new InitializeStates(
                            rankingState: ranking,
                            shopState: new ShopState(),
                            gameConfigState: new GameConfigState(_sheets[nameof(GameConfigSheet)]),
                            redeemCodeState: new RedeemCodeState(Bencodex.Types.Dictionary.Empty
                                .Add("address", RedeemCodeState.Address.Serialize())
                                .Add("map", Bencodex.Types.Dictionary.Empty)
                            ),
                            adminAddressState: new AdminState(default, 0),
                            activatedAccountsState: new ActivatedAccountsState(),
                            goldCurrencyState: new GoldCurrencyState(goldCurrency),
                            goldDistributions: new GoldDistribution[0],
                            tableSheets: _sheets,
                            pendingActivationStates: new PendingActivationState[]{ }
                        ),
                    }
                );
            var properties = new LibplanetNodeServiceProperties<PolymorphicAction<ActionBase>>
            {
                Host = System.Net.IPAddress.Loopback.ToString(),
                AppProtocolVersion = default,
                GenesisBlock = genesis,
                StorePath = null,
                StoreStatesCacheSize = 2,
                PrivateKey = playerPrivateKey,
                Port = null,
                MinimumDifficulty = 4096,
                NoMiner = true,
                Render = false,
                Peers = ImmutableHashSet<Peer>.Empty,
                TrustedAppProtocolVersionSigners = null,
            };
            var service = new NineChroniclesNodeService(properties, null)
            {
                PrivateKey = playerPrivateKey
            };
            StandaloneContextFx.NineChroniclesNodeService = service;
            StandaloneContextFx.BlockChain = service.Swarm.BlockChain;

            return StandaloneContextFx.BlockChain;
        }
    }
}
