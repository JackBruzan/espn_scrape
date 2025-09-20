$body = @{
    SyncType = "full"
    Season = 2024
} | ConvertTo-Json

Write-Host "Triggering full 2024 historical sync..." -ForegroundColor Yellow
Write-Host "Request body: $body" -ForegroundColor Gray

try {
    $response = Invoke-RestMethod -Uri "http://localhost:5000/api/espnintegration/sync/historical" -Method POST -Body $body -ContentType "application/json"
    
    Write-Host "`nSuccess! Historical sync job triggered:" -ForegroundColor Green
    Write-Host "Job Key: $($response.jobKey)" -ForegroundColor Cyan
    Write-Host "Triggered At: $($response.triggeredAt)" -ForegroundColor Cyan
    Write-Host "Message: $($response.message)" -ForegroundColor Cyan
    
    Write-Host "`nCheck the main application console for sync progress logs..." -ForegroundColor Yellow
} catch {
    Write-Host "`nError triggering sync:" -ForegroundColor Red
    Write-Host $_.Exception.Message -ForegroundColor Red
    
    if ($_.Exception.Response) {
        Write-Host "Status Code: $($_.Exception.Response.StatusCode)" -ForegroundColor Red
    }
}

Write-Host "`nPress Enter to continue..."
Read-Host