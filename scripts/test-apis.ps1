$ErrorActionPreference = "Stop"
$gateway = "http://localhost:5079"

Write-Host "=== Running MyMo Live API Smoke Tests against $gateway ===" -ForegroundColor Cyan

# 1. Gateway Health
$health = Invoke-RestMethod -Uri "$gateway/health" -Method Get
if (-not $health.ok) { throw "Gateway health check failed" }
Write-Host "  [PASS] GET /health -> role=$($health.role)" -ForegroundColor Green

# 2. Login as Demo User
$loginBody = @{ email = "demo@mymo.app"; password = "Demo123!" } | ConvertTo-Json
$auth = Invoke-RestMethod -Uri "$gateway/api/Auth/login" -Method Post -ContentType "application/json" -Body $loginBody
if (-not $auth.accessToken) { throw "Login did not return accessToken" }
$headers = @{ Authorization = "Bearer $($auth.accessToken)" }
Write-Host "  [PASS] POST /api/Auth/login -> User: $($auth.username) ($($auth.role))" -ForegroundColor Green

# 3. Get User Profile (/api/users/me)
$me = Invoke-RestMethod -Uri "$gateway/api/users/me" -Method Get -Headers $headers
if (-not $me.success) { throw "GET /api/users/me failed" }
Write-Host "  [PASS] GET /api/users/me -> DisplayName: $($me.data.displayName)" -ForegroundColor Green

# 4. Get Nearby Places (/api/places/nearby)
$places = Invoke-RestMethod -Uri "$gateway/api/places/nearby?latitude=10.7735&longitude=106.7034&radiusKm=10" -Method Get -Headers $headers
if (-not $places.success -or $places.data.Count -lt 1) { throw "GET /api/places/nearby returned no places" }
Write-Host "  [PASS] GET /api/places/nearby -> Found $($places.data.Count) places" -ForegroundColor Green

# 5. Get Social Feed (/api/posts/feed)
$feed = Invoke-RestMethod -Uri "$gateway/api/posts/feed" -Method Get -Headers $headers
if (-not $feed.success) { throw "GET /api/posts/feed failed" }
Write-Host "  [PASS] GET /api/posts/feed -> Found $($feed.data.Count) posts" -ForegroundColor Green

# 6. Chat with AI Hana (/api/mymy/chat)
$hanaBody = [System.Text.Encoding]::UTF8.GetBytes((@{ message = "Tìm quán cafe quanh Nguyễn Huệ" } | ConvertTo-Json))
$hana = Invoke-RestMethod -Uri "$gateway/api/mymy/chat" -Method Post -Headers $headers -ContentType "application/json; charset=utf-8" -Body $hanaBody
if (-not $hana.success) { throw "POST /api/mymy/chat failed" }
Write-Host "  [PASS] POST /api/mymy/chat -> $($hana.data.reply)" -ForegroundColor Green

# 7. Get Product Packages Catalog (/api/catalog/product-packages)
$catalog = Invoke-RestMethod -Uri "$gateway/api/catalog/product-packages" -Method Get
if (-not $catalog.success -or $catalog.data.Count -lt 1) { throw "GET /api/catalog/product-packages failed" }
Write-Host "  [PASS] GET /api/catalog/product-packages -> Found $($catalog.data.Count) packages" -ForegroundColor Green

Write-Host "=== All 7 Live API Smoke Tests PASSED! ===" -ForegroundColor Green

