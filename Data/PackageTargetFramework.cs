using System;
using System.Collections.Generic;
using System.Linq;
using System.Collections.Concurrent;
using Mono.Cecil;

namespace FuGetGallery
{
    public class PackageTargetFramework
    {
        public string Moniker { get; set; } = "";
        public List<PackageDependency> Dependencies { get; } = new List<PackageDependency> ();
        public List<PackageAssembly> Assemblies { get; } = new List<PackageAssembly> ();
        public List<PackageAssembly> BuildAssemblies { get; } = new List<PackageAssembly> ();
        public Dictionary<string, PackageAssemblyXmlDocs> AssemblyXmlDocs { get; } = new Dictionary<string, PackageAssemblyXmlDocs> ();
        public long SizeInBytes => Assemblies.Sum (x => x.SizeInBytes);

        public PackageAssemblyResolver AssemblyResolver { get; }

        readonly ConcurrentDictionary<TypeDefinition, TypeDocumentation> typeDocs =
            new ConcurrentDictionary<TypeDefinition, TypeDocumentation> ();

        public PackageTargetFramework(string lowerPackageId)
        {
            AssemblyResolver = new PackageAssemblyResolver (lowerPackageId, this);
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

        public TypeDocumentation GetTypeDocumentation (TypeDefinition typeDefinition)
        {
            if (typeDocs.TryGetValue (typeDefinition, out var docs)) {
                return docs;
            }
            var asmName = typeDefinition.Module.Assembly.Name.Name;
            AssemblyXmlDocs.TryGetValue (asmName, out var xmlDocs);
            docs = new TypeDocumentation (typeDefinition, xmlDocs);
            typeDocs.TryAdd (typeDefinition, docs);
            return docs;
        }

        public void AddDependency (PackageDependency d)
        {
            var existing = Dependencies.FirstOrDefault (x => x.PackageId == d.PackageId);
            if (existing == null) {
                Dependencies.Add (d);
            }
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
                var s = new HashSet<string> ();
                s.Add (packageId);
                
                var a = TryResolveInFramework (name, packageTargetFramework, s);

                if (a != null) {
                    // System.Console.WriteLine("    RESOLVED " + name);
                    return a.Definition;
                }

                //
                // No? OK, maybe it's in the dependencies
                //
                System.Console.WriteLine("!!! FAILED TO RESOLVE " + name);
                throw new Exception ("Failed to resolve: " + name);
            }

            PackageAssembly TryResolveInFramework (AssemblyNameReference name, PackageTargetFramework packageTargetFramework, HashSet<string> searchedPackages)
            {
                var a = packageTargetFramework.Assemblies.FirstOrDefault(x => {
                    // System.Console.WriteLine("HOW ABOUT? " + x.Definition.Name);
                    return x.Definition.Name.Name == name.Name;
                });

                if (a == null) {
                    int Order (PackageDependency d) => d.PackageId.StartsWith("System.") ? 1 : 0;

                    foreach (var d in packageTargetFramework.Dependencies.OrderBy (Order)) {
                        a = TryResolveInDependency (name, d, searchedPackages);
                        if (a != null)
                            break;
                    }

                    if (a == null) {
                        var bud = new PackageDependency{PackageId = "Microsoft.Build.Utilities.Core", VersionSpec="15.6.82" };
                        a = TryResolveInDependency (name, bud, searchedPackages);                        
                    }
                }

                return a;
            }

            PackageAssembly TryResolveInDependency (AssemblyNameReference name, PackageDependency dep, HashSet<string> searchedPackages)
            {
                var lowerPackageId = dep.PackageId.ToLowerInvariant ();
                if (searchedPackages.Contains (lowerPackageId))
                    return null;
                searchedPackages.Add (lowerPackageId);

                try {
                    var package = PackageData.GetAsync(dep.PackageId, dep.VersionSpec).Result;
                    var fw = package.FindClosestTargetFramework (packageTargetFramework.Moniker);
                    if (fw != null) {
                        return TryResolveInFramework (name, fw, searchedPackages);
                    }
                }
                catch {}
                return null;
            }

        }
    }
}
