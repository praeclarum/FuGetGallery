using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Hosting;

Host.CreateDefaultBuilder (args)
    .ConfigureWebHostDefaults (webBuilder => {
        webBuilder.UseStartup<FuGetGallery.Startup> ();
    })
    .Build ()
    .Run ();
