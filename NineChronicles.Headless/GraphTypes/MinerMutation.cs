using System;
using GraphQL;
using GraphQL.Server.Authorization.AspNetCore;
using GraphQL.Types;
using Microsoft.AspNetCore.Http;

namespace NineChronicles.Headless.GraphTypes
{
    public class MinerMutation : ObjectGraphType
    {
        public MinerMutation(IMiner miner, IHttpContextAccessor httpContextAccessor)
        {
            Field<NonNullGraphType<BooleanGraphType>>(
                "start",
                resolve: context =>
                {
                    if (!(httpContextAccessor.HttpContext.Session.GetPrivateKey() is { } privateKey))
                    {
                        context.Errors.Add(new ExecutionError("The session private key is null."));
                        return false;
                    }

                    miner.PrivateKey = privateKey;
                    miner.StartMining();
                    return true;
                })
                .AuthorizeWith(GraphQLService.AdminPolicyKey);
            
            Field<NonNullGraphType<BooleanGraphType>>(
                "stop",
                resolve: _ =>
                {
                    miner.StopMining();
                    return true;
                })
                .AuthorizeWith(GraphQLService.AdminPolicyKey);
        }
    }
}
