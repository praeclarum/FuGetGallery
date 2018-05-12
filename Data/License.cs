using System;
using System.Collections.Generic;
using System.Linq;

namespace FuGetGallery.Data
{
    public class License
    {
        public string Name { get; set; } = "";
        public bool AllowsDecompilation { get; set; }
        public HashSet<string> KnownUrls { get; set; } = new HashSet<string> ();
        public string TemplateName { get; set; } = "";

        readonly static List<License> known = new List<License> ();

        static License ()
        {
            known.Add (new License {
                Name = "Apache License, version 2.0",
                AllowsDecompilation = true,
                KnownUrls = {
                    "http://www.apache.org/licenses/LICENSE-2.0",
                    "https://www.apache.org/licenses/LICENSE-2.0",
                    "http://opensource.org/licenses/Apache-2.0",
                    "https://opensource.org/licenses/Apache-2.0",
                },
                TemplateName = "Apache2",
            });

            known.Add (new License {
                Name = "3-Clause BSD License",
                AllowsDecompilation = true,
                KnownUrls = {
                    "http://opensource.org/licenses/BSD-3-Clause",
                    "https://opensource.org/licenses/BSD-3-Clause",
                },
                TemplateName = "BSD3Clause",
            });

            known.Add (new License {
                Name = "2-Clause BSD License",
                AllowsDecompilation = true,
                KnownUrls = {
                    "http://opensource.org/licenses/BSD-2-Clause",
                    "https://opensource.org/licenses/BSD-2-Clause",
                },
                TemplateName = "BSD2Clause",
            });

            known.Add (new License {
                Name = "GNU General Public License, version 2",
                AllowsDecompilation = true,
                KnownUrls = {
                    "http://www.gnu.org/licenses/gpl-2.0.html",
                    "https://www.gnu.org/licenses/gpl-2.0.html",
                    "http://opensource.org/licenses/GPL-2.0",
                    "https://opensource.org/licenses/GPL-2.0",
                },
                TemplateName = "GPL2",
            });

            known.Add (new License {
                Name = "GNU General Public License, version 3",
                AllowsDecompilation = true,
                KnownUrls = {
                    "http://www.gnu.org/licenses/gpl-3.0.html",
                    "https://www.gnu.org/licenses/gpl-3.0.html",
                    "http://opensource.org/licenses/GPL-3.0",
                    "https://opensource.org/licenses/GPL-3.0",
                },
                TemplateName = "GPL3",
            });

            known.Add (new License {
                Name = "GNU Lesser General Public License, version 2.1",
                AllowsDecompilation = true,
                KnownUrls = {
                    "http://www.gnu.org/licenses/lgpl-2.1.html",
                    "https://www.gnu.org/licenses/lgpl-2.1.html",
                    "https://opensource.org/licenses/LGPL-2.1",
                    "https://opensource.org/licenses/LGPL-2.1",
                },
                TemplateName = "LGPL21",
            });

            known.Add (new License {
                Name = "MIT",
                AllowsDecompilation = true,
                KnownUrls = {
                    "http://opensource.org/licenses/MIT",
                    "https://opensource.org/licenses/MIT",
                },
                TemplateName = "MIT",
            });

            known.Add (new License {
                Name = "MS-PL",
                AllowsDecompilation = true,
                KnownUrls = {
                    "http://opensource.org/licenses/MS-PL",
                    "https://opensource.org/licenses/MS-PL",
                },
                TemplateName = "MSPL",
            });
        }

        public static License FindLicenseWithUrl (string url)
        {
            return known.FirstOrDefault (x => x.KnownUrls.Contains (url));
        }
    }
}
