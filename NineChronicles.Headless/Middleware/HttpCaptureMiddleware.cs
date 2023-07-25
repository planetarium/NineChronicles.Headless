using System;
using System.Collections.Generic;
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
        private static Dictionary<string, int> _stateQueryAgentList = new();
        private static Dictionary<string, DateTimeOffset> _blockedAgentList = new();
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
                    byte[] payload = ByteUtil.ParseHex(body.Split("\\\"")[1]);
                    Transaction tx = Transaction.Deserialize(payload);
                    if (_blockedAgentList.ContainsKey(tx.Signer.ToString()))
                    {
                        context.Response.StatusCode = 403;
                        context.Response.ContentType = "application/json";
                        return;
                    }
                }

                var agent = string.Empty;
                if (body.Contains("agent(address:"))
                {
                    agent = body.Split("\\\"")[1];
                    if (!_stateQueryAgentList.ContainsKey(agent))
                    {
                        _stateQueryAgentList.Add(agent, 1);
                    }
                    else
                    {
                        _stateQueryAgentList[agent] += 1;
                    }

                    _logger.Information("[IP-RATE-LIMITER] State Query signer: {signer} IP: {ip} Count: {count}.", agent, context.Connection.RemoteIpAddress, _stateQueryAgentList[agent]);
                }

                if (agent != string.Empty && _stateQueryAgentList[agent] > 100)
                {
                    if (!_blockedAgentList.ContainsKey(agent))
                    {
                        _blockedAgentList.Add(agent, DateTimeOffset.Now);
                    }
                    else
                    {
                        if ((DateTimeOffset.Now - _blockedAgentList[agent]).Minutes >= 60)
                        {
                            _logger.Information("[IP-RATE-LIMITER] State Query signer: {signer} removed from blocked list.", agent);
                            _blockedAgentList.Remove(agent);
                            _stateQueryAgentList.Remove(agent);
                        }
                        else
                        {
                            _logger.Information("[IP-RATE-LIMITER] State Query signer: {signer} blocked for the next {time} minutes.", agent, 60 - (DateTimeOffset.Now - _blockedAgentList[agent]).Minutes);
                        }
                    }

                    context.Response.StatusCode = 403;
                    context.Response.ContentType = "application/json";
                    return;
                }
            }

            await _next(context);
        }
    }
}
