using Mono.Cecil;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System;

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
            var fields = type.IsEnum ? type.Fields.Where (x => x.IsPublic && x.IsStatic).Cast<IMemberDefinition> () : type.Fields.Where(x => x.IsPublic).Cast<IMemberDefinition> ();
            var methods = type.Methods.Where (x => x.IsPublic && !x.IsSpecialName).Cast<IMemberDefinition> ();
            var properties = type.Properties.Where (x => x.GetMethod != null && x.GetMethod.IsPublic).Cast<IMemberDefinition> ();
            var events = type.Events.Where (x => x.AddMethod != null && x.AddMethod.IsPublic).Cast<IMemberDefinition> ();
            var types = type.NestedTypes.Where (x => x.IsPublic).Cast<IMemberDefinition> ();
            return fields.Concat (properties).Concat (events).Concat (methods).Concat (types);
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
            if (type.IsGenericInstance) {
                w.Write ("<span class=\"c-tr\">");
                WriteEncoded (type.Name.Substring (0, type.Name.IndexOf('`')), w);
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
            else {
                w.Write ("<span class=\"c-tr\">");
                WriteEncoded (type.Name, w);
                w.Write ("</span>");
            }
        }

        public static void WritePrototypeHtml (this FieldDefinition member, TextWriter w, PackageAssembly assembly, PackageTargetFramework framework, PackageData package)
        {
            if (member.IsStatic && !member.DeclaringType.IsEnum) {
                w.Write ("<span class=\"c-kw\">static</span> ");
            }
            WriteReferenceHtml (member.FieldType, w);
            w.Write (" <span class=\"c-fd\">");
            WriteEncoded (member.Name, w);
            w.Write ("</span>");
        }

        public static void WritePrototypeHtml (this MethodDefinition member, TextWriter w, PackageAssembly assembly, PackageTargetFramework framework, PackageData package)
        {
            if (member.IsStatic) {
                w.Write ("<span class=\"c-kw\">static</span> ");
            }
            WriteReferenceHtml (member.ReturnType, w);
            w.Write (" <span class=\"c-md\">");
            WriteEncoded (member.Name, w);
            w.Write ("</span>(");
            var head = "";
            foreach (var p in member.Parameters) {
                w.Write (head);
                w.Write ("<span class=\"c-ar\">");
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

        public static void WritePrototypeHtml (this IMemberDefinition member, TextWriter w, PackageAssembly assembly, PackageTargetFramework framework, PackageData package)
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
