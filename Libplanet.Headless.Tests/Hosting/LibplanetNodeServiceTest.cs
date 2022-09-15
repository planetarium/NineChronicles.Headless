using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using Bencodex.Types;
using Libplanet.Action;
using Libplanet.Blockchain;
using Libplanet.Blockchain.Policies;
using Libplanet.Crypto;
using Libplanet.Net;
using Libplanet.Headless.Hosting;
using Xunit;

namespace Libplanet.Headless.Tests.Hosting
{
    public class LibplanetNodeServiceTest
    {
        [Fact]
        public void Constructor()
        {
            var genesisBlock = BlockChain<DummyAction>.ProposeGenesisBlock();
            var service = new LibplanetNodeService<DummyAction>(
                new LibplanetNodeServiceProperties<DummyAction>()
                {
                    AppProtocolVersion = new AppProtocolVersion(),
                    GenesisBlock = genesisBlock,
                    SwarmPrivateKey = new PrivateKey(),
                    StoreStatesCacheSize = 2,
                    StorePath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString()),
                    Host = IPAddress.Loopback.ToString(),
                },
                blockPolicy: new BlockPolicy<DummyAction>(),
                stagePolicy: new VolatileStagePolicy<DummyAction>(),
                renderers: null,
                preloadProgress: null,
                exceptionHandlerAction: (code, msg) => throw new Exception($"{code}, {msg}"),
                preloadStatusHandlerAction: isPreloadStart => { }
            );

            Assert.NotNull(service);
        }

        [Fact]
        public void PropertiesMustContainGenesisBlockOrPath()
        {
            var consensusPrivateKey = new BlsPrivateKey();
            Assert.Throws<ArgumentException>(() =>
            {
                var service = new LibplanetNodeService<DummyAction>(
                    new LibplanetNodeServiceProperties<DummyAction>()
                    {
                        AppProtocolVersion = new AppProtocolVersion(),
                        SwarmPrivateKey = new PrivateKey(),
                        ConsensusPrivateKey = consensusPrivateKey,
                        ConsensusPort = null,
                        Validators = new List<BlsPublicKey>()
                        {
                            consensusPrivateKey.PublicKey,
                        },
                        StoreStatesCacheSize = 2,
                        Host = IPAddress.Loopback.ToString(),
                    },
                    blockPolicy: new BlockPolicy<DummyAction>(),
                    stagePolicy: new VolatileStagePolicy<DummyAction>(),
                    renderers: null,
                    preloadProgress: null,
                    exceptionHandlerAction: (code, msg) => throw new Exception($"{code}, {msg}"),
                    preloadStatusHandlerAction: isPreloadStart => { }
                );
            });
        }

        private class DummyAction : IAction
        {
            IValue IAction.PlainValue => Dictionary.Empty;

            IAccountStateDelta IAction.Execute(IActionContext context)
            {
                return context.PreviousStates;
            }

            void IAction.LoadPlainValue(IValue plainValue)
            {
            }
        }
    }
}
