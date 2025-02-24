using System.IO;
using System.Text;
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

        public HttpCaptureMiddleware(RequestDelegate next, IConfiguration configuration)
        {
            _next = next;
            _logger = Log.Logger.ForContext<HttpCaptureMiddleware>();
            _enableIpRateLimiting = configuration.GetValue<bool>("IpRateLimiting:EnableEndpointRateLimiting");
        }

        public async Task InvokeAsync(HttpContext context)
        {
            var remoteIp = context.Connection.RemoteIpAddress!.ToString();

            // ✅ Skip processing for gRPC requests (gRPC uses HTTP/2 and does not work with buffering)
            if (context.Request.ContentType?.StartsWith("application/grpc") == true)
            {
                await _next(context);
                return;
            }

            // Conditionally skip IP banning if endpoint rate-limiting is disabled
            if (_enableIpRateLimiting && IpBanMiddleware.IsIpBanned(remoteIp))
            {
                _logger.Information("[GRAPHQL-REQUEST-CAPTURE] Skipping logging for banned IP: {IP}", remoteIp);
                await _next(context);
                return;
            }

            long requestSize = 0;
            string requestBody = "";

            // Prevent harming HTTP/2 communication (Only modify HTTP/1.1 requests)
            if (context.Request.Protocol == "HTTP/1.1")
            {
                context.Request.EnableBuffering();
                using (var reader = new StreamReader(context.Request.Body, Encoding.UTF8, leaveOpen: true))
                {
                    requestBody = await reader.ReadToEndAsync();
                    requestSize = Encoding.UTF8.GetByteCount(requestBody);
                    context.Items["RequestBody"] = requestBody;
                }
                context.Request.Body.Seek(0, SeekOrigin.Begin);
            }

            // Capture response size
            var originalBodyStream = context.Response.Body;
            await using var responseBody = new MemoryStream();
            context.Response.Body = responseBody;

            // Process request
            await _next(context);

            responseBody.Seek(0, SeekOrigin.Begin);
            long responseSize = responseBody.Length;
            await responseBody.CopyToAsync(originalBodyStream); // Restore original response stream

            // Log the request & response sizes
            _logger.Information(
                "[GRAPHQL-REQUEST-CAPTURE] IP: {IP} Method: {Method} Endpoint: {Path} Request Size: {RequestSize} bytes Response Size: {ResponseSize} bytes {Body}",
                remoteIp, context.Request.Method, context.Request.Path, requestSize, responseSize, requestBody);
        }
    }
}
