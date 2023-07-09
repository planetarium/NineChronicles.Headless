using System;
using System.Collections.Generic;
using Grpc.Core;
using Grpc.Core.Interceptors;
using System.Threading.Tasks;
using Libplanet;
using Libplanet.Action;
using Libplanet.Tx;
using Nekoyume.Blockchain;
using Serilog;
using static NineChronicles.Headless.NCActionUtils;

namespace NineChronicles.Headless.Middleware
{
    public class GrpcCaptureMiddleware : Interceptor
    {
        private const int BanMinutes = 1;
        private const int UnbanMinutes = 1;
        private static Dictionary<Address, DateTimeOffset> _bannedAgentsTracker = new();
        private static Dictionary<Address, DateTimeOffset> _bannedAgents = new();
        private readonly ILogger _logger;
        private StandaloneContext _standaloneContext;
        private Dictionary<string, HashSet<Address>> _ipSignerList;

        public GrpcCaptureMiddleware(StandaloneContext standaloneContext, Dictionary<string, HashSet<Address>> ipSignerList)
        {
            _logger = Log.Logger.ForContext<GrpcCaptureMiddleware>();
            _standaloneContext = standaloneContext;
            _ipSignerList = ipSignerList;
        }

        private static void BanAgent(Address agent)
        {
            _bannedAgents.Add(agent, DateTimeOffset.Now);
        }

        private static void UnbanAgent(Address agent)
        {
            _bannedAgents.Remove(agent);
        }

        public override async Task<TResponse> UnaryServerHandler<TRequest, TResponse>(
            TRequest request, ServerCallContext context, UnaryServerMethod<TRequest, TResponse> continuation)
        {
            if (context.Method is "/IBlockChainService/AddClient" or "/IBlockChainService/GetNextTxNonce" && request is byte[] addressBytes)
            {
                var agent = new Address(addressBytes);
                var httpContext = context.GetHttpContext();
                var ipAddress = httpContext.Connection.RemoteIpAddress + ":" + httpContext.Connection.RemotePort;
                _logger.Information(
                    "[GRPC-REQUEST-CAPTURE] IP: {IP} Method: {Method} Agent: {Agent}",
                    ipAddress, context.Method, agent);
                if (!_ipSignerList.ContainsKey(httpContext.Connection.RemoteIpAddress!.ToString()))
                {
                    _logger.Information(
                        "[GRPC-REQUEST-CAPTURE] Creating a new list for IP: {IP}",
                        httpContext.Connection.RemoteIpAddress!.ToString());
                    _ipSignerList[httpContext.Connection.RemoteIpAddress!.ToString()] = new HashSet<Address>();
                }
                else
                {
                    _logger.Information(
                        "[GRPC-REQUEST-CAPTURE] List already created for IP: {IP} Count: {Count}",
                        httpContext.Connection.RemoteIpAddress!.ToString(),
                        _ipSignerList[httpContext.Connection.RemoteIpAddress!.ToString()].Count);
                }

                _ipSignerList[httpContext.Connection.RemoteIpAddress!.ToString()].Add(agent);
            }

            if (context.Method is "/IBlockChainService/PutTransaction" && request is byte[] txBytes)
            {
                Transaction tx =
                    Transaction.Deserialize(txBytes);
                var actionName = ToAction(tx.Actions[0]) is { } action
                    ? $"{action}"
                    : "NoAction";
                var httpContext = context.GetHttpContext();
                var ipAddress = httpContext.Connection.RemoteIpAddress + ":" + httpContext.Connection.RemotePort;
                var agent = tx.Signer;
                if (_ipSignerList[httpContext.Connection.RemoteIpAddress!.ToString()].Count > 0)
                {
                    if (!_bannedAgents.ContainsKey(agent))
                    {
                        if (!_bannedAgentsTracker.ContainsKey(agent))
                        {
                            _logger.Information($"[GRPC-REQUEST-CAPTURE] Banning Agent {agent} for {BanMinutes} minutes due to 100+ accounts associated with the same IP.");
                            BanAgent(agent);
                            var ncStagePolicy = (NCStagePolicy)_standaloneContext.BlockChain!.StagePolicy;
                            ncStagePolicy.BannedAccounts = ncStagePolicy.BannedAccounts.Add(agent);
                        }
                        else
                        {
                            if ((DateTimeOffset.Now - _bannedAgentsTracker[agent]).Minutes >= UnbanMinutes)
                            {
                                _logger.Information($"[GRPC-REQUEST-CAPTURE] Banning Agent {agent} again for {BanMinutes} minutes due to 100+ accounts associated with the same IP.");
                                BanAgent(agent);
                                _bannedAgentsTracker[agent] = DateTimeOffset.Now;
                                var ncStagePolicy = (NCStagePolicy)_standaloneContext.BlockChain!.StagePolicy;
                                ncStagePolicy.BannedAccounts = ncStagePolicy.BannedAccounts.Add(agent);
                            }
                            else
                            {
                                _logger.Information($"[GRPC-REQUEST-CAPTURE] Agent {agent} in unban status for {UnbanMinutes - (DateTimeOffset.Now - _bannedAgentsTracker[agent]).Minutes} minutes.");
                            }
                        }
                    }
                    else
                    {
                        if ((DateTimeOffset.Now - _bannedAgents[agent]).Minutes >= BanMinutes)
                        {
                            _logger.Information($"[GRPC-REQUEST-CAPTURE] Unbanning Agent {agent} after {BanMinutes} minutes.");
                            UnbanAgent(agent);
                            _bannedAgentsTracker[agent] = DateTimeOffset.Now;
                            var ncStagePolicy = (NCStagePolicy)_standaloneContext.BlockChain!.StagePolicy;
                            ncStagePolicy.BannedAccounts = ncStagePolicy.BannedAccounts.Remove(agent);
                        }
                        else
                        {
                            _logger.Information($"[GRPC-REQUEST-CAPTURE] Agent {agent} in ban status for the next {BanMinutes - (DateTimeOffset.Now - _bannedAgents[agent]).Minutes} minutes.");
                        }
                    }

                    _logger.Information(
                        "[GRPC-REQUEST-CAPTURE] IP: {IP} List Count: {Count}, AgentAddresses: {Agent}",
                        httpContext.Connection.RemoteIpAddress!.ToString(),
                        _ipSignerList[httpContext.Connection.RemoteIpAddress!.ToString()].Count,
                        _ipSignerList[httpContext.Connection.RemoteIpAddress!.ToString()]);
                }

                _logger.Information(
                    "[GRPC-REQUEST-CAPTURE] IP: {IP} Method: {Method} Agent: {Agent} Action: {Action}",
                    ipAddress, context.Method, tx.Signer, actionName);
            }

            return await base.UnaryServerHandler(request, context, continuation);
        }
    }
}
