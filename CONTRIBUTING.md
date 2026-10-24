# Contributing to WinToAws

Thank you for helping. This project is maintained by VENKATESHWAR REDDY VURUMALLA.

## How to contribute

1. Fork the repository and create a branch for your change.
2. Build: `dotnet build WinToAws.sln -c Release`
3. Test: `dotnet test tests/WinToAws.Tests/WinToAws.Tests.csproj -c Release`
4. If you add an analyser rule, add a sample in `samples/` (or a test case) that triggers it, and a test that proves it.
5. Open a pull request with a short description of what changed and why.

## Rules for new analyser rules

- A rule must be a text pattern that is hard to trigger by accident.
- Give it a stable code (for example `WIN_REGISTRY`), a clear title, a severity, and a recommendation that tells the user what to do next — not just what is wrong.
- Never claim a finding proves something it does not. A pattern match is a lead for a human to check, not a verdict.

## Code of conduct

Be kind, be specific, and keep claims honest: no invented metrics, no fake download or adoption numbers, no backdated history.
