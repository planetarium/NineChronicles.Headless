using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using GraphQL;
using GraphQL.Subscription;
using Libplanet;
using Libplanet.Assets;
using Libplanet.Blockchain;
using Libplanet.Crypto;
using Libplanet.Net;
using Libplanet.Headless;
using Nekoyume.Model.State;
using Nekoyume.TableData;
using NineChronicles.Headless.GraphTypes;
using NineChronicles.Headless.GraphTypes.States;
using NineChronicles.Headless.Tests.Common.Actions;
using Xunit;
using Xunit.Abstractions;

namespace NineChronicles.Headless.Tests.GraphTypes
{
    public class StandaloneSubscriptionTest : GraphQLTestBase
    {
        public StandaloneSubscriptionTest(ITestOutputHelper output) : base(output)
        {
        }

        [Fact]
        public async Task SubscribeTipChangedEvent()
        {
            var miner = new Address();

            const int repeat = 10;
            foreach (long index in Enumerable.Range(1, repeat))
            {
                await BlockChain.MineBlock(miner);

                var result = await ExecuteQueryAsync("subscription { tipChanged { index hash } }");

                Assert.IsType<SubscriptionExecutionResult>(result);
                var subscribeResult = (SubscriptionExecutionResult) result;
                Assert.Equal(index, BlockChain.Tip.Index);
                var stream = subscribeResult.Streams.Values.FirstOrDefault();
                var rawEvents = await stream.Take((int)index);
                Assert.NotNull(rawEvents);

                var events = (Dictionary<string, object>) rawEvents.Data;
                var tipChangedEvent = (Dictionary<string, object>) events["tipChanged"];
                Assert.Equal(index, tipChangedEvent["index"]);
                Assert.Equal(BlockChain[index].Hash.ToByteArray(), ByteUtil.ParseHex((string) tipChangedEvent["hash"]));
            }
        }

        [Fact]
        public async Task SubscribePreloadProgress()
        {
            var cts = new CancellationTokenSource();

            var apvPrivateKey = new PrivateKey();
            var apv = AppProtocolVersion.Sign(apvPrivateKey, 0);
            var genesisBlock = BlockChain<EmptyAction>.MakeGenesisBlock(
                HashAlgorithmType.Of<SHA256>()
            );

            // 에러로 인하여 NineChroniclesNodeService 를 사용할 수 없습니다. https://git.io/JfS0M
            // 따라서 LibplanetNodeService로 비슷한 환경을 맞춥니다.
            // 1. 노드를 생성합니다.
            var seedNode = CreateLibplanetNodeService(genesisBlock, apv, apvPrivateKey.PublicKey);
            await StartAsync(seedNode.Swarm, cts.Token);

            // 2. Progress를 넘겨 preloadProgress subscription 과 연결합니다.
            var service = CreateLibplanetNodeService(
                genesisBlock,
                apv,
                apvPrivateKey.PublicKey,
                new Progress<PreloadState>(state =>
                {
                    StandaloneContextFx.PreloadStateSubject.OnNext(state);
                }),
                new [] { seedNode.Swarm.AsPeer });

            var miner = new PrivateKey().ToAddress();
            await seedNode.BlockChain.MineBlock(miner);
            var result = await ExecuteQueryAsync("subscription { preloadProgress { currentPhase totalPhase extra { type currentCount totalCount } } }");
            Assert.IsType<SubscriptionExecutionResult>(result);

            _ = service.StartAsync(cts.Token);

            await service.PreloadEnded.WaitAsync(cts.Token);

            var subscribeResult = (SubscriptionExecutionResult) result;
            var stream = subscribeResult.Streams.Values.FirstOrDefault();

            // BlockHashDownloadState  : 2
            // BlockDownloadState      : 1
            // BlockVerificationState  : 1
            // ActionExecutionState    : 1
            const int preloadStatesCount = 5;
            var preloadProgressRecords =
                new List<(long currentPhase, long totalPhase, string type, long currentCount, long totalCount)>();
            var expectedPreloadProgress = new[]
            {
                (1L, 5L, "BlockHashDownloadState", 0L, 0L),
                (1L, 5L, "BlockHashDownloadState", 1L, 1L),
                (2L, 5L, "BlockDownloadState", 1L, 1L),
                (3L, 5L, "BlockVerificationState", 1L, 1L),
                (5L, 5L, "ActionExecutionState", 1L, 1L),
            }.ToImmutableHashSet();
            foreach (var index in Enumerable.Range(1, preloadStatesCount))
            {
                var rawEvents = await stream.Take(index);
                var preloadProgress = (Dictionary<string, object>)((Dictionary<string, object>)rawEvents.Data)["preloadProgress"];
                var preloadProgressExtra = (Dictionary<string, object>)preloadProgress["extra"];

                preloadProgressRecords.Add((
                    (long)preloadProgress["currentPhase"],
                    (long)preloadProgress["totalPhase"],
                    (string)preloadProgressExtra["type"],
                    (long)preloadProgressExtra["currentCount"],
                    (long)preloadProgressExtra["totalCount"]));
            }

            Assert.Equal(expectedPreloadProgress, preloadProgressRecords.ToImmutableHashSet());

            await seedNode.StopAsync(cts.Token);
            await service.StopAsync(cts.Token);
        }

        [Fact(Timeout = 15000)]
        public async Task SubscribeDifferentAppProtocolVersionEncounter()
        {   
            var result = await ExecuteQueryAsync(@"
                subscription {
                    differentAppProtocolVersionEncounter {
                        peer
                        peerVersion {
                            version
                            signer
                            signature
                            extra
                        }
                        localVersion {
                            version
                            signer
                            signature
                            extra
                        }
                    }
                }
            ");
            var subscribeResult = (SubscriptionExecutionResult) result;
            var stream = subscribeResult.Streams.Values.FirstOrDefault();
            Assert.NotNull(stream);

            await Assert.ThrowsAsync<TimeoutException>(async () =>
            {
                await stream.Take(1).Timeout(TimeSpan.FromMilliseconds(5000)).FirstAsync();
            });

            var apvPrivateKey = new PrivateKey();
            var apv1 = AppProtocolVersion.Sign(apvPrivateKey, 1);
            var apv2 = AppProtocolVersion.Sign(apvPrivateKey, 0);
            var peer = new Peer(apvPrivateKey.PublicKey);
            StandaloneContextFx.DifferentAppProtocolVersionEncounterSubject.OnNext(
                new DifferentAppProtocolVersionEncounter(peer, apv1, apv2)
            );
            var rawEvents = await stream.Take(1);
            var rawEvent = (Dictionary<string, object>)rawEvents.Data;
            var differentAppProtocolVersionEncounter =
                (Dictionary<string, object>)rawEvent["differentAppProtocolVersionEncounter"];
            Assert.Equal(
                peer.ToString(),
                differentAppProtocolVersionEncounter["peer"]
            );
            var peerVersion =
                (Dictionary<string, object>)differentAppProtocolVersionEncounter["peerVersion"];
            Assert.Equal(apv1.Version, peerVersion["version"]);
            Assert.Equal(apv1.Signer, new Address(((string)peerVersion["signer"]).Substring(2)));
            Assert.Equal(apv1.Signature, ByteUtil.ParseHex((string)peerVersion["signature"]));
            Assert.Equal(apv1.Extra, peerVersion["extra"]);
            var localVersion =
                (Dictionary<string, object>)differentAppProtocolVersionEncounter["localVersion"];
            Assert.Equal(apv2.Version, localVersion["version"]);
            Assert.Equal(apv2.Signer, new Address(((string)localVersion["signer"]).Substring(2)));
            Assert.Equal(apv2.Signature, ByteUtil.ParseHex((string)localVersion["signature"]));
            Assert.Equal(apv2.Extra, localVersion["extra"]);
        }

        [Fact(Timeout = 15000)]
        public async Task SubscribeNodeException()
        {   
            var result = await ExecuteQueryAsync(@"
                subscription {
                    nodeException {
                        code
                        message
                    }
                }
            ");
            var subscribeResult = (SubscriptionExecutionResult) result;
            var stream = subscribeResult.Streams.Values.FirstOrDefault();
            Assert.NotNull(stream);

            await Assert.ThrowsAsync<TimeoutException>(async () =>
            {
                await stream.Take(1).Timeout(TimeSpan.FromMilliseconds(5000)).FirstAsync();
            });

            const Libplanet.Headless.NodeExceptionType code = (Libplanet.Headless.NodeExceptionType) 0x01;
            const string message = "This is test message.";
            StandaloneContextFx.NodeExceptionSubject.OnNext(new NodeException(code, message));
            var rawEvents = await stream.Take(1);
            var rawEvent = (Dictionary<string, object>)rawEvents.Data;
            var nodeException =
                (Dictionary<string, object>)rawEvent["nodeException"];
            Assert.Equal((int)code, nodeException["code"]);
            Assert.Equal(message, nodeException["message"]);
        }

        [Fact]
        public async Task SubscribeMonsterCollectionState()
        {
            ExecutionResult result = await ExecuteQueryAsync(@"
                subscription {
                    monsterCollectionState {
                        address
                        level
                        expiredBlockIndex
                        startedBlockIndex
                        receivedBlockIndex
                        rewardLevel
                    }
                }"
            );
            Assert.IsType<SubscriptionExecutionResult>(result);
            SubscriptionExecutionResult subscribeResult = (SubscriptionExecutionResult) result;
            IObservable<ExecutionResult> stream = subscribeResult.Streams.Values.First();
            Assert.NotNull(stream);

            MonsterCollectionState monsterCollectionState = new MonsterCollectionState(default, 1, 2, Fixtures.TableSheetsFX.MonsterCollectionRewardSheet);
            StandaloneContextFx.MonsterCollectionStateSubject.OnNext(monsterCollectionState);
            ExecutionResult rawEvents = await stream.Take(1);
            Dictionary<string, object> rawEvent = (Dictionary<string, object>)rawEvents.Data;
            Dictionary<string, object> subject =
                (Dictionary<string, object>)rawEvent["monsterCollectionState"];
            Dictionary<string, object> expected = new Dictionary<string, object>
            {
                ["address"] = monsterCollectionState.address.ToString(),
                ["level"] = 1L,
                ["expiredBlockIndex"] = 201602L,
                ["startedBlockIndex"] = 2L,
                ["receivedBlockIndex"] = 0L,
                ["rewardLevel"] = 0L,
            };
            Assert.Equal(expected, subject);
        }

        [Theory]
        [InlineData(100, 0, "100.00", true)]
        [InlineData(0, 2, "0.02", false)]
        [InlineData(10, 2, "10.02", true)]
        public async Task SubscribeMonsterCollectionStatus(int major, int minor, string decimalString, bool lockup)
        {
            ExecutionResult result = await ExecuteQueryAsync(@"
                subscription {
                    monsterCollectionStatus {
                        fungibleAssetValue {
                            quantity
                            currency
                        }
                        rewardInfos {
                            itemId
                            quantity
                        },
                        lockup
                    }
                }"
            );
            Assert.IsType<SubscriptionExecutionResult>(result);
            SubscriptionExecutionResult subscribeResult = (SubscriptionExecutionResult) result;
            IObservable<ExecutionResult> stream = subscribeResult.Streams.Values.First();
            Assert.NotNull(stream);

            Currency currency = new Currency("NCG", 2, minter: null);
            FungibleAssetValue fungibleAssetValue = new FungibleAssetValue(currency, major, minor);
            StandaloneContextFx.MonsterCollectionStatusSubject.OnNext(
                new MonsterCollectionStatus(
                    fungibleAssetValue, 
                    new List<MonsterCollectionRewardSheet.RewardInfo>
                    {
                        new MonsterCollectionRewardSheet.RewardInfo("1", "1")
                    },
                    lockup
                )
            );
            ExecutionResult rawEvents = await stream.Take(1);
            Dictionary<string, object> rawEvent = (Dictionary<string, object>)rawEvents.Data;
            Dictionary<string, object> statusSubject =
                (Dictionary<string, object>)rawEvent["monsterCollectionStatus"];
            Dictionary<string, object> expected = new Dictionary<string, object>
            {
                ["fungibleAssetValue"] = new Dictionary<string, object>
                {
                    ["currency"] = "NCG",
                    ["quantity"] = decimal.Parse(decimalString),
                },
                ["rewardInfos"] = new List<object>
                {
                    new Dictionary<string, object>
                    {
                        ["quantity"] = 1,
                        ["itemId"] = 1,
                    }
                },
                ["lockup"] = lockup,
            };
            Assert.Equal(expected, statusSubject);
        }
    }
}
