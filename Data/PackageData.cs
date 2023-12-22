using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Net.Http;
using System.IO.Compression;
using System.Xml.Linq;
using System.Threading;
using System.Diagnostics;
using System.Text.RegularExpressions;
using System.Text;

namespace FuGetGallery
{
    public class Entry
    {
        public PackageData Package { get; set; }
        public string FullName { get; set; }
        public string Name { get; set; }
        public int Mode { get; set; }
        public int CompressedSize { get; set; }
        public int ExpandedSize { get; set; }
        public int EntryOffset { get; set; }
        public int Length => ExpandedSize;
        public override string ToString ()
        {
            return $"{FullName} {Mode}";
        }

        public Stream Open ()
        {
            return OpenAsync ().GetAwaiter ().GetResult ();
        }

        public async Task<Stream> OpenAsync () {

            Debug.WriteLine ("Downloading: " + this.FullName + " " + this.CompressedSize);
            var url = this.Package.DownloadUrl;
            var client = this.Package.Client;

            var req = new HttpRequestMessage () {
                Method = HttpMethod.Get,
                RequestUri = new Uri (url, UriKind.Absolute),
                Version = new Version (1, 1),
            };
            PackageData.AddDataRequestHeaders(req);
            req.Headers.Add ("Range", "bytes=" + EntryOffset + "-" + (EntryOffset + 30 -1));

            var resp = await client.SendAsync (req);
            var buf = await resp.Content.ReadAsByteArrayAsync ();

            var fileNameLen = buf.GetInt16 (26);
            var fileExtraLen = buf.GetInt16 (28);

            var ms = new MemoryStream (this.ExpandedSize);

            req = new HttpRequestMessage () {
                Method = HttpMethod.Get,
                RequestUri = new Uri (url, UriKind.Absolute),
                Version = new Version (1, 1),
            };
            PackageData.AddDataRequestHeaders(req);

            var deflateStart = EntryOffset + 30 + fileNameLen + fileExtraLen;


            req.Headers.Add ("Range", "bytes=" + deflateStart + "-" + (deflateStart + CompressedSize - 1));

            resp = await client.SendAsync (req);
            Stream stream = await resp.Content.ReadAsStreamAsync ();
            if (Mode == 8)  // deflate
            {
                stream = new DeflateStream (stream, CompressionMode.Decompress, false);
            }
            else {
                if (Mode != 0) { // store
                    throw new NotSupportedException ("Compression mode " + Mode + " not supported");
                }
            }

            stream.CopyTo (ms);
            ms.Position = 0;
            stream.Dispose ();
            return ms;
        }
    }

    static class BufferHelpers
    {
        public static int GetInt32 (this byte[] buffer, int offset)
        {
            return BitConverter.ToInt32 (buffer.AsSpan().Slice(offset, 4));
        }

        public static int GetInt16 (this byte[] buffer, int offset)
        {
            return BitConverter.ToInt16 (buffer.AsSpan ().Slice (offset, 2));
        }
    }

    public class PackageData
    {
        public HttpClient Client { get; set; }
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
        string CombinedRepositoryUrlAndCommit {
            get {
                if (RepositoryUrl.StartsWith ("https://dev.azure.com/"))
                    return RepositoryUrlWithoutGit + "?version=GC" + RepositoryCommit;
                else
                    return RepositoryUrlWithoutGit + "/tree/" + RepositoryCommit;
            }
        }
        public string RepositoryUrlTitle => "Source";

        public License MatchedLicense { get; set; }

        public string ProjectUrlTitle => Uri.TryCreate (ProjectUrl, UriKind.Absolute, out var uri) ? uri.Host : "Project Site";

        public bool SourceCodeIsPublic => !string.IsNullOrEmpty (ProjectUrl) && sourceCodeUrlRes.Any (x => x.IsMatch (ProjectUrl));

        public bool AllowedToDecompile => SourceCodeIsPublic || (MatchedLicense != null && MatchedLicense.AllowsDecompilation);

        public string DownloadUrl { get; set; } = "";
        public long SizeInBytes { get; set; }
       
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

        static Regex ContentRangeRegex = new Regex (@"^bytes (\d+)-(\d+)/(\d+)$", RegexOptions.Compiled);

        public static void AddDataRequestHeaders(HttpRequestMessage req)
        {
            // Magic string: https://github.com/praeclarum/FuGetGallery/pull/139#issuecomment-848272330
            req.Headers.UserAgent.Clear();
            req.Headers.UserAgent.Add(new System.Net.Http.Headers.ProductInfoHeaderValue("Mozilla", "5.0"));
            req.Headers.UserAgent.Add(new System.Net.Http.Headers.ProductInfoHeaderValue("(compatible; MSIE 9.0; Windows NT 6.1; Trident/5.0; AppInsights)"));
            req.Headers.UserAgent.Add(new System.Net.Http.Headers.ProductInfoHeaderValue("FuGetGallery", "1.0"));
            req.Headers.UserAgent.Add(new System.Net.Http.Headers.ProductInfoHeaderValue("(+https://github.com/praeclarum/FuGetGallery)"));
        }

        async Task<List<Entry>> ReadEntriesAsync (HttpClient client, string file)
        {
            var req = new HttpRequestMessage () {
                Method = HttpMethod.Get,
                RequestUri = new Uri (file, UriKind.Absolute),
                Version = new Version (1, 1),
            };
            AddDataRequestHeaders(req);
            req.Headers.Add ("Range", "bytes=0-1");
            var resp = await client.SendAsync (req);
            var buf = await resp.Content.ReadAsByteArrayAsync ();
            var str = resp.Content.Headers.GetValues ("Content-Range").Single ();
            var m = ContentRangeRegex.Match (str);
            var len = int.Parse (m.Groups[3].Value);

            req = new HttpRequestMessage () {
                Method = HttpMethod.Get,
                RequestUri = new Uri (file, UriKind.Absolute),
                Version = new Version (1, 1),
            };
            AddDataRequestHeaders(req);

            // this only works if the zip doesn't have a comment. which I think, should always be true, but who knows.
            req.Headers.Add ("Range", "bytes=" + (len - 22) + "-" + (len - 1));

            resp = await client.SendAsync (req);

            buf = await resp.Content.ReadAsByteArrayAsync ();

            var sig = buf.GetInt32 (0);
            if (sig != 0x06054b50) // likely because there was a comment
                throw new Exception ("Package format doesn't support quick access");

            int entryCount = buf.GetInt16 (8);
            int centralDirLen = buf.GetInt32 (12);
            int centralDirOff = buf.GetInt32 (16);

            req = new HttpRequestMessage () {
                Method = HttpMethod.Get,
                RequestUri = new Uri (file, UriKind.Absolute),
                Version = new Version (1, 1),
            };
            AddDataRequestHeaders(req);

            req.Headers.Add ("Range", "bytes=" + (centralDirOff) + "-" + (centralDirOff + centralDirLen - 1));

            resp = await client.SendAsync (req);
            buf = await resp.Content.ReadAsByteArrayAsync ();

            var entries = new List<Entry> (entryCount);
            var offset = 0;
            for (int i = 0; i < entryCount; i++) {
                sig = buf.GetInt32 (offset + 0);
                if (sig != 0x02014b50)
                    throw new Exception ();
                var compressionMethod = buf.GetInt16 (offset + 10);
                // 0 = not compressed, 8 = deflate anything else, we've got probs

                var compSize = buf.GetInt32 (offset + 20);
                var expSize = buf.GetInt32 (offset + 24);
                var nameLen = buf.GetInt16 (offset + 28);
                var extraLen = buf.GetInt16 (offset + 30);
                var comLen = buf.GetInt16 (offset + 32);

                var recOff = buf.GetInt32 (offset + 42);

                // TODO: not sure what encoding pkzip supports here
                // certainly most files will just be ASCII, sufficient for POC.
                var name = Encoding.ASCII.GetString (buf, offset + 46, nameLen);
                var entry = new Entry {
                    Package = this,
                    FullName = name,
                    Name = Path.GetFileName(name),
                    CompressedSize = compSize,
                    ExpandedSize = expSize,
                    Mode = compressionMethod,
                    EntryOffset = recOff,
                };
                entries.Add (entry);

                offset += 46 + nameLen + extraLen + comLen;
            }

            return entries;
        }

        async Task ReadAsync (HttpClient httpClient, string packageUri)
        {
            this.Client = httpClient;
            var entries = await ReadEntriesAsync (httpClient, packageUri);

            TargetFrameworks.Clear ();
            Content.Clear();
            Tools.Clear();
            Entry nuspecEntry = null;
            foreach (var e in entries.Where(x => Path.GetFileName(x.FullName) != "_._").OrderBy (x => x.FullName)) {
                var n = e.FullName;
                var isBuild = n.StartsWith ("build/", StringComparison.InvariantCultureIgnoreCase);
                var isLib = n.StartsWith ("lib/", StringComparison.InvariantCultureIgnoreCase);
                var isResources = n.EndsWith (".resources.dll", StringComparison.InvariantCultureIgnoreCase);
                if ((isBuild || isLib) && (n.EndsWith(".dll", StringComparison.InvariantCultureIgnoreCase) ||
                                           n.EndsWith(".exe", StringComparison.InvariantCultureIgnoreCase) ||
                                           n.EndsWith(".xml", StringComparison.InvariantCultureIgnoreCase))
                                       && !isResources) {
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
                        var docs = new PackageAssemblyXmlLanguageDocs (e);
                        if (string.IsNullOrEmpty (docs.Error)) {
                            // System.Console.WriteLine(docs.AssemblyName);
                            if (!tf.AssemblyXmlDocs.TryGetValue (docs.AssemblyName, out var allLanguageDocs)) {
                                allLanguageDocs = new PackageAssemblyXmlDocs (docs.AssemblyName);
                                tf.AssemblyXmlDocs[docs.AssemblyName] = allLanguageDocs;
                            }
                            allLanguageDocs.AddLanguage (docs.LanguageCode, docs);
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

        void ReadNuspec (Entry entry)
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

        Task SaveDependenciesAsync ()
        {
            //
            // ISSUE #155: Diabled due to database corruption.
            //
            return Task.CompletedTask;

            //var db = new Database ();
            //var depsq =
            //    from tf in TargetFrameworks
            //    from d in tf.Dependencies
            //    where PackageWorthRemembering (d.PackageId)
            //    select d.PackageId;
            //var deps = depsq.Distinct ();
            //var query = "select count(*) from StoredPackageDependency where LowerPackageId = ? and LowerDependentPackageId = ?";
            //var lid = Id.ToLowerInvariant ();
            //foreach (var d in deps) {
            //    var ld = d.ToLowerInvariant ();
            //    var count = await db.ExecuteScalarAsync<int> (query, ld, lid);
            //    if (count == 0) {
            //        try {
            //            await db.InsertAsync (new StoredPackageDependency {
            //                LowerPackageId = ld,
            //                LowerDependentPackageId = lid,
            //                DependentPackageId = this.Id,
            //            });
            //            PackageDependents.Invalidate (ld);
            //        }
            //        catch (Exception ex) {
            //            Console.WriteLine (ex);
            //        }
            //    }
            //}
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
            public PackageDataCache() : base(TimeSpan.FromDays(1))
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
                        package.DownloadUrl = string.Format (source.DownloadUrlFormat, Uri.EscapeDataString (id), Uri.EscapeDataString (version.ShortVersionString));
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
                await package.ReadAsync (httpClient, package.DownloadUrl);
                await package.MatchLicenseAsync (httpClient).ConfigureAwait (false);
                await package.SaveDependenciesAsync ();
                return package;
            }
        }
    }
}
