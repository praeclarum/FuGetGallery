using System;
using System.Collections.Generic;
using System.Linq;
using System.Collections.Concurrent;
using System.Threading.Tasks;
using System.Threading;
using Mono.Cecil;

namespace FuGetGallery
{
    public class PackageTargetFramework
    {
        public string Moniker { get; set; } = "";
        public PackageData Package { get; set; }
        public List<PackageDependency> Dependencies { get; } = new List<PackageDependency> ();
        public List<PackageAssembly> Assemblies { get; } = new List<PackageAssembly> ();
        public List<PackageAssembly> BuildAssemblies { get; } = new List<PackageAssembly> ();
        public Dictionary<string, PackageAssemblyXmlDocs> AssemblyXmlDocs { get; } = new Dictionary<string, PackageAssemblyXmlDocs> ();
        public long SizeInBytes => Assemblies.Sum (x => x.SizeInBytes);

        public PackageAssemblyResolver AssemblyResolver { get; }

        public PackageTargetFramework(PackageData package)
        {
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

        public string FindTypeUrl (string typeFullName)
        {
            if (typeFullName.StartsWith("System.")) {
                var slug = Uri.EscapeDataString(typeFullName).ToLowerInvariant();
                return $"https://docs.microsoft.com/en-us/dotnet/api/{slug}";
            }
            var types =
                from a in Assemblies.Concat(BuildAssemblies)
                from m in a.Definition.Modules
                select new { a, t = m.GetType (typeFullName) };
            var at = types.FirstOrDefault (x => x.t != null);
            if (at == null)
                return null;
            var dir = at.a.IsBuildAssembly ? "build" : "lib";
            return $"/packages/{Uri.EscapeDataString(Package.Id)}/{Uri.EscapeDataString(Package.Version)}/{dir}/{Uri.EscapeDataString(Moniker)}/{Uri.EscapeDataString(at.a.FileName)}/{Uri.EscapeDataString(at.t.Namespace)}/{Uri.EscapeDataString(at.t.Name)}";
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
            var asms = Assemblies.Concat(BuildAssemblies).ToList ();
            Parallel.ForEach (asms, a => {
                a.Search (query, r);
            });
        }

        public class PackageAssemblyResolver : DefaultAssemblyResolver
        {
            readonly string packageId;
            readonly PackageTargetFramework packageTargetFramework;
            public PackageAssemblyResolver (string packageId, PackageTargetFramework packageTargetFramework)
            {
                this.packageId = packageId;
                this.packageTargetFramework = packageTargetFramework;
            }
            public override AssemblyDefinition Resolve (AssemblyNameReference name)
            {
                // System.Console.WriteLine("RESOLVE " + name);

                //
                // See if the default resolver can find it
                //
                try {
                    return base.Resolve (name);
                }
                catch {}

                //
                // Try to find it in this package or one of its dependencies
                //
                var s = new ConcurrentDictionary<string, bool> ();
                var cts = new CancellationTokenSource ();
                s.TryAdd (packageId, true);
                
                var a = TryResolveInFrameworkAsync (name, packageTargetFramework, s, cts).Result;

                if (a != null) {
                    // System.Console.WriteLine("    RESOLVED " + name);
                    return a.Definition;
                }

                //
                // No? OK, maybe it's in the dependencies
                //
                // System.Console.WriteLine("!!! FAILED TO RESOLVE " + name);
                return null;
                // throw new Exception ("Failed to resolve: " + name);
            }

            Task<PackageAssembly> TryResolveInFrameworkAsync (AssemblyNameReference name, PackageTargetFramework packageTargetFramework, ConcurrentDictionary<string, bool> searchedPackages, CancellationTokenSource cts)
            {
                var a = packageTargetFramework.Assemblies.FirstOrDefault(x => {
                    // System.Console.WriteLine("HOW ABOUT? " + x.Definition.Name);
                    return x.Definition.Name.Name == name.Name;
                });
                if (a != null)
                    return Task.FromResult (a);

                int Order (PackageDependency d) => d.PackageId.StartsWith("System.") ? 1 : 0;

                var gotResultCS = new TaskCompletionSource<PackageAssembly> ();
                var tasks = new List<Task<PackageAssembly>> ();
                foreach (var d in packageTargetFramework.Dependencies.OrderBy (Order)) {
                    tasks.Add (TryResolveInDependencyAsync (name, d, searchedPackages, cts).ContinueWith(t => {
                        if (t.IsCompletedSuccessfully && t.Result != null) {
                            gotResultCS.TrySetResult (t.Result);
                            cts.Cancel ();
                            return t.Result;
                        }
                        return null;
                    }));
                }
                var allCompleteTask = Task.WhenAll (tasks);
                var anythingTask = Task.WhenAny (new[]{ (Task)gotResultCS.Task }.Append (allCompleteTask));

                return anythingTask.ContinueWith (t => {
                    // System.Console.WriteLine("DONE RESOLVING IN " + packageTargetFramework.Moniker);
                    if (gotResultCS.Task.IsCompletedSuccessfully) {
                        return gotResultCS.Task.Result;
                    }
                    return null;
                });
            }

            async Task<PackageAssembly> TryResolveInDependencyAsync (AssemblyNameReference name, PackageDependency dep, ConcurrentDictionary<string, bool> searchedPackages, CancellationTokenSource cts)
            {
                var lowerPackageId = dep.PackageId.ToLowerInvariant ();
                if (searchedPackages.ContainsKey (lowerPackageId))
                    return null;
                searchedPackages.TryAdd (lowerPackageId, true);

                try {
                    var package = await PackageData.GetAsync(dep.PackageId, dep.VersionSpec, cts.Token).ConfigureAwait (false);
                    if (cts.Token.IsCancellationRequested)
                        return null;
                    var fw = package.FindClosestTargetFramework (packageTargetFramework.Moniker);
                    if (fw != null) {
                        return await TryResolveInFrameworkAsync (name, fw, searchedPackages, cts).ConfigureAwait (false);
                    }
                }
                catch {}
                return null;
            }

        }
    }
}
