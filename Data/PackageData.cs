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
                        tf.Assemblies.Add (new PackageAssembly (e, tf.AssemblyResolver));
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
                // Console.WriteLine (xdoc);
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
        public List<PackageAssembly> Assemblies { get; } = new List<PackageAssembly> ();
        public long SizeInBytes => Assemblies.Sum (x => x.SizeInBytes);

        public PackageAssemblyResolver AssemblyResolver { get; }

        public PackageTargetFramework()
        {
            AssemblyResolver = new PackageAssemblyResolver (this);
        }

        public PackageAssembly GetAssembly (object inputName)
        {
            var cleanName = (inputName ?? "").ToString().Trim();
            if (cleanName.Length == 0) {
                return Assemblies.OrderByDescending(x=>x.SizeInBytes).FirstOrDefault();
            }
            return Assemblies.FirstOrDefault (x => x.FileName == cleanName);
        }

        public class PackageAssemblyResolver : DefaultAssemblyResolver
        {
            readonly PackageTargetFramework packageTargetFramework;
            public PackageAssemblyResolver (PackageTargetFramework packageTargetFramework)
            {
                this.packageTargetFramework = packageTargetFramework;
            }
            public override AssemblyDefinition Resolve (AssemblyNameReference name)
            {
                var a = packageTargetFramework.Assemblies.FirstOrDefault(x => {
                    // System.Console.WriteLine("HOW ABOUT? " + x.Definition.Name);
                    return x.Definition.Name.Name == name.Name;
                });
                if (a != null) {
                    // System.Console.WriteLine("RESOLVED " + name);
                    return a.Definition;
                }                
                return base.Resolve (name);
            }
        }
    }

    public class PackageAssembly
    {
        public ZipArchiveEntry ArchiveEntry { get; }
        public string FileName => ArchiveEntry?.Name;
        public long SizeInBytes => ArchiveEntry != null ? ArchiveEntry.Length : 0;

        readonly Lazy<AssemblyDefinition> definition;
        readonly Lazy<ICSharpCode.Decompiler.CSharp.CSharpDecompiler> decompiler;
        private readonly IAssemblyResolver resolver;

        public AssemblyDefinition Definition => definition.Value;

        public PackageAssembly (ZipArchiveEntry entry, IAssemblyResolver resolver)
        {
            ArchiveEntry = entry;
            this.resolver = resolver;
            definition = new Lazy<AssemblyDefinition> (() => {
                if (ArchiveEntry == null)
                    return null;
                var ms = new MemoryStream ((int)ArchiveEntry.Length);
                using (var es = ArchiveEntry.Open ()) {
                    es.CopyTo (ms);
                    ms.Position = 0;
                }
                return AssemblyDefinition.ReadAssembly (ms, new ReaderParameters {
                    AssemblyResolver = resolver,
                });
            }, true);
            decompiler = new Lazy<ICSharpCode.Decompiler.CSharp.CSharpDecompiler> (() => {
                var m = Definition?.MainModule;
                if (m == null)
                    return null;
                return new ICSharpCode.Decompiler.CSharp.CSharpDecompiler (m, new ICSharpCode.Decompiler.DecompilerSettings {
                    ShowXmlDocumentation = true,
                    ThrowOnAssemblyResolveErrors = false,
                    CSharpFormattingOptions = ICSharpCode.Decompiler.CSharp.OutputVisitor.FormattingOptionsFactory.CreateMono (),
                });
            }, true);
        }

        public string DecompileType (TypeDefinition type)
        {
            try {
                var d = decompiler.Value;
                if (d == null)
                    return "// No decompiler available";
                return d.DecompileTypeAsString (new ICSharpCode.Decompiler.TypeSystem.FullTypeName (type.FullName));
            }
            catch (Exception e) {
                return "/* " + e.Message + " */";
            }
        }
    }
}
