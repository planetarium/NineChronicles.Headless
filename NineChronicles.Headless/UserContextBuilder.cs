using System.Collections.Generic;
using System.Threading.Tasks;
using GraphQL.Server.Transports.AspNetCore;
using Libplanet.Crypto;
using Libplanet.Explorer.Interfaces;
using Microsoft.AspNetCore.Http;
using NCAction = Libplanet.Action.PolymorphicAction<Nekoyume.Action.ActionBase>;

namespace NineChronicles.Headless
{
    public class UserContextBuilder : IUserContextBuilder
    {
        private readonly StandaloneContext _standaloneContext;

        public UserContextBuilder(StandaloneContext standaloneContext)
        {
            _standaloneContext = standaloneContext;
        }
        
        public Task<IDictionary<string, object?>> BuildUserContext(HttpContext httpContext)
        {
            return new ValueTask<IDictionary<string, object?>>(new Dictionary<string, object?>
            {
                [nameof(IBlockChainContext<NCAction>.Store)] = _standaloneContext.Store,
                [GraphQLService.UserContextPrivateKeyKey] = httpContext.Session.GetPrivateKey(),
            }).AsTask();
        }
    }
}
