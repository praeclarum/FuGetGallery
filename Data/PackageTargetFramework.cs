using System;
using System.Collections.Generic;
using System.Linq;
using System.Collections.Concurrent;
using System.Threading.Tasks;
using System.Threading;
using Mono.Cecil;
using System.Diagnostics;
using System.Net.Http;
using ICSharpCode.Decompiler.TypeSystem;

namespace FuGetGallery
{
    public class PackageTargetFramework
    {
        private readonly HttpClient httpClient;

        public string Moniker { get; set; } = "";
        public PackageData Package { get; set; }
        public List<PackageDependency> Dependencies { get; } = new List<PackageDependency> ();
        public List<PackageAssembly> Assemblies { get; } = new List<PackageAssembly> ();
        public List<PackageAssembly> BuildAssemblies { get; } = new List<PackageAssembly> ();
        public Dictionary<string, PackageAssemblyXmlDocs> AssemblyXmlDocs { get; } = new Dictionary<string, PackageAssemblyXmlDocs> ();
        public long SizeInBytes => Assemblies.Sum (x => x.SizeInBytes);

        public PackageAssemblyResolver AssemblyResolver { get; }

        public IEnumerable<PackageAssembly> PublicAssemblies => Assemblies.Where (x => x.IsPublic).OrderBy (x => x.Definition.Name.Name);

        public PackageTargetFramework(PackageData package, HttpClient httpClient)
        {
            this.httpClient = httpClient;
            Package = package;
            AssemblyResolver = new PackageAssemblyResolver (package.IndexId, this);
        }

        public PackageAssembly GetAssembly (object inputDir, object inputName)
        {
            var asms = "build".Equals(inputDir) ? BuildAssemblies : Assemblies;

            var cleanName = (inputName ?? "").ToString().Trim();
            if (cleanName.Length == 0) {
                return asms.OrderByDescending(x=>x.SizeInBytes).FirstOrDefault();
            }
            return asms.FirstOrDefault (x => x.FileName == cleanName);
        }

        public string FindTypeUrl (IType type) => FindTypeUrl (type.FullName);

        public string FindTypeUrl (TypeDefinition type) => FindTypeUrl (type.FullName);

        public string FindTypeUrl (TypeReference type) => type.IsGenericParameter ? null : FindTypeUrl (type.FullName);

        readonly ConcurrentDictionary<string, string> typeUrls = new ConcurrentDictionary<string, string> ();

        public string FindTypeUrl (string typeFullName, bool shallow = false)
        {
            if (typeUrls.TryGetValue (typeFullName, out var url))
                return url;

            var types =
                from a in Assemblies.Concat (BuildAssemblies)
                from m in a.Definition.Modules
                select new { a, t = m.GetType (typeFullName) };
            var at = types.FirstOrDefault (x => x.t != null);

            if (at == null) {
                if (typeFullName.StartsWith("System.", StringComparison.Ordinal)) {
                    var slug = Uri.EscapeDataString(typeFullName.Replace('`', '-')).ToLowerInvariant();
                    return $"https://docs.microsoft.com/en-us/dotnet/api/{slug}";
                }
                if (typeFullName.StartsWith ("Windows.", StringComparison.Ordinal)) {
                    var slug = Uri.EscapeDataString (typeFullName.Replace ('`', '-')).ToLowerInvariant ();
                    return $"https://docs.microsoft.com/en-us/uwp/api/{slug}";
                }
                if (typeFullName.StartsWith ("Foundation.", StringComparison.Ordinal)
                    || typeFullName.StartsWith ("UIKit.", StringComparison.Ordinal)
                    || typeFullName.StartsWith ("AppKit.", StringComparison.Ordinal)
                    || typeFullName.StartsWith ("CoreGraphics.", StringComparison.Ordinal)
                    || typeFullName.StartsWith ("Android.", StringComparison.Ordinal)) {
                    var slug = Uri.EscapeDataString (typeFullName);
                    return $"https://developer.xamarin.com/api/type/{slug}";
                }

                if (!shallow) {
                    //var sw = new Stopwatch ();
                    //sw.Start ();
                    url = DeepFindTypeUrlAsync (
                        typeFullName, 
                        new ConcurrentDictionary<string, bool> (),
                        new ConcurrentQueue<string> (),
                        httpClient
                        ).Result;
                    //sw.Stop ();
                    //Console.WriteLine ($"RESOLVED IN {sw.ElapsedMilliseconds} millis: {typeFullName} ---> {url}");
                    if (url != null)
                        typeUrls.TryAdd (typeFullName, url);
                    return url;
                }
                return null;
            }
            var dir = at.a.IsBuildAssembly ? "build" : "lib";
            return $"/packages/{Uri.EscapeDataString(Package.Id)}/{Uri.EscapeDataString(Package.Version.VersionString)}/{dir}/{Uri.EscapeDataString(Moniker)}/{Uri.EscapeDataString(at.a.FileName)}/{Uri.EscapeDataString(at.t.Namespace)}/{Uri.EscapeDataString(at.t.Name)}";
        }

        async Task<string> DeepFindTypeUrlAsync (
            string typeFullName,
            ConcurrentDictionary<string, bool> tried,
            ConcurrentQueue<string> found,
            HttpClient httpClient)
        {
            var url = FindTypeUrl (typeFullName, shallow: true);
            if (url != null)
                return url;

            if (found.Count > 0) return null;

            var depFrameworks = new List<PackageTargetFramework> ();

            var shallowDepPackages = Dependencies.Select (async x => {
                if (!tried.TryAdd (x.PackageId, true)) return null;
                //Console.WriteLine ("TRY " + x.PackageId);
                var data = await PackageData.GetAsync (x.PackageId, x.VersionSpec, httpClient, CancellationToken.None).ConfigureAwait (false);
                if (found.Count > 0) return null;
                var fw = data.FindClosestTargetFramework (this.Moniker);
                if (fw == null) return null;
                depFrameworks.Add (fw);
                var r = fw.FindTypeUrl (typeFullName, shallow: true);
                if (r != null) found.Enqueue (r);
                return r;
            });
            var shallowResults = await Task.WhenAll (shallowDepPackages).ConfigureAwait (false);
            var shallowResult = shallowResults.FirstOrDefault (x => x != null);
            if (shallowResult != null)
                return shallowResult;

            var deepDepPackages = depFrameworks.Select (async fw => {
                var r = await fw.DeepFindTypeUrlAsync (typeFullName, tried, found, httpClient).ConfigureAwait (false);
                if (r != null) found.Enqueue (r);
                return r;
            });
            var results = await Task.WhenAll (deepDepPackages).ConfigureAwait (false);
            return results.FirstOrDefault (x => x != null);
        }

        public void AddDependency (PackageDependency d)
        {
            var existing = Dependencies.FirstOrDefault (x => x.PackageId == d.PackageId);
            if (existing == null) {
                Dependencies.Add (d);
            }
        }

        public void Search (string query, PackageSearchResults r)
        {
            Parallel.ForEach (Assemblies, a => {
                a.Search (query, r);
            });
        }

        public class PackageAssemblyResolver : DefaultAssemblyResolver
        {
            readonly string packageId;
            readonly PackageTargetFramework packageTargetFramework;
            private readonly HttpClient httpClient;
            readonly ConcurrentDictionary<string, AssemblyDefinition> assemblies = new ConcurrentDictionary<string, AssemblyDefinition> ();
            
            public PackageAssemblyResolver (string packageId, PackageTargetFramework packageTargetFramework)
            {
                this.packageId = packageId;
                this.packageTargetFramework = packageTargetFramework;
                this.httpClient = packageTargetFramework.httpClient;
            }

            public override AssemblyDefinition Resolve (AssemblyNameReference name)
            {
                //System.Console.WriteLine("RESOLVE " + name);

                //
                // See if we already have it
                //
                if (assemblies.TryGetValue (name.Name, out var asm))
                    return asm;

                //
                // See if the default resolver can find it
                //
                try {
                    asm = base.Resolve (name);
                    if (asm != null) {
                        assemblies[name.Name] = asm;
                        return asm;
                    }
                }
                catch {}

                //
                // Try to find it in this package or one of its dependencies
                //
                var s = new ConcurrentDictionary<string, bool> ();
                var cts = new ConcurrentQueue<PackageAssembly> ();
                s.TryAdd (packageId, true);
                
                var a = TryResolveInFrameworkAsync (name, packageTargetFramework, s, cts).Result;

                if (a != null) {
                    //System.Console.WriteLine("    RESOLVED " + name);
                    assemblies[name.Name] = a.Definition;
                    return a.Definition;
                }

                //
                // No? OK, maybe it's in the dependencies
                //
                //System.Console.WriteLine("!!! FAILED TO RESOLVE " + name);
                assemblies[name.Name] = null;
                return null;
                // throw new Exception ("Failed to resolve: " + name);
            }

            async Task<PackageAssembly> TryResolveInFrameworkAsync (
                AssemblyNameReference name,
                PackageTargetFramework framework,
                ConcurrentDictionary<string, bool> searchedPackages,
                ConcurrentQueue<PackageAssembly> found)
            {
                var a = framework.Assemblies.FirstOrDefault(x => {
                    // System.Console.WriteLine("HOW ABOUT? " + x.Definition.Name);
                    return x.Definition.Name.Name == name.Name;
                });
                if (a != null) {
                    found.Enqueue (a);
                    return a;
                }

                if (found.Count > 0) return null;

                var tasks = new List<Task<PackageAssembly>> ();
                int Order (PackageDependency d) => d.PackageId.StartsWith("System.", StringComparison.Ordinal) ? 1 : 0;
                foreach (var d in framework.Dependencies.OrderBy (Order)) {
                    tasks.Add (TryResolveInDependencyAsync (name, d, searchedPackages, found));
                }

                if (found.Count > 0) return null;

                var results = await Task.WhenAll (tasks).ConfigureAwait (false);

                return results.FirstOrDefault (x => x != null);
            }

            async Task<PackageAssembly> TryResolveInDependencyAsync (
                AssemblyNameReference name,
                PackageDependency dep,
                ConcurrentDictionary<string, bool> searchedPackages,
                ConcurrentQueue<PackageAssembly> found)
            {
                if (found.Count > 0) return null;

                var lowerPackageId = dep.PackageId.ToLowerInvariant ();
                if (!searchedPackages.TryAdd (lowerPackageId, true))
                    return null;

                try {
                    var package = await PackageData.GetAsync(dep.PackageId, dep.VersionSpec, httpClient, CancellationToken.None).ConfigureAwait (false);
                    if (found.Count > 0) return null;
                    var fw = package.FindClosestTargetFramework (packageTargetFramework.Moniker);
                    if (fw != null) {
                        var r = await TryResolveInFrameworkAsync (name, fw, searchedPackages, found).ConfigureAwait (false);
                        if (r != null) {
                            found.Enqueue (r);
                        }
                        return r;
                    }
                }
                catch (Exception ex) {
                    Debug.WriteLine (ex);
                }
                return null;
            }
        }
    }
}
