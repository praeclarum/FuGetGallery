using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading.Tasks;
using System.Text.RegularExpressions;
using ICSharpCode.Decompiler.CSharp.Syntax;
using Mono.Cecil;
using ICSharpCode.Decompiler.TypeSystem;
using ICSharpCode.Decompiler.Semantics;
using ICSharpCode.Decompiler.CSharp.TypeSystem;
using ICSharpCode.Decompiler.CSharp;
using ICSharpCode.Decompiler.IL;
using ICSharpCode.Decompiler.CSharp.Resolver;
using ICSharpCode.Decompiler.CSharp.OutputVisitor;
using System.Xml.Linq;

namespace FuGetGallery
{
    public class TypeDocumentation
    {
        PackageTargetFramework framework;
        TypeDefinition typeDefinition;
        public TypeDefinition Definition => typeDefinition;

        readonly Lazy<ICSharpCode.Decompiler.CSharp.CSharpDecompiler> decompiler;
        readonly Lazy<ICSharpCode.Decompiler.CSharp.CSharpDecompiler> idecompiler;
        readonly ICSharpCode.Decompiler.CSharp.OutputVisitor.CSharpFormattingOptions format;

        public string SummaryText { get; }
        public string SummaryHtml { get; }
        public string DocumentationHtml { get; }

        readonly PackageAssemblyXmlDocs xmlDocs;

        public TypeDocumentation (TypeDefinition typeDefinition, PackageTargetFramework framework, PackageAssemblyXmlDocs xmlDocs,
            Lazy<ICSharpCode.Decompiler.CSharp.CSharpDecompiler> decompiler, Lazy<ICSharpCode.Decompiler.CSharp.CSharpDecompiler> idecompiler,
            ICSharpCode.Decompiler.CSharp.OutputVisitor.CSharpFormattingOptions format)
        {
            this.xmlDocs = xmlDocs;
            this.typeDefinition = typeDefinition;
            this.framework = framework;
            this.decompiler = decompiler;
            this.idecompiler = idecompiler;
            this.format = format;

            SummaryHtml = "";
            SummaryText = "";
            DocumentationHtml = "";

            if (xmlDocs != null) {
                var tn = typeDefinition.GetXmlName ();
                if (xmlDocs.MemberDocs.TryGetValue (tn, out var td)) {                    
                    SummaryHtml = XmlToHtml (td.SummaryXml);
                    SummaryText = XmlToText (td.SummaryXml);
                    if (SummaryHtml == "To be added.")
                        SummaryHtml = "";
                }
            }

            var w = new StringWriter ();
            WriteDocumentation (w);
            DocumentationHtml = w.ToString ();
        }

        void WriteDocumentation (TextWriter w)
        {
            var members = typeDefinition.GetPublicMembers ();
            foreach (var m in members) {

                var xmlName = m.GetXmlName ();
                MemberXmlDocs docs = null;
                xmlDocs?.MemberDocs.TryGetValue (xmlName, out docs);

                w.WriteLine ("<div class='member-code'>");
                m.WritePrototypeHtml (w, framework: framework);
                w.WriteLine ("</div>");

                if (docs != null) {
                    w.WriteLine ("<p>");
                    XmlToHtml (docs?.SummaryXml, w);
                    w.WriteLine ("</p>");
                }
                else {
                    w.WriteLine ("<p><b>");
                    WriteEncodedHtml (xmlName, w);
                    w.WriteLine ("</b></p>");
                }
            }
        }

        void XmlToText (XElement x, TextWriter w)
        {
            if (x == null) return;
            w.Write (x.Value.Trim ());
        }

        string XmlToText (XElement x)
        {
            using (var w = new StringWriter ()) {
                XmlToText (x, w);
                return w.ToString ();
            }
        }

        void XmlToHtml (XElement x, TextWriter w)
        {
            if (x == null) return;
            WriteEncodedHtml (x.Value.Trim (), w);
        }

        string XmlToHtml (XElement x)
        {
            using (var w = new StringWriter ()) {
                XmlToHtml (x, w);
                return w.ToString ();
            }
        }

        public Task<string> GetTypeCodeAsync ()
        {
            return Task.Run (() => {
                try {
                    var d = decompiler.Value;
                    if (d == null)
                        return "// No decompiler available";
                    var syntaxTree = d.DecompileType (new ICSharpCode.Decompiler.TypeSystem.FullTypeName (typeDefinition.FullName));
                    var w = new HtmlWriter (new StringWriter(), framework);
                    w.Writer.Write("<div class=\"code\">");
                    syntaxTree.AcceptVisitor(new ICSharpCode.Decompiler.CSharp.OutputVisitor.CSharpOutputVisitor(w, format));
                    w.Writer.Write("</div>");
                    return PostProcessCode (w.Writer.ToString());
                }
                catch (Exception e) {
                    return "/* " + e.Message + " */";
                }
            });
        }

        public Task<string> GetTypeInterfaceCodeAsync ()
        {
            return Task.Run (() => {
                try {
                    var d = idecompiler.Value;
                    if (d == null)
                        return "// No decompiler available";
                    var syntaxTree = d.DecompileType (new ICSharpCode.Decompiler.TypeSystem.FullTypeName (typeDefinition.FullName));
                    syntaxTree.AcceptVisitor(new RemoveNonInterfaceSyntaxVisitor { StartTypeName = typeDefinition.Name });
                    var w = new HtmlWriter (new StringWriter(), framework);
                    w.Writer.Write("<div class=\"code\">");
                    syntaxTree.AcceptVisitor(new ICSharpCode.Decompiler.CSharp.OutputVisitor.CSharpOutputVisitor(w, format));
                    w.Writer.Write("</div>");
                    return PostProcessCode (w.Writer.ToString());
                }
                catch (Exception e) {
                    return "/* " + e + " */";
                }
            });
        }

        public static void WriteEncodedHtml (string s, TextWriter w)
        {
            if (s == null) return;
            for (var i = 0; i < s.Length; i++) {
                switch (s[i]) {
                    case '&': w.Write ("&amp;"); break;
                    case '<': w.Write ("&lt;"); break;
                    case '>': w.Write ("&gt;"); break;
                    case var c when (c < ' '):
                        w.Write ("&#");
                        w.Write ((int)c);
                        break;
                    default: w.Write (s[i]); break;
                }
            }
        }

        static readonly Regex reGS = new Regex (@"{.*\n.*get</span>;.*\n.*set</span>;.*\n.*}", RegexOptions.Compiled | RegexOptions.Multiline);
        static readonly Regex reG = new Regex (@"{.*\n.*get</span>;.*\n.*}", RegexOptions.Compiled | RegexOptions.Multiline);
        static readonly Regex reAR = new Regex (@" {.*\n.*add</span>;.*\n.*remove</span>;.*\n.*}", RegexOptions.Compiled | RegexOptions.Multiline);

        string PostProcessCode (string code)
        {
            code = reGS.Replace (code, "{ get; set; }");
            code = reG.Replace (code, "{ get; }");
            code = reAR.Replace (code, ";");
            return code;
        }

        class HtmlWriter : ICSharpCode.Decompiler.CSharp.OutputVisitor.TokenWriter
        {
            readonly PackageTargetFramework framework;
            readonly TextWriter w;
            public TextWriter Writer => w;


            bool needsIndent = true;
            int indentLevel = 0;

            void WriteEncoded (string s) => WriteEncodedHtml (s, w);

            string GetClassAndLink(AstNode n, out string link)
            {
                link = null;
                if (n == null || n == AstNode.Null)
                    return "c-uk";
                while (n != null && n.Annotations.Count() == 0)
                    n = n.Parent;
                if (n == null || n == AstNode.Null)
                    return "c-uk";
                var t = n.Annotation<TypeResolveResult> ();
                if (t != null) {
                    if (n.NodeType == NodeType.TypeDeclaration) {
                        return "c-td";
                    }
                    var name = t.Type.FullName;
                    if (t.Type.TypeParameterCount > 0 && name.IndexOf('`') < 0)
                        name += "`" + t.Type.TypeParameterCount;
                    if (t.Type.Kind != TypeKind.TypeParameter) {
                        link = framework.FindTypeUrl (name);
                    }
                    return "c-tr";
                }
                var u = n.Annotation<UsingScope> ();
                if (u != null)
                    return "c-nr";
                var m = n.Annotation<MemberResolveResult> ();
                if (m != null) {
                    if (m.Member.SymbolKind == SymbolKind.Method) {
                        if (n is MethodDeclaration)
                            return "c-md";
                        return "c-mr";
                    }
                    if (m.Member.SymbolKind == SymbolKind.Field) {
                        if (n is FieldDeclaration)
                            return "c-fd";
                        return "c-fr";
                    }
                    if (m.Member.SymbolKind == SymbolKind.Event) {
                        if (n is EventDeclaration || n is CustomEventDeclaration)
                            return "c-ed";
                        return "c-er";
                    }
                    if (m.Member.SymbolKind == SymbolKind.Constructor) {
                        if (n is ConstructorDeclaration)
                            return "c-cd";
                        return "c-cr";
                    }
                    if (n is PropertyDeclaration)
                        return "c-pd";
                    return "c-pr";
                }
                var mg = n.Annotation<MethodGroupResolveResult> ();
                if (mg != null)
                    return "c-mr";
                var v = n.Annotation<ILVariableResolveResult> ();
                if (v != null) {
                    if (v.Variable.Kind == VariableKind.Parameter)
                        return "c-ar";
                    return "c-uk";
                }
                var c = n.Annotation<ConstantResolveResult> ();
                if (c != null) {
                    return "c-fr";
                }

                // Console.WriteLine(n.Annotations.FirstOrDefault());
                return "c-uk";
            }
            void WriteIndent()
            {
                if (!needsIndent) return;
                needsIndent = false;
                w.Write(new String(' ', indentLevel * 4));
            }
            public HtmlWriter(TextWriter w, PackageTargetFramework framework)
            {
                this.w = w;
                this.framework = framework;
            }
            public override void StartNode(AstNode node)
            {
            }

            public override void EndNode(AstNode node)
            {
            }

            public override void Indent()
            {
                indentLevel++;                
            }

            public override void Space()
            {
                WriteIndent ();
                w.Write(" ");
            }

            public override void NewLine()
            {
                w.WriteLine();
                needsIndent = true;
            }

            public override void Unindent()
            {
                indentLevel--;
            }

            public override void WriteComment(CommentType commentType, string content)
            {
            }

            public override void WriteIdentifier(Identifier identifier)
            {
                WriteIndent ();
                var c = GetClassAndLink(identifier, out var link);
                if (link != null)  {
                    w.Write("<a class=\"");
                    w.Write(c);
                    w.Write("\" href=\"");
                    WriteEncoded(link);
                    w.Write("\">");
                    WriteEncoded(identifier.Name);
                    w.Write("</a>");
                }
                else {
                    w.Write("<span class=\"");
                    w.Write(c);
                    w.Write("\">");
                    WriteEncoded(identifier.Name);
                    w.Write("</span>");
                }
            }

            public override void WriteKeyword(Role role, string keyword)
            {
                WriteIndent ();
                w.Write("<span class=\"c-kw\">");
                WriteEncoded(keyword);
                w.Write("</span>");
            }

            public override void WritePreProcessorDirective(PreProcessorDirectiveType type, string argument)
            {
            }

            public override void WritePrimitiveType(string type)
            {
                WriteIndent ();
                w.Write("<span class=\"c-tr\">");
                w.Write(type);
                w.Write("</span>");
            }

            public override void WritePrimitiveValue(object value, string literalValue = null)
            {
                WriteIndent ();
                if (value == null) {
                    w.Write("<span class=\"c-nl\">null</span>");
                    return;
                }
                if (value is bool b) {
                    w.Write(b ? "<span class=\"c-nl\">true</span>" : "<span class=\"c-nl\">false</span>");
                    return;
                }
                if (value is string s) {
                    w.Write("<span class=\"c-st\">\"");
                    for (var i = 0; i < s.Length; i++) {
                        switch (s[i]) {
                            case '&': w.Write("&amp;"); break;
                            case '<': w.Write("&lt;"); break;
                            case '>': w.Write("&gt;"); break;
                            case '\\': w.Write("\\\\"); break;
                            case '\"': w.Write("\\\""); break;
                            case '\n': w.Write("\\n"); break;
                            case '\r': w.Write("\\r"); break;
                            case '\b': w.Write("\\b"); break;
                            case '\t': w.Write("\\t"); break;
                            default: w.Write(s[i]); break;
                        }
                    }
                    w.Write("\"</span>");
                    return;
                }
                if (value is char c) {
                    w.Write("<span class=\"c-st\">\'");
                    switch (c) {
                        case '&': w.Write("&amp;"); break;
                        case '<': w.Write("&lt;"); break;
                        case '>': w.Write("&gt;"); break;
                        case '\\': w.Write("\\\\"); break;
                        case '\'': w.Write("\\\'"); break;
                        case '\n': w.Write("\\n"); break;
                        case '\r': w.Write("\\r"); break;
                        case '\b': w.Write("\\b"); break;
                        case '\t': w.Write("\\t"); break;
                        default: w.Write(c); break;
                    }
                    w.Write("\'</span>");
                    return;                    
                }
                w.Write("<span class=\"c-nu\">");
                WriteEncoded(Convert.ToString (value, System.Globalization.CultureInfo.InvariantCulture));
                w.Write("</span>");
            }

            public override void WriteToken(Role role, string token)
            {
                WriteIndent ();
                w.Write(token);
            }
        }

        class RemoveNonInterfaceSyntaxVisitor : DepthFirstAstVisitor
        {
            public string StartTypeName;
            void RemoveAttributes (IEnumerable<ICSharpCode.Decompiler.CSharp.Syntax.AttributeSection> attrs)
            {
                if (attrs == null)
                    return;
                foreach (var s in attrs.ToList ()) {
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
                    || d.Modifiers.HasFlag(Modifiers.Override)
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
                if (d.Modifiers.HasFlag(Modifiers.Private) || d.Modifiers.HasFlag(Modifiers.Internal)
                    || d.Modifiers.HasFlag(Modifiers.Override)) {
                    d.Remove();
                }
                else {
                    RemoveAttributes (d.Attributes);
                    base.VisitEventDeclaration(d);
                }
            }
            public override void VisitCustomEventDeclaration(CustomEventDeclaration d)
            {
                if (d.Modifiers.HasFlag(Modifiers.Private) || d.Modifiers.HasFlag(Modifiers.Internal)) {
                    d.Remove();
                }
                else {
                    RemoveAttributes (d.Attributes);
                    RemoveAttributes (d.AddAccessor?.Attributes);
                    RemoveAttributes (d.RemoveAccessor?.Attributes);
                    base.VisitCustomEventDeclaration(d);
                }
            }
            public override void VisitTypeDeclaration(TypeDeclaration d)
            {
                if (d.Name != StartTypeName &&
                    (d.Modifiers.HasFlag(Modifiers.Private) || d.Modifiers.HasFlag(Modifiers.Internal))) {
                    d.Remove();
                }
                else {
                    RemoveAttributes (d.Attributes);
                    base.VisitTypeDeclaration(d);
                }
            }

            public override void VisitParameterDeclaration(ParameterDeclaration d)
            {
                RemoveAttributes (d.Attributes);
                base.VisitParameterDeclaration(d);
            }

            public override void VisitUsingDeclaration(UsingDeclaration d)
            {
                d.Remove();
            }
        }
    }
}
