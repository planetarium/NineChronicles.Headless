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
        private readonly string _acsType;
        private readonly string _acsConnectionString;

        public AcsService(string acsType, string acsConnectionString)
        {
            _acsType = acsType;
            _acsConnectionString = acsConnectionString;
        }

        public IHostBuilder Configure(IHostBuilder hostBuilder, int port)
        {
            return hostBuilder.ConfigureWebHostDefaults(builder =>
            {
                builder.UseStartup(
                    x =>
                        new RestApiStartup(
                            x.Configuration,
                            _acsType,
                            _acsConnectionString
                        )
                );
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
            private readonly string _acsType;
            private readonly string _acsConnectionString;

            public RestApiStartup(
                IConfiguration configuration,
                string acsType,
                string acsConnectionString
            )
            {
                Configuration = configuration;
                _acsType = acsType;
                _acsConnectionString = acsConnectionString;
            }

            public IConfiguration Configuration { get; }

            public void ConfigureServices(IServiceCollection services)
            {
                services.AddControllers();

                var accessControlService = MutableAccessControlServiceFactory.Create(
                    Enum.Parse<MutableAccessControlServiceFactory.StorageType>(_acsType, true),
                    _acsConnectionString
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
