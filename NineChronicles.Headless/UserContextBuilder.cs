using System.Collections.Generic;
using System.Threading.Tasks;
using GraphQL.Server.Transports.AspNetCore;
using Libplanet.Explorer.Interfaces;
using Microsoft.AspNetCore.Http;

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
                [nameof(IBlockChainContext.Store)] = _standaloneContext.Store,
            }).AsTask();
        }
    }
}
