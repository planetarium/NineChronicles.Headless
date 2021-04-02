using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using GraphQL;
using GraphQL.Types;
using Libplanet;
using Libplanet.Crypto;
using Libplanet.Explorer.GraphTypes;
using Libplanet.KeyStore;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using NineChronicles.Headless.Options;

namespace NineChronicles.Headless.GraphTypes
{
    public class AuthenticationMutation : ObjectGraphType
    {
        private readonly IHttpContextAccessor _httpContextAccessor;

        public AuthenticationMutation(IHttpContextAccessor httpContextAccessor, IConfiguration configuration)
        {
            _httpContextAccessor = httpContextAccessor;

            FieldAsync<NonNullGraphType<BooleanGraphType>>(
                "login",
                "Log in with the given private key.",
                new QueryArguments(
                    new QueryArgument<NonNullGraphType<ByteStringType>>
                    {
                        Name = "privateKey",
                        Description = "The private key to use in this session.",
                    },
                    new QueryArgument<StringGraphType>
                    {
                        Name = "adminPassphrase",
                        Description = "The passphrase given through command line when execution, to prove permission for admin role.",
                    }
                ), resolve: async context =>
                {
                    byte[] privateKeyBytes = context.GetArgument<byte[]>("privateKey");
                    string? adminPassphrase = context.GetArgument<string?>("adminPassphrase");

                    bool isAdmin = false;
                    AuthenticationMutationOptions options = configuration.Get<AuthenticationMutationOptions>();
                    if (!(options.AdminPassphrase is null) && !(adminPassphrase is null))
                    {
                        if (adminPassphrase != options.AdminPassphrase)
                        {
                            throw new ExecutionError($"The given {nameof(adminPassphrase)} is not mismatched.");
                        }

                        isAdmin = true;
                    }

                    PrivateKey privateKey = new PrivateKey(privateKeyBytes);
                    var claims = new List<Claim>
                    {
                        new Claim(ClaimTypes.Name, privateKey.ToAddress().ToHex()),
                        new Claim(ClaimTypes.Role, isAdmin ? "Admin" : "User"),
                    };
                    var claimsIdentity = new ClaimsIdentity(claims, "Login");
                    await _httpContextAccessor.HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, new ClaimsPrincipal(claimsIdentity));
                    _httpContextAccessor.HttpContext.Session.SetPrivateKey(privateKey);
                    return true;
                });

            FieldAsync<NonNullGraphType<BooleanGraphType>>("logout", "Logout the session.", resolve: async context =>
            {
                if (!_httpContextAccessor.HttpContext.User.HasClaim(ClaimTypes.Role, "User"))
                {
                    context.Errors.Add(new ExecutionError("You seem not logged in."));
                    return false;
                }

                await _httpContextAccessor.HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
                return true;
            });
        }
    }
}
