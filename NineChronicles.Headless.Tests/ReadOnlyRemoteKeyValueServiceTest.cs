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
        private const string Key = "read-only-remote-kv-test-key";
        private const string Issuer = "planetariumhq.com";

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
            new object?[] { "Bearer " + Token(key: "another-key") },
            new object?[] { "Bearer " + Token(issuer: "someone-else") },
            new object?[] { "Bearer " + Token(expires: DateTime.UtcNow.AddMinutes(-10)) },
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

        [Fact]
        public async Task RefusesEverythingWithoutConfiguredKey()
        {
            var service = CreateService(null);
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
            var settings = new Dictionary<string, string?> { ["Jwt:Issuer"] = Issuer };
            if (key is not null)
            {
                settings["Jwt:Key"] = key;
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
            string issuer = Issuer,
            string? scope = ReadOnlyRemoteKeyValueService.RequiredScope,
            DateTime? expires = null)
        {
            var claims = new List<Claim>();
            if (scope is not null)
            {
                claims.Add(new Claim("scope", scope));
            }

            var exp = expires ?? DateTime.UtcNow.AddMinutes(5);
            var token = new JwtSecurityToken(
                issuer: issuer,
                claims: claims,
                notBefore: exp.AddMinutes(-10),
                expires: exp,
                signingCredentials: new SigningCredentials(
                    new SymmetricSecurityKey(Encoding.ASCII.GetBytes(key.PadRight(512 / 8, '\0'))),
                    SecurityAlgorithms.HmacSha256));
            return new JwtSecurityTokenHandler().WriteToken(token);
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
