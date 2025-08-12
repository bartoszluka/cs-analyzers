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

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
        ImmutableArray.Create(UnusedVariableDescriptor);

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSyntaxNodeAction(ReportUnusedVariables, SyntaxKind.VariableDeclaration);
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

            if (IsUsed(variable, variableName))
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

    private static bool IsUsed(VariableDeclaratorSyntax variable, string variableName)
    {
        var scope = variable.FirstAncestorOrSelf<BlockSyntax>();

        return scope.DescendantNodes().OfType<IdentifierNameSyntax>().Any(id => id.Identifier.ValueText == variableName);
    }
}
