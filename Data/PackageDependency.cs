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
        public string PackageId { get; }
        public string VersionSpec { get; }

        public PackageDependency (XElement element)
        {
            PackageId = element.Attribute("id").Value;
            VersionSpec = element.Attribute("version").Value;
        }
    }
}
