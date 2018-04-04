using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Net.Http;
using System.IO.Compression;
using Mono.Cecil;

namespace FuGetGallery
{
    public class PackageData
    {
        public string Id { get; set; } = "";
        public string IndexId { get; set; } = "";
        public string Version { get; set; } = "";

        public string Authors { get; set; } = "";
        public string Owners { get; set; } = "";
        public string AuthorsOrOwners => string.IsNullOrEmpty (Authors) ? Owners : Authors;
        public string Description { get; set; } = "";
        public string ProjectUrl { get; set; } = "";
        public string IconUrl { get; set; } = "";

        public string DownloadUrl { get; set; } = "";
        public long SizeInBytes { get; set; }
        public ZipArchive Archive { get; set; }
        public List<PackageTargetFramework> TargetFrameworks { get; set; } = new List<PackageTargetFramework> ();
        public Exception Error { get; set; }

        static readonly PackageDataCache cache = new PackageDataCache ();


        public static async Task<PackageData> GetAsync (object inputId, object inputVersion)
        {
            var cleanId = (inputId ?? "").ToString().Trim().ToLowerInvariant();

            var versions = await PackageVersions.GetAsync (inputId).ConfigureAwait (false);
            var version = versions.GetVersion (inputVersion);

            return await cache.GetAsync (versions.LowerId, version.Version).ConfigureAwait (false);
        }

        public PackageTargetFramework GetTargetFramework (object inputTargetFramework)
        {
            var moniker = (inputTargetFramework ?? "").ToString().Trim().ToLowerInvariant();
            
            var tf = TargetFrameworks.FirstOrDefault (x => x.Moniker == moniker);
            if (tf != null) return tf;
            
            tf = TargetFrameworks.LastOrDefault (x => x.Moniker.StartsWith("netstandard2"));
            if (tf != null) return tf;

            tf = TargetFrameworks.LastOrDefault (x => x.Moniker.StartsWith("netstandard"));
            if (tf != null) return tf;
            
            if (tf == null)
                tf = TargetFrameworks.FirstOrDefault ();
            return tf;
        }

        void Read (byte[] bytes)
        {
            SizeInBytes = bytes.LongLength;
            Archive = new ZipArchive (new MemoryStream (bytes), ZipArchiveMode.Read);
            TargetFrameworks.Clear ();
            ZipArchiveEntry nuspecEntry = null;
            foreach (var e in Archive.Entries.OrderBy (x => x.FullName)) {
                var n = e.FullName;
                var isBuild = n.StartsWith ("build/");
                var isLib = n.StartsWith ("lib/");
                if ((isBuild || isLib) && (n.EndsWith(".dll", StringComparison.InvariantCultureIgnoreCase) ||
                                           n.EndsWith(".exe", StringComparison.InvariantCultureIgnoreCase) ||
                                           n.EndsWith(".xml", StringComparison.InvariantCultureIgnoreCase) ||
                                           n.EndsWith(".targets", StringComparison.InvariantCultureIgnoreCase))) {
                    var parts = n.Split ('/', StringSplitOptions.RemoveEmptyEntries);
                    if (parts.Length >= 3) {
                        var tfm = Uri.UnescapeDataString (parts[1].Trim ().ToLowerInvariant ());
                        var tf = TargetFrameworks.FirstOrDefault (x => x.Moniker == tfm);
                        if (tf == null) {
                            tf = new PackageTargetFramework {
                                Moniker = tfm,
                            };
                            TargetFrameworks.Add (tf);
                        }
                        if (n.EndsWith(".targets", StringComparison.InvariantCultureIgnoreCase)) {
                        }
                        else if (n.EndsWith(".xml", StringComparison.InvariantCultureIgnoreCase)) {
                            var docs = new PackageAssemblyXmlDocs (e);
                            if (string.IsNullOrEmpty (docs.Error)) {
                                // System.Console.WriteLine(docs.AssemblyName);
                                tf.AssemblyXmlDocs[docs.AssemblyName] = docs;
                            }
                        }
                        else if (isBuild) {
                            tf.BuildAssemblies.Add (new PackageAssembly (e, tf.AssemblyResolver));
                        }
                        else {
                            tf.Assemblies.Add (new PackageAssembly (e, tf.AssemblyResolver));
                        }
                    }
                }
                else if (n.EndsWith (".nuspec", StringComparison.InvariantCultureIgnoreCase)) {
                    nuspecEntry = e;
                }
            }
            if (nuspecEntry != null) {
                ReadNuspec (nuspecEntry);
            }
        }

        void ReadNuspec (ZipArchiveEntry entry)
        {
            using (var stream = entry.Open ()) {
                var xdoc = System.Xml.Linq.XDocument.Load (stream);
                var ns = xdoc.Root.Name.Namespace;
                var meta = xdoc.Root.Element(ns + "metadata");
                string GetS (string name, string def = "") {
                    try { return meta.Element(ns + name).Value.Trim(); }
                    catch { return def; }
                }
                Id = GetS ("id");
                Authors = GetS ("authors");
                Owners = GetS ("owners");
                ProjectUrl = GetS ("projectUrl");
                IconUrl = GetS ("iconUrl");
                Description = GetS ("description");
            }
        }

        class PackageDataCache : DataCache<string, string, PackageData>
        {
            public PackageDataCache () : base (TimeSpan.FromDays (365)) { }
            readonly HttpClient httpClient = new HttpClient ();
            protected override async Task<PackageData> GetValueAsync(string id, string version)
            {
                var package = new PackageData {
                    Id = id,
                    IndexId = id,
                    Version = version,
                    SizeInBytes = 0,
                    DownloadUrl = $"https://www.nuget.org/api/v2/package/{Uri.EscapeDataString(id)}/{Uri.EscapeDataString(version)}",
                };
                try {
                    var data = await httpClient.GetByteArrayAsync (package.DownloadUrl).ConfigureAwait (false);
                    package.Read (data);
                }
                catch (Exception ex) {
                    package.Error = ex;
                }

                return package;
            }
        }
    }
}
