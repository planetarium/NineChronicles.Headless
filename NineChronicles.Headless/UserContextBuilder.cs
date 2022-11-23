using System.Collections.Generic;
using System.Threading.Tasks;
using GraphQL.Server.Transports.AspNetCore;
using Libplanet.Explorer.Interfaces;
using Libplanet.Store;
using Microsoft.AspNetCore.Http;
using NCAction = Libplanet.Action.PolymorphicAction<Nekoyume.Action.ActionBase>;

namespace NineChronicles.Headless
{
    public class UserContextBuilder : IUserContextBuilder
    {
        private readonly IStore _store;

        public UserContextBuilder(IStore store)
        {
            _store = store;
        }

        public Task<IDictionary<string, object?>> BuildUserContext(HttpContext httpContext)
        {
            return new ValueTask<IDictionary<string, object?>>(new Dictionary<string, object?>
            {
                [nameof(IBlockChainContext<NCAction>.Store)] = _store,
            }).AsTask();
        }
    }
}
