param(
  [string]$ProjectFile = "AudioSFV.csproj",
  [string]$AppName = "AudioSFV",
  [string]$AppVersion = "1.3.0",
  [string]$Framework = "net10.0",
  [string]$Configuration = "Release",
  [string]$Rid,
  [string]$OutputDir = "dist"
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

# Resolve repo root and switch to it
$root = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
Set-Location $root

# Determine RID if not provided
if (-not $Rid) {
  switch ($env:PROCESSOR_ARCHITECTURE) {
    'ARM64' { $Rid = 'win-arm64' }
    default { $Rid = 'win-x64' }
  }
}

Write-Host "Building $AppName for $Rid ($Configuration)..."
dotnet publish $ProjectFile -c $Configuration -r $Rid `
  --self-contained -p:DebugSymbols=false

$publishDir = Join-Path -Path "bin/$Configuration/$Framework/$Rid/publish" -ChildPath ''
if (-not (Test-Path $publishDir)) {
  throw "Publish directory not found: $publishDir"
}

# Prepare output
New-Item -ItemType Directory -Force -Path $OutputDir | Out-Null

$archSuffix = $Rid.Split('-')[-1]
$zipPath = Join-Path $OutputDir "$AppName-$AppVersion-Windows-$archSuffix.zip"
if (Test-Path $zipPath) { Remove-Item $zipPath -Force }
Write-Host "Creating archive"
Compress-Archive -Path (Join-Path $publishDir '*') -DestinationPath $zipPath -Force
