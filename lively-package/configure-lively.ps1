$ErrorActionPreference = 'Stop'
$tokenPath = Join-Path $env:LOCALAPPDATA 'NowSpinning\browser-token.txt'
if (-not (Test-Path -LiteralPath $tokenPath)) { throw 'Run the NowSpinning companion once before configuring the Lively package.' }
$token = (Get-Content -Raw -LiteralPath $tokenPath).Trim()
if ($token -notmatch '^[a-f0-9]{64}$') { throw 'The local companion token is invalid.' }
$metadataPath = Join-Path $PSScriptRoot 'LivelyInfo.json'
$metadata = Get-Content -Raw -LiteralPath $metadataPath | ConvertFrom-Json
$metadata.FileName = "index.html?token=$token"
$metadata | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath $metadataPath -Encoding UTF8
Write-Host 'LivelyInfo.json configured for this Windows account. Import the lively-package folder into Lively.'
