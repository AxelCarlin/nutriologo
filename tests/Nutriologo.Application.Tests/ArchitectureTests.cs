namespace Nutriologo.Application.Tests;

using System.Text.RegularExpressions;
using Xunit;

public class ArchitectureTests
{
    private static string FindSolutionRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "Nutriologo.slnx")) && !File.Exists(Path.Combine(dir.FullName, "Nutriologo.sln")))
            dir = dir.Parent;
        return dir?.FullName ?? throw new Exception("Nutriologo.sln not found");
    }

    private static string ReadCsproj(string relative) =>
        File.ReadAllText(Path.Combine(FindSolutionRoot(), relative));

    [Fact]
    public void Domain_HasNoProjectReferences()
    {
        var content = ReadCsproj(Path.Combine("src", "Nutriologo.Domain", "Nutriologo.Domain.csproj"));
        Assert.DoesNotContain("ProjectReference", content);
    }

    [Fact]
    public void Domain_DoesNotReferenceInfrastructurePackages()
    {
        var content = ReadCsproj(Path.Combine("src", "Nutriologo.Domain", "Nutriologo.Domain.csproj"));
        foreach (var forbidden in new[] { "EntityFrameworkCore", "Npgsql", "AspNetCore" })
            Assert.DoesNotContain(forbidden, content);
    }

    [Fact]
    public void Application_DoesNotReferenceInfrastructure()
    {
        var content = ReadCsproj(Path.Combine("src", "Nutriologo.Application", "Nutriologo.Application.csproj"));
        Assert.DoesNotContain("Infrastructure", content);
    }

    [Fact]
    public void Domain_HasNoInfrastructureUsings()
    {
        var root = Path.Combine(FindSolutionRoot(), "src", "Nutriologo.Domain");
        foreach (var file in Directory.EnumerateFiles(root, "*.cs", SearchOption.AllDirectories))
        {
            var text = File.ReadAllText(file);
            Assert.False(Regex.IsMatch(text, @"using\s+(Microsoft\.EntityFrameworkCore|Npgsql|Microsoft\.AspNetCore|Ollama)"), file);
        }
    }
}
