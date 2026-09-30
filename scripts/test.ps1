$ErrorActionPreference = "Stop"

Write-Host "==> Running FaceGate Test Suite..." -ForegroundColor Cyan
dotnet run --project tests/FaceGate.Core.Tests

if ($LASTEXITCODE -eq 0) {
    Write-Host "`nAll tests passed successfully!" -ForegroundColor Green
} else {
    Write-Host "`nTests failed!" -ForegroundColor Red
}
