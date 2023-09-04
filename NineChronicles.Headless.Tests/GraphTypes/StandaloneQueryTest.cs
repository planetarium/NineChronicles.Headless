using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Threading;
using System.Threading.Tasks;
using Bencodex;
using Bencodex.Types;
using GraphQL.Execution;
using Libplanet.Action;
using Libplanet.Action.Sys;
using Libplanet.Blockchain;
using Libplanet.Common;
using Libplanet.Crypto;
using Libplanet.Headless.Hosting;
using Libplanet.KeyStore;
using Libplanet.Net;
using Libplanet.Store;
using Libplanet.Store.Trie;
using Libplanet.Types.Assets;
using Libplanet.Types.Blocks;
using Libplanet.Types.Consensus;
using Libplanet.Types.Tx;
using Nekoyume;
using Nekoyume.Action;
using Nekoyume.Action.Extensions;
using Nekoyume.Action.Loader;
using Nekoyume.Helper;
using Nekoyume.Model;
using Nekoyume.Model.State;
using Nekoyume.Module;
using Nekoyume.TableData;
using NineChronicles.Headless.Properties;
using NineChronicles.Headless.Tests.Common;
using Serilog;
using Xunit;
using Xunit.Abstractions;
using static NineChronicles.Headless.NCActionUtils;

namespace NineChronicles.Headless.Tests.GraphTypes
{
    public partial class StandaloneQueryTest : GraphQLTestBase
    {
        private readonly Dictionary<string, string> _sheets;

        public StandaloneQueryTest(ITestOutputHelper output) : base(output)
        {
            _sheets = TableSheetsImporter.ImportSheets();
        }

        [Fact]
        public async Task GetState()
        {
            Address adminStateAddress = AdminState.Address;
            var result = await ExecuteQueryAsync($"query {{ state(address: \"{adminStateAddress}\") }}");
            var data = (Dictionary<string, object>)((ExecutionNode)result.Data!).ToValue()!;
            IValue rawVal = new Codec().Decode(ByteUtil.ParseHex((string)data!["state"]));
            AdminState adminState = new AdminState((Dictionary)rawVal);

            Assert.Equal(AdminAddress, adminState.AdminAddress);
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

            var result = await ExecuteQueryAsync($"query {{ keyStore {{ decryptedPrivateKey(address: \"{privateKey.ToAddress()}\", passphrase: \"{passphrase}\") }} }}");

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
            var actionEvaluator = new ActionEvaluator(
                _ => null,
                new BlockChainStates(new MemoryStore(), new TrieStateStore(new MemoryKeyValueStore())),
                new NCActionLoader());
            var genesisBlock = BlockChain.ProposeGenesisBlock(actionEvaluator);

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

            var address = privateKey.ToAddress();
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
            PrivateKey userPrivateKey = new PrivateKey();
            var validators = new List<PrivateKey>
            {
                ProposerPrivateKey,
                userPrivateKey,
            }.OrderBy(x => x.ToAddress()).ToList();
            var service = MakeNineChroniclesNodeService(userPrivateKey);
            StandaloneContextFx.NineChroniclesNodeService = service;
            StandaloneContextFx.BlockChain = service.Swarm?.BlockChain;

            var blockChain = StandaloneContextFx.BlockChain;
            for (int i = 0; i < 10; i++)
            {
                Block block = blockChain!.ProposeBlock(
                    userPrivateKey,
                    lastCommit: GenerateBlockCommit(BlockChain.Tip.Index, BlockChain.Tip.Hash, validators));
                blockChain!.Append(block, GenerateBlockCommit(block.Index, block.Hash, validators));
            }

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
            Assert.Equal(privateKey.ToAddress().ToString(), publicKeyResult["address"]);
        }

        [Fact]
        public async Task GoldBalance()
        {
            var userPrivateKey = new PrivateKey();
            var userAddress = userPrivateKey.ToAddress();
            var validators = new List<PrivateKey>
            {
                ProposerPrivateKey, userPrivateKey
            }.OrderBy(x => x.ToAddress()).ToList();
            var service = MakeNineChroniclesNodeService(userPrivateKey);
            StandaloneContextFx.NineChroniclesNodeService = service;
            StandaloneContextFx.BlockChain = service.Swarm?.BlockChain;

            var blockChain = StandaloneContextFx.BlockChain;
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

            Block block = blockChain!.ProposeBlock(
                userPrivateKey,
                lastCommit: GenerateBlockCommit(BlockChain.Tip.Index, BlockChain.Tip.Hash, validators));
            blockChain!.Append(block, GenerateBlockCommit(block.Index, block.Hash, validators));

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
            PrivateKey senderKey = ProposerPrivateKey, recipientKey = new PrivateKey();
            Address sender = senderKey.ToAddress(), recipient = recipientKey.ToAddress();

            Block block = BlockChain.ProposeBlock(
                ProposerPrivateKey,
                lastCommit: GenerateBlockCommit(BlockChain.Tip.Index, BlockChain.Tip.Hash, GenesisValidators));
            BlockChain.Append(block, GenerateBlockCommit(block.Index, block.Hash, GenesisValidators));
            block = BlockChain.ProposeBlock(
                ProposerPrivateKey,
                lastCommit: GenerateBlockCommit(BlockChain.Tip.Index, BlockChain.Tip.Hash, GenesisValidators));
            BlockChain.Append(block, GenerateBlockCommit(block.Index, block.Hash, GenesisValidators));

            var currency = new GoldCurrencyState(
                (Dictionary)LegacyModule.GetState(
                    BlockChain.GetWorldState(),
                    Addresses.GoldCurrency)).Currency;
            Transaction MakeTx(ActionBase action)
            {
                return BlockChain.MakeTransaction(ProposerPrivateKey, new ActionBase[] { action });
            }
            var txs = new[]
            {
                MakeTx(new TransferAsset(sender, recipient, new FungibleAssetValue(currency, 1, 0), memo)),
                MakeTx(new TransferAsset(sender, recipient, new FungibleAssetValue(currency, 1, 0), memo)),
                MakeTx(new TransferAsset(sender, recipient, new FungibleAssetValue(currency, 1, 0), memo)),
            };

            block = BlockChain.ProposeBlock(
                ProposerPrivateKey, lastCommit: GenerateBlockCommit(BlockChain.Tip.Index, BlockChain.Tip.Hash, GenesisValidators));
            BlockChain.Append(block, GenerateBlockCommit(block.Index, block.Hash, GenesisValidators));

            foreach (var tx in txs)
            {
                Assert.NotNull(StandaloneContextFx.Store?.GetTxExecution(block.Hash, tx.Id));
            }

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
            var userAddress = userPrivateKey.ToAddress();
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
            var userAddress = userPrivateKey.ToAddress();
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
            var userAddress = userPrivateKey.ToAddress();
            var validators = new List<PrivateKey>
            {
                ProposerPrivateKey, userPrivateKey
            }.OrderBy(x => x.ToAddress()).ToList();
            var service = MakeNineChroniclesNodeService(userPrivateKey);
            StandaloneContextFx.NineChroniclesNodeService = service;
            StandaloneContextFx.BlockChain = service.Swarm!.BlockChain;
            if (!miner)
            {
                StandaloneContextFx.NineChroniclesNodeService.MinerPrivateKey = null;
            }
            var action = new CreateAvatar
            {
                index = 0,
                hair = 1,
                lens = 2,
                ear = 3,
                tail = 4,
                name = "action",
            };
            var blockChain = StandaloneContextFx.BlockChain;
            var transaction = blockChain.MakeTransaction(userPrivateKey, new ActionBase[] { action });
            blockChain.StageTransaction(transaction);
            Block block = blockChain.ProposeBlock(
                userPrivateKey,
                lastCommit: GenerateBlockCommit(BlockChain.Tip.Index, BlockChain.Tip.Hash, validators));
            blockChain.Append(block, GenerateBlockCommit(block.Index, block.Hash, validators));

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
            var userPrivateKey = new PrivateKey();
            var userAddress = userPrivateKey.ToAddress();
            var validators = new List<PrivateKey>
            {
                ProposerPrivateKey, userPrivateKey
            }.OrderBy(x => x.ToAddress()).ToList();
            var service = MakeNineChroniclesNodeService(userPrivateKey);
            StandaloneContextFx.NineChroniclesNodeService = service;
            StandaloneContextFx.BlockChain = service.Swarm!.BlockChain;
            var action = new CreateAvatar
            {
                index = 0,
                hair = 1,
                lens = 2,
                ear = 3,
                tail = 4,
                name = "action",
            };
            var blockChain = StandaloneContextFx.BlockChain;
            var transaction = blockChain.MakeTransaction(userPrivateKey, new ActionBase[] { action });
            blockChain.StageTransaction(transaction);
            Block block = blockChain.ProposeBlock(
                userPrivateKey,
                lastCommit: GenerateBlockCommit(BlockChain.Tip.Index, BlockChain.Tip.Hash, validators));
            Log.Debug("Appending Block...");
            blockChain.Append(block, GenerateBlockCommit(block.Index, block.Hash, validators));

            var avatarAddress = userAddress.Derive(
                string.Format(
                    CultureInfo.InvariantCulture,
                    CreateAvatar.DeriveFormat,
                    0
                )
            );
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
            var adminPrivateKey = new PrivateKey();
            var adminAddress = adminPrivateKey.ToAddress();
            var activatedAccounts = ImmutableHashSet<Address>.Empty;
            var nonce = new byte[] { 0x00, 0x01, 0x02, 0x03 };
            var privateKey = new PrivateKey();
            (ActivationKey activationKey, PendingActivationState pendingActivation) =
                ActivationKey.Create(privateKey, nonce);
            var pendingActivationStates = new List<PendingActivationState>
            {
                pendingActivation,
            };
            var actionEvaluator = new ActionEvaluator(
                _ => null,
                new BlockChainStates(new MemoryStore(), new TrieStateStore(new MemoryKeyValueStore())),
                new NCActionLoader());
            Block genesis =
                BlockChain.ProposeGenesisBlock(
                    actionEvaluator,
                    transactions: ImmutableList<Transaction>.Empty.Add(
                        Transaction.Create(0, new PrivateKey(), null,
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
                                    pendingActivationStates: pendingActivationStates.ToArray()
                                ),
                            }.ToPlainValues()))
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

            var blockPolicy = NineChroniclesNodeService.GetBlockPolicy(NetworkType.Test, StaticActionLoaderSingleton.Instance);
            var service = new NineChroniclesNodeService(userPrivateKey, properties, blockPolicy, NetworkType.Test, StaticActionLoaderSingleton.Instance);
            StandaloneContextFx.NineChroniclesNodeService = service;
            StandaloneContextFx.BlockChain = service.Swarm?.BlockChain;

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
        [InlineData("9330b3287bd2bbc38770c69ae7cd380350c60a1dff9ec41254f3048d5b3eb01c/44C889Af1e1e90213Cff5d69C9086c34ecCb60B0", "invitationCode is invalid.")]
        public async Task ActivationKeyNonce_Throw_ExecutionError(string code, string msg)
        {
            var adminPrivateKey = new PrivateKey();
            var adminAddress = adminPrivateKey.ToAddress();
            var activatedAccounts = ImmutableHashSet<Address>.Empty;
            var pendingActivationStates = new List<PendingActivationState>();

            var actionEvaluator = new ActionEvaluator(
                _ => null,
                new BlockChainStates(new MemoryStore(), new TrieStateStore(new MemoryKeyValueStore())),
                new NCActionLoader());
            Block genesis =
                BlockChain.ProposeGenesisBlock(
                    actionEvaluator,
                    transactions: ImmutableList<Transaction>.Empty
                        .Add(Transaction.Create(
                            0,
                            new PrivateKey(),
                            null,
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
                                    pendingActivationStates: pendingActivationStates.ToArray()
                                ),
                            }.ToPlainValues()))
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
            var blockPolicy = NineChroniclesNodeService.GetTestBlockPolicy();

            var service = new NineChroniclesNodeService(userPrivateKey, properties, blockPolicy, NetworkType.Test, StaticActionLoaderSingleton.Instance);
            StandaloneContextFx.NineChroniclesNodeService = service;
            StandaloneContextFx.BlockChain = service.Swarm?.BlockChain;

            var query = $"query {{ activationKeyNonce(invitationCode: \"{code}\") }}";
            var queryResult = await ExecuteQueryAsync(query);
            Assert.NotNull(queryResult.Errors);
            Assert.Single(queryResult.Errors!);
            Assert.Equal(msg, queryResult.Errors!.First().Message);

        }

        [Fact]
        public async Task Balance()
        {
            var adminPrivateKey = new PrivateKey();
            var adminAddress = adminPrivateKey.ToAddress();
            var activatedAccounts = ImmutableHashSet<Address>.Empty;
            var pendingActivationStates = new List<PendingActivationState>();

            var actionEvaluator = new ActionEvaluator(
                _ => null,
                new BlockChainStates(new MemoryStore(), new TrieStateStore(new MemoryKeyValueStore())),
                new NCActionLoader());
            Block genesis =
                BlockChain.ProposeGenesisBlock(
                    actionEvaluator,
                    transactions: ImmutableList<Transaction>.Empty.Add(
                        Transaction.Create(0, new PrivateKey(), null,
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
                                    // Use of obsolete method Currency.Legacy(): https://github.com/planetarium/lib9c/discussions/1319
                                    goldCurrencyState: new GoldCurrencyState(Currency.Legacy("NCG", 2, null)),
#pragma warning restore CS0618
                                    goldDistributions: new GoldDistribution[0],
                                    tableSheets: _sheets,
                                    pendingActivationStates: pendingActivationStates.ToArray()
                                ),
                            }.ToPlainValues()))
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
            var blockPolicy = NineChroniclesNodeService.GetTestBlockPolicy();

            var service = new NineChroniclesNodeService(userPrivateKey, properties, blockPolicy, NetworkType.Test, StaticActionLoaderSingleton.Instance);
            StandaloneContextFx.NineChroniclesNodeService = service;
            StandaloneContextFx.BlockChain = service.Swarm?.BlockChain;

            var query = $@"query {{
                stateQuery {{
                    balance(address: ""{adminAddress}"", currency: {{ decimalPlaces: 18, ticker: ""CRYSTAL"" }}) {{
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

            var blockPolicy = NineChroniclesNodeService.GetTestBlockPolicy();
            var validatorSetCandidate = new ValidatorSet(new[]
            {
                new Libplanet.Types.Consensus.Validator(ProposerPrivateKey.PublicKey, BigInteger.One),
                new Libplanet.Types.Consensus.Validator(privateKey.PublicKey, BigInteger.One),
            }.ToList());
            var actionEvaluator = new ActionEvaluator(
                _ => blockPolicy.BlockAction,
                new BlockChainStates(new MemoryStore(), new TrieStateStore(new MemoryKeyValueStore())),
                new NCActionLoader());
            Block genesis =
                BlockChain.ProposeGenesisBlock(
                    actionEvaluator,
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
                                new Initialize(
                                    validatorSetCandidate,
                                    ImmutableDictionary<Address, IImmutableDictionary<Address, IValue>>.Empty),
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

            return new NineChroniclesNodeService(privateKey, properties, blockPolicy, NetworkType.Test, StaticActionLoaderSingleton.Instance);
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
