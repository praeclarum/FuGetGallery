using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using System.Linq;
using Mono.Cecil;

namespace FuGetGallery
{
    public class PackageSearchResults
    {
        public string Query { get; set; } = "";
        public string Error { get; set; }
        readonly ConcurrentDictionary<string, PackageSearchResult> results = new ConcurrentDictionary<string, PackageSearchResult> ();

        readonly PackageData package;

        const int maxResults = 1000;

        public PackageSearchResults (PackageData package)
        {
            this.package = package;
        }

        public void Add (PackageTargetFramework framework, PackageAssembly a, TypeDefinition m, string name, string parent, string type, string id, bool isPublic, int s)
        {
            if (results.Count >= maxResults)
                return;
            var dir = a.IsBuildAssembly ? "build" : "lib";
            var code = isPublic ? "" : (framework.Package.AllowedToDecompile ? "?code=true" : "");
            var link = $"/packages/{Uri.EscapeDataString(package.Id)}/{Uri.EscapeDataString(package.Version.VersionString)}/{framework.Moniker}/{dir}/{Uri.EscapeDataString(a.FileName)}/{Uri.EscapeDataString(m.Namespace)}/{Uri.EscapeDataString(m.Name)}{code}#{Uri.EscapeDataString(id)}";
            results.TryAdd (link, new PackageSearchResult {
                Name = name,
                Parent = parent,
                Type = type,
                Link = link,
                IsPublic = isPublic,
                Score = s,
            });
        }

        public List<PackageSearchResult> Results =>
            results.Values.OrderByDescending (x => x.Score).Take (100).ToList ();
    }

    public class PackageSearchResult
    {
        public string Name { get; set; } = "";
        public int Score { get; set; }
        public string Parent { get; set; } = "";
        public string Type { get; set; } = "";
        public string Link { get; set; } = "";
        public bool IsPublic { get; set; }
        public override string ToString() => Name;
    }
}
