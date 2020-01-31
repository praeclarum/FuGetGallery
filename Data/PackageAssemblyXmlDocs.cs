using System;
using System.Collections.Generic;
using System.IO.Compression;
using System.Linq;
using System.Xml.Linq;

namespace FuGetGallery
{
    public class PackageAssemblyXmlDocs
    {
        readonly XDocument doc;

        public string Error { get; }

        public string AssemblyName { get; }

        public Dictionary<string, MemberXmlDocs> MemberDocs { get; } = new Dictionary<string, MemberXmlDocs> ();

        public PackageAssemblyXmlDocs (ZipArchiveEntry entry)
        {
            try {
                using (var s = entry.Open ()) {
                    doc = XDocument.Load (s);
                    var ns = doc.Root.Name.Namespace;
                    var asm = doc.Root.Element (ns + "assembly");
                    if (asm == null) {
                        Error = "Not an docs files";
                        return;
                    }
                    AssemblyName = asm.Value.Trim();
                    // System.Console.WriteLine(AssemblyName);
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
                // System.Console.WriteLine (ex);
                // if (doc != null) System.Console.WriteLine(doc);
                Error = ex.Message;
            }
        }
    }

    public class MemberXmlDocs
    {
        public string Name { get; }
        public XElement SummaryXml { get; }
        public Dictionary<string, string> ParametersText { get; }
        public Dictionary<string, string> TypeParametersText { get; }

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
            ParametersText = memberElement.Elements (ns + "param").ToDictionary(
                e => e.Attribute(ns + "name")?.Value ?? string.Empty,
                e => GetElementText(e));
            TypeParametersText = memberElement.Elements (ns + "typeparam").ToDictionary (
                e => e.Attribute (ns + "name")?.Value ?? string.Empty,
                e => GetElementText(e));
            // System.Console.WriteLine(SummaryXml.ToString());
        }

        internal static string GetElementText(XElement element)
        {
            return string.Concat (element.DescendantNodes ().Select (n => {
                if (n is XElement el) {
                    if (el.Name == "see") {
                        var cref = el.Attribute (element.Name.Namespace + "cref")?.Value;
                        if (cref != null) {
                            return cref.Substring (cref.LastIndexOf ('.') + 1);
                        }
                        var langword = el.Attribute (element.Name.Namespace + "langword")?.Value;
                        if (langword != null) {
                            return langword;
                        }
                    }
                    return string.Empty;
                }
                return n.ToString ();
            }));
        }
    }
}
