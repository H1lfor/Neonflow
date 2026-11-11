using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
foreach (var file in Directory.GetFiles(args[0], "*.cs", SearchOption.AllDirectories))
{
    var tree = CSharpSyntaxTree.ParseText(File.ReadAllText(file));
    if (tree.GetDiagnostics().Any(x => x.Severity == DiagnosticSeverity.Error)) throw new Exception(file);
    File.WriteAllText(file, tree.GetRoot().NormalizeWhitespace().ToFullString() + Environment.NewLine);
}
Console.WriteLine("Formatted Neonflow C# with Roslyn; syntax preserved.");
