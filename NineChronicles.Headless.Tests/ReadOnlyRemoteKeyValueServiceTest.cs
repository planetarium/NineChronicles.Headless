using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Google.Protobuf;
using Grpc.Core;
using Libplanet.Store.Remote;
using Libplanet.Store.Trie;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using Xunit;

namespace NineChronicles.Headless.Tests
{
    public class ReadOnlyRemoteKeyValueServiceTest
    {
        private const string Key = "read-only-remote-kv-test-key-0123456789";
        private const string GraphQLJwtKey = "graphql-jwt-key-that-must-not-be-accepted";

        private static readonly byte[] StoredKey = { 0x01, 0x02 };
        private static readonly byte[] StoredValue = { 0x0a, 0x0b, 0x0c };

        private readonly IKeyValueStore _store = new MemoryKeyValueStore();
        private readonly ReadOnlyRemoteKeyValueService _service;

        public ReadOnlyRemoteKeyValueServiceTest()
        {
            _store.Set(new KeyBytes(StoredKey), StoredValue);
            _service = CreateService(Key);
        }

        [Fact]
        public async Task GetValue_ValidToken()
        {
            var response = await _service.GetValue(GetRequest(StoredKey), Context(Token()));
            Assert.Equal(StoredValue, response.Data.ToByteArray());
        }

        [Fact]
        public async Task GetValue_MissingKeyIsNotFound()
        {
            var e = await Assert.ThrowsAsync<RpcException>(
                () => _service.GetValue(GetRequest(new byte[] { 0xff }), Context(Token())));
            Assert.Equal(StatusCode.NotFound, e.StatusCode);
        }

        [Fact]
        public async Task ExistsKey_ValidToken()
        {
            var exists = await _service.ExistsKey(ExistsRequest(StoredKey), Context(Token()));
            var missing = await _service.ExistsKey(ExistsRequest(new byte[] { 0xff }), Context(Token()));
            Assert.True(exists.Exists);
            Assert.False(missing.Exists);
        }

        public static IEnumerable<object?[]> UnauthenticatedHeaders() => new[]
        {
            new object?[] { null },
            new object?[] { "Basic abc" },
            new object?[] { "Bearer not-a-jwt" },
            new object?[] { "Bearer " + Token(key: "another-key-another-key-another-key") },
            new object?[] { "Bearer " + Token(key: GraphQLJwtKey) },
            new object?[] { "Bearer " + Token(expires: DateTime.UtcNow.AddMinutes(-10)) },
            new object?[] { "Bearer " + Token(expires: DateTime.UtcNow.AddHours(2)) },
            new object?[] { "Bearer " + Token(withExpiry: false) },
            new object?[] { "Bearer " + Token(notBefore: DateTime.UtcNow.AddMinutes(10), expires: DateTime.UtcNow.AddMinutes(30)) },
            new object?[] { "Bearer " + Token(algorithm: SecurityAlgorithms.HmacSha512) },
            new object?[] { "Bearer " + UnsignedToken() },
        };

        [Theory]
        [MemberData(nameof(UnauthenticatedHeaders))]
        public async Task Unauthenticated(string? authorization)
        {
            var context = new FakeServerCallContext(authorization);
            var get = await Assert.ThrowsAsync<RpcException>(() => _service.GetValue(GetRequest(StoredKey), context));
            var exists = await Assert.ThrowsAsync<RpcException>(() => _service.ExistsKey(ExistsRequest(StoredKey), context));
            Assert.Equal(StatusCode.Unauthenticated, get.StatusCode);
            Assert.Equal(StatusCode.Unauthenticated, exists.StatusCode);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("graphql:read")]
        [InlineData("remote-kv:readwrite")]
        public async Task PermissionDeniedWithoutScope(string? scope)
        {
            var e = await Assert.ThrowsAsync<RpcException>(
                () => _service.GetValue(GetRequest(StoredKey), Context(Token(scope: scope))));
            Assert.Equal(StatusCode.PermissionDenied, e.StatusCode);
        }

        [Fact]
        public async Task ScopeAmongOthers()
        {
            var response = await _service.GetValue(
                GetRequest(StoredKey),
                Context(Token(scope: "graphql:read " + ReadOnlyRemoteKeyValueService.RequiredScope)));
            Assert.Equal(StoredValue, response.Data.ToByteArray());
        }

        [Theory]
        [InlineData(null)]
        [InlineData("too-short-key")]
        public async Task RefusesEverythingWithoutUsableKey(string? key)
        {
            var service = CreateService(key);
            var e = await Assert.ThrowsAsync<RpcException>(
                () => service.GetValue(GetRequest(StoredKey), Context(Token())));
            Assert.Equal(StatusCode.Unauthenticated, e.StatusCode);
        }

        [Fact]
        public async Task WritesAndListingAreUnimplemented()
        {
            var context = Context(Token());
            var pair = new KeyValueStorePair
            {
                Key = new KeyValueStoreKey { Data = ByteString.CopyFrom(StoredKey) },
                Value = new KeyValueStoreValue { Data = ByteString.CopyFrom(new byte[] { 0x00 }) },
            };
            var keyMessage = new KeyValueStoreKey { Data = ByteString.CopyFrom(StoredKey) };

            var calls = new Func<Task>[]
            {
                () => _service.SetValue(new SetValueRequest { Item = pair }, context),
                () => _service.SetValues(new SetValuesRequest { Items = { pair } }, context),
                () => _service.DeleteValue(new DeleteValueRequest { Key = keyMessage }, context),
                () => _service.DeleteValues(new DeleteValuesRequest { Keys = { keyMessage } }, context),
                () => _service.ListKeys(new ListKeysRequest(), context),
            };
            foreach (var call in calls)
            {
                var e = await Assert.ThrowsAsync<RpcException>(call);
                Assert.Equal(StatusCode.Unimplemented, e.StatusCode);
            }

            Assert.Equal(StoredValue, _store.Get(new KeyBytes(StoredKey)));
        }

        private ReadOnlyRemoteKeyValueService CreateService(string? key)
        {
            // Jwt:Key is the GraphQL key; it must never be accepted by this service.
            var settings = new Dictionary<string, string?> { ["Jwt:Key"] = GraphQLJwtKey };
            if (key is not null)
            {
                settings[ReadOnlyRemoteKeyValueService.JwtKeyConfigurationKey] = key;
            }

            var configuration = new ConfigurationBuilder().AddInMemoryCollection(settings).Build();
            return new ReadOnlyRemoteKeyValueService(_store, configuration);
        }

        private static GetValueRequest GetRequest(byte[] key) =>
            new GetValueRequest { Key = new KeyValueStoreKey { Data = ByteString.CopyFrom(key) } };

        private static ExistsKeyRequest ExistsRequest(byte[] key) =>
            new ExistsKeyRequest { Key = new KeyValueStoreKey { Data = ByteString.CopyFrom(key) } };

        private static FakeServerCallContext Context(string token) => new FakeServerCallContext("Bearer " + token);

        private static string Token(
            string key = Key,
            string? scope = ReadOnlyRemoteKeyValueService.RequiredScope,
            DateTime? expires = null,
            bool withExpiry = true,
            string algorithm = SecurityAlgorithms.HmacSha256,
            DateTime? notBefore = null)
        {
            var claims = new List<Claim>();
            if (scope is not null)
            {
                claims.Add(new Claim("scope", scope));
            }

            var keyBytes = Encoding.UTF8.GetBytes(key);
            if (algorithm == SecurityAlgorithms.HmacSha512 && keyBytes.Length < 64)
            {
                keyBytes = Encoding.UTF8.GetBytes(key.PadRight(64, '0'));
            }

            var exp = expires ?? DateTime.UtcNow.AddMinutes(5);
            var token = new JwtSecurityToken(
                claims: claims,
                notBefore: notBefore,
                expires: withExpiry ? exp : null,
                signingCredentials: new SigningCredentials(new SymmetricSecurityKey(keyBytes), algorithm));
            return new JwtSecurityTokenHandler { SetDefaultTimesOnTokenCreation = false }.WriteToken(token);
        }

        private static string UnsignedToken()
        {
            static string Encode(string json) => Base64UrlEncoder.Encode(Encoding.UTF8.GetBytes(json));
            var exp = DateTimeOffset.UtcNow.AddMinutes(5).ToUnixTimeSeconds();
            return Encode("{\"alg\":\"none\",\"typ\":\"JWT\"}") + "." +
                   Encode($"{{\"scope\":\"{ReadOnlyRemoteKeyValueService.RequiredScope}\",\"exp\":{exp}}}") + ".";
        }

        private sealed class FakeServerCallContext : ServerCallContext
        {
            private readonly Metadata _requestHeaders = new Metadata();

            public FakeServerCallContext(string? authorization)
            {
                if (authorization is not null)
                {
                    _requestHeaders.Add("authorization", authorization);
                }
            }

            protected override string MethodCore => "/libplanet.rpc.v1.KeyValueStore/GetValue";

            protected override string HostCore => "localhost";

            protected override string PeerCore => "ipv4:127.0.0.1:12345";

            protected override DateTime DeadlineCore => DateTime.MaxValue;

            protected override Metadata RequestHeadersCore => _requestHeaders;

            protected override CancellationToken CancellationTokenCore => CancellationToken.None;

            protected override Metadata ResponseTrailersCore { get; } = new Metadata();

            protected override Status StatusCore { get; set; }

            protected override WriteOptions? WriteOptionsCore { get; set; }

            protected override AuthContext AuthContextCore =>
                new AuthContext(null, new Dictionary<string, List<AuthProperty>>());

            protected override ContextPropagationToken CreatePropagationTokenCore(ContextPropagationOptions? options) =>
                throw new NotSupportedException();

            protected override Task WriteResponseHeadersAsyncCore(Metadata responseHeaders) => Task.CompletedTask;
        }
    }
}
