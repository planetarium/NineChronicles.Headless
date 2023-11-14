using System.IO.Compression;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using Libplanet.Action;
using Libplanet.Action.Loader;
using Libplanet.Common;
using Libplanet.Types.Blocks;
using Serilog;

namespace Libplanet.Extensions.StateServiceActionEvaluator;

public class StateServiceActionEvaluator : IActionEvaluator
{
    private readonly List<StateServiceWithMetadata> _serviceWithRanges;
    private StateService? _currentService;
    private long _currentServiceStartIndex = 0;
    private long _currentServiceEndIndex = 0;

    public StateServiceActionEvaluator(
        IEnumerable<StateServiceWithMetadata> serviceWithRanges,
        string stateServiceDownloadPath)
    {
        _serviceWithRanges = serviceWithRanges.ToList();
        CheckCollision(_serviceWithRanges);
        DownloadStateServices(_serviceWithRanges, stateServiceDownloadPath);
        _serviceWithRanges = _serviceWithRanges.Select(ToStateServiceDllPath).ToList();
    }
    
    ~StateServiceActionEvaluator()
    {
        _currentService?.StopAsync(new CancellationToken()).Wait();
        _currentService?.Dispose();
    }

    public IActionLoader ActionLoader => throw new NotSupportedException();
    public IReadOnlyList<ICommittedActionEvaluation> Evaluate(
        IPreEvaluationBlock block, HashDigest<SHA256>? baseStateRootHash)
    {
        var service = GetService(block.Index);
        var remoteActionEvaluator = new RemoteActionEvaluator.RemoteActionEvaluator(service.Uri);
        return remoteActionEvaluator.Evaluate(block, baseStateRootHash);
    }
    
    private StateService GetService(long blockIndex)
    {
        if (_currentService is null)
        {
            StateServiceWithMetadata? service = _serviceWithRanges
                .FirstOrDefault(s => s.Range.Start <= blockIndex && blockIndex <= s.Range.End);
            if (service is null)
            {
                throw new InvalidOperationException("No service found.");
            }
            _currentService = service.Value.StateService;
            _currentServiceStartIndex = service.Value.Range.Start;
            _currentServiceEndIndex = service.Value.Range.End;
            _currentService.StartAsync(new CancellationToken()).Wait();
            return service.Value.StateService;
        }

        if (_currentServiceStartIndex <= blockIndex && blockIndex <= _currentServiceEndIndex)
        {
            return _currentService;
        }

        _currentService.StopAsync(new CancellationToken()).Wait();
        _currentService.Dispose();
        _currentService = null;
        return GetService(blockIndex);
    } 
    
    private static void CheckCollision(List<StateServiceWithMetadata> serviceWithRanges)
    {
        if (serviceWithRanges.Count == 0)
        {
            throw new ArgumentOutOfRangeException(nameof(serviceWithRanges), "It must have one more paris at least.");
        }

        if (serviceWithRanges[0].Range.Start != 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(serviceWithRanges),
                "The pairs must cover all range of blockchain. Its first element's start index wasn't 0.");
        }

        if (serviceWithRanges.Last().Range.End != long.MaxValue)
        {
            throw new ArgumentOutOfRangeException(
                nameof(serviceWithRanges),
                "The pairs must cover all range over blockchain. Its last element's start index wasn't 0.");
        }

        if (serviceWithRanges.Count == 1)
        {
            return;
        }

        long previousPairEndIndex = serviceWithRanges[0].Range.End;
        foreach (StateServiceWithMetadata stateServiceWithRange in serviceWithRanges.Skip(1))
        {
            if (previousPairEndIndex + 1 != stateServiceWithRange.Range.Start)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(stateServiceWithRange),
                    "The pairs must cover all range over blockchain. " +
                    "So Nth pair's end index + 1 must be same as N+1th pair's start index. " +
                    $"But Nth pair's end index is {previousPairEndIndex} and N+1th pair's start index is {stateServiceWithRange.Range.Start}");
            }
            previousPairEndIndex = stateServiceWithRange.Range.End;
        }
    }

    private static StateServiceWithMetadata ToStateServiceDllPath(StateServiceWithMetadata stateServiceWithMetadata)
    {
        if (!Uri.IsWellFormedUriString(stateServiceWithMetadata.StateService.Path, UriKind.Absolute))
        {
            return stateServiceWithMetadata;
        }

        var dllPath = Path.Join(
            stateServiceWithMetadata.StateServiceDownloadPath,
            Convert.ToHexString(
                HashDigest<SHA256>.DeriveFrom(
                    Encoding.UTF8.GetBytes(stateServiceWithMetadata.StateService.Path))
                    .ToByteArray()));
        return new StateServiceWithMetadata(
            stateServiceWithMetadata.StateService.ChangePath(dllPath),
            stateServiceWithMetadata.Range,
            stateServiceWithMetadata.StateServiceDownloadPath);
    }
    
    private static void DownloadStateServices(
        List<StateServiceWithMetadata> serviceWithRanges,
        string stateServiceDownloadPath)
    {
        if (Directory.Exists(stateServiceDownloadPath))
        {
            Directory.Delete(stateServiceDownloadPath, true);
        }

        Directory.CreateDirectory(stateServiceDownloadPath);
        
        async Task DownloadStateService(string url)
        {
            if (!Uri.IsWellFormedUriString(url, UriKind.Absolute))
            {
                return;
            }

            var hashed =
                Convert.ToHexString(HashDigest<SHA256>.DeriveFrom(Encoding.UTF8.GetBytes(url)).ToByteArray());
            var logger = Log.ForContext("StateService", hashed);
            using var httpClient = new HttpClient();
            var downloadPath = Path.Join(stateServiceDownloadPath, hashed + ".zip");
            var extractPath = Path.Join(stateServiceDownloadPath, hashed);
            logger.Debug("Downloading...");
            await File.WriteAllBytesAsync(downloadPath, await httpClient.GetByteArrayAsync(url));
            logger.Debug("Finished downloading.");
            logger.Debug("Extracting...");
            ZipFile.ExtractToDirectory(downloadPath, extractPath);
            logger.Debug("Finished extracting.");
        }

        Task.WhenAll(serviceWithRanges.Select(stateService => DownloadStateService(stateService.StateService.Path)))
            .ContinueWith(_ => Log.Information("Finished downloading StateServices..."))
            .Wait();
    }
}
