using System;
using System.IO;
using System.IO.Compression;
using Mono.Cecil;

namespace FuGetGallery
{
    public class TypeDocumentation
    {
        TypeDefinition typeDefinition;
        public TypeDefinition Definition => typeDefinition;

        public string SummaryText { get; }

        public TypeDocumentation (TypeDefinition typeDefinition, PackageAssemblyXmlDocs xmlDocs)
        {
            this.typeDefinition = typeDefinition;

            SummaryText = "";

            if (xmlDocs != null) {
                var tn = GetXmlName (typeDefinition);
                if (xmlDocs.MemberDocs.TryGetValue (tn, out var td)) {
                    SummaryText = td.SummaryText;
                    if (SummaryText == "To be added.")
                        SummaryText = "";
                }
            }
        }

        string GetXmlName (TypeDefinition d)
        {
            return "T:" + d.FullName;
        }
    }
}
