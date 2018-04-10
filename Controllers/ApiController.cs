using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Caching.Memory;
using System.Threading;
using Microsoft.AspNetCore.Mvc;

namespace FuGetGallery.Controllers
{
    [Route("api")]
    public class ApiController : Controller
    {
        [Route("searchpackage")]
        public async Task<ActionResult> SearchPackageAsync (string p, string v, string f, string q)
        {
            var package = await PackageData.GetAsync (p, v);
            var results = await Task.Run (() => package.Search (f, q));
            return Json(results);
        }
    }
}
