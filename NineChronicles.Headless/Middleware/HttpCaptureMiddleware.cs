using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Serilog;
using ILogger = Serilog.ILogger;

namespace NineChronicles.Headless.Middleware
{
    public class HttpCaptureMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger _logger;

        public HttpCaptureMiddleware(RequestDelegate next)
        {
            _next = next;
            _logger = Log.Logger.ForContext<HttpCaptureMiddleware>();
        }

        public async Task InvokeAsync(HttpContext context)
        {
            context.Request.EnableBuffering();
            var body = await new StreamReader(context.Request.Body).ReadToEndAsync();
            _logger.Debug("[REQUEST-CAPTURE] {Method} {Path}\n{Body}", context.Request.Method, context.Request.Path, body);

            await _next(context);
        }
    }
}
