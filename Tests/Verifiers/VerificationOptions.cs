namespace DaS.NoDiscardAnalyzer.Tests.Verifiers;

public sealed record VerificationOptions
{
    /// <summary>
    /// If specified provides an editorconfig that overwrites the 
    /// </summary>
    public string? CustomAttributeName { get; init; }

    /// <summary>
    /// If specified an AdditionalFile with the content is provided to the analyzer
    /// </summary>
    public string? AdditionalForbiddenDiscardTypesFileContent { get; init; }
}
