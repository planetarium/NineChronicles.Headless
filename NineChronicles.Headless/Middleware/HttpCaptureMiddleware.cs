using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Libplanet.Common;
using Libplanet.Types.Tx;
using Microsoft.AspNetCore.Http;
using Serilog;
using Exception = System.Exception;
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
                    try
                    {
                        var pattern = "64313.*6565";
                        var txPayload = Regex.Match(body, pattern).ToString();
                        byte[] bytes = ByteUtil.ParseHex(txPayload);
                        Transaction tx = Transaction.Deserialize(bytes);
                        var agent = tx.Signer;
                        _logger.Information("[GRAPHQL-REQUEST-CAPTURE] IP: {IP} Agent: {Agent} Tx: {Path}",
                            remoteIp, agent, tx.Actions.Actions.FirstOrDefault());
                    }
                    catch (Exception ex)
                    {
                        _logger.Error(
                            "[GRAPHQL-REQUEST-CAPTURE] Error message: {message} Stacktrace: {stackTrace}",
                            ex.Message,
                            ex.StackTrace);
                    }
                }
            }

            await _next(context);
        }
    }
}
