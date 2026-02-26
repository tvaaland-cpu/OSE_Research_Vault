# CI quality gates

This repository includes a GitHub Actions workflow at `.github/workflows/ci.yml` that runs on:

- `pull_request`
- `push` to `main`

The workflow performs the following checks using .NET 8:

1. `dotnet restore`
2. `dotnet build -c Release`
3. `dotnet test -c Release`
4. Uploads `.trx` test results as an artifact when available

## Recommended branch protection

For `main`, configure branch protection with these settings:

- **Require status checks to pass before merging** (select the CI workflow check)
- **Require a linear commit history** (optional)
- **Require pull request reviews before merging** (optional)
