using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace MissingAnalyzers;

#pragma warning disable RS1041 // Compiler extensions should be implemented in assemblies targeting netstandard2.0
[DiagnosticAnalyzer(LanguageNames.CSharp)]
#pragma warning restore RS1041 // Compiler extensions should be implemented in assemblies targeting netstandard2.0
public class UnusedVariableAnalyzer : DiagnosticAnalyzer
{
    public static readonly DiagnosticDescriptor Rule = new(
        id: "UVA001",
        title: "Variable assigned but never used",
        messageFormat: "Variable '{0}' is assigned but never used",
        category: "Usage",
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true
    );

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => [Rule];

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSyntaxNodeAction(AnalyzeMethod, SyntaxKind.MethodDeclaration);
    }

    private static void AnalyzeMethod(SyntaxNodeAnalysisContext context)
    {
        if (context.Node is not MethodDeclarationSyntax method)
        {
            return;
        }

        var declarations = method
            .DescendantNodes()
            .OfType<VariableDeclarationSyntax>()
            .SelectMany(a => a.Variables)
            .ToList();

        foreach (var declaration in declarations)
        {
            var variableName = declaration.Identifier.ValueText;

            if (IsInUsingStatement(declaration))
            {
                continue;
            }

            if (IsUsed(method, variableName))
            {
                continue;
            }

            var location = declaration.Identifier.GetLocation();
            context.ReportDiagnostic(Diagnostic.Create(Rule, location, variableName));
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

    private static bool IsUsed(SyntaxNode method, string variableName)
    {
        return method.DescendantNodes().OfType<IdentifierNameSyntax>().Any(id => id.Identifier.ValueText == variableName);
    }
}
