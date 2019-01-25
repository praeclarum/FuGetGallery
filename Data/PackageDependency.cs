using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Xml.Linq;
using Mono.Cecil;
using SQLite;

namespace FuGetGallery
{
    public class PackageDependency
    {
        public string PackageId { get; set; } = "";
        public string VersionSpec { get; set; } = "";

        public PackageDependency ()
        {
        }

        public PackageDependency (XElement element)
        {
            PackageId = element.Attribute("id").Value;
            VersionSpec = element.Attribute("version")?.Value ?? "0";
        }
    }

    class StoredPackageDependency
    {
        [Unique(Name="StoredPackageDependency_U", Order=0), NotNull]
        public string PackageId { get; set; }
        [Unique(Name="StoredPackageDependency_U", Order=1), NotNull]
        public string DependentPackageId { get; set; }
    }
}
