using System;
using System.Linq;
using Grpc.Core;
using Grpc.Core.Interceptors;
using System.Threading.Tasks;
using Libplanet.Crypto;
using Libplanet.Types.Tx;
using Serilog;
using static NineChronicles.Headless.NCActionUtils;

namespace NineChronicles.Headless.Middleware
{
    public class GrpcCaptureMiddleware : Interceptor
    {
        private readonly ILogger _logger;
        private readonly ActionEvaluationPublisher _actionEvaluationPublisher;

        public GrpcCaptureMiddleware(ActionEvaluationPublisher actionEvaluationPublisher)
        {
            _logger = Log.Logger.ForContext<GrpcCaptureMiddleware>();
            _actionEvaluationPublisher = actionEvaluationPublisher;
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
            }

            if (context.Method is "/IBlockChainService/GetNextTxNonce" && request is byte[] getNextTxNonceAddressBytes)
            {
                var agent = new Address(getNextTxNonceAddressBytes);
                var httpContext = context.GetHttpContext();
                var ipAddress = httpContext.Connection.RemoteIpAddress + ":" + httpContext.Connection.RemotePort;

                _logger.Information(
                    "[GRPC-REQUEST-CAPTURE] IP: {IP} Method: {Method} Agent: {Agent}",
                    ipAddress, context.Method, agent);
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

                _logger.Information(
                    "[GRPC-REQUEST-CAPTURE] IP: {IP} Method: {Method} Agent: {Agent} Action: {Action}",
                    ipAddress, context.Method, tx.Signer, actionName);
            }

            if (context.Method is "/IBlockChainService/GetSheets")
            {
                var httpContext = context.GetHttpContext();
                var ipAddress = httpContext.Connection.RemoteIpAddress + ":" + httpContext.Connection.RemotePort;
                _logger.Information(
                    "[GRPC-REQUEST-CAPTURE] IP: {IP} Method: {Method}",
                    ipAddress, context.Method);
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
