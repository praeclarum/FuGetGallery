using System;
using System.Net;
using System.Net.Http;
using FuGetGallery;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.ResponseCompression;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

var builder = WebApplication.CreateBuilder (args);

var services = builder.Services;

try {
    new Database ().MigrateAsync ().Wait ();
    services.AddScoped<Database, Database> ();
}
catch (Exception dbex) {
    Console.WriteLine ($"Failed to open the database: {dbex}");
}
services.AddControllers ();
services.AddRazorPages (options =>
{
    options.Conventions.AddPageRoute ("/packages/details", "packages/{id}");
    options.Conventions.AddPageRoute ("/packages/badges", "packages/{id}/badges");
    options.Conventions.AddPageRoute ("/packages/dependents", "packages/{id}/dependents");
    options.Conventions.AddPageRoute ("/packages/details", "packages/{id}/{version}");
    options.Conventions.AddPageRoute ("/packages/details", "packages/{id}/{version}/{dir}");
    options.Conventions.AddPageRoute ("/packages/details", "packages/{id}/{version}/{dir}/{targetFramework}");
    options.Conventions.AddPageRoute ("/packages/details", "packages/{id}/{version}/{dir}/{targetFramework}/{assemblyName}");
    options.Conventions.AddPageRoute ("/packages/details", "packages/{id}/{version}/{dir}/{targetFramework}/{assemblyName}/{namespace}");
    options.Conventions.AddPageRoute ("/packages/details", "packages/{id}/{version}/{dir}/{targetFramework}/{assemblyName}/{namespace}/{typeName}");
});
services.Configure<GzipCompressionProviderOptions> (options => options.Level = System.IO.Compression.CompressionLevel.Optimal);
services.AddResponseCompression ();
services.AddHttpClient ("")
    .ConfigurePrimaryHttpMessageHandler (() => new HttpClientHandler {
        AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate,
    });

var app = builder.Build ();

NugetPackageSources.Configure (app.Configuration);

app.UseResponseCompression ();

if (app.Environment.IsDevelopment ()) {
    app.UseDeveloperExceptionPage ();
}
else {
    app.UseExceptionHandler ("/error");
}

app.UseStaticFiles ();
app.UseRouting ();

app.MapControllers ();
app.MapRazorPages ();

app.Run ();

