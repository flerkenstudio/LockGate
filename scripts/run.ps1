$ErrorActionPreference = "Stop"

Write-Host "==> Starting LockGate Windows Application..." -ForegroundColor Cyan
dotnet run --project src/LockGate.App
