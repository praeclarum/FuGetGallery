using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;

namespace FuGetGallery.Controllers
{
    [Route("api")]
    public class ApiController : Controller
    {
        private readonly IHttpClientFactory httpClientFactory;

        public ApiController(IHttpClientFactory httpClientFactory)
        {
           this.httpClientFactory = httpClientFactory;
        }

        [Route("searchpackage")]
        public async Task<ActionResult> SearchPackageAsync (string p, string v, string f, string q)
        {
            var package = await PackageData.GetAsync (p, v, httpClientFactory.CreateClient());
            var results = await Task.Run (() => package.Search (f, q));
            return Json(results);
        }
    }
}
