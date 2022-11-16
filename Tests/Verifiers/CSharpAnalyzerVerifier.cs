using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Testing;
using Microsoft.CodeAnalysis.Testing.Verifiers;
using System.Threading;
using System.Threading.Tasks;

namespace DaS.NoDiscardAnalyzer.Tests.Verifiers;

public static partial class CSharpAnalyzerVerifier<TAnalyzer>
    where TAnalyzer : DiagnosticAnalyzer, new()
{
    /// <inheritdoc cref="AnalyzerVerifier{TAnalyzer, TTest, TVerifier}.Diagnostic()"/>
    public static DiagnosticResult Diagnostic()
        => CSharpAnalyzerVerifier<TAnalyzer, XUnitVerifier>.Diagnostic();

    /// <inheritdoc cref="AnalyzerVerifier{TAnalyzer, TTest, TVerifier}.Diagnostic(string)"/>
    public static DiagnosticResult Diagnostic(string diagnosticId)
        => CSharpAnalyzerVerifier<TAnalyzer, XUnitVerifier>.Diagnostic(diagnosticId);

    /// <inheritdoc cref="AnalyzerVerifier{TAnalyzer, TTest, TVerifier}.Diagnostic(DiagnosticDescriptor)"/>
    public static DiagnosticResult Diagnostic(DiagnosticDescriptor descriptor)
        => CSharpAnalyzerVerifier<TAnalyzer, XUnitVerifier>.Diagnostic(descriptor);

    /// <inheritdoc cref="AnalyzerVerifier{TAnalyzer, TTest, TVerifier}.VerifyAnalyzerAsync(string, DiagnosticResult[])"/>
    public static async Task VerifyAnalyzerAsync(string source, VerificationOptions? options,
        params DiagnosticResult[] expected)
    {
        var test = new MyTest { TestCode = source, };
        if (options?.CustomAttributeName is not null)
        {
            test.TestState.AnalyzerConfigFiles.Add(("/.editorconfig", $"""
            is_global = true
            
            {NoDiscardAnalyzer.CustomAttributeNameOverwriteOption} = {options.CustomAttributeName} 
            """ ));
        }

        if (options?.AdditionalForbiddenDiscardTypesFileContent is not null)
        {
            test.TestState.AdditionalFiles.Add((NoDiscardAnalyzer.AdditionalForbiddenDiscardTypesFileName, options.AdditionalForbiddenDiscardTypesFileContent));
        }

        test.TestState.AddAttributesReference();
        test.ExpectedDiagnostics.AddRange(expected);
        await test.RunAsync(CancellationToken.None);
    }

    /// <inheritdoc cref="AnalyzerVerifier{TAnalyzer, TTest, TVerifier}.VerifyAnalyzerAsync(string, DiagnosticResult[])"/>
    public static Task VerifyAnalyzerAsync(string source, params DiagnosticResult[] expected) =>
        VerifyAnalyzerAsync(source, null, expected);
}
