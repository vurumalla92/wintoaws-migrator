using WinToAws;

if (args.Length == 0 || args[0] is "-h" or "--help" or "help")
{
    Console.WriteLine("WinToAws 1.0.0 - move a Windows / .NET application to AWS");
    Console.WriteLine("  wintoaws analyze <app-folder>");
    Console.WriteLine("  wintoaws generate <app-folder> [--out <folder>] [--region <region>] [--image <ecr-image-uri>]");
    Console.WriteLine("Library use: var report = WinToAws.Migrator.Analyze(path);");
    return 0;
}

var command = args[0].ToLowerInvariant();
if (args.Length < 2)
{
    Console.Error.WriteLine("Missing app folder. Run: wintoaws analyze <app-folder>");
    return 1;
}

var source = args[1];
string GetOpt(string name, string fallback)
{
    var i = Array.IndexOf(args, name);
    return i >= 0 && i + 1 < args.Length ? args[i + 1] : fallback;
}

try
{
    if (command == "analyze")
    {
        var report = Migrator.Analyze(source);
        Console.WriteLine($"App: {report.AppName}");
        Console.WriteLine($"Readiness: {report.ReadinessScore}/100 - {report.ReadinessLabel}");
        Console.WriteLine($"Strategy: {report.RecommendedStrategy}");
        Console.WriteLine($"Blockers: {report.BlockerCount}  Warnings: {report.WarningCount}  Files: {report.FileCount}");
        foreach (var f in report.Findings)
            Console.WriteLine($"[{f.Severity}] {f.Title} - {f.File}");
        return 0;
    }

    if (command == "generate")
    {
        var outDir = GetOpt("--out", Path.Combine(source, "aws-output"));
        var region = GetOpt("--region", "us-east-1");
        var image = GetOpt("--image", "");
        var result = Migrator.Generate(source, outDir, region, image);
        Console.WriteLine($"Readiness: {result.Report.ReadinessScore}/100 - {result.Report.ReadinessLabel}");
        Console.WriteLine($"Strategy: {result.Report.RecommendedStrategy}");
        foreach (var f in result.Files) Console.WriteLine($"Wrote: {f}");
        return 0;
    }

    Console.Error.WriteLine($"Unknown command: {command}");
    return 1;
}
catch (Exception ex)
{
    Console.Error.WriteLine($"Error: {ex.Message}");
    return 1;
}
