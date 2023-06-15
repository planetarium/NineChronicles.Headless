using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using NineChronicles.Headless.Properties;
using Serilog;
using ILogger = Serilog.ILogger;

namespace NineChronicles.Headless.Middleware
{
    public class IpBanMiddleware
    {
        private static Dictionary<string, DateTimeOffset> _bannedIps = new();
        private readonly RequestDelegate _next;
        private readonly ILogger _logger;
        private readonly IOptions<CustomIpRateLimitOptions> _options;

        public IpBanMiddleware(RequestDelegate next, IOptions<CustomIpRateLimitOptions> options)
        {
            _next = next;
            _options = options;
            _logger = Log.Logger.ForContext<IpBanMiddleware>();
        }

        public static void BanIp(string ip)
        {
            if (!_bannedIps.ContainsKey(ip))
            {
                _bannedIps.Add(ip, DateTimeOffset.Now);
            }
        }

        public static void UnbanIp(string ip)
        {
            if (_bannedIps.ContainsKey(ip))
            {
                _bannedIps.Remove(ip);
            }
        }

        public Task InvokeAsync(HttpContext context)
        {
            context.Request.EnableBuffering();
            var remoteIp = context.Connection.RemoteIpAddress!.ToString();
            if (_bannedIps.ContainsKey(remoteIp))
            {
                if ((DateTimeOffset.Now - _bannedIps[remoteIp]).Minutes >= _options.Value.IpBanMinute)
                {
                    _logger.Information($"[IP-RATE-LIMITER] Unbanning IP {remoteIp} after {_options.Value.IpBanMinute} minutes.");
                    UnbanIp(remoteIp);
                }
                else
                {
                    _logger.Information($"[IP-RATE-LIMITER] IP {remoteIp} has been banned");
                    var message = _options.Value.IpBanResponse!.Content!;
                    context.Response.StatusCode = (int)_options.Value.IpBanResponse!.StatusCode!;
                    context.Response.ContentType = _options.Value.IpBanResponse!.ContentType!;
                    return context.Response.WriteAsync(message);
                }
            }

            return _next(context);
        }
    }
}
