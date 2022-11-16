using DaS.NoDiscardAnalyzer.Utilities;
using FluentAssertions;
using Microsoft.CodeAnalysis.Diagnostics;
using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace DaS.NoDiscardAnalyzer.Tests;

public sealed class SharedCodeAnalyzerTests
{
    [Fact]
    public void AnalyzersHaveUniqueDiagnosticId()
    {
        var diagnosticIds = GetDiagnosticAnalyzers().SelectMany(analyzerType =>
        {
            var analyzer = (DiagnosticAnalyzer)(Activator.CreateInstance(analyzerType) ??
                                                throw new InvalidOperationException(
                                                    $"Could not create type {analyzerType}."));
            // one analyzer can return multiple diagnostics with the same id.
            return analyzer.SupportedDiagnostics.Select(diagnostic => diagnostic.Id).Distinct();
        });
        var duplicateDiagnosticIds = diagnosticIds.GroupBy(x => x)
                                                  .Where(g => g.Count() > 1)
                                                  .Select(g => g.First());
        duplicateDiagnosticIds.Should().BeEmpty("Diagnostic IDs should be used by only a single analyzer.");
    }

    private static IEnumerable<Type> GetDiagnosticAnalyzers() => typeof(EnumerableExtensions).Assembly
        .GetExportedTypes()
        .Where(t => !t.IsAbstract && typeof(DiagnosticAnalyzer).IsAssignableFrom(t));
}
