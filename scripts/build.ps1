param(
    [ValidateSet('Debug', 'Release')]
    [string]$Configuration = 'Release',
    [switch]$Installer
)

$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
$frontendRoot = Join-Path $projectRoot 'src\NowSpinning.Frontend'
$livelyRoot = Join-Path $projectRoot 'lively-package'
$publishRoot = Join-Path $projectRoot 'artifacts\publish\win-x64'

Push-Location $frontendRoot
try {
    npm install
    npm run build
} finally { Pop-Location }

Get-ChildItem -LiteralPath $livelyRoot -Force | Where-Object { $_.Name -notin @('LivelyInfo.json', 'README.md', 'configure-lively.ps1') } | Remove-Item -Recurse -Force
Copy-Item -Path (Join-Path $frontendRoot 'dist\*') -Destination $livelyRoot -Recurse -Force

dotnet test (Join-Path $projectRoot 'NowSpinning.sln') -c $Configuration
dotnet publish (Join-Path $projectRoot 'src\NowSpinning.App\NowSpinning.App.csproj') `
    -c $Configuration -r win-x64 --self-contained true `
    -p:PublishSingleFile=false -p:PublishReadyToRun=true -o $publishRoot

if ($Installer) {
    $iscc = Get-Command ISCC.exe -ErrorAction SilentlyContinue
    if (-not $iscc) {
        $commonPath = 'C:\Program Files (x86)\Inno Setup 6\ISCC.exe'
        if (Test-Path -LiteralPath $commonPath) { $iscc = Get-Item $commonPath }
    }
    if (-not $iscc) {
        $userPath = Join-Path $env:LOCALAPPDATA 'Programs\Inno Setup 6\ISCC.exe'
        if (Test-Path -LiteralPath $userPath) { $iscc = Get-Item $userPath }
    }
    if (-not $iscc) { throw 'Inno Setup 6 was not found. Install it, then run this script again with -Installer.' }
    & $iscc (Join-Path $projectRoot 'installer\NowSpinning.iss')
}
