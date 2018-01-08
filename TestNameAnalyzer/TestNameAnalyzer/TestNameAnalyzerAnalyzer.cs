using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace TestNameAnalyzer
{
	[DiagnosticAnalyzer(LanguageNames.CSharp)]
	public class TestNameAnalyzerAnalyzer : DiagnosticAnalyzer
	{
		public const string DiagnosticId = "TESTNAME1";

		// See https://github.com/dotnet/roslyn/blob/master/docs/analyzers/Localizing%20Analyzers.md for more on localization
		private static readonly LocalizableString Title = new LocalizableResourceString(nameof(Resources.AnalyzerTitle), Resources.ResourceManager, typeof(Resources));
		private static readonly LocalizableString MessageFormat = new LocalizableResourceString(nameof(Resources.AnalyzerMessageFormat), Resources.ResourceManager, typeof(Resources));
		private static readonly LocalizableString Description = new LocalizableResourceString(nameof(Resources.AnalyzerDescription), Resources.ResourceManager, typeof(Resources));
		private const string Category = "Naming";

		private static DiagnosticDescriptor Rule = new DiagnosticDescriptor(DiagnosticId, Title, MessageFormat, Category, DiagnosticSeverity.Warning, isEnabledByDefault: true, description: Description);

		public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get { return ImmutableArray.Create(Rule); } }

		public override void Initialize(AnalysisContext context)
		{
			context.RegisterSyntaxNodeAction(AnalyzeSymbol, SyntaxKind.MethodDeclaration);
		}

		private static void AnalyzeSymbol(SyntaxNodeAnalysisContext context)
		{
			var method = (MethodDeclarationSyntax)context.Node;

			if(!HasTestAttribute(method, context))
				return;

			var testNameIdentifierToken = method.ChildTokens().First(tok => tok.IsKind(SyntaxKind.IdentifierToken));

			var methodFullName = testNameIdentifierToken.ValueText;

			string testedMethodName = methodFullName.Contains("_") ?
				methodFullName.Substring(0, methodFullName.IndexOf('_')) :
				methodFullName;

			var invocationExists = method.DescendantNodes()
				.OfType<InvocationExpressionSyntax>()
				.SelectMany(invoc => invoc.DescendantTokens()
					.Where(tok => tok.IsKind(SyntaxKind.IdentifierToken)))
				.Any(id => id.ValueText == testedMethodName);

			var matchName = Regex.IsMatch(methodFullName, ConstructRegex(testedMethodName), RegexOptions.Singleline);

			if(!invocationExists || !matchName)
				context.ReportDiagnostic(Diagnostic.Create(Rule, testNameIdentifierToken.GetLocation()));
		}

		private static string ConstructRegex(string testedMethodName)
		{
			return "^" + testedMethodName + "(_[^\\W_]+){1,2}$";
		}

		private static bool HasTestAttribute(MethodDeclarationSyntax methodDeclarationSyntax, SyntaxNodeAnalysisContext context)
		{
			return methodDeclarationSyntax.AttributeLists.
				SelectMany(al => al.Attributes).
				Any(attr =>
				{
					var attrType = context.SemanticModel.GetTypeInfo(attr).Type;
					return attrType.ContainingAssembly.Name == "nunit.framework" &&
						(attrType.Name == "TestCaseAttribute" || attrType.Name == "TestAttribute");
				});
		}
	}
}
