using System;
using System.Net;
using System.Net.Http;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.ResponseCompression;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace FuGetGallery
{
    public class Startup
    {
        public IConfiguration Configuration { get; }

        public Startup(IConfiguration configuration)
        {
            Configuration = configuration;
            NugetPackageSources.Configure (Configuration);
        }

        // This method gets called by the runtime. Use this method to add services to the container.
        public void ConfigureServices(IServiceCollection services)
        {
            try {
                new Database ().MigrateAsync ().Wait ();
                services.AddScoped<Database, Database> ();
            }
            catch (Exception dbex) {
                Console.WriteLine ($"Failed to open the database: {dbex}");
            }
            services.AddControllers();
            services.AddRazorPages(options =>
            {
                options.Conventions.AddPageRoute("/packages/details", "packages/{id}");
                options.Conventions.AddPageRoute("/packages/badges", "packages/{id}/badges");
                options.Conventions.AddPageRoute("/packages/dependents", "packages/{id}/dependents");
                options.Conventions.AddPageRoute("/packages/details", "packages/{id}/{version}");
                options.Conventions.AddPageRoute("/packages/details", "packages/{id}/{version}/{dir}");
                options.Conventions.AddPageRoute("/packages/details", "packages/{id}/{version}/{dir}/{targetFramework}");
                options.Conventions.AddPageRoute("/packages/details", "packages/{id}/{version}/{dir}/{targetFramework}/{assemblyName}");
                options.Conventions.AddPageRoute("/packages/details", "packages/{id}/{version}/{dir}/{targetFramework}/{assemblyName}/{namespace}");
                options.Conventions.AddPageRoute("/packages/details", "packages/{id}/{version}/{dir}/{targetFramework}/{assemblyName}/{namespace}/{typeName}");
            });
            services.Configure<GzipCompressionProviderOptions>(options => options.Level = System.IO.Compression.CompressionLevel.Optimal);
            services.AddResponseCompression();
            services.AddHttpClient ("")
                .ConfigurePrimaryHttpMessageHandler (() => new HttpClientHandler {
                    AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate,
                });
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            app.UseResponseCompression();

            if (env.IsDevelopment()) {
                app.UseDeveloperExceptionPage();
            }
            else {
                app.UseExceptionHandler("/error");
            }

            app.UseStaticFiles();
            app.UseRouting();
            app.UseEndpoints (configure => {
                configure.MapControllers ();
                configure.MapRazorPages ();
            });
        }
    }
}
