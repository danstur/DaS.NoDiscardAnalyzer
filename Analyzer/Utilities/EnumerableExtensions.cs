using System.Collections.Generic;
using System.Linq;

namespace DaS.NoDiscardAnalyzer.Utilities;

public static class EnumerableExtensions
{
    /// <summary>
    /// Filters all elements in the enumerable that are null.
    /// </summary>
    /// <typeparam name="TSource">Type of enumeration.</typeparam>
    /// <param name="enumerable">Enumerable with possible null elements.</param>
    /// <returns>Enumerable containing all non-null elements of <paramref name="enumerable"/></returns>
    public static IEnumerable<TSource> FilterNull<TSource>(this IEnumerable<TSource?> enumerable)
        where TSource : class
    {
        return enumerable.Where(elem => elem is not null).Cast<TSource>();
    }

    /// <summary>
    /// Filters all elements in the enumerable that are null.
    /// </summary>
    /// <typeparam name="TSource">Type of enumeration.</typeparam>
    /// <param name="enumerable">Enumerable with possible null elements.</param>
    /// <returns>Enumerable containing all non-null elements of <paramref name="enumerable"/></returns>
    public static IEnumerable<TSource> FilterNull<TSource>(this IEnumerable<TSource?> enumerable) where TSource : struct
    {
        return enumerable.Where(elem => elem is not null).Cast<TSource>();
    }
}
