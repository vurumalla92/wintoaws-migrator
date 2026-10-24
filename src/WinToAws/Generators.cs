using System.Text;

namespace WinToAws;

/// <summary>Generates AWS deployment artefacts from an <see cref="AnalysisReport"/>.</summary>
public static class Generators
{
    public static string Dockerfile(AnalysisReport r)
    {
        if (r.HasDesktopUi)
        {
            return "# Windows container for a .NET Framework / desktop-backed application.\n"
                + "# Build this on a Windows host with Docker Desktop (Windows containers) or an EC2 Windows builder.\n"
                + "# This image keeps the app running unchanged first; the UI refactor comes later.\n"
                + "FROM mcr.microsoft.com/dotnet/framework/runtime:4.8-windowsservercore-ltsc2022\n"
                + "WORKDIR /app\n"
                + "COPY ./publish/ .\n"
                + "# Replace MyApp.exe with your real entry point. Expose the port your app listens on, if any.\n"
                + "ENTRYPOINT [\"MyApp.exe\"]\n";
        }
        return "# Linux container for a modernised .NET 8 application.\n"
            + "FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base\n"
            + "WORKDIR /app\n"
            + "EXPOSE 8080\n"
            + "FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build\n"
            + "WORKDIR /src\n"
            + "COPY . .\n"
            + "RUN dotnet publish -c Release -o /out\n"
            + "FROM base AS final\n"
            + "WORKDIR /app\n"
            + "COPY --from=build /out .\n"
            + "ENTRYPOINT [\"dotnet\", \"MyApp.dll\"]\n";
    }

    public static string CloudFormation(AnalysisReport r, string region = "us-east-1", string imageUri = "123456789012.dkr.ecr.us-east-1.amazonaws.com/wintoaws/app:latest")
    {
        var launch = r.HasDesktopUi ? "EC2" : "FARGATE";
        var sb = new StringBuilder();
        sb.AppendLine("AWSTemplateFormatVersion: '2010-09-09'");
        sb.AppendLine("Description: WinToAws generated stack - ECS service for a migrated Windows application.");
        sb.AppendLine("Parameters:");
        sb.AppendLine("  ImageUri:");
        sb.AppendLine("    Type: String");
        sb.AppendLine($"    Default: {imageUri}");
        sb.AppendLine("  ContainerPort:");
        sb.AppendLine("    Type: Number");
        sb.AppendLine("    Default: 8080");
        sb.AppendLine("  DesiredCount:");
        sb.AppendLine("    Type: Number");
        sb.AppendLine("    Default: 1");
        if (r.HasDatabase)
        {
            sb.AppendLine("  DbPassword:");
            sb.AppendLine("    Type: String");
            sb.AppendLine("    NoEcho: true");
            sb.AppendLine("    Description: Master password for Amazon RDS for SQL Server (use Secrets Manager in production).");
        }
        sb.AppendLine("Resources:");
        sb.AppendLine("  Cluster:");
        sb.AppendLine("    Type: AWS::ECS::Cluster");
        sb.AppendLine("  TaskRole:");
        sb.AppendLine("    Type: AWS::IAM::Role");
        sb.AppendLine("    Properties:");
        sb.AppendLine("      AssumeRolePolicyDocument:");
        sb.AppendLine("        Statement:");
        sb.AppendLine("          - Effect: Allow");
        sb.AppendLine("            Principal: { Service: ecs-tasks.amazonaws.com }");
        sb.AppendLine("            Action: sts:AssumeRole");
        sb.AppendLine("  ExecutionRole:");
        sb.AppendLine("    Type: AWS::IAM::Role");
        sb.AppendLine("    Properties:");
        sb.AppendLine("      AssumeRolePolicyDocument:");
        sb.AppendLine("        Statement:");
        sb.AppendLine("          - Effect: Allow");
        sb.AppendLine("            Principal: { Service: ecs-tasks.amazonaws.com }");
        sb.AppendLine("            Action: sts:AssumeRole");
        sb.AppendLine("      ManagedPolicyArns: [arn:aws:iam::aws:policy/service-role/AmazonECSTaskExecutionRolePolicy]");
        sb.AppendLine("  TaskDefinition:");
        sb.AppendLine("    Type: AWS::ECS::TaskDefinition");
        sb.AppendLine("    Properties:");
        sb.AppendLine($"      RequiresCompatibilities: [{launch}]");
        sb.AppendLine("      NetworkMode: awsvpc");
        sb.AppendLine("      Cpu: '512'");
        sb.AppendLine("      Memory: '1024'");
        sb.AppendLine("      ExecutionRoleArn: !GetAtt ExecutionRole.Arn");
        sb.AppendLine("      TaskRoleArn: !GetAtt TaskRole.Arn");
        sb.AppendLine("      ContainerDefinitions:");
        sb.AppendLine("        - Name: app");
        sb.AppendLine("          Image: !Ref ImageUri");
        sb.AppendLine("          Essential: true");
        sb.AppendLine("          PortMappings: [{ ContainerPort: !Ref ContainerPort }]");
        sb.AppendLine("          Environment:");
        sb.AppendLine("            - Name: ASPNETCORE_ENVIRONMENT");
        sb.AppendLine("              Value: Production");
        if (r.HasDatabase)
        {
            sb.AppendLine("  Database:");
            sb.AppendLine("    Type: AWS::RDS::DBInstance");
            sb.AppendLine("    Properties:");
            sb.AppendLine("      Engine: sqlserver-ex");
            sb.AppendLine("      DBInstanceClass: db.t3.micro");
            sb.AppendLine("      AllocatedStorage: '20'");
            sb.AppendLine("      MasterUsername: admin");
            sb.AppendLine("      MasterUserPassword: !Ref DbPassword");
        }
        sb.AppendLine("Outputs:");
        sb.AppendLine("  ClusterName:");
        sb.AppendLine("    Value: !Ref Cluster");
        sb.AppendLine($"# Generated by WinToAws for '{r.AppName}'. Region default: {region}.");
        sb.AppendLine($"# Strategy: {r.RecommendedStrategy}. Review the template before deploying: it is a starting point, not a production-hardened stack.");
        return sb.ToString();
    }

    public static string DeployScript(AnalysisReport r, string region = "us-east-1", string imageUri = "")
    {
        var image = string.IsNullOrWhiteSpace(imageUri) ? "<your-account>.dkr.ecr." + region + ".amazonaws.com/wintoaws/app:latest" : imageUri;
        var stack = "wintoaws-" + r.AppName.ToLowerInvariant().Replace(" ", "-");
        return "# WinToAws deploy script - run in PowerShell on a machine with the AWS CLI and Docker installed.\n"
            + "# 1. Edit the three values below, then run: .\\deploy.ps1\n"
            + "$Region = \"" + region + "\"\n"
            + "$ImageUri = \"" + image + "\"\n"
            + "$StackName = \"" + stack + "\"\n"
            + "$ErrorActionPreference = \"Stop\"\n"
            + "Write-Host \"Building image...\"\n"
            + "docker build -t $ImageUri .\n"
            + "Write-Host \"Pushing to Amazon ECR...\"\n"
            + "aws ecr get-login-password --region $Region | docker login --username AWS --password-stdin ($ImageUri.Split('/')[0])\n"
            + "docker push $ImageUri\n"
            + "Write-Host \"Deploying CloudFormation stack...\"\n"
            + "aws cloudformation deploy --region $Region --stack-name $StackName --template-file aws-template.yaml --parameter-overrides ImageUri=$ImageUri --capabilities CAPABILITY_NAMED_IAM\n"
            + "Write-Host \"Done. Check the stack in the AWS Console (CloudFormation) and the service in ECS.\"\n";
    }

    public static string ReportMarkdown(AnalysisReport r)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"# WinToAws migration report: {r.AppName}");
        sb.AppendLine();
        sb.AppendLine($"- Source: `{r.SourcePath}`");
        sb.AppendLine($"- Files scanned: {r.FileCount} | Projects: {r.ProjectCount} | Target framework: {r.TargetFramework ?? "not detected"}");
        sb.AppendLine($"- **Readiness: {r.ReadinessScore}/100 — {r.ReadinessLabel}**");
        sb.AppendLine($"- Blockers: {r.BlockerCount} | Warnings: {r.WarningCount}");
        sb.AppendLine($"- **Recommended strategy: {r.RecommendedStrategy}**");
        sb.AppendLine();
        sb.AppendLine("## Findings");
        sb.AppendLine();
        sb.AppendLine("| Severity | Finding | File | What to do |");
        sb.AppendLine("|---|---|---|---|");
        foreach (var f in r.Findings)
            sb.AppendLine($"| {f.Severity} | {f.Title} | `{f.File}` | {f.Recommendation} |");
        if (r.Findings.Count == 0) sb.AppendLine("| - | No blockers found | - | Proceed to containerise and deploy. |");
        sb.AppendLine();
        sb.AppendLine("## AWS checklist");
        sb.AppendLine("- [ ] Containerise with the generated Dockerfile (Windows containers need a Windows builder host).");
        sb.AppendLine("- [ ] Push the image to Amazon ECR.");
        sb.AppendLine("- [ ] Deploy the generated CloudFormation stack, then review security groups, IAM roles, and logging.");
        if (r.HasDatabase) sb.AppendLine("- [ ] Migrate the database to Amazon RDS for SQL Server; move the connection string to AWS Secrets Manager.");
        if (r.HasDesktopUi) sb.AppendLine("- [ ] Decide the UI path: Windows containers / AppStream 2.0 now, web refactor later.");
        sb.AppendLine("- [ ] Replace hard-coded paths and registry settings with environment variables / Parameter Store.");
        sb.AppendLine("- [ ] Test end to end in a non-production AWS account first.");
        return sb.ToString();
    }
}
