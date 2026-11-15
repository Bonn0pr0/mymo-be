$root = Split-Path $PSScriptRoot -Parent
Write-Output "Starting RabbitMQ + Kafka + Postgres..."
docker compose -f "$root\docker-compose.yml" up -d
Start-Sleep -Seconds 3

$projects = @(
  "src\Mymo.Identity\Mymo.Identity.Api\Mymo.Identity.Api.csproj",
  "src\Mymo.Social\Mymo.Social.Api\Mymo.Social.Api.csproj",
  "src\Mymo.Map\Mymo.Map.Api\Mymo.Map.Api.csproj",
  "src\Mymo.Chat\Mymo.Chat.Api\Mymo.Chat.Api.csproj",
  "src\Mymo.Business\Mymo.Business.Api\Mymo.Business.Api.csproj",
  "src\Mymo.Gateway\Mymo.Gateway.csproj"
)
foreach ($project in $projects) {
  Start-Process -FilePath "dotnet" -ArgumentList "run --project `"$project`" --no-launch-profile" -WorkingDirectory $root -WindowStyle Hidden
  Start-Sleep -Seconds 1
}
Write-Output "Gateway: http://localhost:5079"
Write-Output "RabbitMQ UI: http://localhost:15672 (guest/guest)"
