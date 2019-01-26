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
        public async Task<ActionResult> GetBadgeAsync (string id)
        {
            var package = await PackageData.GetAsync (id, "", httpClientFactory.CreateClient());

            var content = DrawBadge (package);

            HttpContext.Response.Headers.Add ("Cache-Control", "max-age=3600");
            var r = Content(content);
            r.ContentType = "image/svg+xml";
            return r;
        }

        string DrawBadge (PackageData package)
        {
            var k = "fuget package";
            var v = package.Version.VersionString;
            
            var font = new Font ("DejaVu Sans,Verdana,Geneva,sans-serif", 11);
            var mc = new GraphicCanvas(new Size(0,0));
            var kw = (int)Math.Round(mc.MeasureText(k, font).Width) + 100;
            var vw = (int)Math.Round(mc.MeasureText(v, font).Width) + 50;
            Console.WriteLine((kw, vw));

            var hpad = 8;
            var w = kw+vw+4*hpad;
            var h = 20;
            var c = new GraphicCanvas (new Size (kw+vw+4*hpad, 20));

            c.DrawRectangle (new Rect(0, 0, w, h), new Size(3, 3), null, new SolidBrush (new Color("#555555")));
            
            c.DrawRectangle (new Rect(kw + 2*hpad, 0, w - 2*hpad - vw, 20), new Size(3,3), null, new SolidBrush (new Color("#44cc11")));

            var scolor = new Color(1.0 / 255.0, 0.3);
            c.DrawText(k, new Point(6, 15), font, scolor);
            c.DrawText(k, new Point(6, 14), font, Colors.White);
            
            c.DrawText(v, new Point(100, 15), font, scolor);
            c.DrawText(v, new Point(100, 14), font, Colors.White);

            using (var tw = new System.IO.StringWriter ()) {
                c.Graphic.WriteSvg (tw);
                return tw.ToString ();
            }
        }
    }
}
