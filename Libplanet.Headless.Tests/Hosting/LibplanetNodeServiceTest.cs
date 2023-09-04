using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Reflection;
using Bencodex.Types;
using Libplanet.Action;
using Libplanet.Action.Loader;
using Libplanet.Blockchain;
using Libplanet.Blockchain.Policies;
using Libplanet.Crypto;
using Libplanet.Net;
using Libplanet.Headless.Hosting;
using Libplanet.Action.State;
using Libplanet.Store;
using Libplanet.Store.Trie;
using Xunit;

namespace Libplanet.Headless.Tests.Hosting
{
    public class LibplanetNodeServiceTest
    {
        [Fact]
        public void Constructor()
        {
            var policy = new BlockPolicy();
            var stagePolicy = new VolatileStagePolicy();
            var blockChainStates = new BlockChainStates(
                new MemoryStore(),
                new TrieStateStore(new MemoryKeyValueStore()));
            var actionLoader = new SingleActionLoader(typeof(DummyAction));
            var actionEvaluator = new ActionEvaluator(
                _ => policy.BlockAction,
                blockChainStates,
                actionLoader);
            var genesisBlock = BlockChain.ProposeGenesisBlock(actionEvaluator);
            var service = new LibplanetNodeService(
                new LibplanetNodeServiceProperties()
                {
                    AppProtocolVersion = new AppProtocolVersion(),
                    GenesisBlock = genesisBlock,
                    SwarmPrivateKey = new PrivateKey(),
                    StoreStatesCacheSize = 2,
                    StorePath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString()),
                    Host = IPAddress.Loopback.ToString(),
                    IceServers = new List<IceServer>(),
                },
                blockPolicy: policy,
                stagePolicy: stagePolicy,
                renderers: null,
                preloadProgress: null,
                exceptionHandlerAction: (code, msg) => throw new Exception($"{code}, {msg}"),
                preloadStatusHandlerAction: isPreloadStart => { },
                actionLoader: actionLoader
            );

            Assert.NotNull(service);
        }

        [Fact]
        public void PropertiesMustContainGenesisBlockOrPath()
        {
            Assert.Throws<ArgumentException>(() =>
            {
                IActionLoader actionLoader = new SingleActionLoader(typeof(DummyAction));
                var service = new LibplanetNodeService(
                    new LibplanetNodeServiceProperties()
                    {
                        AppProtocolVersion = new AppProtocolVersion(),
                        SwarmPrivateKey = new PrivateKey(),
                        ConsensusPrivateKey = new PrivateKey(),
                        StoreStatesCacheSize = 2,
                        Host = IPAddress.Loopback.ToString(),
                        IceServers = new List<IceServer>(),
                    },
                    blockPolicy: new BlockPolicy(),
                    stagePolicy: new VolatileStagePolicy(),
                    renderers: null,
                    preloadProgress: null,
                    exceptionHandlerAction: (code, msg) => throw new Exception($"{code}, {msg}"),
                    preloadStatusHandlerAction: isPreloadStart => { },
                    actionLoader: actionLoader
                );
            });
        }

        private class DummyAction : IAction
        {
            IValue IAction.PlainValue => Dictionary.Empty;

            IWorld IAction.Execute(IActionContext context)
            {
                return context.PreviousState;
            }

            void IAction.LoadPlainValue(IValue plainValue)
            {
            }
        }
    }
}
