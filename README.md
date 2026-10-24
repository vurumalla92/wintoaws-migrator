# WinToAws

**Move a Windows / .NET application to AWS by adding one library.**

WinToAws analyses a Windows application folder, finds the things that will block a move to AWS (desktop UI, Registry, COM, hard-coded Windows paths, Windows Integrated Security, .NET Framework, WCF, and more), scores its AWS readiness from 0–100, recommends a strategy (rehost on Windows containers, or replatform to Linux containers on Amazon ECS Fargate), and generates the artefacts you need to actually move: a Dockerfile, an AWS CloudFormation template (ECS + optional Amazon RDS for SQL Server), a PowerShell deployment script, and a migration report.

Author: **VENKATESHWAR REDDY VURUMALLA**

Repository: https://github.com/vurumalla92/wintoaws-migrator
License: MIT

## Install — the library (the way most people will use it)

```bash
dotnet add package WinToAws
```

```csharp
using WinToAws;

// 1. Analyse
var report = Migrator.Analyze(@"C:\MyApp");
Console.WriteLine($"{report.ReadinessScore}/100 - {report.ReadinessLabel}");
Console.WriteLine(report.RecommendedStrategy);

// 2. Analyse + generate everything
var result = Migrator.Generate(@"C:\MyApp", @"C:\MyApp\aws-output", region: "us-east-1");
foreach (var file in result.Files) Console.WriteLine($"Wrote: {file}");
```

Generated files:

| File | What it is |
|---|---|
| `migration-report.md` | Findings table, score, strategy, AWS checklist |
| `Dockerfile` or `Dockerfile.windows` | Linux container, or Windows Server Core container if a desktop UI was found |
| `aws-template.yaml` | CloudFormation: ECS cluster, task definition, IAM roles, optional RDS for SQL Server |
| `deploy.ps1` | Build, push to Amazon ECR, deploy the stack with the AWS CLI |

## Install — the command-line tool

```bash
dotnet tool install -g WinToAws.Cli
wintoaws analyze C:\MyApp
wintoaws generate C:\MyApp --out C:\MyApp\aws-output --region us-east-1
```

Or build from source and run the DLL:

```bash
dotnet build WinToAws.sln -c Release
dotnet src/WinToAws.Cli/bin/Release/net8.0/WinToAwsCli.dll analyze ./samples/LegacySampleApp
```

## Example

Running against the included sample (`samples/LegacySampleApp`, a .NET Framework 4.8 WinForms-style app with Registry use, a `C:\` path, and a SQL Server connection with Integrated Security) produces:

```
App: LegacySampleApp
Readiness: 6/100 - Major rework
Strategy: Rehost on Windows containers (Amazon ECS on EC2) first, then refactor the UI to web in a later phase
Blockers: 3  Warnings: 4
```

## What it does — and what it does not do

**It does:**

- Text-scan `.cs`, `.vb`, `.csproj`, `.sln`, `.config`, `.xaml`, and related files for 14 blocker/warning patterns
- Score readiness (blockers −20, warnings −8, informational findings −1, clamped to 0–100)
- Pick a strategy: desktop UI → Windows containers first; otherwise, high score → Linux Fargate + RDS
- Generate starting-point deployment artefacts

**It does not:**

- Rewrite your code, convert a desktop UI to a web UI, or migrate a database for you — those are the phases the report plans
- Deploy anything by itself — `deploy.ps1` runs only when you run it, with your AWS credentials, in your account
- Guarantee a production-ready stack — review the generated CloudFormation template (security groups, IAM, logging, backups) before deploying to production, and test in a non-production account first

## Requirements

- .NET 8 SDK to build WinToAws itself (the applications it analyses can be .NET Framework — analysis is text-based and does not need to build them)
- For deployment: Docker (Windows containers need a Windows builder host), AWS CLI, an AWS account

## Contributing

See [CONTRIBUTING.md](CONTRIBUTING.md). Issues and pull requests are welcome — especially new analyser rules with a sample that proves them.

## Roadmap

- Amazon AppStream 2.0 and Amazon EC2 output templates
- Amazon SQS/SNS replacement suggestions for MSMQ, with code sketches
- Amazon RDS migration checklist generation (schema, users, jobs)
- A web UI for the report, so teams can share it without the CLI
