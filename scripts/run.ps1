$ErrorActionPreference = "Stop"

Write-Host "==> Starting FaceGate Windows Application..." -ForegroundColor Cyan
dotnet run --project src/FaceGate.App
