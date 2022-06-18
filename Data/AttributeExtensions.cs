using System.Linq;
using Mono.Cecil;

namespace FuGetGallery
{
    internal static class AttributeExtensions
    {
        internal static (bool, string) GetObsoleteMessage (this ICustomAttributeProvider attributeProvider)
        {
            if (!attributeProvider.HasCustomAttributes)
                return (false, null);

            var obsoleteAttrribute =
                attributeProvider.CustomAttributes.FirstOrDefault (ca =>
                    ca.AttributeType.FullName == "System.ObsoleteAttribute");

            return obsoleteAttrribute != null
                ? (true, obsoleteAttrribute.ConstructorArguments.FirstOrDefault ().Value?.ToString ())
                : (false, null);
        }
    }
}
