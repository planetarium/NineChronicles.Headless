using System.Text;
using System.Threading.Tasks;
using GraphQL.Types;
using GraphQL.Utilities;
using Microsoft.AspNetCore.Http;

namespace NineChronicles.Headless.Middleware
{
    public class GraphQLSchemaMiddleware<TSchema>
        where TSchema : ISchema
    {
        private readonly RequestDelegate _next;
        private readonly string _path;

        public GraphQLSchemaMiddleware(RequestDelegate next, string path)
        {
            _next = next;
            _path = path;
        }

        public async Task InvokeAsync(HttpContext context, TSchema schema)
        {
            if (context.Request.Path != _path)
            {
                await _next(context);
                return;
            }

            await context.Response.Body.WriteAsync(Encoding.UTF8.GetBytes(new SchemaPrinter(schema).Print()));
        }
    }
}
