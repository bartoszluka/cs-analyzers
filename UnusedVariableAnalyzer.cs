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
        context.RegisterSyntaxNodeAction(ReportUnusedVariables, SyntaxKind.VariableDeclaration);
        context.RegisterSyntaxNodeAction(ReportUnusedVariables2, SyntaxKind.ParenthesizedLambdaExpression);
        context.RegisterSyntaxNodeAction(ReportUnusedVariables3, SyntaxKind.SimpleLambdaExpression);
    }

    private static void ReportUnusedVariables(SyntaxNodeAnalysisContext context)
    {
        if (context.Node is not VariableDeclarationSyntax declaration)
        {
            return;
        }

        foreach (var variable in declaration.Variables)
        {
            var variableName = variable.Identifier.ValueText;

            if (IsInUsingStatement(variable))
            {
                continue;
            }

            if (IsUsed(context, variable))
            {
                continue;
            }

            var location = variable.Identifier.GetLocation();
            context.ReportDiagnostic(UnusedVariableDiagnostic(location, variableName));
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
        var scope = variable.FirstAncestorOrSelf<BlockSyntax>();
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

        foreach (var identifier in allIdentifiersInScope)
        {
            var identifierSymbol = context.SemanticModel.GetSymbolInfo(identifier).Symbol;

            if (SymbolEqualityComparer.IncludeNullability.Equals(variableSymbol, identifierSymbol))
            {
                return true;
            }
        }

        return false;
    }

    private static void ReportUnusedVariables2(SyntaxNodeAnalysisContext context)
    {
        if (context.Node is not ParenthesizedLambdaExpressionSyntax parenthesizedLambda)
        {
            return;
        }

        foreach (var parameter in parenthesizedLambda.ParameterList.Parameters)
        {
            var variableName = parameter.Identifier.ValueText;

            if (IsLambdaParameterUsed(context, parameter))
            {
                continue;
            }

            var location = parameter.Identifier.GetLocation();
            context.ReportDiagnostic(UnusedVariableDiagnostic(location, variableName));
        }
    }

    private static void ReportUnusedVariables3(SyntaxNodeAnalysisContext context)
    {
        if (context.Node is not SimpleLambdaExpressionSyntax lambda)
        {
            return;
        }
        var parameter = lambda.Parameter;
        var variableName = parameter.Identifier.ValueText;

        if (IsLambdaParameterUsed(context, parameter))
        {
            return;
        }

        var location = parameter.Identifier.GetLocation();
        context.ReportDiagnostic(UnusedVariableDiagnostic(location, variableName));
    }

    private static bool IsLambdaParameterUsed(SyntaxNodeAnalysisContext context, ParameterSyntax parameter)
    {
        var parameterSymbol = context.SemanticModel.GetDeclaredSymbol(parameter);
        if (parameterSymbol is null)
        {
            return true;
        }

        var syntaxNode = parameter.FirstAncestorOrSelf<LambdaExpressionSyntax>() switch
        {
            { Body: var b } when b is not null => b,
            { ExpressionBody: var e } when e is not null => e,
            _ => null,
        };

        if (syntaxNode is null)
        {
            return true;
        }

        var identifiers = syntaxNode.DescendantNodes().OfType<IdentifierNameSyntax>();

        foreach (var identifier in identifiers)
        {
            var identifierSymbol = context.SemanticModel.GetSymbolInfo(identifier).Symbol;

            if (SymbolEqualityComparer.Default.Equals(parameterSymbol, identifierSymbol))
            {
                return true;
            }
        }

        return false;
    }
}
