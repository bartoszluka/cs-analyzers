using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace MissingAnalyzers;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public class UnusedVariableAnalyzer : DiagnosticAnalyzer
{
    public static readonly DiagnosticDescriptor UnusedVariableDescriptor = new(
        id: "UVA001",
        title: "Variable assigned but never used",
        messageFormat: "Variable '{0}' is assigned but never used",
        category: "Usage",
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true
    );

    private static Diagnostic UnusedVariableDiagnostic(Location location, string variableName) =>
        Diagnostic.Create(UnusedVariableDescriptor, location, variableName);

    // public static readonly DiagnosticDescriptor ShadowedVariableDescriptor = new(
    //     id: "UVA002",
    //     title: "Variable shadows a different variable in the scope",
    //     messageFormat: "Variable '{0}' shadows another variable",
    //     category: "Usage",
    //     defaultSeverity: DiagnosticSeverity.Warning,
    //     isEnabledByDefault: true
    // );
    //
    // private static Diagnostic ShadowedVariableDiagnostic(Location location, string variableName) =>
    //     Diagnostic.Create(UnusedVariableDescriptor, location, variableName);

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
        ImmutableArray.Create(UnusedVariableDescriptor);

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSyntaxNodeAction(ReportUnusedVariablesInBlocks, SyntaxKind.VariableDeclaration);
        context.RegisterSyntaxNodeAction(
            ReportUnusedVariablesInParenthesizedLambdas,
            SyntaxKind.ParenthesizedLambdaExpression
        );
        context.RegisterSyntaxNodeAction(ReportUnusedVariablesInSimpleLambdas, SyntaxKind.SimpleLambdaExpression);
    }

    private static void ReportUnusedVariablesInBlocks(SyntaxNodeAnalysisContext context)
    {
        if (context.Node is not VariableDeclarationSyntax declaration)
        {
            return;
        }

        foreach (var variable in declaration.Variables)
        {
            if (IsInUsingStatement(variable))
            {
                continue;
            }

            if (IsUsed(context, variable))
            {
                continue;
            }

            var location = variable.Identifier.GetLocation();
            context.ReportDiagnostic(UnusedVariableDiagnostic(location, variable.Identifier.ValueText));
        }
    }

    private static bool IsInUsingStatement(VariableDeclaratorSyntax declaration)
    {
        if (
            declaration.FirstAncestorOrSelf<LocalDeclarationStatementSyntax>()
            is not LocalDeclarationStatementSyntax localDeclaration
        )
        {
            return false;
        }

        return localDeclaration.UsingKeyword != default;
    }

    private static bool IsUsed(SyntaxNodeAnalysisContext context, VariableDeclaratorSyntax variable)
    {
        var scope = GetParentScope(variable);
        if (scope is null)
        {
            return true;
        }

        var variableSymbol = context.SemanticModel.GetDeclaredSymbol(variable);
        if (variableSymbol is null)
        {
            return true;
        }

        var allIdentifiersInScope = scope.DescendantNodes().OfType<IdentifierNameSyntax>();
        // // TODO: report shadowing?
        // var lambdas = scope.DescendantNodes().OfType<LambdaExpressionSyntax>();
        // var lambdaParameters = lambdas
        //     .SelectMany(l =>
        //         l switch
        //         {
        //             ParenthesizedLambdaExpressionSyntax parens => parens.ParameterList.Parameters.ToList(),
        //             SimpleLambdaExpressionSyntax simple => [simple.Parameter],
        //             _ => [],
        //         }
        //     )
        //     .Select(name => name.Identifier.ValueText)
        //     .ToImmutableHashSet();

        return allIdentifiersInScope.Any(identifier =>
        {
            var identifierSymbol = context.SemanticModel.GetSymbolInfo(identifier).Symbol;
            return SymbolEqualityComparer.Default.Equals(variableSymbol, identifierSymbol);
        });
    }

    private static SyntaxNode GetParentScope(VariableDeclaratorSyntax variable)
    {
        var scope = variable.FirstAncestorOrSelf<BlockSyntax>();
        if (scope is not null)
        {
            return scope;
        }

        var unit = variable.FirstAncestorOrSelf<CompilationUnitSyntax>();
        // check if its direct children are global statements, than it has to be a top-level `Program.cs` style file
        if (unit.ChildNodes().Any(child => child is GlobalStatementSyntax))
        {
            return unit;
        }

        return null;
    }

    private static void ReportUnusedVariablesInParenthesizedLambdas(SyntaxNodeAnalysisContext context)
    {
        if (context.Node is not ParenthesizedLambdaExpressionSyntax parenthesizedLambda)
        {
            return;
        }

        foreach (var parameter in parenthesizedLambda.ParameterList.Parameters)
        {
            if (IsLambdaParameterUsed(context, parameter))
            {
                continue;
            }

            var location = parameter.Identifier.GetLocation();
            context.ReportDiagnostic(UnusedVariableDiagnostic(location, parameter.Identifier.ValueText));
        }
    }

    private static void ReportUnusedVariablesInSimpleLambdas(SyntaxNodeAnalysisContext context)
    {
        if (context.Node is not SimpleLambdaExpressionSyntax lambda)
        {
            return;
        }
        var parameter = lambda.Parameter;

        if (IsLambdaParameterUsed(context, parameter))
        {
            return;
        }

        var location = parameter.Identifier.GetLocation();
        context.ReportDiagnostic(UnusedVariableDiagnostic(location, parameter.Identifier.ValueText));
    }

    private static bool IsLambdaParameterUsed(SyntaxNodeAnalysisContext context, ParameterSyntax parameter)
    {
        var parameterSymbol = context.SemanticModel.GetDeclaredSymbol(parameter);
        if (parameterSymbol is null)
        {
            return true;
        }

        if (parameterSymbol.Name == "_")
        {
            // don't report if it's a discard symbol
            return true;
        }

        var identifiers = GetIdentifiersForLambda(parameter);

        return identifiers.Any(identifier =>
        {
            var identifierSymbol = context.SemanticModel.GetSymbolInfo(identifier).Symbol;
            return SymbolEqualityComparer.Default.Equals(parameterSymbol, identifierSymbol);
        });
    }

    private static IEnumerable<IdentifierNameSyntax> GetIdentifiersForLambda(ParameterSyntax parameter)
    {
        var body = parameter.FirstAncestorOrSelf<AnonymousFunctionExpressionSyntax>()?.Body;

        if (body is null)
        {
            return [];
        }

        return body.DescendantNodesAndSelf().OfType<IdentifierNameSyntax>();
    }
}
