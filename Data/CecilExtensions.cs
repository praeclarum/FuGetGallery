using Mono.Cecil;

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
    }
}
