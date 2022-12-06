using Microsoft.CodeAnalysis;
using System.Linq;

namespace DaS.NoDiscardAnalyzer.Utilities;

internal static class TypeSymbolExtensions
{
    /// <summary>
    /// Checks if the type or one of its base types has an attribute with the given class type.
    /// This is an optimization that's only valid if the <paramref name="attributeClassType"/> has Inherited=true
    /// </summary>
    /// <param name="typeSymbol">Type and its base types for which to check if the attribute is set.</param>
    /// <param name="attributeClassType">The attribute type that should be checked.</param>
    /// <returns>True if the attribute exists, false otherwise.</returns>
    public static bool HasInheritedAttributeClassType(this ITypeSymbol typeSymbol, ITypeSymbol attributeClassType)
    {
        var currentType = typeSymbol;
        while (currentType is not null)
        {
            var hasAttribute = currentType.GetAttributes()
                .Any(att => SymbolEqualityComparer.Default.Equals(att.AttributeClass, attributeClassType));
            if (hasAttribute)
            {
                return true;
            }
            currentType = currentType.BaseType;
        }
        return false;
    }
}