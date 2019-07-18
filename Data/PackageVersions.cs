using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Net.Http;
using Newtonsoft.Json.Linq;
using System.Threading;

namespace FuGetGallery
{
    public class PackageVersions
    {
        public string LowerId { get; set; } = "";
        public List<PackageVersion> Versions { get; set; } = new List<PackageVersion> ();
        public Exception Error { get; set; }

        static readonly PackageVersionsCache cache = new PackageVersionsCache ();

        public static Task<PackageVersions> GetAsync (object inputId, HttpClient client, CancellationToken token)
        {
            var cleanId = (inputId ?? "").ToString().Trim().ToLowerInvariant();
            return cache.GetAsync (cleanId,client, token);
        }

        public PackageVersion GetVersion (object inputVersion)
        {
            var version = (inputVersion ?? "").ToString().Trim().ToLowerInvariant();
            var v = Versions.FirstOrDefault (x => x.VersionString == version);
            if (v == null) {
                v = Versions.LastOrDefault ();
            }
            if (v == null) {
                v = new PackageVersion {
                    VersionString = version.Length > 0 ? version : "0",
                };
            }
            return v;
        }

        void Read (JArray items)
        {
            foreach (var v in items) {
                DateTime? time = null;
                var times = v["catalogEntry"]?["published"];
                if (times != null) {
                    time = (DateTime)times;
                }
                var version = v["version"]?.ToString();
                if (version == null) {
                    version = v["catalogEntry"]?["version"]?.ToString();
                }
                if (version != null && !Versions.Any(x => string.Equals(x.VersionString, version, StringComparison.OrdinalIgnoreCase))) {
                    Versions.Add (new PackageVersion { VersionString = version, PublishTime = time });
                }
            }
        }

        class PackageVersionsCache : DataCache<string, PackageVersions>
        {
            public PackageVersionsCache () : base (TimeSpan.FromMinutes (20)) { }

            protected override async Task<PackageVersions> GetValueAsync (string lowerId, HttpClient httpClient, CancellationToken token)
            {
                var package = new PackageVersions {
                    LowerId = lowerId,
                };

                var succeededOnce = false;
                var exceptions = new List<Exception> ();
                foreach (var source in NugetPackageSources.PackageSources) {
                    try {
                        await ReadVersionsFromUrl (httpClient, package, string.Format (source.VersionUrlFormat, Uri.EscapeDataString (lowerId)));
                        if (package.Versions.Count > 0) {
                            package.Versions.Sort ((x, y) => x.CompareTo (y));
                            succeededOnce = true;
                        }
                    }
                    catch (Exception ex) {
                        exceptions.Add(ex);
                    }
                }

                if (!succeededOnce) {
                    package.Error = exceptions.Count == 1 
                        ? exceptions[0] 
                        : new AggregateException(exceptions);
                }

                return package;
            }

            private static async Task ReadVersionsFromUrl (HttpClient httpClient, PackageVersions package, string url)
            {
                // Console.WriteLine("READ VERSIONS FROM: " + url);
                var rootJson = await httpClient.GetStringAsync (url).ConfigureAwait (false);
                // Console.WriteLine(rootJson + "\n\n\n\n");
                var root = JObject.Parse (rootJson);
                var pages = (JArray)root["items"];
                if (pages.Count > 0) {
                    var lastPage = pages.Last ();
                    var lastPageItems = lastPage["items"] as JArray;
                    if (lastPageItems != null) {
                        package.Read (lastPageItems);
                    }
                    else {
                        foreach (var p in pages.Reverse ()) {
                            var pageUrl = p["@id"].ToString ();
                            var pageRootJson = await httpClient.GetStringAsync (pageUrl).ConfigureAwait (false);
                            var pageRoot = JObject.Parse (pageRootJson);
                            package.Read ((JArray)pageRoot["items"]);
                        }
                    }
                }
            }
        }
    }

    public class PackageVersion : IComparable<PackageVersion>
    {
        string versionString = "";

        public int Major { get; private set; }
        public int Minor { get; private set; }
        public int Patch { get; private set; }
        public int Build { get; private set; }
        public string Rest { get; private set; } = "";
        public DateTime? PublishTime { get; set; }

        public bool IsPublished => PublishTime.HasValue && PublishTime.Value.Year > 1970;

        public string VersionString { 
            get => versionString; 
            set {
                if (versionString == value || string.IsNullOrEmpty (value))
                    return;
                versionString = value;
                var di = value.IndexOf ('-');
                var vpart = di > 0 ? value.Substring (0, di) : value;
                var rest = di > 0 ? value.Substring (di) : "";
                var parts = vpart.Split ('.');
                var maj = 0;
                var min = 0;
                var patch = 0;
                var build = 0;
                if (parts.Length > 0) int.TryParse(parts[0], out maj);
                if (parts.Length > 1) int.TryParse(parts[1], out min);
                if (parts.Length > 2) int.TryParse(parts[2], out patch);
                if (parts.Length > 3) int.TryParse (parts[3], out build);
                Major = maj;
                Minor = min;
                Patch = patch;
                Build = build;
                Rest = rest;
            }
        }

        public int CompareTo(PackageVersion other)
        {
            var c = Major.CompareTo(other.Major);
            if (c != 0) return c;
            c = Minor.CompareTo(other.Minor);
            if (c != 0) return c;
            c = Patch.CompareTo(other.Patch);
            if (c != 0) return c;
            c = Build.CompareTo (other.Build);
            if (c != 0) return c;
            return string.Compare(Rest, other.Rest, StringComparison.Ordinal);
        }

        public override bool Equals(object obj)
        {
            return obj is PackageVersion v && VersionString == v.VersionString;
        }

        public override int GetHashCode()
        {
            return VersionString.GetHashCode ();
        }

        public override string ToString() => VersionString;
    }
}
