$ErrorActionPreference = "Continue"

$baseDir = Split-Path $PSScriptRoot -Parent
$logDir = Join-Path $baseDir "logs"
if (-not (Test-Path $logDir)) {
    New-Item -ItemType Directory -Path $logDir -Force | Out-Null
}

# Load environment variables from .env if present
$envFile = Join-Path $baseDir ".env"
if (Test-Path $envFile) {
    Get-Content $envFile | Where-Object { $_ -match '^\s*([^#=]+)\s*=\s*(.*)$' } | ForEach-Object {
        $k = $matches[1].Trim()
        $v = $matches[2].Trim()
        [System.Environment]::SetEnvironmentVariable($k, $v, "Process")
    }
}

$services = @(
    @{ Name = "Mymo.Identity.Api"; Port = 5001; Path = "src\Mymo.Identity\Mymo.Identity.Api" },
    @{ Name = "Mymo.Social.Api";   Port = 5002; Path = "src\Mymo.Social\Mymo.Social.Api" },
    @{ Name = "Mymo.Map.Api";      Port = 5003; Path = "src\Mymo.Map\Mymo.Map.Api" },
    @{ Name = "Mymo.Chat.Api";     Port = 5004; Path = "src\Mymo.Chat\Mymo.Chat.Api" },
    @{ Name = "Mymo.Business.Api"; Port = 5005; Path = "src\Mymo.Business\Mymo.Business.Api" },
    @{ Name = "Mymo.Gateway";      Port = 5079; Path = "src\Mymo.Gateway" }
)

# 1. Kill any existing processes on these ports first
foreach ($svc in $services) {
    $conns = Get-NetTCPConnection -LocalPort $svc.Port -ErrorAction SilentlyContinue
    if ($conns) {
        $pids = $conns | Select-Object -ExpandProperty OwningProcess -Unique
        foreach ($p in $pids) {
            Stop-Process -Id $p -Force -ErrorAction SilentlyContinue
        }
    }
}

Start-Sleep -Seconds 1

# 2. Check if build is needed
$firstDll = Join-Path $baseDir "src\Mymo.Gateway\bin\Debug\net8.0\Mymo.Gateway.dll"
if (-not (Test-Path $firstDll)) {
    Write-Host "Building Mymo solution..."
    dotnet build (Join-Path $baseDir "Mymo.slnx")
    if ($LASTEXITCODE -ne 0) {
        Write-Host "Build failed! Please fix errors and try again."
        exit $LASTEXITCODE
    }
}

# 3. Start each service
foreach ($svc in $services) {
    $svcDir = Join-Path $baseDir $svc.Path
    $dllPath = Join-Path $svcDir "bin\Debug\net8.0\$($svc.Name).dll"
    $stdoutLog = Join-Path $logDir "$($svc.Name).out.log"
    $stderrLog = Join-Path $logDir "$($svc.Name).err.log"

    Write-Host "Starting $($svc.Name) on port $($svc.Port)..."
    Start-Process -FilePath "dotnet" `
        -ArgumentList "`"$dllPath`"" `
        -WorkingDirectory $svcDir `
        -RedirectStandardOutput $stdoutLog `
        -RedirectStandardError $stderrLog `
        -WindowStyle Hidden

    Start-Sleep -Milliseconds 800
}

Write-Host "`nWaiting for services to initialize..."
Start-Sleep -Seconds 4

foreach ($svc in $services) {
    $listening = Get-NetTCPConnection -LocalPort $svc.Port -ErrorAction SilentlyContinue
    if ($listening) {
        Write-Host "  [OK] $($svc.Name) is running on port $($svc.Port)"
    } else {
        Write-Host "  [WAIT] $($svc.Name) initializing on port $($svc.Port)..."
    }
}

Write-Host "`n============================================================"
Write-Host "All Backend Services are UP!"
Write-Host "API Gateway (Centralized): http://localhost:5079"
Write-Host "Centralized Swagger Hub:   http://localhost:5079/swagger"
Write-Host "Press Ctrl+C to close this terminal (services stay running)."
Write-Host "To STOP all services, run: .\scripts\stop-all.ps1"
Write-Host "============================================================"

# Keep window open if run via double click or separate window
while ($true) {
    Start-Sleep -Seconds 5
}
