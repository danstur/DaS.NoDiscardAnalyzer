using DaS.NoDiscardAnalyzer.Utilities;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Threading;

namespace DaS.NoDiscardAnalyzer;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class NoDiscardAnalyzer : DiagnosticAnalyzer
{
    internal const string CustomAttributeNameOverwriteOption = "build_property.NoDiscardAttributeName";

    internal const string AdditionalForbiddenDiscardTypesFileName = "TypesNotToDiscard.txt";

    public static readonly DiagnosticDescriptor DoNotDiscardResultRule = new DiagnosticDescriptor("DaS1000", "Do not ignore returned value",
        "Do not ignore returned value",
        "Reliability",
        DiagnosticSeverity.Error,
        true,
        "Do not ignore the returned value. The type has been marked as important and should be handled by application code.");

    public static readonly DiagnosticDescriptor NeitherAttributeNorListDeclaredRule = new DiagnosticDescriptor("DaS1001", 
        "NoDiscard Analyzer not active",
        $"Neither a NoDiscardAttribute nor {AdditionalForbiddenDiscardTypesFileName} file is defined",
        "Reliability",
        DiagnosticSeverity.Warning,
        true,
        $"Either include the attribute or include a {AdditionalForbiddenDiscardTypesFileName} file.");

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } = 
        ImmutableArray.Create(DoNotDiscardResultRule, NeitherAttributeNorListDeclaredRule);

    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.RegisterCompilationStartAction(OnCompilationStarted);
    }

    private static void OnCompilationStarted(CompilationStartAnalysisContext context)
    {
        if (!context.Options.AnalyzerConfigOptionsProvider.GlobalOptions.TryGetValue(
                CustomAttributeNameOverwriteOption, out var noDiscardAttributeName))
        {
            noDiscardAttributeName = "DaS.NoDiscardAnalyzer.Attributes.NoDiscardAttribute";
        }
        var noDiscardAttributeType =
            context.Compilation.GetTypeByMetadataName(noDiscardAttributeName);

        var additionalFileExists = TryGetDiscardForbiddenTypes(context.Options, context.Compilation, context.CancellationToken, 
            out var discardForbiddenTypes);
        if (noDiscardAttributeType is null && !additionalFileExists)
        {
            context.RegisterCompilationEndAction(c =>
            {
                var diagnostic = Diagnostic.Create(NeitherAttributeNorListDeclaredRule, Location.None);
                c.ReportDiagnostic(diagnostic);
            });
            return;
        }
        // It'd be more accurate to check if a type is awaitable than simply hardcoding these two types, see e.g.
        // Microsoft.VisualStudio.Threading.Analyzers.DiagnosticAnalyzerState::IsAwaitableType but the performance
        // of this is significantly worse and in practice it's rare to have custom awaiters.
        var taskTypes = new[]
        {
            context.Compilation.GetTypeByMetadataName("System.Threading.Tasks.Task`1"),
            context.Compilation.GetTypeByMetadataName("System.Threading.Tasks.ValueTask`1")
        }.FilterNull().ToImmutableHashSet<INamedTypeSymbol>(SymbolEqualityComparer.Default);
        context.RegisterSyntaxNodeAction(new PerCompilation(noDiscardAttributeType, discardForbiddenTypes, taskTypes).AnalyzeInvocation, 
            SyntaxKind.InvocationExpression);
    }

    private static bool TryGetDiscardForbiddenTypes(AnalyzerOptions analyzerOptions, Compilation compilation, CancellationToken cancellationToken, 
        out IImmutableSet<INamedTypeSymbol> discardForbiddenTypes)
    {
        var additionalTypes = analyzerOptions.AdditionalFiles.FirstOrDefault(file =>
            Path.GetFileName(file.Path).Equals(AdditionalForbiddenDiscardTypesFileName, StringComparison.Ordinal));
        var text = additionalTypes?.GetText(cancellationToken);
        if (text is null)
        {
            discardForbiddenTypes = ImmutableHashSet<INamedTypeSymbol>.Empty;
            return false;
        }
        discardForbiddenTypes = text.Lines.Select(line => line.ToString())
            .Where(line => !string.IsNullOrWhiteSpace(line))
            .Select(line => compilation.GetTypeByMetadataName(line.Trim()))
            .FilterNull()
            .ToImmutableHashSet<INamedTypeSymbol>(SymbolEqualityComparer.Default);
        return true;
        
    }

    private sealed class PerCompilation
    {
        private readonly INamedTypeSymbol? _noDiscardNamedTypeSymbol;
        private readonly IImmutableSet<INamedTypeSymbol> _discardForbiddenTypes;
        private readonly IImmutableSet<INamedTypeSymbol> _taskTypes;

        /// <summary>
        /// Used to cache the result of the computation for performance reasons.
        /// </summary>
        private readonly ConcurrentDictionary<ITypeSymbol, bool> _discardForbiddenTypesCache = new(SymbolEqualityComparer.Default);

        public PerCompilation(INamedTypeSymbol? noDiscardNamedTypeSymbol,
            IImmutableSet<INamedTypeSymbol> discardForbiddenTypes, IImmutableSet<INamedTypeSymbol> taskTypes)
        {
            _noDiscardNamedTypeSymbol = noDiscardNamedTypeSymbol;
            _discardForbiddenTypes = discardForbiddenTypes;
            _taskTypes = taskTypes;
        }

        public void AnalyzeInvocation(SyntaxNodeAnalysisContext context)
        {
            var invocation = (InvocationExpressionSyntax)context.Node;
            var relevantExpression = invocation.Parent;
            // If we await a statement, we are interested in what happens with the awaited expression
            if (relevantExpression is AwaitExpressionSyntax)
            {
                relevantExpression = relevantExpression.Parent;
            }
            // Only consider invocations that are direct statements. Otherwise, we assume their
            // result is consumed.
            if (relevantExpression is not (ExpressionStatementSyntax or ConditionalAccessExpressionSyntax))
            {
                return;
            }
            var methodSymbol = (IMethodSymbol?) context.SemanticModel.GetSymbolInfo(invocation).Symbol;
            if (methodSymbol?.ReturnType is not INamedTypeSymbol returnType)
            {
                return;
            }
            returnType = UnwrapTaskIfNecessary(returnType);
            if (!_discardForbiddenTypesCache.TryGetValue(returnType, out var doNotDiscard))
            {
                doNotDiscard = ComputeDoNotDiscardProperty(methodSymbol, returnType);
                _discardForbiddenTypesCache[returnType] = doNotDiscard;
            }
            if (doNotDiscard)
            {
                var diagnostic = Diagnostic.Create(DoNotDiscardResultRule, IsolateMethodName(invocation).GetLocation());
                context.ReportDiagnostic(diagnostic);
            }
        }

        private bool ComputeDoNotDiscardProperty(IMethodSymbol methodSymbol, INamedTypeSymbol returnType)
        {
            var doNotDiscard = _noDiscardNamedTypeSymbol is not null && (
                HasNoDiscardAttribute(methodSymbol.GetAttributes()) ||
                HasNoDiscardAttribute(methodSymbol.GetReturnTypeAttributes()) ||
                returnType.HasInheritedAttributeClassType(_noDiscardNamedTypeSymbol));
            doNotDiscard = doNotDiscard || TypeInOrInheritsFromTypeInDiscardForbiddenTypes(returnType);
            return doNotDiscard;
        }

        private bool TypeInOrInheritsFromTypeInDiscardForbiddenTypes(INamedTypeSymbol type)
        {
            var currentType = type;
            while (currentType is not null)
            {
                if (_discardForbiddenTypes.Contains(currentType))
                {
                    return true;
                }
                currentType = currentType.BaseType;
            }
            return false;
        }

        private INamedTypeSymbol UnwrapTaskIfNecessary(INamedTypeSymbol type)
        {
            if (!type.IsGenericType)
            {
                return type;
            }
            if (_taskTypes.Contains(type.OriginalDefinition))
            {
                return type.TypeArguments.First() as INamedTypeSymbol ?? type;
            }
            return type;
        }

        private bool HasNoDiscardAttribute(IEnumerable<AttributeData> attributes) => 
            attributes.Any(att => SymbolEqualityComparer.Default.Equals(att.AttributeClass, _noDiscardNamedTypeSymbol));

        private static ExpressionSyntax IsolateMethodName(InvocationExpressionSyntax invocation)
        {
#pragma warning disable CA1508 // Avoid dead conditional code: False positive - the code was taken from the roslyn analyzers themselves.
            return
                (invocation.Expression as MemberAccessExpressionSyntax)?.Name ??
                invocation.Expression as IdentifierNameSyntax ??
                (invocation.Expression as MemberBindingExpressionSyntax)?.Name ??
                invocation.Expression;
#pragma warning restore CA1508 // Avoid dead conditional code
        }

    }

}
