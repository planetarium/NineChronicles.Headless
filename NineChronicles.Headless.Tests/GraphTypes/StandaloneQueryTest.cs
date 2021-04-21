using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Bencodex;
using Bencodex.Types;
using GraphQL;
using Libplanet;
using Libplanet.Action;
using Libplanet.Assets;
using Libplanet.Blockchain;
using Libplanet.Blocks;
using Libplanet.Crypto;
using Libplanet.KeyStore;
using Libplanet.Net;
using Libplanet.Headless.Hosting;
using Libplanet.Tx;
using Nekoyume.Action;
using Nekoyume.Model;
using Nekoyume.Model.State;
using Nekoyume.TableData;
using NineChronicles.Headless.Tests.Common;
using NineChronicles.Headless.Tests.Common.Actions;
using Xunit;
using Xunit.Abstractions;

namespace NineChronicles.Headless.Tests.GraphTypes
{
    public class StandaloneQueryTest : GraphQLTestBase
    {
        private readonly Dictionary<string, string> _sheets;

        public StandaloneQueryTest(ITestOutputHelper output) : base(output)
        {
            var fixturePath = Path.Combine("..", "..", "..", "..", "Lib9c", ".Lib9c.Tests", "Data", "TableCSV");
            _sheets = TableSheetsImporter.ImportSheets(fixturePath);
        }

        [Fact]
        public async Task GetState()
        {
            var codec = new Codec();
            var miner = new Address();

            const int repeat = 10;
            foreach (long index in Enumerable.Range(1, repeat))
            {
                await BlockChain.MineBlock(miner);

                var result = await ExecuteQueryAsync($"query {{ state(address: \"{miner.ToHex()}\") }}");

                var data = (Dictionary<string, object>) result.Data;
                var state = (Integer)codec.Decode(ByteUtil.ParseHex((string) data["state"]));

                // TestRewardGold에서 miner에게 1 gold 씩 주므로 block index와 같을 것입니다.
                Assert.Equal((Integer)index, state);
            }
        }

        [Theory]
        [InlineData(2)]
        [InlineData(16)]
        [InlineData(32)]
        public async Task ListPrivateKeys(int repeat)
        {
            var generatedProtectedPrivateKeys = new List<ProtectedPrivateKey>();
            foreach (var _ in Enumerable.Range(0, repeat))
            {
                var (protectedPrivateKey, _) = CreateProtectedPrivateKey();
                generatedProtectedPrivateKeys.Add(protectedPrivateKey);
                KeyStore.Add(protectedPrivateKey);
            }

            var result = await ExecuteQueryAsync("query { keyStore { protectedPrivateKeys { address } } }");

            var data = (Dictionary<string, object>) result.Data;
            var keyStoreResult = (Dictionary<string, object>) data["keyStore"];
            var protectedPrivateKeyAddresses =
                keyStoreResult["protectedPrivateKeys"].As<List<object>>()
                    .Cast<Dictionary<string, object>>()
                    .Select(x => x["address"].As<string>())
                    .ToImmutableList();

            foreach (var protectedPrivateKey in generatedProtectedPrivateKeys)
            {
                Assert.Contains(protectedPrivateKey.Address.ToString(), protectedPrivateKeyAddresses);
            }

            var (notStoredProtectedPrivateKey, _) = CreateProtectedPrivateKey();
            Assert.DoesNotContain(notStoredProtectedPrivateKey.Address.ToString(), protectedPrivateKeyAddresses);
        }

        [Fact]
        public async Task DecryptedPrivateKey()
        {
            var (protectedPrivateKey, passphrase) = CreateProtectedPrivateKey();
            var privateKey = protectedPrivateKey.Unprotect(passphrase);
            KeyStore.Add(protectedPrivateKey);

            var result = await ExecuteQueryAsync($"query {{ keyStore {{ decryptedPrivateKey(address: \"{privateKey.ToAddress()}\", passphrase: \"{passphrase}\") }} }}");

            var data = (Dictionary<string, object>) result.Data;
            var keyStoreResult = (Dictionary<string, object>) data["keyStore"];
            var decryptedPrivateKeyResult = (string) keyStoreResult["decryptedPrivateKey"];

            Assert.Equal(ByteUtil.Hex(privateKey.ByteArray), decryptedPrivateKeyResult);
        }

        [Fact]
        public async Task NodeStatus()
        {
            var cts = new CancellationTokenSource();

            var apvPrivateKey = new PrivateKey();
            var apv = AppProtocolVersion.Sign(apvPrivateKey, 0);
            var genesisBlock = BlockChain<EmptyAction>.MakeGenesisBlock();

            // 에러로 인하여 NineChroniclesNodeService 를 사용할 수 없습니다. https://git.io/JfS0M
            // 따라서 LibplanetNodeService로 비슷한 환경을 맞춥니다.
            // 1. 노드를 생성합니다.
            var seedNode = CreateLibplanetNodeService<EmptyAction>(genesisBlock, apv, apvPrivateKey.PublicKey);
            await StartAsync(seedNode.Swarm, cts.Token);
            var service = CreateLibplanetNodeService<EmptyAction>(genesisBlock, apv, apvPrivateKey.PublicKey, peers: new [] { seedNode.Swarm.AsPeer });

            // 2. NineChroniclesNodeService.ConfigureStandaloneContext(standaloneContext)를 호출합니다.
            // BlockChain 객체 공유 및 PreloadEnded, BootstrapEnded 이벤트 훅의 처리를 합니다.
            // BlockChain 객체 공유는 액션 타입이 달라 생략합니다.
            _ = service.BootstrapEnded.WaitAsync()
                .ContinueWith(task => StandaloneContextFx.BootstrapEnded = true);
            _ = service.PreloadEnded.WaitAsync()
                .ContinueWith(task => StandaloneContextFx.PreloadEnded = true);

            var bootstrapEndedTask = service.BootstrapEnded.WaitAsync();
            var preloadEndedTask = service.PreloadEnded.WaitAsync();

            async Task<Dictionary<string, bool>> QueryNodeStatus()
            {
                var result = await ExecuteQueryAsync("query { nodeStatus { bootstrapEnded preloadEnded } }");
                var data = (Dictionary<string, object>) result.Data;
                var nodeStatusData = (Dictionary<string, object>) data["nodeStatus"];
                return nodeStatusData.ToDictionary(pair => pair.Key, pair => (bool)pair.Value);
            }

            var nodeStatus = await QueryNodeStatus();
            Assert.False(nodeStatus["bootstrapEnded"]);
            Assert.False(nodeStatus["preloadEnded"]);

            _ = service.StartAsync(cts.Token);

            await bootstrapEndedTask;
            await preloadEndedTask;

            // ContinueWith으로 넘긴 태스크가 실행되기를 기다립니다.
            await Task.Delay(1000);

            nodeStatus = await QueryNodeStatus();
            Assert.True(nodeStatus["bootstrapEnded"]);
            Assert.True(nodeStatus["preloadEnded"]);

            await seedNode.StopAsync(cts.Token);
        }

        [Fact]
        public async Task NodeStatusStagedTxIds()
        {
            var apvPrivateKey = new PrivateKey();
            var apv = AppProtocolVersion.Sign(apvPrivateKey, 0);
            var genesis = BlockChain<PolymorphicAction<ActionBase>>.MakeGenesisBlock();

            var service = ServiceBuilder.CreateNineChroniclesNodeService(genesis);
            StandaloneServices.ConfigureStandaloneContext(service, StandaloneContextFx);

            var result = await ExecuteQueryAsync("query { nodeStatus { stagedTxIds } }");
            var expectedResult = new Dictionary<string, object>()
            {
                ["nodeStatus"] = new Dictionary<string, object>()
                {
                    ["stagedTxIds"] = new List<object>()
                },
            };
            Assert.Null(result.Errors);
            Assert.Equal(expectedResult, result.Data);

            var tx = StandaloneContextFx.BlockChain!.MakeTransaction(
                new PrivateKey(), 
                new PolymorphicAction<ActionBase>[] { }
            );

            result = await ExecuteQueryAsync("query { nodeStatus { stagedTxIds } }");
            expectedResult = new Dictionary<string, object>()
            {
                ["nodeStatus"] = new Dictionary<string, object>()
                {
                    ["stagedTxIds"] = new List<object>
                    {
                        tx.Id.ToString(),
                    }
                },
            };
            Assert.Null(result.Errors);
            Assert.Equal(expectedResult, result.Data);

            var apvTx = StandaloneContextFx.BlockChain.MakeTransaction(
                apvPrivateKey,
                new PolymorphicAction<ActionBase>[] { }
            );

            var apvAddress = apvPrivateKey.ToAddress();
            var query = $@"query {{
                nodeStatus {{
                    stagedTxIds(address: ""{apvAddress}"")
                }}
            }}";
            result = await ExecuteQueryAsync(query);
            expectedResult = new Dictionary<string, object>()
            {
                ["nodeStatus"] = new Dictionary<string, object>()
                {
                    ["stagedTxIds"] = new List<object>
                    {
                        apvTx.Id.ToString(),
                    }
                },
            };
            Assert.Null(result.Errors);
            Assert.Equal(expectedResult, result.Data);
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public async Task ValidateMetadata(bool valid)
        {
            var minerAddress = new PrivateKey().ToAddress();
            var lowMetadata = "{\\\"Index\\\":1}";
            var highMetadata = "{\\\"Index\\\":13340}";

            for (int i = 0; i < 10; i++)
            {
                await BlockChain.MineBlock(minerAddress);
            }
            var query = $@"query {{
                validation {{
                    metadata(raw: ""{(valid ? highMetadata : lowMetadata)}"")
                }}
            }}";

            var result = await ExecuteQueryAsync(query);

            var validationResult =
                result.Data
                    .As<Dictionary<string, object>>()["validation"]
                    .As<Dictionary<string, object>>()["metadata"];
            Assert.IsType<bool>(validationResult);
            Assert.Equal(valid, validationResult);
        }

        // TODO: partial class로 세부 쿼리 별 테스트 분리하기.
        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public async Task ValidatePrivateKey(bool valid)
        {
            var privateKey = new PrivateKey();
            var privateKeyHex = valid
                ? ByteUtil.Hex(privateKey.ByteArray)
                : "0000000000000000000000000000000000000000";
            var query = $@"query {{
                validation {{
                    privateKey(hex: ""{privateKeyHex}"")
                }}
            }}";

            var result = await ExecuteQueryAsync(query);
            var validationResult =
                result.Data
                    .As<Dictionary<string, object>>()["validation"]
                    .As<Dictionary<string, object>>()["privateKey"];
            Assert.IsType<bool>(validationResult);
            Assert.Equal(valid, validationResult);
        }

        // TODO: partial class로 세부 쿼리 별 테스트 분리하기.
        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public async Task ValidatePublicKey(bool valid)
        {
            var privateKey = new PrivateKey();
            var publicKey = privateKey.PublicKey;

            string CreateInvalidPublicKeyHexString(bool compress)
            {
                int length = compress ? 33 : 66;
                do
                {
                    byte[] publicKeyBytes = CreateRandomBytes(length);

                    try
                    {
                        var _ = new PublicKey(publicKeyBytes);
                    }
                    catch (FormatException)
                    {
                        return ByteUtil.Hex(publicKeyBytes);
                    }
                    catch (ArgumentException)
                    {
                        return ByteUtil.Hex(publicKeyBytes);
                    }
                } while (true);
            }

            var publicKeyHex = valid ? ByteUtil.Hex(publicKey.Format(false)) : CreateInvalidPublicKeyHexString(false);
            var query = $@"query {{
                validation {{
                    publicKey(hex: ""{publicKeyHex}"")
                }}
            }}";

            var result = await ExecuteQueryAsync(query);

            var validationResult =
                result.Data
                    .As<Dictionary<string, object>>()["validation"]
                    .As<Dictionary<string, object>>()["publicKey"];
            Assert.IsType<bool>(validationResult);
            Assert.Equal(valid, validationResult);
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public async Task ConvertPrivateKey(bool compress)
        {
            var privateKey = new PrivateKey();
            var privateKeyHex = ByteUtil.Hex(privateKey.ByteArray);
            var query = $@"
            query {{
                keyStore {{
                    privateKey(hex: ""{privateKeyHex}"") {{
                        hex
                        publicKey {{
                            hex(compress: {compress.ToString().ToLowerInvariant()})
                            address
                        }}
                    }}
                }}
            }}
            ";

            var result = await ExecuteQueryAsync(query);
            var privateKeyResult = result.Data.As<Dictionary<string, object>>()["keyStore"]
                .As<Dictionary<string, object>>()["privateKey"]
                .As<Dictionary<string, object>>();
            Assert.Equal(privateKeyHex, privateKeyResult["hex"]);
            var publicKeyResult = privateKeyResult["publicKey"].As<Dictionary<string, object>>();
            Assert.Equal(ByteUtil.Hex(privateKey.PublicKey.Format(compress)), publicKeyResult["hex"]);
            Assert.Equal(privateKey.ToAddress().ToString(), publicKeyResult["address"]);
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public async Task ActivationStatus(bool existsActivatedAccounts)
        {
            var adminPrivateKey = new PrivateKey();
            var adminAddress = adminPrivateKey.ToAddress();
            var activatedAccounts = ImmutableHashSet<Address>.Empty;

            if (existsActivatedAccounts)
            {
                activatedAccounts = new[] { adminAddress }.ToImmutableHashSet();
            }

            Block<PolymorphicAction<ActionBase>> genesis =
                BlockChain<PolymorphicAction<ActionBase>>.MakeGenesisBlock(
                    new PolymorphicAction<ActionBase>[]
                    {
                        new InitializeStates(
                            rankingState: new RankingState(),
                            shopState: new ShopState(),
                            gameConfigState: new GameConfigState(),
                            redeemCodeState: new RedeemCodeState(Bencodex.Types.Dictionary.Empty
                                .Add("address", RedeemCodeState.Address.Serialize())
                                .Add("map", Bencodex.Types.Dictionary.Empty)
                            ),
                            adminAddressState: new AdminState(adminAddress, 1500000),
                            activatedAccountsState: new ActivatedAccountsState(activatedAccounts),
                            goldCurrencyState: new GoldCurrencyState(new Currency("NCG", 2, minter: null)),
                            goldDistributions: new GoldDistribution[0],
                            tableSheets: _sheets,
                            pendingActivationStates: new PendingActivationState[]{ }
                        ),
                    }
                );

            var apvPrivateKey = new PrivateKey();
            var apv = AppProtocolVersion.Sign(apvPrivateKey, 0);
            var userPrivateKey = new PrivateKey();
            var properties = new LibplanetNodeServiceProperties<PolymorphicAction<ActionBase>>
            {
                Host = System.Net.IPAddress.Loopback.ToString(),
                AppProtocolVersion = apv,
                GenesisBlock = genesis,
                StorePath = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName()),
                StoreStatesCacheSize = 2,
                SwarmPrivateKey = new PrivateKey(),
                Port = null,
                MinimumDifficulty = 4096,
                NoMiner = true,
                Render = false,
                Peers = ImmutableHashSet<Peer>.Empty,
                TrustedAppProtocolVersionSigners = null,
                StaticPeers = ImmutableHashSet<Peer>.Empty
            };

            var service = new NineChroniclesNodeService(userPrivateKey, properties, null);
            StandaloneContextFx.NineChroniclesNodeService = service;
            StandaloneContextFx.BlockChain = service.Swarm?.BlockChain;

            var blockChain = StandaloneContextFx.BlockChain!;

            var queryResult = await ExecuteQueryAsync( "query { activationStatus { activated } }");
            var result = (bool)queryResult.Data
                .As<Dictionary<string, object>>()["activationStatus"]
                .As<Dictionary<string, object>>()["activated"];

            // ActivatedAccounts가 비어있을때는 true이고 하나라도 있을경우 false
            Assert.Equal(!existsActivatedAccounts, result);

            var nonce = new byte[] {0x00, 0x01, 0x02, 0x03};
            var privateKey = new PrivateKey();
            (ActivationKey activationKey, PendingActivationState pendingActivation) =
                ActivationKey.Create(privateKey, nonce);
            PolymorphicAction<ActionBase> action = new CreatePendingActivation(pendingActivation);
            blockChain.MakeTransaction(adminPrivateKey, new[] {action});
            await blockChain.MineBlock(adminAddress);

            action = activationKey.CreateActivateAccount(nonce);
            blockChain.MakeTransaction(userPrivateKey, new[] { action });
            await blockChain.MineBlock(adminAddress);

            queryResult = await ExecuteQueryAsync( "query { activationStatus { activated } }");
            result = (bool)queryResult.Data
                .As<Dictionary<string, object>>()["activationStatus"]
                .As<Dictionary<string, object>>()["activated"];

            // ActivatedAccounts에 Address가 추가 되었기 때문에 true
            Assert.True(result);
        }

        [Fact]
        public async Task GoldBalance()
        {
            var userPrivateKey = new PrivateKey();
            var userAddress = userPrivateKey.ToAddress();
            var service = MakeMineChroniclesNodeService(userPrivateKey);
            StandaloneContextFx.NineChroniclesNodeService = service;
            StandaloneContextFx.BlockChain = service.Swarm?.BlockChain;

            var blockChain = StandaloneContextFx.BlockChain;
            var query = $"query {{ goldBalance(address: \"{userAddress}\") }}";
            var queryResult = await ExecuteQueryAsync(query);
            Assert.Equal(
                new Dictionary<string, object>
                {
                    ["goldBalance"] = "0"
                },
                queryResult.Data
            );
           
            await blockChain!.MineBlock(userAddress);

            queryResult = await ExecuteQueryAsync(query);
            Assert.Equal(
                new Dictionary<string, object>
                {
                    ["goldBalance"] = "10"
                },
                queryResult.Data
            );
        }

        [Fact]
        public async Task NextTxNonce()
        {
            var userPrivateKey = new PrivateKey();
            var userAddress = userPrivateKey.ToAddress();
            var service = MakeMineChroniclesNodeService(userPrivateKey);
            StandaloneContextFx.NineChroniclesNodeService = service;
            StandaloneContextFx.BlockChain = service.Swarm?.BlockChain;

            var blockChain = StandaloneContextFx.BlockChain!;
            var query = $"query {{ nextTxNonce(address: \"{userAddress}\") }}";
            var queryResult = await ExecuteQueryAsync(query);
            Assert.Equal(
                new Dictionary<string, object>
                {
                    ["nextTxNonce"] = 0L
                },
                queryResult.Data
            );

            blockChain.MakeTransaction(userPrivateKey, new PolymorphicAction<ActionBase>[] { });
            queryResult = await ExecuteQueryAsync(query);
            Assert.Equal(
                new Dictionary<string, object>
                {
                    ["nextTxNonce"] = 1L
                },
                queryResult.Data
            );
        }

        [Fact]
        public async Task GetTx()
        {
            var userPrivateKey = new PrivateKey();
            var userAddress = userPrivateKey.ToAddress();
            var service = MakeMineChroniclesNodeService(userPrivateKey);
            StandaloneContextFx.NineChroniclesNodeService = service;
            StandaloneContextFx.BlockChain = service.Swarm!.BlockChain;

            var blockChain = StandaloneContextFx.BlockChain;
            var queryFormat = @"query {{
                getTx(txId: ""{0}"") {{
                    id
                    nonce
                    signer
                    signature
                    timestamp
                    updatedAddresses
                    actions {{
                        inspection
                    }}
                }}
            }}";
            var queryResult = await ExecuteQueryAsync(string.Format(queryFormat, new TxId()));
            Assert.Equal(
                new Dictionary<string, object?>
                {
                    ["getTx"] = null
                },
                queryResult.Data
            );

            var action = new CreateAvatar2
            {
                index = 0,
                hair = 1,
                lens = 2,
                ear = 3,
                tail = 4,
                name = "action",
            };
            var transaction = blockChain.MakeTransaction(userPrivateKey, new PolymorphicAction<ActionBase>[] { action });
            blockChain.StageTransaction(transaction);
            await blockChain.MineBlock(new Address());
            queryResult = await ExecuteQueryAsync(string.Format(queryFormat, transaction.Id));
            var tx = queryResult.Data
                .As<Dictionary<string, object>>()["getTx"]
                .As<Dictionary<string, object>>();

            Assert.Equal(tx["id"], transaction.Id.ToString());
            Assert.Equal(tx["nonce"], transaction.Nonce);
            Assert.Equal(tx["signer"], transaction.Signer.ToString());
            Assert.Equal(tx["signature"], ByteUtil.Hex(transaction.Signature));
            Assert.Equal(tx["timestamp"], transaction.Timestamp.ToString());
            Assert.Equal(tx["updatedAddresses"], transaction.UpdatedAddresses.Select(a => a.ToString()));

            var plainValue = tx["actions"]
                .As<List<object>>()
                .First()
                .As<Dictionary<string, object>>()["inspection"];
            Assert.Equal(transaction.Actions.First().PlainValue.Inspection, plainValue);
        }

        private NineChroniclesNodeService MakeMineChroniclesNodeService(PrivateKey privateKey)
        {
            var goldCurrency = new Currency("NCG", 2, minter: null);
            int minimumDifficulty = 4096;
            var blockAction = NineChroniclesNodeService.GetBlockPolicy(minimumDifficulty,
                new LibplanetNodeServiceProperties<PolymorphicAction<ActionBase>>().MaximumTransactions).BlockAction;
            Block<PolymorphicAction<ActionBase>> genesis =
                BlockChain<PolymorphicAction<ActionBase>>.MakeGenesisBlock(
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
                            adminAddressState: new AdminState(default, 0),
                            activatedAccountsState: new ActivatedAccountsState(),
                            goldCurrencyState: new GoldCurrencyState(goldCurrency),
                            goldDistributions: new GoldDistribution[]{ },
                            tableSheets: _sheets,
                            pendingActivationStates: new PendingActivationState[]{ }
                        ),
                    }, blockAction: blockAction
                );

            var properties = new LibplanetNodeServiceProperties<PolymorphicAction<ActionBase>>
            {
                Host = System.Net.IPAddress.Loopback.ToString(),
                AppProtocolVersion = default,
                GenesisBlock = genesis,
                StorePath = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName()),
                StoreStatesCacheSize = 2,
                SwarmPrivateKey = new PrivateKey(),
                Port = null,
                MinimumDifficulty = minimumDifficulty,
                NoMiner = true,
                Render = false,
                Peers = ImmutableHashSet<Peer>.Empty,
                TrustedAppProtocolVersionSigners = null,
                StaticPeers = ImmutableHashSet<Peer>.Empty,
            };

            return new NineChroniclesNodeService(privateKey, properties, null);
        }

        private (ProtectedPrivateKey, string) CreateProtectedPrivateKey()
        {
            string CreateRandomBase64String()
            {
                // TODO: use `CreateRandomBytes()`
                var random = new Random();
                Span<byte> buffer = stackalloc byte[16];
                random.NextBytes(buffer);
                return Convert.ToBase64String(buffer);
            }

            // 랜덤 문자열을 생성하여 passphrase로 사용합니다.
            var passphrase = CreateRandomBase64String();
            return (ProtectedPrivateKey.Protect(new PrivateKey(), passphrase), passphrase);
        }

        private string CreateRandomHexString(int length)
        {
            return ByteUtil.Hex(CreateRandomBytes(length));
        }

        private byte[] CreateRandomBytes(int length)
        {
            var random = new Random();
            byte[] buffer = new byte[length];
            random.NextBytes(buffer);
            return buffer;
        }
    }
}
