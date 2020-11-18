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

        public AWSSink(AWSCredentials credentials, RegionEndpoint regionEndPoint, Func<string> logGroupNameGetter, Func<string> logStreamNameGetter = null)
        {
            Config = new AmazonCloudWatchLogsConfig();
            Config.RegionEndpoint = regionEndPoint;
            _queue = new AsyncProducerConsumerQueue<LogEvent>();
            _client = new AmazonCloudWatchLogsClient(credentials, Config);
            _cancellationTokenSource = new CancellationTokenSource();

            LogGroupNameGetter = logGroupNameGetter;
            LogStreamNameGetter = logStreamNameGetter;

            Worker(_cancellationTokenSource.Token);
        }

        public void Dispose()
        {
            _client.Dispose();
            _cancellationTokenSource.Cancel();
        }

        public AmazonCloudWatchLogsConfig Config { get; }

        public Func<string> LogGroupNameGetter { get; set; }

        [CanBeNull] public Func<string> LogStreamNameGetter { get; set; }

        public void Emit(LogEvent logEvent)
        {
            _queue.Enqueue(logEvent);
        }

        private async Task Worker(CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                while (LogStreamNameGetter is null)
                {
                    await Task.Yield();
                }

                string logGroupName = LogGroupNameGetter();
                string logStreamName = LogStreamNameGetter();
                await CreateLogGroup(logGroupName);
                await CreateLogStreamAsync(logGroupName, logStreamName);
                string sequenceToken = await GetSequenceToken(logGroupName, logStreamName);

                cancellationToken.ThrowIfCancellationRequested();
                var logEvent = await _queue.DequeueAsync(cancellationToken);
                var request = new PutLogEventsRequest(logGroupName, logStreamName, new List<InputLogEvent>
                {
                    new InputLogEvent
                    {
                        Message = logEvent.RenderMessage(),
                        Timestamp = logEvent.Timestamp.UtcDateTime,
                    }
                });
                request.SequenceToken = sequenceToken;

                await _client.PutLogEventsAsync(request, cancellationToken);
            }
        }

        private async Task<string> GetSequenceToken(string logGroupName, string logStreamName)
        {
            var logStreamsResponse = await _client.DescribeLogStreamsAsync(new DescribeLogStreamsRequest(logGroupName)
            {
                LogStreamNamePrefix = logStreamName,
            });

            var stream = logStreamsResponse.LogStreams.First(s => s.LogStreamName == logStreamName);
            return stream.UploadSequenceToken;
        }

        private async Task CreateLogGroup(string logGroupName)
        {
            try
            {
                await _client.CreateLogGroupAsync(new CreateLogGroupRequest(logGroupName));
            }
            catch (ResourceAlreadyExistsException)
            {
            }
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
