using Grpc.Core;
using Grpc.Core.Interceptors;
using System.Threading.Tasks;
using Libplanet;
using Libplanet.Action;
using Libplanet.Tx;
using Nekoyume.Action;
using Serilog;

namespace NineChronicles.Headless.Middleware
{
    public class GrpcCaptureMiddleware : Interceptor
    {
        private readonly ILogger _logger;

        public GrpcCaptureMiddleware()
        {
            _logger = Log.Logger.ForContext<GrpcCaptureMiddleware>();
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
            }

            if (context.Method is "/IBlockChainService/PutTransaction" && request is byte[] txBytes)
            {
                Transaction<PolymorphicAction<ActionBase>> tx =
                    Transaction<PolymorphicAction<ActionBase>>.Deserialize(txBytes);
                var action = tx.CustomActions?[0].GetInnerActionTypeName() ?? "NoAction";
                var httpContext = context.GetHttpContext();
                var ipAddress = httpContext.Connection.RemoteIpAddress + ":" + httpContext.Connection.RemotePort;
                _logger.Information(
                    "[GRPC-REQUEST-CAPTURE] IP: {IP} Method: {Method} Agent: {Agent} Action: {Action}",
                    ipAddress, context.Method, tx.Signer, action);
            }

            return await base.UnaryServerHandler(request, context, continuation);
        }
    }
}
