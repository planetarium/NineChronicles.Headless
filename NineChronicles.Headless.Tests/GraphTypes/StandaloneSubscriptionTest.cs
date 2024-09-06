using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Net;
using System.Numerics;
using System.Reactive.Linq;
using System.Threading;
using System.Threading.Tasks;
using Bencodex.Types;
using GraphQL;
using GraphQL.Execution;
using GraphQL.NewtonsoftJson;
using GraphQL.Subscription;
using Libplanet.Common;
using Libplanet.Action;
using Libplanet.Action.Loader;
using Libplanet.Action.Sys;
using Libplanet.Types.Assets;
using Libplanet.Types.Blocks;
using Libplanet.Blockchain;
using Libplanet.Types.Consensus;
using Libplanet.Crypto;
using Libplanet.Headless;
using Libplanet.Net;
using Libplanet.Store;
using Libplanet.Store.Trie;
using Libplanet.Types.Tx;
using Nekoyume.Action;
using Nekoyume.Model.State;
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
            var miner = new PrivateKey();

            const int repeat = 10;
            foreach (long index in Enumerable.Range(1, repeat))
            {
                Block block = BlockChain.ProposeBlock(
                    ProposerPrivateKey,
                    lastCommit: GenerateBlockCommit(BlockChain.Tip.Index, BlockChain.Tip.Hash, GenesisValidators));
                BlockChain.Append(block, GenerateBlockCommit(block.Index, block.Hash, GenesisValidators));

                // var data = (Dictionary<string, object>)((ExecutionNode) result.Data!).ToValue()!;
                Assert.Equal(index, BlockChain.Tip.Index);
                await Task.Delay(TimeSpan.FromSeconds(1));

                var result = await ExecuteSubscriptionQueryAsync("subscription { tipChanged { index hash } }");
                Assert.IsType<SubscriptionExecutionResult>(result);
                var subscribeResult = (SubscriptionExecutionResult)result;
                var stream = subscribeResult.Streams!.Values.FirstOrDefault();
                var rawEvents = await stream.Take(1);
                Assert.NotNull(rawEvents);

                var events = (Dictionary<string, object>)((ExecutionNode)rawEvents.Data!).ToValue()!;
                var tipChangedEvent = (Dictionary<string, object>)events["tipChanged"];
                Assert.Equal(index, tipChangedEvent["index"]);
                Assert.Equal(BlockChain[index].Hash.ToByteArray(), ByteUtil.ParseHex((string)tipChangedEvent["hash"]));
            }
        }

        [Fact]
        public async Task SubscribeTx()
        {
            const string query = @"
            subscription {
                tx (actionType: ""grinding2"") {
                    transaction { id }
                    txResult { blockIndex }
                }
            }
            ";
            var response = await ExecuteSubscriptionQueryAsync(query);

            Assert.IsType<SubscriptionExecutionResult>(response);
            var result = (SubscriptionExecutionResult)response;
            var stream = result.Streams!.Values.FirstOrDefault();
            var observable = stream.Take(1);

            var targetAction = new Grinding { AvatarAddress = new Address(), EquipmentIds = new List<Guid>() };
            var nonTargetAction = new DailyReward { avatarAddress = new Address() };

            Task<ExecutionResult> task = Task.Run(async () => await observable);
            var (block, transactions) = AppendBlock(targetAction, nonTargetAction);
            Assert.NotNull(block);
            Assert.NotNull(transactions);

            var rawEvents = await task;
            Assert.NotNull(rawEvents);

            var events = (Dictionary<string, object>)((ExecutionNode)rawEvents.Data!).ToValue();
            var tx = (Dictionary<string, object>)events["tx"];
            var transaction = (Dictionary<string, object>)tx["transaction"];
            var txResult = (Dictionary<string, object>)tx["txResult"];
            Assert.Equal(transactions[0].Id.ToString(), transaction["id"]);
            Assert.Equal(block.Index, txResult["blockIndex"]);
        }

        private (Block block, List<Transaction> transactions) AppendBlock(params IAction[] actions)
        {
            var transactions = actions.Select(action => Transaction.Create(
                0,
                new PrivateKey(),
                BlockChain.Genesis.Hash,
                new[] { action.PlainValue }))
                .ToList();
            transactions.ForEach(transaction => BlockChain.StageTransaction(transaction));

            var block = BlockChain.ProposeBlock(
                ProposerPrivateKey,
                lastCommit: GenerateBlockCommit(BlockChain.Tip.Index, BlockChain.Tip.Hash, GenesisValidators));
            BlockChain.Append(block, GenerateBlockCommit(block.Index, block.Hash, GenesisValidators));

            return (block, transactions);
        }

        [Fact(Timeout = 25000)]
        public async Task SubscribeDifferentAppProtocolVersionEncounter()
        {
            var result = await ExecuteSubscriptionQueryAsync(@"
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
            var subscribeResult = (SubscriptionExecutionResult)result;
            var stream = subscribeResult.Streams!.Values.FirstOrDefault();
            Assert.NotNull(stream);

            await Assert.ThrowsAsync<TimeoutException>(async () =>
            {
                await stream.Take(1).Timeout(TimeSpan.FromMilliseconds(5000)).FirstAsync();
            });

            var apvPrivateKey = new PrivateKey();
            var apv1 = AppProtocolVersion.Sign(apvPrivateKey, 1);
            var apv2 = AppProtocolVersion.Sign(apvPrivateKey, 0);
            var peer = new BoundPeer(apvPrivateKey.PublicKey, new DnsEndPoint("0.0.0.0", 0));
            _ = Task.Run(async () =>
            {
                await Task.Delay(TimeSpan.FromSeconds(1));
                StandaloneContextFx.DifferentAppProtocolVersionEncounterSubject.OnNext(
                    new DifferentAppProtocolVersionEncounter(peer, apv1, apv2)
                );
            });
            var rawEvents = await stream.Take(1);
            var rawEvent = (Dictionary<string, object>)((ExecutionNode)rawEvents.Data!).ToValue()!;
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
            var result = await ExecuteSubscriptionQueryAsync(@"
                subscription {
                    nodeException {
                        code
                        message
                    }
                }
            ");
            var subscribeResult = (SubscriptionExecutionResult)result;
            var stream = subscribeResult.Streams!.Values.FirstOrDefault();
            Assert.NotNull(stream);

            await Assert.ThrowsAsync<TimeoutException>(async () =>
            {
                await stream.Take(1).Timeout(TimeSpan.FromMilliseconds(5000)).FirstAsync();
            });

            const Libplanet.Headless.NodeExceptionType code = (Libplanet.Headless.NodeExceptionType)0x01;
            const string message = "This is test message.";
            _ = Task.Run(async () =>
            {
                await Task.Delay(TimeSpan.FromSeconds(1));
                StandaloneContextFx.NodeExceptionSubject.OnNext(new NodeException(code, message));
            });
            var rawEvents = await stream.Take(1);
            var rawEvent = (Dictionary<string, object>)((ExecutionNode)rawEvents.Data!).ToValue()!;
            var nodeException =
                (Dictionary<string, object>)rawEvent["nodeException"];
            Assert.Equal((int)code, nodeException["code"]);
            Assert.Equal(message, nodeException["message"]);
        }

        [Theory]
        [InlineData(100, 0, "100.00")]
        [InlineData(0, 2, "0.02")]
        [InlineData(10, 2, "10.02")]
        public async Task SubscribeBalanceByAgent(int major, int minor, string decimalString)
        {
            var address = new Address();
            Assert.Empty(StandaloneContextFx.AgentAddresses);
            ExecutionResult result = await ExecuteSubscriptionQueryAsync($@"
                subscription {{
                    balanceByAgent(address: ""{address}"")
                }}"
            );
            Assert.IsType<SubscriptionExecutionResult>(result);
            SubscriptionExecutionResult subscribeResult = (SubscriptionExecutionResult)result;
            IObservable<ExecutionResult> stream = subscribeResult.Streams!.Values.First();
            Assert.NotNull(stream);
            Assert.NotEmpty(StandaloneContextFx.AgentAddresses);

#pragma warning disable CS0618
            // Use of obsolete method Currency.Legacy(): https://github.com/planetarium/lib9c/discussions/1319
            Currency currency = Currency.Legacy("NCG", 2, null);
#pragma warning restore CS0618
            FungibleAssetValue fungibleAssetValue = new FungibleAssetValue(currency, major, minor);
            StandaloneContextFx.AgentAddresses[address].OnNext(fungibleAssetValue.GetQuantityString(true));
            ExecutionResult rawEvents = await stream.Take(1);
            var data = ((RootExecutionNode)rawEvents.Data.GetValue()).SubFields![0].Result!;
            Assert.Equal(decimalString, data);
        }
    }
}
