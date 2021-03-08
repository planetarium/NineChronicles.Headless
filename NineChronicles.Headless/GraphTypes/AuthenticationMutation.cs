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

namespace NineChronicles.Headless.GraphTypes
{
    public class AuthenticationMutation : ObjectGraphType
    {
        private readonly IKeyStore _keyStore;
        private readonly IHttpContextAccessor _httpContextAccessor;

        public AuthenticationMutation(IKeyStore keyStore, IHttpContextAccessor httpContextAccessor)
        {
            _keyStore = keyStore;
            _httpContextAccessor = httpContextAccessor;

            FieldAsync<NonNullGraphType<BooleanGraphType>>(
                "login",
                "Log in with the the given address and the given passphrase. " +
                    "If the address doesn't exist in the key store, it will fail.",
                new QueryArguments(
                    new QueryArgument<NonNullGraphType<AddressType>>
                    {
                        Name = "address",
                        Description = "The address of the private key to use in this session.",
                    },
                    new QueryArgument<NonNullGraphType<StringGraphType>>
                    {
                        Name = "passphrase",
                        Description = "The passphrase to unlock the protected private key.",
                    }
                ), resolve: async context =>
                {
                    Address address = context.GetArgument<Address>("address");
                    string passphrase = context.GetArgument<string>("passphrase");
                    if (!(_keyStore.List().First(t => t.Item2.Address == address)?.Item2 is { } protectedPrivateKey))
                    {
                        context.Errors.Add(
                            new ExecutionError(
                                $"The given address '{address}' didn't exist in protected private keys."));
                        return false;
                    }

                    PrivateKey privateKey = protectedPrivateKey.Unprotect(passphrase);
                    var claims = new List<Claim>
                    {
                        new Claim(ClaimTypes.Name, privateKey.ToAddress().ToHex()),
                        new Claim(ClaimTypes.Role, "User"),
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
