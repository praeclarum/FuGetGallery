using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Net.Http;
using System.IO.Compression;

namespace FuGetGallery
{
    public class PackageData
    {
        public string Id { get; set; } = "";
        public string IndexId { get; set; } = "";
        public string Version { get; set; } = "";

        public string DownloadUrl { get; set; } = "";
        public long SizeInBytes { get; set; }
        public ZipArchive Archive { get; set; }
        public List<PackageTargetFramework> TargetFrameworks { get; set; } = new List<PackageTargetFramework> ();
        public Exception Error { get; set; }

        static readonly PackageDataCache cache = new PackageDataCache ();

        public static Task<PackageData> GetAsync (object inputId, object inputVersion)
        {
            var cleanId = (inputId ?? "").ToString().Trim().ToLowerInvariant();
            var cleanVersion = (inputVersion ?? "").ToString().Trim().ToLowerInvariant();
            return cache.GetAsync (cleanId, cleanVersion);
        }

        public PackageTargetFramework GetTargetFramework (object inputTargetFramework)
        {
            var moniker = (inputTargetFramework ?? "").ToString().Trim().ToLowerInvariant();
            var tf = TargetFrameworks.FirstOrDefault (x => x.Moniker == moniker);
            if (tf == null)
                tf = TargetFrameworks.FirstOrDefault ();
            return tf;
        }

        void Read (byte[] bytes)
        {
            SizeInBytes = bytes.LongLength;
            Archive = new ZipArchive (new MemoryStream (bytes), ZipArchiveMode.Read);
            TargetFrameworks.Clear ();
            foreach (var e in Archive.Entries) {
                var n = e.FullName;
                if (n.StartsWith ("lib/") && (n.EndsWith(".dll", StringComparison.InvariantCultureIgnoreCase) ||
                                              n.EndsWith(".exe", StringComparison.InvariantCultureIgnoreCase))) {
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
                        tf.Assemblies.Add (new PackageAssembly { ArchiveEntry = e });
                    }
                }
                else if (n.EndsWith (".nuspec", StringComparison.InvariantCultureIgnoreCase)) {
                    ReadNuspec (e);
                }
            }
        }

        void ReadNuspec (ZipArchiveEntry entry)
        {
            using (var stream = entry.Open ()) {
                var xdoc = System.Xml.Linq.XDocument.Load (stream);
                var ns = xdoc.Root.Name.Namespace;
                Id = xdoc.Root.Element(ns + "metadata").Element(ns + "id").Value.Trim();
                Console.WriteLine (xdoc);
            }
        }

        class PackageDataCache : DataCache<string, string, PackageData>
        {
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

    public class PackageTargetFramework
    {
        public string Moniker { get; set; } = "";
        public List<PackageAssembly> Assemblies { get; set; } = new List<PackageAssembly> ();
        public long SizeInBytes => Assemblies.Sum (x => x.SizeInBytes);
    }

    public class PackageAssembly
    {
        public ZipArchiveEntry ArchiveEntry { get; set; }
        public string FileName => ArchiveEntry?.Name;
        public long SizeInBytes => ArchiveEntry != null ? ArchiveEntry.Length : 0;
    }
}
