using System.Diagnostics;
using Serilog;

namespace Libplanet.Extensions.StateServiceActionEvaluator;

public class StateService : IDisposable
{
    public Uri Uri { get; } 
    
    public string Path { get; }
    
    public int Port { get; }
    
    public bool Running { get; private set; }
    
    private string _stateStorePath { get; }
    
    public StateService(string path, int port, string stateStorePath)
    {
        Process = new Process();
        Process.StartInfo.FileName = "dotnet";
        Process.StartInfo.Arguments = $"{path} --urls=http://localhost:{port}";
        Process.StartInfo.EnvironmentVariables["StateStorePath"] = System.IO.Path.Combine(stateStorePath, "states");
        Process.StartInfo.RedirectStandardError = true;
        Process.StartInfo.RedirectStandardOutput = true;
        Process.OutputDataReceived += (_, args) => Log.Debug($"[StateService] {args.Data}");
        Process.ErrorDataReceived += (_, args) => Log.Error($"[StateService] {args.Data}");

        Uri = new Uri($"http://localhost:{port}/evaluation");
        Path = path;
        Port = port;
        Running = false;
        _stateStorePath = stateStorePath;
    }

    public Task StartAsync(CancellationToken token)
    {
        if (Process.Start())
        {
            throw new InvalidOperationException("Failed to start StateService.");
        }
        Process.BeginOutputReadLine();
        Process.BeginErrorReadLine();

        Task.Delay(10, token);
        Running = true;
        return Task.CompletedTask;
    }

    public async Task StopAsync(CancellationToken token)
    {
        Process.Kill();
        await Process.WaitForExitAsync(token);
        Process.CancelOutputRead();
        Process.CancelErrorRead();
        Running = false;
    }
    
    public StateService ChangePath(string path)
    {
        return new StateService(path, Port, _stateStorePath);
    }

    public void Dispose() => Process.Dispose();

    private Process Process { get; }
}
