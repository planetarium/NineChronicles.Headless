using System;
using System.Collections.Concurrent;
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
    public class GrpcMultiAccountManagementMiddleware : Interceptor
    {
        private static readonly ConcurrentDictionary<Address, DateTimeOffset> MultiAccountTxIntervalTracker = new();
        private static readonly ConcurrentDictionary<Address, DateTimeOffset> MultiAccountManagementList = new();
        private readonly ILogger _logger;
        private StandaloneContext _standaloneContext;
        private readonly ConcurrentDictionary<string, HashSet<Address>> _ipSignerList;
        private readonly ActionEvaluationPublisher _actionEvaluationPublisher;
        private readonly IOptions<MultiAccountManagerProperties> _options;

        public GrpcMultiAccountManagementMiddleware(
            StandaloneContext standaloneContext,
            ConcurrentDictionary<string, HashSet<Address>> ipSignerList,
            ActionEvaluationPublisher actionEvaluationPublisher,
            IOptions<MultiAccountManagerProperties> options)
        {
            _logger = Log.Logger.ForContext<GrpcMultiAccountManagementMiddleware>();
            _standaloneContext = standaloneContext;
            _ipSignerList = ipSignerList;
            _actionEvaluationPublisher = actionEvaluationPublisher;
            _options = options;
        }

        private static void ManageMultiAccount(Address agent)
        {
            MultiAccountManagementList.TryAdd(agent, DateTimeOffset.Now);
        }

        private static void RestoreMultiAccount(Address agent)
        {
            MultiAccountManagementList.TryRemove(agent, out _);
        }

        public override async Task<TResponse> UnaryServerHandler<TRequest, TResponse>(
            TRequest request, ServerCallContext context, UnaryServerMethod<TRequest, TResponse> continuation)
        {
            if (context.Method is "/IBlockChainService/AddClient" && request is byte[] addClientAddressBytes)
            {
                var agent = new Address(addClientAddressBytes);
                var httpContext = context.GetHttpContext();
                var remoteIp = httpContext.Connection.RemoteIpAddress!.ToString();

                if (_options.Value.EnableManaging)
                {
                    UpdateIpSignerList(remoteIp, agent);
                }
            }

            if (context.Method is "/IBlockChainService/GetNextTxNonce" && request is byte[] getNextTxNonceAddressBytes)
            {
                var agent = new Address(getNextTxNonceAddressBytes);
                var httpContext = context.GetHttpContext();
                var remoteIp = httpContext.Connection.RemoteIpAddress!.ToString();

                if (_options.Value.EnableManaging)
                {
                    UpdateIpSignerList(remoteIp, agent);
                }
            }

            if (context.Method is "/IBlockChainService/PutTransaction" && request is byte[] txBytes)
            {
                Transaction tx =
                    Transaction.Deserialize(txBytes);
                var httpContext = context.GetHttpContext();
                var remoteIp = httpContext.Connection.RemoteIpAddress!.ToString();

                if (_options.Value.EnableManaging)
                {
                    var agent = tx.Signer;
                    if (_ipSignerList[remoteIp].Count >
                        _options.Value.ThresholdCount)
                    {
                        _logger.Information(
                            "[GRPC-MULTI-ACCOUNT-MANAGER] IP: {IP} List Count: {Count}, AgentAddresses: {Agent}",
                            remoteIp,
                            _ipSignerList[remoteIp].Count,
                            _ipSignerList[remoteIp]);
                        if (!MultiAccountManagementList.ContainsKey(agent))
                        {
                            if (!MultiAccountTxIntervalTracker.ContainsKey(agent))
                            {
                                _logger.Information(
                                    $"[GRPC-MULTI-ACCOUNT-MANAGER] Adding agent {agent} to the agent tracker.");
                                MultiAccountTxIntervalTracker.TryAdd(agent, DateTimeOffset.Now);
                            }
                            else
                            {
                                if ((DateTimeOffset.Now - MultiAccountTxIntervalTracker[agent]).Minutes >=
                                    _options.Value.TxIntervalMinutes)
                                {
                                    _logger.Information(
                                        $"[GRPC-MULTI-ACCOUNT-MANAGER] Resetting Agent {agent}'s time because " +
                                        $"it has been more than {_options.Value.TxIntervalMinutes} minutes since the last transaction.");
                                    MultiAccountTxIntervalTracker.TryUpdate(agent, DateTimeOffset.Now, MultiAccountTxIntervalTracker[agent]);
                                }
                                else
                                {
                                    _logger.Information(
                                        $"[GRPC-MULTI-ACCOUNT-MANAGER] Managing Agent {agent} for " +
                                        $"{_options.Value.ManagementTimeMinutes} minutes due to " +
                                        $"{_ipSignerList[remoteIp].Count} associated accounts.");
                                    ManageMultiAccount(agent);
                                    MultiAccountTxIntervalTracker.TryUpdate(agent, DateTimeOffset.Now, MultiAccountTxIntervalTracker[agent]);
                                    throw new RpcException(new Status(StatusCode.Cancelled, "Request cancelled."));
                                }
                            }
                        }
                        else
                        {
                            var currentManagedTime = (DateTimeOffset.Now - MultiAccountManagementList[agent]).Minutes;
                            if (currentManagedTime >= _options.Value.ManagementTimeMinutes)
                            {
                                _logger.Information(
                                    $"[GRPC-MULTI-ACCOUNT-MANAGER] Restoring Agent {agent} after {_options.Value.ManagementTimeMinutes} minutes.");
                                RestoreMultiAccount(agent);
                                MultiAccountTxIntervalTracker.TryUpdate(agent, DateTimeOffset.Now.AddMinutes(-_options.Value.TxIntervalMinutes), MultiAccountTxIntervalTracker[agent]);
                                _logger.Information(
                                    $"[GRPC-MULTI-ACCOUNT-MANAGER] Current time: {DateTimeOffset.Now} Added time: {DateTimeOffset.Now.AddMinutes(-_options.Value.TxIntervalMinutes)}.");
                            }
                            else
                            {
                                _logger.Information(
                                    $"[GRPC-MULTI-ACCOUNT-MANAGER] Agent {agent} is in managed status for the next {_options.Value.ManagementTimeMinutes - currentManagedTime} minutes.");
                                throw new RpcException(new Status(StatusCode.Cancelled, "Request cancelled."));
                            }
                        }
                    }
                }
            }

            return await base.UnaryServerHandler(request, context, continuation);
        }

        private void UpdateIpSignerList(string ip, Address agent)
        {
            if (!_ipSignerList.ContainsKey(ip))
            {
                _logger.Information(
                    "[GRPC-MULTI-ACCOUNT-MANAGER] Creating a new list for IP: {IP} Address: {agent}",
                    ip,
                    agent);
                _ipSignerList[ip] = new HashSet<Address>();
            }
            else
            {
                _logger.Information(
                    "[GRPC-MULTI-ACCOUNT-MANAGER] List already created for IP: {IP} Count: {Count} Address: {agent}",
                    ip,
                    _ipSignerList[ip].Count,
                    agent);
            }

            _ipSignerList[ip].Add(agent);
            AddClientIpInfo(agent, ip);
        }

        private void AddClientIpInfo(Address agentAddress, string ipAddress)
        {
            _actionEvaluationPublisher.AddClientAndIp(ipAddress, agentAddress.ToString());
        }
    }
}
