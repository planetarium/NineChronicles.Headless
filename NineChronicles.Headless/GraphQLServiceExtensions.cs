using System;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using GraphQL.Types;
using Libplanet.Explorer.GraphTypes;
using Libplanet.Explorer.Interfaces;
using Libplanet.Explorer.Mutations;
using Libplanet.Explorer.Queries;
using Libplanet.Explorer.Schemas;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace NineChronicles.Headless
{
    public static class GraphQLServiceExtensions
    {
        public static IServiceCollection AddGraphTypes(this IServiceCollection services)
        {
            var graphTypes = Assembly.GetAssembly(typeof(GraphQLServiceExtensions))!.GetTypes().Where(
                type => type.Namespace is { } @namespace &&
                        @namespace.StartsWith($"{nameof(NineChronicles)}.{nameof(Headless)}.{nameof(GraphTypes)}") &&
                        (typeof(IGraphType).IsAssignableFrom(type) || typeof(ISchema).IsAssignableFrom(type)) &&
                        !type.IsAbstract);

            foreach (Type graphType in graphTypes)
            {
                services.TryAddSingleton(graphType);
            }

            return services;
        }

        public static IServiceCollection AddLibplanetScalarTypes(this IServiceCollection services)
        {
            services.TryAddSingleton<AddressType>();
            services.TryAddSingleton<ByteStringType>();
            services.TryAddSingleton<Libplanet.Explorer.GraphTypes.PublicKeyType>();
            services.TryAddSingleton<Libplanet.Explorer.GraphTypes.BlockHashType>();
            services.TryAddSingleton<Libplanet.Explorer.GraphTypes.TxIdType>();
            services.TryAddSingleton<Libplanet.Explorer.GraphTypes.TxResultType>();
            services.TryAddSingleton<Libplanet.Explorer.GraphTypes.TxStatusType>();
            services.TryAddSingleton<Libplanet.Explorer.GraphTypes.AccountStateType>();
            services.TryAddSingleton<Libplanet.Explorer.GraphTypes.BencodexValueType>();
            services.TryAddSingleton<Libplanet.Explorer.GraphTypes.IValueType>();
            services.TryAddSingleton<Libplanet.Explorer.GraphTypes.FungibleAssetValueType>();
            services.TryAddSingleton<Libplanet.Explorer.GraphTypes.CurrencyType>();
            services.TryAddSingleton<Libplanet.Explorer.GraphTypes.CurrencyInputType>();
            services.TryAddSingleton<Libplanet.Explorer.GraphTypes.ValidatorType>();
            services.TryAddSingleton<Libplanet.Explorer.GraphTypes.VoteFlagType>();
            services.TryAddSingleton<Libplanet.Explorer.GraphTypes.VoteType>();
            services.TryAddSingleton<Libplanet.Explorer.GraphTypes.BlockCommitType>();
            services.TryAddSingleton<Libplanet.Explorer.GraphTypes.BoundPeerType>();
            services.TryAddSingleton<Libplanet.Explorer.GraphTypes.WorldStateType>();
            services.TryAddSingleton<Libplanet.Explorer.GraphTypes.HashDigestType<SHA1>>();
            services.TryAddSingleton<Libplanet.Explorer.GraphTypes.HashDigestType<SHA256>>();
            services.TryAddSingleton<Libplanet.Explorer.GraphTypes.HashDigestSHA256Type>();
            services.TryAddSingleton<Libplanet.Explorer.GraphTypes.KeyBytesType>();
            services.TryAddSingleton<Libplanet.Explorer.GraphTypes.TrieType>();
            services.TryAddSingleton<Libplanet.Explorer.GraphTypes.EvidenceIdType>();

            return services;
        }

        public static IServiceCollection AddBlockChainContext(this IServiceCollection services)
        {
            services.TryAddSingleton<IBlockChainContext, BlockChainContext>();

            return services;
        }

        public static IServiceCollection AddLibplanetExplorer(this IServiceCollection services)
        {
            services.AddLibplanetScalarTypes();
            services.AddBlockChainContext();
            services.AddSingleton<LibplanetExplorerSchema>();

            services.TryAddSingleton<ActionType>();
            services.TryAddSingleton<BlockType>();
            services.TryAddSingleton<TransactionType>();
            services.TryAddSingleton<NodeStateType>();
            services.TryAddSingleton<EvidenceType>();
            services.TryAddSingleton<BlockQuery>();
            services.TryAddSingleton<TransactionQuery>();
            services.TryAddSingleton<ExplorerQuery>();
            services.TryAddSingleton<HelperQuery>();
            services.TryAddSingleton<RawStateQuery>();
            services.TryAddSingleton<EvidenceQuery>();
            services.TryAddSingleton<ExplorerMutation>();
            services.TryAddSingleton(_ => new StateQuery()
            {
                Name = "LibplanetStateQuery",
            });

            return services;
        }
    }
}
