using System;
using System.Collections.Generic;
using System.Linq;
using Grpc.Core;
using Grpc.Core.Interceptors;
using System.Threading.Tasks;
using Libplanet.Crypto;
using Libplanet.Types.Tx;
using Microsoft.Extensions.Options;
using NineChronicles.Headless.Properties;
using Serilog;
using static NineChronicles.Headless.NCActionUtils;

namespace NineChronicles.Headless.Middleware
{
    public class GrpcCaptureMiddleware : Interceptor
    {
        private static readonly Dictionary<Address, DateTimeOffset> MultiAccountTxIntervalTracker = new();
        private static readonly Dictionary<Address, DateTimeOffset> MultiAccountManagementList = new();
        private readonly ILogger _logger;
        private StandaloneContext _standaloneContext;
        private readonly Dictionary<string, HashSet<Address>> _ipSignerList;
        private readonly ActionEvaluationPublisher _actionEvaluationPublisher;
        private readonly IOptions<MultiAccountManagerProperties> _options;

        public GrpcCaptureMiddleware(
            StandaloneContext standaloneContext,
            Dictionary<string,
            HashSet<Address>> ipSignerList,
            ActionEvaluationPublisher actionEvaluationPublisher,
            IOptions<MultiAccountManagerProperties> options)
        {
            _logger = Log.Logger.ForContext<GrpcCaptureMiddleware>();
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
                var ipAddress = httpContext.Connection.RemoteIpAddress + ":" + httpContext.Connection.RemotePort;
                var uaHeader = httpContext.Request.Headers["User-Agent"].FirstOrDefault()!;
                AddClientByDevice(agent, uaHeader);

                _logger.Information(
                    "[GRPC-REQUEST-CAPTURE] IP: {IP} Method: {Method} Agent: {Agent} Header: {Header}",
                    ipAddress, context.Method, agent, uaHeader);

                if (_options.Value.EnableManaging)
                {
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
            }

            if (context.Method is "/IBlockChainService/GetNextTxNonce" && request is byte[] getNextTxNonceAddressBytes)
            {
                var agent = new Address(getNextTxNonceAddressBytes);
                var httpContext = context.GetHttpContext();
                var ipAddress = httpContext.Connection.RemoteIpAddress + ":" + httpContext.Connection.RemotePort;
                var uaHeader = httpContext.Request.Headers["User-Agent"].FirstOrDefault()!;
                AddClientByDevice(agent, uaHeader);

                _logger.Information(
                    "[GRPC-REQUEST-CAPTURE] IP: {IP} Method: {Method} Agent: {Agent} Header: {Header}",
                    ipAddress, context.Method, agent, uaHeader);

                if (_options.Value.EnableManaging)
                {
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
                var uaHeader = httpContext.Request.Headers["User-Agent"].FirstOrDefault()!;
                if (!_actionEvaluationPublisher.GetClients().Contains(tx.Signer))
                {
                    await _actionEvaluationPublisher.AddClient(tx.Signer);
                    AddClientByDevice(tx.Signer, uaHeader);
                }

                if (_options.Value.EnableManaging)
                {
                    var agent = tx.Signer;
                    if (_ipSignerList[httpContext.Connection.RemoteIpAddress!.ToString()].Count >
                        _options.Value.ThresholdCount)
                    {
                        _logger.Information(
                            "[GRPC-REQUEST-CAPTURE] IP: {IP} List Count: {Count}, AgentAddresses: {Agent}",
                            httpContext.Connection.RemoteIpAddress!.ToString(),
                            _ipSignerList[httpContext.Connection.RemoteIpAddress!.ToString()].Count,
                            _ipSignerList[httpContext.Connection.RemoteIpAddress!.ToString()]);
                        if (!MultiAccountManagementList.ContainsKey(agent))
                        {
                            if (!MultiAccountTxIntervalTracker.ContainsKey(agent))
                            {
                                _logger.Information(
                                    $"[GRPC-REQUEST-CAPTURE] Adding agent {agent} to the agent tracker.");
                                MultiAccountTxIntervalTracker.Add(agent, DateTimeOffset.Now);
                            }
                            else
                            {
                                if ((DateTimeOffset.Now - MultiAccountTxIntervalTracker[agent]).Minutes >=
                                    _options.Value.TxIntervalMinutes)
                                {
                                    _logger.Information(
                                        $"[GRPC-REQUEST-CAPTURE] Resetting Agent {agent}'s time because " +
                                        $"it has been more than {_options.Value.TxIntervalMinutes} minutes since the last transaction.");
                                    MultiAccountTxIntervalTracker[agent] = DateTimeOffset.Now;
                                }
                                else
                                {
                                    _logger.Information(
                                        $"[GRPC-REQUEST-CAPTURE] Managing Agent {agent} for " +
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
                                    $"[GRPC-REQUEST-CAPTURE] Restoring Agent {agent} after {_options.Value.ManagementTimeMinutes} minutes.");
                                RestoreMultiAccount(agent);
                                MultiAccountTxIntervalTracker[agent] =
                                    DateTimeOffset.Now.AddMinutes(-_options.Value.TxIntervalMinutes);
                                _logger.Information(
                                    $"[GRPC-REQUEST-CAPTURE] Current time: {DateTimeOffset.Now} Added time: {DateTimeOffset.Now.AddMinutes(-_options.Value.TxIntervalMinutes)}.");
                            }
                            else
                            {
                                _logger.Information(
                                    $"[GRPC-REQUEST-CAPTURE] Agent {agent} is in managed status for the next {_options.Value.ManagementTimeMinutes - (DateTimeOffset.Now - MultiAccountManagementList[agent]).Minutes} minutes.");
                                throw new RpcException(new Status(StatusCode.Cancelled, "Request cancelled."));
                            }
                        }
                    }
                }

                _logger.Information(
                    "[GRPC-REQUEST-CAPTURE] IP: {IP} Method: {Method} Agent: {Agent} Action: {Action}",
                    ipAddress, context.Method, tx.Signer, actionName);
            }

            return await base.UnaryServerHandler(request, context, continuation);
        }

        private void AddClientByDevice(Address agentAddress, string userAgentHeader)
        {
            if (userAgentHeader.Contains("windows", StringComparison.InvariantCultureIgnoreCase)
                || userAgentHeader.Contains("macintosh", StringComparison.InvariantCultureIgnoreCase)
                || userAgentHeader.Contains("linux", StringComparison.InvariantCultureIgnoreCase))
            {
                _actionEvaluationPublisher.AddClientByDevice(agentAddress, "pc");
            }
            else if (userAgentHeader.Contains("android", StringComparison.InvariantCultureIgnoreCase)
                     || userAgentHeader.Contains("iphone", StringComparison.InvariantCultureIgnoreCase)
                     || userAgentHeader.Contains("ipad", StringComparison.InvariantCultureIgnoreCase))
            {
                _actionEvaluationPublisher.AddClientByDevice(agentAddress, "mobile");
            }
            else
            {
                _actionEvaluationPublisher.AddClientByDevice(agentAddress, "other");
            }
        }
    }
}
