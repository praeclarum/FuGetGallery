using Mono.Cecil;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System;
using System.Text;

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

        public static IEnumerable<IMemberDefinition> GetPublicMembers (this TypeDefinition type)
        {
            var fields = (type.IsEnum ? type.Fields.Where (x => x.IsPublic && x.IsStatic).Cast<IMemberDefinition> () : type.Fields.Where(x => x.IsPublic).Cast<IMemberDefinition> ()).OrderBy (x => x.Name);
            var methods = type.Methods.Where (x => x.IsPublic && !(x.IsAddOn || x.IsRemoveOn || x.IsGetter || x.IsSetter)).Cast<IMemberDefinition> ().OrderBy (x => x.Name);
            var properties = type.Properties.Where (x => x.GetMethod != null && x.GetMethod.IsPublic).Cast<IMemberDefinition> ().OrderBy (x => x.Name);
            var events = type.Events.Where (x => x.AddMethod != null && x.AddMethod.IsPublic).Cast<IMemberDefinition> ().OrderBy (x => x.Name);
            var types = type.NestedTypes.Where (x => x.IsPublic).Cast<IMemberDefinition> ().OrderBy (x => x.Name);
            return fields.Concat (properties).Concat (events).Concat (methods).Concat (types);
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

        public static void WriteReferenceHtml (this TypeReference type, TextWriter w, PackageTargetFramework framework)
        {
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
                w.Write ("<span class=\"c-kw\">ref</span> ");
                WriteReferenceHtml (type.GetElementType (), w, framework);
            }
            else {
                var name = type.Name;
                var ni = name.LastIndexOf ('`');
                if (ni > 0) name = name.Substring (0, ni);
                var url = framework?.FindTypeUrl (type.FullName);
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

        public static void WritePrototypeHtml (this FieldDefinition member, TextWriter w, PackageAssembly assembly, PackageTargetFramework framework, PackageData package)
        {
            if (!member.DeclaringType.IsEnum) {
                if (member.IsStatic) {
                    w.Write ("<span class=\"c-kw\">static</span> ");
                }
                WriteReferenceHtml (member.FieldType, w, framework);
                w.Write (" ");
            }
            w.Write ("<span class=\"c-fd\">");
            WriteEncoded (member.Name, w);
            w.Write ("</span>");
            if (member.Constant != null) {
                w.Write (" = ");
                w.Write ("<span class=\"c-st\">");
                WriteEncoded (member.Constant.ToString (), w);
                w.Write ("</span>");
            }
        }

        public static void WritePrototypeHtml (this MethodDefinition member, TextWriter w, PackageAssembly assembly, PackageTargetFramework framework, PackageData package)
        {
            if (member.IsStatic) {
                w.Write ("<span class=\"c-kw\">static</span> ");
            }
            if (member.IsConstructor) {
                w.Write ("<span class=\"c-cd\">");
                var name = member.DeclaringType.Name;
                var ni = name.LastIndexOf ('`');
                if (ni > 0) name = name.Substring (0, ni);
                WriteEncoded (name, w);
            }
            else {
                if (member.IsVirtual) {
                    w.Write ("<span class=\"c-kw\">virtual</span> ");
                }
                WriteReferenceHtml (member.ReturnType, w, framework);
                w.Write (" <span class=\"c-md\">");
                WriteEncoded (member.Name, w);
            }
            w.Write ("</span>");
            var head = "";
            if (member.HasGenericParameters) {
                w.Write ("&lt;");
                head = "";
                foreach (var p in member.GenericParameters) {
                    w.Write (head);
                    WriteReferenceHtml (p, w, framework);
                    head = ", ";
                }
                w.Write ("&gt;");
            }
            w.Write ("(");
            head = "";
            foreach (var p in member.Parameters) {
                w.Write (head);
                WriteReferenceHtml (p.ParameterType, w, framework);
                w.Write (" <span class=\"c-ar\">");
                WriteEncoded (p.Name, w);
                w.Write ("</span>");
                head = ", ";
            }
            w.Write (")");
        }

        public static void WritePrototypeHtml (this PropertyDefinition member, TextWriter w, PackageAssembly assembly, PackageTargetFramework framework, PackageData package)
        {
            if (member.GetMethod != null && member.GetMethod.IsStatic) {
                w.Write ("<span class=\"c-kw\">static</span> ");
            }
            WriteReferenceHtml (member.PropertyType, w, framework);
            if (member.GetMethod != null && member.GetMethod.Parameters.Count > 0) {
                w.Write (" <span class=\"c-pd\">this</span>[");
                var head = "";
                foreach (var p in member.GetMethod.Parameters) {
                    w.Write (head);
                    WriteReferenceHtml (p.ParameterType, w, framework);
                    w.Write (" <span class=\"c-ar\">");
                    WriteEncoded (p.Name, w);
                    w.Write ("</span>");
                    head = ", ";
                }
                w.Write ("]");
            }
            else {
                w.Write (" <span class=\"c-pd\">");
                WriteEncoded (member.Name, w);
                w.Write ("</span>");
            }
            w.Write (" {");
            if (member.GetMethod != null) w.Write (" <span class=\"c-kw\">get</span>;");
            if (member.SetMethod != null && member.SetMethod.IsPublic) w.Write (" <span class=\"c-kw\">set</span>;");
            w.Write (" }");
        }

        public static void WritePrototypeHtml (this EventDefinition member, TextWriter w, PackageAssembly assembly, PackageTargetFramework framework, PackageData package)
        {
            if (member.AddMethod != null && member.AddMethod.IsStatic) {
                w.Write ("<span class=\"c-kw\">static</span> ");
            }
            w.Write ("<span class=\"c-kw\">event</span> ");
            WriteReferenceHtml (member.EventType, w, framework);
            w.Write (" <span class=\"c-ed\">");
            WriteEncoded (member.Name, w);
            w.Write ("</span>");
        }

        public static void WritePrototypeHtml (this TypeDefinition member, TextWriter w, PackageAssembly assembly, PackageTargetFramework framework, PackageData package)
        {
            if (member.IsSealed && member.IsAbstract)
                w.Write ("<span class=\"c-kw\">static</span> ");
            else if (member.IsSealed && !member.IsEnum)
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
            
            var name = member.Name;
            var ni = name.LastIndexOf ('`');
            if (ni > 0) name = name.Substring (0, ni);
            var url = framework?.FindTypeUrl (member.FullName);
            if (url != null) {
                w.Write ($"<a href=\"{url}\" class=\"c-td\">");
                WriteEncoded (name, w);
                w.Write ("</a>");
            }
            else {
                w.Write ("<span class=\"c-td\">");
                WriteEncoded (name, w);
                w.Write ("</span>");
            }
            if (member.HasGenericParameters) {
                w.Write ("&lt;");
                var head = "";
                foreach (var a in member.GenericParameters) {
                    w.Write (head);
                    WriteReferenceHtml (a, w, framework);
                    head = ", ";
                }
                w.Write ("&gt;");
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
        }

        public static void WritePrototypeHtml (this IMemberDefinition member, TextWriter w, PackageAssembly assembly = null, PackageTargetFramework framework = null, PackageData package = null)
        {
            switch (member) {
                case FieldDefinition t: WritePrototypeHtml (t, w, assembly, framework, package); break;
                case MethodDefinition t: WritePrototypeHtml (t, w, assembly, framework, package); break;
                case PropertyDefinition t: WritePrototypeHtml (t, w, assembly, framework, package); break;
                case EventDefinition t: WritePrototypeHtml (t, w, assembly, framework, package); break;
                case TypeDefinition t: WritePrototypeHtml (t, w, assembly, framework, package); break;
                default: throw new NotSupportedException (member.GetType() + " " + member.FullName);
            }
        }

        public static string GetPrototypeHtml (this IMemberDefinition member, PackageAssembly assembly = null, PackageTargetFramework framework = null, PackageData package = null)
        {
            using (var w = new StringWriter ()) {
                WritePrototypeHtml (member, w, assembly, framework, package);
                return w.ToString ();
            }
        }
    }
}
