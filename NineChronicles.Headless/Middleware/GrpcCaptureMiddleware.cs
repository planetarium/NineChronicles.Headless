using System;
using System.Collections.Generic;
using System.Linq;
using Grpc.Core;
using Grpc.Core.Interceptors;
using System.Threading.Tasks;
using Libplanet;
using Libplanet.Action;
using Libplanet.Tx;
using Nekoyume.Action;
using Serilog;
using static NineChronicles.Headless.NCActionUtils;

namespace NineChronicles.Headless.Middleware
{
    public class GrpcCaptureMiddleware : Interceptor
    {
        private readonly ILogger _logger;
        private Dictionary<string, HashSet<Address>> _ipSignerList;
        private List<DateTimeOffset> _logTimes;

        public GrpcCaptureMiddleware(Dictionary<string, HashSet<Address>> ipSignerList, List<DateTimeOffset> logTimes)
        {
            _logger = Log.Logger.ForContext<GrpcCaptureMiddleware>();
            _ipSignerList = ipSignerList;
            _logTimes = logTimes;
        }

        public override async Task<TResponse> UnaryServerHandler<TRequest, TResponse>(
            TRequest request, ServerCallContext context, UnaryServerMethod<TRequest, TResponse> continuation)
        {
            if (context.Method is "/IBlockChainService/AddClient" or "/IBlockChainService/GetNextTxNonce" && request is byte[] addressBytes)
            {
                try
                {
                    var agent = new Address(addressBytes);
                    var httpContext = context.GetHttpContext();
                    var ipAddress = httpContext.Connection.RemoteIpAddress;
                    _logger.Information(
                        "[GRPC-REQUEST-CAPTURE-SIGNER] IP: {IP} Method: {Method} Agent: {Agent}",
                        ipAddress, context.Method, agent);
                    if (!_ipSignerList.ContainsKey(httpContext.Connection.RemoteIpAddress!.ToString()))
                    {
                        _logger.Information(
                            "[GRPC-REQUEST-CAPTURE-SIGNER] Creating a new list for IP: {IP}",
                            httpContext.Connection.RemoteIpAddress!.ToString());
                        _ipSignerList[httpContext.Connection.RemoteIpAddress!.ToString()] = new HashSet<Address>();
                    }
                    else
                    {
                        _logger.Information(
                            "[GRPC-REQUEST-CAPTURE-SIGNER] List already created for IP: {IP} Count: {Count}",
                            httpContext.Connection.RemoteIpAddress!.ToString(),
                            _ipSignerList[httpContext.Connection.RemoteIpAddress!.ToString()].Count);
                    }

                    _ipSignerList[httpContext.Connection.RemoteIpAddress!.ToString()].Add(agent);
                    if ((DateTimeOffset.Now - _logTimes.Last()).Minutes >= 5)
                    {
                        _logger.Information(
                            "[GRPC-REQUEST-CAPTURE-SIGNER] Logging multi-account after {time} minutes.",
                            5);
                        foreach (var ipSigner in _ipSignerList)
                        {
                            if (ipSigner.Value.Count > 1)
                            {
                                _logger.Information(
                                    "[GRPC-REQUEST-CAPTURE-SIGNER] IP: {IP} List Count: {Count}, AgentAddresses: {Agent}",
                                    ipSigner.Key,
                                    ipSigner.Value.Count,
                                    ipSigner.Value);
                            }
                        }

                        _logger.Information(
                            "[GRPC-REQUEST-CAPTURE-SIGNER] Finished logging all {count} multi-account sources.",
                            _ipSignerList.Count);
                        _logTimes.Add(DateTimeOffset.Now);
                    }
                }
                catch (Exception ex)
                {
                    _logger.Error(
                        "[GRPC-REQUEST-CAPTURE-SIGNER] Error message: {message} Stacktrace: {stackTrace}",
                        ex.Message,
                        ex.StackTrace);
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
                var ipAddress = httpContext.Connection.RemoteIpAddress;
                _logger.Information(
                    "[GRPC-REQUEST-CAPTURE] IP: {IP} Method: {Method} Agent: {Agent} Action: {Action}",
                    ipAddress, context.Method, tx.Signer, actionName);
            }

            return await base.UnaryServerHandler(request, context, continuation);
        }
    }
}
