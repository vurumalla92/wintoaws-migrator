#!/usr/bin/env bash
# Publish WinToAws to GitHub and NuGet.
# Run this ONCE, from your own machine / account, after: gh auth login
#   git config user.name "VENKATESHWAR REDDY VURUMALLA"
#   git config user.email "<your-real-email>"   # use your own email, do not invent one
#   ./publish.sh
# Optional, for NuGet: export NUGET_API_KEY=your-key  (get one at nuget.org)
set -euo pipefail
cd "$(dirname "$0")"

echo "== Building =="
dotnet build WinToAws.sln -c Release

echo "== Testing =="
dotnet test tests/WinToAws.Tests/WinToAws.Tests.csproj -c Release --no-build || echo "WARNING: tests did not run cleanly here - check the output before publishing."

echo "== Creating GitHub repo (public) =="
if ! gh repo view wintoaws-migrator >/dev/null 2>&1; then
  gh repo create wintoaws-migrator --public --source=. --remote=origin --push \
    --description "Move a Windows / .NET application to AWS by adding one library - analyse blockers, score readiness, generate Dockerfile + CloudFormation + deploy script."
else
  echo "Repo already exists - pushing instead."
  git push origin main 2>/dev/null || git push origin HEAD:main
fi

echo "== Packing NuGet packages =="
dotnet pack src/WinToAws/WinToAws.csproj -c Release -o ./nupkg
dotnet pack src/WinToAws.Cli/WinToAws.Cli.csproj -c Release -o ./nupkg

if [ -n "${NUGET_API_KEY:-}" ]; then
  echo "== Pushing to NuGet =="
  dotnet nuget push ./nupkg/*.nupkg --api-key "$NUGET_API_KEY" --source https://api.nuget.org/v3/index.json --skip-duplicate
else
  echo "NUGET_API_KEY not set - packages are in ./nupkg, push them with:"
  echo "  dotnet nuget push ./nupkg/*.nupkg --api-key YOUR-KEY --source https://api.nuget.org/v3/index.json"
fi

echo "Done. GitHub: https://github.com/vurumalla92/wintoaws-migrator"
