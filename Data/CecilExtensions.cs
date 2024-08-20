using Mono.Cecil;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System;
using System.Text;
using Mono.Collections.Generic;

namespace FuGetGallery
{
    public static class CecilExtensions
    {
        public static bool IsDelegate (this TypeDefinition type)
        {
            if (type.BaseType != null && type.BaseType.Namespace == "System") {
                if (type.BaseType.Name == "MulticastDelegate")
                    return true;
                if (type.BaseType.Name == "Delegate" && type.Name != "MulticastDelegate")
                    return true;
            }
            return false;
        }

        public static int GetSortOrder (this IMemberDefinition member)
        {
            switch (member) {
                case FieldDefinition t:
                    if (t.IsStatic) return 100;
                    return 200;
                case MethodDefinition t when t.IsConstructor:
                    if (t.IsStatic) return 700;
                    return 800;
                case MethodDefinition t:
                    if (t.IsStatic) return 900;
                    return 1000;
                case PropertyDefinition t:
                    if (t.GetMethod != null && t.GetMethod.IsStatic) return 300;
                    return 400;
                case EventDefinition t:
                    if (t.AddMethod != null && t.AddMethod.IsStatic) return 500;
                    return 600;
                case TypeDefinition t:
                    return 10;
                default: throw new NotSupportedException (member.GetType () + " " + member.FullName);
            }
        }

        public static IEnumerable<IMemberDefinition> GetPublicMembers (this TypeDefinition type)
        {
            var fields = (type.IsEnum ? type.Fields.Where (x => x.IsPublic && x.IsStatic).Cast<IMemberDefinition> () :
                          type.Fields.Where(x => (x.IsPublic || x.IsFamily)).Cast<IMemberDefinition> ());
            var methods = type.Methods.Where (x => (x.IsPublic || x.IsFamily)
                                              && !(x.IsAddOn || x.IsRemoveOn || x.IsGetter || x.IsSetter)
                                              && !(x.IsVirtual && !x.IsAbstract && x.IsReuseSlot)).Cast<IMemberDefinition> ();
            var properties = type.Properties.Where (x => x.GetMethod != null
                                                    && (x.GetMethod.IsPublic || x.GetMethod.IsFamily)
                                                    && !(x.GetMethod.IsVirtual && !x.GetMethod.IsAbstract && x.GetMethod.IsReuseSlot)).Cast<IMemberDefinition> ();
            var events = type.Events.Where (x => x.AddMethod != null && (x.AddMethod.IsPublic || x.AddMethod.IsFamily)).Cast<IMemberDefinition> ();
            var types = type.NestedTypes.Where (x => (x.IsPublic || x.IsNestedFamily || x.IsNestedPublic)).Cast<IMemberDefinition> ();
            return fields.Concat (properties).Concat (events).Concat (methods).Concat (types).OrderBy (GetSortOrder).ThenBy (x => x.Name);
        }

        public static string GetXmlName (this IMemberDefinition member)
        {
            return Mono.Cecil.Rocks.DocCommentId.GetDocCommentId (member);
        }

        static void WriteEncoded (string s, TextWriter w)
        {
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

        public static string GetFriendlyName (this TypeDefinition type)
        {
            if (type.HasGenericParameters) {
                var name = type.Name;
                var ni = name.LastIndexOf ('`');
                if (ni > 0) name = name.Substring (0, ni);
                var b = new StringBuilder (name);
                b.Append ("<");
                var head = "";
                foreach (var p in type.GenericParameters) {
                    b.Append (head);
                    b.Append (p.Name);
                    head = ", ";
                }
                b.Append (">");
                return b.ToString ();
            }
            return type.Name;
        }

        public static string GetFriendlyFullName (this TypeDefinition type)
        {
            if (type.IsNested) {
                var d = type.DeclaringType.GetFriendlyFullName ();
                var n = type.GetFriendlyName ();
                return d + "." + n;
            }
            return type.Namespace + "." + type.GetFriendlyName ();
        }

        public static string GetUrl (this IMemberDefinition member, PackageTargetFramework framework, bool linkToCode)
        {
            var url = framework.FindTypeUrl (member.DeclaringType);
            if (string.IsNullOrEmpty (url)) return "";
            if (linkToCode && framework.Package.AllowedToDecompile) url += "?code=true";
            if (member is TypeDefinition t && !t.IsNested) return url;
            return url + "#" + Uri.EscapeDataString (member.GetXmlName ());
        }

        static string GetHref (IMemberDefinition member, PackageTargetFramework framework, bool linkToCode)
        {
            var url = GetUrl (member, framework, linkToCode);
            if (string.IsNullOrEmpty (url)) return "";
            return "href=\"" + url + "\"";
        }

        public static void WriteReferenceHtml(this TypeReference type, TextWriter w, PackageTargetFramework framework, bool isOut = false, bool isExtension = false)
        {
            if (isExtension) {
                w.Write ("<span class=\"c-kw\">this</span> ");
            }

            if (type.FullName == "System.Void") {
                w.Write ("<span class=\"c-tr\">void</span>");
            }
            else if (type.FullName == "System.String") {
                w.Write ("<span class=\"c-tr\">string</span>");
            }
            else if (type.FullName == "System.Object") {
                w.Write ("<span class=\"c-tr\">object</span>");
            }
            else if (type.FullName == "System.Decimal") {
                w.Write ("<span class=\"c-tr\">decimal</span>");
            }
            else if (type.IsPrimitive) {
                w.Write ("<span class=\"c-tr\">");
                switch (type.FullName) {
                    case "System.Byte": w.Write ("byte"); break;
                    case "System.Boolean": w.Write ("bool"); break;
                    case "System.Char": w.Write ("char"); break;
                    case "System.Double": w.Write ("double"); break;
                    case "System.Int16": w.Write ("short"); break;
                    case "System.Int32": w.Write ("int"); break;
                    case "System.Int64": w.Write ("long"); break;
                    case "System.Single": w.Write ("float"); break;
                    case "System.SByte": w.Write ("sbyte"); break;
                    case "System.UInt16": w.Write ("ushort"); break;
                    case "System.UInt32": w.Write ("uint"); break;
                    case "System.UInt64": w.Write ("ulong"); break;
                    default: WriteEncoded (type.Name, w); break;
                }
                w.Write ("</span>");
            }
            else if (type.IsArray) {
                var at = (ArrayType)type;
                WriteReferenceHtml (at.ElementType, w, framework);
                w.Write ("[");
                var head = "";
                foreach (var d in at.Dimensions) {
                    w.Write (head);
                    head = ",";
                }
                w.Write ("]");
            }
            else if (type.IsGenericInstance) {
                GenericInstanceType gi = (GenericInstanceType)type;
                if (gi.ElementType.FullName == "System.Nullable`1") {
                    WriteReferenceHtml (gi.GenericArguments[0], w, framework);
                    w.Write ("?");
                }
                else {
                    WriteReferenceHtml (gi.ElementType, w, framework);
                    w.Write ("&lt;");
                    var head = "";
                    foreach (var a in gi.GenericArguments) {
                        w.Write (head);
                        WriteReferenceHtml (a, w, framework);
                        head = ", ";
                    }
                    w.Write ("&gt;");
                }
            }
            else if (type.IsByReference) {
                if (isOut) {
                    w.Write("<span class=\"c-kw\">out</span> "); 
                } else {
                    w.Write("<span class=\"c-kw\">ref</span> ");
                }
                WriteReferenceHtml (type.GetElementType (), w, framework);
            }
            else {
                var name = type.Name;
                var ni = name.LastIndexOf ('`');
                if (ni > 0) name = name.Substring (0, ni);
                var url = framework?.FindTypeUrl (type);
                if (url != null) {
                    w.Write ($"<a href=\"{url}\" class=\"c-tr\">");
                    WriteEncoded (name, w);
                    w.Write ("</a>");
                }
                else {
                    w.Write ("<span class=\"c-tr\">");
                    WriteEncoded (name, w);
                    w.Write ("</span>");
                }
            }
        }

        public static void WritePrototypeHtml (this FieldDefinition member, TextWriter w, PackageTargetFramework framework, bool linkToCode)
        {
            if (!member.DeclaringType.IsEnum) {
                if (member.IsFamily || member.IsFamilyOrAssembly) {
                    w.Write ("<span class=\"c-kw\">protected</span> ");
                }
                else if (member.IsPublic) {
                    w.Write ("<span class=\"c-kw\">public</span> ");
                }

                if (member.HasConstant) {
                    w.Write ("<span class=\"c-kw\">const</span> ");
                }
                else {
                    if (member.IsStatic) {
                        w.Write ("<span class=\"c-kw\">static</span> ");
                    }
                    if (member.IsInitOnly) {
                        w.Write ("<span class=\"c-kw\">readonly</span> ");
                    }
                }
                WriteReferenceHtml (member.FieldType, w, framework);
                w.Write (" ");
            }
            var id = member.GetXmlName ();
            var href = GetHref (member, framework, linkToCode);
            w.Write ($"<a {href} id=\"{id}\" class=\"c-fd\">");
            WriteEncoded (member.Name, w);
            w.Write ("</a>");
            if (member.HasConstant) {
                w.Write (" = ");
                TypeDocumentation.WritePrimitiveHtml (member.Constant, w);
            }
        }

        public static void WritePrototypeHtml (this MethodDefinition member, TextWriter w, PackageTargetFramework framework, MemberXmlDocs docs, bool linkToCode, bool inExtensionClass)
        {
            if (!member.DeclaringType.IsInterface) {
                if (member.IsFamily || member.IsFamilyOrAssembly) {
                    w.Write ("<span class=\"c-kw\">protected</span> ");
                }
                else if (member.IsPublic) {
                    w.Write ("<span class=\"c-kw\">public</span> ");
                }
                if (member.IsStatic) {
                    w.Write ("<span class=\"c-kw\">static</span> ");
                }
            }
            var id = member.GetXmlName ();
            var href = GetHref (member, framework, linkToCode);
            if (member.IsConstructor) {
                w.Write ($"<a {href} id=\"{id}\" class=\"c-cd\">");
                var name = member.DeclaringType.Name;
                var ni = name.LastIndexOf ('`');
                if (ni > 0) name = name.Substring (0, ni);
                WriteEncoded (name, w);
            }
            else {
                if (!member.DeclaringType.IsInterface) {
                    if (member.IsAbstract) {
                        w.Write ("<span class=\"c-kw\">abstract</span> ");
                    }
                    else if (member.IsVirtual) {
                        if (member.IsReuseSlot) {
                            w.Write ("<span class=\"c-kw\">override</span> ");
                        }
                        else if (!member.IsFinal) {
                            w.Write ("<span class=\"c-kw\">virtual</span> ");
                        }
                    }
                }
                WriteReferenceHtml (member.ReturnType, w, framework);
                w.Write ($" <a {href} id=\"{id}\" class=\"c-md\">");
                WriteEncoded (member.Name, w);
            }
            w.Write ("</a>");
            var head = "";
            if (member.HasGenericParameters) {
                WriteGenericParameterListHtml (member.GenericParameters, w, framework, docs);
            }
            w.Write ("(");
            head = "";

            bool isExtensionMethod = inExtensionClass && member.HasExtensionAttribute ();
            foreach (var p in member.Parameters) {
                w.Write (head);
                WriteReferenceHtml (p.ParameterType, w, framework, p.IsOut, isExtensionMethod);
                w.Write (" <span class=\"c-ar\" title=\"");
                WriteEncoded (docs != null && docs.ParametersText.TryGetValue (p.Name, out var paramText) ? paramText : string.Empty, w);
                w.Write ("\">");
                WriteEncoded (p.Name, w);
                w.Write ("</span>");
                if (p.HasConstant) {
                    w.Write (" = ");
                    var constant = p.Constant;
                    if (constant == null && p.ParameterType.IsValueType) {
                        w.Write ("<span class=\"c-nl\">default</span>");
                    }
                    else {
                        TypeDocumentation.WritePrimitiveHtml (constant, w);
                    }
                }
                head = ", ";

                isExtensionMethod = false;
            }
            w.Write (")");
            if (member.HasGenericParameters) {
                WriteGenericConstraintsHtml (member.GenericParameters, w, framework);
            }
        }

        private static void WriteGenericParameterListHtml (Collection<GenericParameter> genericParameters, TextWriter w, PackageTargetFramework framework, MemberXmlDocs docs)
        {
            w.Write ("&lt;");
            var head = "";
            foreach (var p in genericParameters) {
                w.Write (head);
                w.Write ("<span class=\"c-tr\" title=\"");
                WriteEncoded (docs != null && docs.TypeParametersText.TryGetValue (p.Name, out var paramText) ? paramText : string.Empty, w);
                w.Write ("\">");
                WriteEncoded (p.Name, w);
                w.Write ("</span>");
                head = ", ";
            }
            w.Write ("&gt;");
        }

        private static void WriteGenericConstraintsHtml (Collection<GenericParameter> genericParameters, TextWriter w, PackageTargetFramework framework)
        {
            foreach (var p in genericParameters) {
                if (p.HasConstraints) {
                    w.Write (" where ");
                    WriteReferenceHtml (p, w, framework);
                    w.Write (" : ");
                    var head = "";
                    foreach (var c in p.Constraints) {
                        if (c.ConstraintType.FullName == "System.Object") {
                            w.Write ("class");
                            head = ", ";
                            break;
                        }
                        else if (c.ConstraintType.FullName == "System.ValueType") {
                            w.Write ("struct");
                            head = ", ";
                            break;
                        }
                    }
                    foreach (var c in p.Constraints) {
                        if (c.ConstraintType.FullName != "System.ValueType" && c.ConstraintType.FullName != "System.Object") {
                            w.Write (head);
                            WriteReferenceHtml (c.ConstraintType, w, framework);
                            head = ", ";
                        }
                    }
                }
            }
        }

        public static bool HasExtensionAttribute (this ICustomAttributeProvider provider)
        {
            return provider.CustomAttributes.Any (x => x.AttributeType.Name == "ExtensionAttribute" && x.AttributeType.Namespace == "System.Runtime.CompilerServices");
        }

        public static bool IsExtensionClass (this TypeDefinition type)
        {
            return type.IsClass && type.IsSealed && type.IsAbstract // IsStatic
                && type.HasExtensionAttribute ();
        }

        public static void WritePrototypeHtml (this PropertyDefinition member, TextWriter w, PackageTargetFramework framework, MemberXmlDocs docs, bool linkToCode)
        {
            if (member.GetMethod != null && !member.DeclaringType.IsInterface) {
                if ((member.GetMethod.IsFamily || member.GetMethod.IsFamilyOrAssembly)) {
                    w.Write ("<span class=\"c-kw\">protected</span> ");
                }
                else if ((member.GetMethod.IsPublic)) {
                    w.Write ("<span class=\"c-kw\">public</span> ");
                }

                if (member.GetMethod.IsStatic) {
                    w.Write ("<span class=\"c-kw\">static</span> ");
                }
                else if (member.GetMethod.IsAbstract) {
                    w.Write ("<span class=\"c-kw\">abstract</span> ");
                }
                else if (member.GetMethod.IsVirtual) {
                    if (member.GetMethod.IsReuseSlot) {
                        w.Write ("<span class=\"c-kw\">override</span> ");
                    }
                    else if (!member.GetMethod.IsFinal) {
                        w.Write ("<span class=\"c-kw\">virtual</span> ");
                    }
                }
            }

            WriteReferenceHtml (member.PropertyType, w, framework);
            var id = member.GetXmlName ();
            var href = GetHref (member, framework, linkToCode);
            if (member.GetMethod != null && member.GetMethod.Parameters.Count > 0) {
                w.Write ($" <a {href} id=\"{id}\" class=\"c-cd\">this</a>[");
                var head = "";
                foreach (var p in member.GetMethod.Parameters) {
                    w.Write (head);
                    WriteReferenceHtml (p.ParameterType, w, framework);
                    w.Write (" <span class=\"c-ar\" title=\"");
                    WriteEncoded (docs != null && docs.ParametersText.TryGetValue (p.Name, out var paramText) ? paramText : string.Empty, w);
                    w.Write ("\">");
                    WriteEncoded (p.Name, w);
                    w.Write ("</span>");
                    head = ", ";
                }
                w.Write ("]");
            }
            else {
                w.Write ($" <a {href} id=\"{id}\" class=\"c-pd\">");
                WriteEncoded (member.Name, w);
                w.Write ("</a>");
            }
            w.Write (" {");
            if (member.GetMethod != null) w.Write (" <span class=\"c-kw\">get</span>;");
            if (member.SetMethod != null) {
                if (member.SetMethod.IsPublic) w.Write (" <span class=\"c-kw\">set</span>;");
                else if (member.SetMethod.IsFamily || member.SetMethod.IsFamilyOrAssembly) w.Write (" <span class=\"c-kw\">protected set</span>;");
            }
            w.Write (" }");
        }

        public static void WritePrototypeHtml (this EventDefinition member, TextWriter w, PackageTargetFramework framework, bool linkToCode)
        {
            if (!member.DeclaringType.IsInterface) {
                if (member.AddMethod != null && (member.AddMethod.IsFamily || member.AddMethod.IsFamilyOrAssembly)) {
                    w.Write ("<span class=\"c-kw\">protected</span> ");
                }
                else if (member.AddMethod != null && (member.AddMethod.IsPublic)) {
                    w.Write ("<span class=\"c-kw\">public</span> ");
                }

                if (member.AddMethod != null && member.AddMethod.IsStatic) {
                    w.Write ("<span class=\"c-kw\">static</span> ");
                }
            }
            w.Write ("<span class=\"c-kw\">event</span> ");
            WriteReferenceHtml (member.EventType, w, framework);
            var id = member.GetXmlName ();
            var href = GetHref (member, framework, linkToCode);
            w.Write ($" <a {href} id=\"{id}\" class=\"c-ed\">");
            WriteEncoded (member.Name, w);
            w.Write ("</a>");
        }

        public static void WritePrototypeHtml (this TypeDefinition member, TextWriter w, PackageTargetFramework framework, MemberXmlDocs docs, bool linkToCode)
        {
            if (member.IsNestedFamily || member.IsNestedFamilyOrAssembly) {
                w.Write ("<span class=\"c-kw\">protected</span> ");
            }
            else if (member.IsPublic || member.IsNestedPublic) {
                w.Write ("<span class=\"c-kw\">public</span> ");
            }

            if (member.IsSealed && member.IsAbstract)
                w.Write ("<span class=\"c-kw\">static</span> ");
            else if (member.IsSealed && !member.IsEnum && !member.IsValueType)
                w.Write ("<span class=\"c-kw\">sealed</span> ");
            else if (member.IsAbstract && !member.IsInterface)
                w.Write ("<span class=\"c-kw\">abstract</span> ");
            
            if (member.IsEnum)
                w.Write ("<span class=\"c-kw\">enum</span> ");
            else if (member.IsValueType)
                w.Write ("<span class=\"c-kw\">struct</span> ");
            else if (member.IsInterface)
                w.Write ("<span class=\"c-kw\">interface</span> ");
            else if (member.IsDelegate ())
                w.Write ("<span class=\"c-kw\">delegate</span> ");
            else
                w.Write ("<span class=\"c-kw\">class</span> ");

            var id = member.GetXmlName ();
            var name = member.Name;
            var ni = name.LastIndexOf ('`');
            if (ni > 0) name = name.Substring (0, ni);
            var url = framework?.FindTypeUrl (member);
            if (url != null) {
                w.Write ($"<a id=\"{id}\" href=\"{url}\" class=\"c-td\">");
                WriteEncoded (name, w);
                w.Write ("</a>");
            }
            else {
                w.Write ($"<span id=\"{id}\" class=\"c-td\">");
                WriteEncoded (name, w);
                w.Write ("</span>");
            }
            if (member.HasGenericParameters) {
                WriteGenericParameterListHtml (member.GenericParameters, w, framework, docs);
            }
            var hier = ((!member.IsEnum && !member.IsValueType && member.BaseType != null && member.BaseType.FullName != "System.Object") ? new[] { member.BaseType } : new TypeReference[0])
                .Concat (member.Interfaces.Select (x => x.InterfaceType)).ToList ();
            if (hier.Count > 0) {
                w.Write (" : ");
                var head = "";
                foreach (var h in hier) {
                    w.Write (head);
                    WriteReferenceHtml (h, w, framework);
                    head = ", ";
                }
            }
            if (member.HasGenericParameters) {
                WriteGenericConstraintsHtml (member.GenericParameters, w, framework);
            }
        }

        public static void WritePrototypeHtml (this IMemberDefinition member, TextWriter w, PackageTargetFramework framework, MemberXmlDocs docs, bool linkToCode, bool inExtensionClass)
        {
            switch (member) {
                case FieldDefinition t: WritePrototypeHtml (t, w, framework, linkToCode); break;
                case MethodDefinition t: WritePrototypeHtml (t, w, framework, docs, linkToCode, inExtensionClass); break;
                case PropertyDefinition t: WritePrototypeHtml (t, w, framework, docs, linkToCode); break;
                case EventDefinition t: WritePrototypeHtml (t, w, framework, linkToCode); break;
                case TypeDefinition t: WritePrototypeHtml (t, w, framework, docs, linkToCode); break;
                default: throw new NotSupportedException (member.GetType() + " " + member.FullName);
            }
        }

        public static string GetPrototypeHtml (this IMemberDefinition member, PackageTargetFramework framework, MemberXmlDocs docs, bool linkToCode, bool inExtensionClass)
        {
            using (var w = new StringWriter ()) {
                WritePrototypeHtml (member, w, framework, docs, linkToCode, inExtensionClass);
                return w.ToString ();
            }
        }
    }
}
