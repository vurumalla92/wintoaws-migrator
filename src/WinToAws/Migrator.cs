namespace WinToAws;

/// <summary>
/// The one-library entry point. Add the WinToAws NuGet package, then:
/// <code>
/// var report = Migrator.Analyze(@"C:\MyApp");
/// var result = Migrator.Generate(@"C:\MyApp", @"C:\MyApp\aws-output");
/// </code>
/// </summary>
public static class Migrator
{
    /// <summary>Analyse a Windows / .NET application folder and return its AWS readiness report.</summary>
    public static AnalysisReport Analyze(string sourcePath) => Analyzer.Analyze(sourcePath);

    /// <summary>Analyse and generate all AWS artefacts into <paramref name="outputPath"/>.</summary>
    public static GenerationResult Generate(string sourcePath, string outputPath, string region = "us-east-1", string imageUri = "")
    {
        var report = Analyzer.Analyze(sourcePath);
        Directory.CreateDirectory(outputPath);
        var files = new List<string>();

        void Write(string name, string content)
        {
            var path = Path.Combine(outputPath, name);
            File.WriteAllText(path, content);
            files.Add(path);
        }

        Write("migration-report.md", Generators.ReportMarkdown(report));
        Write(report.HasDesktopUi ? "Dockerfile.windows" : "Dockerfile", Generators.Dockerfile(report));
        Write("aws-template.yaml", Generators.CloudFormation(report, region, string.IsNullOrWhiteSpace(imageUri) ? "123456789012.dkr.ecr.us-east-1.amazonaws.com/wintoaws/app:latest" : imageUri));
        Write("deploy.ps1", Generators.DeployScript(report, region, imageUri));
        return new GenerationResult(outputPath, files, report);
    }
}
