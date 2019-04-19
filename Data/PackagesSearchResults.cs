using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Mono.Cecil;

namespace FuGetGallery
{
    public class PackagesSearchResults
    {
        public string Query { get; set; } = "";
        public Exception Error { get; set; }
        public List<PackagesSearchResult> Results { get; set; } = new List<PackagesSearchResult> ();

        public PackagesSearchResults ()
        {
        }

        static readonly ResultsCache cache = new ResultsCache ();

        public static Task<PackagesSearchResults> GetAsync (string inputId, HttpClient httpClient)
        {
            var cleanId = (inputId ?? "").ToString().Trim().ToLowerInvariant();

            return cache.GetAsync (cleanId, httpClient);
            
            // "https://api-v2v3search-0.nuget.org/query?q=%s" (Uri.EscapeDataString (searchTerm))
            // return Task.FromResult<PackageSearchResults> (null);

        }

        void Read (string json)
        {
            var j = Newtonsoft.Json.Linq.JObject.Parse (json);
            var d = (Newtonsoft.Json.Linq.JArray)j["data"];
            foreach (var x in d) {
                var packageId = (string)x["id"];
                // This check is required to weed out duplicate packages that exist in multiple package sources.
                var existingResult = Results.FirstOrDefault (er => string.Equals (er.PackageId, packageId, StringComparison.OrdinalIgnoreCase));
                if (existingResult == null) {
                    Results.Add (new PackagesSearchResult {
                        PackageId = packageId,
                        Version = (string)x["version"],
                        Description = (string)x["description"],
                        IconUrl = (string)x["iconUrl"],
                        TotalDownloads = (int)x["totalDownloads"],
                        Authors = string.Join (", ", x["authors"]),
                    });
                }
                else {
                    var newVersion = (string)x["version"];
                    if (string.Compare(newVersion, existingResult.Version, true) > 1) {
                        existingResult.Version = newVersion;
                        existingResult.Description = (string)x["description"];
                        existingResult.IconUrl = (string)x["iconUrl"];
                        existingResult.TotalDownloads = (int)x["totalDownloads"];
                        existingResult.Authors = string.Join (", ", x["authors"]);
                    }
                }
                // System.Console.WriteLine(r);
            }
            // Results.Sort ((a, b) => -a.TotalDownloads.CompareTo (b.TotalDownloads));
        }

        class ResultsCache : DataCache<string, PackagesSearchResults>
        {
            public ResultsCache () : base (TimeSpan.FromMinutes (15)) { }

            protected override async Task<PackagesSearchResults> GetValueAsync (string q, HttpClient httpClient, CancellationToken token)
            {
                var results = new PackagesSearchResults {
                    Query = q,
                };

                var succeededOnce = false;
                var exceptions = new List<Exception> ();
                // System.Console.WriteLine($"DOWNLOADING {package.DownloadUrl}");
                foreach (var source in NugetPackageSources.PackageSources) {
                    try {
                        var data = await httpClient.GetStringAsync (string.Format (source.QueryUrlFormat, Uri.EscapeDataString (q))).ConfigureAwait (false);
                        results.Read (data);
                        if (results.Results.Count != 0) {
                            succeededOnce = true;
                        }
                    }
                    catch (Exception ex) {
                        exceptions.Add(ex);
                    }
                }

                if (!succeededOnce) {
                    results.Error = exceptions.Count == 1 
                        ? exceptions[0] 
                        : new AggregateException(exceptions);
                }

                return results;
            }
        }
    }

    public class PackagesSearchResult
    {
        public string PackageId { get; set; } = "";
        public string Version { get; set; } = "";
        public string Description { get; set; } = "";
        public string IconUrl { get; set; } = "";
        public int TotalDownloads { get; set; } = 0;
        public string Authors { get; set; } = "";

        public override string ToString() => PackageId;

        public string SafeIconUrl => string.IsNullOrEmpty (IconUrl) ? "/images/no-icon.png" : IconUrl;
    }
}
