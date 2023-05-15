# DaS.NoDiscardAnalyzer
A roslyn analyzer that provides similar functionality to the [[nodiscard]] attribute in C++17. 

Supports annotating types as well as specific methods. If a caller ignores the return value of an annotated method a warning is generated. 

## How To Use

Install the NuGet package https://www.nuget.org/packages/DaS.NoDiscardAnalyzer. After installing you must configure the analyzer so it knows what types or methods should not be discarded by the caller. You have multiple options for how to do this.

### Use DaS.NoDiscardAnalyzer.Attributes

The easiest option is to install the https://www.nuget.org/packages/DaS.NoDiscardAnalyzer.Attributes package, which includes the NoDiscardAttribute. Simply annotate methods or types with it.

```[csharp]
using DaS.NoDiscardAnalyzer.Attributes;
public static class Example 
{
    [NoDiscard] 
    public static int Foo() => 0xdead; // Ignoring the int returned by this method will cause a compile warning.
}
```

### Define your own custom Attribute

If you do not want to use the Attributes NuGet package you can also define your own attribute. The attribute should look identical to the NoDiscardAttribute, but can have a different name and namespace.

```[csharp]
[AttributeUsage(AttributeTargets.Method |
                AttributeTargets.ReturnValue |
                AttributeTargets.Class | 
                AttributeTargets.Struct | 
                AttributeTargets.Enum, AllowMultiple = false, Inherited = true)]
public sealed class NoDiscardAttribute : Attribute
{
    public string? Explanation { get; }

    public NoDiscardAttribute(string? explanation = null)
    {
        Explanation = explanation;
    }
}
```

You then tell the analyzer to use your package. You can do so 