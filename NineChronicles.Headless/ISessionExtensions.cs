#nullable enable

using Libplanet.Crypto;
using Microsoft.AspNetCore.Http;
using ISession = Microsoft.AspNetCore.Http.ISession;

namespace NineChronicles.Headless
{
    internal static class ISessionExtensions
    {
        internal const string SessionPrivateKeyKey = "private-key";

        internal static PrivateKey? GetPrivateKey(this ISession session) =>
            session.Get(SessionPrivateKeyKey) is { } bytes
                ? new PrivateKey(bytes)
                : null;
        
        internal static void SetPrivateKey(this ISession session, PrivateKey privateKey) =>
            session.Set(SessionPrivateKeyKey, privateKey.ByteArray);
    }
}
