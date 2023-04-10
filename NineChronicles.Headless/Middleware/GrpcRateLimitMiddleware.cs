using Grpc.Core;
using Grpc.Core.Interceptors;
using System;
using System.Collections.Concurrent;
using System.Threading.Tasks;
using Serilog;

namespace NineChronicles.Headless.Middleware
{
    public class RateLimitInterceptor : Interceptor
    {
        private readonly int _limit;
        private readonly TimeSpan _interval;
        private readonly ConcurrentDictionary<string, ConcurrentQueue<DateTime>> _requestsByIp;
        private readonly ILogger _logger;

        public RateLimitInterceptor(int limit, TimeSpan interval)
        {
            _limit = limit;
            _interval = interval;
            _requestsByIp = new ConcurrentDictionary<string, ConcurrentQueue<DateTime>>();
            _logger = Log.Logger.ForContext<RateLimitInterceptor>();
        }

        public override async Task<TResponse> UnaryServerHandler<TRequest, TResponse>(
            TRequest request, ServerCallContext context, UnaryServerMethod<TRequest, TResponse> continuation)
        {
            var httpContext = context.GetHttpContext();
            var ipAddress = httpContext.Connection.RemoteIpAddress + ":" + httpContext.Connection.RemotePort;
            if (context.Method == "/IBlockChainService/AddClient")
            {
                _logger.Information(
                    "[GRPC-REQUEST-CAPTURE] Add client. IP: {IP} Method: {Method} Request: {Request}",
                    ipAddress, context.Method, request);
            }

            if (context.Method == "/IBlockChainService/PutTransaction" && !TryAcquire(ipAddress))
            {
                _logger.Information(
                    "[GRPC-REQUEST-CAPTURE] Rate limit exceeded. IP: {IP} Method: {Method} Request: {Request}",
                    ipAddress, context.Method, request);
                throw new RpcException(new Status(StatusCode.ResourceExhausted, "Rate limit exceeded."));
            }

            return await base.UnaryServerHandler(request, context, continuation);
        }

        private bool TryAcquire(string ipAddress)
        {
            var requests = _requestsByIp.GetOrAdd(ipAddress, new ConcurrentQueue<DateTime>());
            while (requests.TryPeek(out DateTime requestTime))
            {
                if (DateTime.UtcNow - requestTime > _interval)
                {
                    requests.TryDequeue(out requestTime);
                }
                else
                {
                    break;
                }
            }

            if (requests.Count < _limit)
            {
                requests.Enqueue(DateTime.UtcNow);
                return true;
            }

            return false;
        }
    }
}

