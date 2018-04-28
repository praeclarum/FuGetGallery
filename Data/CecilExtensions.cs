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

        static string GetXmlType (this TypeDefinition type)
        {
            return type.FullName;
        }

        static string GetXmlTypeRef (this TypeReference type)
        {
            return type.FullName;
        }

        public static string GetXmlName (this FieldDefinition d)
        {
            return "F:" + GetXmlType (d.DeclaringType) + "." + d.Name;
        }

        public static string GetXmlName (this MethodDefinition d)
        {
            var name = d.IsConstructor ? d.Name.Replace ('.', '#') : d.Name.Replace ("`", "``");

            var b = new StringBuilder ("M:");
            b.Append (GetXmlType (d.DeclaringType));
            b.Append (".");
            b.Append (name);
            return b.ToString ();
        }

        public static string GetXmlName (this PropertyDefinition d)
        {
            return "P:" + GetXmlType (d.DeclaringType) + "." + d.Name;
        }

        public static string GetXmlName (this EventDefinition d)
        {
            return "E:" + GetXmlType (d.DeclaringType) + "." + d.Name;
        }

        public static string GetXmlName (this TypeDefinition d)
        {
            return "T:" + GetXmlType (d);
        }

        public static string GetXmlName (this IMemberDefinition member)
        {
            switch (member) {
                case FieldDefinition t: return GetXmlName (t);
                case MethodDefinition t: return GetXmlName (t);
                case PropertyDefinition t: return GetXmlName (t);
                case EventDefinition t: return GetXmlName (t);
                case TypeDefinition t: return GetXmlName (t);
                default: throw new NotSupportedException (member.GetType () + " " + member.FullName);
            }
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

        public static void WriteReferenceHtml (this TypeReference type, TextWriter w)
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
            else if (type.IsPrimitive) {
                w.Write ("<span class=\"c-tr\">");
                switch (type.FullName) {
                    case "System.Byte": w.Write ("byte"); break;
                    case "System.Boolean": w.Write ("bool"); break;
                    case "System.Double": w.Write ("double"); break;
                    case "System.Int32": w.Write ("int"); break;
                    case "System.Int64": w.Write ("long"); break;
                    case "System.Single": w.Write ("float"); break;
                    default: WriteEncoded (type.Name, w); break;
                }
                w.Write ("</span>");
            }
            else if (type.IsGenericInstance) {
                w.Write ("<span class=\"c-tr\">");
                WriteEncoded (type.Name.Substring (0, type.Name.IndexOf ('`')), w);
                w.Write ("</span>");
                w.Write ("&lt;");
                var head = "";
                foreach (var a in ((GenericInstanceType)type).GenericArguments) {
                    w.Write (head);
                    WriteReferenceHtml (a, w);
                    head = ", ";
                }
                w.Write ("&gt;");
            }
            else if (type.IsByReference) {
                w.Write ("<span class=\"c-kw\">ref</span> ");
                WriteReferenceHtml (type.GetElementType (), w);
            }
            else {
                w.Write ("<span class=\"c-tr\">");
                WriteEncoded (type.Name, w);
                w.Write ("</span>");
            }
        }

        public static void WritePrototypeHtml (this FieldDefinition member, TextWriter w, PackageAssembly assembly, PackageTargetFramework framework, PackageData package)
        {
            if (!member.DeclaringType.IsEnum) {
                if (member.IsStatic) {
                    w.Write ("<span class=\"c-kw\">static</span> ");
                }
                WriteReferenceHtml (member.FieldType, w);
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
                WriteEncoded (member.DeclaringType.Name, w);
            }
            else {
                if (member.IsVirtual) {
                    w.Write ("<span class=\"c-kw\">virtual</span> ");
                }
                WriteReferenceHtml (member.ReturnType, w);
                w.Write (" <span class=\"c-md\">");
                WriteEncoded (member.Name, w);
            }
            w.Write ("</span>(");
            var head = "";
            foreach (var p in member.Parameters) {
                w.Write (head);
                WriteReferenceHtml (p.ParameterType, w);
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
            WriteReferenceHtml (member.PropertyType, w);
            w.Write (" <span class=\"c-pd\">");
            WriteEncoded (member.Name, w);
            w.Write ("</span>");
        }

        public static void WritePrototypeHtml (this EventDefinition member, TextWriter w, PackageAssembly assembly, PackageTargetFramework framework, PackageData package)
        {
            if (member.AddMethod != null && member.AddMethod.IsStatic) {
                w.Write ("<span class=\"c-kw\">static</span> ");
            }
            w.Write ("<span class=\"c-kw\">event</span> ");
            WriteReferenceHtml (member.EventType, w);
            w.Write (" <span class=\"c-ed\">");
            WriteEncoded (member.Name, w);
            w.Write ("</span>");
        }

        public static void WritePrototypeHtml (this TypeDefinition member, TextWriter w, PackageAssembly assembly, PackageTargetFramework framework, PackageData package)
        {
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
            w.Write ("<span class=\"c-td\">");
            WriteEncoded (member.Name, w);
            w.Write ("</span>");
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
