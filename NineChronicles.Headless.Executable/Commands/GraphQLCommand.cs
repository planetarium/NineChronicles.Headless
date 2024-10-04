using System;
using System.Collections.Generic;
using System.IO;
using GraphQL.Server;
using GraphQL.Utilities;
using Lib9c.Renderers;
using Libplanet.Action.State;
using Libplanet.Blockchain;
using Libplanet.Blockchain.Policies;
using Libplanet.Crypto;
using Libplanet.Headless.Hosting;
using Libplanet.Net;
using Libplanet.Store;
using Libplanet.Store.Trie;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Nekoyume;
using Nekoyume.Action.Loader;
using NineChronicles.Headless.GraphTypes;

namespace NineChronicles.Headless.Executable.Commands;

public class GraphQLCommand
{
    [Cocona.Command]
    public void Schema()
    {
        var serviceCollection = new ServiceCollection();
        
        serviceCollection.AddSingleton<StateMemoryCache>();
        serviceCollection.AddSingleton<IConfiguration>(new ConfigurationBuilder().Build());
        serviceCollection.AddSingleton<BlockRenderer>();
        serviceCollection.AddSingleton<ActionRenderer>();
        serviceCollection.AddSingleton<ExceptionRenderer>();
        serviceCollection.AddSingleton<NodeStatusRenderer>();
        serviceCollection.AddSingleton<IBlockChainStates>(
            new BlockChainStates(
                new MemoryStore(),
                new TrieStateStore(new MemoryKeyValueStore())));
        serviceCollection.AddSingleton<string>("STRING");  // Invalid usage but not care in this case.
        serviceCollection.AddSingleton<RpcContext>();
        serviceCollection.AddSingleton<ActionEvaluationPublisher>(services => new ActionEvaluationPublisher(
            services.GetRequiredService<BlockRenderer>(),
            services.GetRequiredService<ActionRenderer>(),
            services.GetRequiredService<ExceptionRenderer>(),
            services.GetRequiredService<NodeStatusRenderer>(),
            services.GetRequiredService<IBlockChainStates>(),
            "host",
            0,
            services.GetRequiredService<RpcContext>(),
            services.GetRequiredService<StateMemoryCache>()
        ));
        serviceCollection.AddSingleton(new NineChroniclesNodeService(
            new PrivateKey(),
            new LibplanetNodeServiceProperties
            {
                SwarmPrivateKey = new PrivateKey(),
                Host = "localhost",
                IceServers = Array.Empty<IceServer>(),
                StorePath = Path.GetRandomFileName(),
                GenesisBlock = BlockChain.ProposeGenesisBlock(),
            },
            new BlockPolicy(),
            Planet.Heimdall,
            new NCActionLoader()));
        serviceCollection.AddSingleton<StandaloneContext>(services => new StandaloneContext
        {
            NineChroniclesNodeService = services.GetRequiredService<NineChroniclesNodeService>(),
        });

        serviceCollection.AddGraphQL()
            .AddLibplanetExplorer()
            .AddGraphTypes(typeof(StandaloneSchema));

        IServiceProvider serviceProvider = serviceCollection.BuildServiceProvider();
        var schema = new StandaloneSchema(serviceProvider);
        var printer = new SchemaPrinter(schema);

        Console.WriteLine(printer.Print());
    }
}
