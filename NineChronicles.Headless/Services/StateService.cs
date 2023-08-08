using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace NineChronicles.Headless.Services;

public class StateService : IDisposable
{
    public StateService(string path, int port, string remoteBlockChainStatesEndpoint)
    {
        Process = new Process();
        Process.StartInfo.FileName = "dotnet";
        Process.StartInfo.Arguments = $"{path} --urls=http://localhost:{port}";
        Process.StartInfo.EnvironmentVariables["RemoteBlockChainStatesEndpoint"] = remoteBlockChainStatesEndpoint;
        Process.Start();
    }

    public Task StartAsync(CancellationToken token) => Process.WaitForExitAsync(token);

    public Task StopAsync(CancellationToken token)
    {
        Process.Kill();
        return Task.CompletedTask;
    }

    public void Dispose() => Process.Dispose();

    private Process Process { get; }
}
