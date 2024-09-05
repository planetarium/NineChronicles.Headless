using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using Bencodex;
using Bencodex.Types;
using GraphQL.Execution;
using GraphQL.Types;
using Lib9c;
using Libplanet.Action;
using Libplanet.Action.State;
using Libplanet.Action.Sys;
using Libplanet.Blockchain;
using Libplanet.Common;
using Libplanet.Crypto;
using Libplanet.Headless.Hosting;
using Libplanet.KeyStore;
using Libplanet.Mocks;
using Libplanet.Net;
using Libplanet.Store.Trie;
using Libplanet.Types.Assets;
using Libplanet.Types.Blocks;
using Libplanet.Types.Consensus;
using Libplanet.Types.Tx;
using Moq;
using Nekoyume;
using Nekoyume.Action;
using Nekoyume.Blockchain.Policy;
using Nekoyume.Helper;
using Nekoyume.Model;
using Nekoyume.Model.Item;
using Nekoyume.Model.Quest;
using Nekoyume.Model.State;
using Nekoyume.Module;
using Nekoyume.TableData;
using NineChronicles.Headless.Executable;
using NineChronicles.Headless.GraphTypes;
using NineChronicles.Headless.Repositories.WorldState;
using NineChronicles.Headless.Tests.Action;
using NineChronicles.Headless.Tests.Common;
using Xunit;
using Xunit.Abstractions;
using static NineChronicles.Headless.NCActionUtils;

namespace NineChronicles.Headless.Tests.GraphTypes
{
    public partial class StandaloneQueryTest : GraphQLTestBase
    {
        private readonly Dictionary<string, string> _sheets;
        private readonly IObjectGraphType _graph;

        public StandaloneQueryTest(ITestOutputHelper output) : base(output)
        {
            _sheets = TableSheetsImporter.ImportSheets();
        }

        [Fact]
        public async Task GetState()
        {
            var adminAddress = new PrivateKey().Address;
            Address adminStateAddress = AdminState.Address;
            var worldState = new World(MockUtil.MockModernWorldState)
                .SetLegacyState(adminStateAddress, new AdminState(adminAddress, 10000).Serialize());
            var stateRootHash = worldState.Trie.Hash;

            var tip = new Domain.Model.BlockChain.Block(
                BlockHash.FromString("613dfa26e104465790625ae7bc03fc27a64947c02a9377565ec190405ef7154b"),
                BlockHash.FromString("36456be15af9a5b9b13a02c7ce1e849ae9cba8781ec309010499cdb93e29237d"),
                default(Address),
                0,
                Timestamp: DateTimeOffset.UtcNow,
                StateRootHash: stateRootHash,
                Transactions: ImmutableArray<Transaction>.Empty
            );

            BlockChainRepository.Setup(repository => repository.GetTip())
                .Returns(tip);
            WorldStateRepository.Setup(repository => repository.GetWorldState(stateRootHash))
                .Returns(worldState);

            var result = await ExecuteQueryAsync($"query {{ state(accountAddress: \"{ReservedAddresses.LegacyAccount}\", address: \"{adminStateAddress}\") }}");
            var data = (Dictionary<string, object>)((ExecutionNode)result.Data!).ToValue()!;
            IValue rawVal = new Codec().Decode(ByteUtil.ParseHex((string)data!["state"]));
            AdminState adminState = new AdminState((Dictionary)rawVal);

            Assert.Equal(adminAddress, adminState.AdminAddress);
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

            var data = (Dictionary<string, object>)((ExecutionNode)result.Data!).ToValue()!;
            var keyStoreResult = (Dictionary<string, object>)data["keyStore"];
            var protectedPrivateKeyAddresses = ((IList)keyStoreResult["protectedPrivateKeys"])
                .Cast<Dictionary<string, object>>()
                .Select(x => x["address"] as string)
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

            var result = await ExecuteQueryAsync($"query {{ keyStore {{ decryptedPrivateKey(address: \"{privateKey.Address}\", passphrase: \"{passphrase}\") }} }}");

            var data = (Dictionary<string, object>)((ExecutionNode)result.Data!).ToValue()!;
            var keyStoreResult = (Dictionary<string, object>)data["keyStore"];
            var decryptedPrivateKeyResult = (string)keyStoreResult["decryptedPrivateKey"];

            Assert.Equal(ByteUtil.Hex(privateKey.ByteArray), decryptedPrivateKeyResult);
        }

        [Fact]
        public async Task NodeStatus()
        {
            var cts = new CancellationTokenSource();

            var apvPrivateKey = new PrivateKey();
            var apv = AppProtocolVersion.Sign(apvPrivateKey, 0);
            var genesisBlock = BlockChain.ProposeGenesisBlock();

            // 에러로 인하여 NineChroniclesNodeService 를 사용할 수 없습니다. https://git.io/JfS0M
            // 따라서 LibplanetNodeService로 비슷한 환경을 맞춥니다.
            // 1. 노드를 생성합니다.
            var seedNode = CreateLibplanetNodeService(genesisBlock, apv, apvPrivateKey.PublicKey);
            await StartAsync(seedNode.Swarm, cts.Token);
            var service = CreateLibplanetNodeService(genesisBlock, apv, apvPrivateKey.PublicKey, peers: new[] { seedNode.Swarm.AsPeer });

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
                var data = (Dictionary<string, object>)((ExecutionNode)result.Data!).ToValue()!;
                var nodeStatusData = (Dictionary<string, object>)data["nodeStatus"];
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
            var privateKey = new PrivateKey();

            var result = await ExecuteQueryAsync("query { nodeStatus { stagedTxIds } }");
            var data = (Dictionary<string, object>)((ExecutionNode)result.Data!).ToValue()!;
            var expectedResult = new Dictionary<string, object>()
            {
                ["nodeStatus"] = new Dictionary<string, object>()
                {
                    ["stagedTxIds"] = new List<object>()
                },
            };
            Assert.Null(result.Errors);
            Assert.Equal(expectedResult, data);

            var anonymousTx = StandaloneContextFx.BlockChain!.MakeTransaction(
                new PrivateKey(),
                new ActionBase[] { }
            );

            result = await ExecuteQueryAsync("query { nodeStatus { stagedTxIds } }");
            data = (Dictionary<string, object>)((ExecutionNode)result.Data!).ToValue()!;
            expectedResult = new Dictionary<string, object>()
            {
                ["nodeStatus"] = new Dictionary<string, object>()
                {
                    ["stagedTxIds"] = new List<object>
                    {
                        anonymousTx.Id.ToString(),
                    }
                },
            };
            Assert.Null(result.Errors);
            Assert.Equal(expectedResult, data);

            var signerTx = StandaloneContextFx.BlockChain.MakeTransaction(
                privateKey,
                new ActionBase[] { }
            );

            var address = privateKey.Address;
            var query = $@"query {{
                nodeStatus {{
                    stagedTxIds(address: ""{address}"")
                }}
            }}";
            result = await ExecuteQueryAsync(query);
            data = (Dictionary<string, object>)((ExecutionNode)result.Data!).ToValue()!;
            expectedResult = new Dictionary<string, object>()
            {
                ["nodeStatus"] = new Dictionary<string, object>()
                {
                    ["stagedTxIds"] = new List<object>
                    {
                        signerTx.Id.ToString(),
                    }
                },
            };
            Assert.Null(result.Errors);
            Assert.Equal(expectedResult, data);
        }

        [Fact]
        public async Task NodeStatusGetTopMostBlocks()
        {
            Domain.Model.BlockChain.Block MakeFakeBlock(long index)
            {
                return new Domain.Model.BlockChain.Block(
                    BlockHash.FromString("613dfa26e104465790625ae7bc03fc27a64947c02a9377565ec190405ef7154b"), null,
                    default, index, DateTimeOffset.UtcNow, MerkleTrie.EmptyRootHash, ImmutableArray<Transaction>.Empty);
            }

            BlockChainRepository.Setup(repo => repo.IterateBlocksDescending(0))
                .Returns(new List<Domain.Model.BlockChain.Block>
                {
                    MakeFakeBlock(10),
                    MakeFakeBlock(9),
                    MakeFakeBlock(8),
                    MakeFakeBlock(7),
                    MakeFakeBlock(6),
                    MakeFakeBlock(5),
                    MakeFakeBlock(4),
                    MakeFakeBlock(3),
                    MakeFakeBlock(2),
                    MakeFakeBlock(1),
                    MakeFakeBlock(0),
                });
            BlockChainRepository.Setup(repo => repo.IterateBlocksDescending(5))
                .Returns(new List<Domain.Model.BlockChain.Block>
                {
                    MakeFakeBlock(5),
                    MakeFakeBlock(4),
                    MakeFakeBlock(3),
                    MakeFakeBlock(2),
                    MakeFakeBlock(1),
                    MakeFakeBlock(0),
                });

            var queryWithoutOffset = @"query {
                nodeStatus {
                    topmostBlocks(limit: 1) {
                        index
                    }
                }
            }";

            var queryWithOffset = @"query {
                nodeStatus {
                    topmostBlocks(limit: 1 offset: 5) {
                        index
                    }
                }
            }";

            var queryResult = await ExecuteQueryAsync(queryWithoutOffset);
            var data = (Dictionary<string, object>)((ExecutionNode)queryResult.Data!).ToValue()!;
            var actualResult = ((Dictionary<string, object>)data["nodeStatus"])["topmostBlocks"];
            var expectedResult = new List<object>
            {
                new Dictionary<string, object?>
                {
                    ["index"] = 10,
                }
            };
            Assert.Equal(expectedResult, actualResult);

            queryResult = await ExecuteQueryAsync(queryWithOffset);
            data = (Dictionary<string, object>)((ExecutionNode)queryResult.Data!).ToValue()!;
            actualResult = ((Dictionary<string, object>)data["nodeStatus"])["topmostBlocks"];
            expectedResult = new List<object>
            {
                new Dictionary<string, object?>
                {
                    ["index"] = 5,
                }
            };
            Assert.Equal(expectedResult, actualResult);
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public async Task ValidateMetadata(bool valid)
        {
            var lowMetadata = "{\\\"Index\\\":1}";
            var highMetadata = "{\\\"Index\\\":13340}";

            for (int i = 0; i < 10; i++)
            {
                Block block = BlockChain.ProposeBlock(
                    ProposerPrivateKey,
                    lastCommit: GenerateBlockCommit(BlockChain.Tip.Index, BlockChain.Tip.Hash, GenesisValidators));
                BlockChain.Append(block, GenerateBlockCommit(block.Index, block.Hash, GenesisValidators));
            }
            var query = $@"query {{
                validation {{
                    metadata(raw: ""{(valid ? highMetadata : lowMetadata)}"")
                }}
            }}";

            var result = await ExecuteQueryAsync(query);
            var data = (Dictionary<string, object>)((ExecutionNode)result.Data!).ToValue()!;

            var validationResult =
                ((Dictionary<string, object>)data["validation"])["metadata"];
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
            var data = (Dictionary<string, object>)((ExecutionNode)result.Data!).ToValue()!;
            var validationResult =
                ((Dictionary<string, object>)data["validation"])["privateKey"];
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
            var data = (Dictionary<string, object>)((ExecutionNode)result.Data!).ToValue()!;

            var validationResult =
                ((Dictionary<string, object>)data["validation"])["publicKey"];
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
            var data = (Dictionary<string, object>)((ExecutionNode)result.Data!).ToValue()!;
            var privateKeyResult = (Dictionary<string, object>)
                ((Dictionary<string, object>)data["keyStore"])["privateKey"];
            Assert.Equal(privateKeyHex, privateKeyResult["hex"]);
            var publicKeyResult = (Dictionary<string, object>)privateKeyResult["publicKey"];
            Assert.Equal(ByteUtil.Hex(privateKey.PublicKey.Format(compress)), publicKeyResult["hex"]);
            Assert.Equal(privateKey.Address.ToString(), publicKeyResult["address"]);
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public async Task ActivationStatus(bool existsActivatedAccounts)
        {
            var adminPrivateKey = new PrivateKey();
            var adminAddress = adminPrivateKey.Address;
            var activatedAccounts = ImmutableHashSet<Address>.Empty;

            if (existsActivatedAccounts)
            {
                activatedAccounts = new[] { adminAddress }.ToImmutableHashSet();
            }

            ValidatorSet validatorSetCandidate = new ValidatorSet(new[]
            {
                new Libplanet.Types.Consensus.Validator(ProposerPrivateKey.PublicKey, BigInteger.One),
            }.ToList());
            Block genesis =
                BlockChain.ProposeGenesisBlock(
                    transactions: ImmutableList<Transaction>.Empty
                        .Add(Transaction.Create(0, ProposerPrivateKey, null,
                            new ActionBase[]
                            {
                                new InitializeStates(
                                    rankingState: new RankingState0(),
                                    shopState: new ShopState(),
                                    gameConfigState: new GameConfigState(),
                                    redeemCodeState: new RedeemCodeState(Bencodex.Types.Dictionary.Empty
                                        .Add("address", RedeemCodeState.Address.Serialize())
                                        .Add("map", Bencodex.Types.Dictionary.Empty)
                                    ),
                                    adminAddressState: new AdminState(adminAddress, 1500000),
                                    activatedAccountsState: new ActivatedAccountsState(activatedAccounts),
#pragma warning disable CS0618
                                    // Use of obsolete method Currency.Legacy():
                                    // https://github.com/planetarium/lib9c/discussions/1319
                                    goldCurrencyState: new GoldCurrencyState(Currency.Legacy("NCG", 2, null)),
#pragma warning restore CS0618
                                    goldDistributions: new GoldDistribution[0],
                                    tableSheets: _sheets,
                                    pendingActivationStates: new PendingActivationState[] { }
                                ),
                            }.ToPlainValues()))
                        .AddRange(new IAction[]
                            {
                                new Initialize(
                                    validatorSet: validatorSetCandidate,
                                    states: ImmutableDictionary<Address, IValue>.Empty),
                            }.Select((sa, nonce) =>
                                Transaction.Create(nonce + 1, ProposerPrivateKey, null,
                                    new[] { sa.PlainValue }))
                        ),
                    privateKey: ProposerPrivateKey
                );

            var apvPrivateKey = new PrivateKey();
            var apv = AppProtocolVersion.Sign(apvPrivateKey, 0);
            var userPrivateKey = new PrivateKey();
            var consensusPrivateKey = new PrivateKey();
            var properties = new LibplanetNodeServiceProperties
            {
                Host = System.Net.IPAddress.Loopback.ToString(),
                AppProtocolVersion = apv,
                GenesisBlock = genesis,
                StorePath = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName()),
                StoreStatesCacheSize = 2,
                SwarmPrivateKey = new PrivateKey(),
                ConsensusPrivateKey = consensusPrivateKey,
                ConsensusPort = null,
                Port = null,
                NoMiner = true,
                Render = false,
                Peers = ImmutableHashSet<BoundPeer>.Empty,
                TrustedAppProtocolVersionSigners = null,
                IceServers = ImmutableList<IceServer>.Empty,
                ConsensusSeeds = ImmutableList<BoundPeer>.Empty,
                ConsensusPeers = ImmutableList<BoundPeer>.Empty
            };
            var blockPolicy = new BlockPolicySource().GetPolicy();

            var service = new NineChroniclesNodeService(
                userPrivateKey, properties, blockPolicy, Planet.Odin, StaticActionLoaderSingleton.Instance);
            StandaloneContextFx.NineChroniclesNodeService = service;
            StandaloneContextFx.BlockChain = service.Swarm?.BlockChain;

            var blockChain = StandaloneContextFx.BlockChain!;
            AppendEmptyBlock(GenesisValidators);

            var queryResult = await ExecuteQueryAsync("query { activationStatus { activated } }");
            var data = (Dictionary<string, object>)((ExecutionNode)queryResult.Data!).ToValue()!;
            var result = (bool)
                ((Dictionary<string, object>)data["activationStatus"])["activated"];

            // If we don't use activated accounts, bypass check (always true).
            Assert.Equal(!existsActivatedAccounts, result);

            var nonce = new byte[] { 0x00, 0x01, 0x02, 0x03 };
            var privateKey = new PrivateKey();
            (ActivationKey activationKey, PendingActivationState pendingActivation) =
                ActivationKey.Create(privateKey, nonce);
            ActionBase action = new CreatePendingActivation(pendingActivation);
            blockChain.MakeTransaction(adminPrivateKey, new[] { action });
            Block block = blockChain.ProposeBlock(
                ProposerPrivateKey,
                lastCommit: GenerateBlockCommit(BlockChain.Tip.Index, BlockChain.Tip.Hash, GenesisValidators));
            blockChain.Append(block, GenerateBlockCommit(block.Index, block.Hash, GenesisValidators));

            action = activationKey.CreateActivateAccount(nonce);
            blockChain.MakeTransaction(userPrivateKey, new[] { action });
            block = blockChain.ProposeBlock(
                ProposerPrivateKey,
                lastCommit: GenerateBlockCommit(BlockChain.Tip.Index, BlockChain.Tip.Hash, GenesisValidators));
            blockChain.Append(block, GenerateBlockCommit(block.Index, block.Hash, GenesisValidators));
            AppendEmptyBlock(GenesisValidators);

            queryResult = await ExecuteQueryAsync("query { activationStatus { activated } }");
            data = (Dictionary<string, object>)((ExecutionNode)queryResult.Data!).ToValue()!;
            result = (bool)
                ((Dictionary<string, object>)data["activationStatus"])["activated"];

            Assert.True(result);
        }

        [Fact]
        public async Task GoldBalance()
        {
            var userAddress = new PrivateKey().Address;
            var worldState = new World(MockUtil.MockModernWorldState)
                .SetLegacyState(Addresses.GoldCurrency, new GoldCurrencyState(Currencies.Crystal).Serialize());
            var stateRootHash = worldState.Trie.Hash;
            var tip = new Domain.Model.BlockChain.Block(
                BlockHash.FromString("613dfa26e104465790625ae7bc03fc27a64947c02a9377565ec190405ef7154b"),
                BlockHash.FromString("36456be15af9a5b9b13a02c7ce1e849ae9cba8781ec309010499cdb93e29237d"),
                default(Address),
                0,
                Timestamp: DateTimeOffset.UtcNow,
                StateRootHash: stateRootHash,
                Transactions: ImmutableArray<Transaction>.Empty
            );
            BlockChainRepository.Setup(repository => repository.GetTip())
                .Returns(tip);
            WorldStateRepository.Setup(repository => repository.GetWorldState(stateRootHash))
                .Returns(worldState);

            var query = $"query {{ goldBalance(address: \"{userAddress}\") }}";
            var queryResult = await ExecuteQueryAsync(query);
            var data = (Dictionary<string, object>)((ExecutionNode)queryResult.Data!).ToValue()!;
            Assert.Equal(
                new Dictionary<string, object>
                {
                    ["goldBalance"] = "0"
                },
                data
            );

            worldState = worldState.MintAsset(new ActionContext(), userAddress, Currencies.Crystal * 10);
            stateRootHash = worldState.Trie.Hash;
            tip = new Domain.Model.BlockChain.Block(
                BlockHash.FromString("613dfa26e104465790625ae7bc03fc27a64947c02a9377565ec190405ef7154b"),
                BlockHash.FromString("36456be15af9a5b9b13a02c7ce1e849ae9cba8781ec309010499cdb93e29237d"),
                default(Address),
                0,
                Timestamp: DateTimeOffset.UtcNow,
                StateRootHash: stateRootHash,
                Transactions: ImmutableArray<Transaction>.Empty
            );
            BlockChainRepository.Setup(repository => repository.GetTip())
                .Returns(tip);
            WorldStateRepository.Setup(repository => repository.GetWorldState(stateRootHash))
                .Returns(worldState);

            queryResult = await ExecuteQueryAsync(query);
            data = (Dictionary<string, object>)((ExecutionNode)queryResult.Data!).ToValue()!;
            Assert.Equal(
                new Dictionary<string, object>
                {
                    ["goldBalance"] = "10"
                },
                data
            );
        }

        [Theory]
        [InlineData(null)]
        [InlineData("memo")]
        public async Task TransferNCGHistories(string? memo)
        {
            PrivateKey senderKey = new PrivateKey(), recipientKey = new PrivateKey();
            Address sender = senderKey.Address, recipient = recipientKey.Address;

            var blockHash = BlockHash.FromString("613dfa26e104465790625ae7bc03fc27a64947c02a9377565ec190405ef7154b");
            Transaction MakeTx(long nonce, ActionBase action)
            {
                return Transaction.Create(nonce, senderKey, blockHash, new[] { action.PlainValue });
            }

            var currency = Currency.Uncapped("NCG", 2, null);
            var txs = new[]
            {
                MakeTx(0, new TransferAsset0(sender, recipient, new FungibleAssetValue(currency, 1, 0), memo)),
                MakeTx(1, new TransferAsset(sender, recipient, new FungibleAssetValue(currency, 1, 0), memo)),
            };
            var block = new Domain.Model.BlockChain.Block(
                blockHash,
                BlockHash.FromString("36456be15af9a5b9b13a02c7ce1e849ae9cba8781ec309010499cdb93e29237d"),
                default(Address),
                0,
                Timestamp: DateTimeOffset.UtcNow,
                StateRootHash: MerkleTrie.EmptyRootHash,
                Transactions: txs.ToImmutableArray()
            );

            BlockChainRepository.Setup(repo => repo.GetBlock(blockHash))
                .Returns(block);
            TransactionRepository.Setup(repo => repo.GetTxExecution(blockHash, txs[0].Id))
                .Returns(new TxExecution(
                    blockHash, txs[0].Id, false, MerkleTrie.EmptyRootHash, MerkleTrie.EmptyRootHash, new List<string?>()));
            TransactionRepository.Setup(repo => repo.GetTxExecution(blockHash, txs[1].Id))
                .Returns(new TxExecution(
                    blockHash, txs[1].Id, false, MerkleTrie.EmptyRootHash, MerkleTrie.EmptyRootHash, new List<string?>()));

            var blockHashHex = ByteUtil.Hex(block.Hash.ToByteArray());
            var result =
                await ExecuteQueryAsync(
                    $"{{ transferNCGHistories(blockHash: \"{blockHashHex}\") {{ blockHash txId sender recipient amount memo }} }}");
            var data = (Dictionary<string, object>)((ExecutionNode)result.Data!).ToValue()!;

            ITransferAsset GetFirstCustomActionAsTransferAsset(Transaction tx)
            {
                return (ITransferAsset)ToAction(tx.Actions!.First());
            }

            Assert.Null(result.Errors);
            var expected = block.Transactions.Select(tx => new Dictionary<string, object?>
            {
                ["blockHash"] = block.Hash.ToString(),
                ["txId"] = tx.Id.ToString(),
                ["sender"] = GetFirstCustomActionAsTransferAsset(tx).Sender.ToString(),
                ["recipient"] = GetFirstCustomActionAsTransferAsset(tx).Recipient.ToString(),
                ["amount"] = GetFirstCustomActionAsTransferAsset(tx).Amount.GetQuantityString(),
                ["memo"] = memo,
            }).ToList();
            var actual = data["transferNCGHistories"];
            Assert.Equal(expected, actual);
        }

        [Fact]
        public async Task MinerAddress()
        {
            var userPrivateKey = new PrivateKey();
            var userAddress = userPrivateKey.Address;
            var service = MakeNineChroniclesNodeService(userPrivateKey);
            StandaloneContextFx.NineChroniclesNodeService = service;
            StandaloneContextFx.BlockChain = service.Swarm!.BlockChain;
            const string query = @"query {
                minerAddress
            }";
            var queryResult = await ExecuteQueryAsync(query);
            var data = (Dictionary<string, object?>)((ExecutionNode)queryResult.Data!).ToValue()!;
            Assert.Equal(
                new Dictionary<string, object?>
                {
                    ["minerAddress"] = userAddress.ToString()
                },
                data
            );
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public async Task MonsterCollectionStatus_AgentState_Null(bool miner)
        {
            var userPrivateKey = new PrivateKey();
            var userAddress = userPrivateKey.Address;
            var service = MakeNineChroniclesNodeService(userPrivateKey);
            StandaloneContextFx.NineChroniclesNodeService = service;
            StandaloneContextFx.BlockChain = service.Swarm!.BlockChain;
            if (!miner)
            {
                StandaloneContextFx.NineChroniclesNodeService.MinerPrivateKey = null;
            }
            else
            {
                Assert.Equal(userPrivateKey, StandaloneContextFx.NineChroniclesNodeService.MinerPrivateKey!);
            }

            // FIXME: Remove the above lines after removing `StandaloneContext` dependency.
            var worldState = new World(MockUtil.MockModernWorldState)
                .SetLegacyState(Addresses.GoldCurrency, new GoldCurrencyState(Currencies.Crystal).Serialize())
                .MintAsset(new ActionContext(), userAddress, Currencies.Crystal * 10);
            var stateRootHash = worldState.Trie.Hash;
            var tip = new Domain.Model.BlockChain.Block(
                BlockHash.FromString("613dfa26e104465790625ae7bc03fc27a64947c02a9377565ec190405ef7154b"),
                BlockHash.FromString("36456be15af9a5b9b13a02c7ce1e849ae9cba8781ec309010499cdb93e29237d"),
                default(Address),
                0,
                Timestamp: DateTimeOffset.UtcNow,
                StateRootHash: stateRootHash,
                Transactions: ImmutableArray<Transaction>.Empty
            );
            BlockChainRepository.Setup(repository => repository.GetTip())
                .Returns(tip);
            WorldStateRepository.Setup(repository => repository.GetWorldState(stateRootHash))
                .Returns(worldState);

            string queryArgs = miner ? "" : $@"(address: ""{userAddress}"")";
            string query = $@"query {{
                monsterCollectionStatus{queryArgs} {{
                    fungibleAssetValue {{
                        quantity
                        currency
                    }}
                    rewardInfos {{
                        itemId
                        quantity
                    }}
                }}
            }}";
            var queryResult = await ExecuteQueryAsync(query);
            Assert.NotNull(queryResult.Errors);
            Assert.Single(queryResult.Errors!);
            Assert.Equal($"{nameof(AgentState)} Address: {userAddress} is null.", queryResult.Errors!.First().Message);
        }


        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public async Task MonsterCollectionStatus_MonsterCollectionState_Null(bool miner)
        {
            var userPrivateKey = new PrivateKey();
            var userAddress = userPrivateKey.Address;
            var validators = new List<PrivateKey>
            {
                ProposerPrivateKey, userPrivateKey
            }.OrderBy(x => x.Address).ToList();
            var service = MakeNineChroniclesNodeService(userPrivateKey);
            StandaloneContextFx.NineChroniclesNodeService = service;
            StandaloneContextFx.BlockChain = service.Swarm!.BlockChain;
            if (!miner)
            {
                StandaloneContextFx.NineChroniclesNodeService.MinerPrivateKey = null;
            }

            // FIXME: Remove the above lines after removing `StandaloneContext` dependency.
            var worldState = new World(MockUtil.MockModernWorldState)
                .SetLegacyState(Addresses.GoldCurrency, new GoldCurrencyState(Currencies.Crystal).Serialize())
                .SetAgentState(userAddress, new AgentState(userAddress))
                .MintAsset(new ActionContext(), userAddress, Currencies.Crystal * 10);
            var stateRootHash = worldState.Trie.Hash;
            var tip = new Domain.Model.BlockChain.Block(
                BlockHash.FromString("613dfa26e104465790625ae7bc03fc27a64947c02a9377565ec190405ef7154b"),
                BlockHash.FromString("36456be15af9a5b9b13a02c7ce1e849ae9cba8781ec309010499cdb93e29237d"),
                default(Address),
                0,
                Timestamp: DateTimeOffset.UtcNow,
                StateRootHash: stateRootHash,
                Transactions: ImmutableArray<Transaction>.Empty
            );
            BlockChainRepository.Setup(repository => repository.GetTip())
                .Returns(tip);
            WorldStateRepository.Setup(repository => repository.GetWorldState(stateRootHash))
                .Returns(worldState);

            string queryArgs = miner ? "" : $@"(address: ""{userAddress}"")";
            string query = $@"query {{
                monsterCollectionStatus{queryArgs} {{
                    fungibleAssetValue {{
                        quantity
                        currency
                    }}
                    rewardInfos {{
                        itemId
                        quantity
                    }}
                }}
            }}";
            var queryResult = await ExecuteQueryAsync(query);
            Assert.NotNull(queryResult.Errors);
            Assert.Single(queryResult.Errors!);
            Assert.Equal(
                $"{nameof(MonsterCollectionState)} Address: {MonsterCollectionState.DeriveAddress(userAddress, 0)} is null.",
                queryResult.Errors!.First().Message
            );
        }

        [Fact]
        public async Task Avatar()
        {
            var agentAddress = new Address("f189c04126e2e708cd7d17cd68a7b7f10bbb6f16");
            var avatarAddress = new Address("f1a005c01e683dbcab9a306d5cc70d5e57fccfa9");
            var avatarState = new AvatarState((List)new Codec().Decode(Convert.FromHexString(
                "6c6c32303af1a005c01e683dbcab9a306d5cc70d5e57fccfa96569326575353a6261447561693130303031306569333336656937313231346575383a313134393233323632303af189c04126e2e708cd7d17cd68a7b7f10bbb6f166c647531303a6174746163686d656e74647531313a616374696f6e506f696e7475313a3075313a636c647531333a646563696d616c506c61636573313a1275373a6d696e746572736e75363a7469636b657275373a4352595354414c65693065657531373a656e68616e63656d656e74526573756c7475373a5375636365737375343a676f6c6469306575323a696431363a17b05d00fea9864c901ede7c9f4d31bb7531303a6974656d557361626c65647531303a62756666536b696c6c736c657531343a656c656d656e74616c5f7479706575343a4c616e6475363a65715f65787075323a333275383a65717569707065646675353a677261646575313a3175323a696475383a313034313330303075363a6974656d496431363a788c0c286da7c14abde5ded62de0f44d7531333a6974656d5f7375625f7479706575383a4e65636b6c61636575393a6974656d5f7479706575393a45717569706d656e7475353a6c6576656c75313a3275323a6f6375313a337531383a7265717569726564426c6f636b496e64657875383a313134393232363775363a7365745f696475313a3475363a736b696c6c736c6475363a6368616e636575313a3875353a706f77657275313a3075383a736b696c6c526f776475353a636f6d626f6675383a636f6f6c646f776e693235657531343a656c656d656e74616c5f7479706575363a4e6f726d616c75393a6869745f636f756e7469316575323a696469323430303030657531343a736b696c6c5f63617465676f727975373a486974427566667531373a736b696c6c5f7461726765745f7479706575343a53656c667531303a736b696c6c5f7479706575343a427566666565657531393a7370696e655f7265736f757263655f7061746875383a313034313330303075343a737461746475343a74797065343a0500000075353a76616c756575333a3335376575383a73746174734d617064343a03000000647531353a6164646974696f6e616c56616c75657531313a33312e303030303030303075383a7374617454797065343a0300000075353a76616c756575313a3065343a05000000647531353a6164646974696f6e616c56616c75657531323a3130362e303030303030303075383a7374617454797065343a0500000075353a76616c756575383a3434322e323133316565657531383a6d6174657269616c4974656d49644c6973746c31363ad5f36402d57e214691607fa35606e52931363a7f45c50e8c9cdc48b87b1c28af35597031363a11eaf70e63890447894dacb39c8402d0657531333a7072654974656d557361626c65647531303a62756666536b696c6c736c657531343a656c656d656e74616c5f7479706575343a4c616e6475363a65715f65787075313a3875383a65717569707065646675353a677261646575313a3175323a696475383a313034313330303075363a6974656d496431363a788c0c286da7c14abde5ded62de0f44d7531333a6974656d5f7375625f7479706575383a4e65636b6c61636575393a6974656d5f7479706575393a45717569706d656e7475353a6c6576656c75313a3075323a6f6375313a337531383a7265717569726564426c6f636b496e64657875383a313134383936323075363a7365745f696475313a3475363a736b696c6c736c6475363a6368616e636575313a3875353a706f77657275313a3075383a736b696c6c526f776475353a636f6d626f6675383a636f6f6c646f776e693235657531343a656c656d656e74616c5f7479706575363a4e6f726d616c75393a6869745f636f756e7469316575323a696469323430303030657531343a736b696c6c5f63617465676f727975373a486974427566667531373a736b696c6c5f7461726765745f7479706575343a53656c667531303a736b696c6c5f7479706575343a427566666565657531393a7370696e655f7265736f757263655f7061746875383a313034313330303075343a737461746475343a74797065343a0500000075353a76616c756575333a3335376575383a73746174734d617064343a03000000647531353a6164646974696f6e616c56616c756575323a333175383a7374617454797065343a0300000075353a76616c756575313a3065343a05000000647531353a6164646974696f6e616c56616c756575333a31303675383a7374617454797065343a0500000075353a76616c756575333a33353765656575363a7479706549647532353a6974656d5f656e68616e63656d656e7431332e726573756c74657531303a626c6f636b496e64657875383a313134393232323775323a696431363a17b05d00fea9864c901ede7c9f4d31bb7531383a7265717569726564426c6f636b496e64657875383a313134393232363775363a7479706549647531313a6974656d456e68616e636565647531303a626c6f636b496e64657875383a313134373830303375323a696431363af4bef400ccc4624da8f69f1e78a0478375323a706931363af4bef400ccc4624da8f69f1e78a047837531383a7265717569726564426c6f636b496e64657875383a313134373830303375363a7479706549647531373a50726f6475637453656c6c65724d61696c65647531303a626c6f636b496e64657875383a313134383035363375323a696431363adc9740029e56f544bcde568ecce9adbf75323a706931363adc9740029e56f544bcde568ecce9adbf7531383a7265717569726564426c6f636b496e64657875383a313134383035363375363a7479706549647531373a50726f6475637453656c6c65724d61696c65647531303a6174746163686d656e74647531313a616374696f6e506f696e7475313a3075343a676f6c6469306575323a696431363a070d1d1d75c4a849a6ee8e626b08542e7531303a6974656d557361626c65647531303a62756666536b696c6c736c657531343a656c656d656e74616c5f7479706575343a4c616e6475363a65715f65787075313a3875383a65717569707065646675353a677261646575313a3175323a696475383a313034313330303075363a6974656d496431363a080f06a366c6e84c993ac8525b1d6cf27531333a6974656d5f7375625f7479706575383a4e65636b6c61636575393a6974656d5f7479706575393a45717569706d656e7475353a6c6576656c75313a3075323a6f6375313a337531383a7265717569726564426c6f636b496e64657875383a313134393033323275363a7365745f696475313a3475363a736b696c6c736c657531393a7370696e655f7265736f757263655f7061746875383a313034313330303075343a737461746475343a74797065343a0500000075353a76616c756575333a3335376575383a73746174734d617064343a03000000647531353a6164646974696f6e616c56616c756575323a323675383a7374617454797065343a0300000075353a76616c756575313a3065343a05000000647531353a6164646974696f6e616c56616c756575333a31373175383a7374617454797065343a0500000075353a76616c756575333a33353765656575393a6d6174657269616c736c6475353a636f756e7475323a313575383a6d6174657269616c647531343a656c656d656e74616c5f7479706575363a4e6f726d616c75353a677261646575313a3175323a696475363a33303333303075373a6974656d5f696433323a1bd96c8d5bce17827b894e01d4eb87bd853bc6a48baee9e6151ce568ae43ad397531333a6974656d5f7375625f747970657531373a45717569706d656e744d6174657269616c75393a6974656d5f7479706575383a4d6174657269616c65656475353a636f756e7475323a323075383a6d6174657269616c647531343a656c656d656e74616c5f7479706575343a4c616e6475353a677261646575313a3275323a696475363a33303630303975373a6974656d5f696433323a67f6ff0b0e20202a8e63742adf804355ceaee3ab636ed3065bd535985e7109a87531333a6974656d5f7375625f747970657531313a4d6f6e737465725061727475393a6974656d5f7479706575383a4d6174657269616c65656475353a636f756e7475323a323975383a6d6174657269616c647531343a656c656d656e74616c5f7479706575343a4c616e6475353a677261646575313a3275323a696475363a33303630313075373a6974656d5f696433323ae0e69c7f4f16174438570e79ef52f5091ff761615a29dc70d3babbb4a099ce337531333a6974656d5f7375625f747970657531313a4d6f6e737465725061727475393a6974656d5f7479706575383a4d6174657269616c65656475353a636f756e7475323a323075383a6d6174657269616c647531343a656c656d656e74616c5f7479706575343a4c616e6475353a677261646575313a3275323a696475363a33303630313175373a6974656d5f696433323a2d85afe3aa2e0ebc49df0149cf939feeddbfb9049a1e0c3bd7d8c00a948fece67531333a6974656d5f7375625f747970657531313a4d6f6e737465725061727475393a6974656d5f7479706575383a4d6174657269616c65656575383a726563697065496475333a3131317531313a737562526563697065496475333a32363075363a7479706549647532343a636f6d62696e6174696f6e2e726573756c742d6d6f64656c657531303a626c6f636b496e64657875383a313134383935393875323a696431363a070d1d1d75c4a849a6ee8e626b08542e7531383a7265717569726564426c6f636b496e64657875383a313134393033323275363a7479706549647531353a636f6d62696e6174696f6e4d61696c65647531303a626c6f636b496e64657875383a313134373837373975313a666c6c647531333a646563696d616c506c61636573313a1275373a6d696e746572736e75363a7469636b657275373a4352595354414c65693234303030303030303030303030303030303030303065656575313a696c6c693830303230316569313265656575323a696431363a5580de2c0e57bc4a936439bd3e0e548275313a6d7536303a706174726f6c207265776172642046314130303543303145363833644263416239413330366435634337304435453537666363466139202f203331327531383a7265717569726564426c6f636b496e64657875383a313134373837373975363a7479706549647531343a436c61696d4974656d734d61696c65647531303a6174746163686d656e74647531313a616374696f6e506f696e7475313a3075343a676f6c6469306575323a696431363a91fe9e33e696034cae9720cea467aaed7531303a6974656d557361626c65647531303a62756666536b696c6c736c657531343a656c656d656e74616c5f7479706575343a57696e6475363a65715f65787075323a313075383a65717569707065646675353a677261646575313a3175323a696475383a313035313430303075363a6974656d496431363afc0b4deaa2aa1342910de9bb0f4e868f7531333a6974656d5f7375625f7479706575343a52696e6775393a6974656d5f7479706575393a45717569706d656e7475353a6c6576656c75313a3075323a6f6375313a337531383a7265717569726564426c6f636b496e64657875383a313135303236383775363a7365745f696475313a3575363a736b696c6c736c657531393a7370696e655f7265736f757263655f7061746875383a313035313430303075343a737461746475343a74797065343a0300000075353a76616c756575333a3130376575383a73746174734d617064343a03000000647531353a6164646974696f6e616c56616c756575323a393275383a7374617454797065343a0300000075353a76616c756575333a31303765343a06000000647531353a6164646974696f6e616c56616c756575333a34323075383a7374617454797065343a0600000075353a76616c756575313a3065656575393a6d6174657269616c736c6475353a636f756e7475323a323475383a6d6174657269616c647531343a656c656d656e74616c5f7479706575363a4e6f726d616c75353a677261646575313a3175323a696475363a33303334303075373a6974656d5f696433323a45c8a87009caf8f9d3ecf58618fa6e2b14fe0edb2283d373e3c190c56eb96d8a7531333a6974656d5f7375625f747970657531373a45717569706d656e744d6174657269616c75393a6974656d5f7479706575383a4d6174657269616c65656475353a636f756e7475323a313275383a6d6174657269616c647531343a656c656d656e74616c5f7479706575343a57696e6475353a677261646575313a3175323a696475363a33303630343275373a6974656d5f696433323a31f48810fa91236281c165659d65003cfb611205a52c1ff6e02c019b86625dda7531333a6974656d5f7375625f747970657531313a4d6f6e737465725061727475393a6974656d5f7479706575383a4d6174657269616c65656475353a636f756e7475323a343775383a6d6174657269616c647531343a656c656d656e74616c5f7479706575343a57696e6475353a677261646575313a3275323a696475363a33303630353075373a6974656d5f696433323adf0c212eb56526891629367fe889cfce0ea89a04e21d11a05b19ff9231802b4c7531333a6974656d5f7375625f747970657531313a4d6f6e737465725061727475393a6974656d5f7479706575383a4d6174657269616c65656475353a636f756e7475323a373075383a6d6174657269616c647531343a656c656d656e74616c5f7479706575343a57696e6475353a677261646575313a3275323a696475363a33303630353175373a6974656d5f696433323a5eac6f2b799a1fc20a50f005f2df8783fefec63595787804b4418ece2b3d07177531333a6974656d5f7375625f747970657531313a4d6f6e737465725061727475393a6974656d5f7479706575383a4d6174657269616c65656575383a726563697065496475333a3133377531313a737562526563697065496475333a33323375363a7479706549647532343a636f6d62696e6174696f6e2e726573756c742d6d6f64656c657531303a626c6f636b496e64657875383a313134393232333075323a696431363a91fe9e33e696034cae9720cea467aaed7531383a7265717569726564426c6f636b496e64657875383a313135303236383775363a7479706549647531353a636f6d62696e6174696f6e4d61696c65647531303a6174746163686d656e74647531313a616374696f6e506f696e7475313a3075343a676f6c6469306575323a696431363aa09bb5427904a84db14bf0e004729f247531303a6974656d557361626c65647531303a62756666536b696c6c736c657531343a656c656d656e74616c5f7479706575363a4e6f726d616c75363a65715f65787075313a3575383a65717569707065646675353a677261646575313a3175323a696475383a313035313030303075363a6974656d496431363a31541a02030725409e67ad7b14fd1d807531333a6974656d5f7375625f7479706575343a52696e6775393a6974656d5f7479706575393a45717569706d656e7475353a6c6576656c75313a3075323a6f6375313a337531383a7265717569726564426c6f636b496e64657875383a313134383334363275363a7365745f696475313a3175363a736b696c6c736c657531393a7370696e655f7265736f757263655f7061746875383a313035313030303075343a737461746475343a74797065343a0300000075353a76616c756575313a386575383a73746174734d617064343a02000000647531353a6164646974696f6e616c56616c756575313a3675383a7374617454797065343a0200000075353a76616c756575313a3065343a03000000647531353a6164646974696f6e616c56616c756575313a3775383a7374617454797065343a0300000075353a76616c756575313a3865656575393a6d6174657269616c736c6475353a636f756e7475313a3275383a6d6174657269616c647531343a656c656d656e74616c5f7479706575363a4e6f726d616c75353a677261646575313a3175323a696475363a33303334303075373a6974656d5f696433323a45c8a87009caf8f9d3ecf58618fa6e2b14fe0edb2283d373e3c190c56eb96d8a7531333a6974656d5f7375625f747970657531373a45717569706d656e744d6174657269616c75393a6974656d5f7479706575383a4d6174657269616c65656475353a636f756e7475313a3275383a6d6174657269616c647531343a656c656d656e74616c5f7479706575353a576174657275353a677261646575313a3175323a696475363a33303630303175373a6974656d5f696433323a77cfad5273885820b69e61c2106ae06583f69c7f6d82a229b59e5f1b984164be7531333a6974656d5f7375625f747970657531313a4d6f6e737465725061727475393a6974656d5f7479706575383a4d6174657269616c65656475353a636f756e7475313a3275383a6d6174657269616c647531343a656c656d656e74616c5f7479706575353a576174657275353a677261646575313a3175323a696475363a33303630303275373a6974656d5f696433323a28a2c38b0934225a8f2327b9bd8cf7c7eaf3551bde8203f44248c011938e97707531333a6974656d5f7375625f747970657531313a4d6f6e737465725061727475393a6974656d5f7479706575383a4d6174657269616c65656475353a636f756e7475313a3175383a6d6174657269616c647531343a656c656d656e74616c5f7479706575343a4669726575353a677261646575313a3175323a696475363a33303630323375373a6974656d5f696433323a1af5e747fc81a7f3149af024db2e5025b6749a000d66b22e665acdd45b8dbd177531333a6974656d5f7375625f747970657531313a4d6f6e737465725061727475393a6974656d5f7479706575383a4d6174657269616c65656575383a726563697065496475333a3133337531313a737562526563697065496475333a34353875363a7479706549647532343a636f6d62696e6174696f6e2e726573756c742d6d6f64656c657531303a626c6f636b496e64657875383a313134383334353475323a696431363aa09bb5427904a84db14bf0e004729f247531383a7265717569726564426c6f636b496e64657875383a313134383334363275363a7479706549647531353a636f6d62696e6174696f6e4d61696c65647531303a626c6f636b496e64657875383a313134383132393975313a696c6c6936303032303165693565656575323a696431363aecaa0b486ea7564da3971e00d5fb8b9d75313a6d7535313a7b22736561736f6e5f70617373223a207b226e223a205b32315d2c202270223a205b5d2c202274223a2022636c61696d227d7d7531383a7265717569726564426c6f636b496e64657875383a313134383132393975363a7479706549647531343a436c61696d4974656d734d61696c65647531303a626c6f636b496e64657875383a313134383934363675313a666c6c647531333a646563696d616c506c61636573313a1275373a6d696e746572736e75363a7469636b657275373a4352595354414c65693234303030303030303030303030303030303030303065656575313a696c6c693830303230316569313265656575323a696431363a5f21e54bd0119549a07713182f22ee7a75313a6d7536303a706174726f6c207265776172642046314130303543303145363833644263416239413330366435634337304435453537666363466139202f203331337531383a7265717569726564426c6f636b496e64657875383a313134383934363675363a7479706549647531343a436c61696d4974656d734d61696c65647531303a6174746163686d656e74647531313a616374696f6e506f696e7475313a3075343a676f6c6469306575323a696431363a48e84158fc051044a7316a5a8e3fed177531303a6974656d557361626c65647531303a62756666536b696c6c736c657531343a656c656d656e74616c5f7479706575343a4c616e6475363a65715f65787075313a3875383a65717569707065646675353a677261646575313a3175323a696475383a313034313330303075363a6974656d496431363af62bd25f4b3e964682fee972bdf6174f7531333a6974656d5f7375625f7479706575383a4e65636b6c61636575393a6974656d5f7479706575393a45717569706d656e7475353a6c6576656c75313a3075323a6f6375313a337531383a7265717569726564426c6f636b496e64657875383a313134383334363975363a7365745f696475313a3475363a736b696c6c736c6475363a6368616e636575313a3875353a706f77657275313a3075383a736b696c6c526f776475353a636f6d626f6675383a636f6f6c646f776e693235657531343a656c656d656e74616c5f7479706575363a4e6f726d616c75393a6869745f636f756e7469316575323a696469323430303030657531343a736b696c6c5f63617465676f727975373a486974427566667531373a736b696c6c5f7461726765745f7479706575343a53656c667531303a736b696c6c5f7479706575343a427566666565657531393a7370696e655f7265736f757263655f7061746875383a313034313330303075343a737461746475343a74797065343a0500000075353a76616c756575333a3335376575383a73746174734d617064343a03000000647531353a6164646974696f6e616c56616c756575323a333075383a7374617454797065343a0300000075353a76616c756575313a3065343a05000000647531353a6164646974696f6e616c56616c756575323a393975383a7374617454797065343a0500000075353a76616c756575333a33353765656575393a6d6174657269616c736c6475353a636f756e7475323a313575383a6d6174657269616c647531343a656c656d656e74616c5f7479706575363a4e6f726d616c75353a677261646575313a3175323a696475363a33303333303075373a6974656d5f696433323a1bd96c8d5bce17827b894e01d4eb87bd853bc6a48baee9e6151ce568ae43ad397531333a6974656d5f7375625f747970657531373a45717569706d656e744d6174657269616c75393a6974656d5f7479706575383a4d6174657269616c65656475353a636f756e7475323a323075383a6d6174657269616c647531343a656c656d656e74616c5f7479706575343a4c616e6475353a677261646575313a3275323a696475363a33303630303975373a6974656d5f696433323a67f6ff0b0e20202a8e63742adf804355ceaee3ab636ed3065bd535985e7109a87531333a6974656d5f7375625f747970657531313a4d6f6e737465725061727475393a6974656d5f7479706575383a4d6174657269616c65656475353a636f756e7475323a323975383a6d6174657269616c647531343a656c656d656e74616c5f7479706575343a4c616e6475353a677261646575313a3275323a696475363a33303630313075373a6974656d5f696433323ae0e69c7f4f16174438570e79ef52f5091ff761615a29dc70d3babbb4a099ce337531333a6974656d5f7375625f747970657531313a4d6f6e737465725061727475393a6974656d5f7479706575383a4d6174657269616c65656475353a636f756e7475323a323075383a6d6174657269616c647531343a656c656d656e74616c5f7479706575343a4c616e6475353a677261646575313a3275323a696475363a33303630313175373a6974656d5f696433323a2d85afe3aa2e0ebc49df0149cf939feeddbfb9049a1e0c3bd7d8c00a948fece67531333a6974656d5f7375625f747970657531313a4d6f6e737465725061727475393a6974656d5f7479706575383a4d6174657269616c65656575383a726563697065496475333a3131317531313a737562526563697065496475333a32363075363a7479706549647532343a636f6d62696e6174696f6e2e726573756c742d6d6f64656c657531303a626c6f636b496e64657875383a313134383334343975323a696431363a48e84158fc051044a7316a5a8e3fed177531383a7265717569726564426c6f636b496e64657875383a313134383334363975363a7479706549647531353a636f6d62696e6174696f6e4d61696c65647531303a626c6f636b496e64657875383a313134383035363375323a696431363a3c5cc35a76d6b9459e6edcac8ae2539575323a706931363a3c5cc35a76d6b9459e6edcac8ae253957531383a7265717569726564426c6f636b496e64657875383a313134383035363375363a7479706549647531373a50726f6475637453656c6c65724d61696c65647531303a6174746163686d656e74647531313a616374696f6e506f696e7475313a3075343a676f6c6469306575323a696431363a5978fb5b2ec75949aec15d76f13d87827531303a6974656d557361626c65647531303a62756666536b696c6c736c657531343a656c656d656e74616c5f7479706575363a4e6f726d616c75363a65715f65787075313a3575383a65717569707065646675353a677261646575313a3175323a696475383a313035313030303075363a6974656d496431363a83c590459b76b6418b041f9f372544ca7531333a6974656d5f7375625f7479706575343a52696e6775393a6974656d5f7479706575393a45717569706d656e7475353a6c6576656c75313a3075323a6f6375313a347531383a7265717569726564426c6f636b496e64657875383a313134383937353675363a7365745f696475313a3175363a736b696c6c736c6475363a6368616e636575313a3875353a706f77657275313a3075383a736b696c6c526f776475353a636f6d626f6675383a636f6f6c646f776e693235657531343a656c656d656e74616c5f7479706575363a4e6f726d616c75393a6869745f636f756e7469316575323a696469323130303030657531343a736b696c6c5f63617465676f72797531303a41747461636b427566667531373a736b696c6c5f7461726765745f7479706575343a53656c667531303a736b696c6c5f7479706575343a427566666565657531393a7370696e655f7265736f757263655f7061746875383a313035313030303075343a737461746475343a74797065343a0300000075353a76616c756575313a386575383a73746174734d617064343a02000000647531353a6164646974696f6e616c56616c756575313a3475383a7374617454797065343a0200000075353a76616c756575313a3065343a03000000647531353a6164646974696f6e616c56616c756575313a3875383a7374617454797065343a0300000075353a76616c756575313a3865656575393a6d6174657269616c736c6475353a636f756e7475313a3275383a6d6174657269616c647531343a656c656d656e74616c5f7479706575363a4e6f726d616c75353a677261646575313a3175323a696475363a33303334303075373a6974656d5f696433323a45c8a87009caf8f9d3ecf58618fa6e2b14fe0edb2283d373e3c190c56eb96d8a7531333a6974656d5f7375625f747970657531373a45717569706d656e744d6174657269616c75393a6974656d5f7479706575383a4d6174657269616c65656475353a636f756e7475313a3275383a6d6174657269616c647531343a656c656d656e74616c5f7479706575353a576174657275353a677261646575313a3175323a696475363a33303630303175373a6974656d5f696433323a77cfad5273885820b69e61c2106ae06583f69c7f6d82a229b59e5f1b984164be7531333a6974656d5f7375625f747970657531313a4d6f6e737465725061727475393a6974656d5f7479706575383a4d6174657269616c65656475353a636f756e7475313a3275383a6d6174657269616c647531343a656c656d656e74616c5f7479706575353a576174657275353a677261646575313a3175323a696475363a33303630303275373a6974656d5f696433323a28a2c38b0934225a8f2327b9bd8cf7c7eaf3551bde8203f44248c011938e97707531333a6974656d5f7375625f747970657531313a4d6f6e737465725061727475393a6974656d5f7479706575383a4d6174657269616c65656475353a636f756e7475313a3175383a6d6174657269616c647531343a656c656d656e74616c5f7479706575343a4669726575353a677261646575313a3175323a696475363a33303630323375373a6974656d5f696433323a1af5e747fc81a7f3149af024db2e5025b6749a000d66b22e665acdd45b8dbd177531333a6974656d5f7375625f747970657531313a4d6f6e737465725061727475393a6974656d5f7479706575383a4d6174657269616c65656575383a726563697065496475333a3133337531313a737562526563697065496475333a34353875363a7479706549647532343a636f6d62696e6174696f6e2e726573756c742d6d6f64656c657531303a626c6f636b496e64657875383a313134383937333675323a696431363a5978fb5b2ec75949aec15d76f13d87827531383a7265717569726564426c6f636b496e64657875383a313134383937353675363a7479706549647531353a636f6d62696e6174696f6e4d61696c65647531303a626c6f636b496e64657875383a313134383035363375323a696431363a75ff80623e199846804b450232e452dd75323a706931363a75ff80623e199846804b450232e452dd7531383a7265717569726564426c6f636b496e64657875383a313134383035363375363a7479706549647531373a50726f6475637453656c6c65724d61696c65647531303a626c6f636b496e64657875383a313134383035363375323a696431363a5797bc645cc0f045b117e4e133e323bb75323a706931363a5797bc645cc0f045b117e4e133e323bb7531383a7265717569726564426c6f636b496e64657875383a313134383035363375363a7479706549647531373a50726f6475637453656c6c65724d61696c65647531303a6174746163686d656e74647531313a616374696f6e506f696e7475313a3075343a676f6c6469306575323a696431363aeb3383670cd49a41aa6a956fb38caa267531303a6974656d557361626c65647531303a62756666536b696c6c736c657531343a656c656d656e74616c5f7479706575343a57696e6475363a65715f65787075323a313075383a65717569707065646675353a677261646575313a3175323a696475383a313035313430303075363a6974656d496431363aa8f4c138733f0e4f851abb2a7552555b7531333a6974656d5f7375625f7479706575343a52696e6775393a6974656d5f7479706575393a45717569706d656e7475353a6c6576656c75313a3075323a6f6375313a347531383a7265717569726564426c6f636b496e64657875383a313134393233343475363a7365745f696475313a3575363a736b696c6c736c6475363a6368616e636575313a3975353a706f77657275343a3334323875383a736b696c6c526f776475353a636f6d626f6675383a636f6f6c646f776e693133657531343a656c656d656e74616c5f7479706575343a57696e6475393a6869745f636f756e7469356575323a696469313430303035657531343a736b696c6c5f63617465676f72797531303a4172656141747461636b7531373a736b696c6c5f7461726765745f7479706575373a456e656d6965737531303a736b696c6c5f7479706575363a41747461636b6565657531393a7370696e655f7265736f757263655f7061746875383a313035313430303075343a737461746475343a74797065343a0300000075353a76616c756575333a3130376575383a73746174734d617064343a03000000647531353a6164646974696f6e616c56616c756575323a393175383a7374617454797065343a0300000075353a76616c756575333a31303765343a06000000647531353a6164646974696f6e616c56616c756575333a34303175383a7374617454797065343a0600000075353a76616c756575313a3065656575393a6d6174657269616c736c6475353a636f756e7475323a323475383a6d6174657269616c647531343a656c656d656e74616c5f7479706575363a4e6f726d616c75353a677261646575313a3175323a696475363a33303334303075373a6974656d5f696433323a45c8a87009caf8f9d3ecf58618fa6e2b14fe0edb2283d373e3c190c56eb96d8a7531333a6974656d5f7375625f747970657531373a45717569706d656e744d6174657269616c75393a6974656d5f7479706575383a4d6174657269616c65656475353a636f756e7475323a313275383a6d6174657269616c647531343a656c656d656e74616c5f7479706575343a57696e6475353a677261646575313a3175323a696475363a33303630343275373a6974656d5f696433323a31f48810fa91236281c165659d65003cfb611205a52c1ff6e02c019b86625dda7531333a6974656d5f7375625f747970657531313a4d6f6e737465725061727475393a6974656d5f7479706575383a4d6174657269616c65656475353a636f756e7475323a343775383a6d6174657269616c647531343a656c656d656e74616c5f7479706575343a57696e6475353a677261646575313a3275323a696475363a33303630353075373a6974656d5f696433323adf0c212eb56526891629367fe889cfce0ea89a04e21d11a05b19ff9231802b4c7531333a6974656d5f7375625f747970657531313a4d6f6e737465725061727475393a6974656d5f7479706575383a4d6174657269616c65656475353a636f756e7475323a373075383a6d6174657269616c647531343a656c656d656e74616c5f7479706575343a57696e6475353a677261646575313a3275323a696475363a33303630353175373a6974656d5f696433323a5eac6f2b799a1fc20a50f005f2df8783fefec63595787804b4418ece2b3d07177531333a6974656d5f7375625f747970657531313a4d6f6e737465725061727475393a6974656d5f7479706575383a4d6174657269616c65656575383a726563697065496475333a3133377531313a737562526563697065496475333a33323375363a7479706549647532343a636f6d62696e6174696f6e2e726573756c742d6d6f64656c657531303a626c6f636b496e64657875383a313134393233323475323a696431363aeb3383670cd49a41aa6a956fb38caa267531383a7265717569726564426c6f636b496e64657875383a313134393233343475363a7479706549647531353a636f6d62696e6174696f6e4d61696c65647531303a626c6f636b496e64657875383a313134383035363375323a696431363a1463b76e102ce24e841f3f88020aa05275323a706931363a1463b76e102ce24e841f3f88020aa0527531383a7265717569726564426c6f636b496e64657875383a313134383035363375363a7479706549647531373a50726f6475637453656c6c65724d61696c65647531303a626c6f636b496e64657875383a313134393232343275313a666c6c647531333a646563696d616c506c61636573313a1275373a6d696e746572736e75363a7469636b657275373a4352595354414c656931353030303030303030303030303030303030303030303065656575323a696431363afa061a8852396448896835e2906fe61175313a6d7535313a7b22736561736f6e5f70617373223a207b226e223a205b32325d2c202270223a205b5d2c202274223a2022636c61696d227d7d7531383a7265717569726564426c6f636b496e64657875383a313134393232343275363a7479706549647531343a436c61696d4974656d734d61696c65647531303a626c6f636b496e64657875383a313134383035363375323a696431363ab966678b92d4424d99fde7bd69d2148775323a706931363ab966678b92d4424d99fde7bd69d214877531383a7265717569726564426c6f636b496e64657875383a313134383035363375363a7479706549647531373a50726f6475637453656c6c65724d61696c65647531303a626c6f636b496e64657875383a313134383035363375323a696431363ad88ca59d18c2ad409e257322e0e3f33475323a706931363ad88ca59d18c2ad409e257322e0e3f3347531383a7265717569726564426c6f636b496e64657875383a313134383035363375363a7479706549647531373a50726f6475637453656c6c65724d61696c65647531303a626c6f636b496e64657875383a313134373836333675323a696431363a05c08f9e2558b849be91dcea16e6d8a475323a706931363a05c08f9e2558b849be91dcea16e6d8a47531383a7265717569726564426c6f636b496e64657875383a313134373836333675363a7479706549647531373a50726f6475637453656c6c65724d61696c65647531303a6174746163686d656e74647531313a616374696f6e506f696e7475313a3075343a676f6c6469306575323a696431363a99257ba2c86ea4449a8ca8132be300f37531303a6974656d557361626c65647531303a62756666536b696c6c736c657531343a656c656d656e74616c5f7479706575343a57696e6475363a65715f65787075323a313075383a65717569707065646675353a677261646575313a3175323a696475383a313035313430303075363a6974656d496431363af2b0e55aa26e454984c09067ce4162827531333a6974656d5f7375625f7479706575343a52696e6775393a6974656d5f7479706575393a45717569706d656e7475353a6c6576656c75313a3075323a6f6375313a337531383a7265717569726564426c6f636b496e64657875383a313135303236383675363a7365745f696475313a3575363a736b696c6c736c657531393a7370696e655f7265736f757263655f7061746875383a313035313430303075343a737461746475343a74797065343a0300000075353a76616c756575333a3130376575383a73746174734d617064343a03000000647531353a6164646974696f6e616c56616c756575323a393475383a7374617454797065343a0300000075353a76616c756575333a31303765343a06000000647531353a6164646974696f6e616c56616c756575333a33393675383a7374617454797065343a0600000075353a76616c756575313a3065656575393a6d6174657269616c736c6475353a636f756e7475323a323475383a6d6174657269616c647531343a656c656d656e74616c5f7479706575363a4e6f726d616c75353a677261646575313a3175323a696475363a33303334303075373a6974656d5f696433323a45c8a87009caf8f9d3ecf58618fa6e2b14fe0edb2283d373e3c190c56eb96d8a7531333a6974656d5f7375625f747970657531373a45717569706d656e744d6174657269616c75393a6974656d5f7479706575383a4d6174657269616c65656475353a636f756e7475323a313275383a6d6174657269616c647531343a656c656d656e74616c5f7479706575343a57696e6475353a677261646575313a3175323a696475363a33303630343275373a6974656d5f696433323a31f48810fa91236281c165659d65003cfb611205a52c1ff6e02c019b86625dda7531333a6974656d5f7375625f747970657531313a4d6f6e737465725061727475393a6974656d5f7479706575383a4d6174657269616c65656475353a636f756e7475323a343775383a6d6174657269616c647531343a656c656d656e74616c5f7479706575343a57696e6475353a677261646575313a3275323a696475363a33303630353075373a6974656d5f696433323adf0c212eb56526891629367fe889cfce0ea89a04e21d11a05b19ff9231802b4c7531333a6974656d5f7375625f747970657531313a4d6f6e737465725061727475393a6974656d5f7479706575383a4d6174657269616c65656475353a636f756e7475323a373075383a6d6174657269616c647531343a656c656d656e74616c5f7479706575343a57696e6475353a677261646575313a3275323a696475363a33303630353175373a6974656d5f696433323a5eac6f2b799a1fc20a50f005f2df8783fefec63595787804b4418ece2b3d07177531333a6974656d5f7375625f747970657531313a4d6f6e737465725061727475393a6974656d5f7479706575383a4d6174657269616c65656575383a726563697065496475333a3133377531313a737562526563697065496475333a33323375363a7479706549647532343a636f6d62696e6174696f6e2e726573756c742d6d6f64656c657531303a626c6f636b496e64657875383a313134393232323975323a696431363a99257ba2c86ea4449a8ca8132be300f37531383a7265717569726564426c6f636b496e64657875383a313135303236383675363a7479706549647531353a636f6d62696e6174696f6e4d61696c65647531303a6174746163686d656e74647531313a616374696f6e506f696e7475313a3075343a676f6c6469306575323a696431363a6f177dabab873a4397f195fad7011e067531303a6974656d557361626c65647531303a62756666536b696c6c736c657531343a656c656d656e74616c5f7479706575343a4c616e6475363a65715f65787075313a3875383a65717569707065646675353a677261646575313a3175323a696475383a313034313330303075363a6974656d496431363ab84219dad196ff44a8f53e9bf494a01b7531333a6974656d5f7375625f7479706575383a4e65636b6c61636575393a6974656d5f7479706575393a45717569706d656e7475353a6c6576656c75313a3075323a6f6375313a337531383a7265717569726564426c6f636b496e64657875383a313134393033323175363a7365745f696475313a3475363a736b696c6c736c657531393a7370696e655f7265736f757263655f7061746875383a313034313330303075343a737461746475343a74797065343a0500000075353a76616c756575333a3335376575383a73746174734d617064343a03000000647531353a6164646974696f6e616c56616c756575323a323375383a7374617454797065343a0300000075353a76616c756575313a3065343a05000000647531353a6164646974696f6e616c56616c756575333a31373275383a7374617454797065343a0500000075353a76616c756575333a33353765656575393a6d6174657269616c736c6475353a636f756e7475323a313575383a6d6174657269616c647531343a656c656d656e74616c5f7479706575363a4e6f726d616c75353a677261646575313a3175323a696475363a33303333303075373a6974656d5f696433323a1bd96c8d5bce17827b894e01d4eb87bd853bc6a48baee9e6151ce568ae43ad397531333a6974656d5f7375625f747970657531373a45717569706d656e744d6174657269616c75393a6974656d5f7479706575383a4d6174657269616c65656475353a636f756e7475323a323075383a6d6174657269616c647531343a656c656d656e74616c5f7479706575343a4c616e6475353a677261646575313a3275323a696475363a33303630303975373a6974656d5f696433323a67f6ff0b0e20202a8e63742adf804355ceaee3ab636ed3065bd535985e7109a87531333a6974656d5f7375625f747970657531313a4d6f6e737465725061727475393a6974656d5f7479706575383a4d6174657269616c65656475353a636f756e7475323a323975383a6d6174657269616c647531343a656c656d656e74616c5f7479706575343a4c616e6475353a677261646575313a3275323a696475363a33303630313075373a6974656d5f696433323ae0e69c7f4f16174438570e79ef52f5091ff761615a29dc70d3babbb4a099ce337531333a6974656d5f7375625f747970657531313a4d6f6e737465725061727475393a6974656d5f7479706575383a4d6174657269616c65656475353a636f756e7475323a323075383a6d6174657269616c647531343a656c656d656e74616c5f7479706575343a4c616e6475353a677261646575313a3275323a696475363a33303630313175373a6974656d5f696433323a2d85afe3aa2e0ebc49df0149cf939feeddbfb9049a1e0c3bd7d8c00a948fece67531333a6974656d5f7375625f747970657531313a4d6f6e737465725061727475393a6974656d5f7479706575383a4d6174657269616c65656575383a726563697065496475333a3131317531313a737562526563697065496475333a32363075363a7479706549647532343a636f6d62696e6174696f6e2e726573756c742d6d6f64656c657531303a626c6f636b496e64657875383a313134383935393775323a696431363a6f177dabab873a4397f195fad7011e067531383a7265717569726564426c6f636b496e64657875383a313134393033323175363a7479706549647531353a636f6d62696e6174696f6e4d61696c65647531303a6174746163686d656e74647531313a616374696f6e506f696e7475313a3075343a676f6c6469306575323a696431363af3c9f6b3f36aa0488c73dc597a06b36c7531303a6974656d557361626c65647531303a62756666536b696c6c736c657531343a656c656d656e74616c5f7479706575343a4c616e6475363a65715f65787075313a3875383a65717569707065646675353a677261646575313a3175323a696475383a313034313330303075363a6974656d496431363a788c0c286da7c14abde5ded62de0f44d7531333a6974656d5f7375625f7479706575383a4e65636b6c61636575393a6974656d5f7479706575393a45717569706d656e7475353a6c6576656c75313a3075323a6f6375313a337531383a7265717569726564426c6f636b496e64657875383a313134383936323075363a7365745f696475313a3475363a736b696c6c736c6475363a6368616e636575313a3875353a706f77657275313a3075383a736b696c6c526f776475353a636f6d626f6675383a636f6f6c646f776e693235657531343a656c656d656e74616c5f7479706575363a4e6f726d616c75393a6869745f636f756e7469316575323a696469323430303030657531343a736b696c6c5f63617465676f727975373a486974427566667531373a736b696c6c5f7461726765745f7479706575343a53656c667531303a736b696c6c5f7479706575343a427566666565657531393a7370696e655f7265736f757263655f7061746875383a313034313330303075343a737461746475343a74797065343a0500000075353a76616c756575333a3335376575383a73746174734d617064343a03000000647531353a6164646974696f6e616c56616c756575323a333175383a7374617454797065343a0300000075353a76616c756575313a3065343a05000000647531353a6164646974696f6e616c56616c756575333a31303675383a7374617454797065343a0500000075353a76616c756575333a33353765656575393a6d6174657269616c736c6475353a636f756e7475323a313575383a6d6174657269616c647531343a656c656d656e74616c5f7479706575363a4e6f726d616c75353a677261646575313a3175323a696475363a33303333303075373a6974656d5f696433323a1bd96c8d5bce17827b894e01d4eb87bd853bc6a48baee9e6151ce568ae43ad397531333a6974656d5f7375625f747970657531373a45717569706d656e744d6174657269616c75393a6974656d5f7479706575383a4d6174657269616c65656475353a636f756e7475323a323075383a6d6174657269616c647531343a656c656d656e74616c5f7479706575343a4c616e6475353a677261646575313a3275323a696475363a33303630303975373a6974656d5f696433323a67f6ff0b0e20202a8e63742adf804355ceaee3ab636ed3065bd535985e7109a87531333a6974656d5f7375625f747970657531313a4d6f6e737465725061727475393a6974656d5f7479706575383a4d6174657269616c65656475353a636f756e7475323a323975383a6d6174657269616c647531343a656c656d656e74616c5f7479706575343a4c616e6475353a677261646575313a3275323a696475363a33303630313075373a6974656d5f696433323ae0e69c7f4f16174438570e79ef52f5091ff761615a29dc70d3babbb4a099ce337531333a6974656d5f7375625f747970657531313a4d6f6e737465725061727475393a6974656d5f7479706575383a4d6174657269616c65656475353a636f756e7475323a323075383a6d6174657269616c647531343a656c656d656e74616c5f7479706575343a4c616e6475353a677261646575313a3275323a696475363a33303630313175373a6974656d5f696433323a2d85afe3aa2e0ebc49df0149cf939feeddbfb9049a1e0c3bd7d8c00a948fece67531333a6974656d5f7375625f747970657531313a4d6f6e737465725061727475393a6974656d5f7479706575383a4d6174657269616c65656575383a726563697065496475333a3131317531313a737562526563697065496475333a32363075363a7479706549647532343a636f6d62696e6174696f6e2e726573756c742d6d6f64656c657531303a626c6f636b496e64657875383a313134383936303075323a696431363af3c9f6b3f36aa0488c73dc597a06b36c7531383a7265717569726564426c6f636b496e64657875383a313134383936323075363a7479706549647531353a636f6d62696e6174696f6e4d61696c65647531303a6174746163686d656e74647531313a616374696f6e506f696e7475313a3075313a636c647531333a646563696d616c506c61636573313a1275373a6d696e746572736e75363a7469636b657275373a4352595354414c65693065657531373a656e68616e63656d656e74526573756c7475373a5375636365737375343a676f6c6469306575323a696431363a360225bce61f584cb67a5f45441797eb7531303a6974656d557361626c65647531303a62756666536b696c6c736c657531343a656c656d656e74616c5f7479706575343a57696e6475363a65715f65787075333a36343075383a65717569707065646675353a677261646575313a3275323a696475383a313031323430303075363a6974656d496431363abb94ea9d0a698e4581e9e177e9efbd017531333a6974656d5f7375625f7479706575363a576561706f6e75393a6974656d5f7479706575393a45717569706d656e7475353a6c6576656c75313a3275323a6f6375313a337531383a7265717569726564426c6f636b496e64657875383a313134383134353275363a7365745f696475323a313075363a736b696c6c736c6475363a6368616e636575323a323875353a706f77657275343a3336353775383a736b696c6c526f776475353a636f6d626f6675383a636f6f6c646f776e6933657531343a656c656d656e74616c5f7479706575343a57696e6475393a6869745f636f756e7469326575323a696469313430303033657531343a736b696c6c5f63617465676f72797531323a446f75626c6541747461636b7531373a736b696c6c5f7461726765745f7479706575353a456e656d797531303a736b696c6c5f7479706575363a41747461636b6565657531393a7370696e655f7265736f757263655f7061746875383a313031323430303075343a737461746475343a74797065343a0200000075353a76616c756575333a3332376575383a73746174734d617064343a02000000647531353a6164646974696f6e616c56616c75657531323a3131372e303030303030303075383a7374617454797065343a0200000075353a76616c756575383a3339302e3533373665343a06000000647531353a6164646974696f6e616c56616c75657531323a3439382e303030303030303075383a7374617454797065343a0600000075353a76616c756575313a306565657531383a6d6174657269616c4974656d49644c6973746c31363aa30c9f43ac29414abdc8e2e2cac3a6e131363ac2746f747d8e964791104ce99ccc779931363a71acadf8602fbf4ab80c58f1c1329c33657531333a7072654974656d557361626c65647531303a62756666536b696c6c736c657531343a656c656d656e74616c5f7479706575343a57696e6475363a65715f65787075333a31383075383a65717569707065646675353a677261646575313a3275323a696475383a313031323430303075363a6974656d496431363abb94ea9d0a698e4581e9e177e9efbd017531333a6974656d5f7375625f7479706575363a576561706f6e75393a6974656d5f7479706575393a45717569706d656e7475353a6c6576656c75313a3075323a6f6375313a337531383a7265717569726564426c6f636b496e64657875383a313134363938333075363a7365745f696475323a313075363a736b696c6c736c6475363a6368616e636575323a323875353a706f77657275343a3336353775383a736b696c6c526f776475353a636f6d626f6675383a636f6f6c646f776e6933657531343a656c656d656e74616c5f7479706575343a57696e6475393a6869745f636f756e7469326575323a696469313430303033657531343a736b696c6c5f63617465676f72797531323a446f75626c6541747461636b7531373a736b696c6c5f7461726765745f7479706575353a456e656d797531303a736b696c6c5f7479706575363a41747461636b6565657531393a7370696e655f7265736f757263655f7061746875383a313031323430303075343a737461746475343a74797065343a0200000075353a76616c756575333a3332376575383a73746174734d617064343a02000000647531353a6164646974696f6e616c56616c756575333a31313775383a7374617454797065343a0200000075353a76616c756575333a33323765343a06000000647531353a6164646974696f6e616c56616c756575333a34393875383a7374617454797065343a0600000075353a76616c756575313a3065656575363a7479706549647532353a6974656d5f656e68616e63656d656e7431332e726573756c74657531303a626c6f636b496e64657875383a313134383133303275323a696431363a360225bce61f584cb67a5f45441797eb7531383a7265717569726564426c6f636b496e64657875383a313134383134353275363a7479706549647531313a6974656d456e68616e636565647531303a6174746163686d656e74647531313a616374696f6e506f696e7475313a3075343a676f6c6469306575323a696431363a83ca3ec0f936834b8e68d82f1a74023e7531303a6974656d557361626c65647531303a62756666536b696c6c736c657531343a656c656d656e74616c5f7479706575343a57696e6475363a65715f65787075323a313075383a65717569707065646675353a677261646575313a3175323a696475383a313035313430303075363a6974656d496431363a171b65986137df448b463fd62824e4997531333a6974656d5f7375625f7479706575343a52696e6775393a6974656d5f7479706575393a45717569706d656e7475353a6c6576656c75313a3075323a6f6375313a337531383a7265717569726564426c6f636b496e64657875383a313135303032313975363a7365745f696475313a3575363a736b696c6c736c657531393a7370696e655f7265736f757263655f7061746875383a313035313430303075343a737461746475343a74797065343a0300000075353a76616c756575333a3130376575383a73746174734d617064343a03000000647531353a6164646974696f6e616c56616c756575323a393475383a7374617454797065343a0300000075353a76616c756575333a31303765343a06000000647531353a6164646974696f6e616c56616c756575333a33373475383a7374617454797065343a0600000075353a76616c756575313a3065656575393a6d6174657269616c736c6475353a636f756e7475323a323475383a6d6174657269616c647531343a656c656d656e74616c5f7479706575363a4e6f726d616c75353a677261646575313a3175323a696475363a33303334303075373a6974656d5f696433323a45c8a87009caf8f9d3ecf58618fa6e2b14fe0edb2283d373e3c190c56eb96d8a7531333a6974656d5f7375625f747970657531373a45717569706d656e744d6174657269616c75393a6974656d5f7479706575383a4d6174657269616c65656475353a636f756e7475323a313275383a6d6174657269616c647531343a656c656d656e74616c5f7479706575343a57696e6475353a677261646575313a3175323a696475363a33303630343275373a6974656d5f696433323a31f48810fa91236281c165659d65003cfb611205a52c1ff6e02c019b86625dda7531333a6974656d5f7375625f747970657531313a4d6f6e737465725061727475393a6974656d5f7479706575383a4d6174657269616c65656475353a636f756e7475323a343775383a6d6174657269616c647531343a656c656d656e74616c5f7479706575343a57696e6475353a677261646575313a3275323a696475363a33303630353075373a6974656d5f696433323adf0c212eb56526891629367fe889cfce0ea89a04e21d11a05b19ff9231802b4c7531333a6974656d5f7375625f747970657531313a4d6f6e737465725061727475393a6974656d5f7479706575383a4d6174657269616c65656475353a636f756e7475323a373075383a6d6174657269616c647531343a656c656d656e74616c5f7479706575343a57696e6475353a677261646575313a3275323a696475363a33303630353175373a6974656d5f696433323a5eac6f2b799a1fc20a50f005f2df8783fefec63595787804b4418ece2b3d07177531333a6974656d5f7375625f747970657531313a4d6f6e737465725061727475393a6974656d5f7479706575383a4d6174657269616c65656575383a726563697065496475333a3133377531313a737562526563697065496475333a33323375363a7479706549647532343a636f6d62696e6174696f6e2e726573756c742d6d6f64656c657531303a626c6f636b496e64657875383a313134383937363275323a696431363a83ca3ec0f936834b8e68d82f1a74023e7531383a7265717569726564426c6f636b496e64657875383a313135303032313975363a7479706549647531353a636f6d62696e6174696f6e4d61696c65647531303a6174746163686d656e74647531313a616374696f6e506f696e7475313a3075343a676f6c6469306575323a696431363a410bdbc1e7784944a326a7c33283949c7531303a6974656d557361626c65647531303a62756666536b696c6c736c657531343a656c656d656e74616c5f7479706575343a4c616e6475363a65715f65787075313a3875383a65717569707065646675353a677261646575313a3175323a696475383a313034313330303075363a6974656d496431363a11eaf70e63890447894dacb39c8402d07531333a6974656d5f7375625f7479706575383a4e65636b6c61636575393a6974656d5f7479706575393a45717569706d656e7475353a6c6576656c75313a3075323a6f6375313a327531383a7265717569726564426c6f636b496e64657875383a313134383139313675363a7365745f696475313a3475363a736b696c6c736c657531393a7370696e655f7265736f757263655f7061746875383a313034313330303075343a737461746475343a74797065343a0500000075353a76616c756575333a3335376575383a73746174734d617064343a03000000647531353a6164646974696f6e616c56616c756575323a323875383a7374617454797065343a0300000075353a76616c756575313a3065343a05000000647531353a6164646974696f6e616c56616c756575333a31313075383a7374617454797065343a0500000075353a76616c756575333a33353765656575393a6d6174657269616c736c6475353a636f756e7475323a313575383a6d6174657269616c647531343a656c656d656e74616c5f7479706575363a4e6f726d616c75353a677261646575313a3175323a696475363a33303333303075373a6974656d5f696433323a1bd96c8d5bce17827b894e01d4eb87bd853bc6a48baee9e6151ce568ae43ad397531333a6974656d5f7375625f747970657531373a45717569706d656e744d6174657269616c75393a6974656d5f7479706575383a4d6174657269616c65656475353a636f756e7475323a323075383a6d6174657269616c647531343a656c656d656e74616c5f7479706575343a4c616e6475353a677261646575313a3275323a696475363a33303630303975373a6974656d5f696433323a67f6ff0b0e20202a8e63742adf804355ceaee3ab636ed3065bd535985e7109a87531333a6974656d5f7375625f747970657531313a4d6f6e737465725061727475393a6974656d5f7479706575383a4d6174657269616c65656475353a636f756e7475323a323975383a6d6174657269616c647531343a656c656d656e74616c5f7479706575343a4c616e6475353a677261646575313a3275323a696475363a33303630313075373a6974656d5f696433323ae0e69c7f4f16174438570e79ef52f5091ff761615a29dc70d3babbb4a099ce337531333a6974656d5f7375625f747970657531313a4d6f6e737465725061727475393a6974656d5f7479706575383a4d6174657269616c65656475353a636f756e7475323a323075383a6d6174657269616c647531343a656c656d656e74616c5f7479706575343a4c616e6475353a677261646575313a3275323a696475363a33303630313175373a6974656d5f696433323a2d85afe3aa2e0ebc49df0149cf939feeddbfb9049a1e0c3bd7d8c00a948fece67531333a6974656d5f7375625f747970657531313a4d6f6e737465725061727475393a6974656d5f7479706575383a4d6174657269616c65656575383a726563697065496475333a3131317531313a737562526563697065496475333a32363075363a7479706549647532343a636f6d62696e6174696f6e2e726573756c742d6d6f64656c657531303a626c6f636b496e64657875383a313134383133303775323a696431363a410bdbc1e7784944a326a7c33283949c7531383a7265717569726564426c6f636b496e64657875383a313134383139313675363a7479706549647531353a636f6d62696e6174696f6e4d61696c65647531303a6174746163686d656e74647531313a616374696f6e506f696e7475313a3075343a676f6c6469306575323a696431363a70fe8ac41b84c746859508c0892a6c907531303a6974656d557361626c65647531303a62756666536b696c6c736c657531343a656c656d656e74616c5f7479706575363a4e6f726d616c75363a65715f65787075313a3575383a65717569707065646675353a677261646575313a3175323a696475383a313035313030303075363a6974656d496431363aa0490f2ce8a1cd47ad9d579a9e9304a07531333a6974656d5f7375625f7479706575343a52696e6775393a6974656d5f7479706575393a45717569706d656e7475353a6c6576656c75313a3075323a6f6375313a337531383a7265717569726564426c6f636b496e64657875383a313134383334363175363a7365745f696475313a3175363a736b696c6c736c657531393a7370696e655f7265736f757263655f7061746875383a313035313030303075343a737461746475343a74797065343a0300000075353a76616c756575313a386575383a73746174734d617064343a02000000647531353a6164646974696f6e616c56616c756575313a3775383a7374617454797065343a0200000075353a76616c756575313a3065343a03000000647531353a6164646974696f6e616c56616c756575313a3675383a7374617454797065343a0300000075353a76616c756575313a3865656575393a6d6174657269616c736c6475353a636f756e7475313a3275383a6d6174657269616c647531343a656c656d656e74616c5f7479706575363a4e6f726d616c75353a677261646575313a3175323a696475363a33303334303075373a6974656d5f696433323a45c8a87009caf8f9d3ecf58618fa6e2b14fe0edb2283d373e3c190c56eb96d8a7531333a6974656d5f7375625f747970657531373a45717569706d656e744d6174657269616c75393a6974656d5f7479706575383a4d6174657269616c65656475353a636f756e7475313a3275383a6d6174657269616c647531343a656c656d656e74616c5f7479706575353a576174657275353a677261646575313a3175323a696475363a33303630303175373a6974656d5f696433323a77cfad5273885820b69e61c2106ae06583f69c7f6d82a229b59e5f1b984164be7531333a6974656d5f7375625f747970657531313a4d6f6e737465725061727475393a6974656d5f7479706575383a4d6174657269616c65656475353a636f756e7475313a3275383a6d6174657269616c647531343a656c656d656e74616c5f7479706575353a576174657275353a677261646575313a3175323a696475363a33303630303275373a6974656d5f696433323a28a2c38b0934225a8f2327b9bd8cf7c7eaf3551bde8203f44248c011938e97707531333a6974656d5f7375625f747970657531313a4d6f6e737465725061727475393a6974656d5f7479706575383a4d6174657269616c65656475353a636f756e7475313a3175383a6d6174657269616c647531343a656c656d656e74616c5f7479706575343a4669726575353a677261646575313a3175323a696475363a33303630323375373a6974656d5f696433323a1af5e747fc81a7f3149af024db2e5025b6749a000d66b22e665acdd45b8dbd177531333a6974656d5f7375625f747970657531313a4d6f6e737465725061727475393a6974656d5f7479706575383a4d6174657269616c65656575383a726563697065496475333a3133337531313a737562526563697065496475333a34353875363a7479706549647532343a636f6d62696e6174696f6e2e726573756c742d6d6f64656c657531303a626c6f636b496e64657875383a313134383334353375323a696431363a70fe8ac41b84c746859508c0892a6c907531383a7265717569726564426c6f636b496e64657875383a313134383334363175363a7479706549647531353a636f6d62696e6174696f6e4d61696c65647531303a6174746163686d656e74647531313a616374696f6e506f696e7475313a3075343a676f6c6469306575323a696431363ac11851cdf4576d49a113d7d756ed35897531303a6974656d557361626c65647531303a62756666536b696c6c736c657531343a656c656d656e74616c5f7479706575343a4c616e6475363a65715f65787075313a3875383a65717569707065646675353a677261646575313a3175323a696475383a313034313330303075363a6974656d496431363a7f45c50e8c9cdc48b87b1c28af3559707531333a6974656d5f7375625f7479706575383a4e65636b6c61636575393a6974656d5f7479706575393a45717569706d656e7475353a6c6576656c75313a3075323a6f6375313a337531383a7265717569726564426c6f636b496e64657875383a313134383230333275363a7365745f696475313a3475363a736b696c6c736c657531393a7370696e655f7265736f757263655f7061746875383a313034313330303075343a737461746475343a74797065343a0500000075353a76616c756575333a3335376575383a73746174734d617064343a03000000647531353a6164646974696f6e616c56616c756575323a323875383a7374617454797065343a0300000075353a76616c756575313a3065343a05000000647531353a6164646974696f6e616c56616c756575333a31383475383a7374617454797065343a0500000075353a76616c756575333a33353765656575393a6d6174657269616c736c6475353a636f756e7475323a313575383a6d6174657269616c647531343a656c656d656e74616c5f7479706575363a4e6f726d616c75353a677261646575313a3175323a696475363a33303333303075373a6974656d5f696433323a1bd96c8d5bce17827b894e01d4eb87bd853bc6a48baee9e6151ce568ae43ad397531333a6974656d5f7375625f747970657531373a45717569706d656e744d6174657269616c75393a6974656d5f7479706575383a4d6174657269616c65656475353a636f756e7475323a323075383a6d6174657269616c647531343a656c656d656e74616c5f7479706575343a4c616e6475353a677261646575313a3275323a696475363a33303630303975373a6974656d5f696433323a67f6ff0b0e20202a8e63742adf804355ceaee3ab636ed3065bd535985e7109a87531333a6974656d5f7375625f747970657531313a4d6f6e737465725061727475393a6974656d5f7479706575383a4d6174657269616c65656475353a636f756e7475323a323975383a6d6174657269616c647531343a656c656d656e74616c5f7479706575343a4c616e6475353a677261646575313a3275323a696475363a33303630313075373a6974656d5f696433323ae0e69c7f4f16174438570e79ef52f5091ff761615a29dc70d3babbb4a099ce337531333a6974656d5f7375625f747970657531313a4d6f6e737465725061727475393a6974656d5f7479706575383a4d6174657269616c65656475353a636f756e7475323a323075383a6d6174657269616c647531343a656c656d656e74616c5f7479706575343a4c616e6475353a677261646575313a3275323a696475363a33303630313175373a6974656d5f696433323a2d85afe3aa2e0ebc49df0149cf939feeddbfb9049a1e0c3bd7d8c00a948fece67531333a6974656d5f7375625f747970657531313a4d6f6e737465725061727475393a6974656d5f7479706575383a4d6174657269616c65656575383a726563697065496475333a3131317531313a737562526563697065496475333a32363075363a7479706549647532343a636f6d62696e6174696f6e2e726573756c742d6d6f64656c657531303a626c6f636b496e64657875383a313134383133303875323a696431363ac11851cdf4576d49a113d7d756ed35897531383a7265717569726564426c6f636b496e64657875383a313134383230333275363a7479706549647531353a636f6d62696e6174696f6e4d61696c65647531303a6174746163686d656e74647531313a616374696f6e506f696e7475313a3075343a676f6c6469306575323a696431363ab21cf1d3f2538e4c9496080cb00216657531303a6974656d557361626c65647531303a62756666536b696c6c736c657531343a656c656d656e74616c5f7479706575343a4c616e6475363a65715f65787075313a3875383a65717569707065646675353a677261646575313a3175323a696475383a313034313330303075363a6974656d496431363ad5f36402d57e214691607fa35606e5297531333a6974656d5f7375625f7479706575383a4e65636b6c61636575393a6974656d5f7479706575393a45717569706d656e7475353a6c6576656c75313a3075323a6f6375313a327531383a7265717569726564426c6f636b496e64657875383a313134383139313575363a7365745f696475313a3475363a736b696c6c736c657531393a7370696e655f7265736f757263655f7061746875383a313034313330303075343a737461746475343a74797065343a0500000075353a76616c756575333a3335376575383a73746174734d617064343a03000000647531353a6164646974696f6e616c56616c756575323a333075383a7374617454797065343a0300000075353a76616c756575313a3065343a05000000647531353a6164646974696f6e616c56616c756575333a31313675383a7374617454797065343a0500000075353a76616c756575333a33353765656575393a6d6174657269616c736c6475353a636f756e7475323a313575383a6d6174657269616c647531343a656c656d656e74616c5f7479706575363a4e6f726d616c75353a677261646575313a3175323a696475363a33303333303075373a6974656d5f696433323a1bd96c8d5bce17827b894e01d4eb87bd853bc6a48baee9e6151ce568ae43ad397531333a6974656d5f7375625f747970657531373a45717569706d656e744d6174657269616c75393a6974656d5f7479706575383a4d6174657269616c65656475353a636f756e7475323a323075383a6d6174657269616c647531343a656c656d656e74616c5f7479706575343a4c616e6475353a677261646575313a3275323a696475363a33303630303975373a6974656d5f696433323a67f6ff0b0e20202a8e63742adf804355ceaee3ab636ed3065bd535985e7109a87531333a6974656d5f7375625f747970657531313a4d6f6e737465725061727475393a6974656d5f7479706575383a4d6174657269616c65656475353a636f756e7475323a323975383a6d6174657269616c647531343a656c656d656e74616c5f7479706575343a4c616e6475353a677261646575313a3275323a696475363a33303630313075373a6974656d5f696433323ae0e69c7f4f16174438570e79ef52f5091ff761615a29dc70d3babbb4a099ce337531333a6974656d5f7375625f747970657531313a4d6f6e737465725061727475393a6974656d5f7479706575383a4d6174657269616c65656475353a636f756e7475323a323075383a6d6174657269616c647531343a656c656d656e74616c5f7479706575343a4c616e6475353a677261646575313a3275323a696475363a33303630313175373a6974656d5f696433323a2d85afe3aa2e0ebc49df0149cf939feeddbfb9049a1e0c3bd7d8c00a948fece67531333a6974656d5f7375625f747970657531313a4d6f6e737465725061727475393a6974656d5f7479706575383a4d6174657269616c65656575383a726563697065496475333a3131317531313a737562526563697065496475333a32363075363a7479706549647532343a636f6d62696e6174696f6e2e726573756c742d6d6f64656c657531303a626c6f636b496e64657875383a313134383133303675323a696431363ab21cf1d3f2538e4c9496080cb00216657531383a7265717569726564426c6f636b496e64657875383a313134383139313575363a7479706549647531353a636f6d62696e6174696f6e4d61696c65647531303a6174746163686d656e74647531313a616374696f6e506f696e7475313a3075343a676f6c6469306575323a696431363a885a5cdfbb73604e99edf84bb44705387531303a6974656d557361626c65647531303a62756666536b696c6c736c657531343a656c656d656e74616c5f7479706575343a57696e6475363a65715f65787075323a313075383a65717569707065646675353a677261646575313a3175323a696475383a313035313430303075363a6974656d496431363af48685a80203184fb5b78808f005198e7531333a6974656d5f7375625f7479706575343a52696e6775393a6974656d5f7479706575393a45717569706d656e7475353a6c6576656c75313a3075323a6f6375313a327531383a7265717569726564426c6f636b496e64657875383a313134393138323175363a7365745f696475313a3575363a736b696c6c736c657531393a7370696e655f7265736f757263655f7061746875383a313035313430303075343a737461746475343a74797065343a0300000075353a76616c756575333a3130376575383a73746174734d617064343a03000000647531353a6164646974696f6e616c56616c756575323a343775383a7374617454797065343a0300000075353a76616c756575333a31303765343a06000000647531353a6164646974696f6e616c56616c756575333a34343275383a7374617454797065343a0600000075353a76616c756575313a3065656575393a6d6174657269616c736c6475353a636f756e7475323a323475383a6d6174657269616c647531343a656c656d656e74616c5f7479706575363a4e6f726d616c75353a677261646575313a3175323a696475363a33303334303075373a6974656d5f696433323a45c8a87009caf8f9d3ecf58618fa6e2b14fe0edb2283d373e3c190c56eb96d8a7531333a6974656d5f7375625f747970657531373a45717569706d656e744d6174657269616c75393a6974656d5f7479706575383a4d6174657269616c65656475353a636f756e7475323a313275383a6d6174657269616c647531343a656c656d656e74616c5f7479706575343a57696e6475353a677261646575313a3175323a696475363a33303630343275373a6974656d5f696433323a31f48810fa91236281c165659d65003cfb611205a52c1ff6e02c019b86625dda7531333a6974656d5f7375625f747970657531313a4d6f6e737465725061727475393a6974656d5f7479706575383a4d6174657269616c65656475353a636f756e7475323a343775383a6d6174657269616c647531343a656c656d656e74616c5f7479706575343a57696e6475353a677261646575313a3275323a696475363a33303630353075373a6974656d5f696433323adf0c212eb56526891629367fe889cfce0ea89a04e21d11a05b19ff9231802b4c7531333a6974656d5f7375625f747970657531313a4d6f6e737465725061727475393a6974656d5f7479706575383a4d6174657269616c65656475353a636f756e7475323a373075383a6d6174657269616c647531343a656c656d656e74616c5f7479706575343a57696e6475353a677261646575313a3275323a696475363a33303630353175373a6974656d5f696433323a5eac6f2b799a1fc20a50f005f2df8783fefec63595787804b4418ece2b3d07177531333a6974656d5f7375625f747970657531313a4d6f6e737465725061727475393a6974656d5f7479706575383a4d6174657269616c65656575383a726563697065496475333a3133377531313a737562526563697065496475333a33323375363a7479706549647532343a636f6d62696e6174696f6e2e726573756c742d6d6f64656c657531303a626c6f636b496e64657875383a313134383334353575323a696431363a885a5cdfbb73604e99edf84bb44705387531383a7265717569726564426c6f636b496e64657875383a313134393138323175363a7479706549647531353a636f6d62696e6174696f6e4d61696c6565693131343932333234656939383832333735656930656475313a3175313a3175323a313075313a3175333a31303075333a32303375333a31303175313a3175333a31303275333a31343875333a31303375323a313175333a31303475313a3575333a31303575313a3275333a31303675313a3175333a31303775313a3275333a31303875313a3175333a31303975323a313075323a313175313a3175333a31313075323a313175333a31313175313a3975333a31313275313a3175333a31313375313a3175333a31313475313a3175333a31313575313a3175333a31313675313a3175333a31313775313a3175333a31313875313a3175333a31313975313a3175323a313275313a3175333a31323075313a3175333a31323175313a3175333a31323275313a3175333a31323375313a3175333a31323475313a3175333a31323575313a3175333a31323675313a3175333a31323775313a3175333a31323875313a3175333a31323975313a3175323a313375313a3175333a31333075313a3175333a31333175313a3175333a31333275313a3175333a31333375313a3175333a31333475313a3175333a31333575313a3175333a31333675313a3175333a31333775313a3175333a31333875313a3175333a31333975313a3175323a313475313a3175333a31343075313a3175333a31343175313a3175333a31343275313a3175333a31343375313a3175333a31343475313a3175333a31343575313a3175333a31343675313a3175333a31343775313a3175333a31343875313a3275333a31343975313a3475323a313575323a313275333a31353075323a333475333a31353175343a3130373275333a31353275313a3175333a31353375313a3275333a31353475313a3175333a31353575313a3175333a31353675323a313175333a31353775313a3275333a31353875313a3175333a31353975323a313975323a313675313a3175333a31363075313a3175333a31363175313a3275333a31363275323a313875333a31363375313a3175333a31363475313a3175333a31363575313a3175333a31363675313a3175333a31363775323a313575333a31363875313a3375333a31363975313a3275323a313775313a3175333a31373075323a313275333a31373175323a333275333a31373275313a3175333a31373375333a37383775333a31373475333a31373375333a31373575333a32303475333a31373675313a3775333a31373775313a3775333a31373875313a3275333a31373975313a3175323a313875313a3675333a31383075323a313075333a31383175323a313875333a31383275323a323575333a31383375313a3175333a31383475313a3175333a31383575313a3175333a31383675313a3175333a31383775313a3175333a31383875313a3175333a31383975313a3175323a313975313a3175333a31393075313a3175333a31393175313a3175333a31393275313a3175333a31393375313a3175333a31393475313a3175333a31393575323a323375333a31393675323a323475333a31393775313a3175333a31393875313a3175333a31393975313a3175313a3275313a3175323a323075313a3175333a32303075313a3275333a32303175313a3175333a32303275313a3175333a32303375313a3175333a32303475313a3175333a32303575313a3175333a32303675313a3175333a32303775313a3275333a32303875323a323175333a32303975313a3175323a323175313a3275333a32313075313a3175333a32313175313a3175333a32313275313a3175333a32313375313a3175333a32313475313a3175333a32313575313a3175333a32313675313a3175333a32313775323a313275333a32313875313a3175333a32313975313a3975323a323275313a3175333a32323075313a3375333a32323175313a3175333a32323275313a3175333a32323375313a3175333a32323475313a3175333a32323575313a3175333a32323675313a3175333a32323775313a3175333a32323875313a3175333a32323975313a3175323a323375313a3375333a32333075313a3275333a32333175313a3175333a32333275313a3175333a32333375313a3175333a32333475313a3175333a32333575313a3175333a32333675313a3175333a32333775313a3175333a32333875313a3175333a32333975313a3175323a323475313a3475333a32343075313a3175333a32343175313a3175333a32343275313a3175333a32343375313a3175333a32343475313a3175333a32343575313a3175333a32343675313a3175333a32343775313a3175333a32343875313a3175333a32343975313a3275323a323575313a3175333a32353075313a3175333a32353175313a3175333a32353275313a3175333a32353375313a3175333a32353475323a313675333a32353575313a3175333a32353675313a3175333a32353775313a3175333a32353875313a3175333a32353975313a3175323a323675313a3175333a32363075313a3175333a32363175313a3175333a32363275313a3175333a32363375313a3175333a32363475313a3175333a32363575313a3175333a32363675313a3175333a32363775313a3175333a32363875313a3175333a32363975313a3175323a323775323a343075333a32373075313a3175333a32373175313a3175333a32373275313a3175333a32373375313a3175333a32373475313a3175333a32373575313a3175333a32373675313a3175333a32373775313a3175333a32373875313a3175333a32373975313a3175323a323875323a333075333a32383075323a313275333a32383175313a3175333a32383275313a3175333a32383375313a3175333a32383475313a3175333a32383575313a3175333a32383675313a3175333a32383775313a3175333a32383875313a3175333a32383975323a333075323a323975313a3175333a32393075313a3175333a32393175313a3175333a32393275313a3175333a32393375313a3175333a32393475313a3175333a32393575323a313975333a32393675313a3775333a32393775313a3175333a32393875313a3775333a32393975313a3175313a3375313a3175323a333075313a3175333a33303075313a3875333a33303175313a3175333a33303275313a3175333a33303375313a3175333a33303475313a3175333a33303575313a3175333a33303675313a3175333a33303775313a3175333a33303875313a3175333a33303975313a3175323a333175313a3375333a33313075313a3175333a33313175313a3175333a33313275313a3175333a33313375313a3175333a33313475313a3175333a33313575313a3175333a33313675313a3175333a33313775313a3175333a33313875313a3175333a33313975313a3175323a333275313a3775333a33323075313a3175333a33323175313a3175333a33323275313a3175333a33323375313a3175333a33323475313a3175333a33323575313a3275333a33323675313a3175333a33323775313a3175333a33323875313a3175333a33323975313a3175323a333375313a3175333a33333075313a3175333a33333175313a3375333a33333275313a3175323a333475313a3375323a333575313a3175323a333675313a3875323a333775313a3375323a333875313a3975323a333975313a3175313a3475313a3175323a343075313a3175323a343175313a3175323a343275313a3175323a343375313a3375323a343475313a3275323a343575313a3175323a343675313a3175323a343775313a3175323a343875313a3175323a343975313a3275313a3575313a3175323a353075313a3175323a353175313a3675323a353275333a31373375323a353375333a34323175323a353475313a3375323a353575333a31353775323a353675313a3775323a353775323a323275323a353875313a3275323a353975313a3275313a3675313a3175323a363075313a3175323a363175313a3175323a363275313a3175323a363375333a31363775323a363475323a353675323a363575323a343175323a363675323a313775323a363775323a323075323a363875323a353775323a363975313a3875313a3775313a3175323a373075323a323275323a373175313a3875323a373275323a323775323a373375333a32313075323a373475323a383275323a373575333a31353275323a373675323a323875323a373775323a343575323a373875323a323175323a373975323a313575313a3875313a3175323a383075323a313475323a383175323a323275323a383275323a313175323a383375323a373275323a383475313a3775323a383575323a313475323a383675313a3675323a383775313a3375323a383875313a3275323a383975313a3275313a3975313a3275323a393075313a3275323a393175313a3275323a393275313a3275323a393375313a3175323a393475313a3175323a393575313a3275323a393675313a3175323a393775313a3975323a393875313a3475323a393975313a33656475363a32303130303075333a36333975363a32303130303175333a31383575363a32303130303275333a32343075363a32303130303375323a393875363a32303130303475333a31353775363a32303130303575313a3175363a32303130303675313a3775363a32303130303775323a323075363a32303130303875323a313575363a32303230303075343a3531373675363a32303230303175343a3432303375363a32303230303275343a3135393575363a32303230303375333a34343775363a32303230303475333a34353975363a32303230303575343a3133303875363a32303230303675323a323075363a32303230303775333a32303375363a32303330303075343a3138303675363a32303330303175333a33333275363a32303330303275333a33383375363a32303330303375323a353675363a32303330303475323a333975363a32303330303575323a323475363a32303330303675323a313075363a32303330303775323a333475363a32303430303075343a3530333075363a32303430303175333a33383775363a32303430303275343a3133363475363a32303430313075343a3237303475363a32303430313175333a39373775363a32303430313275343a3331373575363a32303430313375343a3433313475363a32303430323075343a3138333475363a32303430323175333a32343375363a32303430323275333a33323275363a32303430323375343a3133303275363a32303530303075343a3833353375363a32303530303175353a313038373775363a32303530303275343a3237363975363a32303530303375333a37303875363a32303530303475323a363075363a32303530303575333a38393475363a32303530303675323a333075363a32303530303775313a3275363a32303630303075343a3136383675363a32303630303175343a3135393775363a32303630303275333a36363775363a32303630303375333a31373175363a32303630303475333a31333675363a32303630303575323a373575363a32303630303675313a3675363a32303630303775313a3175363a32303730303075343a3135353875363a32303730303175343a3131343575363a32303730303275333a36343475363a32303730303375333a38393775363a32303730303475333a33353075363a32303730303575333a32313475363a32303730303675323a323375363a32303730303775313a3875363a32303830303075343a3231353375363a32303830303175343a3135373775363a32303830303275343a3233323775363a32303830303375333a35353275363a32303830303475333a33393375363a32303830303575313a32656475383a313031313030303075323a313375383a313031313130303075313a3375383a313031313230303075313a3275383a313031313330303075313a3175383a313031313430303075323a313275383a313031323030303075323a333475383a313031323130303075313a3775383a313031323230303075313a3875383a313031323330303075313a3175383a313031323430303075323a373475383a313031333030303075333a34333675383a313031333030303175323a313875383a313031333130303075313a3975383a313031333130303175313a3175383a313031333230303075313a3575383a313031333230303175323a313675383a313031333330303075323a313475383a313031333330303175323a313375383a313031333430303075313a3875383a313031333430303175313a3475383a313031343030303075313a3175383a313031343130303075323a313375383a313031343330303075313a3875383a313031353130303075313a3475383a313032313030303075313a3375383a313032313130303075323a313475383a313032313230303075323a313675383a313032313330303075313a3375383a313032313430303075313a3475383a313032323030303075333a31303975383a313032323130303075323a313175383a313032323230303075313a3775383a313032323330303075313a3175383a313032323430303075323a353875383a313032333030303075323a313675383a313032333030303175313a3375383a313032333130303075313a3175383a313032333130303175323a313475383a313032333230303075313a3175383a313032333230303175323a313775383a313032333330303075313a3375383a313032333330303175313a3975383a313032333430303075313a3575383a313032343130303075313a3375383a313032343330303075313a3475383a313032353230303175313a3475383a313033313030303075313a3175383a313033313130303075313a3375383a313033313230303075313a3375383a313033313330303075313a3775383a313033313430303075313a3675383a313033323030303075323a313075383a313033323130303075313a3275383a313033323230303075313a3375383a313033323330303075323a323475383a313033323430303075313a3175383a313033333030303075323a313075383a313033333130303075323a313675383a313033333230303075313a3475383a313033333330303075313a3475383a313033333430303075313a3375383a313033343030303075333a31333775383a313033343130303075313a3775383a313033343330303075313a3175383a313033343430303075313a3175383a313033353130303175313a3475383a313033353330303075313a3275383a313033353430303075313a3175383a313033353430303175313a3275383a313034313030303075313a3175383a313034313130303075313a3775383a313034313230303075313a3275383a313034313330303075323a313075383a313034313430303075313a3175383a313034323030303075323a383775383a313034323130303075323a313175383a313034323230303075313a3775383a313034323330303075323a323575383a313034333030303075323a383375383a313034333130303075313a3175383a313034333230303075323a313875383a313034333330303075313a3875383a313034343030303075313a3275383a313034343430303075313a3175383a313034353130303075323a313075383a313034353130303175313a3275383a313034353230303175313a3175383a313034353330303075313a3175383a313034353430303075313a3175383a313035313030303075323a313775383a313035313130303075313a3375383a313035313230303075313a3275383a313035313330303075313a3175383a313035313430303075323a353875383a313035323030303075323a323975383a313035323130303075323a313075383a313035323230303075323a313275383a313035323330303075313a3875383a313035323430303075323a313575383a313035333030303075333a31333375383a313035333130303075313a3775383a313035333230303075323a313275383a313035333330303075313a3875383a313035333430303075313a3375383a313035343030303075313a3175383a313035343330303075313a3175383a313035343430303075323a313675383a313035353130303075313a3275383a313036313030303075333a34383375383a313036323030303075333a32343275383a313036323030303175323a323675383a313036323030303275323a323075383a313036323030303375323a313675383a313036333030303075323a323975383a313036333030303175313a3775383a313036333030303275313a3975383a313036333030303375323a313075383a313036343030303275313a3175383a313230303130303175323a323475383a313230303130303275323a313275383a313230303130303375313a3675363a32303130303075313a3975363a32303130303175323a363775363a32303130303275313a3475363a32303130303375323a313075363a32303130303575323a313375363a32303130303675313a3675363a32303130303875313a3875363a32303130303975313a3775363a32303130313075313a3975363a32303130313175313a3475363a32303130313275313a3375363a32303130313375313a3275363a32303130313575323a343275363a32303130313675313a3575363a32303130313875323a373475363a32303130313975313a3575363a32303130323175323a383775363a32303130323275323a323575363a32303130323375313a3575363a32303130323475313a3775363a32303130323575323a343675363a32303130323675313a3575363a32303130323775313a3775363a33303230303075323a323375363a33303230303175323a313575363a33303230303275323a313675363a33303230303375323a343975363a33303230303475323a323875363a33303230303575323a313775363a33303230303675323a313175363a33303230303875323a313875363a33303230303975323a313075363a33303330303075323a373675363a33303330303175333a35373175363a33303330303275343a3230373575363a33303330303375333a32373775363a33303330303475333a31383675363a33303331303075323a323475363a33303331303175333a34323775363a33303331303275333a38393375363a33303331303375323a393975363a33303331303475313a3775363a33303332303075323a343275363a33303332303175333a32313575363a33303332303275343a3135303575363a33303332303375333a31363275363a33303332303475333a31323975363a33303333303075323a343175363a33303333303175333a33373675363a33303333303275333a35363675363a33303333303375323a353975363a33303333303475323a343775363a33303334303075333a32373375363a33303334303175333a33333775363a33303334303275343a3136363575363a33303334303375333a32313075363a33303334303475323a333475363a33303430303075313a3175363a33303430303175313a3175363a33303430303275313a3175363a33303430303375313a3175363a33303630303075333a31343775363a33303630303175333a31313075363a33303630303275323a373775363a33303630303375333a31343575363a33303630303475333a31353975363a33303630303575323a373075363a33303630303675333a31383775363a33303630303775323a393775363a33303630303875343a3130393475363a33303630303975333a31343975363a33303630313075333a33393675363a33303630313175323a363775363a33303630313275333a33333275363a33303630313375333a33303975363a33303630313475333a31343575363a33303630313575343a3138303275363a33303630313675333a31303575363a33303630313775333a31303175363a33303630323375333a33303175363a33303630323475333a32313775363a33303630323575333a31343175363a33303630323675333a33353475363a33303630323775333a32313675363a33303630323875333a31363075363a33303630323975333a34383375363a33303630333075333a32313275363a33303630333175323a353275363a33303630333575343a3135333575363a33303630333675333a31373375363a33303630333775333a37313775363a33303630333875333a32373175363a33303630333975333a32353975363a33303630343075343a3334363975363a33303630343175333a38303375363a33303630343275343a3232333375363a33303630343375333a35373575363a33303630343475333a34303075363a33303630343575323a393475363a33303630343675323a323375363a33303630343775313a3275363a33303630343875323a313675363a33303630343975313a3975363a33303630353075343a3136323575363a33303630353175343a3132383175363a33303630353275333a36353975363a33303630353375333a37323275363a33303630353475333a36353375363a33303630353575333a34313475363a33303630373075333a31303675363a33303630373275313a3175363a33303630373375323a343175363a33303630373475313a3575363a33303630373575313a3675363a33303630373675313a3575363a33303630373775313a3475363a33303630373875313a3875363a33303630373975323a373875363a33303630383075333a37393775363a33303630383175333a33313575363a34303030303075373a3132303738343075363a35303030303075343a3635363875363a36303031303175313a3275363a36303031303275313a33656475313a3075313a3175313a3175333a34343775313a3275333a32333475313a3375343a3438393375313a3475333a35323975313a3575343a3231303875313a3675333a343530656931656931656939656937656c32303a25ff1be7d26b566a17a406ae8825de3a52034b1132303a68f8b334d7429661c95cb2283628c14da722be7d32303a74573255a354a272c5650b58645427a00ce7e1f132303af69af3454d83ea6f512a8398aac66e2c17ec07ff6532303aae75ca68c5bb8583c379348de5e562933d2b4cee65")));
            avatarState.inventory = new Inventory();
            avatarState.worldInformation = new WorldInformation((Dictionary)new Codec().Decode(Convert.FromHexString(
                "6475313a316475323a496475313a3175343a4e616d6575393a59676764726173696c7531303a5374616765426567696e75313a317532323a5374616765436c6561726564426c6f636b496e64657875363a3437323632387531343a5374616765436c6561726564496475323a353075383a5374616765456e6475323a35307531383a556e6c6f636b6564426c6f636b496e64657875363a3434343436366575353a31303030316475323a496475353a313030303175343a4e616d657531313a4d696d69736272756e6e727531303a5374616765426567696e75383a31303030303030317532323a5374616765436c6561726564426c6f636b496e64657875323a2d317531343a5374616765436c6561726564496475323a2d3175383a5374616765456e6475383a31303030303032307531383a556e6c6f636b6564426c6f636b496e64657875363a3438373734336575313a326475323a496475313a3275343a4e616d6575373a416c666865696d7531303a5374616765426567696e75323a35317532323a5374616765436c6561726564426c6f636b496e64657875363a3438373734337531343a5374616765436c6561726564496475333a31303075383a5374616765456e6475333a3130307531383a556e6c6f636b6564426c6f636b496e64657875363a3437323632386575313a336475323a496475313a3375343a4e616d657531323a5376617274616c666865696d7531303a5374616765426567696e75333a3130317532323a5374616765436c6561726564426c6f636b496e64657875363a3530343031387531343a5374616765436c6561726564496475333a31353075383a5374616765456e6475333a3135307531383a556e6c6f636b6564426c6f636b496e64657875363a3438373734336575313a346475323a496475313a3475343a4e616d6575363a4173676172647531303a5374616765426567696e75333a3135317532323a5374616765436c6561726564426c6f636b496e64657875373a343839313637337531343a5374616765436c6561726564496475333a32303075383a5374616765456e6475333a3230307531383a556e6c6f636b6564426c6f636b496e64657875363a3530343031386575313a356475323a496475313a3575343a4e616d657531303a4d757370656c6865696d7531303a5374616765426567696e75333a3230317532323a5374616765436c6561726564426c6f636b496e64657875373a373132353232397531343a5374616765436c6561726564496475333a32353075383a5374616765456e6475333a3235307531383a556e6c6f636b6564426c6f636b496e64657875373a343839313637336575313a366475323a496475313a3675343a4e616d6575393a4a6f74756e6865696d7531303a5374616765426567696e75333a3235317532323a5374616765436c6561726564426c6f636b496e64657875383a31313237353837337531343a5374616765436c6561726564496475333a33303075383a5374616765456e6475333a3330307531383a556e6c6f636b6564426c6f636b496e64657875373a373132353232396575313a376475323a496475313a3775343a4e616d6575383a4e69666c6865696d7531303a5374616765426567696e75333a3330317532323a5374616765436c6561726564426c6f636b496e64657875383a31313439323332367531343a5374616765436c6561726564496475333a33333275383a5374616765456e6475333a3335307531383a556e6c6f636b6564426c6f636b496e64657875383a31313237353837336565")));
            avatarState.questList = new QuestList((List)new Codec().Decode(Convert.FromHexString(
                "6c6935656c6c7531303a776f726c6451756573747469316569306569313030303031656475363a34303030303075323a32306574656c7531303a776f726c6451756573747469326569306569313030303032656475363a34303030303075323a32306574656c7531303a776f726c6451756573747469336569306569313030303033656475363a33303330303075313a3175363a34303030303075333a3430306574656c7531303a776f726c6451756573747469346569306569313030303034656475363a33303630343075313a376574656c7531303a776f726c6451756573747469356569306569313030303035656475363a34303030303075323a32306574656c7531303a776f726c6451756573747469366569306569313030303036656475363a33303331303075313a316574656c7531303a776f726c6451756573747469376569306569313030303037656475363a34303030303075323a32306574656c7531303a776f726c6451756573747469386569306569313030303038656475363a34303030303075323a32306574656c7531303a776f726c6451756573747469396569306569313030303039656475363a33303332303075313a316574656c7531303a776f726c645175657374746931306569306569313030303130656475363a34303030303075323a32306574656c7531303a776f726c645175657374746931316569306569313030303131656475363a33303630323375313a3175363a33303630343175313a326574656c7531303a776f726c645175657374746931326569306569313030303132656475363a34303030303075323a32306574656c7531303a776f726c645175657374746931336569306569313030303133656475363a33303630323575313a3375363a33303630343175313a326574656c7531303a776f726c645175657374746931346569306569313030303134656475363a34303030303075323a32306574656c7531303a776f726c645175657374746931356569306569313030303135656475363a33303333303075313a316574656c7531303a776f726c645175657374746931366569306569313030303136656475363a34303030303075323a32306574656c7531303a776f726c645175657374746931376569306569313030303137656475363a34303030303075323a32306574656c7531303a776f726c645175657374746931386569306569313030303138656475363a34303030303075323a32306574656c7531303a776f726c645175657374746931396569306569313030303139656475363a34303030303075323a323075363a35303030303075313a316574656c7531303a776f726c645175657374746932306569306569313030303230656475363a33303334303075313a316574656c7531303a776f726c645175657374746932316569306569313030303231656475363a33303630303175313a3175363a33303630343475313a316574656c7531303a776f726c645175657374746932326569306569313030303232656475363a33303630303075313a3175363a33303630303275313a316574656c7531303a776f726c645175657374746932336569306569313030303233656475363a34303030303075323a32306574656c7531303a776f726c645175657374746932346569306569313030303234656475363a33303330303175313a326574656c7531303a776f726c645175657374746932356569306569313030303235656475363a33303630303075313a3175363a33303630303175313a316574656c7531303a776f726c645175657374746932366569306569313030303236656475363a34303030303075323a32306574656c7531303a776f726c645175657374746932376569306569313030303237656475363a34303030303075323a32306574656c7531303a776f726c645175657374746932386569306569313030303238656475363a34303030303075323a32306574656c7531303a776f726c645175657374746932396569306569313030303239656475363a34303030303075323a32306574656c7531303a776f726c645175657374746933306569306569313030303330656475363a34303030303075323a32306574656c7531303a776f726c645175657374746933316569306569313030303331656475363a34303030303075323a32306574656c7531303a776f726c645175657374746933326569306569313030303332656475363a34303030303075323a32306574656c7531303a776f726c645175657374746933336569306569313030303333656475363a34303030303075323a32306574656c7531303a776f726c645175657374746933346569306569313030303334656475363a34303030303075323a32306574656c7531303a776f726c645175657374746933356569306569313030303335656475363a34303030303075323a32306574656c7531303a776f726c645175657374746933366569306569313030303336656475363a34303030303075323a32306574656c7531303a776f726c645175657374746933376569306569313030303337656475363a34303030303075323a32306574656c7531303a776f726c645175657374746933386569306569313030303338656475363a34303030303075323a32306574656c7531303a776f726c645175657374746933396569306569313030303339656475363a34303030303075323a32306574656c7531303a776f726c645175657374746934306569306569313030303430656475363a34303030303075323a323075363a35303030303075313a316574656c7531303a776f726c645175657374746934316569306569313030303431656475363a34303030303075323a32306574656c7531303a776f726c645175657374746934326569306569313030303432656475363a34303030303075323a32306574656c7531303a776f726c645175657374746934336569306569313030303433656475363a34303030303075323a32306574656c7531303a776f726c645175657374746934346569306569313030303434656475363a34303030303075323a32306574656c7531303a776f726c645175657374746934356569306569313030303435656475363a34303030303075323a32306574656c7531303a776f726c645175657374746934366569306569313030303436656475363a34303030303075323a32306574656c7531303a776f726c645175657374746934376569306569313030303437656475363a34303030303075323a32306574656c7531303a776f726c645175657374746934386569306569313030303438656475363a34303030303075323a32306574656c7531303a776f726c645175657374746934396569306569313030303439656475363a34303030303075323a32306574656c7531303a776f726c645175657374746935306569306569313030303530656475363a33303430303275313a316574656c7531303a776f726c645175657374746935316569306569313030303531656475363a34303030303075323a32306574656c7531303a776f726c645175657374746935326569306569313030303532656475363a34303030303075323a32306574656c7531303a776f726c645175657374746935336569306569313030303533656475363a34303030303075323a32306574656c7531303a776f726c645175657374746935346569306569313030303534656475363a34303030303075323a32306574656c7531303a776f726c645175657374746935356569306569313030303535656475363a34303030303075323a32306574656c7531303a776f726c645175657374746935366569306569313030303536656475363a34303030303075323a32306574656c7531303a776f726c645175657374746935376569306569313030303537656475363a34303030303075323a32306574656c7531303a776f726c645175657374746935386569306569313030303538656475363a34303030303075323a32306574656c7531303a776f726c645175657374746935396569306569313030303539656475363a34303030303075323a32306574656c7531303a776f726c645175657374746936306569306569313030303630656475363a34303030303075323a323075363a35303030303075313a316574656c7531303a776f726c645175657374746936316569306569313030303631656475363a34303030303075323a32306574656c7531303a776f726c645175657374746936326569306569313030303632656475363a34303030303075323a32306574656c7531303a776f726c645175657374746936336569306569313030303633656475363a34303030303075323a32306574656c7531303a776f726c645175657374746936346569306569313030303634656475363a34303030303075323a32306574656c7531303a776f726c645175657374746936356569306569313030303635656475363a34303030303075323a32306574656c7531303a776f726c645175657374746936366569306569313030303636656475363a34303030303075323a32306574656c7531303a776f726c645175657374746936376569306569313030303637656475363a34303030303075323a32306574656c7531303a776f726c645175657374746936386569306569313030303638656475363a34303030303075323a32306574656c7531303a776f726c645175657374746936396569306569313030303639656475363a34303030303075323a32306574656c7531303a776f726c645175657374746937306569306569313030303730656475363a34303030303075323a32306574656c7531303a776f726c645175657374746937316569306569313030303731656475363a34303030303075323a32306574656c7531303a776f726c645175657374746937326569306569313030303732656475363a34303030303075323a32306574656c7531303a776f726c645175657374746937336569306569313030303733656475363a34303030303075323a32306574656c7531303a776f726c645175657374746937346569306569313030303734656475363a34303030303075323a32306574656c7531303a776f726c645175657374746937356569306569313030303735656475363a34303030303075323a32306574656c7531303a776f726c645175657374746937366569306569313030303736656475363a34303030303075323a32306574656c7531303a776f726c645175657374746937376569306569313030303737656475363a34303030303075323a32306574656c7531303a776f726c645175657374746937386569306569313030303738656475363a34303030303075323a32306574656c7531303a776f726c645175657374746937396569306569313030303739656475363a34303030303075323a32306574656c7531303a776f726c645175657374746938306569306569313030303830656475363a34303030303075323a323075363a35303030303075313a316574656c7531303a776f726c645175657374746938316569306569313030303831656475363a34303030303075323a32306574656c7531303a776f726c645175657374746938326569306569313030303832656475363a34303030303075323a32306574656c7531303a776f726c645175657374746938336569306569313030303833656475363a34303030303075323a32306574656c7531303a776f726c645175657374746938346569306569313030303834656475363a34303030303075323a32306574656c7531303a776f726c645175657374746938356569306569313030303835656475363a34303030303075323a32306574656c7531303a776f726c645175657374746938366569306569313030303836656475363a34303030303075323a32306574656c7531303a776f726c645175657374746938376569306569313030303837656475363a34303030303075323a32306574656c7531303a776f726c645175657374746938386569306569313030303838656475363a34303030303075323a32306574656c7531303a776f726c645175657374746938396569306569313030303839656475363a34303030303075323a32306574656c7531303a776f726c645175657374746939306569306569313030303930656475363a34303030303075323a32306574656c7531303a776f726c645175657374746939316569306569313030303931656475363a34303030303075323a32306574656c7531303a776f726c645175657374746939326569306569313030303932656475363a34303030303075323a32306574656c7531303a776f726c645175657374746939336569306569313030303933656475363a34303030303075323a32306574656c7531303a776f726c645175657374746939346569306569313030303934656475363a34303030303075323a32306574656c7531303a776f726c645175657374746939356569306569313030303935656475363a34303030303075323a32306574656c7531303a776f726c645175657374746939366569306569313030303936656475363a34303030303075323a32306574656c7531303a776f726c645175657374746939376569306569313030303937656475363a34303030303075323a32306574656c7531303a776f726c645175657374746939386569306569313030303938656475363a34303030303075323a32306574656c7531303a776f726c645175657374746939396569306569313030303939656475363a34303030303075323a323075363a35303030303075313a316574656c7531303a776f726c64517565737474693130306569306569313030313030656475363a33303430303175313a316574656c7531303a776f726c64517565737474693130316569306569313030313031656475363a34303030303075323a32306574656c7531303a776f726c64517565737474693130326569306569313030313032656475363a34303030303075323a32306574656c7531303a776f726c64517565737474693130336569306569313030313033656475363a34303030303075323a32306574656c7531303a776f726c64517565737474693130346569306569313030313034656475363a34303030303075323a32306574656c7531303a776f726c64517565737474693130356569306569313030313035656475363a34303030303075323a32306574656c7531303a776f726c64517565737474693130366569306569313030313036656475363a34303030303075323a32306574656c7531303a776f726c64517565737474693130376569306569313030313037656475363a34303030303075323a32306574656c7531303a776f726c64517565737474693130386569306569313030313038656475363a34303030303075323a32306574656c7531303a776f726c64517565737474693130396569306569313030313039656475363a34303030303075323a32306574656c7531303a776f726c64517565737474693131306569306569313030313130656475363a34303030303075323a32306574656c7531303a776f726c64517565737474693131316569306569313030313131656475363a34303030303075323a32306574656c7531303a776f726c64517565737474693131326569306569313030313132656475363a34303030303075323a32306574656c7531303a776f726c64517565737474693131336569306569313030313133656475363a34303030303075323a32306574656c7531303a776f726c64517565737474693131346569306569313030313134656475363a34303030303075323a32306574656c7531303a776f726c64517565737474693131356569306569313030313135656475363a34303030303075323a32306574656c7531303a776f726c64517565737474693131366569306569313030313136656475363a34303030303075323a32306574656c7531303a776f726c64517565737474693131376569306569313030313137656475363a34303030303075323a32306574656c7531303a776f726c64517565737474693131386569306569313030313138656475363a34303030303075323a32306574656c7531303a776f726c64517565737474693131396569306569313030313139656475363a34303030303075323a32306574656c7531303a776f726c64517565737474693132306569306569313030313230656475363a34303030303075323a323075363a35303030303075313a316574656c7531303a776f726c64517565737474693132316569306569313030313231656475363a34303030303075323a32306574656c7531303a776f726c64517565737474693132326569306569313030313232656475363a34303030303075323a32306574656c7531303a776f726c64517565737474693132336569306569313030313233656475363a34303030303075323a32306574656c7531303a776f726c64517565737474693132346569306569313030313234656475363a34303030303075323a32306574656c7531303a776f726c64517565737474693132356569306569313030313235656475363a34303030303075323a32306574656c7531303a776f726c64517565737474693132366569306569313030313236656475363a34303030303075323a32306574656c7531303a776f726c64517565737474693132376569306569313030313237656475363a34303030303075323a32306574656c7531303a776f726c64517565737474693132386569306569313030313238656475363a34303030303075323a32306574656c7531303a776f726c64517565737474693132396569306569313030313239656475363a34303030303075323a32306574656c7531303a776f726c64517565737474693133306569306569313030313330656475363a34303030303075323a32306574656c7531303a776f726c64517565737474693133316569306569313030313331656475363a34303030303075323a32306574656c7531303a776f726c64517565737474693133326569306569313030313332656475363a34303030303075323a32306574656c7531303a776f726c64517565737474693133336569306569313030313333656475363a34303030303075323a32306574656c7531303a776f726c64517565737474693133346569306569313030313334656475363a34303030303075323a32306574656c7531303a776f726c64517565737474693133356569306569313030313335656475363a34303030303075323a32306574656c7531303a776f726c64517565737474693133366569306569313030313336656475363a34303030303075323a32306574656c7531303a776f726c64517565737474693133376569306569313030313337656475363a34303030303075323a32306574656c7531303a776f726c64517565737474693133386569306569313030313338656475363a34303030303075323a32306574656c7531303a776f726c64517565737474693133396569306569313030313339656475363a34303030303075323a32306574656c7531303a776f726c64517565737474693134306569306569313030313430656475363a34303030303075323a323075363a35303030303075313a316574656c7531303a776f726c64517565737474693134316569306569313030313431656475363a34303030303075323a32306574656c7531303a776f726c64517565737474693134326569306569313030313432656475363a34303030303075323a32306574656c7531303a776f726c64517565737474693134336569306569313030313433656475363a34303030303075323a32306574656c7531303a776f726c64517565737474693134346569306569313030313434656475363a34303030303075323a32306574656c7531303a776f726c64517565737474693134356569306569313030313435656475363a34303030303075323a32306574656c7531303a776f726c64517565737474693134366569306569313030313436656475363a34303030303075323a32306574656c7531303a776f726c64517565737474693134376569306569313030313437656475363a34303030303075323a32306574656c7531303a776f726c64517565737474693134386569306569313030313438656475363a34303030303075323a32306574656c7531303a776f726c64517565737474693134396569306569313030313439656475363a34303030303075323a32306574656c7531303a776f726c64517565737474693135306569306569313030313530656475363a33303430303075313a316574656c7531303a776f726c64517565737474693135316569306569313030313531656475363a34303030303075323a32306574656c7531303a776f726c64517565737474693135326569306569313030313532656475363a34303030303075323a32306574656c7531303a776f726c64517565737474693135336569306569313030313533656475363a34303030303075323a32306574656c7531303a776f726c64517565737474693135346569306569313030313534656475363a34303030303075323a32306574656c7531303a776f726c64517565737474693135356569306569313030313535656475363a34303030303075323a32306574656c7531303a776f726c64517565737474693135366569306569313030313536656475363a34303030303075323a32306574656c7531303a776f726c64517565737474693135376569306569313030313537656475363a34303030303075323a32306574656c7531303a776f726c64517565737474693135386569306569313030313538656475363a34303030303075323a32306574656c7531303a776f726c64517565737474693135396569306569313030313539656475363a34303030303075323a32306574656c7531303a776f726c64517565737474693136306569306569313030313630656475363a34303030303075323a32306574656c7531303a776f726c64517565737474693136316569306569313030313631656475363a34303030303075323a32306574656c7531303a776f726c64517565737474693136326569306569313030313632656475363a34303030303075323a32306574656c7531303a776f726c64517565737474693136336569306569313030313633656475363a34303030303075323a32306574656c7531303a776f726c64517565737474693136346569306569313030313634656475363a34303030303075323a32306574656c7531303a776f726c64517565737474693136356569306569313030313635656475363a34303030303075323a32306574656c7531303a776f726c64517565737474693136366569306569313030313636656475363a34303030303075323a32306574656c7531303a776f726c64517565737474693136376569306569313030313637656475363a34303030303075323a32306574656c7531303a776f726c64517565737474693136386569306569313030313638656475363a34303030303075323a32306574656c7531303a776f726c64517565737474693136396569306569313030313639656475363a34303030303075323a32306574656c7531303a776f726c64517565737474693137306569306569313030313730656475363a34303030303075323a32306574656c7531303a776f726c64517565737474693137316569306569313030313731656475363a34303030303075323a32306574656c7531303a776f726c64517565737474693137326569306569313030313732656475363a34303030303075323a32306574656c7531303a776f726c64517565737474693137336569306569313030313733656475363a34303030303075323a32306574656c7531303a776f726c64517565737474693137346569306569313030313734656475363a34303030303075323a32306574656c7531303a776f726c64517565737474693137356569306569313030313735656475363a34303030303075323a32306574656c7531303a776f726c64517565737474693137366569306569313030313736656475363a34303030303075323a32306574656c7531303a776f726c64517565737474693137376569306569313030313737656475363a34303030303075323a32306574656c7531303a776f726c64517565737474693137386569306569313030313738656475363a34303030303075323a32306574656c7531303a776f726c64517565737474693137396569306569313030313739656475363a34303030303075323a32306574656c7531303a776f726c64517565737474693138306569306569313030313830656475363a34303030303075323a32306574656c7531303a776f726c64517565737474693138316569306569313030313831656475363a34303030303075323a32306574656c7531303a776f726c64517565737474693138326569306569313030313832656475363a34303030303075323a32306574656c7531303a776f726c64517565737474693138336569306569313030313833656475363a34303030303075323a32306574656c7531303a776f726c64517565737474693138346569306569313030313834656475363a34303030303075323a32306574656c7531303a776f726c64517565737474693138356569306569313030313835656475363a34303030303075323a32306574656c7531303a776f726c64517565737474693138366569306569313030313836656475363a34303030303075323a32306574656c7531303a776f726c64517565737474693138376569306569313030313837656475363a34303030303075323a32306574656c7531303a776f726c64517565737474693138386569306569313030313838656475363a34303030303075323a32306574656c7531303a776f726c64517565737474693138396569306569313030313839656475363a34303030303075323a32306574656c7531303a776f726c64517565737474693139306569306569313030313930656475363a34303030303075323a32306574656c7531303a776f726c64517565737474693139316569306569313030313931656475363a34303030303075323a32306574656c7531303a776f726c64517565737474693139326569306569313030313932656475363a34303030303075323a32306574656c7531303a776f726c64517565737474693139336569306569313030313933656475363a34303030303075323a32306574656c7531303a776f726c64517565737474693139346569306569313030313934656475363a34303030303075323a32306574656c7531303a776f726c64517565737474693139356569306569313030313935656475363a34303030303075323a32306574656c7531303a776f726c64517565737474693139366569306569313030313936656475363a34303030303075323a32306574656c7531303a776f726c64517565737474693139376569306569313030313937656475363a34303030303075323a32306574656c7531303a776f726c64517565737474693139386569306569313030313938656475363a34303030303075323a32306574656c7531303a776f726c64517565737474693139396569306569313030313939656475363a34303030303075323a32306574656c7531303a776f726c64517565737474693230306569306569313030323030656475363a33303430303375313a316574656c7531303a776f726c64517565737474693230316569306569313030323031656475363a34303030303075323a32306574656c7531303a776f726c64517565737474693230326569306569313030323032656475363a34303030303075323a32306574656c7531303a776f726c64517565737474693230336569306569313030323033656475363a34303030303075323a32306574656c7531303a776f726c64517565737474693230346569306569313030323034656475363a34303030303075323a32306574656c7531303a776f726c64517565737474693230356569306569313030323035656475363a34303030303075323a32306574656c7531303a776f726c64517565737474693230366569306569313030323036656475363a34303030303075323a32306574656c7531303a776f726c64517565737474693230376569306569313030323037656475363a34303030303075323a32306574656c7531303a776f726c64517565737474693230386569306569313030323038656475363a34303030303075323a32306574656c7531303a776f726c64517565737474693230396569306569313030323039656475363a34303030303075323a32306574656c7531303a776f726c64517565737474693231306569306569313030323130656475363a34303030303075323a32306574656c7531303a776f726c64517565737474693231316569306569313030323131656475363a34303030303075323a32306574656c7531303a776f726c64517565737474693231326569306569313030323132656475363a34303030303075323a32306574656c7531303a776f726c64517565737474693231336569306569313030323133656475363a34303030303075323a32306574656c7531303a776f726c64517565737474693231346569306569313030323134656475363a34303030303075323a32306574656c7531303a776f726c64517565737474693231356569306569313030323135656475363a34303030303075323a32306574656c7531303a776f726c64517565737474693231366569306569313030323136656475363a34303030303075323a32306574656c7531303a776f726c64517565737474693231376569306569313030323137656475363a34303030303075323a32306574656c7531303a776f726c64517565737474693231386569306569313030323138656475363a34303030303075323a32306574656c7531303a776f726c64517565737474693231396569306569313030323139656475363a34303030303075323a32306574656c7531303a776f726c64517565737474693232306569306569313030323230656475363a34303030303075323a32306574656c7531303a776f726c64517565737474693232316569306569313030323231656475363a34303030303075323a32306574656c7531303a776f726c64517565737474693232326569306569313030323232656475363a34303030303075323a32306574656c7531303a776f726c64517565737474693232336569306569313030323233656475363a34303030303075323a32306574656c7531303a776f726c64517565737474693232346569306569313030323234656475363a34303030303075323a32306574656c7531303a776f726c64517565737474693232356569306569313030323235656475363a34303030303075323a32306574656c7531303a776f726c64517565737474693232366569306569313030323236656475363a34303030303075323a32306574656c7531303a776f726c64517565737474693232376569306569313030323237656475363a34303030303075323a32306574656c7531303a776f726c64517565737474693232386569306569313030323238656475363a34303030303075323a32306574656c7531303a776f726c64517565737474693232396569306569313030323239656475363a34303030303075323a32306574656c7531303a776f726c64517565737474693233306569306569313030323330656475363a34303030303075323a32306574656c7531303a776f726c64517565737474693233316569306569313030323331656475363a34303030303075323a32306574656c7531303a776f726c64517565737474693233326569306569313030323332656475363a34303030303075323a32306574656c7531303a776f726c64517565737474693233336569306569313030323333656475363a34303030303075323a32306574656c7531303a776f726c64517565737474693233346569306569313030323334656475363a34303030303075323a32306574656c7531303a776f726c64517565737474693233356569306569313030323335656475363a34303030303075323a32306574656c7531303a776f726c64517565737474693233366569306569313030323336656475363a34303030303075323a32306574656c7531303a776f726c64517565737474693233376569306569313030323337656475363a34303030303075323a32306574656c7531303a776f726c64517565737474693233386569306569313030323338656475363a34303030303075323a32306574656c7531303a776f726c64517565737474693233396569306569313030323339656475363a34303030303075323a32306574656c7531303a776f726c64517565737474693234306569306569313030323430656475363a34303030303075323a32306574656c7531303a776f726c64517565737474693234316569306569313030323431656475363a34303030303075323a32306574656c7531303a776f726c64517565737474693234326569306569313030323432656475363a34303030303075323a32306574656c7531303a776f726c64517565737474693234336569306569313030323433656475363a34303030303075323a32306574656c7531303a776f726c64517565737474693234346569306569313030323434656475363a34303030303075323a32306574656c7531303a776f726c64517565737474693234356569306569313030323435656475363a34303030303075323a32306574656c7531303a776f726c64517565737474693234366569306569313030323436656475363a34303030303075323a32306574656c7531303a776f726c64517565737474693234376569306569313030323437656475363a34303030303075323a32306574656c7531303a776f726c64517565737474693234386569306569313030323438656475363a34303030303075323a32306574656c7531303a776f726c64517565737474693234396569306569313030323439656475363a34303030303075323a32306574656c7531303a776f726c64517565737474693235306569306569313030323530656475363a34303030303075323a32306574656c7531303a776f726c64517565737474693235316569306569313030323531656475363a34303030303075323a32306574656c7531303a776f726c64517565737474693235326569306569313030323532656475363a34303030303075323a32306574656c7531303a776f726c64517565737474693235336569306569313030323533656475363a34303030303075323a32306574656c7531303a776f726c64517565737474693235346569306569313030323534656475363a34303030303075323a32306574656c7531303a776f726c64517565737474693235356569306569313030323535656475363a34303030303075323a32306574656c7531303a776f726c64517565737474693235366569306569313030323536656475363a34303030303075323a32306574656c7531303a776f726c64517565737474693235376569306569313030323537656475363a34303030303075323a32306574656c7531303a776f726c64517565737474693235386569306569313030323538656475363a34303030303075323a32306574656c7531303a776f726c64517565737474693235396569306569313030323539656475363a34303030303075323a32306574656c7531303a776f726c64517565737474693236306569306569313030323630656475363a34303030303075323a32306574656c7531303a776f726c64517565737474693236316569306569313030323631656475363a34303030303075323a32306574656c7531303a776f726c64517565737474693236326569306569313030323632656475363a34303030303075323a32306574656c7531303a776f726c64517565737474693236336569306569313030323633656475363a34303030303075323a32306574656c7531303a776f726c64517565737474693236346569306569313030323634656475363a34303030303075323a32306574656c7531303a776f726c64517565737474693236356569306569313030323635656475363a34303030303075323a32306574656c7531303a776f726c64517565737474693236366569306569313030323636656475363a34303030303075323a32306574656c7531303a776f726c64517565737474693236376569306569313030323637656475363a34303030303075323a32306574656c7531303a776f726c64517565737474693236386569306569313030323638656475363a34303030303075323a32306574656c7531303a776f726c64517565737474693236396569306569313030323639656475363a34303030303075323a32306574656c7531303a776f726c64517565737474693237306569306569313030323730656475363a34303030303075323a32306574656c7531303a776f726c64517565737474693237316569306569313030323731656475363a34303030303075323a32306574656c7531303a776f726c64517565737474693237326569306569313030323732656475363a34303030303075323a32306574656c7531303a776f726c64517565737474693237336569306569313030323733656475363a34303030303075323a32306574656c7531303a776f726c64517565737474693237346569306569313030323734656475363a34303030303075323a32306574656c7531303a776f726c64517565737474693237356569306569313030323735656475363a34303030303075323a32306574656c7531303a776f726c64517565737474693237366569306569313030323736656475363a34303030303075323a32306574656c7531303a776f726c64517565737474693237376569306569313030323737656475363a34303030303075323a32306574656c7531303a776f726c64517565737474693237386569306569313030323738656475363a34303030303075323a32306574656c7531303a776f726c64517565737474693237396569306569313030323739656475363a34303030303075323a32306574656c7531303a776f726c64517565737474693238306569306569313030323830656475363a34303030303075323a32306574656c7531303a776f726c64517565737474693238316569306569313030323831656475363a34303030303075323a32306574656c7531303a776f726c64517565737474693238326569306569313030323832656475363a34303030303075323a32306574656c7531303a776f726c64517565737474693238336569306569313030323833656475363a34303030303075323a32306574656c7531303a776f726c64517565737474693238346569306569313030323834656475363a34303030303075323a32306574656c7531303a776f726c64517565737474693238356569306569313030323835656475363a34303030303075323a32306574656c7531303a776f726c64517565737474693238366569306569313030323836656475363a34303030303075323a32306574656c7531303a776f726c64517565737474693238376569306569313030323837656475363a34303030303075323a32306574656c7531303a776f726c64517565737474693238386569306569313030323838656475363a34303030303075323a32306574656c7531303a776f726c64517565737474693238396569306569313030323839656475363a34303030303075323a32306574656c7531303a776f726c64517565737474693239306569306569313030323930656475363a34303030303075323a32306574656c7531303a776f726c64517565737474693239316569306569313030323931656475363a34303030303075323a32306574656c7531303a776f726c64517565737474693239326569306569313030323932656475363a34303030303075323a32306574656c7531303a776f726c64517565737474693239336569306569313030323933656475363a34303030303075323a32306574656c7531303a776f726c64517565737474693239346569306569313030323934656475363a34303030303075323a32306574656c7531303a776f726c64517565737474693239356569306569313030323935656475363a34303030303075323a32306574656c7531303a776f726c64517565737474693239366569306569313030323936656475363a34303030303075323a32306574656c7531303a776f726c64517565737474693239376569306569313030323937656475363a34303030303075323a32306574656c7531303a776f726c64517565737474693239386569306569313030323938656475363a34303030303075323a32306574656c7531303a776f726c64517565737474693239396569306569313030323939656475363a34303030303075323a32306574656c7531303a776f726c64517565737474693330306569306569313030333030656475363a34303030303075323a32306574656c7531303a776f726c64517565737474693330316569306569313030333031656475363a34303030303075323a32306574656c7531303a776f726c64517565737474693330326569306569313030333032656475363a34303030303075323a32306574656c7531303a776f726c64517565737474693330336569306569313030333033656475363a34303030303075323a32306574656c7531303a776f726c64517565737474693330346569306569313030333034656475363a34303030303075323a32306574656c7531303a776f726c64517565737474693330356569306569313030333035656475363a34303030303075323a32306574656c7531303a776f726c64517565737474693330366569306569313030333036656475363a34303030303075323a32306574656c7531303a776f726c64517565737474693330376569306569313030333037656475363a34303030303075323a32306574656c7531303a776f726c64517565737474693330386569306569313030333038656475363a34303030303075323a32306574656c7531303a776f726c64517565737474693330396569306569313030333039656475363a34303030303075323a32306574656c7531303a776f726c64517565737474693331306569306569313030333130656475363a34303030303075323a32306574656c7531303a776f726c64517565737474693331316569306569313030333131656475363a34303030303075323a32306574656c7531303a776f726c64517565737474693331326569306569313030333132656475363a34303030303075323a32306574656c7531303a776f726c64517565737474693331336569306569313030333133656475363a34303030303075323a32306574656c7531303a776f726c64517565737474693331346569306569313030333134656475363a34303030303075323a32306574656c7531303a776f726c64517565737474693331356569306569313030333135656475363a34303030303075323a32306574656c7531303a776f726c64517565737474693331366569306569313030333136656475363a34303030303075323a32306574656c7531303a776f726c64517565737474693331376569306569313030333137656475363a34303030303075323a32306574656c7531303a776f726c64517565737474693331386569306569313030333138656475363a34303030303075323a32306574656c7531303a776f726c64517565737474693331396569306569313030333139656475363a34303030303075323a32306574656c7531303a776f726c64517565737474693332306569306569313030333230656475363a34303030303075323a32306574656c7531303a776f726c64517565737474693332316569306569313030333231656475363a34303030303075323a32306574656c7531303a776f726c64517565737474693332326569306569313030333232656475363a34303030303075323a32306574656c7531303a776f726c64517565737474693332336569306569313030333233656475363a34303030303075323a32306574656c7531303a776f726c64517565737474693332346569306569313030333234656475363a34303030303075323a32306574656c7531303a776f726c64517565737474693332356569306569313030333235656475363a34303030303075323a32306574656c7531303a776f726c64517565737474693332366569306569313030333236656475363a34303030303075323a32306574656c7531303a776f726c64517565737474693332376569306569313030333237656475363a34303030303075323a32306574656c7531303a776f726c64517565737474693332386569306569313030333238656475363a34303030303075323a32306574656c7531303a776f726c64517565737474693332396569306569313030333239656475363a34303030303075323a32306574656c7531303a776f726c64517565737474693333306569306569313030333330656475363a34303030303075323a32306574656c7531303a776f726c64517565737474693333316569306569313030333331656475363a34303030303075323a32306574656c7531303a776f726c64517565737474693333326569306569313030333332656475363a34303030303075323a32306574656c7531303a776f726c64517565737466693333336569306569313030333333656475363a34303030303075323a32306566656c7531303a776f726c64517565737466693333346569306569313030333334656475363a34303030303075323a32306566656c7531303a776f726c64517565737466693333356569306569313030333335656475363a34303030303075323a32306566656c7531303a776f726c64517565737466693333366569306569313030333336656475363a34303030303075323a32306566656c7531303a776f726c64517565737466693333376569306569313030333337656475363a34303030303075323a32306566656c7531303a776f726c64517565737466693333386569306569313030333338656475363a34303030303075323a32306566656c7531303a776f726c64517565737466693333396569306569313030333339656475363a34303030303075323a32306566656c7531303a776f726c64517565737466693334306569306569313030333430656475363a34303030303075323a32306566656c7531303a776f726c64517565737466693334316569306569313030333431656475363a34303030303075323a32306566656c7531303a776f726c64517565737466693334326569306569313030333432656475363a34303030303075323a32306566656c7531303a776f726c64517565737466693334336569306569313030333433656475363a34303030303075323a32306566656c7531303a776f726c64517565737466693334346569306569313030333434656475363a34303030303075323a32306566656c7531303a776f726c64517565737466693334356569306569313030333435656475363a34303030303075323a32306566656c7531303a776f726c64517565737466693334366569306569313030333436656475363a34303030303075323a32306566656c7531303a776f726c64517565737466693334376569306569313030333437656475363a34303030303075323a32306566656c7531303a776f726c64517565737466693334386569306569313030333438656475363a34303030303075323a32306566656c7531303a776f726c64517565737466693334396569306569313030333439656475363a34303030303075323a32306566656c7531303a776f726c64517565737466693335306569306569313030333530656475363a34303030303075323a32306566656c7531303a776f726c64517565737466693335316569306569313030333531656475363a34303030303075333a3530306566656c7531303a776f726c64517565737466693335326569306569313030333532656475363a34303030303075333a3530306566656c7531303a776f726c64517565737466693335336569306569313030333533656475363a34303030303075333a3530306566656c7531303a776f726c64517565737466693335346569306569313030333534656475363a34303030303075333a3530306566656c7531303a776f726c64517565737466693335356569306569313030333535656475363a34303030303075333a3530306566656c7531303a776f726c64517565737466693335366569306569313030333536656475363a34303030303075333a3530306566656c7531303a776f726c64517565737466693335376569306569313030333537656475363a34303030303075333a3530306566656c7531303a776f726c64517565737466693335386569306569313030333538656475363a34303030303075333a3530306566656c7531303a776f726c64517565737466693335396569306569313030333539656475363a34303030303075333a3530306566656c7531303a776f726c64517565737466693336306569306569313030333630656475363a34303030303075333a3530306566656c7531303a776f726c64517565737466693336316569306569313030333631656475363a34303030303075333a3530306566656c7531303a776f726c64517565737466693336326569306569313030333632656475363a34303030303075333a3530306566656c7531303a776f726c64517565737466693336336569306569313030333633656475363a34303030303075333a3530306566656c7531303a776f726c64517565737466693336346569306569313030333634656475363a34303030303075333a3530306566656c7531303a776f726c64517565737466693336356569306569313030333635656475363a34303030303075333a3530306566656c7531303a776f726c64517565737466693336366569306569313030333636656475363a34303030303075333a3530306566656c7531303a776f726c64517565737466693336376569306569313030333637656475363a34303030303075333a3530306566656c7531303a776f726c64517565737466693336386569306569313030333638656475363a34303030303075333a3530306566656c7531303a776f726c64517565737466693336396569306569313030333639656475363a34303030303075333a3530306566656c7531303a776f726c64517565737466693337306569306569313030333730656475363a34303030303075333a3530306566656c7531303a776f726c64517565737466693337316569306569313030333731656475363a34303030303075333a3530306566656c7531303a776f726c64517565737466693337326569306569313030333732656475363a34303030303075333a3530306566656c7531303a776f726c64517565737466693337336569306569313030333733656475363a34303030303075333a3530306566656c7531303a776f726c64517565737466693337346569306569313030333734656475363a34303030303075333a3530306566656c7531303a776f726c64517565737466693337356569306569313030333735656475363a34303030303075333a3530306566656c7531303a776f726c64517565737466693337366569306569313030333736656475363a34303030303075333a3530306566656c7531303a776f726c64517565737466693337376569306569313030333737656475363a34303030303075333a3530306566656c7531303a776f726c64517565737466693337386569306569313030333738656475363a34303030303075333a3530306566656c7531303a776f726c64517565737466693337396569306569313030333739656475363a34303030303075333a3530306566656c7531303a776f726c64517565737466693338306569306569313030333830656475363a34303030303075333a3530306566656c7531303a776f726c64517565737466693338316569306569313030333831656475363a34303030303075333a3530306566656c7531303a776f726c64517565737466693338326569306569313030333832656475363a34303030303075333a3530306566656c7531303a776f726c64517565737466693338336569306569313030333833656475363a34303030303075333a3530306566656c7531303a776f726c64517565737466693338346569306569313030333834656475363a34303030303075333a3530306566656c7531303a776f726c64517565737466693338356569306569313030333835656475363a34303030303075333a3530306566656c7531303a776f726c64517565737466693338366569306569313030333836656475363a34303030303075333a3530306566656c7531303a776f726c64517565737466693338376569306569313030333837656475363a34303030303075333a3530306566656c7531303a776f726c64517565737466693338386569306569313030333838656475363a34303030303075333a3530306566656c7531303a776f726c64517565737466693338396569306569313030333839656475363a34303030303075333a3530306566656c7531303a776f726c64517565737466693339306569306569313030333930656475363a34303030303075333a3530306566656c7531303a776f726c64517565737466693339316569306569313030333931656475363a34303030303075333a3530306566656c7531303a776f726c64517565737466693339326569306569313030333932656475363a34303030303075333a3530306566656c7531303a776f726c64517565737466693339336569306569313030333933656475363a34303030303075333a3530306566656c7531303a776f726c64517565737466693339346569306569313030333934656475363a34303030303075333a3530306566656c7531303a776f726c64517565737466693339356569306569313030333935656475363a34303030303075333a3530306566656c7531303a776f726c64517565737466693339366569306569313030333936656475363a34303030303075333a3530306566656c7531303a776f726c64517565737466693339376569306569313030333937656475363a34303030303075333a3530306566656c7531303a776f726c64517565737466693339386569306569313030333938656475363a34303030303075333a3530306566656c7531303a776f726c64517565737466693339396569306569313030333939656475363a34303030303075333a3530306566656c7531303a776f726c64517565737466693430306569306569313030343030656475363a34303030303075333a3530306566656c7531323a636f6c6c65637451756573747469316569316569323030303031656475363a33303230303175313a3175363a33303230303275313a3165746932303130303065656c7531323a636f6c6c65637451756573747469316569316569323030303032656475363a33303230303475313a3275363a33303230303675313a3165746932303130303165656c7531323a636f6c6c65637451756573747469316569316569323030303033656475363a33303230303375313a3165746932303130303265656c7531323a636f6c6c65637451756573747469316569316569323030303034656475363a33303230303375313a3265746932303130303365656c7531323a636f6c6c65637451756573746669316569306569323030303035656475363a33303230303375313a3275363a33303230303575313a3265666932303130303465656c7531323a636f6c6c65637451756573747469316569316569323030303036656475363a33303230303375313a3165746932303130303565656c7531323a636f6c6c65637451756573747469316569316569323030303037656475363a33303230303375313a3265746932303130303665656c7531323a636f6c6c65637451756573746669316569306569323030303038656475363a33303230303075313a3275363a33303230303375313a3265666932303130303765656c7531323a636f6c6c65637451756573747469316569316569323030303039656475363a33303230303875313a3165746932303130303865656c7531323a636f6c6c65637451756573747469316569316569323030303130656475363a33303230303075313a3275363a33303230303875313a3265746932303130303965656c7531323a636f6c6c65637451756573747469316569316569323030303131656475363a33303230303975313a3165746932303130313065656c7531323a636f6c6c65637451756573747469316569316569323030303132656475363a33303230303875313a3165746932303130313165656c7531323a636f6c6c65637451756573747469316569316569323030303133656475363a33303230303575313a3275363a33303230303875313a3265746932303130313265656c7531323a636f6c6c65637451756573747469316569316569323030303134656475363a33303230303975313a3165746932303130313365656c7531363a636f6d62696e6174696f6e51756573747469316569316569333030303031656475363a33303230303175313a316574693265693665656c7531363a636f6d62696e6174696f6e51756573747469336569336569333030303032656475363a33303230303175313a316574693265693665656c7531363a636f6d62696e6174696f6e51756573747469356569356569333030303033656475363a33303230303175313a316574693265693665656c7531363a636f6d62696e6174696f6e517565737474693130656931306569333030303034656475363a33303230303175313a326574693265693665656c7531363a636f6d62696e6174696f6e517565737474693530656935306569333030303035656475363a33303230303175313a326574693265693665656c7531363a636f6d62696e6174696f6e51756573747469316569316569333030303036656475363a33303230303375313a316574693265693765656c7531363a636f6d62696e6174696f6e51756573747469336569336569333030303037656475363a33303230303375313a316574693265693765656c7531363a636f6d62696e6174696f6e51756573747469356569356569333030303038656475363a33303230303375313a316574693265693765656c7531363a636f6d62696e6174696f6e517565737474693130656931306569333030303039656475363a33303230303375313a326574693265693765656c7531363a636f6d62696e6174696f6e517565737474693530656935306569333030303130656475363a33303230303375313a326574693265693765656c7531363a636f6d62696e6174696f6e51756573747469316569316569333030303131656475363a33303230303575313a316574693265693865656c7531363a636f6d62696e6174696f6e51756573747469336569336569333030303132656475363a33303230303575313a316574693265693865656c7531363a636f6d62696e6174696f6e51756573747469356569356569333030303133656475363a33303230303575313a316574693265693865656c7531363a636f6d62696e6174696f6e517565737474693130656931306569333030303134656475363a33303230303575313a326574693265693865656c7531363a636f6d62696e6174696f6e517565737474693530656935306569333030303135656475363a33303230303575313a326574693265693865656c7531363a636f6d62696e6174696f6e51756573747469316569316569333030303136656475363a33303230303075313a316574693265693965656c7531363a636f6d62696e6174696f6e51756573747469336569336569333030303137656475363a33303230303075313a316574693265693965656c7531363a636f6d62696e6174696f6e51756573747469356569356569333030303138656475363a33303230303075313a316574693265693965656c7531363a636f6d62696e6174696f6e517565737474693130656931306569333030303139656475363a33303230303075313a326574693265693965656c7531363a636f6d62696e6174696f6e517565737474693530656935306569333030303230656475363a33303230303075313a326574693265693965656c7531363a636f6d62696e6174696f6e51756573747469316569316569333030303231656475363a33303230303175313a31657469326569313065656c7531363a636f6d62696e6174696f6e51756573747469336569336569333030303232656475363a33303230303175313a31657469326569313065656c7531363a636f6d62696e6174696f6e51756573747469356569356569333030303233656475363a33303230303175313a31657469326569313065656c7531363a636f6d62696e6174696f6e517565737474693130656931306569333030303234656475363a33303230303175313a32657469326569313065656c7531363a636f6d62696e6174696f6e517565737474693530656935306569333030303235656475363a33303230303175313a32657469326569313065656c7531363a636f6d62696e6174696f6e51756573747469316569316569333030303236656475363a33303230303375313a316574693065693065656c7531363a636f6d62696e6174696f6e517565737474693130656931306569333030303237656475363a33303230303375313a316574693065693065656c7531363a636f6d62696e6174696f6e517565737474693230656932306569333030303238656475363a33303230303375313a316574693065693065656c7531363a636f6d62696e6174696f6e517565737474693330656933306569333030303239656475363a33303230303375313a326574693065693065656c7531363a636f6d62696e6174696f6e517565737474693530656935306569333030303330656475363a33303230303375313a326574693065693065656c7531303a747261646551756573747469316569316569343030303031656475363a33303230303075313a326574693165656c7531303a7472616465517565737474693130656931306569343030303032656475363a33303230303075313a326574693165656c7531303a7472616465517565737474693530656935306569343030303033656475363a33303230303875313a326574693165656c7531303a74726164655175657374746931303065693130306569343030303034656475363a33303230303975313a326574693165656c7531303a747261646551756573747469316569316569343030303035656475363a33303230303575313a326574693065656c7531303a7472616465517565737474693130656931306569343030303036656475363a33303230303575313a326574693065656c7531303a7472616465517565737474693530656935306569343030303037656475363a33303230303875313a326574693065656c7531303a74726164655175657374746931303065693130306569343030303038656475363a33303230303975313a326574693065656c7531323a6d6f6e7374657251756573747469316569316569353030303031656475363a33303230303475313a3265746932303130303565656c7531323a6d6f6e7374657251756573747469316569316569353030303032656475363a33303230303475313a3275363a33303230303675313a3165746932303230303765656c7531323a6d6f6e7374657251756573747469316569316569353030303033656475363a33303230303875313a3265746932303330303765656c7531323a6d6f6e7374657251756573747469316569316569353030303034656475363a33303230303975313a3265746932303530303765656c7532303a6974656d456e68616e63656d656e7451756573747469336569316569363030303031656475363a33303230303275313a326574693165693165656c7532303a6974656d456e68616e63656d656e7451756573746669336569356569363030303032656475363a33303230303475313a326566693165693665656c7532303a6974656d456e68616e63656d656e7451756573747469366569316569363030303033656475363a33303230303475313a326574693265693165656c7532303a6974656d456e68616e63656d656e7451756573746669366569316569363030303034656475363a33303230303475313a3275363a33303230303675313a316566693265693665656c7532303a6974656d456e68616e63656d656e7451756573746669396569306569363030303035656475363a33303230303475313a3275363a33303230303675313a316566693365693165656c7532303a6974656d456e68616e63656d656e7451756573746669396569306569363030303036656475363a33303230303875313a326566693365693665656c7532303a6974656d456e68616e63656d656e7451756573746669396569306569363030303037656475363a33303230303975313a326566693465693165656c7532303a6974656d456e68616e63656d656e7451756573746669396569306569363030303038656475363a33303230303975313a326566693465693665656c7532303a6974656d456e68616e63656d656e7451756573746669396569306569363030303039656475363a33303230303975313a326566693565693165656c7532303a6974656d456e68616e63656d656e7451756573746669396569306569363030303130656475363a33303230303975313a326566693565693665656c7531323a67656e6572616c51756573747469316569316569373030303031656475363a33303230303275313a316574693165656c7531323a67656e6572616c517565737474693130656931306569373030303032656475363a33303230303275313a326574693165656c7531323a67656e6572616c517565737474693530656935306569373030303033656475363a33303230303275313a326574693165656c7531323a67656e6572616c517565737474693130656931306569373130303030656475363a33303230303275313a316574693265656c7531323a67656e6572616c517565737474693230656932306569373130303031656475363a33303230303275313a316574693265656c7531323a67656e6572616c517565737474693330656933306569373130303032656475363a33303230303375313a3275363a33303230303575313a326574693265656c7531323a67656e6572616c517565737474693530656935306569373130303033656475363a33303230303375313a3275363a33303230303575313a326574693265656c7531323a67656e6572616c517565737474693730656937306569373130303034656475363a33303230303475313a326574693265656c7531323a67656e6572616c517565737474693930656939306569373130303035656475363a33303230303475313a3275363a33303230303675313a316574693265656c7531323a67656e6572616c5175657374746931303065693130306569373130303036656475363a33303230303475313a3275363a33303230303675313a316574693265656c7531323a67656e6572616c5175657374746931323065693132306569373130303037656475363a33303230303475313a3275363a33303230303675313a316574693265656c7531323a67656e6572616c5175657374746931343065693134306569373130303038656475363a33303230303475313a3275363a33303230303675313a316574693265656c7531323a67656e6572616c5175657374746931363065693136306569373130303039656475363a33303230303475313a3275363a33303230303675313a316574693265656c7531323a67656e6572616c5175657374746931383065693138306569373130303130656475363a33303230303475313a3275363a33303230303675313a316574693265656c7531323a67656e6572616c5175657374746932303065693230306569373130303131656475363a33303230303475313a3275363a33303230303675313a316574693265656c7531323a67656e6572616c51756573747469316569316569373230303030656475363a33303230303275313a316574693365656c7531323a67656e6572616c517565737474693130656931306569373230303031656475363a33303230303275313a316574693365656c7531323a67656e6572616c517565737474693530656935306569373230303032656475363a33303230303275313a326574693365656c7531323a67656e6572616c5175657374746931303065693130306569373230303033656475363a33303230303275313a326574693365656c7531323a67656e6572616c517565737474693130656931316569373930303030656475363a33303230303375313a316574693465656c7531323a67656e6572616c517565737474693230656932306569373930303031656475363a33303230303375313a316574693465656c7531323a67656e6572616c517565737474693330656933316569373930303032656475363a33303230303375313a326574693465656c7531323a67656e6572616c517565737474693430656934306569373930303033656475363a33303230303375313a326574693465656c7531323a67656e6572616c517565737474693530656935306569373930303034656475363a33303230303375313a326574693465656c7531343a6974656d477261646551756573747469366569366569383030303030656475363a33303230303375313a3265746931656c69313031313030303065693130313131303030656931303231303030306569313032313130303065693130333130303030656931303431303030306565656c7531343a6974656d477261646551756573747469366569366569383030303031656475363a33303230303375313a3265746932656c69313031323030303065693130313231303030656931303132323030306569313032323030303065693130323231303030656931303332303030306565656c7531343a6974656d477261646551756573747469366569366569383030303032656475363a33303230303075313a3275363a33303230303375313a3265746933656c69313031333030303065693130313331303030656931303133333030306569313032333030303065693130323331303030656931303233313030316565656c7531343a6974656d477261646551756573747469366569366569383030303033656475363a33303230303875313a3265746934656c693230313030396569323031303132656931303334303030306569313033343130303065693130333434303030656931303434343030306565656c7531343a6974656d477261646551756573747469366569366569383030303034656475363a33303230303975313a3265746935656c693230313031306569323031303133656932303130313665693230313031396569323031303234656931303435323030316565656c7532303a6974656d54797065436f6c6c65637451756573747469356569356569393030303030656475363a33303230303375313a3165746c693330323030316569333033303030656933303331303065693330363034306569343030303030656575383a4d6174657269616c656c7532303a6974656d54797065436f6c6c656374517565737474693130656931306569393030303031656475363a33303230303075313a3275363a33303230303375313a3265746c69333032303031656933303230303265693330323030336569333032303035656933303330303065693330333130306569333033323030656933303630323365693330363034306569343030303030656575383a4d6174657269616c656c7532303a6974656d54797065436f6c6c656374517565737474693230656932306569393030303032656475363a33303230303075313a3275363a33303230303375313a3265746c693330323030306569333032303031656933303230303265693330323030336569333032303035656933303330303065693330333130306569333033323030656933303333303065693330333430306569333036303030656933303630303165693330363032336569333036303234656933303630323565693330363034306569333036303431656933303630343465693430303030306569353030303030656575383a4d6174657269616c656c7532303a6974656d54797065436f6c6c656374517565737474693330656933306569393030303033656475363a33303230303075313a3275363a33303230303375313a3265746c6933303230303065693330323030316569333032303032656933303230303365693330323030346569333032303035656933303230303665693330333030306569333033303031656933303331303065693330333130316569333033323030656933303332303165693330333330306569333033333031656933303334303065693330343030326569333036303030656933303630303165693330363030326569333036303039656933303630323365693330363032346569333036303235656933303630343065693330363034316569333036303433656933303630343465693430303030306569353030303030656575383a4d6174657269616c656c75393a476f6c6451756573747469313030656931353030656931303030303030656475363a33303230303375313a326574693065656c75393a476f6c645175657374746931303030656931353030656931303030303031656475363a33303230303475313a3275363a33303230303675313a316574693065656c75393a476f6c6451756573747469313030303065693138303732656931303030303033656475363a33303230303875313a326574693065656c75393a476f6c64517565737474693130306569313030656931303030303034656475363a33303230303075313a3275363a33303230303375313a326574693165656c75393a476f6c645175657374746931303030656931393330656931303030303035656475363a33303230303475313a3275363a33303230303675313a316574693165656c75393a476f6c6451756573747469313030303065693132353830656931303030303036656475363a33303230303875313a326574693165656c7532353a636f6d62696e6174696f6e45717569706d656e745175657374746931656931656931313030303031656475363a33303330303075313a31657475313a3175313a33656c7532353a636f6d62696e6174696f6e45717569706d656e745175657374746931656931656931313030303032656475363a33303330303075313a31657475313a3275323a3131656c7532353a636f6d62696e6174696f6e45717569706d656e745175657374746931656931656931313030303033656475363a33303330303075313a31657475313a3375323a3231656c7532353a636f6d62696e6174696f6e45717569706d656e745175657374746931656931656931313030303034656475363a33303330303075313a31657475313a3475323a3531656c7532353a636f6d62696e6174696f6e45717569706d656e745175657374746931656931656931313030303035656475363a33303330303075313a31657475313a3575323a3939656c7532353a636f6d62696e6174696f6e45717569706d656e745175657374746931656931656931313030303036656475363a33303330303175313a32657475313a3675323a3234656c7532353a636f6d62696e6174696f6e45717569706d656e745175657374746931656931656931313030303037656475363a33303330303175313a32657475313a3775323a3239656c7532353a636f6d62696e6174696f6e45717569706d656e745175657374746931656931656931313030303038656475363a33303330303175313a32657475313a3875323a3337656c7532353a636f6d62696e6174696f6e45717569706d656e745175657374746931656931656931313030303039656475363a33303330303175313a32657475313a3975333a313134656c7532353a636f6d62696e6174696f6e45717569706d656e745175657374746931656931656931313030303130656475363a33303330303175313a32657475323a313075333a313734656c7532353a636f6d62696e6174696f6e45717569706d656e745175657374746931656931656931313030303131656475363a33303330303275313a33657475323a313175323a3633656c7532353a636f6d62696e6174696f6e45717569706d656e745175657374746931656931656931313030303132656475363a33303330303275313a33657475323a313275323a3636656c7532353a636f6d62696e6174696f6e45717569706d656e745175657374746931656931656931313030303133656475363a33303330303275313a33657475323a313375323a3834656c7532353a636f6d62696e6174696f6e45717569706d656e745175657374746931656931656931313030303134656475363a33303330303275313a33657475323a313475333a313934656c7532353a636f6d62696e6174696f6e45717569706d656e745175657374746931656931656931313030303135656475363a33303330303275313a33657475323a313575333a323631656c7532353a636f6d62696e6174696f6e45717569706d656e745175657374746931656931656931313030303136656475363a33303330303275313a33657475323a313675333a313330656c7532353a636f6d62696e6174696f6e45717569706d656e745175657374746931656931656931313030303137656475363a33303330303275313a33657475323a313775333a313334656c7532353a636f6d62696e6174696f6e45717569706d656e745175657374746931656931656931313030303138656475363a33303330303275313a33657475323a313875333a313538656c7532353a636f6d62696e6174696f6e45717569706d656e745175657374746931656931656931313030303139656475363a33303330303275313a33657475323a313975333a323831656c7532353a636f6d62696e6174696f6e45717569706d656e745175657374746931656931656931313030303230656475363a33303330303275313a33657475323a323075333a333534656c7532353a636f6d62696e6174696f6e45717569706d656e745175657374746931656931656931313030303231656475363a33303330303375313a3465746932316575333a323136656c7532353a636f6d62696e6174696f6e45717569706d656e745175657374746931656931656931313030303232656475363a33303330303375313a34657475323a323275333a323231656c7532353a636f6d62696e6174696f6e45717569706d656e745175657374666931656930656931313030303233656475363a33303330303375313a3465666932336575333a323431656c7532353a636f6d62696e6174696f6e45717569706d656e745175657374746931656931656931313030303234656475363a33303330303375313a3465746932346575333a333837656c7532353a636f6d62696e6174696f6e45717569706d656e745175657374666931656930656931313030303235656475363a33303330303375313a3465666932356575333a353136656c7532353a636f6d62696e6174696f6e45717569706d656e745175657374666931656930656931313030303236656475363a33303330303375313a3465666932366575333a333031656c7532353a636f6d62696e6174696f6e45717569706d656e745175657374746931656931656931313030303237656475363a33303330303375313a3465746932376575333a333036656c7532353a636f6d62696e6174696f6e45717569706d656e745175657374666931656930656931313030303238656475363a33303330303375313a3465666932386575333a333330656c7532353a636f6d62696e6174696f6e45717569706d656e745175657374666931656930656931313030303239656475363a33303330303375313a3465666932396575333a353631656c7532353a636f6d62696e6174696f6e45717569706d656e745175657374666931656930656931313030303330656475363a33303330303375313a3465666933306575333a363736656c7532353a636f6d62696e6174696f6e45717569706d656e745175657374666931656930656931313030303331656475363a33303330303475313a3565666933316575333a343135656c7532353a636f6d62696e6174696f6e45717569706d656e745175657374666931656930656931313030303332656475363a33303330303475313a3565666933326575333a343232656c7532353a636f6d62696e6174696f6e45717569706d656e745175657374666931656930656931313030303333656475363a33303330303475313a3565666933336575333a343736656c7532353a636f6d62696e6174696f6e45717569706d656e745175657374666931656930656931313030303334656475363a33303330303475313a3565666933346575333a363936656c7532353a636f6d62696e6174696f6e45717569706d656e745175657374666931656930656931313030303335656475363a33303330303475313a3565666933356575333a373531656c7532353a636f6d62696e6174696f6e45717569706d656e745175657374666931656930656931313030303336656475363a33303330303475313a3565666933366575333a363036656c7532353a636f6d62696e6174696f6e45717569706d656e745175657374666931656930656931313030303337656475363a33303330303475313a3565666933376575333a363136656c7532353a636f6d62696e6174696f6e45717569706d656e745175657374666931656930656931313030303338656475363a33303330303475313a3565666933386575333a363536656c7532353a636f6d62696e6174696f6e45717569706d656e745175657374666931656930656931313030303339656475363a33303330303475313a3565666933396575333a383036656c7532353a636f6d62696e6174696f6e45717569706d656e745175657374666931656930656931313030303430656475363a33303330303475313a3565666934306575333a383330656c7532353a636f6d62696e6174696f6e45717569706d656e745175657374666931656930656931313030303431656475363a33303330303475313a3565666934316575333a383534656c7532353a636f6d62696e6174696f6e45717569706d656e745175657374746931656931656931313030303432656475363a33303331303075313a31657475323a343275313a36656c7532353a636f6d62696e6174696f6e45717569706d656e745175657374746931656931656931313030303433656475363a33303331303075313a31657475323a343375323a3133656c7532353a636f6d62696e6174696f6e45717569706d656e745175657374746931656931656931313030303434656475363a33303331303075313a31657475323a343475323a3232656c7532353a636f6d62696e6174696f6e45717569706d656e745175657374746931656931656931313030303435656475363a33303331303075313a31657475323a343575323a3539656c7532353a636f6d62696e6174696f6e45717569706d656e745175657374746931656931656931313030303436656475363a33303331303075313a31657475323a343675333a313131656c7532353a636f6d62696e6174696f6e45717569706d656e745175657374746931656931656931313030303437656475363a33303331303175313a32657475323a343775323a3333656c7532353a636f6d62696e6174696f6e45717569706d656e745175657374746931656931656931313030303438656475363a33303331303175313a32657475323a343875323a3335656c7532353a636f6d62696e6174696f6e45717569706d656e745175657374746931656931656931313030303439656475363a33303331303175313a32657475323a343975323a3435656c7532353a636f6d62696e6174696f6e45717569706d656e745175657374746931656931656931313030303530656475363a33303331303175313a32657475323a353075333a313236656c7532353a636f6d62696e6174696f6e45717569706d656e745175657374746931656931656931313030303531656475363a33303331303175313a32657475323a353175333a313832656c7532353a636f6d62696e6174696f6e45717569706d656e745175657374746931656931656931313030303532656475363a33303331303275313a33657475323a353275323a3735656c7532353a636f6d62696e6174696f6e45717569706d656e745175657374746931656931656931313030303533656475363a33303331303275313a33657475323a353375323a3738656c7532353a636f6d62696e6174696f6e45717569706d656e745175657374746931656931656931313030303534656475363a33303331303275313a33657475323a353475323a3936656c7532353a636f6d62696e6174696f6e45717569706d656e745175657374746931656931656931313030303535656475363a33303331303275313a33657475323a353575333a323032656c7532353a636f6d62696e6174696f6e45717569706d656e745175657374746931656931656931313030303536656475363a33303331303275313a33657475323a353675333a323736656c7532353a636f6d62696e6174696f6e45717569706d656e745175657374746931656931656931313030303537656475363a33303331303275313a33657475323a353775333a313436656c7532353a636f6d62696e6174696f6e45717569706d656e745175657374746931656931656931313030303538656475363a33303331303275313a33657475323a353875333a313530656c7532353a636f6d62696e6174696f6e45717569706d656e745175657374746931656931656931313030303539656475363a33303331303275313a33657475323a353975333a313636656c7532353a636f6d62696e6174696f6e45717569706d656e745175657374746931656931656931313030303630656475363a33303331303275313a33657475323a363075333a323931656c7532353a636f6d62696e6174696f6e45717569706d656e745175657374666931656930656931313030303631656475363a33303331303275313a3365666936316575333a333733656c7532353a636f6d62696e6174696f6e45717569706d656e745175657374666931656930656931313030303632656475363a33303331303375313a3465666936326575333a323331656c7532353a636f6d62696e6174696f6e45717569706d656e745175657374746931656931656931313030303633656475363a33303331303375313a3465746936336575333a323336656c7532353a636f6d62696e6174696f6e45717569706d656e745175657374666931656930656931313030303634656475363a33303331303375313a3465666936346575333a323536656c7532353a636f6d62696e6174696f6e45717569706d656e745175657374746931656931656931313030303635656475363a33303331303375313a3465746936356575333a343038656c7532353a636f6d62696e6174696f6e45717569706d656e745175657374666931656930656931313030303636656475363a33303331303375313a3465666936366575333a353433656c7532353a636f6d62696e6174696f6e45717569706d656e745175657374666931656930656931313030303637656475363a33303331303375313a3465666936376575333a333138656c7532353a636f6d62696e6174696f6e45717569706d656e745175657374666931656930656931313030303638656475363a33303331303375313a3465666936386575333a333234656c7532353a636f6d62696e6174696f6e45717569706d656e745175657374746931656931656931313030303639656475363a33303331303375313a3465746936396575333a333432656c7532353a636f6d62696e6174696f6e45717569706d656e745175657374666931656930656931313030303730656475363a33303331303375313a3465666937306575333a353838656c7532353a636f6d62696e6174696f6e45717569706d656e745175657374666931656930656931313030303731656475363a33303331303375313a3465666937316575333a363836656c7532353a636f6d62696e6174696f6e45717569706d656e745175657374666931656930656931313030303732656475363a33303331303475313a3565666937326575333a343532656c7532353a636f6d62696e6174696f6e45717569706d656e745175657374666931656930656931313030303733656475363a33303331303475313a3565666937336575333a343630656c7532353a636f6d62696e6174696f6e45717569706d656e745175657374666931656930656931313030303734656475363a33303331303475313a3565666937346575333a353030656c7532353a636f6d62696e6174696f6e45717569706d656e745175657374666931656930656931313030303735656475363a33303331303475313a3565666937356575333a373037656c7532353a636f6d62696e6174696f6e45717569706d656e745175657374666931656930656931313030303736656475363a33303331303475313a3565666937366575333a373632656c7532353a636f6d62696e6174696f6e45717569706d656e745175657374666931656930656931313030303737656475363a33303331303475313a3565666937376575333a363336656c7532353a636f6d62696e6174696f6e45717569706d656e745175657374666931656930656931313030303738656475363a33303331303475313a3565666937386575333a363436656c7532353a636f6d62696e6174696f6e45717569706d656e745175657374666931656930656931313030303739656475363a33303331303475313a3565666937396575333a363636656c7532353a636f6d62696e6174696f6e45717569706d656e745175657374666931656930656931313030303830656475363a33303331303475313a3565666938306575333a383138656c7532353a636f6d62696e6174696f6e45717569706d656e745175657374666931656930656931313030303831656475363a33303331303475313a3565666938316575333a383432656c7532353a636f6d62696e6174696f6e45717569706d656e745175657374666931656930656931313030303832656475363a33303331303475313a3565666938326575333a383636656c7532353a636f6d62696e6174696f6e45717569706d656e745175657374746931656931656931313030303833656475363a33303332303075313a31657475323a383375313a39656c7532353a636f6d62696e6174696f6e45717569706d656e745175657374746931656931656931313030303834656475363a33303332303075313a31657475323a383475323a3137656c7532353a636f6d62696e6174696f6e45717569706d656e745175657374746931656931656931313030303835656475363a33303332303075313a31657475323a383575323a3235656c7532353a636f6d62696e6174696f6e45717569706d656e745175657374746931656931656931313030303836656475363a33303332303075313a31657475323a383675323a3639656c7532353a636f6d62696e6174696f6e45717569706d656e745175657374746931656931656931313030303837656475363a33303332303075313a31657475323a383775333a313338656c7532353a636f6d62696e6174696f6e45717569706d656e745175657374746931656931656931313030303838656475363a33303332303175313a32657475323a383875323a3339656c7532353a636f6d62696e6174696f6e45717569706d656e745175657374746931656931656931313030303839656475363a33303332303175313a32657475323a383975323a3431656c7532353a636f6d62696e6174696f6e45717569706d656e745175657374746931656931656931313030303930656475363a33303332303175313a32657475323a393075323a3533656c7532353a636f6d62696e6174696f6e45717569706d656e745175657374746931656931656931313030303931656475363a33303332303175313a32657475323a393175333a313632656c7532353a636f6d62696e6174696f6e45717569706d656e745175657374666931656930656931313030303932656475363a33303332303175313a3265666939326575333a323731656c7532353a636f6d62696e6174696f6e45717569706d656e745175657374746931656931656931313030303933656475363a33303332303275313a33657475323a393375323a3837656c7532353a636f6d62696e6174696f6e45717569706d656e745175657374746931656931656931313030303934656475363a33303332303275313a33657475323a393475323a3930656c7532353a636f6d62696e6174696f6e45717569706d656e745175657374746931656931656931313030303935656475363a33303332303275313a33657475323a393575333a313038656c7532353a636f6d62696e6174696f6e45717569706d656e745175657374746931656931656931313030303936656475363a33303332303275313a33657475323a393675333a333132656c7532353a636f6d62696e6174696f6e45717569706d656e745175657374666931656930656931313030303937656475363a33303332303275313a3365666939376575333a343834656c7532353a636f6d62696e6174696f6e45717569706d656e745175657374746931656931656931313030303938656475363a33303332303375313a34657475323a393875333a313836656c7532353a636f6d62696e6174696f6e45717569706d656e745175657374746931656931656931313030303939656475363a33303332303375313a34657475323a393975333a313930656c7532353a636f6d62696e6174696f6e45717569706d656e745175657374666931656930656931313030313030656475363a33303332303375313a346566693130306575333a323236656c7532353a636f6d62696e6174696f6e45717569706d656e745175657374666931656930656931313030313031656475363a33303332303375313a346566693130316575333a353235656c7532353a636f6d62696e6174696f6e45717569706d656e745175657374666931656930656931313030313032656475363a33303332303375313a346566693130326575333a353730656c7532353a636f6d62696e6174696f6e45717569706d656e745175657374666931656930656931313030313033656475363a33303332303475313a356566693130336575333a333630656c7532353a636f6d62696e6174696f6e45717569706d656e745175657374666931656930656931313030313034656475363a33303332303475313a356566693130346575333a333636656c7532353a636f6d62696e6174696f6e45717569706d656e745175657374666931656930656931313030313035656475363a33303332303475313a356566693130356575333a343434656c7532353a636f6d62696e6174696f6e45717569706d656e745175657374746931656931656931313030313036656475363a33303332303475313a356574693130366575333a373138656c7532353a636f6d62696e6174696f6e45717569706d656e745175657374746931656931656931313030313037656475363a33303332303475313a356574693130376575333a373733656c7532353a636f6d62696e6174696f6e45717569706d656e745175657374746931656931656931313030313038656475363a33303333303075313a31657475333a31303875323a3135656c7532353a636f6d62696e6174696f6e45717569706d656e745175657374746931656931656931313030313039656475363a33303333303075313a31657475333a31303975323a3139656c7532353a636f6d62696e6174696f6e45717569706d656e745175657374746931656931656931313030313130656475363a33303333303075313a31657475333a31313075323a3331656c7532353a636f6d62696e6174696f6e45717569706d656e745175657374746931656931656931313030313131656475363a33303333303075313a31657475333a31313175323a3831656c7532353a636f6d62696e6174696f6e45717569706d656e745175657374746931656931656931313030313132656475363a33303333303075313a31657475333a31313275333a313534656c7532353a636f6d62696e6174696f6e45717569706d656e745175657374746931656931656931313030313133656475363a33303333303175313a32657475333a31313375323a3437656c7532353a636f6d62696e6174696f6e45717569706d656e745175657374746931656931656931313030313134656475363a33303333303175313a32657475333a31313475323a3439656c7532353a636f6d62696e6174696f6e45717569706d656e745175657374746931656931656931313030313135656475363a33303333303175313a32657475333a31313575323a3631656c7532353a636f6d62696e6174696f6e45717569706d656e745175657374746931656931656931313030313136656475363a33303333303175313a32657475333a31313675333a313738656c7532353a636f6d62696e6174696f6e45717569706d656e745175657374666931656930656931313030313137656475363a33303333303175313a326566693131376575333a323936656c7532353a636f6d62696e6174696f6e45717569706d656e745175657374746931656931656931313030313138656475363a33303333303275313a33657475333a31313875333a313032656c7532353a636f6d62696e6174696f6e45717569706d656e745175657374746931656931656931313030313139656475363a33303333303275313a33657475333a31313975333a313035656c7532353a636f6d62696e6174696f6e45717569706d656e745175657374746931656931656931313030313230656475363a33303333303275313a33657475333a31323075333a313233656c7532353a636f6d62696e6174696f6e45717569706d656e745175657374746931656931656931313030313231656475363a33303333303275313a33657475333a31323175333a333438656c7532353a636f6d62696e6174696f6e45717569706d656e745175657374666931656930656931313030313232656475363a33303333303275313a336566693132326575333a353038656c7532353a636f6d62696e6174696f6e45717569706d656e745175657374746931656931656931313030313233656475363a33303333303375313a34657475333a31323375333a323036656c7532353a636f6d62696e6174696f6e45717569706d656e745175657374666931656930656931313030313234656475363a33303333303375313a346566693132346575333a323131656c7532353a636f6d62696e6174696f6e45717569706d656e745175657374666931656930656931313030313235656475363a33303333303375313a346566693132356575333a323636656c7532353a636f6d62696e6174696f6e45717569706d656e745175657374666931656930656931313030313236656475363a33303333303375313a346566693132366575333a353532656c7532353a636f6d62696e6174696f6e45717569706d656e745175657374666931656930656931313030313237656475363a33303333303375313a346566693132376575333a353937656c7532353a636f6d62696e6174696f6e45717569706d656e745175657374666931656930656931313030313238656475363a33303333303475313a356566693132386575333a333934656c7532353a636f6d62696e6174696f6e45717569706d656e745175657374746931656931656931313030313239656475363a33303333303475313a35657475333a31323975333a343031656c7532353a636f6d62696e6174696f6e45717569706d656e745175657374666931656930656931313030313330656475363a33303333303475313a356566693133306575333a343638656c7532353a636f6d62696e6174696f6e45717569706d656e745175657374746931656931656931313030313331656475363a33303333303475313a356574693133316575333a373239656c7532353a636f6d62696e6174696f6e45717569706d656e745175657374746931656931656931313030313332656475363a33303333303475313a356574693133326575333a373834656c7532353a636f6d62696e6174696f6e45717569706d656e745175657374746931656931656931313030313333656475363a33303334303075313a31657475333a31333375323a3230656c7532353a636f6d62696e6174696f6e45717569706d656e745175657374746931656931656931313030313334656475363a33303334303075313a31657475333a31333475323a3237656c7532353a636f6d62696e6174696f6e45717569706d656e745175657374746931656931656931313030313335656475363a33303334303075313a31657475333a31333575323a3433656c7532353a636f6d62696e6174696f6e45717569706d656e745175657374746931656931656931313030313336656475363a33303334303075313a31657475333a31333675323a3933656c7532353a636f6d62696e6174696f6e45717569706d656e745175657374746931656931656931313030313337656475363a33303334303075313a31657475333a31333775333a313730656c7532353a636f6d62696e6174696f6e45717569706d656e745175657374746931656931656931313030313338656475363a33303334303175313a32657475333a31333875323a3535656c7532353a636f6d62696e6174696f6e45717569706d656e745175657374746931656931656931313030313339656475363a33303334303175313a32657475333a31333975323a3537656c7532353a636f6d62696e6174696f6e45717569706d656e745175657374746931656931656931313030313430656475363a33303334303175313a32657475333a31343075323a3732656c7532353a636f6d62696e6174696f6e45717569706d656e745175657374746931656931656931313030313431656475363a33303334303175313a32657475333a31343175333a313938656c7532353a636f6d62696e6174696f6e45717569706d656e745175657374746931656931656931313030313432656475363a33303334303175313a32657475333a31343275333a333336656c7532353a636f6d62696e6174696f6e45717569706d656e745175657374746931656931656931313030313433656475363a33303334303275313a33657475333a31343375333a313137656c7532353a636f6d62696e6174696f6e45717569706d656e745175657374746931656931656931313030313434656475363a33303334303275313a33657475333a31343475333a313230656c7532353a636f6d62696e6174696f6e45717569706d656e745175657374746931656931656931313030313435656475363a33303334303275313a33657475333a31343575333a313432656c7532353a636f6d62696e6174696f6e45717569706d656e745175657374666931656930656931313030313436656475363a33303334303275313a336566693134366575333a333830656c7532353a636f6d62696e6174696f6e45717569706d656e745175657374746931656931656931313030313437656475363a33303334303275313a33657475333a31343775333a353334656c7532353a636f6d62696e6174696f6e45717569706d656e745175657374746931656931656931313030313438656475363a33303334303375313a346574693134386575333a323436656c7532353a636f6d62696e6174696f6e45717569706d656e745175657374666931656930656931313030313439656475363a33303334303375313a346566693134396575333a323531656c7532353a636f6d62696e6174696f6e45717569706d656e745175657374666931656930656931313030313530656475363a33303334303375313a346566693135306575333a323836656c7532353a636f6d62696e6174696f6e45717569706d656e745175657374746931656931656931313030313531656475363a33303334303375313a346574693135316575333a353739656c7532353a636f6d62696e6174696f6e45717569706d656e745175657374746931656931656931313030313532656475363a33303334303375313a346574693135326575333a363236656c7532353a636f6d62696e6174696f6e45717569706d656e745175657374666931656930656931313030313533656475363a33303334303475313a356566693135336575333a343239656c7532353a636f6d62696e6174696f6e45717569706d656e745175657374746931656931656931313030313534656475363a33303334303475313a356574693135346575333a343336656c7532353a636f6d62696e6174696f6e45717569706d656e745175657374666931656930656931313030313535656475363a33303334303475313a356566693135356575333a343932656c7532353a636f6d62696e6174696f6e45717569706d656e745175657374666931656930656931313030313536656475363a33303334303475313a356566693135366575333a373430656c7532353a636f6d62696e6174696f6e45717569706d656e745175657374666931656930656931313030313537656475363a33303334303475313a356566693135376575333a373935656c7532353a636f6d62696e6174696f6e45717569706d656e745175657374666931656930656931313030313538656475363a33303332303475323a31326566693136316575333a333330656c7532353a636f6d62696e6174696f6e45717569706d656e745175657374746931656931656931313030313539656475363a33303332303475323a31326574693136326575333a333130656c7532353a636f6d62696e6174696f6e45717569706d656e745175657374666931656930656931313030313630656475363a33303332303475323a31326566693136336575333a333130656c7532353a636f6d62696e6174696f6e45717569706d656e745175657374666931656930656931313030313631656475363a33303332303475323a31326566693136346575333a333330656c7532353a636f6d62696e6174696f6e45717569706d656e745175657374746931656931656931313030313632656475363a33303332303475323a31326574693136356575333a333330656c7532353a636f6d62696e6174696f6e45717569706d656e745175657374666931656930656931313030313633656475363a33303333303475323a31336566693136366575333a333335656c7532353a636f6d62696e6174696f6e45717569706d656e745175657374746931656931656931313030313634656475363a33303333303475323a31336574693136376575333a333230656c7532353a636f6d62696e6174696f6e45717569706d656e745175657374746931656931656931313030313635656475363a33303333303475323a31336574693136386575333a333230656c7532353a636f6d62696e6174696f6e45717569706d656e745175657374666931656930656931313030313636656475363a33303333303475323a31336566693136396575333a333335656c7532353a636f6d62696e6174696f6e45717569706d656e745175657374666931656930656931313030313637656475363a33303333303475323a31336566693137306575333a333335656c7532353a636f6d62696e6174696f6e45717569706d656e745175657374666931656930656931313030313638656475363a33303330303575323a3530656669313839656933383065656c7532353a636f6d62696e6174696f6e45717569706d656e745175657374666931656930656931313030313639656475363a33303330303575323a3530656669313930656933353565656c7532353a636f6d62696e6174696f6e45717569706d656e745175657374666931656930656931313030313730656475363a33303330303575323a3530656669313931656933353565656c7532353a636f6d62696e6174696f6e45717569706d656e745175657374666931656930656931313030313731656475363a33303330303575323a3530656669313932656933383065656c7532353a636f6d62696e6174696f6e45717569706d656e745175657374666931656930656931313030313732656475363a33303330303575323a3530656669313933656934303065656c7532353a636f6d62696e6174696f6e45717569706d656e745175657374666931656930656931313030313733656475363a33303331303575323a3530656669313934656933383565656c7532353a636f6d62696e6174696f6e45717569706d656e745175657374666931656930656931313030313734656475363a33303331303575323a3530656669313935656933363565656c7532353a636f6d62696e6174696f6e45717569706d656e745175657374666931656930656931313030313735656475363a33303331303575323a3530656669313936656933363565656c7532353a636f6d62696e6174696f6e45717569706d656e745175657374666931656930656931313030313736656475363a33303331303575323a3530656669313937656933383565656c7532353a636f6d62696e6174696f6e45717569706d656e745175657374666931656930656931313030313737656475363a33303331303575323a3530656669313938656933393865656c7532353a636f6d62696e6174696f6e45717569706d656e745175657374666931656930656931313030313738656475363a33303332303575323a3530656669313939656933383065656c7532353a636f6d62696e6174696f6e45717569706d656e745175657374666931656930656931313030313739656475363a33303332303575323a3530656669323030656933363065656c7532353a636f6d62696e6174696f6e45717569706d656e745175657374666931656930656931313030313830656475363a33303332303575323a3530656669323031656933363065656c7532353a636f6d62696e6174696f6e45717569706d656e745175657374666931656930656931313030313831656475363a33303332303575323a3530656669323032656933383065656c7532353a636f6d62696e6174696f6e45717569706d656e745175657374666931656930656931313030313832656475363a33303332303575323a3530656669323033656933393265656c7532353a636f6d62696e6174696f6e45717569706d656e745175657374666931656930656931313030313833656475363a33303333303575323a3530656669323034656933383565656c7532353a636f6d62696e6174696f6e45717569706d656e745175657374666931656930656931313030313834656475363a33303333303575323a3530656669323035656933373065656c7532353a636f6d62696e6174696f6e45717569706d656e745175657374666931656930656931313030313835656475363a33303333303575323a3530656669323036656933373065656c7532353a636f6d62696e6174696f6e45717569706d656e745175657374666931656930656931313030313836656475363a33303333303575323a3530656669323037656933383565656c7532353a636f6d62696e6174696f6e45717569706d656e745175657374666931656930656931313030313837656475363a33303333303575323a3530656669323038656933393465656c7532353a636f6d62696e6174696f6e45717569706d656e745175657374666931656930656931313030313838656475363a33303334303575323a3530656669323039656933393065656c7532353a636f6d62696e6174696f6e45717569706d656e745175657374666931656930656931313030313839656475363a33303334303575323a3530656669323130656933373565656c7532353a636f6d62696e6174696f6e45717569706d656e745175657374666931656930656931313030313930656475363a33303334303575323a3530656669323131656933373565656c7532353a636f6d62696e6174696f6e45717569706d656e745175657374666931656930656931313030313931656475363a33303334303575323a3530656669323132656933393065656c7532353a636f6d62696e6174696f6e45717569706d656e745175657374666931656930656931313030313932656475363a33303334303575323a353065666932313365693339366565656c69313030333332656565")));
            var worldState = new World(MockUtil.MockModernWorldState)
                .SetAvatarState(avatarAddress, avatarState);
            var stateRootHash = worldState.Trie.Hash;
            var tip = new Domain.Model.BlockChain.Block(
                BlockHash.FromString("613dfa26e104465790625ae7bc03fc27a64947c02a9377565ec190405ef7154b"),
                BlockHash.FromString("36456be15af9a5b9b13a02c7ce1e849ae9cba8781ec309010499cdb93e29237d"),
                default(Address),
                0,
                Timestamp: DateTimeOffset.UtcNow,
                StateRootHash: stateRootHash,
                Transactions: ImmutableArray<Transaction>.Empty
            );
            BlockChainRepository.Setup(repository => repository.GetTip())
                .Returns(tip);
            WorldStateRepository.Setup(repository => repository.GetWorldState(stateRootHash))
                .Returns(worldState);

            string query = $@"query {{
                stateQuery {{
                    avatar(avatarAddress: ""{avatarAddress}"") {{
                        name
                    }}
                }}
            }}";
            var queryResult = await ExecuteQueryAsync(query);
            Assert.Null(queryResult.Errors);
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public async Task ActivationKeyNonce(bool trim)
        {
            var privateKey = new PrivateKey();
            var random = new Random();
            var nonce = new byte[10];
            random.NextBytes(nonce);
            var (activationKey, pendingActivationState) = ActivationKey.Create(privateKey, nonce);

            var worldState = new World(MockUtil.MockModernWorldState)
                .SetLegacyState(activationKey.PendingAddress, pendingActivationState.Serialize());
            var stateRootHash = worldState.Trie.Hash;

            var tip = new Domain.Model.BlockChain.Block(
                BlockHash.FromString("613dfa26e104465790625ae7bc03fc27a64947c02a9377565ec190405ef7154b"),
                BlockHash.FromString("36456be15af9a5b9b13a02c7ce1e849ae9cba8781ec309010499cdb93e29237d"),
                default(Address),
                0,
                Timestamp: DateTimeOffset.UtcNow,
                StateRootHash: stateRootHash,
                Transactions: ImmutableArray<Transaction>.Empty
            );

            BlockChainRepository.Setup(repo => repo.GetTip())
                .Returns(tip);
            WorldStateRepository.Setup(repo => repo.GetWorldState(stateRootHash))
                .Returns(worldState);

            var code = activationKey.Encode();
            if (trim)
            {
                code += " ";
            }
            var query = $"query {{ activationKeyNonce(invitationCode: \"{code}\") }}";
            var queryResult = await ExecuteQueryAsync(query);
            var data = (Dictionary<string, object>)((ExecutionNode)queryResult.Data!).ToValue()!;
            var result = (string)data["activationKeyNonce"];
            Assert.Equal(nonce, ByteUtil.ParseHex(result));
        }

        [Theory]
        [InlineData("1", "invitationCode format is invalid.")]
        [InlineData("9330b3287bd2bbc38770c69ae7cd380350c60a1dff9ec41254f3048d5b3eb01c", "invitationCode format is invalid.")]
        public async Task ActivationKeyNonce_ThrowError_WithInvalidFormatCode(string code, string msg)
        {
            var query = $"query {{ activationKeyNonce(invitationCode: \"{code}\") }}";
            var queryResult = await ExecuteQueryAsync(query);
            Assert.NotNull(queryResult.Errors);
            Assert.Single(queryResult.Errors!);
            Assert.Equal(msg, queryResult.Errors!.First().Message);
        }

        [Theory]
        [InlineData("9330b3287bd2bbc38770c69ae7cd380350c60a1dff9ec41254f3048d5b3eb01c/44C889Af1e1e90213Cff5d69C9086c34ecCb60B0", "invitationCode is invalid.")]
        public async Task ActivationKeyNonce_ThrowError_WithOutdatedCode(string code, string msg)
        {
            var activationKey = ActivationKey.Decode(code);

            var worldState = new World(MockUtil.MockModernWorldState)
                .SetLegacyState(activationKey.PendingAddress, Bencodex.Types.Null.Value);
            var stateRootHash = worldState.Trie.Hash;

            var tip = new Domain.Model.BlockChain.Block(
                BlockHash.FromString("613dfa26e104465790625ae7bc03fc27a64947c02a9377565ec190405ef7154b"),
                BlockHash.FromString("36456be15af9a5b9b13a02c7ce1e849ae9cba8781ec309010499cdb93e29237d"),
                default(Address),
                0,
                Timestamp: DateTimeOffset.UtcNow,
                StateRootHash: stateRootHash,
                Transactions: ImmutableArray<Transaction>.Empty
            );

            BlockChainRepository.Setup(repo => repo.GetTip())
                .Returns(tip);
            WorldStateRepository.Setup(repo => repo.GetWorldState(stateRootHash))
                .Returns(worldState);

            var query = $"query {{ activationKeyNonce(invitationCode: \"{code}\") }}";
            var queryResult = await ExecuteQueryAsync(query);
            Assert.NotNull(queryResult.Errors);
            Assert.Single(queryResult.Errors!);
            Assert.Equal(msg, queryResult.Errors!.First().Message);
        }

        [Fact]
        public async Task Balance()
        {
            var address = new PrivateKey().Address;
            var worldState = new World(MockUtil.MockModernWorldState);
            var stateRootHash = worldState.Trie.Hash;

            var tip = new Domain.Model.BlockChain.Block(
                BlockHash.FromString("613dfa26e104465790625ae7bc03fc27a64947c02a9377565ec190405ef7154b"),
                BlockHash.FromString("36456be15af9a5b9b13a02c7ce1e849ae9cba8781ec309010499cdb93e29237d"),
                default(Address),
                0,
                Timestamp: DateTimeOffset.UtcNow,
                StateRootHash: stateRootHash,
                Transactions: ImmutableArray<Transaction>.Empty
            );

            BlockChainRepository.Setup(repo => repo.GetTip())
                .Returns(tip);
            WorldStateRepository.Setup(repo => repo.GetWorldState(stateRootHash))
                .Returns(worldState);

            var query = $@"query {{
                stateQuery {{
                    balance(address: ""{address}"", currency: {{ decimalPlaces: 18, ticker: ""CRYSTAL"" }}) {{
                        quantity
                        currency {{
                            ticker
                            minters
                            decimalPlaces
                        }}
                    }}
                }}
            }}";
            var queryResult = await ExecuteQueryAsync(query);
            Assert.Null(queryResult.Errors);
            var data = (Dictionary<string, object>)((Dictionary<string, object>)((Dictionary<string, object>)((ExecutionNode)queryResult.Data!).ToValue()!)["stateQuery"])["balance"];
            Assert.Equal("0", data["quantity"]);
            var currencyData = (Dictionary<string, object>)data["currency"];
#pragma warning disable CS0618
            // Use of obsolete method Currency.Legacy(): https://github.com/planetarium/lib9c/discussions/1319
            var currency = Currency.Legacy((string)currencyData["ticker"], (byte)currencyData["decimalPlaces"],
                minters: (IImmutableSet<Address>?)currencyData["minters"]);
#pragma warning restore CS0618
            var crystal = CrystalCalculator.CRYSTAL;
#pragma warning disable CS0618
            // Use of obsolete method Currency.Legacy(): https://github.com/planetarium/lib9c/discussions/1319
            Assert.Equal(0 * crystal, 0 * Currency.Legacy(crystal.Ticker, crystal.DecimalPlaces, crystal.Minters));
#pragma warning restore CS0618
            Assert.Equal(0 * CrystalCalculator.CRYSTAL, 0 * currency);
        }

        private NineChroniclesNodeService MakeNineChroniclesNodeService(PrivateKey privateKey)
        {
#pragma warning disable CS0618
            // Use of obsolete method Currency.Legacy(): https://github.com/planetarium/lib9c/discussions/1319
            var goldCurrency = Currency.Legacy("NCG", 2, null);
#pragma warning restore CS0618

            var blockPolicy = new BlockPolicySource().GetPolicy();
            var validatorSetCandidate = new ValidatorSet(new[]
            {
                new Libplanet.Types.Consensus.Validator(ProposerPrivateKey.PublicKey, BigInteger.One),
                new Libplanet.Types.Consensus.Validator(privateKey.PublicKey, BigInteger.One),
            }.ToList());
            Block genesis =
                BlockChain.ProposeGenesisBlock(
                    transactions: ImmutableList<Transaction>.Empty
                        .Add(
                            Transaction.Create(0, ProposerPrivateKey, null,
                                new ActionBase[]
                                {
                                    new InitializeStates(
                                        rankingState: new RankingState0(),
                                        shopState: new ShopState(),
                                        gameConfigState: new GameConfigState(_sheets[nameof(GameConfigSheet)]),
                                        redeemCodeState: new RedeemCodeState(Bencodex.Types.Dictionary.Empty
                                            .Add("address", RedeemCodeState.Address.Serialize())
                                            .Add("map", Bencodex.Types.Dictionary.Empty)
                                        ),
                                        adminAddressState: new AdminState(default, 0),
                                        activatedAccountsState: new ActivatedAccountsState(),
                                        goldCurrencyState: new GoldCurrencyState(goldCurrency),
                                        goldDistributions: new GoldDistribution[] { },
                                        tableSheets: _sheets,
                                        pendingActivationStates: new PendingActivationState[] { }
                                    ),
                                }.ToPlainValues()
                            )
                        )
                        .AddRange(
                            new IAction[]
                            {
                                new Initialize(validatorSetCandidate, ImmutableDictionary<Address, IValue>.Empty),
                            }.Select((sa, nonce) =>
                                Transaction.Create(nonce + 1, ProposerPrivateKey, null,
                                    new[] { sa.PlainValue }))
                        ),
                    privateKey: ProposerPrivateKey
                );

            var consensusPrivateKey = new PrivateKey();
            var properties = new LibplanetNodeServiceProperties
            {
                Host = System.Net.IPAddress.Loopback.ToString(),
                AppProtocolVersion = default,
                GenesisBlock = genesis,
                StorePath = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName()),
                StoreStatesCacheSize = 2,
                SwarmPrivateKey = new PrivateKey(),
                ConsensusPrivateKey = consensusPrivateKey,
                ConsensusPort = null,
                Port = null,
                NoMiner = true,
                Render = false,
                Peers = ImmutableHashSet<BoundPeer>.Empty,
                TrustedAppProtocolVersionSigners = null,
                IceServers = ImmutableList<IceServer>.Empty,
                ConsensusSeeds = ImmutableList<BoundPeer>.Empty,
                ConsensusPeers = ImmutableList<BoundPeer>.Empty,
            };

            return new NineChroniclesNodeService(privateKey, properties, blockPolicy, Planet.Odin, StaticActionLoaderSingleton.Instance);
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
