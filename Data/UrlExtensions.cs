using System;
using System.Threading.Tasks;
using System.Net.Http;

namespace FuGetGallery
{
    static class UrlExtensions
    {
        public static Task<string> GetTextFileAsync (string url)
        {
            var client = new HttpClient ();
            return client.GetStringAsync (url);
        }
    }
}
