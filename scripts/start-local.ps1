$root = Split-Path $PSScriptRoot -Parent
docker compose --project-directory "$root" up --build -d
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

docker compose --project-directory "$root" ps
exit $LASTEXITCODE
