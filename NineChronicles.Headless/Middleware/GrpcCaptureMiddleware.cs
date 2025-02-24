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
            var httpContext = context.GetHttpContext();
            var ipAddress = httpContext.Connection.RemoteIpAddress + ":" + httpContext.Connection.RemotePort;
            long requestSize = request is byte[] requestBytes ? requestBytes.Length : 0;

            // Process request and capture response size
            var response = await continuation(request, context);
            long responseSize = response is byte[] responseBytes ? responseBytes.Length : 0;

            // Preserve existing logic
            if (context.Method == "/IBlockChainService/AddClient" && request is byte[] addClientAddressBytes)
            {
                var agent = new Address(addClientAddressBytes);
                var uaHeader = httpContext.Request.Headers["User-Agent"].FirstOrDefault()!;
                AddClientByDevice(agent, uaHeader);

                _logger.Information(
                    "[GRPC-REQUEST-CAPTURE] IP: {IP} Method: {Method} Agent: {Agent} Header: {Header} Request Size: {RequestSize} bytes Response Size: {ResponseSize} bytes",
                    ipAddress, context.Method, agent, uaHeader, requestSize, responseSize);
            }

            if (context.Method == "/IBlockChainService/GetNextTxNonce" && request is byte[] getNextTxNonceAddressBytes)
            {
                var agent = new Address(getNextTxNonceAddressBytes);

                _logger.Information(
                    "[GRPC-REQUEST-CAPTURE] IP: {IP} Method: {Method} Agent: {Agent} Request Size: {RequestSize} bytes Response Size: {ResponseSize} bytes",
                    ipAddress, context.Method, agent, requestSize, responseSize);
            }

            if (context.Method == "/IBlockChainService/PutTransaction" && request is byte[] txBytes)
            {
                Transaction tx = Transaction.Deserialize(txBytes);
                var actionName = ToAction(tx.Actions[0]) is { } action ? $"{action}" : "NoAction";
                var uaHeader = httpContext.Request.Headers["User-Agent"].FirstOrDefault()!;

                if (!_actionEvaluationPublisher.GetClients().Contains(tx.Signer))
                {
                    await _actionEvaluationPublisher.AddClient(tx.Signer);
                    AddClientByDevice(tx.Signer, uaHeader);
                }

                _logger.Information(
                    "[GRPC-REQUEST-CAPTURE] IP: {IP} Method: {Method} Agent: {Agent} Action: {Action} Request Size: {RequestSize} bytes Response Size: {ResponseSize} bytes",
                    ipAddress, context.Method, tx.Signer, actionName, requestSize, responseSize);
            }

            if (context.Method == "/IBlockChainService/GetSheets")
            {
                _logger.Information(
                    "[GRPC-REQUEST-CAPTURE] IP: {IP} Method: {Method} Request Size: {RequestSize} bytes Response Size: {ResponseSize} bytes",
                    ipAddress, context.Method, requestSize, responseSize);
            }

            // Log for any other gRPC method not explicitly covered
            if (!new[] { "/IBlockChainService/AddClient", "/IBlockChainService/GetNextTxNonce", "/IBlockChainService/PutTransaction", "/IBlockChainService/GetSheets" }.Contains(context.Method))
            {
                _logger.Information(
                    "[GRPC-REQUEST-CAPTURE] IP: {IP} Method: {Method} Request Size: {RequestSize} bytes Response Size: {ResponseSize} bytes",
                    ipAddress, context.Method, requestSize, responseSize);
            }

            return response;
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
