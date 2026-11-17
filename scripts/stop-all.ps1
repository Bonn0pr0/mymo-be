# Stop all Mymo Backend Services
$ports = @(5001, 5011, 5002, 5003, 5004, 5005, 5079)

Write-Host "Stopping all Mymo Backend Services..."

foreach ($port in $ports) {
    $conns = Get-NetTCPConnection -LocalPort $port -ErrorAction SilentlyContinue
    if ($conns) {
        $pids = $conns | Select-Object -ExpandProperty OwningProcess -Unique
        foreach ($p in $pids) {
            Write-Host "Stopping process $p (Port $port)..."
            Stop-Process -Id $p -Force -ErrorAction SilentlyContinue
        }
    }
}

Write-Host "`nAll Backend Services stopped successfully."
