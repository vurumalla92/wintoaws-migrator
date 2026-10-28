using WinToAws;
using Xunit;

public class MigratorTests
{
    private static string MakeApp(string code, string config = "")
    {
        var dir = Path.Combine(Path.GetTempPath(), "wintoaws-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        File.WriteAllText(Path.Combine(dir, "Program.cs"), code);
        if (config != "") File.WriteAllText(Path.Combine(dir, "App.config"), config);
        return dir;
    }

    [Fact]
    public void DetectsDesktopUiAndScoresBelowReady()
    {
        var dir = MakeApp("using System.Windows.Forms; class P { void M() { var f = new Form(); } }");
        var report = Migrator.Analyze(dir);
        Assert.True(report.HasDesktopUi);
        Assert.Contains(report.Findings, f => f.Code == "DESKTOP_WINFORMS");
        Assert.Equal(80, report.ReadinessScore);
        Assert.Equal("Needs work", report.ReadinessLabel);
    }

    [Fact]
    public void DetectsSqlServerAndRegistry()
    {
        var dir = MakeApp(
            "using Microsoft.Win32; class P { void M() { var k = Registry.CurrentUser; } }",
            "<configuration><connectionStrings><add connectionString=\"Server=localhost;Database=X;\" /></connectionStrings></configuration>");
        var report = Migrator.Analyze(dir);
        Assert.True(report.HasDatabase);
        Assert.Contains(report.Findings, f => f.Code == "WIN_REGISTRY");
        Assert.Contains(report.Findings, f => f.Code == "DB_SQLSERVER");
    }

    [Fact]
    public void CleanAppScoresHigh()
    {
        var dir = MakeApp("class P { void M() { System.Console.WriteLine(\"hello\"); } }");
        var report = Migrator.Analyze(dir);
        Assert.Equal(100, report.ReadinessScore);
        Assert.Equal("Ready", report.ReadinessLabel);
    }

    [Fact]
    public void GenerateWritesAllArtefacts()
    {
        var dir = MakeApp("using System.Windows.Forms; class P { void M() { var f = new Form(); } }");
        var outDir = Path.Combine(Path.GetTempPath(), "wintoaws-out-" + Guid.NewGuid().ToString("N"));
        var result = Migrator.Generate(dir, outDir);
        Assert.True(File.Exists(Path.Combine(outDir, "migration-report.md")));
        Assert.True(File.Exists(Path.Combine(outDir, "Dockerfile.windows")));
        Assert.True(File.Exists(Path.Combine(outDir, "aws-template.yaml")));
        Assert.True(File.Exists(Path.Combine(outDir, "deploy.ps1")));
        Assert.Contains("ECS", File.ReadAllText(Path.Combine(outDir, "aws-template.yaml")));
    }

    [Fact]
    public void MissingFolderThrows()
    {
        Assert.Throws<DirectoryNotFoundException>(() => Migrator.Analyze("/definitely/not/a/real/folder"));
    }
    [Fact]
    public void EmptyFolderReturnsReadyWithNoFindings()
    {
        var dir = Path.Combine(Path.GetTempPath(), "wintoaws-empty-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);

        var report = Migrator.Analyze(dir);

        Assert.Equal(0, report.FileCount);
        Assert.Empty(report.Findings);
        Assert.Equal(100, report.ReadinessScore);
        Assert.Equal("Ready", report.ReadinessLabel);
    }

    [Fact]
    public void IgnoresGeneratedBuildDirectories()
    {
        var dir = Path.Combine(Path.GetTempPath(), "wintoaws-skip-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(dir, "bin", "Debug"));
        Directory.CreateDirectory(Path.Combine(dir, "obj", "Debug"));
        File.WriteAllText(Path.Combine(dir, "bin", "Debug", "Generated.cs"),
            "using System.Windows.Forms; class Generated { Form f = new Form(); }");
        File.WriteAllText(Path.Combine(dir, "obj", "Debug", "Generated.cs"),
            "using Microsoft.Win32; class Generated { object key = Registry.LocalMachine; }");

        var report = Migrator.Analyze(dir);

        Assert.Equal(0, report.FileCount);
        Assert.Empty(report.Findings);
        Assert.Equal(100, report.ReadinessScore);
    }

}
