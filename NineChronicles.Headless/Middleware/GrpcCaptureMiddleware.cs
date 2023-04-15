using Grpc.Core;
using Grpc.Core.Interceptors;
using System.Threading.Tasks;
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
            if (context.Method is "/IBlockChainService/AddClient" or "/IBlockChainService/PutTransaction")
            {
                var httpContext = context.GetHttpContext();
                var ipAddress = httpContext.Connection.RemoteIpAddress + ":" + httpContext.Connection.RemotePort;
                _logger.Information(
                    "[GRPC-REQUEST-CAPTURE] IP: {IP} Method: {Method} Request: {Request}",
                    ipAddress, context.Method, request);
            }

            return await base.UnaryServerHandler(request, context, continuation);
        }
    }
}
