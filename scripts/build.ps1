param(
    [string]$Configuration = "Release"
)

$ErrorActionPreference = "Stop"

Write-Host "==> Building LockGate Solution ($Configuration)..." -ForegroundColor Cyan
dotnet build LockGate.sln -c $Configuration

Write-Host "`n==> Running Automated Tests..." -ForegroundColor Cyan
dotnet run --project tests/LockGate.Core.Tests -c $Configuration

Write-Host "`n==> Build and Tests completed successfully!" -ForegroundColor Green
