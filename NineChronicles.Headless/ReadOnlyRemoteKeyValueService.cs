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
/// Every call requires an HS256 JWT signed with <c>RemoteKeyValueService:JwtKey</c> — a key of its own,
/// so these tokens grant nothing elsewhere (the GraphQL <c>Jwt:Key</c> is not accepted) — whose
/// <c>scope</c> claim contains <see cref="RequiredScope"/> and which expires within
/// <see cref="MaxTokenLifetime"/>. Without a key of at least <see cref="MinKeyLength"/> bytes every call
/// is refused.
/// </para>
/// </summary>
public class ReadOnlyRemoteKeyValueService : KeyValueStore.KeyValueStoreBase
{
    public const string JwtKeyConfigurationKey = "RemoteKeyValueService:JwtKey";

    public const string RequiredScope = "remote-kv:read";

    public const int MinKeyLength = 32;

    public static readonly TimeSpan MaxTokenLifetime = TimeSpan.FromHours(1);

    // Tolerated clock difference between the token issuer and this node.
    private static readonly TimeSpan ClockSkew = TimeSpan.FromMinutes(2);

    private readonly ILogger _logger;
    private readonly IKeyValueStore _baseStore;
    private readonly JwtSecurityTokenHandler _tokenHandler = new JwtSecurityTokenHandler { MapInboundClaims = false };
    private readonly TokenValidationParameters? _validationParams;

    public ReadOnlyRemoteKeyValueService(IKeyValueStore baseStore, IConfiguration configuration)
    {
        _logger = Log.Logger.ForContext<ReadOnlyRemoteKeyValueService>();
        _baseStore = baseStore;

        var key = configuration[JwtKeyConfigurationKey];
        if (key is null || Encoding.UTF8.GetByteCount(key) < MinKeyLength)
        {
            _logger.Warning(
                "{ConfigKey} is missing or shorter than {MinKeyLength} bytes; " +
                "every read-only remote key-value request will be refused.",
                JwtKeyConfigurationKey,
                MinKeyLength);
            return;
        }

        _validationParams = new TokenValidationParameters
        {
            ValidateIssuer = false,
            ValidateAudience = false,
            ValidateLifetime = true,
            RequireExpirationTime = true,
            ValidateIssuerSigningKey = true,
            ValidAlgorithms = new[] { SecurityAlgorithms.HmacSha256 },
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key)),
            // Replaces the default lifetime check, so exp presence, expiry and nbf are all handled here.
            LifetimeValidator = (notBefore, expires, _, _) =>
            {
                var now = DateTime.UtcNow;
                return expires is { } exp
                       && exp > now - ClockSkew
                       && exp - now <= MaxTokenLifetime + ClockSkew
                       && (notBefore is not { } nbf || nbf <= now + ClockSkew);
            },
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
            // Debug, not Information: anyone can send these, and they must not be able to flood the logs.
            _logger.Debug("Rejected read-only remote key-value request from {Peer}: {Message}", context.Peer, e.Message);
            throw new RpcException(new Status(StatusCode.Unauthenticated, "Invalid token."));
        }

        var scopes = principal.FindAll("scope").SelectMany(c => c.Value.Split(' ', StringSplitOptions.RemoveEmptyEntries));
        if (!scopes.Contains(RequiredScope))
        {
            throw new RpcException(new Status(StatusCode.PermissionDenied, $"Token lacks the {RequiredScope} scope."));
        }
    }
}
