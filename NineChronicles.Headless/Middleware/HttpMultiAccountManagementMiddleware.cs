using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Libplanet.Common;
using Libplanet.Crypto;
using Libplanet.Types.Tx;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using Nekoyume.Action;
using NineChronicles.Headless.Properties;
using Serilog;
using ILogger = Serilog.ILogger;

namespace NineChronicles.Headless.Middleware
{
    public class HttpMultiAccountManagementMiddleware
    {
        private static readonly Dictionary<Address, DateTimeOffset> MultiAccountTxIntervalTracker = new();
        private static readonly Dictionary<Address, DateTimeOffset> MultiAccountManagementList = new();
        private readonly RequestDelegate _next;
        private readonly ILogger _logger;
        private StandaloneContext _standaloneContext;
        private readonly Dictionary<string, HashSet<Address>> _ipSignerList;
        private readonly IOptions<MultiAccountManagerProperties> _options;
        private ActionEvaluationPublisher _publisher;

        public HttpMultiAccountManagementMiddleware(
            RequestDelegate next,
            StandaloneContext standaloneContext,
            Dictionary<string, HashSet<Address>> ipSignerList,
            IOptions<MultiAccountManagerProperties> options,
            ActionEvaluationPublisher publisher)
        {
            _next = next;
            _logger = Log.Logger.ForContext<HttpMultiAccountManagementMiddleware>();
            _standaloneContext = standaloneContext;
            _ipSignerList = ipSignerList;
            _options = options;
            _publisher = publisher;
        }

        private static void ManageMultiAccount(Address agent)
        {
            MultiAccountManagementList.Add(agent, DateTimeOffset.Now);
        }

        private static void RestoreMultiAccount(Address agent)
        {
            MultiAccountManagementList.Remove(agent);
        }

        public async Task InvokeAsync(HttpContext context)
        {
            // Prevent to harm HTTP/2 communication.
            if (context.Request.Protocol == "HTTP/1.1")
            {
                context.Request.EnableBuffering();
                var remoteIp = context.Connection.RemoteIpAddress!.ToString();
                var body = await new StreamReader(context.Request.Body).ReadToEndAsync();
                context.Request.Body.Seek(0, SeekOrigin.Begin);
                if (_options.Value.EnableManaging)
                {
                    if (body.Contains("agent(address:\\\"") || body.Contains("agent(address: \\\""))
                    {
                        try
                        {
                            var agent = new Address(body.Split("\\\"")[1].Split("0x")[1]);
                            UpdateIpSignerList(remoteIp, agent);
                        }
                        catch (Exception ex)
                        {
                            _logger.Error(
                                "[GRAPHQL-MULTI-ACCOUNT-MANAGER] Error message: {message} Stacktrace: {stackTrace}",
                                ex.Message,
                                ex.StackTrace);
                        }
                    }

                    if (body.Contains("stageTransaction"))
                    {
                        try
                        {
                            var pattern = "64313.*6565";
                            var txPayload = Regex.Match(body, pattern).ToString();
                            byte[] bytes = ByteUtil.ParseHex(txPayload);
                            Transaction tx = Transaction.Deserialize(bytes);
                            var agent = tx.Signer;
                            var action = NCActionUtils.ToAction(tx.Actions.Actions.First());

                            // Only monitoring actions not used in the launcher
                            if (action is not Stake
                                and not ClaimStakeReward
                                and not TransferAsset)
                            {
                                if (_ipSignerList.ContainsKey(remoteIp))
                                {
                                    if (_ipSignerList[remoteIp].Count > _options.Value.ThresholdCount)
                                    {
                                        _logger.Information(
                                            "[GRAPHQL-MULTI-ACCOUNT-MANAGER] IP: {IP} List Count: {Count}, AgentAddresses: {Agent}",
                                            remoteIp,
                                            _ipSignerList[remoteIp].Count,
                                            _ipSignerList[remoteIp]);

                                        if (!MultiAccountManagementList.ContainsKey(agent))
                                        {
                                            if (!MultiAccountTxIntervalTracker.ContainsKey(agent))
                                            {
                                                _logger.Information($"[GRAPHQL-MULTI-ACCOUNT-MANAGER] Adding agent {agent} to the agent tracker.");
                                                MultiAccountTxIntervalTracker.Add(agent, DateTimeOffset.Now);
                                            }
                                            else
                                            {
                                                if ((DateTimeOffset.Now - MultiAccountTxIntervalTracker[agent]).Minutes >= _options.Value.TxIntervalMinutes)
                                                {
                                                    _logger.Information($"[GRAPHQL-MULTI-ACCOUNT-MANAGER] Resetting Agent {agent}'s time because it has been more than {_options.Value.TxIntervalMinutes} minutes since the last transaction.");
                                                    MultiAccountTxIntervalTracker[agent] = DateTimeOffset.Now;
                                                }
                                                else
                                                {
                                                    _logger.Information($"[GRAPHQL-MULTI-ACCOUNT-MANAGER] Managing Agent {agent} for {_options.Value.ManagementTimeMinutes} minutes due to {_ipSignerList[remoteIp].Count} associated accounts.");
                                                    ManageMultiAccount(agent);
                                                    MultiAccountTxIntervalTracker[agent] = DateTimeOffset.Now;
                                                    await CancelRequestAsync(context);
                                                    return;
                                                }
                                            }
                                        }
                                        else
                                        {
                                            var currentManagedTime = (DateTimeOffset.Now - MultiAccountManagementList[agent]).Minutes;
                                            if (currentManagedTime > _options.Value.ManagementTimeMinutes)
                                            {
                                                _logger.Information($"[GRAPHQL-MULTI-ACCOUNT-MANAGER] Restoring Agent {agent} after {_options.Value.ManagementTimeMinutes} minutes.");
                                                RestoreMultiAccount(agent);
                                                MultiAccountTxIntervalTracker[agent] = DateTimeOffset.Now.AddMinutes(-_options.Value.TxIntervalMinutes);
                                                _logger.Information($"[GRAPHQL-MULTI-ACCOUNT-MANAGER] Current time: {DateTimeOffset.Now} Added time: {DateTimeOffset.Now.AddMinutes(-_options.Value.TxIntervalMinutes)}.");
                                            }
                                            else
                                            {
                                                _logger.Information($"[GRAPHQL-MULTI-ACCOUNT-MANAGER] Agent {agent} is in managed status for the next {_options.Value.ManagementTimeMinutes - currentManagedTime} minutes.");
                                                await CancelRequestAsync(context);
                                                return;
                                            }
                                        }
                                    }
                                }
                                else
                                {
                                    UpdateIpSignerList(remoteIp, agent);
                                }
                            }

                        }
                        catch (Exception ex)
                        {
                            _logger.Error(
                                "[GRAPHQL-MULTI-ACCOUNT-MANAGER] Error message: {message} Stacktrace: {stackTrace}",
                                ex.Message,
                                ex.StackTrace);
                        }
                    }
                }
            }

            await _next(context);
        }

        private void UpdateIpSignerList(string ip, Address agent)
        {
            if (!_ipSignerList.ContainsKey(ip))
            {
                _logger.Information(
                    "[GRAPHQL-MULTI-ACCOUNT-MANAGER] Creating a new list for IP: {IP}",
                    ip);
                _ipSignerList[ip] = new HashSet<Address>();
            }
            else
            {
                _logger.Information(
                    "[GRAPHQL-MULTI-ACCOUNT-MANAGER] List already created for IP: {IP} Count: {Count}",
                    ip,
                    _ipSignerList[ip].Count);
            }

            _ipSignerList[ip].Add(agent);
            AddClientIpInfo(agent, ip);
        }

        private void AddClientIpInfo(Address agentAddress, string ipAddress)
        {
            _publisher.AddClientAndIp(ipAddress, agentAddress.ToString());
        }

        private async Task CancelRequestAsync(HttpContext context)
        {
            var message = "{ \"message\": \"Request cancelled.\" }";
            context.Response.StatusCode = 403;
            context.Response.ContentType = "application/json";
            await context.Response.WriteAsync(message);
        }
    }
}
