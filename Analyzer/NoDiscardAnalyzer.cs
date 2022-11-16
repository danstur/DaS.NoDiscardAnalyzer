using DaS.NoDiscardAnalyzer.Utilities;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using System;
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

    internal const string DiagnosticId = "DaS1000";

    public static readonly DiagnosticDescriptor DoNotDiscardResultRule = new DiagnosticDescriptor(DiagnosticId, "Do not ignore return value",
        "Do not ignore return value",
        "Reliability",
        DiagnosticSeverity.Warning,
        true,
        "Do not discard the return value.");

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } = 
        ImmutableArray.Create(DoNotDiscardResultRule);

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
        if (noDiscardAttributeType is null)
        {
            return;
        }
        var discardForbiddenTypes = GetDiscardForbiddenTypes(context.Options, context.Compilation, context.CancellationToken);
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

    private static IImmutableSet<INamedTypeSymbol> GetDiscardForbiddenTypes(AnalyzerOptions analyzerOptions, Compilation compilation, CancellationToken cancellationToken)
    {
        var additionalTypes = analyzerOptions.AdditionalFiles.FirstOrDefault(file =>
            Path.GetFileName(file.Path).Equals(AdditionalForbiddenDiscardTypesFileName, StringComparison.Ordinal));
        var text = additionalTypes?.GetText(cancellationToken);
        if (text is null)
        {
            return ImmutableHashSet<INamedTypeSymbol>.Empty;
        }
        return text.Lines.Select(line => line.ToString())
            .Where(line => !string.IsNullOrWhiteSpace(line))
            .Select(line => compilation.GetTypeByMetadataName(line.Trim()))
            .FilterNull()
            .ToImmutableHashSet<INamedTypeSymbol>(SymbolEqualityComparer.Default);
    }

    private sealed class PerCompilation
    {
        private readonly INamedTypeSymbol _noDiscardNamedTypeSymbol;
        private readonly IImmutableSet<INamedTypeSymbol> _discardForbiddenTypes;
        private readonly IImmutableSet<INamedTypeSymbol> _taskTypes;

        public PerCompilation(INamedTypeSymbol noDiscardNamedTypeSymbol,
            IImmutableSet<INamedTypeSymbol> discardForbiddenTypes, IImmutableSet<INamedTypeSymbol> taskTypes)
        {
            this._noDiscardNamedTypeSymbol = noDiscardNamedTypeSymbol;
            this._discardForbiddenTypes = discardForbiddenTypes;
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
            if (_discardForbiddenTypes.Contains(returnType) ||
                HasNoDiscardAttribute(methodSymbol.GetAttributes()) ||
                HasNoDiscardAttribute(methodSymbol.GetReturnTypeAttributes()) ||
                HasNoDiscardAttribute(returnType.GetAttributes()))
            {
                var diagnostic = Diagnostic.Create(DoNotDiscardResultRule, IsolateMethodName(invocation).GetLocation());
                context.ReportDiagnostic(diagnostic);
            }
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

        private bool HasNoDiscardAttribute(ImmutableArray<AttributeData> attributes) => attributes.Any(att =>
            SymbolEqualityComparer.Default.Equals(att.AttributeClass, this._noDiscardNamedTypeSymbol));

        private static ExpressionSyntax IsolateMethodName(InvocationExpressionSyntax invocation)
        {
            return
                (invocation.Expression as MemberAccessExpressionSyntax)?.Name ??
                invocation.Expression as IdentifierNameSyntax ??
                (invocation.Expression as MemberBindingExpressionSyntax)?.Name ??
                invocation.Expression;
        }
    }

}