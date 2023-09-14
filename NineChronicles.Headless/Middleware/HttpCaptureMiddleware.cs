using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Libplanet.Common;
using Libplanet.Crypto;
using Libplanet.Types.Tx;
using Microsoft.AspNetCore.Http;
using Nekoyume.Action;
using Nekoyume.Blockchain;
using Serilog;
using ILogger = Serilog.ILogger;

namespace NineChronicles.Headless.Middleware
{
    public class HttpCaptureMiddleware
    {
        private const int MultiAccountManagementTime = 60;
        private const int MultiAccountTxInterval = 60;
        private static Dictionary<Address, DateTimeOffset> _multiAccountTxIntervalTracker = new();
        private static Dictionary<Address, DateTimeOffset> _multiAccountList = new();
        private readonly RequestDelegate _next;
        private readonly ILogger _logger;
        private StandaloneContext _standaloneContext;
        private Dictionary<string, HashSet<Address>> _ipSignerList;
        private ActionEvaluationPublisher _publisher;

        public HttpCaptureMiddleware(
            RequestDelegate next,
            StandaloneContext standaloneContext,
            Dictionary<string, HashSet<Address>> ipSignerList,
            ActionEvaluationPublisher publisher)
        {
            _next = next;
            _logger = Log.Logger.ForContext<HttpCaptureMiddleware>();
            _standaloneContext = standaloneContext;
            _ipSignerList = ipSignerList;
            _publisher = publisher;
        }

        private static void ManageMultiAccount(Address agent)
        {
            _multiAccountList.Add(agent, DateTimeOffset.Now);
        }

        private static void RestoreMultiAccount(Address agent)
        {
            _multiAccountList.Remove(agent);
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
                        UpdateIpSignerList(remoteIp!.ToString(), agent);
                        AddClientIpInfo(agent, remoteIp.ToString());
                    }
                    catch (Exception ex)
                    {
                        _logger.Error(
                            "[GRAPHQL-REQUEST-CAPTURE-SIGNER] Error message: {message} Stacktrace: {stackTrace}",
                            ex.Message,
                            ex.StackTrace);
                    }
                }

                if (body.Contains("arenaInformation"))
                {
                    try
                    {
                        _logger.Information("[GRAPHQL-REQUEST-CAPTURE] Cancelling ArenaInformation Query from IP: {IP}",
                            remoteIp);
                        await CancelRequestAsync(context);
                        return;
                    }
                    catch (Exception ex)
                    {
                        _logger.Error(
                            "[GRAPHQL-REQUEST-CAPTURE-SIGNER] Error message: {message} Stacktrace: {stackTrace}",
                            ex.Message,
                            ex.StackTrace);
                    }
                }

                if (body.Contains("actionQuery{combinationEquipment") || body.Contains("actionQuery{hackAndSlash"))
                {
                    try
                    {
                        _logger.Information("[GRAPHQL-REQUEST-CAPTURE] Cancelling actionQuery from IP: {IP}",
                            remoteIp);
                        await CancelRequestAsync(context);
                        return;
                    }
                    catch (Exception ex)
                    {
                        _logger.Error(
                            "[GRAPHQL-REQUEST-CAPTURE-SIGNER] Error message: {message} Stacktrace: {stackTrace}",
                            ex.Message,
                            ex.StackTrace);
                    }
                }

                if (body.Contains("stageTransaction"))
                {
                    try
                    {
                        var txPayload = body.Split("\\\"")[1];
                        byte[] bytes = ByteUtil.ParseHex(txPayload);
                        Transaction tx = Transaction.Deserialize(bytes);
                        var agent = tx.Signer;
                        var action = NCActionUtils.ToAction(tx.Actions.Actions.First());
                        if (action is HackAndSlash || action is CombinationEquipment || action is CombinationConsumable)
                        {
                            UpdateIpSignerList("1", agent);
                            AddClientIpInfo(agent, "1");
                        }

                        _logger.Information("[GRAPHQL-REQUEST-CAPTURE] IP: {IP} Agent: {Agent} Tx: {Path}",
                            remoteIp, agent, tx.Actions.Actions.FirstOrDefault());
                        if (_ipSignerList.ContainsKey(context.Connection.RemoteIpAddress!.ToString()))
                        {
                            if (_ipSignerList[context.Connection.RemoteIpAddress!.ToString()].Count > 29)
                            {
                                if (!_multiAccountList.ContainsKey(agent))
                                {
                                    if (!_multiAccountTxIntervalTracker.ContainsKey(agent))
                                    {
                                        _logger.Information($"[GRAPHQL-REQUEST-CAPTURE] Adding agent {agent} to the agent tracker.");
                                        _multiAccountTxIntervalTracker.Add(agent, DateTimeOffset.Now);
                                    }
                                    else
                                    {
                                        if ((DateTimeOffset.Now - _multiAccountTxIntervalTracker[agent]).Minutes >= MultiAccountTxInterval)
                                        {
                                            _logger.Information($"[GRAPHQL-REQUEST-CAPTURE] Resetting Agent {agent}'s time because it has been more than {MultiAccountTxInterval} minutes since the last transaction.");
                                            _multiAccountTxIntervalTracker[agent] = DateTimeOffset.Now;
                                        }
                                        else
                                        {
                                            _logger.Information($"[GRAPHQL-REQUEST-CAPTURE] Managing Agent {agent} for {MultiAccountManagementTime} minutes due to {_ipSignerList[context.Connection.RemoteIpAddress!.ToString()].Count} associated accounts.");
                                            ManageMultiAccount(agent);
                                            _multiAccountTxIntervalTracker[agent] = DateTimeOffset.Now;
                                            var ncStagePolicy = (NCStagePolicy)_standaloneContext.BlockChain!.StagePolicy;
                                            ncStagePolicy.BannedAccounts = ncStagePolicy.BannedAccounts.Add(agent);
                                            await CancelRequestAsync(context);
                                            return;
                                        }
                                    }
                                }
                                else
                                {
                                    if ((DateTimeOffset.Now - _multiAccountList[agent]).Minutes >= MultiAccountManagementTime)
                                    {
                                        _logger.Information($"[GRAPHQL-REQUEST-CAPTURE] Restoring Agent {agent} after {MultiAccountManagementTime} minutes.");
                                        RestoreMultiAccount(agent);
                                        _multiAccountTxIntervalTracker[agent] = DateTimeOffset.Now.AddMinutes(-MultiAccountTxInterval);
                                        _logger.Information($"[GRAPHQL-REQUEST-CAPTURE] Current time: {DateTimeOffset.Now} Added time: {DateTimeOffset.Now.AddMinutes(-MultiAccountTxInterval)}.");
                                        var ncStagePolicy = (NCStagePolicy)_standaloneContext.BlockChain!.StagePolicy;
                                        ncStagePolicy.BannedAccounts = ncStagePolicy.BannedAccounts.Remove(agent);
                                    }
                                    else
                                    {
                                        _logger.Information($"[GRAPHQL-REQUEST-CAPTURE] Agent {agent} is in managed status for the next {MultiAccountManagementTime - (DateTimeOffset.Now - _multiAccountList[agent]).Minutes} minutes.");
                                    }
                                }
                            }
                        }
                        else
                        {
                            UpdateIpSignerList(remoteIp!.ToString(), agent);
                            AddClientIpInfo(agent, remoteIp.ToString());
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

        public void UpdateIpSignerList(string ip, Address agent)
        {
            if (!_ipSignerList.ContainsKey(ip))
            {
                _logger.Information(
                    "[GRAPHQL-REQUEST-CAPTURE] Creating a new list for IP: {IP}",
                    ip);
                _ipSignerList[ip] = new HashSet<Address>();
            }
            else
            {
                _logger.Information(
                    "[GRAPHQL-REQUEST-CAPTURE] List already created for IP: {IP} Count: {Count}",
                    ip,
                    _ipSignerList[ip].Count);
            }

            _ipSignerList[ip].Add(agent);
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
