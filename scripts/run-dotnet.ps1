$root = Split-Path $PSScriptRoot -Parent
$env:TEMP = "D:\prn asm\.temp"
$env:TMP = "D:\prn asm\.temp"
$env:Messaging__RabbitMq = ""
$env:Messaging__Kafka = ""

$services = @(
    @{ Name = "Identity"; Dir = "$root\src\Mymo.Identity\Mymo.Identity.Api"; Dll = "bin\Debug\net8.0\Mymo.Identity.Api.dll"; Port = 5001; Db = "mymo_identity.db" },
    @{ Name = "Social";   Dir = "$root\src\Mymo.Social\Mymo.Social.Api";     Dll = "bin\Debug\net8.0\Mymo.Social.Api.dll";     Port = 5002; Db = "mymo_social.db" },
    @{ Name = "Map";      Dir = "$root\src\Mymo.Map\Mymo.Map.Api";           Dll = "bin\Debug\net8.0\Mymo.Map.Api.dll";         Port = 5003; Db = "mymo_map.db" },
    @{ Name = "Chat";     Dir = "$root\src\Mymo.Chat\Mymo.Chat.Api";         Dll = "bin\Debug\net8.0\Mymo.Chat.Api.dll";       Port = 5004; Db = "mymo_chat.db" },
    @{ Name = "Business"; Dir = "$root\src\Mymo.Business\Mymo.Business.Api"; Dll = "bin\Debug\net8.0\Mymo.Business.Api.dll";   Port = 5005; Db = "mymo_business.db" },
    @{ Name = "Gateway";  Dir = "$root\src\Mymo.Gateway";                    Dll = "bin\Debug\net8.0\Mymo.Gateway.dll";         Port = 5079; Db = "" }
)

Get-Process dotnet -ErrorAction SilentlyContinue | Stop-Process -Force -ErrorAction SilentlyContinue
Start-Sleep -Seconds 1

$procs = @()
foreach ($svc in $services) {
    Write-Host "Starting $($svc.Name) on port $($svc.Port)..." -ForegroundColor Cyan
    if ($svc.Db -ne "") {
        $dbPath = Join-Path "$root\data" $svc.Db
        $env:ConnectionStrings__Database = "Data Source=$dbPath"
    } else {
        Remove-Item Env:\ConnectionStrings__Database -ErrorAction SilentlyContinue
    }
    $p = Start-Process -FilePath "dotnet" -ArgumentList "`"$($svc.Dll)`"" -WorkingDirectory $svc.Dir -WindowStyle Hidden -PassThru
    $procs += $p
}

Start-Sleep -Seconds 3
foreach ($svc in $services) {
    try {
        $res = Invoke-RestMethod -Uri "http://localhost:$($svc.Port)/health" -TimeoutSec 3
        Write-Host "[OK] $($svc.Name) (http://localhost:$($svc.Port)/health) -> $($res | ConvertTo-Json -Compress)" -ForegroundColor Green
    } catch {
        Write-Host "[WAIT] $($svc.Name) on port $($svc.Port): $($_.Exception.Message)" -ForegroundColor Yellow
    }
}

Write-Host "Swagger UI Hub: http://localhost:5079/swagger" -ForegroundColor Green
Wait-Process -Id ($procs | Select-Object -ExpandProperty Id)
