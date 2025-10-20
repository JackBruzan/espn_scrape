# PowerShell script to run schedule sync and pull week 7 stats
# This script will start the ESPN Scrape application and trigger the necessary sync operations

param(
    [int]$Season = 2025,
    [int]$Week = 7,
    [int]$SeasonType = 2,  # 2 = Regular Season, 3 = Playoffs
    [string]$BaseUrl = "http://localhost:60772",
    [switch]$UseHttp,  # Use HTTP instead of HTTPS
    [switch]$WaitForApp = $true,  # Wait for app to start before making API calls
    [int]$TimeoutSeconds = 300  # 5 minute timeout for operations
)

# Use HTTP if specified
if ($UseHttp) {
    $BaseUrl = "http://localhost:60772"
}

# Function to check if the application is running
function Test-AppRunning {
    param([string]$Url)
    
    try {
        # Ignore SSL certificate errors for localhost
        if ($Url -like "https://*") {
            [System.Net.ServicePointManager]::ServerCertificateValidationCallback = { $true }
        }
        
        # Try the simpler diagnostics health endpoint first
        $response = Invoke-WebRequest -Uri "$Url/api/Diagnostics/health" -Method GET -TimeoutSec 10 -UseBasicParsing
        return $response.StatusCode -eq 200
    }
    catch [System.Net.WebException] {
        # If health endpoint fails, try the sync status endpoint
        try {
            $response2 = Invoke-WebRequest -Uri "$Url/api/EspnIntegration/sync/status" -Method GET -TimeoutSec 5 -UseBasicParsing
            # Accept both 200 and 500 status codes - if we get a response, the app is running
            return ($response2.StatusCode -eq 200) -or ($response2.StatusCode -eq 500)
        }
        catch {
            return $false
        }
    }
    catch {
        # For any other exception, assume app is not running
        return $false
    }
}

# Function to wait for application to start
function Wait-ForApp {
    param([string]$Url, [int]$MaxWaitSeconds = 60)
    
    Write-Host "Waiting for application to start..." -ForegroundColor Yellow
    $elapsed = 0
    
    while ($elapsed -lt $MaxWaitSeconds) {
        if (Test-AppRunning -Url $Url) {
            Write-Host "Application is running!" -ForegroundColor Green
            return $true
        }
        
        Start-Sleep -Seconds 2
        $elapsed += 2
        Write-Host "." -NoNewline
    }
    
    Write-Host ""
    Write-Host "Application failed to start within $MaxWaitSeconds seconds" -ForegroundColor Red
    return $false
}

# Function to make API request with error handling
function Invoke-ApiRequest {
    param(
        [string]$Url,
        [string]$Method = "GET",
        [object]$Body = $null,
        [int]$TimeoutSec = 300
    )
    
    try {
        # Ignore SSL certificate errors for localhost
        if ($Url -like "https://*") {
            [System.Net.ServicePointManager]::ServerCertificateValidationCallback = { $true }
        }
        
        $headers = @{
            "Content-Type" = "application/json"
            "Accept" = "application/json"
        }
        
        $params = @{
            Uri = $Url
            Method = $Method
            Headers = $headers
            TimeoutSec = $TimeoutSec
            UseBasicParsing = $true
        }
        
        if ($Body -and $Method -ne "GET") {
            $params.Body = $Body | ConvertTo-Json -Depth 10
        }
        
        $response = Invoke-WebRequest @params
        
        if ($response.StatusCode -ge 200 -and $response.StatusCode -lt 300) {
            return @{
                Success = $true
                StatusCode = $response.StatusCode
                Content = $response.Content | ConvertFrom-Json
            }
        } else {
            return @{
                Success = $false
                StatusCode = $response.StatusCode
                Error = "HTTP $($response.StatusCode): $($response.StatusDescription)"
            }
        }
    }
    catch {
        return @{
            Success = $false
            Error = $_.Exception.Message
        }
    }
}

# Function to watch sync status
function Watch-SyncStatus {
    param([string]$BaseUrl, [int]$TimeoutSeconds = 300)
    
    $startTime = Get-Date
    $timeout = (Get-Date).AddSeconds($TimeoutSeconds)
    
    Write-Host "Monitoring sync status..." -ForegroundColor Yellow
    
    do {
        $statusResult = Invoke-ApiRequest -Url "$BaseUrl/api/EspnIntegration/sync/status"
        
        if ($statusResult.Success) {
            $status = $statusResult.Content
            
            if ($status.IsRunning) {
                $elapsed = (Get-Date) - $startTime
                Write-Host "Sync in progress... (Elapsed: $($elapsed.ToString('mm\:ss')))" -ForegroundColor Yellow
                Start-Sleep -Seconds 10
            } else {
                Write-Host "Sync completed!" -ForegroundColor Green
                return $true
            }
        } else {
            Write-Host "Failed to check sync status: $($statusResult.Error)" -ForegroundColor Red
            Start-Sleep -Seconds 5
        }
        
    } while ((Get-Date) -lt $timeout)
    
    Write-Host "Sync monitoring timed out after $TimeoutSeconds seconds" -ForegroundColor Red
    return $false
}

# Main script execution
Write-Host "=== ESPN Scrape Week $Week Sync Script ===" -ForegroundColor Cyan
Write-Host "Season: $Season" -ForegroundColor White
Write-Host "Week: $Week" -ForegroundColor White
Write-Host "Season Type: $SeasonType" -ForegroundColor White
Write-Host "Base URL: $BaseUrl" -ForegroundColor White
Write-Host ""
Write-Host "Attempting to trigger sync operations..." -ForegroundColor Yellow
Write-Host ""

# Step 1: Trigger Schedule Sync
Write-Host "Step 1: Triggering Schedule Sync..." -ForegroundColor Cyan
$scheduleRequest = @{
    Year = $Season
    Week = $Week
    SeasonType = $SeasonType
}

$scheduleResult = Invoke-ApiRequest -Url "$BaseUrl/api/EspnIntegration/sync/schedule" -Method "POST" -Body $scheduleRequest -TimeoutSec $TimeoutSeconds

if ($scheduleResult.Success) {
    Write-Host "✅ Schedule sync triggered successfully!" -ForegroundColor Green
    Write-Host "Job Key: $($scheduleResult.Content.JobKey)" -ForegroundColor White
    Write-Host "Message: $($scheduleResult.Content.Message)" -ForegroundColor White
    Write-Host ""
    
    # Wait a bit for schedule sync to complete before starting stats sync
    Write-Host "Waiting for schedule sync to process..." -ForegroundColor Yellow
    Start-Sleep -Seconds 30
} else {
    Write-Host "❌ Failed to trigger schedule sync!" -ForegroundColor Red
    Write-Host "Error: $($scheduleResult.Error)" -ForegroundColor Red
    Write-Host ""
}

# Step 2: Trigger Stats Sync for Week 7
Write-Host "Step 2: Triggering Stats Sync for Week $Week..." -ForegroundColor Cyan
$statsRequest = @{
    Season = $Season
    Week = $Week
    Options = @{
        ForceFullSync = $false
        SkipInactives = $true
        BatchSize = 200
        DryRun = $false
        MaxRetries = 3
        RetryDelayMs = 1000
        TimeoutMinutes = 60
        EnableDetailedLogging = $true
        ValidateData = $true
        SkipInvalidRecords = $true
        CreateBackup = $false
    }
}

$statsResult = Invoke-ApiRequest -Url "$BaseUrl/api/EspnIntegration/sync/stats" -Method "POST" -Body $statsRequest -TimeoutSec $TimeoutSeconds

if ($statsResult.Success) {
    Write-Host "✅ Stats sync triggered successfully!" -ForegroundColor Green
    Write-Host "Sync ID: $($statsResult.Content.SyncId)" -ForegroundColor White
    Write-Host "Status: $($statsResult.Content.Status)" -ForegroundColor White
    Write-Host ""
    
    # Monitor the sync progress
    $syncCompleted = Watch-SyncStatus -BaseUrl $BaseUrl -TimeoutSeconds $TimeoutSeconds
    
    if ($syncCompleted) {
        # Get final status
        $finalStatusResult = Invoke-ApiRequest -Url "$BaseUrl/api/EspnIntegration/sync/status"
        
        if ($finalStatusResult.Success) {
            $finalStatus = $finalStatusResult.Content
            
            Write-Host "=== Final Sync Report ===" -ForegroundColor Cyan
            Write-Host "Running: $($finalStatus.IsRunning)" -ForegroundColor White
            
            if ($finalStatus.LastSyncReport) {
                $report = $finalStatus.LastSyncReport.Result
                Write-Host "Records Processed: $($report.RecordsProcessed)" -ForegroundColor White
                Write-Host "Players Processed: $($report.PlayersProcessed)" -ForegroundColor White
                Write-Host "Players Updated: $($report.PlayersUpdated)" -ForegroundColor White
                Write-Host "New Players Added: $($report.NewPlayersAdded)" -ForegroundColor White
                Write-Host "Stats Records Processed: $($report.StatsRecordsProcessed)" -ForegroundColor White
                Write-Host "New Stats Added: $($report.NewStatsAdded)" -ForegroundColor White
                Write-Host "Stats Updated: $($report.StatsUpdated)" -ForegroundColor White
                Write-Host "Success Rate: $($report.SuccessRate.ToString('F1'))%" -ForegroundColor White
                Write-Host "Duration: $($report.Duration)" -ForegroundColor White
                
                if ($report.Errors.Count -gt 0) {
                    Write-Host "Errors:" -ForegroundColor Red
                    foreach ($errorMsg in $report.Errors) {
                        Write-Host "  - $errorMsg" -ForegroundColor Red
                    }
                }
                
                if ($report.Warnings.Count -gt 0) {
                    Write-Host "Warnings:" -ForegroundColor Yellow
                    foreach ($warning in $report.Warnings) {
                        Write-Host "  - $warning" -ForegroundColor Yellow
                    }
                }
            }
        }
    }
} else {
    Write-Host "❌ Failed to trigger stats sync!" -ForegroundColor Red
    Write-Host "Error: $($statsResult.Error)" -ForegroundColor Red
}

# Step 3: Get final sync status and job information
Write-Host ""
Write-Host "Step 3: Getting final sync status and job information..." -ForegroundColor Cyan

$jobsResult = Invoke-ApiRequest -Url "$BaseUrl/api/EspnIntegration/jobs"

if ($jobsResult.Success) {
    Write-Host "=== Scheduled Jobs Status ===" -ForegroundColor Cyan
    foreach ($job in $jobsResult.Content) {
        Write-Host "Job: $($job.JobName)" -ForegroundColor White
        Write-Host "  Type: $($job.JobType)" -ForegroundColor Gray
        Write-Host "  Description: $($job.Description)" -ForegroundColor Gray
        
        if ($job.Triggers.Count -gt 0) {
            foreach ($trigger in $job.Triggers) {
                Write-Host "  Next Fire: $($trigger.NextFireTime)" -ForegroundColor Gray
                Write-Host "  Previous Fire: $($trigger.PreviousFireTime)" -ForegroundColor Gray
                Write-Host "  State: $($trigger.TriggerState)" -ForegroundColor Gray
            }
        }
        Write-Host ""
    }
}

Write-Host "=== Script Completed ===" -ForegroundColor Cyan
Write-Host "Schedule sync and Week $Week stats sync have been triggered." -ForegroundColor Green
Write-Host "Check the application logs for detailed progress information." -ForegroundColor Yellow
Write-Host ""
Write-Host "You can also check the sync status anytime by visiting:" -ForegroundColor White
Write-Host "$BaseUrl/api/EspnIntegration/sync/status" -ForegroundColor Blue