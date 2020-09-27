using System;
using System.Linq;
using Mono.Cecil;

namespace FuGetGallery
{
    public class PackageContext
    {
        public PackageData package;
        public PackageTargetFramework framework;
        public PackageAssembly asm;
        public string dir;
        public bool autodir;
        public IGrouping<string, TypeDefinition> ns;

        public PackageVersions versions;
        public PackageVersion versionSpec;
        public string nupkgName;

        public string PackageId { get; set; } = "";
        public string PackageVersion { get; set; } = "";
        public string TargetFramework { get; set; } = "";
        public string Directory { get; set; } = "";
        public string AssemblyName { get; set; } = "";
        public string Namespace { get; set; } = "";
        public string TypeName { get; set; } = "";
        public bool ShowCode { get; set; }

        public string authors => package.AuthorsOrOwners;


        public string GetUrl (object oid = null, object oversion = null, object otargetFramework = null, object odir = null, object oassemblyName = null, object onamespace = null, object otypeName = null, bool ocode = false)
        {
            oid = (oid ?? package?.Id) ?? PackageId;
            oversion = (oversion ?? package?.Version) ?? PackageVersion;
            otargetFramework = (otargetFramework ?? framework?.Moniker) ?? TargetFramework;
            odir = (odir ?? dir) ?? Directory;
            if (autodir && oassemblyName == null && onamespace == null && otypeName == null && ocode == null)
                odir = null;
            oassemblyName = (oassemblyName ?? asm?.FileName) ?? ("diff".Equals (AssemblyName) ? null : AssemblyName);
            onamespace = (onamespace ?? ns?.Key) ?? Namespace;
            otypeName = otypeName ?? TypeName;
            ocode = ShowCode;
            var r = "/packages";
            if (oid != null) {
                r += "/" + Uri.EscapeDataString (oid.ToString ());
                if (oversion != null) {
                    r += "/" + Uri.EscapeDataString (oversion.ToString ());
                    if (odir != null) {
                        r += "/" + Uri.EscapeDataString (odir.ToString ());
                        if (otargetFramework != null) {
                            r += "/" + Uri.EscapeDataString (otargetFramework.ToString ());
                            if (oassemblyName != null) {
                                r += "/" + Uri.EscapeDataString (oassemblyName.ToString ());
                                if (onamespace != null) {
                                    r += "/" + Uri.EscapeDataString (onamespace.ToString ());
                                    if (otypeName != null) {
                                        r += "/" + Uri.EscapeDataString (otypeName.ToString ());
                                    }
                                }
                            }
                        }
                    }
                }
            }
            if (ocode && package.AllowedToDecompile) {
                r += "?code=true";
            }
            return r;
        }
    }
}
