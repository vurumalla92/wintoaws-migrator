namespace WinToAws;

/// <summary>How hard a finding is to move to AWS.</summary>
public enum Severity { Info = 0, Warning = 1, Blocker = 2 }

/// <summary>One thing the analyser found in the application.</summary>
public sealed record Finding(string Code, string Title, Severity Severity, string File, string Detail, string Recommendation);

/// <summary>Result of analysing one application folder.</summary>
public sealed record AnalysisReport(
    string AppName,
    string SourcePath,
    int FileCount,
    int ProjectCount,
    string? TargetFramework,
    bool HasDesktopUi,
    bool HasDatabase,
    bool HasWindowsService,
    int ReadinessScore,
    string ReadinessLabel,
    string RecommendedStrategy,
    IReadOnlyList<Finding> Findings)
{
    public int BlockerCount => Findings.Count(f => f.Severity == Severity.Blocker);
    public int WarningCount => Findings.Count(f => f.Severity == Severity.Warning);
}

/// <summary>Everything <see cref="Migrator.Generate"/> writes to the output folder.</summary>
public sealed record GenerationResult(string OutputPath, IReadOnlyList<string> Files, AnalysisReport Report);
