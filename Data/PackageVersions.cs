using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Net.Http;
using Newtonsoft.Json.Linq;

namespace FuGetGallery
{
    public class PackageVersions
    {
        public string LowerId { get; set; } = "";
        public List<PackageVersion> Versions { get; set; } = new List<PackageVersion> ();
        public Exception Error { get; set; }

        static readonly PackageVersionsCache cache = new PackageVersionsCache ();

        public static Task<PackageVersions> GetAsync (object inputId)
        {
            var cleanId = (inputId ?? "").ToString().Trim().ToLowerInvariant();
            return cache.GetAsync (cleanId);
        }

        public PackageVersion GetVersion (object inputVersion)
        {
            var version = (inputVersion ?? "").ToString().Trim().ToLowerInvariant();
            var v = Versions.FirstOrDefault (x => x.Version == version);
            if (v == null) {
                v = Versions.LastOrDefault ();
            }
            if (v == null) {
                v = new PackageVersion {
                    Version = version.Length > 0 ? version : "0",
                };
            }
            return v;
        }

        void Read (JArray items)
        {
            foreach (var v in items) {
                var version = v["version"]?.ToString();
                if (version == null) {
                    version = v["catalogEntry"]?["version"]?.ToString();
                }
                if (version != null) {
                    Versions.Add (new PackageVersion { Version = version });
                }
            }
        }

        class PackageVersionsCache : DataCache<string, PackageVersions>
        {
            readonly HttpClient httpClient = new HttpClient ();
            protected override async Task<PackageVersions> GetValueAsync(string lowerId)
            {
                var package = new PackageVersions {
                    LowerId = lowerId,
                };
                try {
                    var url = "https://api.nuget.org/v3/registration3/" + Uri.EscapeDataString(lowerId) + "/index.json";
                    var rootJson = await httpClient.GetStringAsync (url).ConfigureAwait (false);
                    //System.Console.WriteLine(rootJson + "\n\n\n\n");
                    var root = JObject.Parse(rootJson);
                    var pages = (JArray)root["items"];
                    if (pages.Count > 0) {
                        var lastPage = pages.Last();
                        var lastPageItems = lastPage["items"] as JArray;
                        if (lastPageItems != null) {
                            package.Read (lastPageItems);
                        }
                        else {
                            var pageUrl = lastPage["@id"].ToString ();
                            var pageRootJson = await httpClient.GetStringAsync (pageUrl).ConfigureAwait (false);
                            var pageRoot = JObject.Parse (pageRootJson);
                            package.Read ((JArray)pageRoot["items"]);
                        }
                    }
                }
                catch (Exception ex) {
                    package.Error = ex;
                }

                return package;
            }
        }
    }

    public class PackageVersion
    {
        public string Version { get; set; } = "";

        public override string ToString() => Version;
    }
}
