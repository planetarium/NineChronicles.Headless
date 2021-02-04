using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Amazon;
using Amazon.CloudWatchLogs;
using Amazon.CloudWatchLogs.Model;
using Amazon.Runtime;
using Serilog.Events;
using Serilog.Sinks.PeriodicBatching;

namespace NineChronicles.Headless.Executable
{
    public class AWSSink : IBatchedLogEventSink, IDisposable
    {
        private readonly AmazonCloudWatchLogsClient _client;
        private readonly CancellationTokenSource _cancellationTokenSource;

        public AWSSink(AWSCredentials credentials, RegionEndpoint regionEndPoint, string logGroupName, string logStreamName)
        {
            Config = new AmazonCloudWatchLogsConfig();
            Config.RegionEndpoint = regionEndPoint;
            _client = new AmazonCloudWatchLogsClient(credentials, Config);
            _cancellationTokenSource = new CancellationTokenSource();

            LogGroupName = logGroupName;
            LogStreamName = logStreamName;
        }

        public void Dispose()
        {
            _cancellationTokenSource.Cancel();
            _client.Dispose();
        }

        public AmazonCloudWatchLogsConfig Config { get; }

        public string LogGroupName { get; }

        public string LogStreamName { get; }

        public async Task EmitBatchAsync(IEnumerable<LogEvent> batch)
        {
            string sequenceToken = "token";
            try
            {
                await _client.CreateLogStreamAsync(new CreateLogStreamRequest(LogGroupName, LogStreamName));
            }
            catch (ResourceAlreadyExistsException)
            {
                // ignore
            }
            catch (Exception e)
            {
                Console.Error.WriteLine("Unexpected exception occurred during CreateLogStreamAsync. {0}", e);
                throw;
            }

            try
            {
                List<InputLogEvent> inputLogEvents = batch.Select(
                    e => new InputLogEvent
                    {
                        Message = e.RenderMessage(),
                        Timestamp = e.Timestamp.UtcDateTime,
                    }).ToList();

                var request = new PutLogEventsRequest(LogGroupName, LogStreamName, inputLogEvents);
                request.SequenceToken = sequenceToken;
                await PutLogEventsAsync(request, CancellationToken.None);
            }
            catch (OperationCanceledException e)
            {
                Console.Error.WriteLine($"Worker canceled: {e}.");
                throw;
            }
            catch (Exception e)
            {
                Console.Error.WriteLine($"Worker exception occurred: {e}.");
            }
        }

        public Task OnEmptyBatchAsync()
        {
            return Task.CompletedTask;
        }

        private async Task<PutLogEventsResponse> PutLogEventsAsync(
            PutLogEventsRequest request,
            CancellationToken cancellationToken)
        {
            try
            {
                return await _client.PutLogEventsAsync(request, cancellationToken);
            }
            catch (InvalidSequenceTokenException e)
            {
                // Try once more with expected sequence token.
                request.SequenceToken = e.ExpectedSequenceToken;
                return await PutLogEventsAsync(request, cancellationToken);
            }
        }
    }
}
