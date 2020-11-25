using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Amazon;
using Amazon.CloudWatchLogs;
using Amazon.CloudWatchLogs.Model;
using Amazon.Runtime;
using JetBrains.Annotations;
using Nito.AsyncEx;
using Serilog.Core;
using Serilog.Events;

namespace NineChronicles.Standalone.Executable
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
            string sequenceToken = await GetSequenceToken(LogGroupName, LogStreamName);
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

                    var resp = await _client.PutLogEventsAsync(request, cancellationToken);
                    sequenceToken = resp.NextSequenceToken;
                }
                catch (OperationCanceledException e)
                {
                    Console.Error.WriteLine($"Worker canceled: {e}.");
                    throw;
                }
                catch (Exception e)
                {
                    Console.Error.WriteLine($"Worker exception occured: {e}.");
                }
            }

            Console.Error.WriteLine("Worker ended.");
        }

        private async Task<string> GetSequenceToken(string logGroupName, string logStreamName)
        {
            await CreateLogStreamAsync(logGroupName, logStreamName);

            var logStreamsResponse = await _client.DescribeLogStreamsAsync(new DescribeLogStreamsRequest(logGroupName)
            {
                LogStreamNamePrefix = logStreamName,
            });

            var stream = logStreamsResponse.LogStreams.First(s => s.LogStreamName == logStreamName);
            return stream.UploadSequenceToken;
        }

        private async Task CreateLogStreamAsync(string logGroupName, string logStreamName)
        {
            try
            {
                await _client.CreateLogStreamAsync(new CreateLogStreamRequest(logGroupName, logStreamName));
            }
            catch (ResourceAlreadyExistsException)
            {
            }
        }
    }
}
