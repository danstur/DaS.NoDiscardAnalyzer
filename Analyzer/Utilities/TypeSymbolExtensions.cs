using Microsoft.CodeAnalysis;
using System;
using System.Collections.Generic;
using System.Linq;

namespace DaS.NoDiscardAnalyzer.Utilities;

internal static class TypeSymbolExtensions
{
    public static IEnumerable<AttributeData> GetAttributesWithInherited(this ITypeSymbol typeSymbol)
    {
        foreach (var attributeData in typeSymbol.GetAttributes())
        {
            yield return attributeData;
        }

        var type = typeSymbol.BaseType;
        while (type is not null) 
        {
            foreach (var attributeData in type.GetAttributes().Where(IsInherited))
            {
                yield return attributeData;
            }
            type = type.BaseType;
        }
    }

    private static bool IsInherited(AttributeData attribute)
    {
        if (attribute.AttributeClass == null)
        {
            return false;
        }

        foreach (var attributeAttribute in attribute.AttributeClass.GetAttributes())
        {
            var attributeClass = attributeAttribute.AttributeClass;
            if (attributeClass is { Name: nameof(AttributeUsageAttribute), ContainingNamespace.Name: "System" })
            {
                foreach (var kvp in attributeAttribute.NamedArguments)
                {
                    if (kvp.Key == nameof(AttributeUsageAttribute.Inherited))
                    {
                        return (bool)kvp.Value.Value!;
                    }
                }
                // Default value of Inherited is true
                return true;
            }
        }
        return false;

    }
}