using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Serilog;
using ILogger = Serilog.ILogger;
using Microsoft.Extensions.Configuration;

namespace NineChronicles.Headless.Middleware
{
    public class HttpCaptureMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger _logger;
        private readonly bool _enableIpRateLimiting;

        public HttpCaptureMiddleware(RequestDelegate next, Microsoft.Extensions.Configuration.IConfiguration configuration)
        {
            _next = next;
            _logger = Log.Logger.ForContext<HttpCaptureMiddleware>();
            _enableIpRateLimiting = configuration.GetValue<bool>("IpRateLimiting:EnableEndpointRateLimiting");
        }

        public async Task InvokeAsync(HttpContext context)
        {
            var remoteIp = context.Connection.RemoteIpAddress!.ToString();

            // Conditionally skip IP banning if endpoint rate-limiting is disabled
            if (_enableIpRateLimiting && IpBanMiddleware.IsIpBanned(remoteIp))
            {
                _logger.Information($"[GRAPHQL-REQUEST-CAPTURE] Skipping logging for banned IP: {remoteIp}");
                await _next(context);
                return;
            }

            // Prevent to harm HTTP/2 communication.
            if (context.Request.Protocol == "HTTP/1.1")
            {
                context.Request.EnableBuffering();
                var body = await new StreamReader(context.Request.Body).ReadToEndAsync();
                context.Items["RequestBody"] = body;
                _logger.Information("[GRAPHQL-REQUEST-CAPTURE] IP: {IP} Method: {Method} Endpoint: {Path} {Body}",
                    remoteIp, context.Request.Method, context.Request.Path, body);
                context.Request.Body.Seek(0, SeekOrigin.Begin);
            }

            await _next(context);
        }
    }
}
