using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;

namespace FuGetGallery
{
    public class PackageAssemblyXmlDocs
    {
        readonly string assemblyName;

        readonly Dictionary<string, PackageAssemblyXmlLanguageDocs> languages =
            new Dictionary<string, PackageAssemblyXmlLanguageDocs> ();

        public PackageAssemblyXmlDocs (string assemblyName)
        {
            this.assemblyName = assemblyName;
        }

        public PackageAssemblyXmlLanguageDocs GetLanguage (string languageCode)
        {
            if (languages.TryGetValue (languageCode.ToLowerInvariant (), out var docs))
                return docs;
            if (languages.TryGetValue ("en-us", out docs))
                return docs;
            if (languages.TryGetValue ("en", out docs))
                return docs;
            return new PackageAssemblyXmlLanguageDocs (assemblyName, languageCode);
        }

        public void AddLanguage (string languageCode, PackageAssemblyXmlLanguageDocs docs)
        {
            languages[languageCode.ToLowerInvariant ()] = docs;
        }
    }

    public class PackageAssemblyXmlLanguageDocs
    {
        XDocument doc;

        Entry entry;

        public string LanguageCode { get; private set; }

        public string Error { get; private set; }

        public string AssemblyName { get; private set; }

        Dictionary<string, MemberXmlDocs> members;

        public Dictionary<string, MemberXmlDocs> MemberDocs { 
            get {
                // lazily load members to avoid accessing the stream
                // unless we need it
                if (members == null) {
                    members = new Dictionary<string, MemberXmlDocs>();
                    if (entry != null) {
                        LoadMembers ();
                    }
                }
                return members;
            }
        }

        public PackageAssemblyXmlLanguageDocs (string assemblyName, string languageCode)
        {
            LanguageCode = languageCode;
            Error = "";
            AssemblyName = assemblyName;
        }

        static readonly char[] pathSplitChars = new[] { '/', '\\' };

        public PackageAssemblyXmlLanguageDocs (Entry entry)
        {
            this.entry = entry;
            LanguageCode = "en";

            try {
                var path = entry.FullName;
                var pathParts = path.Split (pathSplitChars, StringSplitOptions.RemoveEmptyEntries);
                // lib/framework/language/file.xml
                // 0   1         2        3
                if (pathParts.Length == 4) {
                    LanguageCode = pathParts[2];
                }
                AssemblyName = Path.GetFileNameWithoutExtension (pathParts[pathParts.Length - 1]);
            }
            catch (Exception ex) {
                Console.WriteLine (ex);
            }
        }

        void LoadMembers ()
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
                            members[m.Name] = m;
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
