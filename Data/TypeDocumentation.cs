using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading.Tasks;
using ICSharpCode.Decompiler.CSharp.Syntax;
using Mono.Cecil;
using ICSharpCode.Decompiler.CSharp.Syntax;
using ICSharpCode.Decompiler.TypeSystem;
using ICSharpCode.Decompiler.Semantics;

namespace FuGetGallery
{
    public class TypeDocumentation
    {
        TypeDefinition typeDefinition;
        public TypeDefinition Definition => typeDefinition;

        readonly Lazy<ICSharpCode.Decompiler.CSharp.CSharpDecompiler> decompiler;
        readonly Lazy<ICSharpCode.Decompiler.CSharp.CSharpDecompiler> idecompiler;
        readonly ICSharpCode.Decompiler.CSharp.OutputVisitor.CSharpFormattingOptions format;

        public string SummaryText { get; }

        public TypeDocumentation (TypeDefinition typeDefinition, PackageAssemblyXmlDocs xmlDocs,
            Lazy<ICSharpCode.Decompiler.CSharp.CSharpDecompiler> decompiler, Lazy<ICSharpCode.Decompiler.CSharp.CSharpDecompiler> idecompiler,
            ICSharpCode.Decompiler.CSharp.OutputVisitor.CSharpFormattingOptions format)
        {
            this.typeDefinition = typeDefinition;
            this.decompiler = decompiler;
            this.idecompiler = idecompiler;
            this.format = format;

            format = ICSharpCode.Decompiler.CSharp.OutputVisitor.FormattingOptionsFactory.CreateMono ();
            format.SpaceBeforeMethodCallParentheses = false;
            format.SpaceBeforeMethodDeclarationParentheses = false;
            format.SpaceBeforeConstructorDeclarationParentheses = false;

            SummaryText = "";

            if (xmlDocs != null) {
                var tn = GetXmlName (typeDefinition);
                if (xmlDocs.MemberDocs.TryGetValue (tn, out var td)) {
                    SummaryText = td.SummaryText.Trim ();
                    if (SummaryText == "To be added.")
                        SummaryText = "";
                }
            }
        }

        string GetXmlName (TypeDefinition d)
        {
            return "T:" + d.FullName;
        }

        public Task<string> GetTypeCodeAsync (TypeDefinition type)
        {
            return Task.Run (() => {
                try {
                    var d = decompiler.Value;
                    if (d == null)
                        return "// No decompiler available";
                    return d.DecompileTypeAsString (new ICSharpCode.Decompiler.TypeSystem.FullTypeName (type.FullName));
                }
                catch (Exception e) {
                    return "/* " + e.Message + " */";
                }
            });
        }

        public Task<string> GetTypeInterfaceCodeAsync (TypeDefinition type)
        {
            return Task.Run (() => {
                try {
                    var d = idecompiler.Value;
                    if (d == null)
                        return "// No decompiler available";
                    var syntaxTree = d.DecompileType (new ICSharpCode.Decompiler.TypeSystem.FullTypeName (type.FullName));
                    StringWriter w = new StringWriter();
                    syntaxTree.AcceptVisitor(new RemoveNonInterfaceSyntaxVisitor { StartTypeName = type.Name });
                    syntaxTree.AcceptVisitor(new ICSharpCode.Decompiler.CSharp.OutputVisitor.CSharpOutputVisitor(w, format));
                    return w.ToString();
                }
                catch (Exception e) {
                    return "/* " + e.Message + " */";
                }
            });
        }

        class RemoveNonInterfaceSyntaxVisitor : DepthFirstAstVisitor
        {
            public string StartTypeName;
            readonly HashSet<string> skipAttrs = new HashSet<string> {
                "CLSCompliant",
                "CompilerGenerated",
                "EditorBrowsable",
            };
            void RemoveAttributes (IEnumerable<ICSharpCode.Decompiler.CSharp.Syntax.AttributeSection> attrs)
            {
                foreach (var s in attrs.ToList ()) {
                    var toRemove = s.Attributes.Where (x => x.Type is SimpleType t && skipAttrs.Contains(t.Identifier)).ToList();
                    foreach (var a in toRemove) {
                        a.Remove();
                    }
                    if (s.Children.Count() == 0)
                        s.Remove();
                }
            }
            public override void VisitMethodDeclaration(MethodDeclaration d)
            {
                if (d.Modifiers.HasFlag(Modifiers.Private) || d.Modifiers.HasFlag(Modifiers.Internal)
                    || d.Modifiers.HasFlag(Modifiers.Override)
                    || d.PrivateImplementationType != AstType.Null
                    ) {
                    d.Remove();
                }
                else {
                    RemoveAttributes (d.Attributes);
                    base.VisitMethodDeclaration(d);
                }
            }
            public override void VisitConstructorDeclaration(ConstructorDeclaration d)
            {
                if (d.Modifiers.HasFlag(Modifiers.Private) || d.Modifiers.HasFlag(Modifiers.Static) || d.Modifiers.HasFlag(Modifiers.Internal)) {
                    d.Remove();
                }
                else {
                    RemoveAttributes (d.Attributes);
                    base.VisitConstructorDeclaration(d);
                }
            }
            public override void VisitFieldDeclaration(FieldDeclaration d)
            {
                if (d.Modifiers.HasFlag(Modifiers.Private) || d.Modifiers.HasFlag(Modifiers.Internal)) {
                    d.Remove();
                }
                else {
                    RemoveAttributes (d.Attributes);
                    base.VisitFieldDeclaration(d);
                }
            }
            public override void VisitPropertyDeclaration(PropertyDeclaration d)
            {
                if (d.Modifiers.HasFlag(Modifiers.Private) || d.Modifiers.HasFlag(Modifiers.Internal)
                    || d.PrivateImplementationType != AstType.Null) {
                    d.Remove();
                }
                else {
                    RemoveAttributes (d.Attributes);
                    if (d.Getter != null) {
                        if (d.Getter.Modifiers.HasFlag(Modifiers.Private) || d.Getter.Modifiers.HasFlag(Modifiers.Internal)) {
                            d.Getter.Remove();
                        }
                        else {
                            RemoveAttributes (d.Getter.Attributes);
                        }
                    }
                    if (d.Setter != null) {
                        if (d.Setter.Modifiers.HasFlag(Modifiers.Private) || d.Setter.Modifiers.HasFlag(Modifiers.Internal)) {
                            d.Setter.Remove();
                        }
                        else {
                            RemoveAttributes (d.Setter.Attributes);
                        }
                    }
                    base.VisitPropertyDeclaration(d);
                }
            }
            public override void VisitEventDeclaration(EventDeclaration d)
            {
                if (d.Modifiers.HasFlag(Modifiers.Private) || d.Modifiers.HasFlag(Modifiers.Internal)) {
                    d.Remove();
                }
                else {
                    RemoveAttributes (d.Attributes);
                    base.VisitEventDeclaration(d);
                }
            }
            public override void VisitTypeDeclaration(TypeDeclaration d)
            {
                if (d.Name != StartTypeName &&
                    (d.Modifiers.HasFlag(Modifiers.Private))) {
                    d.Remove();
                }
                else {
                    RemoveAttributes (d.Attributes);
                    base.VisitTypeDeclaration(d);
                }
            }
        }
    }
}
