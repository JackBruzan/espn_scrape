# ESPN API Service - Windows Deployment Guide

This guide covers deploying the ESPN API Service on Windows environments using IIS, Windows Services, or standalone execution.

## Prerequisites

- Windows 10/11 or Windows Server 2019+
- .NET 8.0 Runtime or SDK
- PowerShell 5.1+ or PowerShell Core 7+
- Optional: IIS with ASP.NET Core Hosting Bundle

## Quick Start

### 1. Build and Run Locally

```powershell
# Build the application
dotnet build -c Release

# Run the application
dotnet run

# Or run the published executable
dotnet .\bin\Release\net8.0\ESPNScrape.dll
```

The service will be available at:
- HTTP: `http://localhost:5000`
- HTTPS: `https://localhost:5001`

### 2. Publish for Deployment

```powershell
# Publish self-contained deployment
dotnet publish -c Release -r win-x64 --self-contained

# Publish framework-dependent deployment (requires .NET runtime on target)
dotnet publish -c Release
```

## Deployment Options

### Option 1: IIS Deployment

#### Prerequisites
- Install IIS with ASP.NET Core Module
- Download and install [ASP.NET Core Hosting Bundle](https://dotnet.microsoft.com/download/dotnet/8.0)

#### Steps
1. **Publish the application:**
   ```powershell
   dotnet publish -c Release -o "C:\inetpub\wwwroot\espn-api"
   ```

2. **Create IIS Application:**
   - Open IIS Manager
   - Right-click "Default Web Site" → "Add Application"
   - Alias: `espn-api`
   - Physical path: `C:\inetpub\wwwroot\espn-api`
   - Application pool: Create new pool targeting ".NET CLR Version: No Managed Code"

3. **Configure Application Pool:**
   - Set Identity to `ApplicationPoolIdentity`
   - Set "Load User Profile" to `True`
   - Set "Idle Time-out" to `0` (for continuous running)

4. **Access the service:**
   - `http://localhost/espn-api/health`
   - `http://localhost/espn-api/metrics`

### Option 2: Windows Service

#### Create Windows Service using SC command

1. **Publish the application:**
   ```powershell
   dotnet publish -c Release -r win-x64 --self-contained -o "C:\Services\ESPNApi"
   ```

2. **Create the service:**
   ```powershell
   sc create "ESPN API Service" binPath="C:\Services\ESPNApi\ESPNScrape.exe" start=auto
   sc description "ESPN API Service" "ESPN API monitoring and diagnostic service"
   ```

3. **Start the service:**
   ```powershell
   sc start "ESPN API Service"
   ```

4. **Check service status:**
   ```powershell
   sc query "ESPN API Service"
   ```

#### Using PowerShell Service Management

```powershell
# Install as Windows Service (requires administrative privileges)
New-Service -Name "ESPNApiService" -BinaryPathName "C:\Services\ESPNApi\ESPNScrape.exe" -DisplayName "ESPN API Service" -StartupType Automatic

# Start the service
Start-Service -Name "ESPNApiService"

# Check service status
Get-Service -Name "ESPNApiService"

# Stop the service
Stop-Service -Name "ESPNApiService"

# Remove the service
Remove-Service -Name "ESPNApiService"
```

### Option 3: Standalone Console Application

#### Run as Background Task

```powershell
# Start the application in background
Start-Process -FilePath "dotnet" -ArgumentList ".\bin\Release\net8.0\ESPNScrape.dll" -WindowStyle Hidden

# Or run directly
.\bin\Release\net8.0\ESPNScrape.exe
```

#### Using Task Scheduler

1. Open Task Scheduler (`taskschd.msc`)
2. Create Basic Task:
   - Name: "ESPN API Service"
   - Trigger: "When the computer starts"
   - Action: "Start a program"
   - Program: `C:\Path\To\ESPNScrape.exe`
3. Configure for "Run whether user is logged on or not"

## Configuration

### Environment Variables

Set environment variables for different deployment scenarios:

```powershell
# Set environment for current session
$env:ASPNETCORE_ENVIRONMENT = "Production"
$env:ASPNETCORE_URLS = "http://+:5000;https://+:5001"

# Set permanent environment variables
[Environment]::SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", "Production", "Machine")
[Environment]::SetEnvironmentVariable("ASPNETCORE_URLS", "http://+:5000;https://+:5001", "Machine")
```

### Configuration Files

#### appsettings.Production.json
```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Warning",
      "Microsoft.AspNetCore": "Warning",
      "ESPNScrape": "Information"
    }
  },
  "AllowedHosts": "*",
  "Kestrel": {
    "Endpoints": {
      "Http": {
        "Url": "http://+:5000"
      },
      "Https": {
        "Url": "https://+:5001"
      }
    }
  },
  "EspnApi": {
    "BaseUrl": "https://site.api.espn.com/apis/site/v2/sports/football/nfl",
    "RateLimit": 100,
    "CacheTtlMinutes": 30
  }
}
```

## Monitoring and Health Checks

### PowerShell Health Check Script

Create `health-check.ps1`:

```powershell
# ESPN API Service Health Check
param(
    [string]$ServiceUrl = "http://localhost:5000",
    [int]$TimeoutSeconds = 30
)

function Test-ServiceHealth {
    param([string]$Url)
    
    try {
        $response = Invoke-RestMethod -Uri "$Url/health" -TimeoutSec $TimeoutSeconds -ErrorAction Stop
        
        if ($response.status -eq "Healthy") {
            Write-Host "✓ Service is healthy" -ForegroundColor Green
            return $true
        } else {
            Write-Host "✗ Service is unhealthy: $($response.status)" -ForegroundColor Red
            return $false
        }
    }
    catch {
        Write-Host "✗ Health check failed: $($_.Exception.Message)" -ForegroundColor Red
        return $false
    }
}

function Test-ServiceEndpoints {
    param([string]$BaseUrl)
    
    $endpoints = @("/health", "/metrics", "/diagnostic/system-info")
    
    foreach ($endpoint in $endpoints) {
        try {
            $null = Invoke-RestMethod -Uri "$BaseUrl$endpoint" -TimeoutSec 10 -ErrorAction Stop
            Write-Host "✓ $endpoint - OK" -ForegroundColor Green
        }
        catch {
            Write-Host "✗ $endpoint - Failed" -ForegroundColor Red
        }
    }
}

# Main health check
Write-Host "ESPN API Service Health Check" -ForegroundColor Cyan
Write-Host "Service URL: $ServiceUrl" -ForegroundColor Yellow

if (Test-ServiceHealth -Url $ServiceUrl) {
    Write-Host "`nTesting individual endpoints:" -ForegroundColor Cyan
    Test-ServiceEndpoints -BaseUrl $ServiceUrl
    exit 0
} else {
    exit 1
}
```

### Windows Event Log Integration

The service can log to Windows Event Log. To view logs:

```powershell
# View application logs
Get-WinEvent -LogName Application | Where-Object {$_.ProviderName -eq "ESPNScrape"} | Select-Object -First 20

# View system logs related to the service
Get-WinEvent -LogName System | Where-Object {$_.LevelDisplayName -eq "Error"} | Select-Object -First 10
```

## Performance Monitoring

### Performance Counters

Monitor the service using Windows Performance Toolkit:

```powershell
# Monitor CPU and memory usage
Get-Counter "\Process(ESPNScrape)\% Processor Time", "\Process(ESPNScrape)\Working Set"

# Monitor ASP.NET Core counters
Get-Counter "\ASP.NET Core Apps\Requests Per Second", "\ASP.NET Core Apps\Current Requests"
```

### Task Manager Monitoring

1. Open Task Manager (`taskmgr.exe`)
2. Go to "Details" tab
3. Find `ESPNScrape.exe` process
4. Monitor CPU, Memory, and Network usage

## Troubleshooting

### Common Issues

#### Service Won't Start
```powershell
# Check Windows Event Log
Get-WinEvent -LogName Application -MaxEvents 10 | Where-Object {$_.LevelDisplayName -eq "Error"}

# Verify .NET runtime
dotnet --info

# Test manual startup
dotnet .\ESPNScrape.dll
```

#### Port Already in Use
```powershell
# Find process using port 5000
netstat -ano | findstr :5000

# Kill process (replace PID with actual process ID)
taskkill /PID <PID> /F
```

#### Access Denied Errors
```powershell
# Run as administrator or check file permissions
icacls "C:\Path\To\ESPNScrape" /grant "IIS_IUSRS:(OI)(CI)F"
```

### Log Locations

- **Application Logs**: `.\logs\` directory
- **IIS Logs**: `%SystemDrive%\inetpub\logs\LogFiles\W3SVC1\`
- **Windows Event Log**: Application and System logs
- **Service Logs**: Check Windows Service event logs

## Security Considerations

### Firewall Configuration

```powershell
# Allow inbound connections on port 5000
New-NetFirewallRule -DisplayName "ESPN API HTTP" -Direction Inbound -Protocol TCP -LocalPort 5000 -Action Allow

# Allow inbound connections on port 5001 (HTTPS)
New-NetFirewallRule -DisplayName "ESPN API HTTPS" -Direction Inbound -Protocol TCP -LocalPort 5001 -Action Allow
```

### User Account Configuration

For Windows Service deployment:
- Run under `Local Service` or dedicated service account
- Grant necessary permissions to log directory
- Ensure network access for ESPN API calls

## Backup and Recovery

### Application Backup

```powershell
# Backup application files
$backupPath = "C:\Backups\ESPNApi\$(Get-Date -Format 'yyyyMMdd_HHmmss')"
New-Item -Path $backupPath -ItemType Directory -Force
Copy-Item -Path "C:\Services\ESPNApi\*" -Destination $backupPath -Recurse

# Backup configuration
Copy-Item -Path ".\appsettings*.json" -Destination $backupPath

# Backup logs
Copy-Item -Path ".\logs\*" -Destination "$backupPath\logs" -Recurse
```

### Recovery Process

```powershell
# Stop the service
Stop-Service -Name "ESPNApiService" -ErrorAction SilentlyContinue

# Restore application files
Copy-Item -Path "$backupPath\*" -Destination "C:\Services\ESPNApi\" -Recurse -Force

# Start the service
Start-Service -Name "ESPNApiService"

# Verify service health
.\health-check.ps1
```

## Automated Deployment Script

Create `deploy.ps1`:

```powershell
# ESPN API Service Deployment Script
param(
    [string]$DeploymentPath = "C:\Services\ESPNApi",
    [string]$ServiceName = "ESPNApiService",
    [switch]$CreateService,
    [switch]$UpdateOnly
)

function Deploy-Application {
    Write-Host "Deploying ESPN API Service..." -ForegroundColor Cyan
    
    # Stop service if running
    if (Get-Service -Name $ServiceName -ErrorAction SilentlyContinue) {
        Write-Host "Stopping existing service..." -ForegroundColor Yellow
        Stop-Service -Name $ServiceName -Force
    }
    
    # Create deployment directory
    if (-not (Test-Path $DeploymentPath)) {
        New-Item -Path $DeploymentPath -ItemType Directory -Force
        Write-Host "Created deployment directory: $DeploymentPath" -ForegroundColor Green
    }
    
    # Publish application
    Write-Host "Publishing application..." -ForegroundColor Yellow
    dotnet publish -c Release -r win-x64 --self-contained -o $DeploymentPath
    
    if ($LASTEXITCODE -eq 0) {
        Write-Host "Application published successfully" -ForegroundColor Green
    } else {
        Write-Host "Publication failed" -ForegroundColor Red
        exit 1
    }
    
    # Create Windows Service
    if ($CreateService -and -not (Get-Service -Name $ServiceName -ErrorAction SilentlyContinue)) {
        Write-Host "Creating Windows Service..." -ForegroundColor Yellow
        New-Service -Name $ServiceName -BinaryPathName "$DeploymentPath\ESPNScrape.exe" -DisplayName "ESPN API Service" -StartupType Automatic
        Write-Host "Windows Service created" -ForegroundColor Green
    }
    
    # Start service
    Write-Host "Starting service..." -ForegroundColor Yellow
    Start-Service -Name $ServiceName
    
    # Wait for startup
    Start-Sleep -Seconds 5
    
    # Verify deployment
    if (Test-NetConnection -ComputerName localhost -Port 5000 -WarningAction SilentlyContinue) {
        Write-Host "✓ Deployment successful - Service is running on port 5000" -ForegroundColor Green
    } else {
        Write-Host "✗ Deployment may have issues - Service not responding on port 5000" -ForegroundColor Red
    }
}

# Execute deployment
Deploy-Application
```

## Usage Examples

### Development Environment
```powershell
# Quick start for development
dotnet run
```

### Production Deployment
```powershell
# Deploy as Windows Service
.\deploy.ps1 -CreateService

# Check service status
Get-Service -Name "ESPNApiService"

# View logs
Get-Content .\logs\espn-scrape-$(Get-Date -Format 'yyyyMMdd').txt -Tail 50
```

### Health Monitoring
```powershell
# Manual health check
.\health-check.ps1

# Automated monitoring with Task Scheduler
# Schedule health-check.ps1 to run every 5 minutes
```

This deployment guide provides Windows-native solutions without unnecessary complexity, focusing on the tools and methods that work best in your .NET Windows environment.