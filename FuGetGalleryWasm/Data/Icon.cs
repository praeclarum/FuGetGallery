using System;
namespace FuGetGallery
{
    public class Icon
    {
        public const string packageIcon = "briefcase";
        public Icon ()
        {
        }

        public static string ForObject (object o)
        {
            if (o == null)
                return "";
            if (o is PackageAssembly)
                return "file";
            if (o is Mono.Cecil.TypeDefinition t) {
                var isPublic = t.IsPublic;
                var isEnum = t.IsEnum;
                var isStruct = !isEnum && t.IsValueType;
                var isDel = !(isEnum || isStruct) && t.IsDelegate ();
                var isIface = !(isEnum || isStruct || isDel) && (t.IsInterface || t.IsAbstract);
                return isEnum ? "menu-hamburger" : (isDel ? "flash" : (isStruct ? "unchecked" : (isIface ? "star-empty" : "star")));
            }
            if (o is System.Linq.IGrouping<string, Mono.Cecil.TypeDefinition> g) {
                return "book";
            }
            if (o is PackageDependency d)
                return packageIcon;
            if (o is PackageData p)
                return packageIcon;
            if (o is PackageFile f)
                return "file";
            return "";
        }
    }
}
