using System;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.OpenApi.Models;
using NineChronicles.Headless.AccessControlCenter.AccessControlService;

namespace NineChronicles.Headless.AccessControlCenter
{
    public class AccService
    {
        public AccService(Configuration configuration)
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

                services.AddSwaggerGen(c =>
                {
                    c.SwaggerDoc(
                        "v1",
                        new OpenApiInfo { Title = "Access Control Center API", Version = "v1" }
                    );
                    c.DocInclusionPredicate(
                        (docName, apiDesc) =>
                        {
                            var controllerType =
                                apiDesc.ActionDescriptor
                                as Microsoft.AspNetCore.Mvc.Controllers.ControllerActionDescriptor;
                            if (controllerType != null)
                            {
                                var assemblyName = controllerType.ControllerTypeInfo.Assembly
                                    .GetName()
                                    .Name;
                                var namespaceName = controllerType.ControllerTypeInfo.Namespace;
                                return namespaceName?.StartsWith(
                                        "NineChronicles.Headless.AccessControlCenter"
                                    ) ?? false;
                            }
                            return false;
                        }
                    );
                });

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
                app.UseSwagger();
                app.UseSwaggerUI(c =>
                {
                    c.SwaggerEndpoint("/swagger/v1/swagger.json", "Access Control Center API V1");
                });

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
