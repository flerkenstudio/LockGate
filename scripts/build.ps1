param(
    [string]$Configuration = "Release"
)

$ErrorActionPreference = "Stop"

Write-Host "==> Building FaceGate Solution ($Configuration)..." -ForegroundColor Cyan
dotnet build FaceGate.sln -c $Configuration

Write-Host "`n==> Running Automated Tests..." -ForegroundColor Cyan
dotnet run --project tests/FaceGate.Core.Tests -c $Configuration

Write-Host "`n==> Build and Tests completed successfully!" -ForegroundColor Green
