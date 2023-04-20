using System;
using System.Threading;
using System.Threading.RateLimiting;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;
using NineChronicles.Headless.Properties;
using Serilog;

namespace NineChronicles.Headless
{
    public class GrpcRateLimiterPolicy : IRateLimiterPolicy<string>
    {
        private readonly GrpcRateLimitOptions _options;

        public GrpcRateLimiterPolicy(
            IOptions<GrpcRateLimitOptions> options)
        {
            var logger = Log.Logger.ForContext<GrpcRateLimiterPolicy>();
            OnRejected = (ctx, _) =>
            {
                ctx.HttpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;
                var ipAddress = GetUserEndPoint(ctx.HttpContext);
                logger.Information($"[GRPC-REQUEST-CAPTURE] Rate limit exceeded. IP: {ipAddress} Method: {ctx.HttpContext.Request.Path}");
                return ValueTask.CompletedTask;
            };
            _options = options.Value;
        }

        public Func<OnRejectedContext, CancellationToken, ValueTask>? OnRejected { get; }

        public RateLimitPartition<string> GetPartition(HttpContext httpContext)
        {
            var ipAddress = GetUserEndPoint(httpContext);
            return RateLimitPartition.GetFixedWindowLimiter(ipAddress,
                _ => new FixedWindowRateLimiterOptions()
                {
                    PermitLimit = _options.PermitLimit,
                    QueueLimit = _options.QueueLimit,
                    Window = TimeSpan.FromSeconds(_options.Window),
                    AutoReplenishment = _options.AutoReplenishment
                });
        }

        static string GetUserEndPoint(HttpContext context) =>
            context.Connection.RemoteIpAddress + ":" + context.Connection.RemotePort;
    }
}
