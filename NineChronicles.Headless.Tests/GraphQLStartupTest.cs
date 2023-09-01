using Microsoft.AspNetCore.Cors.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Xunit;

namespace NineChronicles.Headless.Tests
{
    public class GraphQLStartupTest
    {
        private readonly GraphQLService.GraphQLStartup _startup;
        private readonly IConfiguration _configuration;

        public GraphQLStartupTest()
        {
            _configuration = new ConfigurationBuilder().AddInMemoryCollection().Build();
            _startup = new GraphQLService.GraphQLStartup(_configuration);
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void Configure(bool noCors)
        {
            if (noCors)
            {
                _configuration[GraphQLService.NoCorsKey] = string.Empty;
            }

            var services = new ServiceCollection();
            services.AddLogging();
            _startup.ConfigureServices(services);
            ServiceProvider serviceProvider = services.BuildServiceProvider();

            var options = serviceProvider.GetService<IOptions<CorsOptions>>();
            Assert.Equal(noCors, !(options!.Value.GetPolicy(GraphQLService.NoCorsPolicyName) is null));
        }
    }
}
