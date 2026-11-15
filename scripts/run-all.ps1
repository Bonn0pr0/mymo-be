$ErrorActionPreference = "Continue"

$baseDir = "D:\ASM PRM\BE\mymo-be"
$logDir = Join-Path $baseDir "logs"
if (-not (Test-Path $logDir)) {
    New-Item -ItemType Directory -Path $logDir -Force | Out-Null
}

$services = @(
    @{ Name = "Mymo.Identity.Api"; Port = 5001; Path = "src\Mymo.Identity\Mymo.Identity.Api" },
    @{ Name = "Mymo.Social.Api";   Port = 5002; Path = "src\Mymo.Social\Mymo.Social.Api" },
    @{ Name = "Mymo.Map.Api";      Port = 5003; Path = "src\Mymo.Map\Mymo.Map.Api" },
    @{ Name = "Mymo.Chat.Api";     Port = 5004; Path = "src\Mymo.Chat\Mymo.Chat.Api" },
    @{ Name = "Mymo.Business.Api"; Port = 5005; Path = "src\Mymo.Business\Mymo.Business.Api" },
    @{ Name = "Mymo.Gateway";      Port = 5079; Path = "src\Mymo.Gateway" }
)

# Kill any existing processes on these ports first
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

foreach ($svc in $services) {
    $svcDir = Join-Path $baseDir $svc.Path
    $dllPath = Join-Path $svcDir "bin\Debug\net8.0\$($svc.Name).dll"
    $stdoutLog = Join-Path $logDir "$($svc.Name).out.log"
    $stderrLog = Join-Path $logDir "$($svc.Name).err.log"

    Write-Host "Starting $($svc.Name)..."
    Start-Process -FilePath "dotnet" `
        -ArgumentList "`"$dllPath`"" `
        -WorkingDirectory $svcDir `
        -RedirectStandardOutput $stdoutLog `
        -RedirectStandardError $stderrLog `
        -WindowStyle Hidden

    Start-Sleep -Milliseconds 800
}

Write-Host "Checking ports..."
Start-Sleep -Seconds 3

foreach ($svc in $services) {
    $listening = Get-NetTCPConnection -LocalPort $svc.Port -ErrorAction SilentlyContinue
    if ($listening) {
        Write-Host " [OK] $($svc.Name) is listening on port $($svc.Port)"
    } else {
        Write-Host " [WAIT] $($svc.Name) not yet listening on port $($svc.Port)"
    }
}

Write-Host "`nGateway available at: http://localhost:5079"

while ($true) {
    Start-Sleep -Seconds 5
}
