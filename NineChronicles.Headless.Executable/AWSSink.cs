using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Amazon;
using Amazon.CloudWatchLogs;
using Amazon.CloudWatchLogs.Model;
using Amazon.Runtime;
using Nito.AsyncEx;
using Serilog.Core;
using Serilog.Events;

namespace NineChronicles.Headless.Executable
{
    public class AWSSink : ILogEventSink, IDisposable
    {
        private readonly AmazonCloudWatchLogsClient _client;
        private readonly CancellationTokenSource _cancellationTokenSource;

        private AsyncProducerConsumerQueue<LogEvent> _queue;

        public AWSSink(AWSCredentials credentials, RegionEndpoint regionEndPoint, string logGroupName, string logStreamName)
        {
            Config = new AmazonCloudWatchLogsConfig();
            Config.RegionEndpoint = regionEndPoint;
            _queue = new AsyncProducerConsumerQueue<LogEvent>();
            _client = new AmazonCloudWatchLogsClient(credentials, Config);
            _cancellationTokenSource = new CancellationTokenSource();

            LogGroupName = logGroupName;
            LogStreamName = logStreamName;

            Worker(_cancellationTokenSource.Token);
        }

        public void Dispose()
        {
            _cancellationTokenSource.Cancel();
            _client.Dispose();
        }

        public AmazonCloudWatchLogsConfig Config { get; }

        public string LogGroupName { get; }

        public string LogStreamName { get; }

        public void Emit(LogEvent logEvent)
        {
            _queue.Enqueue(logEvent);
        }

        private async Task Worker(CancellationToken cancellationToken)
        {
            string sequenceToken = "token";
            try
            {
                await _client.CreateLogStreamAsync(
                    new CreateLogStreamRequest(LogGroupName, LogStreamName), cancellationToken);
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

            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    LogEvent logEvent = await _queue.DequeueAsync(cancellationToken);
                    var request = new PutLogEventsRequest(LogGroupName, LogStreamName, new List<InputLogEvent>
                    {
                        new InputLogEvent
                        {
                            Message = logEvent.RenderMessage(),
                            Timestamp = logEvent.Timestamp.UtcDateTime,
                        }
                    });
                    request.SequenceToken = sequenceToken;
                    PutLogEventsResponse response = await PutLogEventsAsync(request, cancellationToken);
                    sequenceToken = response.NextSequenceToken;
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

            Console.Error.WriteLine("Worker ended.");
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
                return await _client.PutLogEventsAsync(request, cancellationToken);
            }
        }
    }
}
