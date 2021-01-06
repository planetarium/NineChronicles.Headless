using System.Collections.Generic;
using System.Security.Claims;
using GraphQL.Authorization;

namespace NineChronicles.Headless
{
    public class GraphQLUserContext : Dictionary<string, object>, IProvideClaimsPrincipal
    {
        public ClaimsPrincipal User { get; set; }
    }
}
