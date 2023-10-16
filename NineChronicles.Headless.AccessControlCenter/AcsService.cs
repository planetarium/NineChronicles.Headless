using System;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using NineChronicles.Headless.AccessControlCenter.AccessControlService;

namespace NineChronicles.Headless.AccessControlCenter
{
    public class AcsService
    {
        public AcsService(Configuration configuration)
        {
            Configuration = configuration;
        }

        public Configuration Configuration { get; }

        public IHostBuilder Configure(IHostBuilder hostBuilder, int port)
        {
            return hostBuilder.ConfigureWebHostDefaults(builder =>
            {
                builder.UseStartup(x => new RestApiStartup(Configuration));
                builder.ConfigureKestrel(options =>
                {
                    options.ListenAnyIP(
                        port,
                        listenOptions =>
                        {
                            listenOptions.Protocols = HttpProtocols.Http1AndHttp2;
                        }
                    );
                });
            });
        }

        internal class RestApiStartup
        {
            public RestApiStartup(Configuration configuration)
            {
                Configuration = configuration;
            }

            public Configuration Configuration { get; }

            public void ConfigureServices(IServiceCollection services)
            {
                services.AddControllers();

                var accessControlService = MutableAccessControlServiceFactory.Create(
                    Enum.Parse<MutableAccessControlServiceFactory.StorageType>(
                        Configuration.AccessControlServiceType,
                        true
                    ),
                    Configuration.AccessControlServiceConnectionString
                );

                services.AddSingleton(accessControlService);
            }

            public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
            {
                if (env.IsDevelopment())
                {
                    app.UseDeveloperExceptionPage();
                }

                app.UseRouting();
                app.UseAuthorization();

                app.UseEndpoints(endpoints =>
                {
                    endpoints.MapControllers();
                });
            }
        }
    }
}
