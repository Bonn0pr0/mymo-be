$root = Split-Path $PSScriptRoot -Parent
$projects = @(
  "src\Mymo.Identity\Mymo.Identity.csproj",
  "src\Mymo.Social\Mymo.Social.csproj",
  "src\Mymo.Map\Mymo.Map.csproj",
  "src\Mymo.Chat\Mymo.Chat.csproj",
  "src\Mymo.Business\Mymo.Business.csproj",
  "src\Mymo.Gateway\Mymo.Gateway.csproj"
)
foreach ($project in $projects) {
  Start-Process -FilePath "dotnet" -ArgumentList "run --project `"$project`" --no-launch-profile" -WorkingDirectory $root -WindowStyle Hidden
  Start-Sleep -Seconds 1
}
Write-Output "Gateway: http://localhost:5079"
