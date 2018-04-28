using Mono.Cecil;
using System.Collections.Generic;
using System.Linq;

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
            var fields = type.Fields.Where(x => x.IsPublic).Cast<IMemberDefinition> ();
            var methods = type.Methods.Where (x => x.IsPublic && !x.IsSpecialName).Cast<IMemberDefinition> ();
            var properties = type.Properties.Where (x => x.GetMethod != null && x.GetMethod.IsPublic).Cast<IMemberDefinition> ();
            var events = type.Events.Where (x => x.AddMethod != null && x.AddMethod.IsPublic).Cast<IMemberDefinition> ();
            var types = type.NestedTypes.Where (x => x.IsPublic).Cast<IMemberDefinition> ();
            return fields.Concat (methods).Concat (properties).Concat (events).Concat (types);
        }
    }
}
