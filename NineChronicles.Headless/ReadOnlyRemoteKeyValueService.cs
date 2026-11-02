using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Security.Claims;
using System.Text;
using System.Threading.Tasks;
using Google.Protobuf;
using Grpc.Core;
using Libplanet.Store.Remote;
using Libplanet.Store.Trie;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using Serilog;

namespace NineChronicles.Headless;

/// <summary>
/// Read-only variant of Libplanet's <c>RemoteKeyValueService</c>, for remote replays
/// (e.g. <c>replay remote-tx</c>) against public nodes.
/// <para>
/// Only <see cref="GetValue"/> and <see cref="ExistsKey"/> are implemented; writes, deletes and
/// <c>ListKeys</c> are left to the generated base class, which answers <see cref="StatusCode.Unimplemented"/>.
/// Every call requires a JWT signed with <c>Jwt:Key</c> (issuer <c>Jwt:Issuer</c>) whose <c>scope</c>
/// claim contains <see cref="RequiredScope"/>. Without <c>Jwt:Key</c> every call is refused.
/// </para>
/// </summary>
public class ReadOnlyRemoteKeyValueService : KeyValueStore.KeyValueStoreBase
{
    public const string RequiredScope = "remote-kv:read";

    private readonly ILogger _logger;
    private readonly IKeyValueStore _baseStore;
    private readonly JwtSecurityTokenHandler _tokenHandler = new JwtSecurityTokenHandler { MapInboundClaims = false };
    private readonly TokenValidationParameters? _validationParams;

    public ReadOnlyRemoteKeyValueService(IKeyValueStore baseStore, IConfiguration configuration)
    {
        _logger = Log.Logger.ForContext<ReadOnlyRemoteKeyValueService>();
        _baseStore = baseStore;

        var jwtConfig = configuration.GetSection("Jwt");
        var key = jwtConfig["Key"];
        if (string.IsNullOrEmpty(key))
        {
            _logger.Warning("Jwt:Key is not configured; every read-only remote key-value request will be refused.");
            return;
        }

        // Same key derivation as JwtAuthenticationMiddleware, so one issuer serves both.
        _validationParams = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = false,
            ValidateLifetime = true,
            RequireExpirationTime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = jwtConfig["Issuer"] ?? "",
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.ASCII.GetBytes(key.PadRight(512 / 8, '\0'))),
        };
    }

    public override Task<KeyValueStoreValue> GetValue(GetValueRequest request, ServerCallContext context)
    {
        Authorize(context);
        var key = new KeyBytes(request.Key.Data.ToByteArray());
        byte[] value;
        try
        {
            value = _baseStore.Get(key);
        }
        catch (KeyNotFoundException e)
        {
            throw new RpcException(new Status(StatusCode.NotFound, e.Message));
        }

        return Task.FromResult(new KeyValueStoreValue { Data = ByteString.CopyFrom(value) });
    }

    public override Task<ExistsKeyResponse> ExistsKey(ExistsKeyRequest request, ServerCallContext context)
    {
        Authorize(context);
        var key = new KeyBytes(request.Key.Data.ToByteArray());
        return Task.FromResult(new ExistsKeyResponse { Exists = _baseStore.Exists(key) });
    }

    private void Authorize(ServerCallContext context)
    {
        if (_validationParams is null)
        {
            throw new RpcException(new Status(StatusCode.Unauthenticated, "Authentication is not configured."));
        }

        var authorization = context.RequestHeaders.GetValue("authorization");
        const string bearer = "Bearer ";
        if (authorization is null || !authorization.StartsWith(bearer, StringComparison.OrdinalIgnoreCase))
        {
            throw new RpcException(new Status(StatusCode.Unauthenticated, "Bearer token is required."));
        }

        ClaimsPrincipal principal;
        try
        {
            principal = _tokenHandler.ValidateToken(authorization.Substring(bearer.Length).Trim(), _validationParams, out _);
        }
        catch (Exception e) when (e is SecurityTokenException or ArgumentException)
        {
            _logger.Information("Rejected read-only remote key-value request from {Peer}: {Message}", context.Peer, e.Message);
            throw new RpcException(new Status(StatusCode.Unauthenticated, "Invalid token."));
        }

        var scopes = principal.FindAll("scope").SelectMany(c => c.Value.Split(' ', StringSplitOptions.RemoveEmptyEntries));
        if (!scopes.Contains(RequiredScope))
        {
            throw new RpcException(new Status(StatusCode.PermissionDenied, $"Token lacks the {RequiredScope} scope."));
        }
    }
}
