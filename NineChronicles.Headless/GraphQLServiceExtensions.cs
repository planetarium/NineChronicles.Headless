using System;
using System.Linq;
using System.Reflection;
using GraphQL.Types;
using Libplanet.Action;
using Libplanet.Explorer.GraphTypes;
using Libplanet.Explorer.Interfaces;
using Libplanet.Explorer.Queries;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using NCAction = Libplanet.Action.PolymorphicAction<Nekoyume.Action.ActionBase>;

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
            services.TryAddSingleton<Libplanet.Explorer.GraphTypes.TxResultType>();
            services.TryAddSingleton<Libplanet.Explorer.GraphTypes.TxStatusType>();
            services.TryAddSingleton<Libplanet.Explorer.GraphTypes.BencodexValueType>();
            services.TryAddSingleton<Libplanet.Explorer.GraphTypes.FungibleAssetValueType>();
            services.TryAddSingleton<Libplanet.Explorer.GraphTypes.CurrencyType>();

            return services;
        }

        public static IServiceCollection AddBlockChainContext(this IServiceCollection services)
        {
            services.TryAddSingleton<IBlockChainContext<NCAction>, BlockChainContext>();

            return services;
        }

        public static IServiceCollection AddLibplanetExplorer<T>(this IServiceCollection services)
            where T : IAction, new()
        {
            services.AddLibplanetScalarTypes();
            services.AddBlockChainContext();

            services.TryAddSingleton<ActionType<T>>();
            services.TryAddSingleton<BlockType<T>>();
            services.TryAddSingleton<TransactionType<T>>();
            services.TryAddSingleton<NodeStateType<T>>();
            services.TryAddSingleton<BlockQuery<T>>();
            services.TryAddSingleton<TransactionQuery<T>>();
            services.TryAddSingleton<ExplorerQuery<T>>();
            services.TryAddSingleton(_ => new StateQuery<T>()
            {
                Name = "LibplanetStateQuery",
            });
            services.TryAddSingleton<BlockPolicyType<T>>();

            return services;
        }
    }
}
