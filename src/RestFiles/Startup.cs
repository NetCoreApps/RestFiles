using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Funq;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using RestFiles.ServiceInterface;
using ServiceStack;
using ServiceStack.Configuration;

namespace RestFiles
{
    public class Startup
    {
        IConfiguration Configuration { get; set; }
        public Startup(IConfiguration configuration) => Configuration = configuration;

        // This method gets called by the runtime. Use this method to add services to the container.
        // For more information on how to configure your application, visit http://go.microsoft.com/fwlink/?LinkID=398940
        public void ConfigureServices(IServiceCollection services)
        {
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }

            app.UseServiceStack(new AppHost {
                AppSettings = new NetCoreAppSettings(Configuration)
            });
        }
    }

    public class AppHost : AppHostBase
    {
        /// <summary>
        /// Initializes a new instance of your ServiceStack application, with the specified name and assembly containing the services.
        /// </summary>
        public AppHost() : base("REST Files", typeof(FilesService).Assembly) { }

        /// <summary>
        /// Configure the container with the necessary routes for your ServiceStack application.
        /// </summary>
        /// <param name="container">The built-in IoC used with ServiceStack.</param>
        public override void Configure(Container container)
        {
            //Permit modern browsers (e.g. Firefox) to allow sending of any REST HTTP Method
            Plugins.Add(new CorsFeature());

            SetConfig(new HostConfig
            {
                UseCamelCase = false,
            });

            var config = new AppConfig
            {
                RootDirectory = AppSettings.GetString("RootDirectory"),
                TextFileExtensions = AppSettings.GetList("TextFileExtensions").ToList(),
                ExcludeDirectories = AppSettings.GetList("ExcludeDirectories").ToList(),
            };
            container.Register(config);

            if (!Directory.Exists(config.RootDirectory))
                Directory.CreateDirectory(config.RootDirectory);

            Plugins.Add(new SharpPagesFeature { 
                MetadataDebugAdminRole = RoleNames.AllowAnon,
                Args = { ["config"] = config }       
            });
        }
    }
}
