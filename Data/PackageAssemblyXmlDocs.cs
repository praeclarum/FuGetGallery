using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Xml.Linq;
using Mono.Cecil;

namespace FuGetGallery
{
    public class PackageAssemblyXmlDocs
    {
        readonly XDocument doc;

        public string Error { get; }

        public Dictionary<string, MemberXmlDocs> MemberDocs { get; } = new Dictionary<string, MemberXmlDocs> ();

        public PackageAssemblyXmlDocs (ZipArchiveEntry entry)
        {
            try {
                using (var s = entry.Open ()) {
                    doc = XDocument.Load (s);
                    var ns = doc.Root.Name.Namespace;
                    foreach (var me in doc.Root.Element (ns + "members").Elements (ns + "member")) {
                        var m = new MemberXmlDocs (me);
                        // System.Console.WriteLine(m.Name);
                        if (!string.IsNullOrEmpty (m.Name)) {
                            MemberDocs[m.Name] = m;
                        }
                    }
                }
                Error = "";
            }
            catch (Exception ex)
            {
                System.Console.WriteLine (ex);
                Error = ex.Message;
            }
        }
    }

    public class MemberXmlDocs
    {
        public string Name { get; }
        public XElement SummaryXml { get; }

        public string SummaryText => SummaryXml != null ? SummaryXml.Value : "";

        public MemberXmlDocs (XElement memberElement)
        {
            var ns = memberElement.Name.Namespace;
            var na = memberElement.Attribute("name");
            if (na != null && !string.IsNullOrWhiteSpace (na.Value)) {
                Name = na.Value;
            }
            else {
                Name = "";
            }
            SummaryXml = memberElement.Element (ns + "summary");
            // System.Console.WriteLine(SummaryXml.ToString());
        }
    }
}
