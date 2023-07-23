using System.IO;
using System.Threading.Tasks;
using Libplanet;
using Libplanet.Tx;
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
            // Prevent to harm HTTP/2 communication.
            if (context.Request.Protocol == "HTTP/1.1")
            {
                context.Request.EnableBuffering();
                var remoteIp = context.Connection.RemoteIpAddress;
                var body = await new StreamReader(context.Request.Body).ReadToEndAsync();
                _logger.Information("[GRAPHQL-REQUEST-CAPTURE] IP: {IP} Method: {Method} Endpoint: {Path} {Body}",
                    remoteIp, context.Request.Method, context.Request.Path, body);
                context.Request.Body.Seek(0, SeekOrigin.Begin);
                if (body.Contains("stageTransaction"))
                {
                    _logger.Information("[GRAPHQL-REQUEST-CAPTURE] Contains stageTransaction: {yes}.", "yes");
                    byte[] payload = ByteUtil.ParseHex(body.Split("\\\"")[1]);
                    Transaction tx = Transaction.Deserialize(payload);
                    _logger.Information("[GRAPHQL-REQUEST-CAPTURE] Transaction signer: {signer}.", tx.Signer);
                }
            }

            await _next(context);
        }
    }
}
