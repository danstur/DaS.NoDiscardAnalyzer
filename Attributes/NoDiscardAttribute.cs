using System;

namespace DaS.NoDiscardAnalyzer.Attributes;

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
