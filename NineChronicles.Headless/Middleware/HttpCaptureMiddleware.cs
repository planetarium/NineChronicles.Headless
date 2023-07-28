using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Libplanet;
using Microsoft.AspNetCore.Http;
using Serilog;
using ILogger = Serilog.ILogger;

namespace NineChronicles.Headless.Middleware
{
    public class HttpCaptureMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger _logger;
        private Dictionary<string, HashSet<Address>> _ipSignerList;
        private DateTimeOffset _logTime = DateTimeOffset.Now;

        public HttpCaptureMiddleware(RequestDelegate next, Dictionary<string, HashSet<Address>> ipSignerList)
        {
            _next = next;
            _logger = Log.Logger.ForContext<HttpCaptureMiddleware>();
            _ipSignerList = ipSignerList;
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

                if (body.Contains("agent(address:\\\"") || body.Contains("agent(address: \\\""))
                {
                    try
                    {
                        var agent = new Address(body.Split("\\\"")[1].Split("0x")[1]);
                        if (!_ipSignerList.ContainsKey(context.Connection.RemoteIpAddress!.ToString()))
                        {
                            _logger.Information(
                                "[GRAPHQL-REQUEST-CAPTURE-SIGNER] Creating a new list for IP: {IP}",
                                context.Connection.RemoteIpAddress!.ToString());
                            _ipSignerList[context.Connection.RemoteIpAddress!.ToString()] = new HashSet<Address>();
                        }
                        else
                        {
                            _logger.Information(
                                "[GRAPHQL-REQUEST-CAPTURE-SIGNER] List already created for IP: {IP} Count: {Count}",
                                context.Connection.RemoteIpAddress!.ToString(),
                                _ipSignerList[context.Connection.RemoteIpAddress!.ToString()].Count);
                        }

                        _ipSignerList[context.Connection.RemoteIpAddress!.ToString()].Add(agent);
                        if ((DateTimeOffset.Now - _logTime).Minutes >= 5)
                        {
                            _logger.Information(
                                "[GRAPHQL-REQUEST-CAPTURE-SIGNER] Logging multi-account after {time} minutes.",
                                5);
                            foreach (var ipSigner in _ipSignerList)
                            {
                                if (ipSigner.Value.Count > 1)
                                {
                                    _logger.Information(
                                        "[GRAPHQL-REQUEST-CAPTURE-SIGNER] IP: {IP} List Count: {Count}, AgentAddresses: {Agent}",
                                        ipSigner.Key,
                                        ipSigner.Value.Count,
                                        ipSigner.Value);
                                }
                            }

                            _logger.Information(
                                "[GRAPHQL-REQUEST-CAPTURE-SIGNER] Finished logging all {count} multi-account sources.",
                                _ipSignerList.Count);
                            _logTime = DateTimeOffset.Now;
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.Error(
                            "[GRAPHQL-REQUEST-CAPTURE-SIGNER] Error message: {message} Stacktrace: {stackTrace}",
                            ex.Message,
                            ex.StackTrace);
                    }
                }
            }

            await _next(context);
        }
    }
}
