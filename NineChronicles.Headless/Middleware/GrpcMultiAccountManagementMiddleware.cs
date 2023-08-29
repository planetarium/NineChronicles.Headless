using System;
using System.Collections.Generic;
using Grpc.Core;
using Grpc.Core.Interceptors;
using System.Threading.Tasks;
using Libplanet.Crypto;
using Libplanet.Types.Tx;
using Microsoft.Extensions.Options;
using NineChronicles.Headless.Properties;
using Serilog;

namespace NineChronicles.Headless.Middleware
{
    public class GrpMultiAccountManagementMiddleware : Interceptor
    {
        private static readonly Dictionary<Address, DateTimeOffset> MultiAccountTxIntervalTracker = new();
        private static readonly Dictionary<Address, DateTimeOffset> MultiAccountManagementList = new();
        private readonly ILogger _logger;
        private StandaloneContext _standaloneContext;
        private readonly Dictionary<string, HashSet<Address>> _ipSignerList;
        private readonly ActionEvaluationPublisher _actionEvaluationPublisher;
        private readonly IOptions<MultiAccountManagerProperties> _options;

        public GrpMultiAccountManagementMiddleware(
            StandaloneContext standaloneContext,
            Dictionary<string, HashSet<Address>> ipSignerList,
            ActionEvaluationPublisher actionEvaluationPublisher,
            IOptions<MultiAccountManagerProperties> options)
        {
            _logger = Log.Logger.ForContext<GrpMultiAccountManagementMiddleware>();
            _standaloneContext = standaloneContext;
            _ipSignerList = ipSignerList;
            _actionEvaluationPublisher = actionEvaluationPublisher;
            _options = options;
        }

        private static void ManageMultiAccount(Address agent)
        {
            MultiAccountManagementList.Add(agent, DateTimeOffset.Now);
        }

        private static void RestoreMultiAccount(Address agent)
        {
            MultiAccountManagementList.Remove(agent);
        }

        public override async Task<TResponse> UnaryServerHandler<TRequest, TResponse>(
            TRequest request, ServerCallContext context, UnaryServerMethod<TRequest, TResponse> continuation)
        {
            if (context.Method is "/IBlockChainService/AddClient" && request is byte[] addClientAddressBytes)
            {
                var agent = new Address(addClientAddressBytes);
                var httpContext = context.GetHttpContext();

                if (_options.Value.EnableManaging)
                {
                    if (!_ipSignerList.ContainsKey(httpContext.Connection.RemoteIpAddress!.ToString()))
                    {
                        _logger.Information(
                            "[GRPC-MULTI-ACCOUNT-MANAGER] Creating a new list for IP: {IP}",
                            httpContext.Connection.RemoteIpAddress!.ToString());
                        _ipSignerList[httpContext.Connection.RemoteIpAddress!.ToString()] = new HashSet<Address>();
                    }
                    else
                    {
                        _logger.Information(
                            "[GRPC-MULTI-ACCOUNT-MANAGER] List already created for IP: {IP} Count: {Count}",
                            httpContext.Connection.RemoteIpAddress!.ToString(),
                            _ipSignerList[httpContext.Connection.RemoteIpAddress!.ToString()].Count);
                    }

                    _ipSignerList[httpContext.Connection.RemoteIpAddress!.ToString()].Add(agent);
                }
            }

            if (context.Method is "/IBlockChainService/GetNextTxNonce" && request is byte[] getNextTxNonceAddressBytes)
            {
                var agent = new Address(getNextTxNonceAddressBytes);
                var httpContext = context.GetHttpContext();

                if (_options.Value.EnableManaging)
                {
                    if (!_ipSignerList.ContainsKey(httpContext.Connection.RemoteIpAddress!.ToString()))
                    {
                        _logger.Information(
                            "[GRPC-MULTI-ACCOUNT-MANAGER] Creating a new list for IP: {IP}",
                            httpContext.Connection.RemoteIpAddress!.ToString());
                        _ipSignerList[httpContext.Connection.RemoteIpAddress!.ToString()] = new HashSet<Address>();
                    }
                    else
                    {
                        _logger.Information(
                            "[GRPC-MULTI-ACCOUNT-MANAGER] List already created for IP: {IP} Count: {Count}",
                            httpContext.Connection.RemoteIpAddress!.ToString(),
                            _ipSignerList[httpContext.Connection.RemoteIpAddress!.ToString()].Count);
                    }

                    _ipSignerList[httpContext.Connection.RemoteIpAddress!.ToString()].Add(agent);
                }
            }

            if (context.Method is "/IBlockChainService/PutTransaction" && request is byte[] txBytes)
            {
                Transaction tx =
                    Transaction.Deserialize(txBytes);
                var httpContext = context.GetHttpContext();

                if (_options.Value.EnableManaging)
                {
                    var agent = tx.Signer;
                    if (_ipSignerList[httpContext.Connection.RemoteIpAddress!.ToString()].Count >
                        _options.Value.ThresholdCount)
                    {
                        _logger.Information(
                            "[GRPC-MULTI-ACCOUNT-MANAGER] IP: {IP} List Count: {Count}, AgentAddresses: {Agent}",
                            httpContext.Connection.RemoteIpAddress!.ToString(),
                            _ipSignerList[httpContext.Connection.RemoteIpAddress!.ToString()].Count,
                            _ipSignerList[httpContext.Connection.RemoteIpAddress!.ToString()]);
                        if (!MultiAccountManagementList.ContainsKey(agent))
                        {
                            if (!MultiAccountTxIntervalTracker.ContainsKey(agent))
                            {
                                _logger.Information(
                                    $"[GRPC-MULTI-ACCOUNT-MANAGER] Adding agent {agent} to the agent tracker.");
                                MultiAccountTxIntervalTracker.Add(agent, DateTimeOffset.Now);
                            }
                            else
                            {
                                if ((DateTimeOffset.Now - MultiAccountTxIntervalTracker[agent]).Minutes >=
                                    _options.Value.TxIntervalMinutes)
                                {
                                    _logger.Information(
                                        $"[GRPC-MULTI-ACCOUNT-MANAGER] Resetting Agent {agent}'s time because " +
                                        $"it has been more than {_options.Value.TxIntervalMinutes} minutes since the last transaction.");
                                    MultiAccountTxIntervalTracker[agent] = DateTimeOffset.Now;
                                }
                                else
                                {
                                    _logger.Information(
                                        $"[GRPC-MULTI-ACCOUNT-MANAGER] Managing Agent {agent} for " +
                                        $"{_options.Value.ManagementTimeMinutes} minutes due to " +
                                        $"{_ipSignerList[httpContext.Connection.RemoteIpAddress!.ToString()].Count} associated accounts.");
                                    ManageMultiAccount(agent);
                                    MultiAccountTxIntervalTracker[agent] = DateTimeOffset.Now;
                                    throw new RpcException(new Status(StatusCode.Cancelled, "Request cancelled."));
                                }
                            }
                        }
                        else
                        {
                            if ((DateTimeOffset.Now - MultiAccountManagementList[agent]).Minutes >=
                                _options.Value.ManagementTimeMinutes)
                            {
                                _logger.Information(
                                    $"[GRPC-MULTI-ACCOUNT-MANAGER] Restoring Agent {agent} after {_options.Value.ManagementTimeMinutes} minutes.");
                                RestoreMultiAccount(agent);
                                MultiAccountTxIntervalTracker[agent] =
                                    DateTimeOffset.Now.AddMinutes(-_options.Value.TxIntervalMinutes);
                                _logger.Information(
                                    $"[GRPC-MULTI-ACCOUNT-MANAGER] Current time: {DateTimeOffset.Now} Added time: {DateTimeOffset.Now.AddMinutes(-_options.Value.TxIntervalMinutes)}.");
                            }
                            else
                            {
                                _logger.Information(
                                    $"[GRPC-MULTI-ACCOUNT-MANAGER] Agent {agent} is in managed status for the next {_options.Value.ManagementTimeMinutes - (DateTimeOffset.Now - MultiAccountManagementList[agent]).Minutes} minutes.");
                                throw new RpcException(new Status(StatusCode.Cancelled, "Request cancelled."));
                            }
                        }
                    }
                }
            }

            return await base.UnaryServerHandler(request, context, continuation);
        }
    }
}
