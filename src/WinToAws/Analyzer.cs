using System.Text.RegularExpressions;

namespace WinToAws;

/// <summary>
/// Scans a Windows / .NET application folder (text-based, so it works for
/// .NET Framework projects even on Linux) and scores how ready it is for AWS.
/// </summary>
public static class Analyzer
{
    private static readonly (string Pattern, string Code, string Title, Severity Severity, string Recommendation)[] Rules =
    {
        (@"UseWindowsForms|System\.Windows\.Forms|WinForms", "DESKTOP_WINFORMS", "Windows Forms UI detected", Severity.Blocker, "A desktop UI cannot run headless on Linux. Choose Windows containers on Amazon ECS (EC2 launch type), Amazon AppStream 2.0, or refactor the UI to a web front end."),
        (@"UseWPF|PresentationFramework|System\.Windows\.Controls", "DESKTOP_WPF", "WPF UI detected", Severity.Blocker, "A WPF UI cannot run headless on Linux. Choose Windows containers on Amazon ECS (EC2 launch type), Amazon AppStream 2.0, or refactor the UI to a web front end."),
        (@"Microsoft\.Win32\.Registry|Registry\.CurrentUser|Registry\.LocalMachine", "WIN_REGISTRY", "Windows Registry use", Severity.Blocker, "Replace registry settings with environment variables, AWS Systems Manager Parameter Store, or AWS Secrets Manager."),
        (@"ServiceBase|ServiceProcess", "WIN_SERVICE", "Windows Service detected", Severity.Warning, "Run it as a container task on Amazon ECS, or as an Amazon EC2 service. Remove ServiceBase installers if you containerise."),
        (@"System\.Messaging|MessageQueue|MSMQ", "WIN_MSMQ", "MSMQ messaging detected", Severity.Warning, "Replace MSMQ with Amazon SQS or Amazon SNS, or keep it inside a Windows container and migrate later."),
        (@"DllImport\s*\(\s*""(kernel32|user32|advapi32)", "WIN_PINVOKE", "Windows native API (P/Invoke) detected", Severity.Warning, "Check each native call: it will not run on Linux. Keep Windows containers, or replace the call with a cross-platform .NET API."),
        (@"COM|Interop|CreateObject|Activator\.CreateInstance.*ProgId", "WIN_COM", "COM / Office interop detected", Severity.Blocker, "COM does not run on Linux. Keep Windows containers, or replace Office automation with a library such as Open XML."),
        (@"[A-Za-z]:\\|\\\\[A-Za-z0-9]", "WIN_LOCAL_PATH", "Windows or UNC file path detected", Severity.Warning, "Move files to Amazon S3 or Amazon EFS and read paths from configuration, not hard-coded drive letters."),
        (@"Server\s*=|Data Source\s*=|LocalDB|AttachDBFilename", "DB_SQLSERVER", "SQL Server connection detected", Severity.Info, "Move the database to Amazon RDS for SQL Server (or Amazon Aurora) and store the connection string in AWS Secrets Manager."),
        (@"Integrated Security\s*=\s*(True|SSPI)", "DB_WINDOWS_AUTH", "Windows Integrated Security detected", Severity.Warning, "Integrated Security does not work the same way in containers. Use SQL authentication via Secrets Manager, or AWS IAM database authentication where supported."),
        (@"TargetFrameworkVersion[^>]*v4\.|net4[0-9]|<TargetFramework>net4", "FW_NETFRAMEWORK", ".NET Framework target detected", Severity.Warning, "Modernise to .NET 8 where possible, or run on Windows Server Core containers with the .NET Framework runtime."),
        (@"System\.ServiceModel|WCF|BasicHttpBinding|NetTcpBinding", "LEGACY_WCF", "WCF / SOAP service detected", Severity.Warning, "WCF server code is Windows-only in .NET Framework. Migrate to ASP.NET Core Web APIs, or keep a Windows container for this service."),
        (@"CrystalDecisions|Crystal Reports", "LEGACY_CRYSTAL", "Crystal Reports detected", Severity.Warning, "Crystal Reports is Windows-only. Replace reporting with a cross-platform library, or keep it in a Windows container."),
        (@"app\.config|web\.config", "CFG_FILE", "Classic .config file detected", Severity.Info, "Move settings that change per environment (connection strings, paths, endpoints) to environment variables or Parameter Store."),
    };

    public static AnalysisReport Analyze(string sourcePath)
    {
        if (!Directory.Exists(sourcePath))
            throw new DirectoryNotFoundException($"Source folder not found: {sourcePath}");

        var files = Directory.EnumerateFiles(sourcePath, "*.*", SearchOption.AllDirectories)
            .Where(f => !IsSkipped(f))
            .Where(f => HasAnalysableExtension(f))
            .ToList();

        var findings = new List<Finding>();
        string? framework = null;
        var projectCount = 0;

        foreach (var file in files)
        {
            var name = Path.GetFileName(file);
            if (name.EndsWith(".csproj", StringComparison.OrdinalIgnoreCase) ||
                name.EndsWith(".vbproj", StringComparison.OrdinalIgnoreCase) ||
                name.EndsWith(".sln", StringComparison.OrdinalIgnoreCase))
                projectCount++;

            string text;
            try { text = File.ReadAllText(file); }
            catch { continue; }

            var tf = Regex.Match(text, @"TargetFramework(?:Version)?[^>]*>([^<]+)<");
            if (tf.Success && framework is null) framework = tf.Groups[1].Value.Trim();

            if (name.EndsWith(".config", StringComparison.OrdinalIgnoreCase))
                findings.Add(new Finding("CFG_FILE", "Classic .config file detected", Severity.Info, Path.GetRelativePath(sourcePath, file), "Move settings that change per environment (connection strings, paths, endpoints) to environment variables or Parameter Store.", "Move settings that change per environment (connection strings, paths, endpoints) to environment variables or Parameter Store."));

            foreach (var (pattern, code, title, severity, recommendation) in Rules)
            {
                if (Regex.IsMatch(text, pattern, RegexOptions.IgnoreCase))
                {
                    findings.Add(new Finding(code, title, severity, Path.GetRelativePath(sourcePath, file), recommendation, recommendation));
                }
            }
        }

        var hasUi = findings.Any(f => f.Code is "DESKTOP_WINFORMS" or "DESKTOP_WPF");
        var hasDb = findings.Any(f => f.Code is "DB_SQLSERVER" or "DB_WINDOWS_AUTH");
        var hasService = findings.Any(f => f.Code is "WIN_SERVICE");

        var score = 100 - (findings.Count(f => f.Severity == Severity.Blocker) * 20)
                          - (findings.Count(f => f.Severity == Severity.Warning) * 8)
                          - (findings.Count(f => f.Severity == Severity.Info) * 1);
        score = Math.Clamp(score, 0, 100);
        // A single blocker must never read as "Ready": blockers cap the label at "Needs work".
        var label = findings.Any(f => f.Severity == Severity.Blocker)
            ? (score >= 30 ? "Needs work" : "Major rework")
            : score >= 80 ? "Ready" : score >= 55 ? "Mostly ready" : score >= 30 ? "Needs work" : "Major rework";
        var strategy = hasUi
            ? "Rehost on Windows containers (Amazon ECS on EC2) first, then refactor the UI to web in a later phase"
            : score >= 80
                ? "Replatform: Linux containers on Amazon ECS Fargate + Amazon RDS"
                : "Rehost first (Windows containers or Amazon EC2), then replatform service by service";

        var appName = new DirectoryInfo(Path.GetFullPath(sourcePath)).Name;
        return new AnalysisReport(appName, Path.GetFullPath(sourcePath), files.Count, projectCount,
            framework, hasUi, hasDb, hasService, score, label, strategy, findings);
    }

    private static bool IsSkipped(string path)
    {
        var parts = path.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        return parts.Any(p => p is ".git" or "bin" or "obj" or "node_modules" or ".vs" or "packages");
    }

    private static bool HasAnalysableExtension(string path) =>
        new[] { ".cs", ".vb", ".csproj", ".vbproj", ".sln", ".config", ".xml", ".json", ".xaml", ".aspx", ".asmx" }
            .Contains(Path.GetExtension(path), StringComparer.OrdinalIgnoreCase);
}
