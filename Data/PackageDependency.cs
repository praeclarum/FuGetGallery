using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Xml.Linq;
using Mono.Cecil;

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
}
