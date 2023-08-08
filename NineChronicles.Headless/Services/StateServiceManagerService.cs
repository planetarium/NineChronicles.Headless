using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using GraphQL;
using Libplanet.Common;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using NineChronicles.Headless.Properties;

namespace NineChronicles.Headless.Services;

public class StateServiceManagerService : IHostedService, IDisposable
{
    private IEnumerable<StateService> StateServices { get; init; }

    public StateServiceManagerService(StateServiceManagerServiceOptions options)
    {
        if (options.StateServices is null || options.StateServices.Any(x => x.Path is null))
        {
            throw new ArgumentException(nameof(options));
        }

        string ToStateServiceDLLPath(string url)
        {
            return Path.Join(
                options.StateServicesDownloadPath,
                Convert.ToHexString(HashDigest<SHA256>.DeriveFrom(Encoding.UTF8.GetBytes(url)).ToByteArray()),
                "Lib9c.StateService.dll");
        }

        StateServices = options.StateServices.Select(conf =>
            conf.Path is null
                ? throw new ArgumentException(nameof(options))
                : new StateService(ToStateServiceDLLPath(conf.Path), conf.Port, options.RemoteBlockChainStatesEndpoint)).ToList();
    }

    public Task StartAsync(CancellationToken cancellationToken) =>
        Task.WhenAny(StateServices.Select(s => s.StartAsync(cancellationToken)));

    public Task StopAsync(CancellationToken cancellationToken) =>
        Task.WhenAny(StateServices.Select(s => s.StopAsync(cancellationToken)));

    public void Dispose() =>
        Task.Run(() =>
        {
            foreach (StateService stateService in StateServices)
            {
                stateService.Dispose();
            }
        });
}
