using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Net.Http;
using System.IO.Compression;
using System.Xml.Linq;
using Mono.Cecil;
using System.Threading;
using System.Diagnostics;
using System.Text.RegularExpressions;

namespace FuGetGallery
{
    public class PackageData
    {
        public string Id { get; set; } = "";
        public string IndexId { get; set; } = "";
        public PackageVersion Version { get; set; }

        public string NuspecXml { get; set; } = "";

        public string Authors { get; set; } = "";
        public string Owners { get; set; } = "";
        public string AuthorsOrOwners => string.IsNullOrEmpty (Authors) ? Owners : Authors;
        public string Description { get; set; } = "";
        public string ProjectUrl { get; set; } = "";
        public string IconUrl { get; set; } = "";
        public string LicenseUrl { get; set; } = "";

        public string RepositoryType { get; set; } = "";
        public string RepositoryUrl { get; set; } = "";
        public string RepositoryCommit { get; set; } = "";
        public string RepositoryFullUrl =>
            string.IsNullOrEmpty (RepositoryUrl) ? "" :
            (string.IsNullOrEmpty (RepositoryCommit) ? RepositoryUrlWithoutGit : CombinedRepositoryUrlAndCommit);
        string RepositoryUrlWithoutGit => RepositoryUrl.EndsWith(".git") ?
            RepositoryUrl.Substring(0, RepositoryUrl.Length - 4) :
            RepositoryUrl;
        string CombinedRepositoryUrlAndCommit => RepositoryUrlWithoutGit + "/tree/" + RepositoryCommit;
        public string RepositoryUrlTitle => "Source";

        public License MatchedLicense { get; set; }

        public string ProjectUrlTitle => Uri.TryCreate (ProjectUrl, UriKind.Absolute, out var uri) ? uri.Host : "Project Site";

        public bool SourceCodeIsPublic => !string.IsNullOrEmpty (ProjectUrl) && sourceCodeUrlRes.Any (x => x.IsMatch (ProjectUrl));

        public bool AllowedToDecompile => SourceCodeIsPublic || (MatchedLicense != null && MatchedLicense.AllowsDecompilation);

        public string DownloadUrl { get; set; } = "";
        public long SizeInBytes { get; set; }
        public ZipArchive Archive { get; set; }
        public List<PackageTargetFramework> TargetFrameworks { get; set; } = new List<PackageTargetFramework> ();
        public List<PackageFile> Content { get; } = new List<PackageFile> ();
        public List<PackageFile> Tools { get; } = new List<PackageFile> ();
        public Exception Error { get; set; }
        public string DisplayUrl { get; set; }
        public string PackageDetailsUrlFormat { get; set; }

        static readonly PackageDataCache cache = new PackageDataCache ();

        public string SafeIconUrl => string.IsNullOrEmpty (IconUrl) ? "/images/no-icon.png" : IconUrl;

        public static Task<PackageData> GetAsync (object inputId, object inputVersion, HttpClient client) => 
            GetAsync (inputId, inputVersion, client, CancellationToken.None);

        public static async Task<PackageData> GetAsync (object inputId, object inputVersion, HttpClient client, CancellationToken token)
        {
            var versions = await PackageVersions.GetAsync (inputId, client, token).ConfigureAwait (false);
            var version = versions.GetVersion (inputVersion);
            return await cache.GetAsync (versions.LowerId, version, client, token).ConfigureAwait (false);
        }

        public PackageTargetFramework FindClosestTargetFramework (object inputTargetFramework)
        {
            var moniker = (inputTargetFramework ?? "").ToString().Trim().ToLowerInvariant();
            
            var tf = TargetFrameworks.FirstOrDefault (x => x.Moniker == moniker);
            if (tf != null) return tf;

            tf = TargetFrameworks.LastOrDefault (x => x.Moniker.StartsWith ("netstandard2", StringComparison.Ordinal));
            if (tf != null) return tf;

            tf = TargetFrameworks.LastOrDefault (x => x.Moniker.StartsWith ("netstandard", StringComparison.Ordinal));
            if (tf != null) return tf;

            tf = TargetFrameworks.LastOrDefault (x => x.Moniker.StartsWith ("net", StringComparison.Ordinal));
            if (tf != null) return tf;
            
            if (tf == null)
                tf = TargetFrameworks.FirstOrDefault ();
            return tf;
        }

        public PackageTargetFramework FindExactTargetFramework (string moniker)
        {
            return TargetFrameworks.FirstOrDefault (x => x.Moniker == moniker);
        }

        public PackageSearchResults Search (string framework, string query)
        {
            var r = new PackageSearchResults (this) {
                Query = query,
            };

            var f = FindClosestTargetFramework(framework);
            if (f != null)
                f.Search (query, r);

            return r;
        }

        void Read (MemoryStream bytes, HttpClient httpClient)
        {
            SizeInBytes = bytes.Length;
            Archive = new ZipArchive (bytes, ZipArchiveMode.Read);
            TargetFrameworks.Clear ();
            Content.Clear();
            Tools.Clear();
            ZipArchiveEntry nuspecEntry = null;
            foreach (var e in Archive.Entries.Where(x => x.Name != "_._").OrderBy (x => x.FullName)) {
                var n = e.FullName;                
                var isBuild = n.StartsWith ("build/", StringComparison.InvariantCultureIgnoreCase);
                var isLib = n.StartsWith ("lib/", StringComparison.InvariantCultureIgnoreCase);
                if ((isBuild || isLib) && (n.EndsWith(".dll", StringComparison.InvariantCultureIgnoreCase) ||
                                           n.EndsWith(".exe", StringComparison.InvariantCultureIgnoreCase) ||
                                           n.EndsWith(".xml", StringComparison.InvariantCultureIgnoreCase))) {
                    var parts = n.Split ('/', StringSplitOptions.RemoveEmptyEntries);
                    string tfm;
                    if (parts.Length >= 3) {
                        tfm = Uri.UnescapeDataString (parts[1].Trim ().ToLowerInvariant ());
                    }
                    else {
                        tfm = "net";
                    }
                    var tf = TargetFrameworks.FirstOrDefault (x => x.Moniker == tfm);
                    if (tf == null) {
                        tf = new PackageTargetFramework (this, httpClient) {
                            Moniker = tfm,
                        };
                        TargetFrameworks.Add (tf);
                    }
                    if (n.EndsWith(".xml", StringComparison.InvariantCultureIgnoreCase)) {
                        var docs = new PackageAssemblyXmlDocs (e);
                        if (string.IsNullOrEmpty (docs.Error)) {
                            // System.Console.WriteLine(docs.AssemblyName);
                            tf.AssemblyXmlDocs[docs.AssemblyName] = docs;
                        }
                    }
                    else if (isBuild) {
                        tf.BuildAssemblies.Add (new PackageAssembly (e, tf));
                    }
                    else {
                        tf.Assemblies.Add (new PackageAssembly (e, tf));
                    }
                }
                else if (n.EndsWith (".nuspec", StringComparison.InvariantCultureIgnoreCase)) {
                    nuspecEntry = e;
                }
                else if (n.StartsWith ("content/", StringComparison.InvariantCultureIgnoreCase)) {
                    Content.Add (new PackageFile (e));
                }
                else if (n.StartsWith ("tools/", StringComparison.InvariantCultureIgnoreCase)) {
                    Tools.Add (new PackageFile (e));
                }
                else {
                    // System.Console.WriteLine(e);
                }
            }
            if (nuspecEntry != null) {
                ReadNuspec (nuspecEntry);
            }
            TargetFrameworks.Sort ((a,b) => string.Compare (a.Moniker, b.Moniker, StringComparison.Ordinal));
        }

        void ReadNuspec (ZipArchiveEntry entry)
        {
            using (var stream = entry.Open ()) {
                var xdoc = XDocument.Load (stream);
                var ns = xdoc.Root.Name.Namespace;
                var meta = xdoc.Root.Elements().FirstOrDefault(x => x.Name.LocalName == "metadata");
                if (meta == null) {
                    throw new Exception ("Failed to find metadata in " + xdoc);
                }
                string GetS (string name, string def = "") {
                    try { return meta.Element(ns + name).Value.Trim(); }
                    catch { return def; }
                }
                string GetUrl (string name) {
                    var u = GetS (name);
                    if (!u.StartsWith ("http", StringComparison.OrdinalIgnoreCase)) return "";
                    if (u.ToLowerInvariant ().Contains ("_url_here_or_delete")) return "";
                    return u;
                }
                NuspecXml = xdoc.ToString (SaveOptions.OmitDuplicateNamespaces);
                Id = GetS ("id", IndexId);
                Authors = GetS ("authors");
                Owners = GetS ("owners");
                ProjectUrl = GetUrl ("projectUrl");
                LicenseUrl = GetUrl ("licenseUrl");
                if (LicenseUrl == ProjectUrl) LicenseUrl = "";
                IconUrl = GetUrl ("iconUrl");
                Description = GetS ("description");
                var repo = meta.Element (ns + "repository");
                if (repo != null) {
                    RepositoryType = repo.Attribute ("type")?.Value ?? "";
                    RepositoryUrl = repo.Attribute ("url")?.Value ?? "";
                    RepositoryCommit = repo.Attribute ("commit")?.Value ?? "";
                }
                var deps = meta.Element (ns + "dependencies");
                if (deps != null) {
                    // System.Console.WriteLine(deps);
                    foreach (var de in deps.Elements()) {
                        if (de.Name.LocalName == "group") {
                            var tfa = de.Attribute("targetFramework");
                            if (tfa != null) {
                                var tfName = tfa.Value;
                                var tf = FindExactTargetFramework (TargetFrameworkNameToMoniker (tfName));
                                if (tf != null) {
                                    foreach (var ge in de.Elements(ns + "dependency")) {
                                        var dep = new PackageDependency (ge);
                                        tf.AddDependency (dep);
                                    }
                                }
                            }
                        }
                        else if (de.Name.LocalName == "dependency") {
                            var dep = new PackageDependency (de);
                            foreach (var tf in TargetFrameworks) {
                                tf.AddDependency (dep);
                            }
                        }
                    }
                }
            }
        }

        static readonly Regex[] sourceCodeUrlRes = {
            new Regex ("https?://github.com/[^/]+/[^/]+", RegexOptions.Compiled | RegexOptions.IgnoreCase),
            new Regex ("https?://bitbucket.org/[^/]+/[^/]+", RegexOptions.Compiled | RegexOptions.IgnoreCase),
        };

        async Task MatchLicenseAsync(HttpClient httpClient)
        {
            if (!string.IsNullOrEmpty (LicenseUrl)) {
                MatchedLicense = License.FindLicenseWithUrl (LicenseUrl);

                if (MatchedLicense == null) {
                    try {
                        var licenseText = await UrlExtensions.GetTextFileAsync (LicenseUrl, httpClient).ConfigureAwait (false);
                        MatchedLicense = License.FindLicenseWithText (licenseText);
                    }
                    catch (Exception ex) {
                        Debug.WriteLine ("Failed to read " + LicenseUrl);
                        Debug.WriteLine (ex);
                    }
                }
            }
        }

        async Task SaveDependenciesAsync ()
        {
            var db = new Database ();

            var depsq =
                from tf in TargetFrameworks
                from d in tf.Dependencies
                where PackageWorthRemembering (d.PackageId)
                select d.PackageId;
            var deps = depsq.Distinct ();

            var query = "select count(*) from StoredPackageDependency where LowerPackageId = ? and LowerDependentPackageId = ?";
            var lid = Id.ToLowerInvariant ();
            foreach (var d in deps) {
                var ld = d.ToLowerInvariant ();
                var count = await db.ExecuteScalarAsync<int> (query, ld, lid);
                if (count == 0) {
                    try {
                        await db.InsertAsync (new StoredPackageDependency {
                            LowerPackageId = ld,
                            LowerDependentPackageId = lid,
                            DependentPackageId = this.Id,
                        });
                        PackageDependents.Invalidate (ld);
                    }
                    catch (Exception ex) {
                        Console.WriteLine (ex);
                    }
                }
            }
        }

        static bool PackageWorthRemembering(string packageId)
        {
            return
                !packageId.StartsWith ("NETStandard.", StringComparison.OrdinalIgnoreCase) &&
                !packageId.StartsWith ("System.", StringComparison.OrdinalIgnoreCase) &&
                !packageId.StartsWith ("Microsoft.", StringComparison.OrdinalIgnoreCase);
        }

        static string TargetFrameworkNameToMoniker (string name)
        {
            var r = name.ToLowerInvariant ();
            if (r[0] == '.')
                r = r.Substring(1);
            if (r.StartsWith ("netframework", StringComparison.Ordinal))
                r = "net" + r.Substring (12).Replace(".", "");
            if (r.StartsWith ("windowsphoneapp", StringComparison.Ordinal))
                r = "wpa" + r.Substring (15).Replace(".0", "").Replace(".", "");
            if (r.StartsWith ("windowsphone", StringComparison.Ordinal))
                r = "wp" + r.Substring (12).Replace(".", "");
            if (r.StartsWith ("windows", StringComparison.Ordinal))
                r = "win" + r.Substring (7).Replace(".0", "").Replace(".", "");
            if (r.StartsWith ("xamarin.", StringComparison.Ordinal))
                r = "xamarin." + r.Substring (8).Replace(".", "");
            if (!r.StartsWith ("uap", StringComparison.Ordinal))
                r = r.Replace ("0.0", "");
            if (r.StartsWith ("netportable", StringComparison.Ordinal)) {
                var d = r.IndexOf('-');
                var s = r.Substring (d + 1);
                var i = s;
                switch (s) {
                    case "profile7":
                        return "netstandard1.1";
                    case "profile31":
                        return "netstandard1.0";
                    case "profile32":
                        return "netstandard1.2";
                    case "profile44":
                        return "netstandard1.2";
                    case "profile49":
                        return "netstandard1.0";
                    case "profile78":
                        return "netstandard1.0";
                    case "profile84":
                        return "netstandard1.0";
                    case "profile111":
                        return "netstandard1.1";
                    case "profile151":
                        return "netstandard1.2";
                    case "profile157":
                        return "netstandard1.0";
                    case "profile259":
                        return "netstandard1.0";
                    case "profile328":
                        i = "net40+sl5+win8+wp8+wpa81";
                        break;
                }
                r = "portable-" + i;
            }
            return r;
        }

        class PackageDataCache : DataCache<string, PackageVersion, PackageData>
        {
            public PackageDataCache() : base(TimeSpan.FromDays(365))
            {

            }

            protected override async Task<PackageData> GetValueAsync(string arg0, PackageVersion arg1, HttpClient httpClient, CancellationToken token)
            {
                var id = arg0;
                var version = arg1;

                var package = new PackageData {
                    Id = id,
                    IndexId = id,
                    Version = version
                };

                var exceptions = new List<Exception> ();
                foreach (var source in NugetPackageSources.PackageSources) {
                    try {
                        package.SizeInBytes = 0;
                        package.DisplayUrl = source.DisplayUrl;
                        package.PackageDetailsUrlFormat = source.PackageDetailsUrlFormat;
                        package.DownloadUrl = string.Format (source.DownloadUrlFormat, Uri.EscapeDataString (id), Uri.EscapeDataString (version.VersionString));
                        return await ReadPackageFromUrl (package, httpClient, token);
                    }
                    catch (OperationCanceledException) {
                        throw;
                    }
                    catch (Exception ex) {
                        exceptions.Add(ex);
                    }
                }

                // If we reach here, all urls failed, return the latest error.
                package.Error = exceptions.Count == 1 
                    ? exceptions[0] 
                    : new AggregateException (exceptions);

                return package;
            }

            private static async Task<PackageData> ReadPackageFromUrl (PackageData package, HttpClient httpClient, CancellationToken token)
            {
                //System.Console.WriteLine($"DOWNLOADING {package.DownloadUrl}");
                var r = await httpClient.GetAsync (package.DownloadUrl, token).ConfigureAwait (false);
                var data = new MemoryStream ();
                using (var s = await r.Content.ReadAsStreamAsync ().ConfigureAwait (false)) {
                    await s.CopyToAsync (data, 16 * 1024, token).ConfigureAwait (false);
                }
                data.Position = 0;
                await Task.Run (() => package.Read (data, httpClient), token).ConfigureAwait (false);
                await package.MatchLicenseAsync (httpClient).ConfigureAwait (false);
                await package.SaveDependenciesAsync ();
                return package;
            }
        }
    }
}
