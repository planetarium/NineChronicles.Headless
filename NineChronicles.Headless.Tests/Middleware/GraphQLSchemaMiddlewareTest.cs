using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using GraphQL.Types;
using Microsoft.AspNetCore.Http;
using NineChronicles.Headless.GraphTypes;
using NineChronicles.Headless.Middleware;
using Xunit;

namespace NineChronicles.Headless.Tests.Middleware
{
    public class GraphQLSchemaMiddlewareTest
    {
        private const string GraphQlSchemaMiddlewareEndpoint = "/schema.graphql";

        private readonly GraphQLSchemaMiddleware<Schema> _middleware;

        public GraphQLSchemaMiddlewareTest()
        {
            _middleware = new GraphQLSchemaMiddleware<Schema>(context => Task.CompletedTask, GraphQlSchemaMiddlewareEndpoint);
        }

        public class Fruit : ObjectGraphType
        {
            public Fruit()
            {
                Field<NonNullGraphType<StringGraphType>>(
                    name: "fruit",
                    resolve: _ => "fruit"
                );
            }
        }

        [Fact]
        public async Task InvokeAsync()
        {
            var httpContext = new DefaultHttpContext
            {
                Request =
                {
                    Path = GraphQlSchemaMiddlewareEndpoint,
                },
                Response =
                {
                    Body = new MemoryStream(),
                }
            };
            await _middleware.InvokeAsync(httpContext, new Schema
            {
                Query = new Fruit()
                {
                    Name = "Fruit"
                }
            });

            httpContext.Response.Body.Seek(0, SeekOrigin.Begin);
            using var reader = new StreamReader(httpContext.Response.Body);
            Assert.Equal(
                string.Format("schema {{{0}  query: Fruit{0}}}{0}{0}type Fruit {{{0}  fruit: String!{0}}}{0}", Environment.NewLine),
                await reader.ReadToEndAsync());
        }
    }
}
