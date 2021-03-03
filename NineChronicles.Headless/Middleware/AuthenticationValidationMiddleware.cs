using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;

namespace NineChronicles.Headless.Middleware
{
    public class AuthenticationValidationMiddleware : IMiddleware
    {
        public async Task InvokeAsync(HttpContext context, RequestDelegate next)
        {
            if (context.Session.GetPrivateKey() is null)
            {
                await context.SignOutAsync();
            }

            await next(context);
        }
    }
}
