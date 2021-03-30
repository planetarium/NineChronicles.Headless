using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;

namespace NineChronicles.Headless.Tests
{
    public class InMemorySession : ISession
    {
        private readonly Dictionary<string, byte[]> _value;

        public InMemorySession(string id, bool isAvailable, Dictionary<string, byte[]>? value = null)
        {
            Id = id;
            IsAvailable = isAvailable;
            _value = value ?? new Dictionary<string, byte[]>();
        }

        /// <inheritdoc/>
        public void Clear()
        {
            _value.Clear();
        }

        /// <summary>
        /// It doesn't do anything because it stores data in memory.
        /// </summary>
        /// <param name="cancellationToken">A cancellation token to broadcast the cancellation.</param>
        /// <returns>An awaitable task.</returns>
        public Task CommitAsync(CancellationToken cancellationToken = new CancellationToken())
        {
            return Task.CompletedTask;
        }

        /// <summary>
        /// It doesn't do anything because it stores data in memory.
        /// </summary>
        /// <param name="cancellationToken">A cancellation token to broadcast the cancellation.</param>
        /// <returns>An awaitable task.</returns>
        public Task LoadAsync(CancellationToken cancellationToken = new CancellationToken())
        {
            return Task.CompletedTask;
        }

        /// <inheritdoc/>
        public void Remove(string key)
        {
            _value.Remove(key);
        }

        /// <inheritdoc/>
        public void Set(string key, byte[] value)
        {
            _value[key] = value;
        }

        /// <inheritdoc/>
        public bool TryGetValue(string key, out byte[]? value)
        {
            return _value.TryGetValue(key, out value);
        }

        /// <inheritdoc/>
        public string Id { get; }

        /// <inheritdoc/>
        public bool IsAvailable { get; }

        /// <inheritdoc/>
        public IEnumerable<string> Keys => Value.Keys;

        /// <summary>
        /// The storage to store data of the session in memory.
        /// </summary>
        public IReadOnlyDictionary<string, byte[]> Value => _value;
    }
}
