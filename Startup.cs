using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.ResponseCompression;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace FuGetGallery
{
    public class Startup
    {
        public Startup(IConfiguration configuration)
        {
            Configuration = configuration;
        }

        public IConfiguration Configuration { get; }

        // This method gets called by the runtime. Use this method to add services to the container.
        public void ConfigureServices(IServiceCollection services)
        {
            services.AddMvc().AddRazorPagesOptions(options =>
            {
                options.Conventions.AddPageRoute("/packages/details", "packages/{id}");
                options.Conventions.AddPageRoute("/packages/details", "packages/{id}/{version}");
                options.Conventions.AddPageRoute("/packages/details", "packages/{id}/{version}/{dir}");
                options.Conventions.AddPageRoute("/packages/details", "packages/{id}/{version}/{dir}/{targetFramework}");
                options.Conventions.AddPageRoute("/packages/details", "packages/{id}/{version}/{dir}/{targetFramework}/{assemblyName}");
                options.Conventions.AddPageRoute("/packages/details", "packages/{id}/{version}/{dir}/{targetFramework}/{assemblyName}/{namespace}");
                options.Conventions.AddPageRoute("/packages/details", "packages/{id}/{version}/{dir}/{targetFramework}/{assemblyName}/{namespace}/{typeName}");
            });
            services.Configure<GzipCompressionProviderOptions>(options => options.Level = System.IO.Compression.CompressionLevel.Optimal);
            services.AddResponseCompression();
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IHostingEnvironment env)
        {
            app.UseResponseCompression();

            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }
            else
            {
                app.UseExceptionHandler("/error");
            }

            app.UseStaticFiles();

            app.UseMvc();
        }
    }
}
