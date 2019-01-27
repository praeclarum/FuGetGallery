using System;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using NGraphics;

namespace FuGetGallery.Controllers
{
    public class BadgeController : Controller
    {
        private readonly IHttpClientFactory httpClientFactory;

        public BadgeController(IHttpClientFactory httpClientFactory)
        {
           this.httpClientFactory = httpClientFactory;
        }

        [Route("/packages/{id}/badge.svg")]
        public async Task<ActionResult> GetBadgeAsync (string id, string v)
        {
            var package = await PackageData.GetAsync (id, v, httpClientFactory.CreateClient());

            var content = DrawBadge (package);

            HttpContext.Response.Headers.Add ("Cache-Control", "max-age=3600");
            var r = Content(content);
            r.ContentType = "image/svg+xml";
            return r;
        }

        string DrawBadge (PackageData package)
        {
            var k = "fuget";
            var v = "v" + package.Version.VersionString;
            
            var font = new Font ("DejaVu Sans,Verdana,Geneva,sans-serif", 11);
            var kw = (int)Math.Round(NullPlatform.GlobalMeasureText(k, font).Width * 1.15);
            var vw = (int)Math.Round(NullPlatform.GlobalMeasureText(v, font).Width * 1.15);

            var hpad = 8;
            var w = kw + vw + 4*hpad;
            var h = 20;
            var c = new GraphicCanvas (new Size (w, h));

            c.FillRectangle (new Rect(0, 0, w, h), new Size(3, 3), "#555");
            c.FillRectangle (new Rect(kw + 2*hpad, 0, w - 2*hpad - kw, h), new Size(3,3), "#5cb85c");
            c.FillRectangle (new Rect(kw + 2*hpad, 0, 6, h), "#5cb85c");

            var scolor = new Color(1.0 / 255.0, 0.3);
            // c.FillRectangle(new Rect(hpad, 5, kw, font.Size), "#F00");
            c.DrawText(k, new Point(hpad, 15), font, scolor);
            c.DrawText(k, new Point(hpad, 14), font, Colors.White);
            
            // c.FillRectangle(new Rect(w - vw - hpad, 5, vw, font.Size), "#FF0");
            c.DrawText(v, new Point(w - vw - hpad, 15), font, scolor);
            c.DrawText(v, new Point(w - vw - hpad, 14), font, Colors.White);

            using (var tw = new System.IO.StringWriter ()) {
                c.Graphic.WriteSvg (tw);
                return tw.ToString ();
            }
        }
    }
}
