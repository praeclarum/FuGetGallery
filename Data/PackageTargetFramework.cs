using System.Collections.Generic;
using System.Linq;
using Mono.Cecil;

namespace FuGetGallery
{
    public class PackageTargetFramework
    {
        public string Moniker { get; set; } = "";
        public List<PackageAssembly> Assemblies { get; } = new List<PackageAssembly> ();
        public List<PackageAssemblyXmlDocs> AssemblyXmlDocs { get; } = new List<PackageAssemblyXmlDocs> ();
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
}
